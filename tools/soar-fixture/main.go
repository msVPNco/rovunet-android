// Loopback-only peers for testing the shipped Android Soar client. No Internet targets.
package main

import (
	"context"
	"crypto/ed25519"
	"crypto/rand"
	"encoding/base64"
	"encoding/binary"
	"encoding/hex"
	"encoding/json"
	soarlib "github.com/getlantern/soar"
	"io"
	"net"
	"net/http"
	"os"
	"time"
)

func main() {
	ctx := context.Background()
	pub, key, err := ed25519.GenerateKey(rand.Reader)
	if err != nil {
		panic(err)
	}
	httpListener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		panic(err)
	}
	go http.Serve(httpListener, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.WriteHeader(204) }))
	authoritative, err := net.ListenPacket("udp", "127.0.0.1:0")
	if err != nil {
		panic(err)
	}
	go soarlib.Serve(ctx, authoritative, soarlib.ServerConfig{Zone: "t.soar.invalid", PrivateKey: key,
		Dial: func(ctx context.Context, network, address string) (net.Conn, error) {
			return (&net.Dialer{}).DialContext(ctx, "tcp", httpListener.Addr().String())
		}})
	// This port intentionally has no UDP listener. DNS/TCP forwards to the real Soar server.
	tcpOnly := tcpResolver(authoritative.LocalAddr().String(), false)
	// A DNS responder which never supplies an authenticated Soar answer.
	fake := tcpResolver("", true)
	udpPort := authoritative.LocalAddr().(*net.UDPAddr).Port
	json.NewEncoder(os.Stdout).Encode(map[string]any{"zone": "t.soar.invalid", "publicKey": base64.StdEncoding.EncodeToString(pub),
		"publicKeyHex": hex.EncodeToString(pub), "udpPort": udpPort, "tcpOnlyPort": tcpOnly, "fakeTcpPort": fake})
	select {}
}

func tcpResolver(target string, fake bool) int {
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		panic(err)
	}
	go func() {
		for {
			c, e := ln.Accept()
			if e != nil {
				return
			}
			go func() {
				defer c.Close()
				for {
					c.SetDeadline(time.Now().Add(10 * time.Second))
					var size [2]byte
					if _, e := io.ReadFull(c, size[:]); e != nil {
						return
					}
					query := make([]byte, int(binary.BigEndian.Uint16(size[:])))
					if _, e := io.ReadFull(c, query); e != nil {
						return
					}
					var response []byte
					if fake {
						response = append([]byte(nil), query...)
						if len(response) >= 12 {
							response[2] = 0x81
							response[3] = 0x83
							for i := 6; i < 12; i++ {
								response[i] = 0
							}
						}
					} else {
						upstream, e := net.DialTimeout("udp", target, time.Second)
						if e != nil {
							return
						}
						upstream.SetDeadline(time.Now().Add(5 * time.Second))
						_, e = upstream.Write(query)
						data := make([]byte, 65535)
						n, readErr := upstream.Read(data)
						upstream.Close()
						if e != nil || readErr != nil {
							return
						}
						response = data[:n]
					}
					binary.BigEndian.PutUint16(size[:], uint16(len(response)))
					if _, e := c.Write(append(size[:], response...)); e != nil {
						return
					}
				}
			}()
		}
	}()
	return ln.Addr().(*net.TCPAddr).Port
}
