// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"context"
	"encoding/base64"
	"encoding/json"
	"encoding/pem"
	"fmt"
	"github.com/gorilla/websocket"
	"golang.org/x/net/proxy"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

func TestPanelStunTLS(t *testing.T) {
	if os.Getenv("ROVUNET_STUN_CHILD") == "1" {
		var c config
		if err := json.NewDecoder(os.Stdin).Decode(&c); err != nil {
			t.Fatal(err)
		}
		o := newOwner(context.Background())
		defer o.stop()
		_, err := start(o, c)
		if err != nil {
			t.Fatal("TLS/WS/SSH handshake failed")
		}
		d, _ := proxy.SOCKS5("tcp", fmt.Sprintf("127.0.0.1:%d", c.Port), nil, &net.Dialer{Timeout: time.Second})
		tr := &http.Transport{DialContext: d.(proxy.ContextDialer).DialContext}
		defer tr.CloseIdleConnections()
		client := http.Client{Transport: tr, Timeout: 10 * time.Second}
		resp, err := client.Get(os.Getenv("ROVUNET_STUN_TARGET"))
		if err != nil {
			t.Fatal(err)
		}
		defer resp.Body.Close()
		body, _ := io.ReadAll(resp.Body)
		if string(body) != "stun-verified" {
			t.Fatal("payload mismatch")
		}
		return
	}
	sshAddr, key, stop := sshFixture(t)
	defer stop()
	up := websocket.Upgrader{}
	front := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/tunnel" {
			w.WriteHeader(404)
			return
		}
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
	target := httptest.NewTLSServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { io.WriteString(w, "stun-verified") }))
	defer target.Close()
	f := fixtureFields(front.Listener.Addr().String())
	f[55] = "1"
	f[56] = "/tunnel"
	f[57] = "1"
	f[58] = "127.0.0.1"
	port, _ := freePort()
	c := config{Fields: f, Port: port, HostKey: base64.StdEncoding.EncodeToString(key.PublicKey().Marshal())}
	body, _ := json.Marshal(c)
	cert := filepath.Join(t.TempDir(), "fixture.pem")
	os.WriteFile(cert, pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: front.Certificate().Raw}), 0600)
	child := exec.Command(os.Args[0], "-test.run=^TestPanelStunTLS$", "-test.timeout=25s")
	child.Stdin = strings.NewReader(string(body))
	child.Env = append(os.Environ(), "SSL_CERT_FILE="+cert, "ROVUNET_STUN_CHILD=1", "ROVUNET_STUN_TARGET="+target.URL)
	if out, err := child.CombinedOutput(); err != nil {
		t.Fatalf("StunTLS fixture failed: %s", out)
	}
}
