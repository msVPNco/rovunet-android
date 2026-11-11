//go:build linux

package main

import (
	"io"
	"net"
	"net/http"
	"os"
	"os/exec"
	"strconv"
	"strings"
	"testing"
	"time"

	MLog "github.com/metacubex/mihomo/log"
	"github.com/miekg/dns"
)

// The launcher creates two fresh network namespaces with no internet route.
// A real authenticated Xray SOCKS listener is the only TUN uplink. These are
// Linux data-plane checks; they do not prove Darwin routing or Installer APIs.
func TestTunNetworkFixture(t *testing.T) {
	if os.Getenv("ROVUNET_NETNS_TEST") != "1" {
		t.Skip("requires isolated root namespace fixture")
	}
	base := os.Getenv("ROVUNET_TEST_BASE")
	port, e := strconv.Atoi(os.Getenv("ROVUNET_TEST_SOCKS"))
	if e != nil {
		t.Fatal(e)
	}
	r := validRequest()
	r.SocksPort = port
	plane, ready, e := startDataPlane(r, base, []string{"udp://203.0.113.10:5353#ROVUNET"})
	if e != nil {
		t.Fatal(e)
	}
	defer plane.Close()
	MLog.SetLevel(MLog.DEBUG)
	route, e := exec.Command("ip", "route", "get", "203.0.113.10").Output()
	if e != nil || !strings.Contains(string(route), ready.Interface) {
		t.Fatalf("IPv4 was not captured by TUN: %s %v", route, e)
	}
	route6, e := exec.Command("ip", "-6", "route", "get", "2001:db8:100::10").Output()
	if e != nil || !strings.Contains(string(route6), ready.Interface) {
		t.Fatalf("IPv6 was not captured by TUN: %s %v", route6, e)
	}
	client := &http.Client{Transport: &http.Transport{Proxy: nil}, Timeout: 10 * time.Second}
	for _, url := range []string{"http://203.0.113.10:8080/", "http://[2001:db8:100::10]:8081/"} {
		response, e := client.Get(url)
		if e != nil {
			t.Fatal(e)
		}
		body, e := io.ReadAll(io.LimitReader(response.Body, 64))
		response.Body.Close()
		if e != nil || string(body) != "Rovunet TUN fixture" {
			t.Fatal("HTTP body did not traverse TUN", e)
		}
	}
	for _, address := range []string{"203.0.113.10:7777", "[2001:db8:100::10]:7777"} {
		socket, e := net.DialTimeout("udp", address, 5*time.Second)
		if e != nil {
			t.Fatal(e)
		}
		_ = socket.SetDeadline(time.Now().Add(5 * time.Second))
		_, e = socket.Write([]byte("Rovunet UDP fixture"))
		if e != nil {
			t.Fatal(e)
		}
		buffer := make([]byte, 64)
		n, e := socket.Read(buffer)
		socket.Close()
		if e != nil || string(buffer[:n]) != "Rovunet UDP fixture" {
			t.Fatal("UDP did not traverse TUN", e)
		}
	}
	for _, network := range []string{"udp", "tcp"} {
		message := new(dns.Msg)
		message.SetQuestion("tun-fixture.example.invalid.", dns.TypeA)
		response, _, e := (&dns.Client{Net: network, Timeout: 8 * time.Second}).Exchange(message, "8.8.8.8:53")
		if e != nil || len(response.Answer) != 1 {
			t.Fatal("DNS hijack/proxy failed", e)
		}
		record, ok := response.Answer[0].(*dns.A)
		if !ok || record.A.String() != "203.0.113.10" {
			t.Fatal("unexpected DNS response")
		}
	}
	client.CloseIdleConnections()
	if e = plane.Close(); e != nil {
		t.Fatal(e)
	}
	if _, e = net.InterfaceByName(ready.Interface); e == nil {
		t.Fatal("TUN interface remained after cleanup")
	}
	route, e = exec.Command("ip", "route", "get", "203.0.113.10").Output()
	if e != nil || !strings.Contains(string(route), "rvclient") {
		t.Fatal("original route was not restored")
	}
	t.Log("TUN_NETNS_RESULT: IPv4 TCP, IPv6 TCP, IPv4 UDP, IPv6 UDP, DNS UDP/TCP hijack, real SOCKS authentication, route/interface cleanup")
}
