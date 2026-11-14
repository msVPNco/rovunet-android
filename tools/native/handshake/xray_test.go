package tls

import (
	"crypto/rand"
	"crypto/rsa"
	"crypto/tls"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/pem"
	"math/big"
	"net"
	"os"
	"path/filepath"
	"sync/atomic"
	"testing"
	"time"

	utls "github.com/refraction-networking/utls"
)

func rovunetTestCertificate(t *testing.T) tls.Certificate {
	t.Helper()
	key, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		t.Fatal(err)
	}
	template := &x509.Certificate{SerialNumber: big.NewInt(time.Now().UnixNano()), Subject: pkix.Name{CommonName: "Synthetic TLS"}, DNSNames: []string{"localhost"}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().Add(time.Hour), KeyUsage: x509.KeyUsageDigitalSignature | x509.KeyUsageKeyEncipherment, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}}
	der, err := x509.CreateCertificate(rand.Reader, template, template, &key.PublicKey, key)
	if err != nil {
		t.Fatal(err)
	}
	result, err := tls.X509KeyPair(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der}), pem.EncodeToMemory(&pem.Block{Type: "RSA PRIVATE KEY", Bytes: x509.MarshalPKCS1PrivateKey(key)}))
	if err != nil {
		t.Fatal(err)
	}
	return result
}

func TestRovunetAutomaticCertificateRequiresProofNamesAndExistingIdentity(t *testing.T) {
	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer listener.Close()
	var active atomic.Pointer[tls.Certificate]
	first := rovunetTestCertificate(t)
	active.Store(&first)
	go func() {
		for {
			raw, err := listener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer raw.Close()
				raw.SetDeadline(time.Now().Add(3 * time.Second))
				server := tls.Server(raw, &tls.Config{GetCertificate: func(*tls.ClientHelloInfo) (*tls.Certificate, error) { return active.Load(), nil }})
				server.Handshake()
			}()
		}
	}()
	previousDirectory, previousEndpoint := RovunetCertificateDirectory, RovunetCertificateEndpoint
	defer func() { RovunetCertificateDirectory = previousDirectory; RovunetCertificateEndpoint = previousEndpoint }()
	RovunetCertificateEndpoint = listener.Addr().String()
	for _, fingerprint := range []bool{false, true} {
		t.Run(map[bool]string{false: "standard", true: "uTLS"}[fingerprint], func(t *testing.T) {
			RovunetCertificateDirectory = t.TempDir()
			connect := func(host string) error {
				config := (&Config{ServerName: host}).GetTLSConfig()
				raw, err := net.DialTimeout("tcp", listener.Addr().String(), 2*time.Second)
				if err != nil {
					return err
				}
				defer raw.Close()
				raw.SetDeadline(time.Now().Add(3 * time.Second))
				if fingerprint {
					return utls.UClient(raw, copyConfig(config), utls.HelloChrome_Auto).Handshake()
				}
				return tls.Client(raw, config).Handshake()
			}
			if err := connect("wrong.example.invalid"); err == nil {
				t.Fatal("Wrong name enrolled")
			}
			files, _ := os.ReadDir(RovunetCertificateDirectory)
			if len(files) != 0 {
				t.Fatal("Failed handshake stored a certificate")
			}
			if err := connect("localhost"); err != nil {
				t.Fatal(err)
			}
			files, _ = os.ReadDir(RovunetCertificateDirectory)
			if len(files) != 1 {
				t.Fatal("Missing private certificate")
			}
			path := filepath.Join(RovunetCertificateDirectory, files[0].Name())
			before, _ := os.ReadFile(path)
			if err := connect("localhost"); err != nil {
				t.Fatal(err)
			}
			changed := rovunetTestCertificate(t)
			active.Store(&changed)
			if err := connect("localhost"); err == nil {
				t.Fatal("Changed identity accepted")
			}
			after, _ := os.ReadFile(path)
			if string(before) != string(after) {
				t.Fatal("Stored certificate replaced")
			}
			active.Store(&first)
			if err := connect("localhost"); err != nil {
				t.Fatal(err)
			}
		})
	}
}

func TestRovunetPublicCaRenewalCannotDowngradeToUnknownPrivateIdentity(t *testing.T) {
	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer listener.Close()
	first, second := rovunetTestCertificate(t), rovunetTestCertificate(t)
	var active atomic.Pointer[tls.Certificate]
	active.Store(&first)
	go func() {
		for {
			raw, err := listener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer raw.Close()
				raw.SetDeadline(time.Now().Add(3 * time.Second))
				tls.Server(raw, &tls.Config{GetCertificate: func(*tls.ClientHelloInfo) (*tls.Certificate, error) { return active.Load(), nil }}).Handshake()
			}()
		}
	}()
	previousDirectory, previousEndpoint := RovunetCertificateDirectory, RovunetCertificateEndpoint
	defer func() { RovunetCertificateDirectory = previousDirectory; RovunetCertificateEndpoint = previousEndpoint }()
	RovunetCertificateEndpoint = listener.Addr().String()
	for _, fingerprint := range []bool{false, true} {
		t.Run(map[bool]string{false: "standard", true: "uTLS"}[fingerprint], func(t *testing.T) {
			active.Store(&first)
			RovunetCertificateDirectory = t.TempDir()
			connect := func(trusted *tls.Certificate) error {
				config := (&Config{ServerName: "localhost"}).GetTLSConfig()
				config.RootCAs = x509.NewCertPool()
				if trusted != nil {
					cert, err := x509.ParseCertificate(trusted.Certificate[0])
					if err != nil {
						t.Fatal(err)
					}
					config.RootCAs.AddCert(cert)
				}
				raw, err := net.DialTimeout("tcp", listener.Addr().String(), 2*time.Second)
				if err != nil {
					return err
				}
				defer raw.Close()
				raw.SetDeadline(time.Now().Add(3 * time.Second))
				if fingerprint {
					return utls.UClient(raw, copyConfig(config), utls.HelloChrome_Auto).Handshake()
				}
				return tls.Client(raw, config).Handshake()
			}
			if err := connect(&first); err != nil {
				t.Fatal("Trusted CA certificate failed", err)
			}
			files, _ := os.ReadDir(RovunetCertificateDirectory)
			if len(files) != 1 {
				t.Fatal("CA certificate was not cached")
			}
			path := filepath.Join(RovunetCertificateDirectory, files[0].Name())
			before, _ := os.ReadFile(path)
			stored, err := rovunetReadIdentity(path)
			if err != nil || !stored.PublicCA {
				t.Fatal("CA identity became a private pin", err)
			}
			active.Store(&second)
			if err := connect(nil); err == nil {
				t.Fatal("Known CA identity downgraded to an unknown self-signed key")
			}
			after, _ := os.ReadFile(path)
			if string(before) != string(after) {
				t.Fatal("Rejected downgrade replaced cached identity")
			}
			if err := connect(&second); err != nil {
				t.Fatal("CA-validated key renewal was blocked", err)
			}
			if err := connect(nil); err != nil {
				t.Fatal("Unchanged known key was forgotten", err)
			}
			stored, err = rovunetReadIdentity(path)
			if err != nil || !stored.PublicCA {
				t.Fatal("Known CA renewal was converted to a private pin", err)
			}
		})
	}
}
