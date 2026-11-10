// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"net"
	"testing"
	wire "www.bamsoftware.com/git/dnstt.git/dns"
)

func TestScanner19LatencyAndPartialDiagnostics(t *testing.T) {
	pc, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer pc.Close()
	done := make(chan struct{})
	go func() {
		defer close(done)
		buffer := make([]byte, 4096)
		for {
			n, address, err := pc.ReadFrom(buffer)
			if err != nil {
				return
			}
			query, err := wire.MessageFromWireFormat(buffer[:n])
			if err != nil || len(query.Question) != 1 {
				continue
			}
			if query.Question[0].Type == 2 {
				continue
			}
			reply := wire.Message{ID: query.ID, Flags: 0x8100, Question: query.Question}
			output, _ := reply.WireFormat()
			pc.WriteTo(output, address)
		}
	}()
	request := scanRequest{Mode: "advanced", Family: "dnstt", Zone: "t.example.invalid", Candidates: []string{pc.LocalAddr().String()}, Transports: []string{"udp"}, Workers: 1, TimeoutMS: 350, Count: 1, Threshold: 1}
	row := request.examine(context.Background(), nil, 0, "udp")
	if !row.Dns || !row.Pass || row.DnsElapsedMS <= 0 || row.DnsElapsedMS >= 150 || row.ElapsedMS < 300 {
		t.Fatalf("bad latency accounting: %d / %d", row.DnsElapsedMS, row.ElapsedMS)
	}
	if row.Completed < 4 || row.Completed >= 6 || len(row.CompletedChecks) != row.Completed {
		t.Fatalf("independent checks were blocked: completed=%d", row.Completed)
	}
	pc.Close()
	<-done
}
