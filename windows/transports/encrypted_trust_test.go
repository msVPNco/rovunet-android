// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/tls"
	"crypto/x509"
	"crypto/x509/pkix"
	"io"
	"log"
	"math/big"
	"net"
	dnsclient "noizdns/mobile"
	"strings"
	"testing"
	"time"
)

func TestEncryptedResolverUnknownRootRejected(t *testing.T) {
	log.SetOutput(io.Discard)
	key, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	template := &x509.Certificate{SerialNumber: big.NewInt(7), Subject: pkix.Name{CommonName: "private fixture"}, NotBefore: time.Now().Add(-time.Hour), NotAfter: time.Now().Add(time.Hour), IPAddresses: []net.IP{net.ParseIP("127.0.0.1")}, KeyUsage: x509.KeyUsageDigitalSignature, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}}
	der, err := x509.CreateCertificate(rand.Reader, template, template, &key.PublicKey, key)
	if err != nil {
		t.Fatal(err)
	}
	certificate := tls.Certificate{Certificate: [][]byte{der}, PrivateKey: key}
	for _, mode := range []string{"tls", "https"} {
		t.Run(mode, func(t *testing.T) {
			listener, err := tls.Listen("tcp", "127.0.0.1:0", &tls.Config{Certificates: []tls.Certificate{certificate}})
			if err != nil {
				t.Fatal(err)
			}
			defer listener.Close()
			outcome := make(chan bool, 8)
			go func() {
				for {
					c, err := listener.Accept()
					if err != nil {
						return
					}
					go func() {
						defer c.Close()
						conn := c.(*tls.Conn)
						conn.SetDeadline(time.Now().Add(3 * time.Second))
						err := conn.Handshake()
						select {
						case outcome <- err != nil:
						default:
						}
					}()
				}
			}()
			address := mode + "://" + listener.Addr().String()
			if mode == "https" {
				address += "/dns-query"
			}
			client, err := dnsclient.NewClient(address, "t.example.invalid", strings.Repeat("a", 64), "127.0.0.1:0")
			if err != nil {
				t.Fatal(err)
			}
			defer client.Stop()
			_ = client.Start()
			select {
			case rejected := <-outcome:
				if !rejected {
					t.Fatal("untrusted TLS handshake accepted")
				}
			case <-time.After(5 * time.Second):
				t.Fatal("no encrypted handshake observed")
			}
		})
	}
}
