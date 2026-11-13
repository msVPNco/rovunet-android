// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"errors"
	"net"
	"net/http"
	"time"
	wire "www.bamsoftware.com/git/dnstt.git/dns"
)

// DoH resolves destination names over verified HTTPS. The destination TCP
// connection is ordinary TCP, optionally carried by the preceding chain layer.
// It is explicitly not an encrypted full-traffic tunnel by itself.
func startDoH(o *owner, c config, dial func(context.Context, string) (net.Conn, error)) error {
	tr := http.DefaultTransport.(*http.Transport).Clone()
	return startDoHWithTransport(o, c, dial, tr)
}

// The transport parameter permits a fixture-owned root CA without modifying
// the Windows trust store or weakening the production system-root transport.
func startDoHWithTransport(o *owner, c config, dial func(context.Context, string) (net.Conn, error), tr *http.Transport) error {
	tr.Proxy = nil
	tr.DialContext = func(ctx context.Context, network, address string) (net.Conn, error) { return dial(ctx, address) }
	tr.ResponseHeaderTimeout = 10 * time.Second
	client := &http.Client{Transport: tr, Timeout: 15 * time.Second, CheckRedirect: func(_ *http.Request, _ []*http.Request) error { return errors.New("DoH redirect refused") }}
	o.stops = append(o.stops, tr.CloseIdleConnections)
	resolver := resolverProbe{address: c.Fields[21], transport: "https", client: client}
	return serveSocks(o, c.Port, func(ctx context.Context, address string) (net.Conn, error) {
		host, port, err := net.SplitHostPort(address)
		if err != nil {
			return nil, err
		}
		if net.ParseIP(host) != nil {
			return dial(ctx, address)
		}
		// No system-resolver or plaintext fallback for destination names.
		var last error = errors.New("DoH name resolution failed")
		for _, typ := range []uint16{wire.RRTypeA, wire.RRTypeAAAA} {
			answer, e := resolver.exchange(ctx, host, typ, 0)
			if e != nil {
				last = e
				continue
			}
			if answer.Rcode() != wire.RcodeNoError {
				continue
			}
			for _, record := range answer.Answer {
				if record.Class != wire.ClassIN || record.Type != typ {
					continue
				}
				if typ == wire.RRTypeA && len(record.Data) != 4 || typ == wire.RRTypeAAAA && len(record.Data) != 16 {
					continue
				}
				conn, e := dial(ctx, net.JoinHostPort(net.IP(record.Data).String(), port))
				if e == nil {
					return conn, nil
				}
				last = e
			}
		}
		return nil, last
	})
}
