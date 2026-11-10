// SPDX-License-Identifier: AGPL-3.0-only
// Wire behaviour follows Rovunet Android VlessBridge/SniFragmentForwarder.
package main

import (
	"context"
	"crypto/rand"
	"crypto/tls"
	"encoding/binary"
	"encoding/hex"
	"errors"
	"github.com/gorilla/websocket"
	"io"
	mathrand "math/rand/v2"
	"net"
	"net/http"
	"strconv"
	"strings"
	"time"
)

func vlessDial(ctx context.Context, c config, addr string) (net.Conn, error) {
	f := c.Fields
	host := f[66]
	if host == "" {
		host = f[3]
	}
	d := net.Dialer{Timeout: 20 * time.Second}
	raw, e := d.DialContext(ctx, "tcp", endpoint(host, f[67]))
	if e != nil {
		return nil, e
	}
	ok := false
	defer func() {
		if !ok {
			raw.Close()
		}
	}()
	_ = raw.SetDeadline(time.Now().Add(30 * time.Second))
	var conn net.Conn = raw
	if f[63] == "tls" {
		if f[68] == "1" || f[72] == "1" {
			strategy := f[69]
			if f[72] == "1" {
				strategy = "micro"
			}
			conn = &fragmentConn{Conn: conn, strategy: strategy, delay: num(f[70])}
		}
		sni := f[78]
		if sni == "" {
			sni = f[3]
		}
		secure := tls.Client(conn, &tls.Config{ServerName: sni, MinVersion: tls.VersionTLS12})
		if e = secure.HandshakeContext(ctx); e != nil {
			return nil, e
		}
		conn = secure
	}
	h := http.Header{}
	if f[73] == "1" {
		h.Set("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/154.0.0.0 Safari/537.36")
		h.Set("Accept-Language", "en-US,en;q=0.9")
		h.Set("Accept-Encoding", "gzip, deflate, br")
		h.Set("Cache-Control", "no-cache")
		h.Set("Pragma", "no-cache")
	}
	if f[73] == "1" {
		conn = &obfuscatedConn{Conn: conn}
	}
	wd := websocket.Dialer{NetDialContext: func(context.Context, string, string) (net.Conn, error) { return conn, nil }, HandshakeTimeout: 20 * time.Second}
	ws, _, e := wd.DialContext(ctx, "ws://"+f[3]+f[65], h)
	if e != nil {
		return nil, e
	}
	ws.SetReadLimit(1 << 20)
	stream := &wsConn{Conn: conn, ws: ws}
	id, e := hex.DecodeString(strings.ReplaceAll(f[62], "-", ""))
	if e != nil || len(id) != 16 {
		return nil, errors.New("UUID")
	}
	target, p, e := net.SplitHostPort(addr)
	if e != nil {
		return nil, e
	}
	port, _ := strconv.Atoi(p)
	head := append([]byte{0}, id...)
	head = append(head, 0, 1, byte(port>>8), byte(port))
	ip := net.ParseIP(target)
	if ip4 := ip.To4(); ip4 != nil {
		head = append(head, 1)
		head = append(head, ip4...)
	} else if ip != nil {
		head = append(head, 3)
		head = append(head, ip.To16()...)
	} else {
		if len(target) > 255 {
			return nil, errors.New("HOST")
		}
		head = append(head, 2, byte(len(target)))
		head = append(head, []byte(target)...)
	}
	if _, e = stream.Write(head); e != nil {
		return nil, e
	}
	_ = conn.SetDeadline(time.Time{})
	ok = true
	if f[74] == "1" {
		go func() {
			for {
				var v [2]byte
				rand.Read(v[:])
				time.Sleep(time.Duration(500+binary.BigEndian.Uint16(v[:])%2000) * time.Millisecond)
				b := make([]byte, 4+int(v[0])%120)
				rand.Read(b)
				if e := ws.WriteControl(websocket.PingMessage, b, time.Now().Add(5*time.Second)); e != nil {
					return
				}
			}
		}()
	}
	return &vlessConn{wsConn: stream}, nil
}

type fragmentConn struct {
	net.Conn
	strategy string
	delay    int
	sent     bool
}

func (c *fragmentConn) Write(p []byte) (int, error) {
	if c.sent || len(p) < 6 || p[0] != 22 || p[5] != 1 {
		return c.Conn.Write(p)
	}
	c.sent = true
	length := int(binary.BigEndian.Uint16(p[3:5]))
	if length+5 > len(p) {
		return 0, errors.New("TLS_RECORD")
	}
	payload := p[5 : 5+length]
	var cuts []int
	switch c.strategy {
	case "micro":
		for i := 1; i < len(payload); i++ {
			cuts = append(cuts, i)
		}
	case "half":
		cuts = []int{1, 1 + (len(payload)-1)/2}
	case "multi":
		cuts = []int{1}
		for i := 1 + 16 + mathrand.IntN(25); i < len(payload); i += 16 + mathrand.IntN(25) {
			cuts = append(cuts, i)
		}
	case "sni_split":
		cuts = []int{1, sniMiddle(payload)}
	default:
		return 0, errors.New("FRAGMENT")
	}
	cuts = append(cuts, len(payload))
	start := 0
	for i, end := range cuts {
		if end <= start || end > len(payload) {
			continue
		}
		frame := []byte{22, p[1], p[2], byte((end - start) >> 8), byte(end - start)}
		frame = append(frame, payload[start:end]...)
		for len(frame) > 0 {
			n, e := c.Conn.Write(frame)
			if e != nil {
				return 0, e
			}
			frame = frame[n:]
		}
		start = end
		if i < len(cuts)-1 {
			delay := c.delay / 2
			if c.delay > 1 {
				delay += mathrand.IntN(c.delay / 2)
			}
			if c.strategy == "micro" {
				maximum := c.delay / 10
				if maximum < 1 {
					maximum = 1
				}
				if maximum > 10 {
					maximum = 10
				}
				delay = 1 + mathrand.IntN(maximum)
			}
			if delay > 0 {
				time.Sleep(time.Duration(delay) * time.Millisecond)
			}
		}
	}
	if len(p) > 5+length {
		if _, e := c.Conn.Write(p[5+length:]); e != nil {
			return 0, e
		}
	}
	return len(p), nil
}
func sniMiddle(p []byte) int {
	// ClientHello fixed fields, session ID, cipher suites, compression, extensions.
	fallback := len(p) / 2
	if len(p) < 39 {
		return fallback
	}
	i := 38
	i += 1 + int(p[i])
	if i+2 > len(p) {
		return fallback
	}
	i += 2 + int(binary.BigEndian.Uint16(p[i:i+2]))
	if i >= len(p) {
		return fallback
	}
	i += 1 + int(p[i])
	if i+2 > len(p) {
		return fallback
	}
	i += 2
	for i+4 <= len(p) {
		typ := binary.BigEndian.Uint16(p[i : i+2])
		n := int(binary.BigEndian.Uint16(p[i+2 : i+4]))
		i += 4
		if i+n > len(p) {
			return fallback
		}
		if typ == 0 && n > 7 {
			return i + 5 + 1 + mathrand.IntN(n-6)
		}
		i += n
	}
	return fallback
}

// Reorder the real HTTP upgrade headers before TLS encrypts them; no identity changes.
type obfuscatedConn struct {
	net.Conn
	sent bool
}

func (c *obfuscatedConn) Write(p []byte) (int, error) {
	if c.sent {
		return c.Conn.Write(p)
	}
	c.sent = true
	text := string(p)
	if !strings.HasPrefix(text, "GET ") || !strings.HasSuffix(text, "\r\n\r\n") {
		return 0, errors.New("WS_UPGRADE")
	}
	lines := strings.Split(strings.TrimSuffix(text, "\r\n\r\n"), "\r\n")
	mathrand.Shuffle(len(lines)-1, func(i, j int) { lines[i+1], lines[j+1] = lines[j+1], lines[i+1] })
	b := []byte(strings.Join(lines, "\r\n") + "\r\n\r\n")
	for len(b) > 0 {
		n, e := c.Conn.Write(b)
		if e != nil {
			return 0, e
		}
		b = b[n:]
	}
	return len(p), nil
}

// Xray may defer the VLESS response until origin data arrives. Read it lazily:
// waiting before returning SOCKS success would deadlock a TLS client.
type vlessConn struct {
	*wsConn
	responseRead bool
}

func (c *vlessConn) Read(p []byte) (int, error) {
	if !c.responseRead {
		var response [2]byte
		if _, e := io.ReadFull(c.wsConn, response[:]); e != nil {
			return 0, e
		}
		if response[0] != 0 {
			return 0, errors.New("VLESS_AUTH")
		}
		if response[1] > 0 {
			if _, e := io.CopyN(io.Discard, c.wsConn, int64(response[1])); e != nil {
				return 0, e
			}
		}
		c.responseRead = true
	}
	return c.wsConn.Read(p)
}
