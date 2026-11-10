// Loopback-only SSH and TLS-wrapped SSH peers for the Android regression gate.
// Uses synthetic credentials and only serves an in-memory response for one reserved
// benchmark address. It never opens a forwarded connection or external server.
package main

import (
	"bufio"
	"crypto/ecdsa"
	"crypto/ed25519"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/tls"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/json"
	"encoding/pem"
	"fmt"
	"math/big"
	"net"
	"os"
	"os/signal"
	"sync/atomic"
	"syscall"
	"time"

	"golang.org/x/crypto/ssh"
)

func main() {
	_, private, err := ed25519.GenerateKey(rand.Reader)
	must(err)
	signer, err := ssh.NewSignerFromKey(private)
	must(err)
	var authentications atomic.Int64
	config := &ssh.ServerConfig{PasswordCallback: func(c ssh.ConnMetadata, password []byte) (*ssh.Permissions, error) {
		authentications.Add(1)
		if c.User() != "device-test" || string(password) != "synthetic-only" {
			return nil, fmt.Errorf("synthetic credentials rejected")
		}
		return nil, nil
	}}
	config.AddHostKey(signer)
	plain, err := net.Listen("tcp4", "127.0.0.1:0")
	must(err)
	defer plain.Close()
	now := time.Now()
	// ECDSA TLS certificates also work with Android 7–11 providers. SSH retains its Ed25519 host key.
	tlsKey, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	must(err)
	template := &x509.Certificate{SerialNumber: big.NewInt(1), Subject: pkix.Name{CommonName: "localhost"},
		DNSNames: []string{"localhost"}, NotBefore: now.Add(-time.Hour), NotAfter: now.Add(24 * time.Hour),
		KeyUsage: x509.KeyUsageDigitalSignature, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth}}
	der, err := x509.CreateCertificate(rand.Reader, template, template, tlsKey.Public(), tlsKey)
	must(err)
	encodedKey, err := x509.MarshalPKCS8PrivateKey(tlsKey)
	must(err)
	cert, err := tls.X509KeyPair(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: der}),
		pem.EncodeToMemory(&pem.Block{Type: "PRIVATE KEY", Bytes: encodedKey}))
	must(err)
	wrapped, err := tls.Listen("tcp4", "127.0.0.1:0", &tls.Config{Certificates: []tls.Certificate{cert}, MinVersion: tls.VersionTLS12})
	must(err)
	defer wrapped.Close()
	serve := func(listener net.Listener) {
		for {
			raw, err := listener.Accept()
			if err != nil {
				return
			}
			go func() {
				defer raw.Close()
				_ = raw.SetDeadline(time.Now().Add(2 * time.Minute))
				conn, channels, requests, err := ssh.NewServerConn(raw, config)
				if err != nil {
					return
				}
				defer conn.Close()
				go ssh.DiscardRequests(requests)
				for channel := range channels {
					var destination struct {
						Host       string
						Port       uint32
						Origin     string
						OriginPort uint32
					}
					if channel.ChannelType() != "direct-tcpip" || ssh.Unmarshal(channel.ExtraData(), &destination) != nil ||
						destination.Host != "198.18.0.13" || destination.Port != 80 {
						_ = channel.Reject(ssh.Prohibited, "fixture does not forward network traffic")
						continue
					}
					stream, requests, err := channel.Accept()
					if err != nil {
						continue
					}
					go func() {
						defer stream.Close()
						go ssh.DiscardRequests(requests)
						reader := bufio.NewReader(stream)
						for size := 0; size < 4096; {
							line, err := reader.ReadString('\n')
							if err != nil {
								return
							}
							size += len(line)
							if line == "\r\n" {
								body := "chain-through-inner-ssh\n"
								fmt.Fprintf(stream, "HTTP/1.0 200 OK\r\nContent-Length: %d\r\n\r\n%s", len(body), body)
								return
							}
						}
					}()
				}
			}()
		}
	}
	go serve(plain)
	go serve(wrapped)
	must(json.NewEncoder(os.Stdout).Encode(map[string]int{"ssh": plain.Addr().(*net.TCPAddr).Port, "tls": wrapped.Addr().(*net.TCPAddr).Port}))
	stopping := make(chan os.Signal, 1)
	signal.Notify(stopping, os.Interrupt, syscall.SIGTERM)
	<-stopping
	fmt.Fprintf(os.Stderr, "synthetic authentication requests: %d\n", authentications.Load())
}

func must(err error) {
	if err != nil {
		panic(err)
	}
}
