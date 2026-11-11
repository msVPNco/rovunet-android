package main

import (
	"context"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/pem"
	"math/big"
	"net"
	"net/netip"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"testing"
	"time"

	"github.com/miekg/dns"
)

func TestBinaryTXTIsDecodedExactly(t *testing.T) {
	message := &dns.Msg{MsgHdr: dns.MsgHdr{Id: 7, Response: true}}
	message.Question = []dns.Question{{Name: "packet.t.example.invalid.", Qtype: dns.TypeTXT, Qclass: dns.ClassINET}}
	message.Answer = []dns.RR{&dns.TXT{Hdr: dns.RR_Header{Name: message.Question[0].Name, Rrtype: dns.TypeTXT, Class: dns.ClassINET}, Txt: []string{`\000\255\092abc`, `\001\002`}}}
	wire, err := message.Pack()
	if err != nil {
		t.Fatal(err)
	}
	_, packet, err := decodeResponse(wire)
	if err != nil || string(packet) != string([]byte{0, 255, 92, 'a', 'b', 'c', 1, 2}) {
		t.Fatalf("binary TXT changed: %x %v", packet, err)
	}
	for size := 0; size < len(wire); size++ {
		if _, _, err := decodeResponse(wire[:size]); err == nil {
			t.Fatalf("accepted truncated DNS payload %d", size)
		}
	}
}

func TestInvalidDiscoveryDoesNotDial(t *testing.T) {
	for _, input := range []request{
		{Domain: "", Resolvers: []string{"127.0.0.1:53"}, TimeoutMS: 1000},
		{Domain: strings.Repeat("a", 138), Resolvers: []string{"127.0.0.1:53"}, TimeoutMS: 1000},
		{Domain: "t.example.invalid", Resolvers: []string{"resolver.example.invalid:53"}, TimeoutMS: 1000},
		{Domain: "t.example.invalid", Resolvers: []string{"0.0.0.0:53"}, TimeoutMS: 1000},
		{Domain: "t.example.invalid", Resolvers: []string{"127.0.0.1:0"}, TimeoutMS: 1000},
		{Domain: "t.example.invalid", Resolvers: []string{"127.0.0.1:53"}, TimeoutMS: 60001},
	} {
		if result := discover(context.Background(), input); result.Error != "SLIP_CERT_INVALID" || result.Certificate != "" {
			t.Fatal("invalid input accepted")
		}
	}
}

func TestCancelledDiscoveryDoesNotDial(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	result := discover(ctx, request{Domain: "t.example.invalid", Resolvers: []string{"127.0.0.1:53"}, TimeoutMS: 1000})
	if result.Error != "SLIP_CERT_DISCOVERY_TIMEOUT" || result.Certificate != "" {
		t.Fatal("cancelled discovery returned a certificate")
	}
}

func TestCertificateFromOfficialSlipstreamServer(t *testing.T) {
	server := os.Getenv("ROVUNET_SLIP_TEST_SERVER")
	if server == "" {
		t.Skip("set ROVUNET_SLIP_TEST_SERVER to the pinned upstream Linux fixture")
	}
	for _, expired := range []bool{false, true} {
		t.Run(strconv.FormatBool(expired), func(t *testing.T) {
			directory := t.TempDir()
			private, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
			if err != nil {
				t.Fatal(err)
			}
			expiry := time.Now().Add(time.Hour)
			if expired {
				expiry = time.Now().Add(-time.Hour)
			}
			template := &x509.Certificate{SerialNumber: big.NewInt(1), Subject: pkix.Name{CommonName: "t.example.invalid"}, NotBefore: time.Now().Add(-2 * time.Hour), NotAfter: expiry, KeyUsage: x509.KeyUsageDigitalSignature, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}}
			der, err := x509.CreateCertificate(rand.Reader, template, template, &private.PublicKey, private)
			if err != nil {
				t.Fatal(err)
			}
			keyDER, err := x509.MarshalPKCS8PrivateKey(private)
			if err != nil {
				t.Fatal(err)
			}
			certificate := pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der})
			certPath, keyPath := filepath.Join(directory, "cert.pem"), filepath.Join(directory, "key.pem")
			if os.WriteFile(certPath, certificate, 0600) != nil || os.WriteFile(keyPath, pem.EncodeToMemory(&pem.Block{Type: "PRIVATE KEY", Bytes: keyDER}), 0600) != nil {
				t.Fatal("fixture file creation failed")
			}
			reservation, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4(127, 0, 0, 1)})
			if err != nil {
				t.Fatal(err)
			}
			port := reservation.LocalAddr().(*net.UDPAddr).Port
			reservation.Close()
			ctx, cancel := context.WithTimeout(context.Background(), 15*time.Second)
			defer cancel()
			process := exec.CommandContext(ctx, server, "--dns-listen-host", "127.0.0.1", "--dns-listen-port", strconv.Itoa(port), "--domain", "t.example.invalid", "--cert", certPath, "--key", keyPath, "--target-address", "127.0.0.1:9")
			if err := process.Start(); err != nil {
				t.Fatal(err)
			}
			defer func() { _ = process.Process.Kill(); _ = process.Wait() }()
			time.Sleep(200 * time.Millisecond)
			endpoint := netip.AddrPortFrom(netip.MustParseAddr("127.0.0.1"), uint16(port))
			received, err := discoverPeer(ctx, endpoint, "t.example.invalid")
			if expired {
				if err == nil || received != "" {
					t.Fatal("expired server certificate accepted")
				}
				return
			}
			if err != nil || strings.TrimSpace(received) != strings.TrimSpace(string(certificate)) {
				t.Fatalf("actual peer certificate mismatch: %v", err)
			}
		})
	}
}
