// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"encoding/binary"
	"errors"
	"io"
	"net"
	"strconv"
	"time"
)

func serveSocks(o *owner, p int, dial func(context.Context, string) (net.Conn, error)) error {
	return serveSocksWithin(o, p, dial, 25*time.Second)
}

func serveSocksWithin(o *owner, p int, dial func(context.Context, string) (net.Conn, error), timeout time.Duration) error {
	l, e := net.Listen("tcp", endpoint("127.0.0.1", strconv.Itoa(p)))
	if e != nil {
		return e
	}
	o.add(l)
	o.wg.Add(1)
	go func() {
		defer o.wg.Done()
		for {
			c, e := l.Accept()
			if e != nil {
				return
			}
			select {
			case o.slots <- struct{}{}:
			default:
				c.Close()
				continue
			}
			o.add(c)
			o.wg.Add(1)
			go func() {
				defer o.wg.Done()
				defer func() { <-o.slots }()
				defer o.drop(c)
				_ = c.SetDeadline(time.Now().Add(timeout))
				addr, e := socksRequest(c)
				if e != nil {
					return
				}
				ctx, cancel := context.WithTimeout(o.ctx, timeout)
				remote, e := dial(ctx, addr)
				cancel()
				if e != nil {
					c.Write([]byte{5, 5, 0, 1, 0, 0, 0, 0, 0, 0})
					return
				}
				o.add(remote)
				defer o.drop(remote)
				if _, e = c.Write([]byte{5, 0, 0, 1, 127, 0, 0, 1, 0, 0}); e != nil {
					return
				}
				_ = c.SetDeadline(time.Time{})
				done := make(chan struct{})
				go func() { io.Copy(remote, c); remote.Close(); close(done) }()
				io.Copy(c, remote)
				c.Close()
				<-done
			}()
		}
	}()
	return nil
}
func socksRequest(c net.Conn) (string, error) {
	var h [2]byte
	if _, e := io.ReadFull(c, h[:]); e != nil || h[0] != 5 || h[1] == 0 {
		return "", errors.New("SOCKS")
	}
	methods := make([]byte, int(h[1]))
	if _, e := io.ReadFull(c, methods); e != nil {
		return "", e
	}
	noauth := false
	for _, m := range methods {
		if m == 0 {
			noauth = true
		}
	}
	if !noauth {
		c.Write([]byte{5, 255})
		return "", errors.New("AUTH")
	}
	c.Write([]byte{5, 0})
	var req [4]byte
	if _, e := io.ReadFull(c, req[:]); e != nil || req[0] != 5 || req[1] != 1 || req[2] != 0 {
		return "", errors.New("CMD")
	}
	var host string
	switch req[3] {
	case 1:
		b := make([]byte, 4)
		if _, e := io.ReadFull(c, b); e != nil {
			return "", e
		}
		host = net.IP(b).String()
	case 4:
		b := make([]byte, 16)
		if _, e := io.ReadFull(c, b); e != nil {
			return "", e
		}
		host = net.IP(b).String()
	case 3:
		var n [1]byte
		if _, e := io.ReadFull(c, n[:]); e != nil || n[0] == 0 {
			return "", errors.New("ADDR")
		}
		b := make([]byte, int(n[0]))
		if _, e := io.ReadFull(c, b); e != nil {
			return "", e
		}
		host = string(b)
	default:
		return "", errors.New("ADDR")
	}
	var port [2]byte
	if _, e := io.ReadFull(c, port[:]); e != nil {
		return "", e
	}
	return endpoint(host, strconv.Itoa(int(binary.BigEndian.Uint16(port[:])))), nil
}

// Bounded raw front door for modules whose own listener is private to this host.
func serveRaw(o *owner, port int, upstream string) error {
	l, e := net.Listen("tcp", endpoint("127.0.0.1", strconv.Itoa(port)))
	if e != nil {
		return e
	}
	o.add(l)
	o.wg.Add(1)
	go func() {
		defer o.wg.Done()
		for {
			c, e := l.Accept()
			if e != nil {
				return
			}
			select {
			case o.slots <- struct{}{}:
			default:
				c.Close()
				continue
			}
			o.add(c)
			o.wg.Add(1)
			go func() {
				defer o.wg.Done()
				defer func() { <-o.slots }()
				defer o.drop(c)
				d := net.Dialer{Timeout: 5 * time.Second}
				r, e := d.DialContext(o.ctx, "tcp", upstream)
				if e != nil {
					return
				}
				o.add(r)
				defer o.drop(r)
				done := make(chan struct{})
				go func() { io.Copy(r, c); r.Close(); close(done) }()
				io.Copy(c, r)
				c.Close()
				<-done
			}()
		}
	}()
	return nil
}
