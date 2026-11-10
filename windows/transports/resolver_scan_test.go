// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"crypto/ed25519"
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"encoding/base64"
	"encoding/hex"
	"io"
	"log/slog"
	"net"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	soarlib "github.com/getlantern/soar"
	wire "www.bamsoftware.com/git/dnstt.git/dns"
)

func TestPrismThresholdAndWire(t *testing.T) {
	key := make([]byte, 32)
	rand.Read(key)
	pc, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer pc.Close()
	var calls atomic.Int32
	var bad atomic.Bool
	done := make(chan struct{})
	go func() {
		defer close(done)
		buf := make([]byte, 4096)
		for {
			n, addr, err := pc.ReadFrom(buf)
			if err != nil {
				return
			}
			query, err := wire.MessageFromWireFormat(buf[:n])
			if err != nil {
				continue
			}
			q := query.Question[0]
			payload, err := scanEncoding.DecodeString(strings.ToUpper(string(q.Name[0])))
			if err != nil || len(payload) != 32 || len(query.Additional) != 1 || query.Additional[0].Class != 1232 || payload[14] != 3 || payload[15] != 132 {
				continue
			}
			mac := hmac.New(sha256.New, key)
			mac.Write(payload[:16])
			if !hmac.Equal(mac.Sum(nil)[:16], payload[16:]) {
				continue
			}
			calls.Add(1)
			mac.Write([]byte{1})
			data := mac.Sum(nil)
			if bad.Load() {
				data[6] ^= 1
			}
			reply := wire.Message{ID: query.ID, Flags: 0x8100, Question: query.Question, Answer: []wire.RR{{Name: q.Name, Class: q.Class, Type: q.Type, Data: wire.EncodeRDataTXT(data)}}}
			out, _ := reply.WireFormat()
			pc.WriteTo(out, addr)
		}
	}()
	resolver, unexpected, stop := tcpDNSFixture(t, pc.LocalAddr().String())
	defer stop()
	req := scanRequest{Family: "dnstt", Zone: "t.example.invalid", PublicKey: hex.EncodeToString(key), Mode: "prism", Candidates: []string{resolver}, Transports: []string{"tcp"}, Workers: 2, Count: 5, Threshold: 2, ResponseSize: 900, TimeoutMS: 1200}
	if !req.valid() {
		t.Fatal("valid request rejected")
	}
	run := func() scanResult {
		var row scanResult
		scanResolvers(context.Background(), req, func(r scanResult) { row = r })
		return row
	}
	row := run()
	if !row.Pass || row.Attempted != 2 || row.Passed != 2 || calls.Load() != 2 || unexpected.Load() != 0 {
		t.Fatal(row, calls.Load())
	}
	bad.Store(true)
	row = run()
	if row.Pass || row.Attempted != 4 || row.Passed != 0 || calls.Load() != 6 {
		t.Fatal(row, calls.Load())
	}
	pc.Close()
	<-done
}

func TestAdvancedAndIndependentTransports(t *testing.T) {
	pc, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer pc.Close()
	go func() {
		buf := make([]byte, 4096)
		for {
			n, addr, err := pc.ReadFrom(buf)
			if err != nil {
				return
			}
			q, err := wire.MessageFromWireFormat(buf[:n])
			if err != nil {
				continue
			}
			req := q.Question[0]
			reply := wire.Message{ID: q.ID, Flags: 0x8100, Question: q.Question, Additional: q.Additional}
			if strings.HasSuffix(req.Name.String(), ".invalid") && !strings.Contains(req.Name.String(), "example.invalid") {
				reply.Flags |= 3
			}
			if req.Type == 2 {
				reply.Answer = []wire.RR{{Name: req.Name, Class: 1, Type: 2, Data: []byte{0}}}
			}
			out, _ := reply.WireFormat()
			pc.WriteTo(out, addr)
		}
	}()
	resolver, _, stop := tcpDNSFixture(t, pc.LocalAddr().String())
	defer stop() // Its UDP listener discards every packet.
	req := scanRequest{Family: "sayedns", Zone: "t.example.invalid", Mode: "advanced", Candidates: []string{resolver}, Transports: []string{"udp", "tcp"}, Workers: 2, Count: 5, Threshold: 2, TimeoutMS: 900}
	var results []scanResult
	var mu sync.Mutex
	scanResolvers(context.Background(), req, func(r scanResult) { mu.Lock(); defer mu.Unlock(); results = append(results, r) })
	if len(results) != 2 || results[0].Transport != "tcp" || !results[0].Pass || results[0].Edns != 1232 || results[0].Completed != 6 || len(results[0].Checks) != 6 || results[1].Pass {
		t.Fatal(results)
	}
	ctx, cancel := context.WithCancel(context.Background())
	time.AfterFunc(40*time.Millisecond, cancel)
	before := time.Now()
	scanResolvers(ctx, req, func(scanResult) {})
	if time.Since(before) > 400*time.Millisecond {
		t.Fatal("cancellation waited for a whole timeout")
	}
}

func TestSoarPrismAuthenticatedTCP(t *testing.T) {
	pub, key, err := ed25519.GenerateKey(rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	pc, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer pc.Close()
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	go soarlib.Serve(ctx, pc, soarlib.ServerConfig{Zone: "t.example.invalid", PrivateKey: key, Dial: (&net.Dialer{}).DialContext, Logger: slog.New(slog.NewTextHandler(io.Discard, nil))})
	resolver, unexpected, stop := tcpDNSFixture(t, pc.LocalAddr().String())
	defer stop()
	req := scanRequest{Family: "soar", Zone: "t.example.invalid", PublicKey: base64.StdEncoding.EncodeToString(pub), Mode: "prism", Candidates: []string{resolver}, Transports: []string{"tcp"}, Workers: 2, Count: 5, Threshold: 2, TimeoutMS: 2000}
	var row scanResult
	scanResolvers(ctx, req, func(r scanResult) { row = r })
	if !row.Handshake || !row.Pass || row.Attempted != 2 || unexpected.Load() != 0 {
		t.Fatal(row, unexpected.Load())
	}
	pub[3] ^= 1
	req.PublicKey = base64.StdEncoding.EncodeToString(pub)
	req.Count = 2
	req.Threshold = 2
	req.TimeoutMS = 600
	scanResolvers(ctx, req, func(r scanResult) { row = r })
	if row.Pass || row.Handshake || row.Attempted != 1 || unexpected.Load() != 0 {
		t.Fatal(row)
	}
}

func TestDNSLocalSocketDoesNotProveRemoteHandshake(t *testing.T) {
	pc, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer pc.Close()
	for _, family := range []string{"dnstt", "sayedns", "soar"} {
		t.Run(family, func(t *testing.T) {
			f := fixtureFields("127.0.0.1:22")
			f[1], f[3], f[4], f[11], f[22] = family, "t.example.invalid", pc.LocalAddr().String()+":0", strings.Repeat("ab", 32), "udp"
			f[79], f[81] = "0", "off"
			p, err := freePort()
			if err != nil {
				t.Fatal(err)
			}
			ctx, cancel := context.WithTimeout(context.Background(), 300*time.Millisecond)
			defer cancel()
			o := newOwner(ctx)
			defer o.stop()
			result, err := start(o, config{Fields: f, Port: p})
			if err == nil || result.Error != "DNS_TUNNEL_FAILED" {
				t.Fatal("local listener certified as tunnel", result.Error)
			}
		})
	}
}
