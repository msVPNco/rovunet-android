//go:build darwin && with_gvisor

package main

import (
	"bufio"
	"bytes"
	"context"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"github.com/metacubex/gvisor/pkg/tcpip"
	"github.com/metacubex/gvisor/pkg/tcpip/adapters/gonet"
	"github.com/metacubex/gvisor/pkg/tcpip/network/ipv4"
	tunstack "github.com/metacubex/sing-tun"
	"github.com/miekg/dns"
	"io"
	"net"
	"net/http"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"testing"
	"time"
)

// Explicit opt-in. Only the test process owns this IP stack. Input is the
// owner's selected link on stdin. It never enters logs or a file.
func TestOperationalDarwinPacketPath(t *testing.T) {
	if os.Getenv("ROVUNET_OPERATIONAL_SINGLE_PROFILE") != "1" {
		t.Skip("requires an explicitly selected profile on stdin")
	}
	var profile struct{ Link string }
	if err := json.NewDecoder(io.LimitReader(os.Stdin, 150000)).Decode(&profile); err != nil {
		t.Fatal("missing operational profile")
	}
	httpListener, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	socksListener, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		httpListener.Close()
		t.Fatal(err)
	}
	r := validRequest()
	r.SocksPort = socksListener.Addr().(*net.TCPAddr).Port
	httpPort := httpListener.Addr().(*net.TCPAddr).Port
	for _, value := range []*string{&r.SocksUser, &r.SocksPassword} {
		var cred [32]byte
		if _, err := rand.Read(cred[:]); err != nil {
			t.Fatal(err)
		}
		*value = hex.EncodeToString(cred[:])
	}
	helper, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	hport := helper.Addr().(*net.TCPAddr).Port
	input, _ := json.Marshal(map[string]any{"link": profile.Link, "helper": hport, "http": httpPort, "socks": r.SocksPort, "user": r.SocksUser, "pass": r.SocksPassword, "pin": "", "operational": true})
	ctx, cancel := context.WithTimeout(context.Background(), 25*time.Second)
	defer cancel()
	cfg := exec.CommandContext(ctx, os.Getenv("ROVUNET_PACKET_CONFIG_DRIVER"))
	cfg.Stdin = bytes.NewReader(input)
	encoded, err := cfg.Output()
	helper.Close()
	httpListener.Close()
	socksListener.Close()
	if err != nil {
		t.Fatal("pre-route outer endpoint bootstrap/config generation failed")
	}
	t.Log("original profile accepted; outer bootstrap complete")
	var bundle struct{ Xray, Transport string }
	if json.Unmarshal(encoded, &bundle) != nil || bundle.Xray == "" {
		t.Fatal("invalid operational fixture configuration")
	}
	if bundle.Transport != "" {
		cmd := exec.CommandContext(ctx, filepath.Join(filepath.Dir(os.Getenv("ROVUNET_PACKET_TEST_XRAY")), "rovunet-transports"))
		input, err := cmd.StdinPipe()
		if err != nil {
			t.Fatal(err)
		}
		output, err := cmd.StdoutPipe()
		if err != nil {
			t.Fatal(err)
		}
		if cmd.Start() != nil {
			t.Fatal("native transport failed to start")
		}
		t.Cleanup(func() { input.Close(); cmd.Process.Kill(); cmd.Wait() })
		if _, err = io.WriteString(input, bundle.Transport+"\n"); err != nil {
			t.Fatal("native transport configuration failed")
		}
		scanner := bufio.NewScanner(output)
		var ready struct{ State string }
		if !scanner.Scan() || json.Unmarshal(scanner.Bytes(), &ready) != nil || ready.State != "ready" {
			t.Fatal("native transport did not report ready")
		}
		go func() {
			for scanner.Scan() {
			}
		}()
		t.Log("original VLESS native transport options accepted; helper ready")
	}
	runFixtureXray(t, os.Getenv("ROVUNET_PACKET_TEST_XRAY"), []byte(bundle.Xray))
	until := time.Now().Add(4 * time.Second)
	for checkUplink(r) != nil {
		if time.Now().After(until) {
			t.Fatal("authenticated SOCKS unavailable")
		}
		time.Sleep(30 * time.Millisecond)
	}
	t.Log("loopback SOCKS authenticated")
	const target = "https://example.com/"
	get := func(stage string, tr *http.Transport) ([32]byte, bool) {
		defer tr.CloseIdleConnections()
		c := http.Client{Transport: tr, Timeout: 8 * time.Second, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}
		resp, e := c.Get(target)
		if e != nil {
			t.Errorf("%s: HTTPS failed (%T)", stage, e)
			return [32]byte{}, false
		}
		defer resp.Body.Close()
		body, e := io.ReadAll(io.LimitReader(resp.Body, 65537))
		if e != nil || resp.StatusCode != 200 || len(body) > 65536 || !strings.Contains(string(body), "Example Domain") {
			t.Errorf("%s: HTTPS payload incomplete", stage)
			return [32]byte{}, false
		}
		hash := sha256.Sum256(body)
		t.Logf("%s: HTTP 200, %d bytes, sha256 %x", stage, len(body), hash)
		return hash, true
	}
	local, _ := url.Parse("http://127.0.0.1:" + strconv.Itoa(httpPort))
	want, passed := get("local-http", &http.Transport{Proxy: http.ProxyURL(local)})
	if !passed {
		return
	}
	st := isolatedStack(t, r, nil)
	dnsPeer := tcpip.FullAddress{NIC: tunstack.DefaultNIC, Addr: tcpip.AddrFrom4([4]byte{8, 8, 8, 8}), Port: 53}
	c, err := gonet.DialContextTCP(ctx, st, dnsPeer, ipv4.ProtocolNumber)
	if err != nil {
		t.Fatal("TUN DNS connect failed")
	}
	defer c.Close()
	c.SetDeadline(time.Now().Add(7 * time.Second))
	dc := &dns.Conn{Conn: c}
	q := new(dns.Msg)
	q.SetQuestion("example.com.", dns.TypeA)
	if dc.WriteMsg(q) != nil {
		t.Fatal("TUN DNS request failed")
	}
	answer, err := dc.ReadMsg()
	if err != nil || answer.Id != q.Id || answer.Rcode != 0 {
		t.Fatal("TUN DNS did not return a valid matching response")
	}
	var addr net.IP
	for _, rr := range answer.Answer {
		if a, ok := rr.(*dns.A); ok {
			addr = a.A.To4()
			break
		}
	}
	if addr == nil {
		t.Fatal("TUN DNS answer contained no IPv4 address")
	}
	t.Log("TUN DNS -> production DoH through selected SOCKS relay: matching A answer")
	tr := &http.Transport{DialContext: func(ctx context.Context, _, _ string) (net.Conn, error) {
		return gonet.DialContextTCP(ctx, st, tcpip.FullAddress{NIC: tunstack.DefaultNIC, Addr: tcpip.AddrFromSlice(addr), Port: 443}, ipv4.ProtocolNumber)
	}}
	got, passed := get("isolated-tun", tr)
	if passed && got != want {
		t.Fatal("HTTPS response differs from local-proxy control")
	}
}
