// SPDX-License-Identifier: AGPL-3.0-only
// Rovunet transport supervisor; secrets are read only from standard input.
package main

import (
	"bufio"
	"context"
	_ "embed"
	"encoding/base64"
	"encoding/json"
	"errors"
	"io"
	"log"
	"net"
	"os"
	"strconv"
	"strings"
	"sync"
	"time"

	"golang.org/x/crypto/ssh"
	"golang.org/x/net/proxy"
	dns "noizdns/mobile"
	soar "soar-mobile/soar"
)

//go:embed resolverdata/resolvers.txt
var resolverInventory string

type config struct {
	Fields       []string `json:"fields"`
	Port         int      `json:"port"`
	UpstreamPort int      `json:"upstreamPort"`
	HostKey      string   `json:"hostKey"`
	TLSCA        string   `json:"tlsCa"`
}
type status struct {
	State       string `json:"state"`
	Error       string `json:"error,omitempty"`
	HostKey     string `json:"hostKey,omitempty"`
	Fingerprint string `json:"fingerprint,omitempty"`
}

func num(s string) int { n, _ := strconv.Atoi(s); return n }
func dec(s string) string {
	b, e := base64.StdEncoding.DecodeString(s)
	if e != nil {
		b, _ = base64.RawURLEncoding.DecodeString(s)
	}
	return string(b)
}
func endpoint(host, port string) string { return net.JoinHostPort(strings.Trim(host, "[]"), port) }
func emit(s status)                     { _ = json.NewEncoder(os.Stdout).Encode(s) }

type owner struct {
	ctx    context.Context
	cancel context.CancelFunc
	mu     sync.Mutex
	conns  map[io.Closer]bool
	stops  []func()
	wg     sync.WaitGroup
	slots  chan struct{}
}

func newOwner(ctx context.Context) *owner {
	c, cancel := context.WithCancel(ctx)
	return &owner{ctx: c, cancel: cancel, conns: map[io.Closer]bool{}, slots: make(chan struct{}, 64)}
}
func (o *owner) add(c io.Closer) {
	o.mu.Lock()
	defer o.mu.Unlock()
	if o.ctx.Err() != nil {
		c.Close()
		return
	}
	o.conns[c] = true
}
func (o *owner) drop(c io.Closer) { c.Close(); o.mu.Lock(); delete(o.conns, c); o.mu.Unlock() }
func (o *owner) stop() {
	o.cancel()
	o.mu.Lock()
	for c := range o.conns {
		c.Close()
	}
	o.mu.Unlock()
	for i := len(o.stops) - 1; i >= 0; i-- {
		o.stops[i]()
	}
	o.wg.Wait()
}
func main() {
	log.SetOutput(io.Discard)
	if len(os.Args) == 2 && os.Args[1] == "--scan" {
		os.Exit(scanMain())
	}
	scanner := bufio.NewScanner(os.Stdin)
	scanner.Buffer(make([]byte, 4096), 2*1024*1024)
	if !scanner.Scan() {
		return
	}
	var c config
	if json.Unmarshal(scanner.Bytes(), &c) != nil || len(c.Fields) != 88 || c.Port < 1 || c.Port > 65535 {
		emit(status{State: "error", Error: "INVALID_IMPORT"})
		return
	}
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	go func() {
		for scanner.Scan() {
		}
		cancel()
	}()
	o := newOwner(ctx)
	defer o.stop()
	s, e := start(o, c)
	if e != nil {
		if s.State == "" {
			s = status{State: "error", Error: "TRANSPORT_START_FAILED"}
		}
		emit(s)
		<-ctx.Done()
		return
	}
	emit(status{State: "ready"})
	<-o.ctx.Done()
}
func dnsAddress(f []string) (string, error) {
	mode := f[22]
	if mode == "https" {
		if !strings.HasPrefix(f[21], "https://") {
			return "", errors.New("DNS")
		}
		return f[21], nil
	}
	if mode != "udp" && mode != "tcp" && mode != "tls" {
		return "", errors.New("DNS")
	}
	var out []string
	for _, r := range strings.Split(f[4], ",") {
		r = strings.TrimSpace(r)
		if r == "" {
			continue
		}
		// Native export uses host:port:authoritative. Split from the right so
		// bracketed and unbracketed IPv6 addresses remain intact.
		last := strings.LastIndex(r, ":")
		if last > 0 && (r[last+1:] == "0" || r[last+1:] == "1") && strings.Contains(r[:last], ":") {
			r = r[:last]
		}
		host, p, err := net.SplitHostPort(r)
		if err != nil {
			last = strings.LastIndex(r, ":")
			if last > 0 {
				host, p = r[:last], r[last+1:]
			} else {
				host, p = r, "53"
			}
		}
		host = strings.Trim(host, "[]")
		if host == "" || num(p) < 1 || num(p) > 65535 {
			return "", errors.New("DNS")
		}
		if mode == "tls" && p == "53" {
			p = "853"
		}
		a := endpoint(host, p)
		if mode != "udp" {
			a = mode + "://" + a
		}
		out = append(out, a)
	}
	if len(out) == 0 || len(out) > 16 {
		return "", errors.New("DNS")
	}
	return strings.Join(out, ","), nil
}
func freePort() (int, error) {
	l, e := net.Listen("tcp", "127.0.0.1:0")
	if e != nil {
		return 0, e
	}
	p := l.Addr().(*net.TCPAddr).Port
	l.Close()
	return p, nil
}
func start(o *owner, c config) (status, error) {
	f := c.Fields
	t := f[1]
	dialBudget := 25 * time.Second
	streamSSH := t == "slipstream_ssh"
	if t == "ss" || t == "slipstream" || streamSSH {
		dialBudget = time.Duration(min(60000, max(25000, 10*num(f[6])))) * time.Millisecond
	}
	sshMode := t == "ssh" || strings.HasSuffix(t, "_ssh")
	var rawDial func(context.Context, string) (net.Conn, error)
	dialer := &net.Dialer{Timeout: 20 * time.Second, KeepAlive: 30 * time.Second}
	rawDial = func(ctx context.Context, addr string) (net.Conn, error) { return dialer.DialContext(ctx, "tcp", addr) }
	if t == "dnstt" || t == "dnstt_ssh" || t == "sayedns" || t == "sayedns_ssh" || t == "soar" || t == "soar_ssh" {
		listenPort, e := freePort()
		for attempt := 0; e == nil && listenPort == c.Port && attempt < 16; attempt++ {
			listenPort, e = freePort()
		}
		if listenPort == c.Port {
			return status{}, errors.New("PORT_COLLISION")
		}
		if e != nil {
			return status{}, e
		}
		listen := endpoint("127.0.0.1", strconv.Itoa(listenPort))
		a, e := dnsAddress(f)
		if e != nil {
			return status{}, e
		}
		ready, done := context.WithTimeout(o.ctx, 35*time.Second)
		defer done()
		if strings.HasPrefix(t, "soar") {
			client, e := soar.NewClient(a, f[3], f[11], listen)
			if e != nil {
				return status{}, e
			}
			country := f[80]
			if f[82] == "custom" || f[82] == "app" {
				country = ""
			}
			client.SetDiscovery(f[79] == "1", country)
			if f[22] == "tcp" {
				client.SetTCPMode("only")
			} else {
				client.SetTCPMode(f[81])
			}
			client.SetVerbose(false)
			if f[79] == "1" {
				if f[82] == "custom" {
					client.SetDiscoveryCandidates(f[83])
				} else if f[82] == "app" {
					client.SetDiscoveryCandidates(appDiscovery(resolverInventory))
				}
			}
			o.stops = append(o.stops, client.Stop)
			if e = client.Start(); e != nil {
				return status{State: "error", Error: "DNS_TUNNEL_FAILED"}, e
			}
			if !waitSoar(ready, client) {
				return status{State: "error", Error: "DNS_TUNNEL_FAILED"}, errors.New("soar handshake")
			}
		} else {
			client, e := dns.NewClient(a, f[3], f[11], listen)
			if e != nil {
				return status{}, e
			}
			client.SetAuthoritativeMode(f[27] == "1")
			client.SetNoizMode(strings.HasPrefix(t, "sayedns"))
			client.SetStealthMode(f[38] == "1")
			client.SetMaxPayload(num(f[39]))
			client.SetResolverMode(f[60])
			client.SetRRSpreadCount(int64(num(f[61])))
			if !sshMode {
				client.SetSocksCredentials(f[12], f[13])
			}
			o.stops = append(o.stops, client.Stop)
			if e = client.Start(); e != nil {
				return status{State: "error", Error: "DNS_TUNNEL_FAILED"}, e
			}
			if !client.WaitSession(ready) {
				return status{State: "error", Error: "DNS_TUNNEL_FAILED"}, errors.New("noise handshake")
			}
		}
		if !sshMode {
			return status{}, serveRaw(o, c.Port, listen)
		}
		// DNSTT SSH mode exposes a raw SSH stream. Soar exposes SOCKS instead.
		if strings.HasPrefix(t, "soar") {
			d, e := proxy.SOCKS5("tcp", listen, nil, dialer)
			if e != nil {
				return status{}, e
			}
			cd := d.(proxy.ContextDialer)
			rawDial = func(ctx context.Context, addr string) (net.Conn, error) { return cd.DialContext(ctx, "tcp", addr) }
		} else {
			rawDial = func(ctx context.Context, _ string) (net.Conn, error) { return dialer.DialContext(ctx, "tcp", listen) }
		}
	}
	if t == "vaydns" || t == "vaydns_ssh" || t == "ss" || t == "slipstream" || t == "slipstream_ssh" {
		var stream tunnelStream
		if strings.HasPrefix(t, "vaydns") {
			var err error
			stream, err = startVay(o, f)
			if err != nil {
				return status{}, err
			}
		} else {
			if c.UpstreamPort < 1 || c.UpstreamPort > 65535 {
				return status{}, errors.New("missing Slipstream listener")
			}
			addr := endpoint("127.0.0.1", strconv.Itoa(c.UpstreamPort))
			stream = func(ctx context.Context) (net.Conn, error) { return dialer.DialContext(ctx, "tcp", addr) }
		}
		if !sshMode {
			dial, err := backendSOCKS(stream, f[12], f[13])
			if err != nil {
				return status{}, err
			}
			return status{}, serveSocksWithin(o, c.Port, dial, dialBudget)
		}
		rawDial = func(ctx context.Context, _ string) (net.Conn, error) { return stream(ctx) }
	}
	if c.UpstreamPort > 0 && t == "naive_ssh" {
		d, e := proxy.SOCKS5("tcp", endpoint("127.0.0.1", strconv.Itoa(c.UpstreamPort)), nil, dialer)
		if e != nil {
			return status{}, e
		}
		cd := d.(proxy.ContextDialer)
		rawDial = func(ctx context.Context, addr string) (net.Conn, error) { return cd.DialContext(ctx, "tcp", addr) }
	}
	if t == "vless" {
		return status{}, serveSocks(o, c.Port, func(ctx context.Context, addr string) (net.Conn, error) { return vlessDial(ctx, c, addr) })
	}
	if !sshMode {
		return status{}, errors.New("ENGINE")
	}
	target := f[19]
	if target == "" {
		target = f[3]
	}
	addr := endpoint(target, f[17])
	handshakeBudget := max(35*time.Second, dialBudget)
	ctx, cancel := context.WithTimeout(o.ctx, handshakeBudget)
	defer cancel()
	var conn net.Conn
	var e error
	if t == "ssh" {
		conn, e = sshTransport(ctx, f, addr, rawDial, c.TLSCA)
	} else {
		conn, e = rawDial(ctx, addr)
	}
	if e != nil {
		return status{State: "error", Error: "SSH_CONNECT_FAILED"}, e
	}
	o.add(conn)
	_ = conn.SetDeadline(time.Now().Add(handshakeBudget))
	var trust status
	auth := []ssh.AuthMethod{}
	if f[23] == "key" {
		var signer ssh.Signer
		if dec(f[25]) != "" {
			signer, e = ssh.ParsePrivateKeyWithPassphrase([]byte(dec(f[24])), []byte(dec(f[25])))
		} else {
			signer, e = ssh.ParsePrivateKey([]byte(dec(f[24])))
		}
		if e != nil {
			return status{}, e
		}
		auth = append(auth, ssh.PublicKeys(signer))
	} else {
		auth = append(auth, ssh.Password(f[16]))
	}
	cfg := &ssh.ClientConfig{User: f[15], Auth: auth, Timeout: 35 * time.Second, HostKeyCallback: func(_ string, _ net.Addr, key ssh.PublicKey) error {
		encoded := base64.StdEncoding.EncodeToString(key.Marshal())
		if c.HostKey == encoded {
			return nil
		}
		code := "SSH_TRUST_REQUIRED"
		if c.HostKey != "" {
			code = "SSH_KEY_CHANGED"
		}
		trust = status{State: "trust", Error: code, HostKey: encoded, Fingerprint: ssh.FingerprintSHA256(key)}
		return errors.New("SSH_TRUST_REQUIRED")
	}}
	cc, ch, req, e := ssh.NewClientConn(conn, addr, cfg)
	if e != nil {
		if trust.State != "" {
			return trust, e
		}
		code := "SSH_HANDSHAKE_FAILED"
		if strings.Contains(e.Error(), "unable to authenticate") {
			code = "SSH_AUTH_FAILED"
		}
		return status{State: "error", Error: code}, e
	}
	_ = conn.SetDeadline(time.Time{})
	client := ssh.NewClient(cc, ch, req)
	o.add(client)
	o.wg.Add(1)
	go func() {
		defer o.wg.Done()
		ticker := time.NewTicker(30 * time.Second)
		defer ticker.Stop()
		for {
			select {
			case <-o.ctx.Done():
				return
			case <-ticker.C:
				done := make(chan error, 1)
				go func() { _, _, err := client.SendRequest("keepalive@openssh.com", true, nil); done <- err }()
				timer := time.NewTimer(max(10*time.Second, dialBudget/2))
				select {
				case err := <-done:
					timer.Stop()
					if err != nil {
						o.cancel()
						return
					}
				case <-timer.C:
					client.Close()
					o.cancel()
					return
				case <-o.ctx.Done():
					timer.Stop()
					return
				}

			}
		}
	}()
	return status{}, serveSocksWithin(o, c.Port, func(ctx context.Context, addr string) (net.Conn, error) {
		return client.DialContext(ctx, "tcp", addr)
	}, dialBudget)
}

// Module Start may launch its listener goroutine asynchronously. This is only
// readiness of our owned loopback socket, never a resolver scan or remote probe.
func waitLocal(ctx context.Context, addr string) error {
	ctx, cancel := context.WithTimeout(ctx, 3*time.Second)
	defer cancel()
	d := net.Dialer{Timeout: 100 * time.Millisecond}
	for {
		c, e := d.DialContext(ctx, "tcp", addr)
		if e == nil {
			c.Close()
			return nil
		}
		select {
		case <-ctx.Done():
			return ctx.Err()
		case <-time.After(20 * time.Millisecond):
		}
	}
}
