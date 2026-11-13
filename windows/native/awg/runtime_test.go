package main

import (
	"bufio"
	"context"
	"crypto/ecdh"
	"crypto/rand"
	"encoding/base64"
	"encoding/binary"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/netip"
	"os"
	"os/exec"
	"path/filepath"
	goruntime "runtime"
	"strconv"
	"strings"
	"sync"
	"testing"
	"time"

	awg "github.com/metacubex/amneziawg-go/device"
	awgv1 "github.com/metacubex/amneziawg-go/device_v1"
	"github.com/metacubex/mipstack"
	"github.com/metacubex/wireguard-go/conn"
	"github.com/metacubex/wireguard-go/device"
	"github.com/metacubex/wireguard-go/tun"
)

func TestMain(m *testing.M) {
	if os.Getenv("ROVUNET_AWG_TEST_CHILD") == "1" {
		if err := run(); err != nil {
			fmt.Fprintf(os.Stderr, "fixture child: %v\n", err)
			os.Exit(1)
		}
		os.Exit(0)
	}
	os.Exit(m.Run())
}

type fixtureTun struct {
	*mipstack.Stack
	events chan tun.Event
	once   sync.Once
}

func (t *fixtureTun) File() *os.File           { return nil }
func (t *fixtureTun) Events() <-chan tun.Event { return t.events }
func (t *fixtureTun) Close() error             { t.once.Do(func() { close(t.events) }); return t.Stack.Close() }

// Loopback fixtures need no privileged kernel route notifications.
type fixtureBind struct{ conn.Bind }

type awgDevice interface {
	IpcSet(string) error
	Up() error
	Close()
}

func keyPair(t *testing.T) ([]byte, []byte) {
	t.Helper()
	k, e := ecdh.X25519().GenerateKey(rand.Reader)
	if e != nil {
		t.Fatal(e)
	}
	return k.Bytes(), k.PublicKey().Bytes()
}
func freePort(t *testing.T) int {
	l, e := net.ListenPacket("udp", "127.0.0.1:0")
	if e != nil {
		t.Fatal(e)
	}
	defer l.Close()
	return l.LocalAddr().(*net.UDPAddr).Port
}
func readN(t *testing.T, r io.Reader, n int) []byte {
	t.Helper()
	b := make([]byte, n)
	if _, e := io.ReadFull(r, b); e != nil {
		t.Fatal(e)
	}
	return b
}
func socksConnect(t *testing.T, p, command int, target netip.AddrPort) (net.Conn, netip.AddrPort) {
	t.Helper()
	s, e := net.DialTimeout("tcp", fmt.Sprintf("127.0.0.1:%d", p), 5*time.Second)
	if e != nil {
		t.Fatal(e)
	}
	s.SetDeadline(time.Now().Add(8 * time.Second))
	s.Write([]byte{5, 1, 2})
	if string(readN(t, s, 2)) != string([]byte{5, 2}) {
		t.Fatal("auth method")
	}
	s.Write([]byte{1, 4, 't', 'e', 's', 't', 4, 'p', 'a', 's', 's'})
	if readN(t, s, 2)[1] != 0 {
		t.Fatal("auth")
	}
	ip := target.Addr().As4()
	req := []byte{5, byte(command), 0, 1, ip[0], ip[1], ip[2], ip[3], byte(target.Port() >> 8), byte(target.Port())}
	s.Write(req)
	h := readN(t, s, 4)
	if h[1] != 0 || h[3] != 1 {
		t.Fatalf("SOCKS response %v", h)
	}
	a := readN(t, s, 6)
	addr := netip.AddrPortFrom(netip.AddrFrom4([4]byte{a[0], a[1], a[2], a[3]}), binary.BigEndian.Uint16(a[4:]))
	return s, addr
}
func TestRuntimeRejectsForeignListeners(t *testing.T) {
	for _, body := range []string{`{}`, `{"inbounds":[{"listen":"0.0.0.0","port":1080}],"outbounds":[]}`} {
		if _, _, _, e := convert([]byte(body)); e == nil {
			t.Fatal("accepted foreign config")
		}
	}
}
func TestAmneziaEncryptedTcpUdpAndShutdown(t *testing.T) {
	for _, version := range []string{"1", "2", "3.1"} {
		t.Run(version, func(t *testing.T) {
			serverPrivate, serverPublic := keyPair(t)
			clientPrivate, clientPublic := keyPair(t)
			protection, _ := keyPair(t)
			options := map[string]any{"jc": 4, "jmin": 40, "jmax": 70, "s1": 16, "s2": 16, "h1": "123456", "h2": "234567", "h3": "345678", "h4": "456789"}
			ipc := "jc=4\njmin=40\njmax=70\ns1=16\ns2=16\nh1=123456\nh2=234567\nh3=345678\nh4=456789\n"
			conf := "Jc=4\nJmin=40\nJmax=70\nS1=16\nS2=16\nH1=123456\nH2=234567\nH3=345678\nH4=456789\n"
			if version != "1" {
				options["s3"] = 16
				options["s4"] = 16
				ipc += "s3=16\ns4=16\n"
				conf += "S3=16\nS4=16\n"
			}
			if version == "2" {
				options["h1"] = "123456-123499"
				ipc = strings.ReplaceAll(ipc, "h1=123456", "h1=123456-123499")
				conf = strings.ReplaceAll(conf, "H1=123456", "H1=123456-123499")
			}
			if version == "3.1" {
				options["version"] = 3
				options["header-protection-key"] = base64.StdEncoding.EncodeToString(protection)
				options["random-trailers"] = true
				options["disable-cookies"] = true
				ipc += "header_protection_key=" + hex.EncodeToString(protection) + "\nrandom_trailers=true\ndisable_cookies=true\n"
				conf += "HeaderProtectionKey=" + base64.StdEncoding.EncodeToString(protection) + "\nRandomTrailers=true\nDisableCookies=true\n"
			}
			stack, e := mipstack.New(mipstack.Config{LocalAddresses: []netip.Prefix{netip.MustParsePrefix("10.77.0.1/32")}, MTU: 1280})
			if e != nil {
				t.Fatal(e)
			}
			if e = stack.Start(); e != nil {
				t.Fatal(e)
			}
			ft := &fixtureTun{Stack: stack, events: make(chan tun.Event, 1)}
			ft.events <- tun.EventUp
			var server awgDevice
			logger := device.NewLogger(device.LogLevelSilent, "")
			if version == "3.1" {
				server = awg.NewDevice(ft, fixtureBind{conn.NewDefaultBind()}, logger, 2)
			} else {
				server = awgv1.NewDevice(ft, fixtureBind{conn.NewDefaultBind()}, logger, 2)
			}
			defer server.Close()
			port := freePort(t)
			ipc += "private_key=" + hex.EncodeToString(serverPrivate) + "\nlisten_port=" + strconv.Itoa(port) + "\npublic_key=" + hex.EncodeToString(clientPublic) + "\nallowed_ip=10.77.0.2/32\n"
			if e = server.IpcSet(ipc); e != nil {
				t.Fatal(e)
			}
			if e = server.Up(); e != nil {
				t.Fatal(e)
			}
			httpListener, e := stack.ListenTCP(context.Background(), "tcp4", netip.MustParseAddrPort("10.77.0.1:8080"))
			if e != nil {
				t.Fatal(e)
			}
			defer httpListener.Close()
			hs := &http.Server{Handler: http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { io.WriteString(w, "awg-real-payload") })}
			defer hs.Close()
			go hs.Serve(httpListener)
			udp, e := stack.ListenUDP(context.Background(), "udp4", netip.MustParseAddrPort("10.77.0.1:9090"))
			if e != nil {
				t.Fatal(e)
			}
			defer udp.Close()
			go func() {
				b := make([]byte, 65535)
				for {
					n, a, err := udp.ReadFrom(b)
					if err != nil {
						return
					}
					udp.WriteTo(b[:n], a)
				}
			}()
			if fixture := os.Getenv("ROVUNET_WINDOWS_AGENT_FIXTURE"); fixture != "" {
				text := "[Interface]\nPrivateKey=" + base64.StdEncoding.EncodeToString(clientPrivate) + "\nAddress=10.77.0.2/32\nMTU=1280\n" + conf + "\n[Peer]\nPublicKey=" + base64.StdEncoding.EncodeToString(serverPublic) + "\nEndpoint=127.0.0.1:" + strconv.Itoa(port) + "\nAllowedIPs=0.0.0.0/0\n"
				cmd := exec.Command(os.Getenv("ROVUNET_TEST_DOTNET"), fixture)
				cmd.Stdin = strings.NewReader(text)
				if output, err := cmd.CombinedOutput(); err != nil {
					t.Fatalf("Windows agent fixture failed: %v %s", err, output)
				} else {
					t.Logf("Windows agent fixture: %s", output)
				}
			}
			localPort := freePort(t)
			runtime := map[string]any{"inbounds": []any{map[string]any{"listen": "127.0.0.1", "port": localPort, "protocol": "socks", "settings": map[string]any{"auth": "password", "accounts": []any{map[string]string{"user": "test", "pass": "pass"}}}}}, "outbounds": []any{map[string]any{"protocol": "amneziawg", "settings": map[string]any{"name": "proxy", "type": "wireguard", "server": "127.0.0.1", "port": port, "ip": "10.77.0.2/32", "private-key": base64.StdEncoding.EncodeToString(clientPrivate), "public-key": base64.StdEncoding.EncodeToString(serverPublic), "allowed-ips": []string{"0.0.0.0/0"}, "udp": true, "mtu": 1280, "amnezia-wg-option": options}}, map[string]any{"protocol": "freedom"}}}
			raw, _ := json.Marshal(runtime)
			if jar := os.Getenv("CORE_TEST_JAR"); jar != "" {
				if _, err := os.Stat(jar); err == nil {
					dir := t.TempDir()
					link := filepath.Join(dir, "fixture.conf")
					output := filepath.Join(dir, "runtime.json")
					text := "[Interface]\nPrivateKey=" + base64.StdEncoding.EncodeToString(clientPrivate) + "\nAddress=10.77.0.2/32\nMTU=1280\n" + conf + "\n[Peer]\nPublicKey=" + base64.StdEncoding.EncodeToString(serverPublic) + "\nEndpoint=127.0.0.1:" + strconv.Itoa(port) + "\nAllowedIPs=0.0.0.0/0\n"
					os.WriteFile(link, []byte(text), 0600)
					cmd := exec.Command("java", "-cp", jar+":"+filepath.Join(os.Getenv("KOTLIN_JARS"), "*"), "CoreFixtureKt", link, output, strconv.Itoa(localPort))
					if out, err := cmd.CombinedOutput(); err != nil {
						t.Fatalf("Kotlin fixture: %s %v", out, err)
					}
					raw, err = os.ReadFile(output)
					if err != nil {
						t.Fatal(err)
					}
					// Add the production-style random local authentication after using the actual Kotlin importer.
					var cfg map[string]any
					json.Unmarshal(raw, &cfg)
					cfg["inbounds"].([]any)[0].(map[string]any)["settings"] = runtime["inbounds"].([]any)[0].(map[string]any)["settings"]
					raw, _ = json.Marshal(cfg)
				} else if os.Getenv("ROVUNET_REQUIRE_KOTLIN") == "true" {
					t.Fatal("Kotlin fixture JAR unavailable")
				}
			}
			exe, e := os.Executable()
			if e != nil {
				t.Fatal(e)
			}
			child := exec.Command(exe, "-test.run=^$")
			child.Env = append(os.Environ(), "ROVUNET_AWG_TEST_CHILD=1")
			child.Stderr = os.Stderr
			child.Stdin = strings.NewReader(string(raw))
			pipe, e := child.StdoutPipe()
			if e != nil {
				t.Fatal(e)
			}
			if e = child.Start(); e != nil {
				t.Fatal(e)
			}
			defer child.Process.Kill()
			ready := make(chan string, 1)
			go func() {
				scanner := bufio.NewScanner(pipe)
				for scanner.Scan() {
					ready <- scanner.Text()
					return
				}
				ready <- "EOF"
			}()
			select {
			case line := <-ready:
				if line != "ROVUNET_AWG_READY" {
					t.Fatalf("child failed: %s", line)
				}
			case <-time.After(10 * time.Second):
				t.Fatal("startup timeout")
			}
			tcp, _ := socksConnect(t, localPort, 1, netip.MustParseAddrPort("10.77.0.1:8080"))
			tcp.Write([]byte("GET / HTTP/1.0\r\nHost: fixture\r\n\r\n"))
			body, e := io.ReadAll(tcp)
			tcp.Close()
			if e != nil || !strings.Contains(string(body), "awg-real-payload") {
				t.Fatalf("TCP relay %v", e)
			}
			control, relay := socksConnect(t, localPort, 3, netip.MustParseAddrPort("0.0.0.0:0"))
			defer control.Close()
			if relay.Port() == uint16(localPort) {
				t.Fatal("UDP relay must be scoped to authentication")
			}
			u, e := net.DialUDP("udp", nil, net.UDPAddrFromAddrPort(relay))
			if e != nil {
				t.Fatal(e)
			}
			defer u.Close()
			u.SetDeadline(time.Now().Add(5 * time.Second))
			frame := append([]byte{0, 0, 0, 1, 10, 77, 0, 1, 0x23, 0x82}, []byte("udp-payload")...)
			u.Write(frame)
			b := make([]byte, 100)
			n, err := u.Read(b)
			if err != nil || !strings.HasSuffix(string(b[:n]), "udp-payload") {
				t.Fatalf("UDP relay: %v", err)
			}
			control.Close()
			u.Close()
			released := false
			for n := 0; n < 50; n++ {
				check, err := net.ListenPacket("udp4", relay.String())
				if err == nil {
					check.Close()
					released = true
					break
				}
				time.Sleep(10 * time.Millisecond)
			}
			if !released {
				t.Fatal("UDP association survived its authenticated TCP owner")
			}
			// Windows cannot deliver Unix SIGINT to this pipe-only fixture child.
			// Keep graceful signal assertions on Darwin/Linux; on Windows verify
			// forced process termination and release of the local listener instead.
			if goruntime.GOOS == "windows" {
				child.Process.Kill()
			} else {
				child.Process.Signal(os.Interrupt)
			}
			done := make(chan error, 1)
			go func() { done <- child.Wait() }()
			select {
			case err := <-done:
				if err != nil && goruntime.GOOS != "windows" {
					t.Fatal(err)
				}
			case <-time.After(4 * time.Second):
				t.Fatal("shutdown stalled")
			}
			check, e := net.Listen("tcp", fmt.Sprintf("127.0.0.1:%d", localPort))
			if e != nil {
				t.Fatal("port retained", e)
			}
			check.Close()
		})
	}
}
