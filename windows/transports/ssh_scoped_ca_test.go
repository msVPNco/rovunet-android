package main

import (
	"context"
	"encoding/base64"
	"encoding/pem"
	"github.com/gorilla/websocket"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestScopedSshTLSCertificate(t *testing.T) {
	sshAddr, key, stop := sshFixture(t)
	defer stop()
	up := websocket.Upgrader{}
	front := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ws, err := up.Upgrade(w, r, nil)
		if err != nil {
			return
		}
		defer ws.Close()
		remote, err := net.Dial("tcp", sshAddr)
		if err != nil {
			return
		}
		defer remote.Close()
		stream := &wsConn{Conn: ws.UnderlyingConn(), ws: ws}
		done := make(chan struct{})
		go func() { io.Copy(remote, stream); remote.Close(); close(done) }()
		io.Copy(stream, remote)
		ws.Close()
		<-done
	}))
	defer front.Close()
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { io.WriteString(w, "rovunet-dns-https") }))
	defer target.Close()
	cert := string(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: front.Certificate().Raw}))
	for _, name := range []string{"missing_ca_rejected", "wrong_name_rejected", "scoped_ca_and_host_key_https"} {
		t.Run(name, func(t *testing.T) {
			f := fixtureFields(front.Listener.Addr().String())
			f[55] = "1"
			f[56] = "/tunnel"
			f[57] = "1"
			f[58] = "127.0.0.1"
			f[51] = "example.com"
			p, _ := freePort()
			c := config{Fields: f, Port: p, HostKey: base64.StdEncoding.EncodeToString(key.PublicKey().Marshal()), TLSCA: cert}
			if name == "missing_ca_rejected" {
				c.TLSCA = ""
			}
			if name == "wrong_name_rejected" {
				f[51] = "wrong.example.invalid"
			}
			o := newOwner(context.Background())
			defer o.stop()
			_, err := start(o, c)
			if name != "scoped_ca_and_host_key_https" {
				if err == nil {
					t.Fatal("untrusted TLS identity accepted")
				}
				return
			}
			if err != nil {
				t.Fatal("scoped certificate failed")
			}
			queryHTTPS(t, p, target)
		})
	}
}
