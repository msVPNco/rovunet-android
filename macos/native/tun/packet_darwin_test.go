//go:build darwin && with_gvisor

package main

import (
	"bytes"
	"context"
	"crypto/rand"
	"crypto/sha256"
	"crypto/tls"
	"encoding/binary"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"sync"
	"testing"
	"time"

	"github.com/metacubex/gvisor/pkg/buffer"
	"github.com/metacubex/gvisor/pkg/tcpip"
	"github.com/metacubex/gvisor/pkg/tcpip/adapters/gonet"
	"github.com/metacubex/gvisor/pkg/tcpip/link/channel"
	"github.com/metacubex/gvisor/pkg/tcpip/network/ipv4"
	"github.com/metacubex/gvisor/pkg/tcpip/network/ipv6"
	"github.com/metacubex/gvisor/pkg/tcpip/stack"
	tunstack "github.com/metacubex/sing-tun"
	"github.com/miekg/dns"
	"golang.org/x/sys/unix"
)

// The OS side is a private userspace IP stack exchanging Darwin-framed packets.
// The worker uses the production Mihomo parser, sing-tun listener and gVisor
// backend. Only TEST-NET fixtures and the authenticated loopback relay are used.
func TestDarwinPacketPath(t *testing.T) {
	exe := os.Getenv("ROVUNET_PACKET_TEST_XRAY")
	if exe == "" {
		t.Skip("set the path to the packaged Xray for the isolated packet fixture")
	}
	payload := []byte("Rovunet authenticated Darwin packet fixture\n")
	web := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) { w.Write(payload) }))
	defer web.Close()
	udp, err := net.ListenPacket("udp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer udp.Close()
	go func() {
		b := make([]byte, 512)
		for {
			n, a, e := udp.ReadFrom(b)
			if e != nil {
				return
			}
			udp.WriteTo(b[:n], a)
		}
	}()
	dnsListener, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	dnsServer := &dns.Server{Listener: dnsListener, Handler: dns.HandlerFunc(func(w dns.ResponseWriter, r *dns.Msg) {
		m := new(dns.Msg)
		m.SetReply(r)
		if len(r.Question) == 1 {
			q := r.Question[0]
			if q.Name == "packet.test." && q.Qtype == dns.TypeA {
				m.Answer = []dns.RR{&dns.A{Hdr: dns.RR_Header{Name: q.Name, Rrtype: dns.TypeA, Class: dns.ClassINET, Ttl: 30}, A: net.IPv4(203, 0, 113, 19)}}
			}
		}
		w.WriteMsg(m)
	})}
	go dnsServer.ActivateAndServe()
	defer dnsServer.Shutdown()
	r := validRequest()
	reservation, err := net.Listen("tcp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	r.SocksPort = reservation.Addr().(*net.TCPAddr).Port
	reservation.Close()
	for _, s := range []*string{&r.SocksUser, &r.SocksPassword} {
		var b [32]byte
		if _, err := rand.Read(b[:]); err != nil {
			t.Fatal(err)
		}
		*s = fmt.Sprintf("%x", b)
	}
	outbounds := []any{}
	rules := []any{}
	for name, target := range map[string]string{"443": web.Listener.Addr().String(), "7777": udp.LocalAddr().String(), "5353": dnsListener.Addr().String()} {
		outbounds = append(outbounds, map[string]any{"tag": "test-" + name, "protocol": "freedom", "settings": map[string]any{"redirect": target}})
		rules = append(rules, map[string]any{"type": "field", "port": name, "outboundTag": "test-" + name})
	}
	outbounds = append([]any{map[string]any{"tag": "deny", "protocol": "blackhole"}}, outbounds...)
	cfg := map[string]any{"log": map[string]any{"loglevel": "none"}, "inbounds": []any{map[string]any{"listen": "127.0.0.1", "port": r.SocksPort, "protocol": "socks", "settings": map[string]any{"auth": "password", "udp": true, "accounts": []any{map[string]string{"user": r.SocksUser, "pass": r.SocksPassword}}}}}, "outbounds": outbounds, "routing": map[string]any{"rules": rules}}
	encoded, _ := json.Marshal(cfg)
	if kind := os.Getenv("ROVUNET_PACKET_TRANSPORT"); kind != "" {
		startTransportPair(t, kind, os.Getenv("ROVUNET_PACKET_CONFIG_DRIVER"), exe, r, map[string]string{"443": web.Listener.Addr().String(), "7777": udp.LocalAddr().String(), "5353": dnsListener.Addr().String()})
	} else {
		runFixtureXray(t, exe, encoded)
	}
	deadline := time.Now().Add(4 * time.Second)
	for checkUplink(r) != nil {
		if time.Now().After(deadline) {
			t.Fatal("packaged Xray did not authenticate fixture relay")
		}
		time.Sleep(20 * time.Millisecond)
	}
	st := isolatedStack(t, r, []string{"tcp://203.0.113.19:5353#ROVUNET"})
	v4 := tcpip.AddrFrom4([4]byte{203, 0, 113, 19})
	v6 := tcpip.AddrFrom16([16]byte{0x20, 1, 0x0d, 0xb8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 19})
	for _, family := range []struct {
		name  string
		proto tcpip.NetworkProtocolNumber
		addr  tcpip.Address
	}{{"IPv4", ipv4.ProtocolNumber, v4}, {"IPv6", ipv6.ProtocolNumber, v6}} {
		t.Run(family.name+"-HTTPS", func(t *testing.T) {
			tr := &http.Transport{TLSClientConfig: &tls.Config{RootCAs: web.Client().Transport.(*http.Transport).TLSClientConfig.RootCAs}, DialContext: func(c context.Context, _, _ string) (net.Conn, error) {
				return gonet.DialContextTCP(c, st, tcpip.FullAddress{NIC: tunstack.DefaultNIC, Addr: family.addr, Port: 443}, family.proto)
			}}
			defer tr.CloseIdleConnections()
			client := &http.Client{Transport: tr, Timeout: 4 * time.Second}
			resp, err := client.Get("https://example.com/")
			if err != nil {
				t.Fatal(err)
			}
			defer resp.Body.Close()
			b, err := io.ReadAll(resp.Body)
			if err != nil || resp.StatusCode != 200 || !bytes.Equal(b, payload) {
				t.Fatal("incomplete HTTPS fixture response")
			}
			t.Logf("HTTP 200, %d bytes, sha256 %x", len(b), sha256.Sum256(b))
		})
		t.Run(family.name+"-UDP", func(t *testing.T) {
			c, err := gonet.DialUDP(st, nil, &tcpip.FullAddress{NIC: tunstack.DefaultNIC, Addr: family.addr, Port: 7777}, family.proto)
			if err != nil {
				t.Fatal(err)
			}
			defer c.Close()
			c.SetDeadline(time.Now().Add(4 * time.Second))
			if _, err = c.Write(payload); err != nil {
				t.Fatal(err)
			}
			b := make([]byte, 128)
			n, err := c.Read(b)
			if err != nil || !bytes.Equal(b[:n], payload) {
				t.Fatalf("UDP echo: %v", err)
			}
		})
	}
	for _, network := range []string{"tcp", "udp"} {
		t.Run("DNS-"+network, func(t *testing.T) {
			dest := tcpip.FullAddress{NIC: tunstack.DefaultNIC, Addr: tcpip.AddrFrom4([4]byte{8, 8, 8, 8}), Port: 53}
			ctx, stop := context.WithTimeout(context.Background(), 4*time.Second)
			defer stop()
			var c net.Conn
			var err error
			if network == "tcp" {
				c, err = gonet.DialContextTCP(ctx, st, dest, ipv4.ProtocolNumber)
			} else {
				c, err = gonet.DialUDP(st, nil, &dest, ipv4.ProtocolNumber)
			}
			if err != nil {
				t.Fatal(err)
			}
			defer c.Close()
			c.SetDeadline(time.Now().Add(4 * time.Second))
			m := new(dns.Msg)
			m.SetQuestion("packet.test.", dns.TypeA)
			wire, _ := m.Pack()
			if network == "tcp" {
				wire = append([]byte{byte(len(wire) >> 8), byte(len(wire))}, wire...)
			}
			if _, err = c.Write(wire); err != nil {
				t.Fatal(err)
			}
			b := make([]byte, 4096)
			var n int
			if network == "tcp" {
				if _, err = io.ReadFull(c, b[:2]); err == nil {
					n = int(binary.BigEndian.Uint16(b))
					if n > len(b) {
						t.Fatal("DNS frame too long")
					}
					_, err = io.ReadFull(c, b[:n])
				}
			} else {
				n, err = c.Read(b)
			}
			if err != nil {
				t.Fatal(err)
			}
			var result dns.Msg
			if err = result.Unpack(b[:n]); err != nil || result.Id != m.Id || result.Rcode != 0 || len(result.Answer) != 1 {
				t.Fatal("DNS did not return controlled answer")
			}
			if a, ok := result.Answer[0].(*dns.A); !ok || a.A.String() != "203.0.113.19" {
				t.Fatal("unexpected DNS address")
			}
		})
	}
}

func isolatedStack(t *testing.T, r request, resolvers []string) *stack.Stack {
	t.Helper()
	pair, err := unix.Socketpair(unix.AF_UNIX, unix.SOCK_DGRAM, 0)
	if err != nil {
		t.Fatal(err)
	}
	peerFile := os.NewFile(uintptr(pair[1]), "fixture-peer")
	packet, err := net.FileConn(peerFile)
	peerFile.Close()
	if err != nil {
		unix.Close(pair[0])
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = packet.Close() })
	opts := tunOptions("utun-fixture")
	opts.FileDescriptor = pair[0]
	opts.AutoRoute = false
	opts.AutoDetectInterface = false
	opts.DNSMode = "disabled"
	base, err := appBase(filepath.Join("/Applications/Rovunet.app/Contents/MacOS/engines", "rovunet-tun"))
	if err != nil {
		t.Fatal(err)
	}
	plane, _, err := openDataPlane(r, base, resolvers, opts)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() {
		if err := plane.Close(); err != nil {
			t.Error("packet worker cleanup failed")
		}
	})
	link := channel.New(128, 1500, "")
	st, err := tunstack.NewGVisorStack(link)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(st.Close)
	for _, addr := range []struct {
		proto tcpip.NetworkProtocolNumber
		addr  tcpip.Address
	}{{ipv4.ProtocolNumber, tcpip.AddrFrom4([4]byte{198, 18, 0, 1})}, {ipv6.ProtocolNumber, tcpip.AddrFrom16([16]byte{0xfd, 0xfe, 0xdc, 0xba, 0x98, 0x76, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1})}} {
		if e := st.AddProtocolAddress(tunstack.DefaultNIC, tcpip.ProtocolAddress{Protocol: addr.proto, AddressWithPrefix: addr.addr.WithPrefix()}, stack.AddressProperties{}); e != nil {
			t.Fatal(e)
		}
	}
	ctx, cancel := context.WithCancel(context.Background())
	var pumps sync.WaitGroup
	pumps.Add(2)
	go func() {
		defer pumps.Done()
		for {
			p := link.ReadContext(ctx)
			if p == nil {
				return
			}
			v := p.ToView()
			data := v.AsSlice()
			family := byte(unix.AF_INET)
			if data[0]>>4 == 6 {
				family = unix.AF_INET6
			}
			frame := append([]byte{0, 0, 0, family}, data...)
			v.Release()
			p.DecRef()
			if _, err := packet.Write(frame); err != nil {
				return
			}
		}
	}()
	go func() {
		defer pumps.Done()
		buf := make([]byte, 65540)
		for {
			n, err := packet.Read(buf)
			if err != nil {
				return
			}
			if n < 5 {
				continue
			}
			proto := tcpip.NetworkProtocolNumber(ipv4.ProtocolNumber)
			if buf[3] == unix.AF_INET6 {
				proto = ipv6.ProtocolNumber
			}
			p := stack.NewPacketBuffer(stack.PacketBufferOptions{Payload: buffer.MakeWithData(append([]byte(nil), buf[4:n]...))})
			link.InjectInbound(proto, p)
			p.DecRef()
		}
	}()
	t.Cleanup(func() { cancel(); packet.Close(); pumps.Wait() })

	return st
}
