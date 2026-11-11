// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"errors"
	"net"
	"net/http"
	"strconv"
	"strings"
	"sync"
	"time"

	vay "github.com/net2share/vaydns/client"
	"github.com/sirupsen/logrus"
	"io"
)

// Only the CC0 upstream client is used. Keep one authenticated multiplexed
// session for every TCP connection of this profile.
func startVay(o *owner, f []string) (tunnelStream, error) {
	if f[22] == "https" || !strings.Contains(f[4], ",") {
		return startVaySingle(o, f)
	}
	candidates := strings.Split(f[4], ",")
	if len(candidates) > 16 {
		return nil, errors.New("resolver limit")
	}
	started := time.Now()
	for _, candidate := range candidates {
		if o.ctx.Err() != nil {
			return nil, o.ctx.Err()
		}
		if time.Since(started) >= 25*time.Second {
			return nil, errors.New("resolver startup budget")
		}
		attempt := newOwner(o.ctx)
		fields := append([]string(nil), f...)
		fields[4] = candidate
		stream, err := startVaySingle(attempt, fields)
		if err == nil {
			o.stops = append(o.stops, attempt.stop)
			return stream, nil
		}
		attempt.stop()
	}
	return nil, errors.New("no authenticated resolver")
}

func startVaySingle(o *owner, f []string) (tunnelStream, error) {
	addr, err := dnsAddress(f)
	if err != nil || strings.Contains(addr, ",") {
		return nil, errors.New("single resolver required")
	}
	kind := vay.ResolverTypeUDP
	switch f[22] {
	case "udp":
	case "tcp":
		kind = vay.ResolverTypeTCP
		addr = strings.TrimPrefix(addr, "tcp://")
	case "tls":
		kind = vay.ResolverTypeDOT
		addr = strings.TrimPrefix(addr, "tls://")
	case "https":
		kind = vay.ResolverTypeDOH
	default:
		return nil, errors.New("unsupported VayDNS transport")
	}
	resolver, err := vay.NewResolver(kind, addr)
	if err != nil {
		return nil, err
	}
	resolver.UDPWorkers = 4
	resolver.UDPTimeout = time.Duration(num(f[47])) * time.Millisecond
	if kind == vay.ResolverTypeDOH {
		tr := http.DefaultTransport.(*http.Transport).Clone()
		tr.Proxy = nil
		resolver.RoundTripper = tr
		o.stops = append(o.stops, tr.CloseIdleConnections)
	}
	server, err := vay.NewTunnelServer(f[3], f[11])
	if err != nil {
		return nil, err
	}
	server.DnsttCompat = f[41] == "1"
	server.RecordType = f[42]
	server.MaxQnameLen = num(f[43])
	server.MaxNumLabels = num(f[48])
	server.ClientIDSize = num(f[49])
	if f[44] != "" {
		server.RPS, err = strconv.ParseFloat(f[44], 64)
		if err != nil {
			return nil, err
		}
	}
	tunnel, err := vay.NewTunnel(resolver, server)
	if err != nil {
		return nil, err
	}
	tunnel.MaxStreams = 64
	tunnel.HandshakeTimeout = 12 * time.Second
	tunnel.OpenStreamTimeout = 8 * time.Second
	tunnel.KeepAlive = time.Duration(num(f[46])) * time.Second
	tunnel.IdleTimeout = time.Duration(num(f[45])) * time.Second
	logrus.SetOutput(io.Discard)
	var lock sync.RWMutex
	o.stops = append(o.stops, func() { lock.Lock(); defer lock.Unlock(); tunnel.Close() })
	for _, step := range []func() error{tunnel.InitiateResolverConnection, func() error { return tunnel.InitiateDNSPacketConn(server.Addr) }, func() error { return tunnel.InitiateKCPConn(num(f[39])) }, tunnel.InitiateNoiseChannel, tunnel.InitiateSmuxSession} {
		if err := o.ctx.Err(); err != nil {
			return nil, err
		}
		if err := step(); err != nil {
			return nil, err
		}
	}
	return func(ctx context.Context) (net.Conn, error) {
		lock.RLock()
		defer lock.RUnlock()
		if err := ctx.Err(); err != nil {
			return nil, err
		}
		return tunnel.OpenStream()
	}, nil
}
