// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"errors"
	"golang.org/x/net/proxy"
	"net"
)

// A DNS tunnel stream ends at the server's chosen backend. SOCKS and SSH
// must negotiate over that stream, never over the host's default network.
type tunnelStream func(context.Context) (net.Conn, error)

func (d tunnelStream) Dial(_ string, _ string) (net.Conn, error)                      { return d(context.Background()) }
func (d tunnelStream) DialContext(ctx context.Context, _, _ string) (net.Conn, error) { return d(ctx) }
func backendSOCKS(stream tunnelStream, user, password string) (func(context.Context, string) (net.Conn, error), error) {
	var auth *proxy.Auth
	if user != "" || password != "" {
		auth = &proxy.Auth{User: user, Password: password}
	}
	d, err := proxy.SOCKS5("tcp", "backend.invalid:1", auth, stream)
	if err != nil {
		return nil, err
	}
	cd, ok := d.(proxy.ContextDialer)
	if !ok {
		return nil, errors.New("backend lacks cancellable dial")
	}
	return func(ctx context.Context, dest string) (net.Conn, error) { return cd.DialContext(ctx, "tcp", dest) }, nil
}
