// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"crypto/tls"
	"crypto/x509"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"golang.org/x/net/proxy"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"strings"
	"sync/atomic"
	"testing"
	"time"
	wire "www.bamsoftware.com/git/dnstt.git/dns"
)

func TestDoHVerifiedDestinationResolution25(t *testing.T) {
	var queries atomic.Int32
	resolver := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		queries.Add(1)
		raw, _ := io.ReadAll(io.LimitReader(r.Body, 4097))
		m, e := wire.MessageFromWireFormat(raw)
		if e != nil || len(m.Question) != 1 {
			http.Error(w, "bad query", 400)
			return
		}
		answer := wire.Message{ID: m.ID, Flags: 0x8180, Question: m.Question, Answer: []wire.RR{{Name: m.Question[0].Name, Type: wire.RRTypeA, Class: wire.ClassIN, TTL: 60, Data: []byte{127, 0, 0, 1}}}}
		body, _ := answer.WireFormat()
		w.Header().Set("Content-Type", "application/dns-message")
		w.Write(body)
	}))
	defer resolver.Close()
	target := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { io.WriteString(w, "doh25-verified-payload") }))
	defer target.Close()
	_, port, _ := net.SplitHostPort(strings.TrimPrefix(target.URL, "http://"))
	local, _ := freePort()
	fields := make([]string, 88)
	fields[1] = "doh"
	fields[21] = resolver.URL
	roots := x509.NewCertPool()
	roots.AddCert(resolver.Certificate())
	tr := http.DefaultTransport.(*http.Transport).Clone()
	tr.TLSClientConfig = &tls.Config{RootCAs: roots, MinVersion: tls.VersionTLS12}
	o := newOwner(context.Background())
	defer o.stop()
	dialer := &net.Dialer{Timeout: 3 * time.Second}
	dial := func(ctx context.Context, address string) (net.Conn, error) {
		return dialer.DialContext(ctx, "tcp", address)
	}
	if e := startDoHWithTransport(o, config{Fields: fields, Port: local}, dial, tr); e != nil {
		t.Fatal(e)
	}
	d, _ := proxy.SOCKS5("tcp", fmt.Sprintf("127.0.0.1:%d", local), nil, proxy.Direct)
	c, e := d.Dial("tcp", net.JoinHostPort("fixture.example.invalid", port))
	if e != nil {
		t.Fatal(e)
	}
	c.SetDeadline(time.Now().Add(4 * time.Second))
	fmt.Fprint(c, "GET / HTTP/1.0\r\nHost: fixture\r\n\r\n")
	body, e := io.ReadAll(c)
	c.Close()
	if e != nil || !strings.Contains(string(body), "doh25-verified-payload") || queries.Load() != 1 {
		t.Fatal("DoH-verified destination TCP failed", e)
	}
	// Same real endpoint without the scoped fixture root must be rejected.
	untrusted, _ := freePort()
	second := newOwner(context.Background())
	defer second.stop()
	if _, e := start(second, config{Fields: fields, Port: untrusted}); e != nil {
		t.Fatal(e)
	}
	d, _ = proxy.SOCKS5("tcp", fmt.Sprintf("127.0.0.1:%d", untrusted), nil, proxy.Direct)
	if c, e := d.Dial("tcp", net.JoinHostPort("fixture.example.invalid", port)); e == nil {
		c.Close()
		t.Fatal("untrusted DoH accepted")
	}
	if queries.Load() != 1 {
		t.Fatal("untrusted TLS query reached server")
	}
}

func TestWindowsAgentSSHChain25(t *testing.T) {
	runner := os.Getenv("ROVUNET_PROTOCOL25_RUNNER_DLL")
	if runner == "" {
		t.Skip("Windows runner not requested")
	}
	address, key, stop := sshFixture(t)
	defer stop()
	body, _ := json.Marshal(map[string]string{"address": address, "hostKey": base64.StdEncoding.EncodeToString(key.PublicKey().Marshal())})
	cmd := exec.Command(os.Getenv("ROVUNET_TEST_DOTNET"), runner, "--ssh-chain")
	cmd.Stdin = strings.NewReader(string(body))
	if output, e := cmd.CombinedOutput(); e != nil {
		t.Fatalf("Windows chained SSH: %v %s", e, output)
	} else {
		t.Logf("Windows chained SSH: %s", output)
	}
}
