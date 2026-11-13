package main

import (
	"context"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/json"
	"encoding/pem"
	"io"
	"math/big"
	"net"
	"net/netip"
	"strconv"
	"testing"
	"time"

	"github.com/metacubex/mihomo/component/ca"
	"github.com/metacubex/mihomo/config"
	C "github.com/metacubex/mihomo/constant"
	anyserver "github.com/metacubex/mihomo/listener/anytls"
	LC "github.com/metacubex/mihomo/listener/config"
	inboundserver "github.com/metacubex/mihomo/listener/inbound"
	tuicserver "github.com/metacubex/mihomo/listener/tuic"
)

type fixtureTunnel struct{}

func (fixtureTunnel) HandleTCPConn(conn net.Conn, metadata *C.Metadata) {
	defer conn.Close()
	if metadata.DstIP.IsLoopback() && metadata.DstPort == 9 {
		_ = conn.SetDeadline(time.Now().Add(5 * time.Second))
		_, _ = io.Copy(conn, conn)
	}
}
func (fixtureTunnel) HandleUDPPacket(packet C.UDPPacket, _ *C.Metadata) { packet.Drop() }
func (fixtureTunnel) NatTable() C.NatTable                              { return nil }

func settings(kind string, port int) map[string]any {
	values := map[string]any{"name": "proxy", "type": kind, "server": "127.0.0.1", "port": port, "udp": true, "password": "fixture-password"}
	switch kind {
	case "anytls":
		values["skip-cert-verify"] = false
		values["sni"] = "fixture.example.invalid"
	case "tuic":
		values["uuid"] = "00000000-0000-4000-8000-000000000001"
		values["skip-cert-verify"] = false
		values["sni"] = "fixture.example.invalid"
		values["alpn"] = []string{"h3"}
	case "mieru":
		values["username"] = "fixture-user"
		values["transport"] = "TCP"
	}
	return values
}

func runtime(kind string, values map[string]any) []byte {
	encoded, _ := json.Marshal(map[string]any{
		"inbounds":  []any{map[string]any{"listen": "127.0.0.1", "port": 10808, "protocol": "socks", "settings": map[string]any{"auth": "noauth"}}},
		"outbounds": []any{map[string]any{"protocol": kind, "settings": values}, map[string]any{"protocol": "freedom"}},
	})
	return encoded
}

func TestValidatedSupplementalConfig(t *testing.T) {
	C.SetHomeDir(t.TempDir())
	for _, kind := range []string{"anytls", "tuic", "mieru"} {
		t.Run(kind, func(t *testing.T) {
			converted, _, _, err := convert(runtime(kind, settings(kind, 443)))
			if err != nil {
				t.Fatal(err)
			}
			cfg, err := config.Parse(converted)
			if err != nil {
				t.Fatal(err)
			}
			for _, proxy := range cfg.Proxies {
				_ = proxy.Close()
			}
			invalid := settings(kind, 443)
			invalid["dialer-proxy"] = "untrusted"
			if _, _, _, err := convert(runtime(kind, invalid)); err == nil {
				t.Fatal("arbitrary backend option accepted")
			}
			if kind != "mieru" {
				invalid = settings(kind, 443)
				invalid["skip-cert-verify"] = true
				if _, _, _, err := convert(runtime(kind, invalid)); err == nil {
					t.Fatal("unverified TLS accepted")
				}
			}
		})
	}
}

func TestEncryptedSupplementalLoopbackData(t *testing.T) {
	private, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	template := &x509.Certificate{SerialNumber: big.NewInt(1), Subject: pkix.Name{CommonName: "fixture.example.invalid"}, DNSNames: []string{"fixture.example.invalid"}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().Add(time.Hour), IsCA: true, BasicConstraintsValid: true, KeyUsage: x509.KeyUsageDigitalSignature | x509.KeyUsageCertSign}
	der, err := x509.CreateCertificate(rand.Reader, template, template, &private.PublicKey, private)
	if err != nil {
		t.Fatal(err)
	}
	key, _ := x509.MarshalPKCS8PrivateKey(private)
	certificate := string(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der}))
	privateKey := string(pem.EncodeToMemory(&pem.Block{Type: "PRIVATE KEY", Bytes: key}))
	if err := ca.AddCertificate(certificate); err != nil {
		t.Fatal(err)
	}
	defer ca.ResetCertificate()
	C.SetHomeDir(t.TempDir())
	for _, kind := range []string{"anytls", "tuic", "mieru"} {
		t.Run(kind, func(t *testing.T) {
			var port int
			var closeServer func() error
			switch kind {
			case "anytls":
				server, err := anyserver.New(LC.AnyTLSServer{Enable: true, Listen: "127.0.0.1:0", Users: map[string]string{"fixture-user": "fixture-password"}, Certificate: certificate, PrivateKey: privateKey}, inboundserver.BaseOption{}.ListenConfig(), fixtureTunnel{})
				if err != nil {
					t.Fatal(err)
				}
				port = server.AddrList()[0].(*net.TCPAddr).Port
				closeServer = server.Close
			case "tuic":
				server, err := tuicserver.New(LC.TuicServer{Enable: true, Listen: "127.0.0.1:0", Users: map[string]string{"00000000-0000-4000-8000-000000000001": "fixture-password"}, Certificate: certificate, PrivateKey: privateKey, ALPN: []string{"h3"}}, inboundserver.BaseOption{}.ListenConfig(), fixtureTunnel{})
				if err != nil {
					t.Fatal(err)
				}
				port = server.AddrList()[0].(*net.UDPAddr).Port
				closeServer = server.Close
			case "mieru":
				reservation, err := net.Listen("tcp4", "127.0.0.1:0")
				if err != nil {
					t.Fatal(err)
				}
				port = reservation.Addr().(*net.TCPAddr).Port
				_ = reservation.Close()
				server, err := inboundserver.NewMieru(&inboundserver.MieruOption{BaseOption: inboundserver.BaseOption{NameStr: "fixture", Listen: "127.0.0.1", Port: strconv.Itoa(port)}, Transport: "TCP", Users: map[string]string{"fixture-user": "fixture-password"}})
				if err != nil {
					t.Fatal(err)
				}
				if err := server.Listen(fixtureTunnel{}); err != nil {
					t.Fatal(err)
				}
				closeServer = server.Close
			}
			defer closeServer()
			converted, _, _, err := convert(runtime(kind, settings(kind, port)))
			if err != nil {
				t.Fatal(err)
			}
			cfg, err := config.Parse(converted)
			if err != nil {
				t.Fatal(err)
			}
			defer func() {
				for _, proxy := range cfg.Proxies {
					_ = proxy.Close()
				}
			}()
			ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
			defer cancel()
			connection, err := cfg.Proxies["proxy"].DialContext(ctx, &C.Metadata{NetWork: C.TCP, DstIP: netip.MustParseAddr("127.0.0.1"), DstPort: 9})
			if err != nil {
				t.Fatal(err)
			}
			defer connection.Close()
			_ = connection.SetDeadline(time.Now().Add(5 * time.Second))
			body := "Rovunet encrypted loopback fixture"
			if _, err := io.WriteString(connection, body); err != nil {
				t.Fatal(err)
			}
			buffer := make([]byte, len(body))
			if _, err := io.ReadFull(connection, buffer); err != nil || string(buffer) != body {
				t.Fatalf("encrypted data exchange failed: %v", err)
			}
		})
	}
}
