// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"crypto/ed25519"
	"crypto/rand"
	"encoding/base64"
	"encoding/binary"
	"encoding/hex"
	"fmt"
	soarlib "github.com/getlantern/soar"
	"golang.org/x/crypto/ssh"
	"golang.org/x/net/proxy"
	"io"
	"log"
	"log/slog"
	"net"
	"net/http"
	"net/http/httptest"
	noizserver "noizdns/server"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"
	dnswire "www.bamsoftware.com/git/dnstt.git/dns"
	serverlib "www.bamsoftware.com/git/dnstt.git/dnstt-server/lib"
	"www.bamsoftware.com/git/dnstt.git/noise"
)

func queryHTTPS(t *testing.T, port int, target *httptest.Server) {
	t.Helper()
	queryHTTPSWithin(t, port, target, 20*time.Second)
}
func queryHTTPSWithin(t *testing.T, port int, target *httptest.Server, timeout time.Duration) {
	t.Helper()
	d, e := proxy.SOCKS5("tcp", fmt.Sprintf("127.0.0.1:%d", port), nil, &net.Dialer{Timeout: 5 * time.Second})
	if e != nil {
		t.Fatal(e)
	}
	tr := target.Client().Transport.(*http.Transport).Clone()
	tr.DialContext = d.(proxy.ContextDialer).DialContext
	client := http.Client{Transport: tr, Timeout: timeout}
	defer tr.CloseIdleConnections()
	for i := 0; i < 2; i++ {
		r, e := client.Get(target.URL)
		if e != nil {
			t.Fatal(e)
		}
		body, _ := io.ReadAll(r.Body)
		r.Body.Close()
		if string(body) != "rovunet-dns-https" {
			t.Fatal("payload mismatch")
		}
	}
}
func TestDNSFamiliesAuthenticatedHTTPS(t *testing.T) {
	log.SetOutput(io.Discard)
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("rovunet-dns-https")) }))
	defer target.Close()
	for _, name := range []string{"dnstt", "sayedns", "dnstt_ssh", "sayedns_ssh", "dnstt/tcp", "sayedns/tcp", "dnstt_ssh/tcp", "sayedns_ssh/tcp"} {
		t.Run(name, func(t *testing.T) {
			mode, tcpOnly := strings.CutSuffix(name, "/tcp")
			upstream := newOwner(context.Background())
			defer upstream.stop()
			up, _ := freePort()
			var pin string
			if mode == "dnstt_ssh" || mode == "sayedns_ssh" {
				addr, key, stop := sshFixture(t)
				defer stop()
				f := fixtureFields(addr)
				up = num(f[17])
				pin = base64.StdEncoding.EncodeToString(key.PublicKey().Marshal())
			} else {
				if e := serveSocks(upstream, up, func(ctx context.Context, addr string) (net.Conn, error) {
					return (&net.Dialer{Timeout: time.Second}).DialContext(ctx, "tcp", addr)
				}); e != nil {
					t.Fatal(e)
				}
			}
			priv, e := noise.GeneratePrivkey()
			if e != nil {
				t.Fatal(e)
			}
			domain, _ := dnswire.ParseName("t.example.invalid")
			pc, e := net.ListenPacket("udp", "127.0.0.1:0")
			if e != nil {
				t.Fatal(e)
			}
			defer pc.Close()
			go serverlib.Run(priv, domain, fmt.Sprintf("127.0.0.1:%d", up), pc, 1232, noizserver.NewHooks())
			f := fixtureFields(fmt.Sprintf("127.0.0.1:%d", up))
			f[1] = mode
			f[3] = "t.example.invalid"
			f[19] = "127.0.0.1"
			f[4] = pc.LocalAddr().String() + ":0"
			f[11] = hex.EncodeToString(noise.PubkeyFromPrivkey(priv))
			f[22] = "udp"
			if tcpOnly {
				resolver, unexpected, stop := tcpDNSFixture(t, pc.LocalAddr().String())
				defer stop()
				defer func() {
					if unexpected.Load() != 0 {
						t.Error("TCP-only sent UDP to candidate")
					}
				}()
				f[22] = "tcp"
				f[4] = resolver + ":0"
			}
			f[27] = "0"
			f[38] = "1"
			f[39] = "1232"
			f[60] = "fanout"
			f[61] = "3"
			p, _ := freePort()
			o := newOwner(context.Background())
			defer o.stop()
			s, e := start(o, config{Fields: f, Port: p, HostKey: pin})
			if e != nil {
				t.Fatalf("fixture start failed: %s: %v", s.Error, e)
			}
			queryHTTPS(t, p, target)
		})
	}
}

// TCP DNS fixture forwards to the authoritative UDP server. Its UDP port counts
// accidental fallback packets and deliberately never replies to them.
func tcpDNSFixture(t *testing.T, upstream string) (string, *atomic.Int64, func()) {
	t.Helper()
	l, u := pairedDNSFixtureListeners(t)
	var unexpected atomic.Int64
	var mu sync.Mutex
	var conns []net.Conn
	go func() {
		b := make([]byte, 4096)
		for {
			if _, _, e := u.ReadFrom(b); e != nil {
				return
			}
			unexpected.Add(1)
		}
	}()
	go func() {
		for {
			c, e := l.Accept()
			if e != nil {
				return
			}
			mu.Lock()
			conns = append(conns, c)
			mu.Unlock()
			go func() {
				defer c.Close()
				var writes sync.Mutex
				for {
					var h [2]byte
					if _, e := io.ReadFull(c, h[:]); e != nil {
						return
					}
					n := int(binary.BigEndian.Uint16(h[:]))
					if n < 12 || n > 4096 {
						return
					}
					b := make([]byte, n)
					if _, e := io.ReadFull(c, b); e != nil {
						return
					}
					go func() {
						r, e := net.DialTimeout("udp", upstream, time.Second)
						if e != nil {
							return
						}
						defer r.Close()
						r.SetDeadline(time.Now().Add(5 * time.Second))
						r.Write(b)
						reply := make([]byte, 4096)
						n, e := r.Read(reply)
						if e != nil {
							return
						}
						frame := append([]byte{byte(n >> 8), byte(n)}, reply[:n]...)
						writes.Lock()
						defer writes.Unlock()
						c.Write(frame)
					}()
				}
			}()
		}
	}()
	return l.Addr().String(), &unexpected, func() {
		l.Close()
		u.Close()
		mu.Lock()
		for _, c := range conns {
			c.Close()
		}
		mu.Unlock()
	}
}
func TestSoarOnlyTCPHTTPSAndSSH(t *testing.T) {
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("rovunet-dns-https")) }))
	defer target.Close()
	pub, key, _ := ed25519.GenerateKey(rand.Reader)
	pc, _ := net.ListenPacket("udp", "127.0.0.1:0")
	defer pc.Close()
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	go soarlib.Serve(ctx, pc, soarlib.ServerConfig{Zone: "t.example.invalid", PrivateKey: key, Dial: (&net.Dialer{Timeout: 3 * time.Second}).DialContext, Logger: slog.New(slog.NewTextHandler(io.Discard, nil))})
	resolver, unexpected, stop := tcpDNSFixture(t, pc.LocalAddr().String())
	defer stop()
	for _, mode := range []string{"soar", "soar_ssh"} {
		t.Run(mode, func(t *testing.T) {
			var pin string
			sshAddr := "127.0.0.1:22"
			if mode == "soar_ssh" {
				addr, key, stop := sshFixture(t)
				defer stop()
				sshAddr = addr
				pin = base64.StdEncoding.EncodeToString(key.PublicKey().Marshal())
			}
			f := fixtureFields(sshAddr)
			f[1] = mode
			f[3] = "t.example.invalid"
			f[4] = resolver + ":0"
			f[11] = base64.StdEncoding.EncodeToString(pub)
			f[22] = "udp"
			f[79] = "0"
			f[81] = "only"
			f[82] = "soar"
			p, _ := freePort()
			o := newOwner(context.Background())
			defer o.stop()
			s, e := start(o, config{Fields: f, Port: p, HostKey: pin})
			if e != nil {
				t.Fatalf("fixture start failed: %s: %v", s.Error, e)
			}
			queryHTTPS(t, p, target)
		})
	}
	if unexpected.Load() != 0 {
		t.Fatal("TCP-only sent UDP queries")
	}
}

var _ ssh.PublicKey

func pairedDNSFixtureListeners(t *testing.T) (net.Listener, net.PacketConn) {
	t.Helper()
	// Windows can exclude different dynamic ranges for TCP and UDP. Alternate
	// the allocator so consecutive TCP ports in a UDP exclusion do not exhaust
	// the fixture retries without ever trying the UDP allocator's allowed range.
	for attempt := 0; attempt < 128; attempt++ {
		if attempt%2 == 1 {
			u, err := net.ListenPacket("udp", "127.0.0.1:0")
			if err != nil {
				continue
			}
			l, err := net.Listen("tcp", u.LocalAddr().String())
			if err == nil {
				return l, u
			}
			u.Close()
			continue
		}
		l, err := net.Listen("tcp", "127.0.0.1:0")
		if err != nil {
			continue
		}
		u, err := net.ListenPacket("udp", l.Addr().String())
		if err == nil {
			return l, u
		}
		l.Close()
	}
	t.Fatal("could not reserve matching local TCP/UDP ports")
	return nil, nil
}
