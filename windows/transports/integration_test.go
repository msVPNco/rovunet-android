// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"crypto/ed25519"
	"crypto/rand"
	"encoding/base64"
	"fmt"
	"golang.org/x/crypto/ssh"
	"golang.org/x/net/proxy"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"testing"
	"time"
)

func sshFixture(t *testing.T) (string, ssh.Signer, func()) {
	t.Helper()
	_, key, e := ed25519.GenerateKey(rand.Reader)
	if e != nil {
		t.Fatal(e)
	}
	signer, _ := ssh.NewSignerFromKey(key)
	cfg := &ssh.ServerConfig{PasswordCallback: func(c ssh.ConnMetadata, p []byte) (*ssh.Permissions, error) {
		if c.User() == "fixture" && string(p) == "private-fixture-only" {
			return nil, nil
		}
		return nil, fmt.Errorf("AUTH")
	}}
	cfg.AddHostKey(signer)
	l, e := net.Listen("tcp", "127.0.0.1:0")
	if e != nil {
		t.Fatal(e)
	}
	var mu sync.Mutex
	var all []net.Conn
	var wg sync.WaitGroup
	wg.Add(1)
	go func() {
		defer wg.Done()
		for {
			raw, e := l.Accept()
			if e != nil {
				return
			}
			mu.Lock()
			all = append(all, raw)
			mu.Unlock()
			go func() {
				defer raw.Close()
				conn, chs, reqs, e := ssh.NewServerConn(raw, cfg)
				if e != nil {
					return
				}
				defer conn.Close()
				go func() {
					for r := range reqs {
						r.Reply(true, nil)
					}
				}()
				for ch := range chs {
					if ch.ChannelType() != "direct-tcpip" {
						ch.Reject(ssh.UnknownChannelType, "DENIED")
						continue
					}
					var d struct {
						Host       string
						Port       uint32
						Origin     string
						OriginPort uint32
					}
					if ssh.Unmarshal(ch.ExtraData(), &d) != nil {
						ch.Reject(ssh.ConnectionFailed, "INVALID")
						continue
					}
					target, e := net.DialTimeout("tcp", net.JoinHostPort(d.Host, fmt.Sprint(d.Port)), time.Second)
					if e != nil {
						ch.Reject(ssh.ConnectionFailed, "TARGET")
						continue
					}
					channel, req, e := ch.Accept()
					if e != nil {
						target.Close()
						continue
					}
					go ssh.DiscardRequests(req)
					go func() {
						defer target.Close()
						defer channel.Close()
						done := make(chan struct{})
						go func() { io.Copy(target, channel); target.Close(); close(done) }()
						io.Copy(channel, target)
						channel.Close()
						<-done
					}()
				}
			}()
		}
	}()
	return l.Addr().String(), signer, func() {
		l.Close()
		mu.Lock()
		for _, c := range all {
			c.Close()
		}
		mu.Unlock()
		wg.Wait()
	}
}
func fixtureFields(addr string) []string {
	f := make([]string, 88)
	host, p, _ := net.SplitHostPort(addr)
	f[0] = "33"
	f[1] = "ssh"
	f[3] = host
	f[15] = "fixture"
	f[16] = "private-fixture-only"
	f[17] = p
	f[19] = host
	f[23] = "password"
	return f
}
func TestSSHTrustTransferAndCleanup(t *testing.T) {
	addr, key, stop := sshFixture(t)
	defer stop()
	fields := fixtureFields(addr)
	p, _ := freePort()
	o := newOwner(context.Background())
	s, e := start(o, config{Fields: fields, Port: p})
	o.stop()
	if e == nil || s.State != "trust" || s.Fingerprint != ssh.FingerprintSHA256(key.PublicKey()) {
		t.Fatal("unknown key accepted or wrong identity")
	}
	p, _ = freePort()
	o = newOwner(context.Background())
	s, e = start(o, config{Fields: fields, Port: p, HostKey: base64.StdEncoding.EncodeToString([]byte("changed"))})
	o.stop()
	if e == nil || s.Error != "SSH_KEY_CHANGED" {
		t.Fatal("changed identity accepted")
	}
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("rovunet-authenticated-https")) }))
	defer target.Close()
	p, _ = freePort()
	o = newOwner(context.Background())
	_, e = start(o, config{Fields: fields, Port: p, HostKey: base64.StdEncoding.EncodeToString(key.PublicKey().Marshal())})
	if e != nil {
		o.stop()
		t.Fatal("approved SSH failed")
	}
	d, e := proxy.SOCKS5("tcp", net.JoinHostPort("127.0.0.1", fmt.Sprint(p)), nil, &net.Dialer{Timeout: time.Second})
	if e != nil {
		t.Fatal(e)
	}
	transport := target.Client().Transport.(*http.Transport).Clone()
	transport.DialContext = d.(proxy.ContextDialer).DialContext
	client := http.Client{Transport: transport, Timeout: 5 * time.Second}
	response, e := client.Get(target.URL)
	if e != nil {
		o.stop()
		t.Fatal(e)
	}
	body, _ := io.ReadAll(response.Body)
	response.Body.Close()
	transport.CloseIdleConnections()
	if string(body) != "rovunet-authenticated-https" {
		t.Fatal("payload mismatch")
	}
	o.stop()
	c, e := net.DialTimeout("tcp", net.JoinHostPort("127.0.0.1", fmt.Sprint(p)), time.Second)
	if e == nil {
		c.Close()
		t.Fatal("listener leaked")
	}
}
func TestTCPOnlyNeverConvertedToUDP(t *testing.T) {
	f := make([]string, 88)
	f[4] = "127.0.0.1:5300:0"
	f[22] = "tcp"
	a, e := dnsAddress(f)
	if e != nil || a != "tcp://127.0.0.1:5300" {
		t.Fatal("TCP choice lost")
	}
	f[22] = "unsupported"
	if _, e = dnsAddress(f); e == nil {
		t.Fatal("unknown DNS transport accepted")
	}
}
func TestDNSIPv6AddressPreserved(t *testing.T) {
	f := make([]string, 88)
	f[22] = "tcp"
	f[4] = "2001:db8::1:5353:0"
	a, e := dnsAddress(f)
	if e != nil || a != "tcp://[2001:db8::1]:5353" {
		t.Fatal("IPv6 resolver changed")
	}
}
func TestUnsupportedSOCKSCommandCloses(t *testing.T) {
	a, b := net.Pipe()
	done := make(chan error, 1)
	go func() { defer a.Close(); _, e := socksRequest(a); done <- e }()
	b.SetDeadline(time.Now().Add(time.Second))
	b.Write([]byte{5, 1, 0})
	var reply [2]byte
	io.ReadFull(b, reply[:])
	b.Write([]byte{5, 3, 0, 1})
	b.Close()
	if e := <-done; e == nil {
		t.Fatal("UDP command accepted")
	}
}
func TestStopClosesPartialClient(t *testing.T) {
	o := newOwner(context.Background())
	p, _ := freePort()
	if e := serveSocks(o, p, func(context.Context, string) (net.Conn, error) { return nil, fmt.Errorf("NO_ROUTE") }); e != nil {
		t.Fatal(e)
	}
	c, e := net.Dial("tcp", net.JoinHostPort("127.0.0.1", fmt.Sprint(p)))
	if e != nil {
		t.Fatal(e)
	}
	c.Write([]byte{5})
	done := make(chan struct{})
	go func() { o.stop(); close(done) }()
	select {
	case <-done:
	case <-time.After(2 * time.Second):
		t.Fatal("stop did not close partial client")
	}
	c.Close()
}
func TestNoHostKeyCredentialLeakInStatus(t *testing.T) {
	addr, _, stop := sshFixture(t)
	defer stop()
	o := newOwner(context.Background())
	defer o.stop()
	f := fixtureFields(addr)
	p, _ := freePort()
	s, e := start(o, config{Fields: f, Port: p})
	if e == nil || strings.Contains(fmt.Sprint(s), f[16]) {
		t.Fatal("status leaked password")
	}
}
