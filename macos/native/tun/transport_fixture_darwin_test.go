//go:build darwin && with_gvisor

package main

import (
	"bytes"
	"crypto/rand"
	"crypto/sha256"
	"encoding/json"
	"encoding/pem"
	"fmt"
	"net"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"testing"
	"time"
)

func runFixtureXray(t *testing.T, exe string, config []byte) {
	t.Helper()
	cmd := exec.Command(exe, "run", "-c", "stdin:")
	cmd.Stdin = bytes.NewReader(config)
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { cmd.Process.Kill(); cmd.Wait() })
}

// Build the actual managed outbound with two different DNS states: the bootstrap
// resolver can resolve the proxy name, while that name cannot resolve afterwards.
// A different SNI, a pinned test certificate and an independent HTTPS destination
// exercise all three identities. No private profile or global trust store is used.
func startTransportPair(t *testing.T, kind, driver, exe string, relay request, redirects map[string]string) {
	t.Helper()
	listen, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	port := listen.Addr().(*net.TCPAddr).Port
	listen.Close()
	if kind == "hy2" {
		u, e := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
		if e != nil {
			t.Fatal(e)
		}
		port = u.LocalAddr().(*net.UDPAddr).Port
		u.Close()
	}
	var randomID [16]byte
	if _, err = rand.Read(randomID[:]); err != nil {
		t.Fatal(err)
	}
	randomID[6] = (randomID[6] & 15) | 64
	randomID[8] = (randomID[8] & 63) | 128
	id := fmt.Sprintf("%x-%x-%x-%x-%x", randomID[:4], randomID[4:6], randomID[6:8], randomID[8:10], randomID[10:])
	var mask [32]byte
	rand.Read(mask[:])
	maskText := fmt.Sprintf("%x", mask[:])
	dir := t.TempDir()
	certFile := filepath.Join(dir, "cert.pem")
	keyFile := filepath.Join(dir, "key.pem")
	openssl := exec.Command("/usr/bin/openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "1", "-subj", "/CN=fixture.test", "-keyout", keyFile, "-out", certFile)
	if err = openssl.Run(); err != nil {
		t.Fatal(err)
	}
	cert, err := os.ReadFile(certFile)
	if err != nil {
		t.Fatal(err)
	}
	block, _ := pem.Decode(cert)
	if block == nil {
		t.Fatal("missing certificate")
	}
	pin := fmt.Sprintf("%x", sha256.Sum256(block.Bytes))
	if os.Getenv("ROVUNET_PACKET_BAD_PIN") == "1" {
		pin = fmt.Sprintf("%064x", 0)
	}
	tlsServer := map[string]any{"certificates": []any{map[string]any{"certificateFile": certFile, "keyFile": keyFile}}}
	stream := map[string]any{"security": "tls", "tlsSettings": tlsServer, "network": kind}
	proto := "vless"
	settings := map[string]any{"decryption": "none", "clients": []any{map[string]string{"id": id}}}
	q := url.Values{"type": {kind}, "security": {"tls"}, "sni": {"fixture.test"}, "path": {"/data"}}
	scheme := "vless"
	switch kind {
	case "ws", "xhttp":
		stream[kind+"Settings"] = map[string]any{"path": "/data"}
	case "trojan":
		proto = "trojan"
		scheme = proto
		settings = map[string]any{"clients": []any{map[string]string{"password": id}}}
		stream["network"] = "tcp"
		q.Del("path")
		q.Set("type", "tcp")
	case "hy2":
		proto = "hysteria"
		scheme = "hy2"
		settings = map[string]any{"version": 2, "clients": []any{map[string]string{"auth": id}}}
		stream["network"] = "hysteria"
		stream["hysteriaSettings"] = map[string]any{"version": 2}
		stream["finalmask"] = map[string]any{"udp": []any{map[string]any{"type": "salamander", "settings": map[string]string{"password": maskText}}}}
		tlsServer["alpn"] = []string{"h3"}
		q = url.Values{"sni": {"fixture.test"}, "obfs": {"salamander"}, "obfs-password": {maskText}}
	default:
		t.Fatal("unknown controlled transport")
	}
	outbounds := []any{map[string]string{"tag": "deny", "protocol": "blackhole"}}
	rules := []any{}
	for dest, target := range redirects {
		outbounds = append(outbounds, map[string]any{"tag": dest, "protocol": "freedom", "settings": map[string]any{"redirect": target, "finalRules": []any{map[string]any{"action": "allow", "ip": []string{"127.0.0.1/32"}}}}})
		rules = append(rules, map[string]any{"type": "field", "port": dest, "outboundTag": dest})
	}
	server, _ := json.Marshal(map[string]any{"log": map[string]string{"loglevel": "none"}, "inbounds": []any{map[string]any{"listen": "127.0.0.1", "port": port, "protocol": proto, "settings": settings, "streamSettings": stream}}, "routing": map[string]any{"rules": rules}, "outbounds": outbounds})
	runFixtureXray(t, exe, server)
	httpListener, e := net.Listen("tcp4", "127.0.0.1:0")
	if e != nil {
		t.Fatal(e)
	}
	httpPort := httpListener.Addr().(*net.TCPAddr).Port
	httpListener.Close()
	link := fmt.Sprintf("%s://%s@outer.test.invalid:%d?%s", scheme, id, port, q.Encode())
	input, _ := json.Marshal(map[string]any{"link": link, "pin": pin, "bootstrap": os.Getenv("ROVUNET_PACKET_NO_BOOTSTRAP") != "1", "http": httpPort, "socks": relay.SocksPort, "user": relay.SocksUser, "pass": relay.SocksPassword})
	build := exec.Command(driver)
	build.Stdin = bytes.NewReader(input)
	cfg, e := build.Output()
	if e != nil {
		t.Fatalf("managed config builder: %v", e)
	}
	var out struct {
		Outbounds []struct {
			Stream map[string]json.RawMessage `json:"streamSettings"`
		}
	}
	if json.Unmarshal(cfg, &out) != nil || len(out.Outbounds) != 1 {
		t.Fatal("invalid generated config")
	}
	runFixtureXray(t, exe, cfg)
	if kind != "hy2" {
		deadline := time.Now().Add(3 * time.Second)
		for {
			c, e := net.DialTimeout("tcp4", fmt.Sprintf("127.0.0.1:%d", port), 100*time.Millisecond)
			if e == nil {
				c.Close()
				break
			}
			if time.Now().After(deadline) {
				t.Fatal("fixture server startup timeout")
			}
			time.Sleep(20 * time.Millisecond)
		}
	}
}
