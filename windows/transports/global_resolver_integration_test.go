// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"crypto/tls"
	"crypto/x509"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"net/http/httptest"
	noizserver "noizdns/server"
	"os"
	"os/exec"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"
	dnswire "www.bamsoftware.com/git/dnstt.git/dns"
	serverlib "www.bamsoftware.com/git/dnstt.git/dnstt-server/lib"
	"www.bamsoftware.com/git/dnstt.git/noise"
)

func TestGlobalResolverActualCoreOverrideHTTPS(t *testing.T) {
	log.SetOutput(io.Discard)
	dotnet, dll := os.Getenv("ROVUNET_DOTNET"), os.Getenv("ROVUNET_DOMAIN_TEST_DLL")
	if dotnet == "" || dll == "" {
		t.Fatal("explicit .NET fixture adapter required")
	}
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("rovunet-dns-https")) }))
	defer target.Close()
	t.Setenv("GODEBUG", "x509usefallbackroots=1")
	fixtureRootOnce.Do(func() { pool := x509.NewCertPool(); pool.AddCert(target.Certificate()); x509.SetFallbackRoots(pool) })
	for _, family := range []string{"dnstt", "sayedns"} {
		for _, mode := range []string{"udp", "tcp", "tls", "https"} {
			t.Run(family+"/"+mode, func(t *testing.T) {
				upstream := newOwner(context.Background())
				defer upstream.stop()
				up, _ := freePort()
				if err := serveSocks(upstream, up, func(ctx context.Context, addr string) (net.Conn, error) {
					return (&net.Dialer{}).DialContext(ctx, "tcp", addr)
				}); err != nil {
					t.Fatal(err)
				}
				priv, err := noise.GeneratePrivkey()
				if err != nil {
					t.Fatal(err)
				}
				domain, _ := dnswire.ParseName("t.example.invalid")
				pc, err := net.ListenPacket("udp", "127.0.0.1:0")
				if err != nil {
					t.Fatal(err)
				}
				defer pc.Close()
				go serverlib.Run(priv, domain, fmt.Sprintf("127.0.0.1:%d", up), pc, 1232, noizserver.NewHooks())
				good := pc.LocalAddr().String()
				if mode == "tcp" {
					relay, unexpected, stop := tcpDNSFixture(t, good)
					defer stop()
					defer func() {
						if unexpected.Load() != 0 {
							t.Error("TCP-only emitted UDP")
						}
					}()
					good = relay
				}
				if mode == "tls" || mode == "https" {
					good = encryptedResolverFixture(t, pc.LocalAddr().String(), mode, target.TLS.Certificates)
				}
				badTCP, bad := pairedDNSFixtureListeners(t)
				defer bad.Close()
				defer badTCP.Close()
				var fallback atomic.Int64
				go func() {
					b := make([]byte, 4096)
					for {
						if _, _, err := bad.ReadFrom(b); err != nil {
							return
						}
						fallback.Add(1)
					}
				}()
				go func() {
					for {
						c, err := badTCP.Accept()
						if err != nil {
							return
						}
						fallback.Add(1)
						c.Close()
					}
				}()
				request, _ := json.Marshal(map[string]string{"family": family, "mode": mode, "key": hex.EncodeToString(noise.PubkeyFromPrivkey(priv)), "bad": bad.LocalAddr().String(), "good": good})
				cmd := exec.Command(dotnet, dll, "--global-resolver-fixture")
				cmd.Stdin = strings.NewReader(string(request))
				cmd.Stderr = io.Discard
				wire, err := cmd.Output()
				if err != nil {
					t.Fatal(".NET override adapter failed")
				}
				var fields []string
				if json.Unmarshal(wire, &fields) != nil {
					t.Fatal("invalid adapter reply")
				}
				port, _ := freePort()
				owner := newOwner(context.Background())
				defer owner.stop()
				if _, err := start(owner, config{Fields: fields, Port: port}); err != nil {
					t.Fatal("DNS tunnel start failed")
				}
				queryHTTPS(t, port, target)
				if fallback.Load() != 0 {
					t.Fatal("traffic reached original resolver")
				}
			})
		}
	}
}

var fixtureRootOnce sync.Once

func encryptedResolverFixture(t *testing.T, upstream, mode string, certs []tls.Certificate) string {
	t.Helper()
	exchange := func(query []byte) ([]byte, error) {
		c, err := net.Dial("udp", upstream)
		if err != nil {
			return nil, err
		}
		defer c.Close()
		c.SetDeadline(time.Now().Add(2 * time.Second))
		if _, err = c.Write(query); err != nil {
			return nil, err
		}
		b := make([]byte, 4096)
		n, err := c.Read(b)
		return b[:n], err
	}
	if mode == "https" {
		server := httptest.NewUnstartedServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			query, err := io.ReadAll(io.LimitReader(r.Body, 4096))
			if err != nil {
				w.WriteHeader(400)
				return
			}
			reply, err := exchange(query)
			if err != nil {
				w.WriteHeader(502)
				return
			}
			w.Header().Set("Content-Type", "application/dns-message")
			w.Write(reply)
		}))
		server.TLS = &tls.Config{Certificates: certs}
		server.StartTLS()
		t.Cleanup(server.Close)
		return server.URL + "/dns-query"
	}
	relay, unexpected, stop := tcpDNSFixture(t, upstream)
	t.Cleanup(stop)
	t.Cleanup(func() {
		if unexpected.Load() != 0 {
			t.Error("unexpected UDP to TLS relay")
		}
	})
	listener, err := tls.Listen("tcp", "127.0.0.1:0", &tls.Config{Certificates: certs, MinVersion: tls.VersionTLS12})
	if err != nil {
		t.Fatal(err)
	}
	owner := newOwner(context.Background())
	t.Cleanup(func() { listener.Close(); owner.stop() })
	go func() {
		for {
			incoming, err := listener.Accept()
			if err != nil {
				return
			}
			owner.add(incoming)
			go func() {
				defer owner.drop(incoming)
				out, err := net.Dial("tcp", relay)
				if err != nil {
					return
				}
				owner.add(out)
				defer owner.drop(out)
				go io.Copy(out, incoming)
				io.Copy(incoming, out)
			}()
		}
	}()
	return listener.Addr().String()
}
