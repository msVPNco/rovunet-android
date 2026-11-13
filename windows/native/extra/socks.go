package main

import (
	"io"
	"net"
	"sync"
	"time"

	"github.com/metacubex/mihomo/adapter/inbound"
	"github.com/metacubex/mihomo/component/auth"
	C "github.com/metacubex/mihomo/constant"
	"github.com/metacubex/mihomo/transport/socks5"
	"github.com/metacubex/mihomo/tunnel"
)

// Each UDP relay belongs to one authenticated TCP association. There is no
// unauthenticated shared UDP listener on the public local SOCKS port.
type localListener struct {
	net.Listener
	mu      sync.Mutex
	clients map[net.Conn]struct{}
	closed  bool
}
type replyAddress struct {
	net.Conn
	address net.Addr
}

func (c replyAddress) LocalAddr() net.Addr { return c.address }
func (l *localListener) Close() error {
	l.mu.Lock()
	defer l.mu.Unlock()
	l.closed = true
	for c := range l.clients {
		c.Close()
	}
	return l.Listener.Close()
}
func newLocalListener(address string, authenticator auth.Authenticator) (*localListener, error) {
	listener, err := net.Listen("tcp", address)
	if err != nil {
		return nil, err
	}
	l := &localListener{Listener: listener, clients: make(map[net.Conn]struct{})}
	go func() {
		for {
			c, e := listener.Accept()
			if e != nil {
				return
			}
			l.mu.Lock()
			if l.closed || len(l.clients) >= 512 {
				l.mu.Unlock()
				c.Close()
				continue
			}
			l.clients[c] = struct{}{}
			l.mu.Unlock()
			go func() {
				defer func() { c.Close(); l.mu.Lock(); delete(l.clients, c); l.mu.Unlock() }()
				serveLocal(c, authenticator)
			}()
		}
	}()
	return l, nil
}
func serveLocal(c net.Conn, authenticator auth.Authenticator) {
	udp, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
	if err != nil {
		return
	}
	defer udp.Close()
	c.SetDeadline(time.Now().Add(10 * time.Second))
	target, command, user, err := socks5.ServerHandshake(replyAddress{c, udp.LocalAddr()}, authenticator)
	if err != nil {
		return
	}
	c.SetDeadline(time.Time{})
	if command == socks5.CmdConnect {
		udp.Close()
		tunnel.Tunnel.HandleTCPConn(inbound.NewSocket(target, c, C.SOCKS5, inbound.WithInUser(user)))
		return
	}
	if command != socks5.CmdUDPAssociate {
		return
	}
	go func() {
		buf := make([]byte, 65535)
		var client *net.UDPAddr
		for {
			n, remote, e := udp.ReadFromUDP(buf)
			if e != nil {
				return
			}
			if !remote.IP.IsLoopback() {
				continue
			}
			if client == nil {
				client = remote
			} else if !remote.IP.Equal(client.IP) || remote.Port != client.Port {
				continue
			}
			data := append([]byte(nil), buf[:n]...)
			addr, payload, e := socks5.DecodeUDPPacket(data)
			if e != nil {
				continue
			}
			packet := &associatedPacket{socket: udp, source: remote, target: addr, payload: payload}
			tunnel.Tunnel.HandleUDPPacket(inbound.NewPacket(addr, packet, C.SOCKS5, inbound.WithInUser(user)))
		}
	}()
	io.Copy(io.Discard, c)
}

type associatedPacket struct {
	socket  *net.UDPConn
	source  *net.UDPAddr
	target  socks5.Addr
	payload []byte
}

func (p *associatedPacket) Data() []byte        { return p.payload }
func (p *associatedPacket) Drop()               {}
func (p *associatedPacket) LocalAddr() net.Addr { return p.source }
func (p *associatedPacket) InAddr() net.Addr    { return p.socket.LocalAddr() }
func (p *associatedPacket) WriteBack(data []byte, address net.Addr) (int, error) {
	target := socks5.ParseAddrToSocksAddr(address)
	if target == nil {
		target = p.target
	}
	encoded, err := socks5.EncodeUDPPacket(target, data)
	if err != nil {
		return 0, err
	}
	_, err = p.socket.WriteToUDP(encoded, p.source)
	if err != nil {
		return 0, err
	}
	return len(data), nil
}
