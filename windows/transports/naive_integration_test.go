// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"bytes"
	"context"
	"encoding/base64"
	"encoding/json"
	"encoding/pem"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

type flushingWriter struct{ w http.ResponseWriter }

func (f flushingWriter) Write(b []byte) (int, error) {
	n, e := f.w.Write(b)
	f.w.(http.Flusher).Flush()
	return n, e
}
func TestNaiveBinaryHTTPSAndSSH(t *testing.T) {
	binary := os.Getenv("ROVUNET_NAIVE_FIXTURE")
	if binary == "" {
		t.Skip("set ROVUNET_NAIVE_FIXTURE to the pinned Linux binary")
	}
	var connects, unauthorized, requests atomic.Int64
	var logs bytes.Buffer
	defer func() {
		if t.Failed() {
			t.Logf("proxy_requests=%d unauthorized=%d connected=%d errors=%v", requests.Load(), unauthorized.Load(), connects.Load(), regexp.MustCompile(`ERR_[A-Z_]+`).FindAllString(logs.String(), -1))
		}
	}()
	proxyServer := httptest.NewUnstartedServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requests.Add(1)
		if r.Method != "CONNECT" || r.Header.Get("Proxy-Authorization") != "Basic "+base64.StdEncoding.EncodeToString([]byte("fixture:private-fixture-only")) {
			unauthorized.Add(1)
			w.WriteHeader(407)
			return
		}
		host, port, err := net.SplitHostPort(r.Host)
		if err != nil || (host != "example.com" && host != "ssh.example.invalid") {
			w.WriteHeader(403)
			return
		}
		connects.Add(1)
		remote, e := net.DialTimeout("tcp", net.JoinHostPort("127.0.0.1", port), 3*time.Second)
		if e != nil {
			w.WriteHeader(502)
			return
		}
		defer remote.Close()
		w.WriteHeader(200)
		w.(http.Flusher).Flush()
		done := make(chan struct{})
		go func() { io.Copy(remote, r.Body); remote.Close(); close(done) }()
		io.Copy(flushingWriter{w}, remote)
		r.Body.Close()
		<-done
	}))
	proxyServer.EnableHTTP2 = true
	proxyServer.StartTLS()
	defer proxyServer.Close()
	temp := t.TempDir()
	ca := filepath.Join(temp, "ca.pem")
	if e := os.WriteFile(ca, pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: proxyServer.Certificate().Raw}), 0600); e != nil {
		t.Fatal(e)
	}
	p, _ := freePort()
	cfg, _ := json.Marshal(map[string]string{"listen": "socks://127.0.0.1:" + strconv.Itoa(p), "proxy": "https://fixture:private-fixture-only@" + proxyServer.Listener.Addr().String(), "log": ""})
	configFile := filepath.Join(temp, "config.json")
	os.WriteFile(configFile, cfg, 0600)
	cmd := exec.Command(binary, configFile)
	cmd.Stderr = &logs
	cmd.Env = append(os.Environ(), "SSL_CERT_FILE="+ca)
	if e := cmd.Start(); e != nil {
		t.Fatal(e)
	}
	defer func() { cmd.Process.Kill(); cmd.Wait() }()
	deadline := time.Now().Add(8 * time.Second)
	for {
		c, e := net.DialTimeout("tcp", net.JoinHostPort("127.0.0.1", strconv.Itoa(p)), 100*time.Millisecond)
		if e == nil {
			c.Close()
			break
		}
		if time.Now().After(deadline) {
			t.Fatal("Naive listener did not start")
		}
		time.Sleep(40 * time.Millisecond)
	}
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("rovunet-dns-https")) }))
	defer target.Close()
	if target.Certificate().VerifyHostname("example.com") != nil {
		t.Fatal("fixture certificate identity")
	}
	target.URL = strings.Replace(target.URL, "127.0.0.1", "example.com", 1)
	queryHTTPS(t, p, target)
	addr, key, stop := sshFixture(t)
	defer stop()
	f := fixtureFields(addr)
	f[1] = "naive_ssh"
	f[19] = "ssh.example.invalid"
	hp, _ := freePort()
	o := newOwner(context.Background())
	defer o.stop()
	s, e := start(o, config{Fields: f, Port: hp, UpstreamPort: p, HostKey: base64.StdEncoding.EncodeToString(key.PublicKey().Marshal())})
	if e != nil {
		t.Fatalf("SSH over Naive failed: %s", s.Error)
	}
	target.URL = strings.Replace(target.URL, "example.com", "127.0.0.1", 1)
	queryHTTPS(t, hp, target)
	if connects.Load() < 2 {
		t.Fatal("Naive bypassed the HTTP/2 proxy")
	}
}
