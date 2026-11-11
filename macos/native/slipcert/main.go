// SPDX-License-Identifier: AGPL-3.0-only
// Retrieves the public peer certificate through the profile's Slipstream DNS
// transport. It never opens an application stream or approves/stores a pin.
package main

import (
	"bytes"
	"context"
	"crypto/rand"
	"encoding/base32"
	"encoding/binary"
	"encoding/json"
	"encoding/pem"
	"errors"
	"io"
	"net"
	"net/netip"
	"os"
	"strings"
	"sync"
	"time"

	quic "github.com/metacubex/quic-go"
	"github.com/metacubex/tls"
	"github.com/miekg/dns"
)

type request struct {
	Domain    string   `json:"domain"`
	Resolvers []string `json:"resolvers"`
	TimeoutMS int      `json:"timeoutMS"`
}

type reply struct {
	Certificate string `json:"Certificate,omitempty"`
	Error       string `json:"Error,omitempty"`
}

type pendingQuery struct {
	name string
	sent time.Time
}

type dnsTunnel struct {
	socket  *net.UDPConn
	remote  *net.UDPAddr
	domain  string
	mu      sync.Mutex
	pending map[uint16]pendingQuery
}

func (t *dnsTunnel) WriteTo(packet []byte, _ net.Addr) (int, error) {
	encoded := strings.ToLower(base32.StdEncoding.WithPadding(base32.NoPadding).EncodeToString(packet))
	var labels []string
	for len(encoded) > 0 {
		length := min(57, len(encoded))
		labels = append(labels, encoded[:length])
		encoded = encoded[length:]
	}
	name := strings.Join(labels, ".") + "." + t.domain + "."
	if len(name) > 253 {
		return 0, errors.New("Slipstream packet exceeds QNAME capacity")
	}
	var random [2]byte
	if _, err := rand.Read(random[:]); err != nil {
		return 0, err
	}
	id := binary.BigEndian.Uint16(random[:])
	now := time.Now()
	t.mu.Lock()
	for key, query := range t.pending {
		if now.Sub(query.sent) > 10*time.Second {
			delete(t.pending, key)
		}
	}
	if len(t.pending) >= 256 {
		var oldest uint16
		oldestTime := now
		for key, query := range t.pending {
			if query.sent.Before(oldestTime) {
				oldest, oldestTime = key, query.sent
			}
		}
		delete(t.pending, oldest)
	}
	for {
		if _, exists := t.pending[id]; !exists {
			break
		}
		id++
	}
	t.pending[id] = pendingQuery{name: name, sent: now}
	t.mu.Unlock()
	query := &dns.Msg{MsgHdr: dns.MsgHdr{Id: id, RecursionDesired: true}}
	query.Question = []dns.Question{{Name: name, Qtype: dns.TypeTXT, Qclass: dns.ClassINET}}
	query.SetEdns0(1232, false)
	wire, err := query.Pack()
	if err != nil {
		return 0, err
	}
	if _, err = t.socket.Write(wire); err != nil {
		return 0, err
	}
	return len(packet), nil
}

func decodeResponse(wire []byte) (dns.Msg, []byte, error) {
	var message dns.Msg
	if err := message.Unpack(wire); err != nil {
		return message, nil, err
	}
	if !message.Response || len(message.Question) != 1 || message.Rcode != dns.RcodeSuccess ||
		len(message.Answer) != 1 || message.Answer[0].Header().Rrtype != dns.TypeTXT {
		return message, nil, errors.New("unexpected DNS response")
	}
	// TXT strings in miekg/dns use presentation escaping. Read the bounded wire
	// RDATA itself, so arbitrary encrypted QUIC bytes retain their exact value.
	_, offset, err := dns.UnpackDomainName(wire, 12)
	if err != nil || offset+4 > len(wire) {
		return message, nil, errors.New("invalid DNS question")
	}
	_, offset, err = dns.UnpackDomainName(wire, offset+4)
	if err != nil || offset+10 > len(wire) {
		return message, nil, errors.New("invalid DNS answer")
	}
	end := offset + 10 + int(binary.BigEndian.Uint16(wire[offset+8:offset+10]))
	offset += 10
	if end > len(wire) {
		return message, nil, errors.New("truncated DNS answer")
	}
	var packet []byte
	for offset < end {
		length := int(wire[offset])
		offset++
		if offset+length > end {
			return message, nil, errors.New("truncated TXT segment")
		}
		packet = append(packet, wire[offset:offset+length]...)
		offset += length
	}
	return message, packet, nil
}

func (t *dnsTunnel) ReadFrom(buffer []byte) (int, net.Addr, error) {
	wire := make([]byte, 8192)
	for {
		n, err := t.socket.Read(wire)
		if err != nil {
			return 0, nil, err
		}
		message, packet, err := decodeResponse(wire[:n])
		if err != nil || len(packet) == 0 || len(packet) > len(buffer) {
			continue
		}
		t.mu.Lock()
		query, found := t.pending[message.Id]
		matches := found && strings.EqualFold(query.name, message.Question[0].Name) &&
			message.Question[0].Qtype == dns.TypeTXT && time.Since(query.sent) <= 10*time.Second
		if matches {
			delete(t.pending, message.Id)
		}
		t.mu.Unlock()
		if !matches {
			continue
		}
		copy(buffer, packet)
		return len(packet), t.remote, nil
	}
}

func (t *dnsTunnel) Close() error                       { return t.socket.Close() }
func (t *dnsTunnel) LocalAddr() net.Addr                { return t.socket.LocalAddr() }
func (t *dnsTunnel) SetDeadline(v time.Time) error      { return t.socket.SetDeadline(v) }
func (t *dnsTunnel) SetReadDeadline(v time.Time) error  { return t.socket.SetReadDeadline(v) }
func (t *dnsTunnel) SetWriteDeadline(v time.Time) error { return t.socket.SetWriteDeadline(v) }

func discoverPeer(ctx context.Context, endpoint netip.AddrPort, domain string) (string, error) {
	remote := net.UDPAddrFromAddrPort(endpoint)
	socket, err := net.DialUDP("udp", nil, remote)
	if err != nil {
		return "", err
	}
	tunnel := &dnsTunnel{socket: socket, remote: remote, domain: domain, pending: make(map[uint16]pendingQuery)}
	defer tunnel.Close()
	transport := &quic.Transport{Conn: tunnel}
	defer transport.Close()
	mtu := uint16((240 - len(domain)) * 5 / 8)
	// First contact is TOFU discovery, with no application data. Completing the
	// TLS handshake still checks CertificateVerify (proof of the peer's private
	// key). The GUI then asks the user to approve the exact leaf certificate.
	// All subsequent application traffic uses the existing pinned native client.
	configuration := &tls.Config{
		ServerName: domain, InsecureSkipVerify: true,
		NextProtos: []string{"picoquic_sample"}, MinVersion: tls.VersionTLS13,
	}
	connection, err := transport.Dial(ctx, remote, configuration, &quic.Config{
		InitialPacketSize: mtu, DisablePathMTUDiscovery: true,
		HandshakeIdleTimeout: 8 * time.Second, MaxIdleTimeout: 10 * time.Second,
		KeepAlivePeriod: 200 * time.Millisecond, Versions: []quic.Version{quic.Version1},
	})
	if err != nil {
		return "", err
	}
	defer connection.CloseWithError(0, "")
	session := connection.ConnectionState().TLS
	if !session.HandshakeComplete || len(session.PeerCertificates) == 0 {
		return "", errors.New("no authenticated TLS session")
	}
	certificate := session.PeerCertificates[0]
	now := time.Now()
	if len(certificate.Raw) > 24000 || now.Before(certificate.NotBefore) || now.After(certificate.NotAfter) {
		return "", errors.New("invalid peer certificate")
	}
	return string(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: certificate.Raw})), nil
}

func discover(ctx context.Context, input request) reply {
	domain := strings.ToLower(strings.TrimSuffix(strings.TrimSpace(input.Domain), "."))
	if len(domain) == 0 || len(domain) > 137 || net.ParseIP(domain) != nil || input.TimeoutMS < 1000 ||
		input.TimeoutMS > 60000 || len(input.Resolvers) == 0 || len(input.Resolvers) > 16 {
		return reply{Error: "SLIP_CERT_INVALID"}
	}
	if _, valid := dns.IsDomainName(domain); !valid {
		return reply{Error: "SLIP_CERT_INVALID"}
	}
	var endpoints []netip.AddrPort
	for _, text := range input.Resolvers {
		address, err := netip.ParseAddrPort(text)
		if err != nil || address.Port() == 0 || address.Addr().IsUnspecified() || address.Addr().IsMulticast() {
			return reply{Error: "SLIP_CERT_INVALID"}
		}
		endpoints = append(endpoints, address)
	}
	ctx, cancel := context.WithTimeout(ctx, time.Duration(input.TimeoutMS)*time.Millisecond)
	defer cancel()
	for _, endpoint := range endpoints {
		if ctx.Err() != nil {
			return reply{Error: "SLIP_CERT_DISCOVERY_TIMEOUT"}
		}
		attempt, stop := context.WithTimeout(ctx, 8*time.Second)
		certificate, err := discoverPeer(attempt, endpoint, domain)
		stop()
		if err == nil {
			return reply{Certificate: certificate}
		}
	}
	if ctx.Err() != nil {
		return reply{Error: "SLIP_CERT_DISCOVERY_TIMEOUT"}
	}
	return reply{Error: "SLIP_CERT_DISCOVERY_FAILED"}
}

func main() {
	result := reply{Error: "SLIP_CERT_INVALID"}
	var length uint32
	if binary.Read(os.Stdin, binary.LittleEndian, &length) == nil && length > 0 && length <= 16384 {
		buffer := make([]byte, length)
		if _, err := io.ReadFull(os.Stdin, buffer); err == nil {
			decoder := json.NewDecoder(bytes.NewReader(buffer))
			decoder.DisallowUnknownFields()
			var input request
			if decoder.Decode(&input) == nil {
				result = discover(context.Background(), input)
			}
		}
	}
	encoded, _ := json.Marshal(result)
	_ = binary.Write(os.Stdout, binary.LittleEndian, uint32(len(encoded)))
	_, _ = os.Stdout.Write(encoded)
}
