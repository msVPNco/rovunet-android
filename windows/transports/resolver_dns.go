// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"bytes"
	"context"
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"crypto/tls"
	"encoding/base32"
	"encoding/binary"
	"encoding/hex"
	"errors"
	"io"
	"mime"
	"net"
	"net/http"
	"net/netip"
	"net/url"
	"strings"
	"sync"
	"time"

	wire "www.bamsoftware.com/git/dnstt.git/dns"
)

var scanInvalid = errors.New("invalid resolver response")
var scanEncoding = base32.StdEncoding.WithPadding(base32.NoPadding)

type resolverProbe struct {
	address, transport string
	client             *http.Client
}

func (p resolverProbe) exchange(ctx context.Context, name string, typ, edns uint16) (*wire.Message, error) {
	return p.exchangeCapture(ctx, name, typ, edns, nil)
}

func (p resolverProbe) exchangeCapture(ctx context.Context, name string, typ, edns uint16, capture *[]byte) (*wire.Message, error) {
	n, err := wire.ParseName(name)
	if err != nil {
		return nil, err
	}
	var id [2]byte
	if _, err = rand.Read(id[:]); err != nil {
		return nil, err
	}
	q := wire.Question{Name: n, Type: typ, Class: wire.ClassIN}
	m := wire.Message{ID: binary.BigEndian.Uint16(id[:]), Flags: 0x0100, Question: []wire.Question{q}}
	if edns != 0 {
		m.Additional = []wire.RR{{Type: wire.RRTypeOPT, Class: edns}}
	}
	payload, err := m.WireFormat()
	if err != nil {
		return nil, err
	}
	var response []byte
	if p.transport == "https" {
		u, err := url.Parse(p.address)
		if err != nil || u.Scheme != "https" || u.Hostname() == "" || u.User != nil || u.Fragment != "" {
			return nil, scanInvalid
		}
		req, err := http.NewRequestWithContext(ctx, http.MethodPost, u.String(), bytes.NewReader(payload))
		if err != nil {
			return nil, err
		}
		req.Header.Set("Content-Type", "application/dns-message")
		req.Header.Set("Accept", "application/dns-message")
		res, err := p.client.Do(req)
		if err != nil {
			return nil, err
		}
		defer res.Body.Close()
		kind, _, _ := mime.ParseMediaType(res.Header.Get("Content-Type"))
		if res.StatusCode != http.StatusOK || kind != "application/dns-message" {
			return nil, scanInvalid
		}
		response, err = io.ReadAll(io.LimitReader(res.Body, 4097))
		if err != nil {
			return nil, err
		}
	} else {
		addr, err := netip.ParseAddrPort(p.address)
		if err != nil || !addr.IsValid() || addr.Port() == 0 {
			return nil, scanInvalid
		}
		var c net.Conn
		dialer := new(net.Dialer)
		switch p.transport {
		case "udp", "tcp":
			c, err = dialer.DialContext(ctx, p.transport, p.address)
		case "tls":
			c, err = (&tls.Dialer{NetDialer: dialer, Config: &tls.Config{MinVersion: tls.VersionTLS12, ServerName: addr.Addr().String()}}).DialContext(ctx, "tcp", p.address)
		default:
			return nil, scanInvalid
		}
		if err != nil {
			return nil, err
		}
		defer c.Close()
		stop := context.AfterFunc(ctx, func() { c.Close() })
		defer stop()
		if deadline, ok := ctx.Deadline(); ok {
			_ = c.SetDeadline(deadline)
		}
		if p.transport == "udp" {
			if _, err = c.Write(payload); err != nil {
				return nil, err
			}
			buf := make([]byte, 4097)
			n, err := c.Read(buf)
			if err != nil {
				return nil, err
			}
			response = buf[:n]
		} else {
			frame := binary.BigEndian.AppendUint16(nil, uint16(len(payload)))
			if _, err = io.Copy(c, bytes.NewReader(append(frame, payload...))); err != nil {
				return nil, err
			}
			if _, err = io.ReadFull(c, frame); err != nil {
				return nil, err
			}
			size := binary.BigEndian.Uint16(frame)
			if size < 12 || size > 4096 {
				return nil, scanInvalid
			}
			response = make([]byte, size)
			if _, err = io.ReadFull(c, response); err != nil {
				return nil, err
			}
		}
	}
	if len(response) > 4096 {
		return nil, scanInvalid
	}
	r, err := wire.MessageFromWireFormat(response)
	if err != nil || r.ID != m.ID || r.Flags&0x8000 == 0 || r.Flags&0x0200 != 0 || r.Opcode() != 0 || len(r.Question) != 1 {
		return nil, scanInvalid
	}
	echo := r.Question[0]
	if echo.Type != typ || echo.Class != q.Class || !strings.EqualFold(echo.Name.String(), n.String()) {
		return nil, scanInvalid
	}
	if capture != nil {
		*capture = response
	}
	return &r, nil
}

func randomLabel(bytesWanted int) string {
	data := make([]byte, bytesWanted)
	if _, err := rand.Read(data); err != nil {
		panic(err)
	}
	return strings.ToLower(scanEncoding.EncodeToString(data))
}

// The server recognizes a TXT challenge keyed by the profile's public Noise key.
// It tests compatibility with the Prism responder; it is not a secret-key identity proof.
func (p resolverProbe) prism(ctx context.Context, zone string, key []byte, size int) bool {
	var nonce [16]byte
	if _, err := rand.Read(nonce[:]); err != nil {
		return false
	}
	binary.BigEndian.PutUint16(nonce[14:], uint16(size))
	mac := hmac.New(sha256.New, key)
	mac.Write(nonce[:])
	request := append(append([]byte{}, nonce[:]...), mac.Sum(nil)[:16]...)
	name := strings.ToLower(scanEncoding.EncodeToString(request)) + "." + zone
	result, err := p.exchange(ctx, name, wire.RRTypeTXT, uint16(max(size, 1232)))
	if err != nil || result.Rcode() != wire.RcodeNoError {
		return false
	}
	mac.Reset()
	mac.Write(nonce[:])
	mac.Write([]byte{1})
	expected := mac.Sum(nil)
	for _, rr := range result.Answer {
		if rr.Type != wire.RRTypeTXT || rr.Class != wire.ClassIN || !strings.EqualFold(rr.Name.String(), name) {
			continue
		}
		actual, err := wire.DecodeRDataTXT(rr.Data)
		if err == nil && len(actual) >= len(expected) && hmac.Equal(expected, actual[:len(expected)]) {
			return true
		}
	}
	return false
}

func prismKey(raw string) ([]byte, error) {
	key, err := hex.DecodeString(raw)
	if err != nil || len(key) != 32 {
		return nil, scanInvalid
	}
	return key, nil
}

func (p resolverProbe) diagnostic(ctx context.Context, zone string, row *scanResult, querySizes ...int) {
	parent := zone
	if parts := strings.Split(zone, "."); len(parts) > 2 {
		parent = strings.Join(parts[1:], ".")
	}
	check := func(name string, typ, edns uint16) (*wire.Message, bool) {
		reply, err := p.exchange(ctx, name, typ, edns)
		return reply, err == nil
	}
	started := time.Now()
	_, row.Dns = check(randomLabel(6)+"."+parent, wire.RRTypeA, 0)
	if !row.Dns {
		return
	}
	row.DnsElapsedMS = max(1, time.Since(started).Milliseconds())
	// Match the mobile six independent diagnostics under one shared deadline.
	// A silent NS server must not prevent a fast TXT or EDNS result from being observed.
	type outcome struct {
		label string
		pass  bool
		edns  int
	}
	results := make(chan outcome, 6)
	var group sync.WaitGroup
	launch := func(label string, test func() (bool, int)) {
		group.Add(1)
		go func() {
			defer group.Done()
			ok, edns := test()
			if ctx.Err() == nil {
				results <- outcome{label, ok, edns}
			}
		}()
	}
	launch("NS", func() (bool, int) {
		var packet []byte
		reply, err := p.exchangeCapture(ctx, parent, 2, 0, &packet)
		if err != nil || reply.Rcode() != 0 {
			return false, 0
		}
		host, err := scannerNSHost(packet)
		if err != nil {
			return false, 0
		}
		glue, ok := check(host, wire.RRTypeA, 0)
		return ok && glue.Rcode() == 0 && hasRR(glue, wire.RRTypeA), 0
	})
	launch("TXT", func() (bool, int) {
		_, ok := check(randomLabel(6)+"."+parent, wire.RRTypeTXT, 0)
		return ok, 0
	})
	launch("A", func() (bool, int) {
		for i := 0; i < 2 && ctx.Err() == nil; i++ {
			_, ok := check(randomLabel(6)+"."+randomLabel(6)+"."+zone, wire.RRTypeA, 0)
			if ok {
				return true, 0
			}
		}
		return false, 0
	})
	launch("LONG-TXT", func() (bool, int) {
		size := 130
		if len(querySizes) > 0 && querySizes[0] > 0 {
			size = min(130, max(8, max(10, querySizes[0]-16-len(zone)-2)*5/9))
		}
		encoded := randomLabel(size)
		labels := []string{}
		for len(encoded) > 0 {
			n := min(57, len(encoded))
			labels = append(labels, encoded[:n])
			encoded = encoded[n:]
		}
		_, ok := check(strings.Join(labels, ".")+"."+zone, wire.RRTypeTXT, 0)
		return ok, 0
	})
	launch("NXDOMAIN", func() (bool, int) {
		passed := 0
		for i := 0; i < 3 && passed < 2 && passed+3-i >= 2 && ctx.Err() == nil; i++ {
			reply, ok := check(randomLabel(8)+".invalid", wire.RRTypeA, 0)
			if ok && reply.Rcode() == wire.RcodeNameError {
				passed++
			}
		}
		return passed >= 2, 0
	})
	launch("EDNS", func() (bool, int) {
		size := 0
		for _, n := range []uint16{512, 900, 1232} {
			if ctx.Err() != nil {
				break
			}
			reply, ok := check(randomLabel(6)+"."+zone, wire.RRTypeTXT, n)
			if ok && reply.Rcode() != wire.RcodeFormatError && hasRR(reply, wire.RRTypeOPT) {
				size = int(n)
			}
		}
		return size > 0, size
	})
	group.Wait()
	close(results)
	byName := map[string]outcome{}
	for result := range results {
		byName[result.label] = result
	}
	for _, label := range []string{"NS", "TXT", "A", "LONG-TXT", "NXDOMAIN", "EDNS"} {
		if result, ok := byName[label]; ok {
			row.Completed++
			row.CompletedChecks = append(row.CompletedChecks, label)
			if result.pass {
				row.Checks = append(row.Checks, label)
			}
			if result.edns > 0 {
				row.Edns = result.edns
			}
		}
	}
}

// NS RDATA can contain compression pointers into the original packet. Decode
// there, with bounded pointer traversal, before checking the first NS's A glue.
func scannerNSHost(packet []byte) (string, error) {
	name := func(start int) (string, int, error) {
		labels := []string{}
		cursor := start
		end := -1
		octets := 1
		for steps := 0; steps < 128; steps++ {
			if cursor < 0 || cursor >= len(packet) {
				return "", 0, scanInvalid
			}
			n := int(packet[cursor])
			cursor++
			if n == 0 {
				if end < 0 {
					end = cursor
				}
				return strings.Join(labels, "."), end, nil
			}
			if n&0xc0 == 0xc0 {
				if cursor >= len(packet) || steps > 10 {
					return "", 0, scanInvalid
				}
				if end < 0 {
					end = cursor + 1
				}
				cursor = (n&63)<<8 | int(packet[cursor])
				continue
			}
			if n > 63 || cursor+n > len(packet) || octets+n+1 > 255 {
				return "", 0, scanInvalid
			}
			labels = append(labels, string(packet[cursor:cursor+n]))
			cursor += n
			octets += n + 1
		}
		return "", 0, scanInvalid
	}
	if len(packet) < 12 {
		return "", scanInvalid
	}
	cursor := 12
	for i := 0; i < int(binary.BigEndian.Uint16(packet[4:6])); i++ {
		_, end, err := name(cursor)
		if err != nil || end+4 > len(packet) {
			return "", scanInvalid
		}
		cursor = end + 4
	}
	count := int(binary.BigEndian.Uint16(packet[6:8])) + int(binary.BigEndian.Uint16(packet[8:10]))
	for i := 0; i < count; i++ {
		_, end, err := name(cursor)
		if err != nil || end+10 > len(packet) {
			return "", scanInvalid
		}
		size := int(binary.BigEndian.Uint16(packet[end+8 : end+10]))
		start := end + 10
		if start+size > len(packet) {
			return "", scanInvalid
		}
		if binary.BigEndian.Uint16(packet[end:end+2]) == 2 && binary.BigEndian.Uint16(packet[end+2:end+4]) == wire.ClassIN {
			host, after, err := name(start)
			if err == nil && host != "" && after == start+size {
				return host, nil
			}
			return "", scanInvalid
		}
		cursor = start + size
	}
	return "", scanInvalid
}

func hasRR(reply *wire.Message, typ uint16) bool {
	for _, section := range [][]wire.RR{reply.Answer, reply.Authority, reply.Additional} {
		for _, entry := range section {
			if entry.Type == typ {
				return true
			}
		}
	}
	return false
}

// All HTTPS probes in one scan reuse owned sockets, without following redirects or system proxy settings.
func scanHTTPClient() (*http.Client, func()) {
	tr := &http.Transport{MaxConnsPerHost: 4, MaxIdleConns: 8, IdleConnTimeout: 5 * time.Second, TLSClientConfig: &tls.Config{MinVersion: tls.VersionTLS12}, ForceAttemptHTTP2: true}
	return &http.Client{Transport: tr, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}, tr.CloseIdleConnections
}
