package main

import (
	"context"
	"encoding/base64"
	"encoding/hex"
	"encoding/json"
	"net"
	"net/netip"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"testing"
	"time"

	"github.com/metacubex/mipstack"
	"github.com/metacubex/wireguard-go/conn"
	"github.com/metacubex/wireguard-go/device"
	"github.com/metacubex/wireguard-go/tun"
)

func probeChild(t *testing.T, outbound map[string]any, directory string) (time.Duration, error) {
	t.Helper()
	data, err := json.Marshal(outbound)
	if err != nil {
		t.Fatal(err)
	}
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	ctx, cancel := context.WithTimeout(context.Background(), 12*time.Second)
	defer cancel()
	child := exec.CommandContext(ctx, exe, "--probe")
	child.Env = append(os.Environ(), "ROVUNET_AWG_TEST_CHILD=1", "ROVUNET_CERTIFICATE_STORE="+directory)
	child.Stdin = strings.NewReader(string(data))
	started := time.Now()
	output, err := child.CombinedOutput()
	if err != nil {
		t.Logf("Synthetic probe response: %s", output)
	}
	if err == nil {
		var result struct {
			Kind      string
			LatencyMs int64
		}
		if json.Unmarshal(output, &result) != nil || result.Kind != "protocol-handshake" || result.LatencyMs < 0 {
			t.Fatalf("Invalid handshake evidence: %s", output)
		}
	}
	return time.Since(started), err
}

func TestWireGuardProbeRequiresTheRealPeerWithoutAnyInternetDestination(t *testing.T) {
	serverPrivate, serverPublic := keyPair(t)
	clientPrivate, clientPublic := keyPair(t)
	stack, err := mipstack.New(mipstack.Config{LocalAddresses: []netip.Prefix{netip.MustParsePrefix("10.77.0.1/32")}, MTU: 1280})
	if err != nil {
		t.Fatal(err)
	}
	if err = stack.Start(); err != nil {
		t.Fatal(err)
	}
	ft := &fixtureTun{Stack: stack, events: make(chan tun.Event, 1)}
	ft.events <- tun.EventUp
	server := device.NewDevice(ft, fixtureBind{conn.NewDefaultBind()}, device.NewLogger(device.LogLevelSilent, ""), 2)
	defer server.Close()
	port := freePort(t)
	config := "private_key=" + hex.EncodeToString(serverPrivate) + "\nlisten_port=" + strconv.Itoa(port) + "\npublic_key=" + hex.EncodeToString(clientPublic) + "\nallowed_ip=10.77.0.2/32\n"
	if err = server.IpcSet(config); err != nil {
		t.Fatal(err)
	}
	if err = server.Up(); err != nil {
		t.Fatal(err)
	}
	// A reachable TCP host must not turn a wrong WireGuard key into success.
	tcp, err := net.Listen("tcp", net.JoinHostPort("127.0.0.1", strconv.Itoa(port)))
	if err != nil {
		t.Fatal(err)
	}
	defer tcp.Close()
	go func() {
		for {
			c, err := tcp.Accept()
			if err != nil {
				return
			}
			c.Close()
		}
	}()
	peer := map[string]any{"endpoint": net.JoinHostPort("127.0.0.1", strconv.Itoa(port)), "publicKey": base64.StdEncoding.EncodeToString(serverPublic), "allowedIPs": []string{"0.0.0.0/0"}}
	out := map[string]any{"protocol": "wireguard", "settings": map[string]any{"secretKey": base64.StdEncoding.EncodeToString(clientPrivate), "address": []string{"10.77.0.2/32"}, "mtu": 1280, "peers": []any{peer}}}
	if _, err = probeChild(t, out, t.TempDir()); err != nil {
		t.Fatal("Authenticated WireGuard handshake failed", err)
	}
	_, wrong := keyPair(t)
	peer["publicKey"] = base64.StdEncoding.EncodeToString(wrong)
	if _, err = probeChild(t, out, t.TempDir()); err == nil {
		t.Fatal("TCP reachability or wrong peer became a WireGuard handshake")
	}
}

func TestQuicProbesEnrollPrivateCertificatesAndUseProtocolHandshakes(t *testing.T) {
	for _, kind := range []string{"tuic", "hysteria2"} {
		t.Run(kind, func(t *testing.T) {
			cert, key := testCertificate(t)
			certPEM, _ := os.ReadFile(cert)
			keyPEM, _ := os.ReadFile(key)
			port := freePort(t)
			password := "synthetic-password"
			listener := map[string]any{"name": "peer", "type": kind, "listen": "127.0.0.1", "port": port, "certificate": string(certPEM), "private-key": string(keyPEM), "alpn": []string{"h3"}}
			if kind == "tuic" {
				listener["users"] = map[string]string{"00000000-0000-4000-8000-000000000001": password}
			} else {
				listener["users"] = map[string]string{"synthetic": password}
			}
			raw, _ := json.Marshal(map[string]any{"mode": "rule", "log-level": "silent", "dns": map[string]bool{"enable": false}, "listeners": []any{listener}, "rules": []string{"MATCH,REJECT"}})
			peer := startFixtureChild(t, raw, "FIXTURE_READY", "ROVUNET_SUPPLEMENTAL_FIXTURE", "DISABLE_LOOPBACK_DETECTOR=1")
			defer func() { peer.Process.Kill(); peer.Wait() }()
			settings := map[string]any{"name": "proxy", "type": "tuic", "server": "127.0.0.1", "port": port, "password": password, "uuid": "00000000-0000-4000-8000-000000000001", "sni": "localhost", "alpn": []string{"h3"}, "skip-cert-verify": false}
			out := map[string]any{"protocol": "tuic", "settings": settings}
			if kind == "hysteria2" {
				out = map[string]any{"protocol": "hysteria", "settings": map[string]any{"version": 2, "address": "127.0.0.1", "port": port}, "streamSettings": map[string]any{"network": "hysteria", "tlsSettings": map[string]any{"serverName": "localhost"}, "hysteriaSettings": map[string]any{"auth": password}}}
			}
			directory := t.TempDir()
			for attempt := 0; attempt < 2; attempt++ {
				if _, err := probeChild(t, out, directory); err != nil {
					t.Fatal("Protocol handshake failed", err)
				}
			}
			pins, err := filepath.Glob(filepath.Join(directory, "*.pem"))
			if err != nil || len(pins) != 1 {
				t.Fatal("Certificate was not stored once", len(pins), err)
			}
			before, _ := os.ReadFile(pins[0])
			if kind == "hysteria2" {
				auth := out["streamSettings"].(map[string]any)["hysteriaSettings"].(map[string]any)
				auth["auth"] = "wrong-synthetic-password"
				if _, err := probeChild(t, out, directory); err == nil {
					t.Fatal("Hysteria authentication failure became handshake success")
				}
				auth["auth"] = password
			}
			if kind == "tuic" {
				settings["sni"] = "wrong.example.invalid"
			} else {
				out["streamSettings"].(map[string]any)["tlsSettings"].(map[string]any)["serverName"] = "wrong.example.invalid"
			}
			if _, err := probeChild(t, out, directory); err == nil {
				t.Fatal("Wrong certificate name accepted")
			}
			after, _ := os.ReadFile(pins[0])
			if string(before) != string(after) {
				t.Fatal("Stored certificate changed")
			}
		})
	}
}
