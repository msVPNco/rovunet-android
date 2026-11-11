// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"crypto/ecdsa"
	"crypto/ed25519"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/base64"
	"encoding/json"
	"encoding/pem"
	"fmt"
	soarlib "github.com/getlantern/soar"
	"io"
	"log/slog"
	"math/big"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

type countedPacket struct {
	net.PacketConn
	count atomic.Int64
}

func (p *countedPacket) ReadFrom(b []byte) (int, net.Addr, error) {
	n, a, e := p.PacketConn.ReadFrom(b)
	if e == nil {
		p.count.Add(1)
	}
	return n, a, e
}
func localSoar(t *testing.T) (string, string, *atomic.Int64) {
	t.Helper()
	pub, key, e := ed25519.GenerateKey(rand.Reader)
	if e != nil {
		t.Fatal(e)
	}
	pc, e := net.ListenPacket("udp", "127.0.0.1:0")
	if e != nil {
		t.Fatal(e)
	}
	counted := &countedPacket{PacketConn: pc}
	ctx, cancel := context.WithCancel(context.Background())
	t.Cleanup(func() { cancel(); pc.Close() })
	go soarlib.Serve(ctx, counted, soarlib.ServerConfig{Zone: "t.example.invalid", PrivateKey: key, Dial: (&net.Dialer{Timeout: 3 * time.Second}).DialContext, Logger: slog.New(slog.NewTextHandler(io.Discard, nil))})
	return base64.StdEncoding.EncodeToString(pub), pc.LocalAddr().String(), &counted.count
}
func coreResolverFixture(t *testing.T, family, mode, key, bad, good, output string, edits map[int]string) []byte {
	t.Helper()
	sshHost, sshPort := edits[19], edits[17]
	if sshHost == "" {
		sshHost = "127.0.0.1"
	}
	if sshPort == "" {
		sshPort = "22"
	}
	allowed := map[int]string{}
	for index, value := range edits {
		if index != 17 && index != 19 {
			allowed[index] = value
		}
	}
	editJSON, _ := json.Marshal(allowed)
	request, _ := json.Marshal(map[string]string{"family": family, "mode": mode, "key": key, "bad": bad, "good": good, "output": output, "password": "private-fixture-only", "edits": string(editJSON), "sshHost": sshHost, "sshPort": sshPort})
	cmd := exec.Command(os.Getenv("ROVUNET_DOTNET"), os.Getenv("ROVUNET_DOMAIN_TEST_DLL"), "--global-resolver-fixture")
	cmd.Stdin = strings.NewReader(string(request))
	cmd.Stderr = io.Discard
	body, e := cmd.Output()
	if e != nil {
		t.Fatal("core synthetic fixture failed")
	}
	return body
}
func TestSoarGlobalOverrideAndIsolatedDiscovery(t *testing.T) {
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("rovunet-dns-https")) }))
	defer target.Close()
	for _, mode := range []string{"udp", "tcp"} {
		t.Run(mode, func(t *testing.T) {
			key, good, received := localSoar(t)
			badTCP, badUDP := pairedDNSFixtureListeners(t)
			defer badTCP.Close()
			defer badUDP.Close()
			var badTraffic atomic.Int64
			go func() {
				b := make([]byte, 4096)
				for {
					if _, _, e := badUDP.ReadFrom(b); e != nil {
						return
					}
					badTraffic.Add(1)
				}
			}()
			go func() {
				for {
					c, e := badTCP.Accept()
					if e != nil {
						return
					}
					badTraffic.Add(1)
					c.Close()
				}
			}()
			edits := map[int]string{79: "0", 81: "off"}
			if mode == "tcp" {
				relay, unexpected, closeRelay := tcpDNSFixture(t, good)
				defer closeRelay()
				defer func() {
					if unexpected.Load() != 0 {
						t.Error("TCP-only sent UDP")
					}
				}()
				good = relay
				edits[81] = "only"
			}
			wire := coreResolverFixture(t, "soar", mode, key, badUDP.LocalAddr().String(), good, "", edits)
			var fields []string
			if json.Unmarshal(wire, &fields) != nil {
				t.Fatal("invalid core reply")
			}
			port, _ := freePort()
			owner := newOwner(context.Background())
			defer owner.stop()
			if _, e := start(owner, config{Fields: fields, Port: port}); e != nil {
				t.Fatal("global Soar start failed")
			}
			queryHTTPS(t, port, target)
			if badTraffic.Load() != 0 || received.Load() == 0 {
				t.Fatal("override isolation failed")
			}
		})
	}
	t.Run("probe_disables_working_discovery_escape", func(t *testing.T) {
		key, good, received := localSoar(t)
		sink, e := net.ListenPacket("udp", "127.0.0.1:0")
		if e != nil {
			t.Fatal(e)
		}
		defer sink.Close()
		bad := sink.LocalAddr().String()
		wire := coreResolverFixture(t, "soar", "udp", key, good, bad, "candidate", map[int]string{79: "1", 82: "custom", 83: good, 81: "off"})
		var fields []string
		json.Unmarshal(wire, &fields)
		if fields[79] != "0" || fields[83] != good {
			t.Fatal("probe changed discovery contract")
		}
		ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
		defer cancel()
		owner := newOwner(ctx)
		defer owner.stop()
		port, _ := freePort()
		if _, e := start(owner, config{Fields: fields, Port: port}); e == nil {
			t.Fatal("bad probe escaped through discovery")
		}
		if received.Load() != 0 {
			t.Fatal("disabled discovery contacted working candidate")
		}
	})
}
func slipFixtureCertificate(t *testing.T) (string, string) {
	t.Helper()
	key, e := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if e != nil {
		t.Fatal(e)
	}
	tpl := &x509.Certificate{SerialNumber: big.NewInt(time.Now().UnixNano()), Subject: pkix.Name{CommonName: "t.example.invalid"}, DNSNames: []string{"t.example.invalid"}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().Add(24 * time.Hour), KeyUsage: x509.KeyUsageDigitalSignature, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}, BasicConstraintsValid: true, IsCA: true}
	der, e := x509.CreateCertificate(rand.Reader, tpl, tpl, &key.PublicKey, key)
	if e != nil {
		t.Fatal(e)
	}
	priv, e := x509.MarshalECPrivateKey(key)
	if e != nil {
		t.Fatal(e)
	}
	return string(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der})), string(pem.EncodeToMemory(&pem.Block{Type: "EC PRIVATE KEY", Bytes: priv}))
}
func TestWpfRemainingResolverFamilies(t *testing.T) {
	desktop, folder := os.Getenv("ROVUNET_DESKTOP_FIXTURE"), os.Getenv("ROVUNET_REMAINING_REPORTS")
	if desktop == "" || folder == "" {
		t.Fatal("explicit desktop/reports required")
	}
	os.MkdirAll(folder, 0700)
	for _, name := range []string{"soar_udp", "soar_tcp", "soar_ssh", "soar_ssh_unknown", "slipstream_global", "slipstream_pool", "slipstream_authoritative", "slipstream_ssh", "slipstream_ssh_repeat2", "slipstream_ssh_repeat3", "slipstream_missing_certificate", "slipstream_wrong_certificate"} {
		t.Run(name, func(t *testing.T) {
			family, mode, key, good := "soar", "udp", "", ""
			edits := map[int]string{79: "0", 81: "off"}
			payload := map[string]string{}
			if strings.Contains(name, "ssh") {
				family += "_ssh"
				addr, signer, stop := sshFixture(t)
				defer stop()
				host, port, _ := net.SplitHostPort(addr)
				edits[19] = host
				edits[17] = port
				payload["hostKey"] = base64.StdEncoding.EncodeToString(signer.PublicKey().Marshal())
			}
			if strings.HasPrefix(name, "slipstream") {
				family = strings.Replace(family, "soar", "slipstream", 1)
				exe := os.Getenv("ROVUNET_SLIP_SERVER")
				if exe == "" {
					t.Fatal("pinned Windows Slipstream server required")
				}
				up := newOwner(context.Background())
				defer up.stop()
				port, _ := freePort()
				backend := fmt.Sprintf("127.0.0.1:%d", port)
				if strings.Contains(name, "ssh") {
					backend = net.JoinHostPort(edits[19], edits[17])
				} else if e := serveSocks(up, port, func(ctx context.Context, addr string) (net.Conn, error) {
					return (&net.Dialer{}).DialContext(ctx, "tcp", addr)
				}); e != nil {
					t.Fatal(e)
				}
				cert, priv := slipFixtureCertificate(t)
				dir := t.TempDir()
				certFile, keyFile := filepath.Join(dir, "server.pem"), filepath.Join(dir, "server.key")
				os.WriteFile(certFile, []byte(cert), 0600)
				os.WriteFile(keyFile, []byte(priv), 0600)
				dns, e := net.ListenPacket("udp", "127.0.0.1:0")
				if e != nil {
					t.Fatal(e)
				}
				good = dns.LocalAddr().String()
				dns.Close()
				_, dnsPort, _ := net.SplitHostPort(good)
				child(t, exe, "--domain", "t.example.invalid", "--dns-listen-host", "127.0.0.1", "--dns-listen-port", dnsPort, "--target-address", backend, "--cert", certFile, "--key", keyFile)
				payload["certificate"] = cert
				if name == "slipstream_authoritative" {
					edits[5] = "1"
				}
				if name == "slipstream_wrong_certificate" {
					payload["certificate"], _ = slipFixtureCertificate(t)
					payload["reject"] = "true"
				}
				if name == "slipstream_missing_certificate" {
					delete(payload, "certificate")
					payload["reject"] = "true"
				}
				if name == "slipstream_global" {
					payload["action"] = "global"
				}
			} else {
				key, good, _ = localSoar(t)
				if name == "soar_tcp" {
					mode = "tcp"
					relay, unexpected, stop := tcpDNSFixture(t, good)
					defer stop()
					defer func() {
						if unexpected.Load() != 0 {
							t.Error("TCP-only pool emitted UDP")
						}
					}()
					good = relay
					edits[81] = "only"
				}
				if name == "soar_ssh_unknown" {
					delete(payload, "hostKey")
					payload["reject"] = "true"
				}
			}
			bad, _ := freePort()
			raw := coreResolverFixture(t, family, mode, key, fmt.Sprintf("127.0.0.1:%d", bad), good, "original", edits)
			payload["raw"] = string(raw)
			payload["pool"] = good
			input, _ := json.Marshal(payload)
			report := filepath.Join(folder, name+".json")
			ctx, cancel := context.WithTimeout(context.Background(), 210*time.Second)
			defer cancel()
			cmd := exec.CommandContext(ctx, desktop, "--ui-dns-pool-verified-smoke", report)
			cmd.Stdin = strings.NewReader(string(input))
			cmd.Stdout = io.Discard
			cmd.Stderr = io.Discard
			if e := cmd.Run(); e != nil {
				t.Fatal("WPF family verification failed; inspect safe report")
			}
			body, e := os.ReadFile(report)
			if e != nil {
				t.Fatal(e)
			}
			var result struct{ Passed bool }
			if json.Unmarshal(body, &result) != nil || !result.Passed {
				t.Fatal("family report failed")
			}
		})
	}
}
