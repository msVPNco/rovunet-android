package main

import (
	"bufio"
	"encoding/binary"
	"net"
	"testing"
)

// A remote frame must be rejected from just its header, before allocating its body.
func TestRefuseOversizeSSHWebSocketFrame(t *testing.T) {
	local, remote := net.Pipe()
	defer local.Close()
	defer remote.Close()
	go func() {
		header := []byte{0x82, 127, 0, 0, 0, 0, 0, 0, 0, 0}
		binary.BigEndian.PutUint64(header[2:], 1<<48)
		remote.Write(header)
	}()
	ws := &wsConn{reader: bufio.NewReader(local), raw: local}
	if _, _, err := ws.readFrame(); err == nil {
		t.Fatal("oversized frame accepted")
	}
}

func TestLocalSocksRejectsUnauthenticatedSharing(t *testing.T) {
	if l, err := listenLocalSOCKS("0.0.0.0:0", "", ""); err == nil {
		l.Close()
		t.Fatal("opened public proxy")
	}
	l, err := listenLocalSOCKS("127.0.0.1:0", "", "")
	if err != nil {
		t.Fatal(err)
	}
	l.Close()
}

func TestShortDNSHeaderAndShortVlessUUID(t *testing.T) {
	sock, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
	if err != nil {
		t.Fatal(err)
	}
	defer sock.Close()
	go func() {
		b := make([]byte, 512)
		n, peer, e := sock.ReadFromUDP(b)
		if e == nil && n >= 2 {
			sock.WriteToUDP([]byte{b[0], b[1], 0x80}, peer)
		}
	}()
	if _, err := dnsQuery("127.0.0.1", sock.LocalAddr().(*net.UDPAddr).Port, "test.invalid", 1, 2000, nil); err == nil {
		t.Fatal("short DNS header accepted")
	}
	connectVless(&Profile{VlessUuid: "abc", VlessCdnIp: "127.0.0.1"})
}
