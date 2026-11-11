// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"crypto/x509"
	"encoding/base64"
	"fmt"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

func child(t *testing.T, exe string, args ...string) *exec.Cmd {
	t.Helper()
	cmd := exec.Command(exe, args...)
	cmd.Dir = t.TempDir()
	if os.Getenv("ROVUNET_FIXTURE_DEBUG") != "" {
		cmd.Stderr = os.Stderr
		cmd.Stdout = os.Stdout
	}
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { cmd.Process.Kill(); cmd.Wait() })
	return cmd
}
func TestPanelStreams(t *testing.T) {
	for _, kind := range []string{"vaydns", "vaydns_ssh", "vaydns_tcp", "vaydns_ssh_tcp", "vaydns_multi", "vaydns_multi_tcp", "vaydns_ssh_multi", "vaydns_ssh_multi_tcp", "vaydns_tls", "vaydns_ssh_tls", "vaydns_https", "vaydns_ssh_https", "ss", "slipstream_ssh"} {
		t.Run(kind, func(t *testing.T) {
			encrypted := ""
			if strings.HasSuffix(kind, "_https") {
				encrypted = "https"
				kind = strings.TrimSuffix(kind, "_https")
			} else if strings.HasSuffix(kind, "_tls") {
				encrypted = "tls"
				kind = strings.TrimSuffix(kind, "_tls")
			}
			tcpOnly := strings.HasSuffix(kind, "_tcp")
			kind = strings.TrimSuffix(kind, "_tcp")
			multi := strings.HasSuffix(kind, "_multi")
			kind = strings.TrimSuffix(kind, "_multi")
			vay := strings.HasPrefix(kind, "vaydns")
			exe := os.Getenv("ROVUNET_VAY_SERVER")
			if !vay {
				exe = os.Getenv("ROVUNET_SLIP_SERVER")
			}
			if exe == "" {
				t.Skip("external fixture is not configured")
			}
			upstream := newOwner(context.Background())
			defer upstream.stop()
			var sshPin string
			port, _ := freePort()
			if strings.HasSuffix(kind, "_ssh") {
				addr, signer, stop := sshFixture(t)
				defer stop()
				_, n, _ := net.SplitHostPort(addr)
				port = num(n)
				sshPin = base64.StdEncoding.EncodeToString(signer.PublicKey().Marshal())
			} else {
				if err := serveSocks(upstream, port, func(ctx context.Context, addr string) (net.Conn, error) {
					return (&net.Dialer{}).DialContext(ctx, "tcp", addr)
				}); err != nil {
					t.Fatal(err)
				}
			}
			backend := fmt.Sprintf("127.0.0.1:%d", port)
			f := fixtureFields(backend)
			f[1] = kind
			f[6] = "5000"
			if n := os.Getenv("ROVUNET_FIXTURE_KEEPALIVE"); n != "" {
				f[6] = n
			}
			f[3] = "t.example.invalid"
			f[19] = "127.0.0.1"
			f[22] = "udp"
			f[42] = "txt"
			f[43] = "101"
			f[49] = "2"
			f[44] = "0"
			f[39] = "0"
			udp, err := net.ListenPacket("udp", "127.0.0.1:0")
			if err != nil {
				t.Fatal(err)
			}
			resolver := udp.LocalAddr().String()
			udp.Close()
			f[4] = resolver + ":0"
			dir := t.TempDir()
			key := filepath.Join(dir, "server.key")
			pub := filepath.Join(dir, "server.pub")
			cfg := config{Fields: f, HostKey: sshPin}
			cfg.Port, _ = freePort()
			if vay {
				if err = exec.Command(exe, "-gen-key", "-privkey-file", key, "-pubkey-file", pub).Run(); err != nil {
					t.Fatal(err)
				}
				kb, _ := os.ReadFile(pub)
				f[11] = strings.TrimSpace(string(kb))
				child(t, exe, "-udp", resolver, "-privkey-file", key, "-domain", f[3], "-upstream", backend)
				if tcpOnly {
					relay, unexpected, stop := tcpDNSFixture(t, resolver)
					defer stop()
					defer func() {
						if unexpected.Load() != 0 {
							t.Error("TCP-only client sent UDP")
						}
					}()
					f[4] = relay + ":0"
					f[22] = "tcp"
				}
			} else {
				if err := exec.Command("openssl", "req", "-x509", "-nodes", "-newkey", "ec", "-pkeyopt", "ec_paramgen_curve:prime256v1", "-keyout", key, "-out", pub, "-days", "1", "-subj", "/CN="+f[3]).Run(); err != nil {
					t.Fatal(err)
				}
				_, dnsPort, _ := net.SplitHostPort(resolver)
				child(t, exe, "--debug-streams", "--domain", f[3], "--dns-listen-host", "127.0.0.1", "--dns-listen-port", dnsPort, "--target-address", backend, "--cert", pub, "--key", key)
				sp, _ := freePort()
				cfg.UpstreamPort = sp
				child(t, os.Getenv("ROVUNET_SLIP_CLIENT"), "--debug-streams", "--domain", f[3], "--resolver", resolver, "--cert", pub, "--tcp-listen-host", "127.0.0.1", "--tcp-listen-port", fmt.Sprint(sp), "--congestion-control", "bbr", "--keep-alive-interval", f[6])
				if err = waitLocal(context.Background(), fmt.Sprintf("127.0.0.1:%d", sp)); err != nil {
					t.Fatal(err)
				}
			}
			target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("rovunet-dns-https")) }))
			defer target.Close()
			if encrypted != "" {
				t.Setenv("GODEBUG", "x509usefallbackroots=1")
				fixtureRootOnce.Do(func() { pool := x509.NewCertPool(); pool.AddCert(target.Certificate()); x509.SetFallbackRoots(pool) })
				value := encryptedResolverFixture(t, resolver, encrypted, target.TLS.Certificates)
				f[22] = encrypted
				if encrypted == "https" {
					f[21] = value
				} else {
					f[4] = value + ":0"
				}
			}
			o := newOwner(context.Background())
			defer o.stop()
			if multi {
				dead, _ := freePort()
				f[4] = fmt.Sprintf("127.0.0.1:%d:0,", dead) + f[4]
			}
			before := time.Now()
			s, err := start(o, cfg)
			if err != nil {
				t.Fatalf("start: %s %v", s.Error, err)
			}
			ready := time.Since(before)
			queryHTTPSWithin(t, cfg.Port, target, 60*time.Second)
			t.Logf("ready_ms=%d through_two_https_ms=%d", ready.Milliseconds(), time.Since(before).Milliseconds())
		})
	}
}
