// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"net"
	"strings"
	"testing"
	wire "www.bamsoftware.com/git/dnstt.git/dns"
)

func TestScanner20CompressedNSAndMalformedPointers(t *testing.T) {
	// NS ns.example.test, where the suffix is compressed into the question.
	packet := []byte{0, 1, 0x81, 0x80, 0, 1, 0, 1, 0, 0, 0, 0, 7, 'e', 'x', 'a', 'm', 'p', 'l', 'e', 4, 't', 'e', 's', 't', 0, 0, 2, 0, 1, 0xc0, 12, 0, 2, 0, 1, 0, 0, 0, 60, 0, 5, 2, 'n', 's', 0xc0, 12}
	host, err := scannerNSHost(packet)
	if err != nil || host != "ns.example.test" {
		t.Fatalf("compressed NS decoding failed")
	}
	for _, bad := range [][]byte{nil, packet[:len(packet)-1], append(append([]byte{}, packet[:len(packet)-2]...), 0xff, 0xff)} {
		if _, err := scannerNSHost(bad); err == nil {
			t.Fatal("malformed NS accepted")
		}
	}
	cycle := append([]byte{}, packet...)
	cycle[len(cycle)-2] = 0xc0
	cycle[len(cycle)-1] = byte(len(cycle) - 2)
	if _, err := scannerNSHost(cycle); err == nil {
		t.Fatal("compression cycle accepted")
	}
}

func TestScanner20ReferenceNegativeRepliesAndQueryShapes(t *testing.T) {
	pc, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer pc.Close()
	queries := make(chan *wire.Message, 32)
	done := make(chan struct{})
	go func() {
		defer close(done)
		buffer := make([]byte, 4096)
		for {
			n, a, err := pc.ReadFrom(buffer)
			if err != nil {
				return
			}
			q, err := wire.MessageFromWireFormat(buffer[:n])
			if err != nil || len(q.Question) != 1 {
				continue
			}
			queries <- &q
			// NXDOMAIN/SERVFAIL are still measured DNS replies, never proof of a server tunnel.
			code := uint16(3)
			if q.Question[0].Type == wire.RRTypeTXT {
				code = 2
			}
			reply := wire.Message{ID: q.ID, Flags: 0x8100 | code, Question: q.Question}
			if len(q.Additional) > 0 {
				reply.Additional = q.Additional
			}
			out, _ := reply.WireFormat()
			pc.WriteTo(out, a)
		}
	}()
	request := scanRequest{Mode: "simple", Family: "dnstt", Zone: "t.example.test", Candidates: []string{pc.LocalAddr().String()}, Transports: []string{"udp"}, Workers: 1, TimeoutMS: 1000, Count: 1, Threshold: 1}
	row := request.examine(context.Background(), nil, 0, "udp")
	pc.Close()
	<-done
	close(queries)
	passed := strings.Join(row.Checks, ",")
	if !row.Dns || !row.Pass || row.Handshake || row.Completed != 6 || !strings.Contains(passed, "TXT") || !strings.Contains(passed, "LONG-TXT") || !strings.Contains(passed, "NXDOMAIN") || !strings.Contains(passed, "A") {
		t.Fatalf("reference response criteria lost: %v", row.Checks)
	}
	nsParent, randomNested, longPayload := false, false, false
	for q := range queries {
		name := q.Question[0].Name.String()
		if q.Question[0].Type == 2 {
			nsParent = name == "example.test"
		}
		if q.Question[0].Type == wire.RRTypeA && strings.HasSuffix(name, ".t.example.test") {
			randomNested = len(strings.Split(name, ".")) == 5
		}
		if q.Question[0].Type == wire.RRTypeTXT && len(name) > 200 {
			longPayload = true
		}
	}
	if !nsParent || !randomNested || !longPayload {
		t.Fatalf("mobile query shape mismatch: NS=%v RND=%v DPI=%v", nsParent, randomNested, longPayload)
	}
}
