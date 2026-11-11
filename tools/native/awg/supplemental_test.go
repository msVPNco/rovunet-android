package main

import (
	"bufio"
	"crypto/rand"
	"crypto/rsa"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/json"
	"encoding/pem"
	"fmt"
	"io"
	"math/big"
	"net"
	"net/http"
	"net/http/httptest"
	"net/netip"
	"os"
	"os/exec"
	"os/signal"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"github.com/metacubex/mihomo/config"
	C "github.com/metacubex/mihomo/constant"
	"github.com/metacubex/mihomo/hub/executor"
	logrus "github.com/sirupsen/logrus"
)

func runSupplementalFixture() {
	logrus.SetOutput(io.Discard)
	raw, err := io.ReadAll(os.Stdin)
	if err != nil {
		os.Exit(2)
	}
	dir, _ := os.MkdirTemp("", "rovunet-peer-")
	defer os.RemoveAll(dir)
	C.SetHomeDir(dir)
	cfg, err := config.Parse(raw)
	if err != nil {
		fmt.Println("FIXTURE_FAILED")
		os.Exit(2)
	}
	executor.ApplyConfig(cfg, true)
	defer executor.Shutdown()
	fmt.Println("FIXTURE_READY")
	ch := make(chan os.Signal, 1)
	signal.Notify(ch, os.Interrupt)
	<-ch
}

func testCertificate(t *testing.T) (string, string) {
	t.Helper()
	dir := t.TempDir()
	key, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		t.Fatal(err)
	}
	template := &x509.Certificate{SerialNumber: big.NewInt(1), Subject: pkix.Name{CommonName: "localhost"}, DNSNames: []string{"localhost"}, IPAddresses: []net.IP{net.IPv4(127, 0, 0, 1)}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().Add(time.Hour), KeyUsage: x509.KeyUsageCertSign | x509.KeyUsageDigitalSignature | x509.KeyUsageKeyEncipherment, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}, BasicConstraintsValid: true, IsCA: true}
	der, err := x509.CreateCertificate(rand.Reader, template, template, &key.PublicKey, key)
	if err != nil {
		t.Fatal(err)
	}
	cert := filepath.Join(dir, "cert.pem")
	private := filepath.Join(dir, "key.pem")
	os.WriteFile(cert, pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der}), 0600)
	os.WriteFile(private, pem.EncodeToMemory(&pem.Block{Type: "RSA PRIVATE KEY", Bytes: x509.MarshalPKCS1PrivateKey(key)}), 0600)
	return cert, private
}
func startFixtureChild(t *testing.T, raw []byte, marker, env string, extra ...string) *exec.Cmd {
	t.Helper()
	exe, _ := os.Executable()
	cmd := exec.Command(exe, "-test.run=^$")
	cmd.Env = append(os.Environ(), env+"=1")
	cmd.Env = append(cmd.Env, extra...)
	cmd.Stdin = strings.NewReader(string(raw))
	pipe, _ := cmd.StdoutPipe()
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { cmd.Process.Kill() })
	ready := make(chan bool, 1)
	go func() {
		scan := bufio.NewScanner(pipe)
		for scan.Scan() {
			if scan.Text() == marker {
				ready <- true
				return
			}
			if strings.Contains(scan.Text(), "FAILED") {
				break
			}
		}
		ready <- false
	}()
	select {
	case ok := <-ready:
		if !ok {
			t.Fatal("fixture startup failed")
		}
	case <-time.After(15 * time.Second):
		t.Fatal("fixture startup timed out")
	}
	return cmd
}
func stopFixtureChild(t *testing.T, cmd *exec.Cmd) {
	t.Helper()
	cmd.Process.Signal(os.Interrupt)
	done := make(chan error, 1)
	go func() { done <- cmd.Wait() }()
	select {
	case err := <-done:
		if err != nil {
			t.Fatal(err)
		}
	case <-time.After(5 * time.Second):
		t.Fatal("shutdown timeout")
	}
}
func TestSupplementalEncryptedRelayAndShutdown(t *testing.T) {
	cert, key := testCertificate(t)
	for _, mode := range []string{"anytls", "tuic", "mieru-tcp", "mieru-udp"} {
		t.Run(mode, func(t *testing.T) {
			port := freePort(t)
			local := freePort(t)
			kind := mode
			if strings.HasPrefix(mode, "mieru") {
				kind = "mieru"
			}
			user := "00000000-0000-4000-8000-000000000001"
			pass := "synthetic-password"
			listener := map[string]any{"name": "peer", "type": kind, "listen": "127.0.0.1", "port": port, "users": map[string]string{user: pass}}
			proxy := map[string]any{"name": "proxy", "type": kind, "server": "127.0.0.1", "port": port, "password": pass, "udp": true}
			var link string
			if kind == "mieru" {
				transport := "TCP"
				if mode == "mieru-udp" {
					transport = "UDP"
				}
				listener["transport"] = transport
				proxy["transport"] = transport
				proxy["username"] = user
				link = fmt.Sprintf("mierus://%s:%s@127.0.0.1?profile=fixture&port=%d&protocol=%s", user, pass, port, transport)
			} else {
				certPEM, _ := os.ReadFile(cert)
				keyPEM, _ := os.ReadFile(key)
				listener["certificate"] = string(certPEM)
				listener["private-key"] = string(keyPEM)
				proxy["sni"] = "localhost"
				proxy["skip-cert-verify"] = false
				if kind == "tuic" {
					proxy["uuid"] = user
					listener["alpn"] = []string{"h3"}
					proxy["alpn"] = []string{"h3"}
					link = fmt.Sprintf("tuic://%s:%s@127.0.0.1:%d?sni=localhost&alpn=h3", user, pass, port)
				} else {
					link = fmt.Sprintf("anytls://%s@127.0.0.1:%d?sni=localhost", pass, port)
				}
			}
			server, _ := json.Marshal(map[string]any{"mode": "rule", "log-level": "silent", "dns": map[string]bool{"enable": false}, "listeners": []any{listener}, "rules": []string{"MATCH,DIRECT"}})
			peer := startFixtureChild(t, server, "FIXTURE_READY", "ROVUNET_SUPPLEMENTAL_FIXTURE", "DISABLE_LOOPBACK_DETECTOR=1")
			runtime := map[string]any{"inbounds": []any{map[string]any{"listen": "127.0.0.1", "port": local, "protocol": "socks", "settings": map[string]any{"auth": "password", "accounts": []any{map[string]string{"user": "test", "pass": "pass"}}}}}, "outbounds": []any{map[string]any{"protocol": kind, "settings": proxy}, map[string]any{"protocol": "freedom"}}}
			jar := os.Getenv("CORE_TEST_JAR")
			_, jarError := os.Stat(jar)
			if os.Getenv("ROVUNET_REQUIRE_KOTLIN") == "true" && jarError != nil {
				t.Fatal("Kotlin fixture JAR unavailable")
			}
			if jar != "" && jarError == nil {
				dir := t.TempDir()
				input := filepath.Join(dir, "profile.txt")
				output := filepath.Join(dir, "runtime.json")
				os.WriteFile(input, []byte(link), 0600)
				command := exec.Command("java", "-cp", jar+":"+filepath.Join(os.Getenv("KOTLIN_JARS"), "*"), "CoreFixtureKt", input, output, fmt.Sprint(local))
				if data, err := command.CombinedOutput(); err != nil {
					t.Fatalf("Kotlin runtime fixture failed: %v %s", err, data)
				}
				data, err := os.ReadFile(output)
				if err != nil {
					t.Fatal(err)
				}
				var generated map[string]any
				if err = json.Unmarshal(data, &generated); err != nil {
					t.Fatal(err)
				}
				generated["inbounds"].([]any)[0].(map[string]any)["settings"] = runtime["inbounds"].([]any)[0].(map[string]any)["settings"]
				runtime = generated
			}
			raw, _ := json.Marshal(runtime)
			child := startFixtureChild(t, raw, "ROVUNET_AWG_READY", "ROVUNET_AWG_TEST_CHILD", "SSL_CERT_FILE="+cert)
			target := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { io.WriteString(w, "verified-supplemental-payload") }))
			defer target.Close()
			address := netip.MustParseAddrPort(strings.TrimPrefix(target.URL, "http://"))
			tcp, _ := socksConnect(t, local, 1, address)
			tcp.Write([]byte("GET / HTTP/1.0\r\nHost: fixture\r\n\r\n"))
			body, err := io.ReadAll(tcp)
			tcp.Close()
			if err != nil || !strings.Contains(string(body), "verified-supplemental-payload") {
				t.Fatalf("TCP relay: %v", err)
			}
			echo, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
			if err != nil {
				t.Fatal(err)
			}
			defer echo.Close()
			go func() {
				b := make([]byte, 1024)
				for {
					n, a, e := echo.ReadFromUDP(b)
					if e != nil {
						return
					}
					echo.WriteToUDP(b[:n], a)
				}
			}()
			control, relay := socksConnect(t, local, 3, netip.MustParseAddrPort("0.0.0.0:0"))
			defer control.Close()
			udp, err := net.DialUDP("udp4", nil, net.UDPAddrFromAddrPort(relay))
			if err != nil {
				t.Fatal(err)
			}
			defer udp.Close()
			udp.SetDeadline(time.Now().Add(8 * time.Second))
			ep := echo.LocalAddr().(*net.UDPAddr).Port
			frame := append([]byte{0, 0, 0, 1, 127, 0, 0, 1, byte(ep >> 8), byte(ep)}, []byte("udp-verified")...)
			udp.Write(frame)
			b := make([]byte, 1024)
			n, err := udp.Read(b)
			if err != nil || !strings.HasSuffix(string(b[:n]), "udp-verified") {
				t.Fatalf("UDP relay: %v", err)
			}
			control.Close()
			udp.Close()
			stopFixtureChild(t, child)
			stopFixtureChild(t, peer)
			bind, err := net.Listen("tcp", fmt.Sprintf("127.0.0.1:%d", local))
			if err != nil {
				t.Fatal("retained local listener", err)
			}
			bind.Close()
		})
	}
}
