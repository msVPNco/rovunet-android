// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net"
	noizserver "noizdns/server"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	dnswire "www.bamsoftware.com/git/dnstt.git/dns"
	serverlib "www.bamsoftware.com/git/dnstt.git/dnstt-server/lib"
	"www.bamsoftware.com/git/dnstt.git/noise"
)

func TestWpfDnsPoolVerifiedPublicHTTPS(t *testing.T) {
	log.SetOutput(io.Discard)
	dotnet, dll, desktop, report := os.Getenv("ROVUNET_DOTNET"), os.Getenv("ROVUNET_DOMAIN_TEST_DLL"), os.Getenv("ROVUNET_DESKTOP_FIXTURE"), os.Getenv("ROVUNET_POOL_REPORT")
	if desktop == "" || report == "" {
		t.Fatal("explicit desktop and report required")
	}
	upstream := newOwner(context.Background())
	defer upstream.stop()
	port, _ := freePort()
	if err := serveSocks(upstream, port, func(ctx context.Context, addr string) (net.Conn, error) {
		return (&net.Dialer{}).DialContext(ctx, "tcp", addr)
	}); err != nil {
		t.Fatal(err)
	}
	key, err := noise.GeneratePrivkey()
	if err != nil {
		t.Fatal(err)
	}
	zone, _ := dnswire.ParseName("t.example.invalid")
	dns, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer dns.Close()
	go serverlib.Run(key, zone, fmt.Sprintf("127.0.0.1:%d", port), dns, 1232, noizserver.NewHooks())
	good, unexpected, closeRelay := tcpDNSFixture(t, dns.LocalAddr().String())
	defer closeRelay()
	bad, _ := freePort()
	request, _ := json.Marshal(map[string]string{"family": "dnstt", "mode": "tcp", "key": hex.EncodeToString(noise.PubkeyFromPrivkey(key)), "bad": fmt.Sprintf("127.0.0.1:%d", bad), "good": good, "output": "original"})
	cmd := exec.Command(dotnet, dll, "--global-resolver-fixture")
	cmd.Stdin = strings.NewReader(string(request))
	cmd.Stderr = io.Discard
	raw, err := cmd.Output()
	if err != nil {
		t.Fatal("profile fixture adapter failed")
	}
	payload, _ := json.Marshal(map[string]string{"raw": string(raw), "pool": good})
	if os.Getenv("ROVUNET_SCANNER_FIXTURE") == "1" {
		payload, _ = json.Marshal(map[string]string{"raw": string(raw), "pool": good, "action": "scanner"})
	}
	if os.Getenv("ROVUNET_SCANNER_MULTI_FIXTURE") == "1" {
		second, secondUDP, closeSecond := tcpDNSFixture(t, dns.LocalAddr().String())
		defer closeSecond()
		third, thirdUDP, closeThird := tcpDNSFixture(t, dns.LocalAddr().String())
		defer closeThird()
		defer func() { if secondUDP.Load() != 0 || thirdUDP.Load() != 0 { t.Error("TCP-only multi-selection emitted UDP") } }()
		payload, _ = json.Marshal(map[string]string{"raw": string(raw), "pool": strings.Join([]string{good, second, third}, "\n"), "action": "scanner"})
	}
	if os.Getenv("ROVUNET_COLLECTION_FIXTURE") == "1" {
		payload, _ = json.Marshal(map[string]string{"raw": string(raw), "pool": good, "action": "collection", "direct": fmt.Sprintf("127.0.0.1:%d", port)})
	}
	if os.Getenv("ROVUNET_MANUAL_PROBE_FIXTURE") == "1" {
		payload, _ = json.Marshal(map[string]string{"raw": string(raw), "pool": good, "action": "manual-probe"})
	}
	if os.Getenv("ROVUNET_PROBE_FALLBACK_FIXTURE") == "1" {
		payload, _ = json.Marshal(map[string]string{"raw": string(raw), "pool": good, "action": "probe-fallback"})
	}
	if err := os.MkdirAll(filepath.Dir(report), 0700); err != nil {
		t.Fatal(err)
	}
	process := exec.Command(desktop, "--ui-dns-pool-verified-smoke", report)
	process.Stdin = strings.NewReader(string(payload))
	process.Stdout = io.Discard
	process.Stderr = io.Discard
	if err := process.Run(); err != nil {
		t.Fatal("WPF verified pool failed; inspect safe report")
	}
	if unexpected.Load() != 0 {
		t.Fatal("TCP-only pool emitted UDP")
	}
	body, err := os.ReadFile(report)
	if err != nil {
		t.Fatal(err)
	}
	var result struct {
		Passed bool `json:"passed"`
	}
	if json.Unmarshal(body, &result) != nil || !result.Passed {
		t.Fatal("pool verification failed")
	}
}
