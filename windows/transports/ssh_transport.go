// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"bufio"
	"context"
	"crypto/tls"
	"crypto/x509"
	"errors"
	"github.com/gorilla/websocket"
	"io"
	"net"
	"net/http"
	"strings"
	"time"
)

func sshTransport(ctx context.Context, f []string, addr string, dial func(context.Context, string) (net.Conn, error), ca string) (net.Conn, error) {
	tcp := addr
	if f[52] != "" {
		tcp = endpoint(f[52], f[53])
	}
	conn, e := dial(ctx, tcp)
	if e != nil {
		return nil, e
	}
	ok := false
	defer func() {
		if !ok {
			conn.Close()
		}
	}()
	_ = conn.SetDeadline(time.Now().Add(30 * time.Second))
	if f[52] != "" {
		host := addr
		if f[54] != "" {
			host = f[54]
		}
		if strings.ContainsAny(host, "\r\n") {
			return nil, errors.New("HEADER")
		}
		if _, e = io.WriteString(conn, "CONNECT "+addr+" HTTP/1.1\r\nHost: "+host+"\r\n\r\n"); e != nil {
			return nil, e
		}
		r := bufio.NewReaderSize(conn, 8192)
		var headers strings.Builder
		for headers.Len() < 16384 {
			line, err := r.ReadString('\n')
			if err != nil || len(line) > 4096 {
				return nil, errors.New("HEADER_LIMIT")
			}
			headers.WriteString(line)
			if line == "\r\n" {
				break
			}
		}
		if headers.Len() >= 16384 {
			return nil, errors.New("HEADER_LIMIT")
		}
		resp, e := http.ReadResponse(bufio.NewReader(strings.NewReader(headers.String())), &http.Request{Method: "CONNECT"})
		if e != nil || resp.StatusCode != 200 {
			return nil, errors.New("CONNECT")
		}
		conn = &readConn{Conn: conn, r: r}
	}
	if p := dec(f[59]); p != "" {
		if len(p) > 8192 {
			return nil, errors.New("PAYLOAD")
		}
		host, _, _ := net.SplitHostPort(addr)
		p = strings.NewReplacer("[host_port]", addr, "[host]", host, "[crlf]", "\r\n", "[lf]", "\n", "[cr]", "\r").Replace(p)
		if _, e = io.WriteString(conn, p); e != nil {
			return nil, e
		}
	}
	useTLS := f[50] == "1" || f[55] == "1" && f[57] == "1"
	if useTLS {
		sni := f[51]
		if sni == "" {
			sni = f[19]
			if sni == "" {
				sni = f[3]
			}
		}
		var roots *x509.CertPool
		if ca != "" {
			if len(ca) > 32768 || strings.Contains(ca, "PRIVATE KEY") {
				return nil, errors.New("TLS_CERT_INVALID")
			}
			roots, e = x509.SystemCertPool()
			if e != nil {
				roots = x509.NewCertPool()
			}
			if !roots.AppendCertsFromPEM([]byte(ca)) {
				return nil, errors.New("TLS_CERT_INVALID")
			}
		}
		secure := tls.Client(conn, &tls.Config{ServerName: sni, MinVersion: tls.VersionTLS12, RootCAs: roots})
		if e = secure.HandshakeContext(ctx); e != nil {
			return nil, e
		}
		conn = secure
	}
	if f[55] == "1" {
		host := f[58]
		if host == "" {
			host = f[19]
			if host == "" {
				host = f[3]
			}
		}
		path := f[56]
		if path == "" {
			path = "/"
		}
		d := websocket.Dialer{NetDialContext: func(context.Context, string, string) (net.Conn, error) { return conn, nil }, HandshakeTimeout: 20 * time.Second}
		ws, _, e := d.DialContext(ctx, "ws://"+host+path, nil)
		if e != nil {
			return nil, e
		}
		ws.SetReadLimit(1 << 20)
		conn = &wsConn{Conn: conn, ws: ws}
	}
	_ = conn.SetDeadline(time.Time{})
	ok = true
	return conn, nil
}

type readConn struct {
	net.Conn
	r *bufio.Reader
}

func (c *readConn) Read(p []byte) (int, error) { return c.r.Read(p) }

type wsConn struct {
	net.Conn
	ws *websocket.Conn
	r  io.Reader
}

func (c *wsConn) Read(p []byte) (int, error) {
	for {
		if c.r != nil {
			n, e := c.r.Read(p)
			if e == io.EOF {
				c.r = nil
				if n > 0 {
					return n, nil
				}
				continue
			}
			return n, e
		}
		kind, r, e := c.ws.NextReader()
		if e != nil {
			return 0, e
		}
		if kind != websocket.BinaryMessage {
			return 0, errors.New("WS_TYPE")
		}
		c.r = r
	}
}
func (c *wsConn) Write(p []byte) (int, error) {
	if e := c.ws.WriteMessage(websocket.BinaryMessage, p); e != nil {
		return 0, e
	}
	return len(p), nil
}
func (c *wsConn) Close() error { return c.ws.Close() }
