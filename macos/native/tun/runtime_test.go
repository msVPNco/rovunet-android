package main

import (
	"bytes"
	"context"
	"encoding/binary"
	"encoding/json"
	"io"
	"net"
	"path/filepath"
	"strings"
	"sync"
	"testing"
	"time"

	"github.com/metacubex/mihomo/config"
)

func validRequest() request {
	return request{Action: "start", SocksPort: 10808, SocksUser: strings.Repeat("a", 64), SocksPassword: strings.Repeat("b", 64)}
}
func fixturePath() string {
	base, _ := filepath.Abs("Rovunet.app/Contents/MacOS/engines/rovunet-tun")
	return base
}
func TestControlInputIsBounded(t *testing.T) {
	for _, value := range []string{`{"Action":"start","action":"stop"}`, `{"Action":"status","Path":"/tmp/evil"}`, `{"Action":"status"} {}`, `[]`} {
		var stream bytes.Buffer
		_ = binary.Write(&stream, binary.LittleEndian, uint32(len(value)))
		stream.WriteString(value)
		var r request
		if readFrame(&stream, &r) == nil {
			t.Fatal("invalid protocol accepted")
		}
	}
	var stream bytes.Buffer
	_ = binary.Write(&stream, binary.LittleEndian, uint32(maxFrame+1))
	var r request
	if readFrame(&stream, &r) == nil {
		t.Fatal("oversized request accepted")
	}
	good := validRequest()
	stream.Reset()
	if writeFrame(&stream, good) != nil || readFrame(&stream, &r) != nil || r != good {
		t.Fatal("valid secret frame did not roundtrip")
	}
	for _, bad := range []request{{Action: "start", SocksPort: 0, SocksUser: good.SocksUser, SocksPassword: good.SocksPassword}, {Action: "start", SocksPort: 10808, SocksUser: "a", SocksPassword: good.SocksPassword}, {Action: "status", SocksUser: good.SocksUser}, {Action: "exec"}} {
		if bad.validate() == nil {
			t.Fatal("unsafe request validated")
		}
	}
}
func TestTunConfigurationHasNoDirectFallback(t *testing.T) {
	base, e := appBase(fixturePath())
	if e != nil {
		t.Fatal(e)
	}
	raw, e := configuration(validRequest(), base, nil)
	if e != nil {
		t.Fatal(e)
	}
	cfg, e := config.Parse(raw)
	if e != nil {
		t.Fatal(e)
	}
	if cfg.Proxies["ROVUNET"] == nil || !cfg.General.IPv6 || cfg.General.Tun.Enable {
		t.Fatal("unexpected proxy or listener configuration")
	}
	var source map[string]any
	_ = json.Unmarshal(raw, &source)
	dns := source["dns"].(map[string]any)
	for _, value := range dns["nameserver"].([]any) {
		if !strings.HasSuffix(value.(string), "#ROVUNET") {
			t.Fatal("DNS bypasses selected proxy")
		}
	}
	if len(dns["fallback"].([]any)) != 0 || dns["use-system-hosts"] != false {
		t.Fatal("system DNS fallback")
	}
	rules := source["rules"].([]any)
	if rules[len(rules)-1] != "MATCH,ROVUNET" {
		t.Fatal("non-engine traffic bypasses proxy")
	}
	if len(rules) != len(enginePaths(base))+2 || rules[len(rules)-2] != "DST-PORT,53,ROVUNET-DNS" || cfg.Proxies["ROVUNET-DNS"] == nil {
		t.Fatal("DNS routing order is not engine / internal DNS / selected proxy")
	}
	if len(tunOptions("utun90").DNSHijack) != 0 {
		t.Fatal("wildcard hijack intercepts engine bootstrap before process matching")
	}
	for i, rule := range rules[:len(rules)-2] {
		if rule != "PROCESS-PATH,"+enginePaths(base)[i]+",DIRECT" {
			t.Fatal("unbounded process bypass")
		}
	}
	if strings.Contains(string(raw), "GEOSITE") || strings.Contains(string(raw), "GEOIP") {
		t.Fatal("external geodata dependency")
	}
}

type fakeWorker struct {
	done chan struct{}
	once sync.Once
}

func (f *fakeWorker) Done() <-chan struct{} { return f.done }
func (f *fakeWorker) Ready() reply {
	return reply{State: "active", Version: version, DNS: true, IPv6: true, UDP: true}
}
func (f *fakeWorker) Close() error { f.once.Do(func() { close(f.done) }); return nil }
func TestLeaseDiesWithAuthenticatedChannel(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	worker := &fakeWorker{done: make(chan struct{})}
	s := &service{lease: make(chan struct{}, 1), spawn: func(context.Context, request, string) (sessionWorker, error) { return worker, nil }}
	server, client := net.Pipe()
	defer client.Close()
	finished := make(chan struct{})
	go func() { s.serve(ctx, server, peer{UID: 501, Path: fixturePath()}); close(finished) }()
	_ = client.SetDeadline(time.Now().Add(3 * time.Second))
	if writeFrame(client, validRequest()) != nil {
		t.Fatal("start failed")
	}
	var ready reply
	if readFrame(client, &ready) != nil || ready.State != "active" {
		t.Fatal("lease not active")
	}
	secondServer, secondClient := net.Pipe()
	defer secondClient.Close()
	go s.serve(ctx, secondServer, peer{UID: 501, Path: fixturePath()})
	_ = secondClient.SetDeadline(time.Now().Add(3 * time.Second))
	_ = writeFrame(secondClient, validRequest())
	var busy reply
	_ = readFrame(secondClient, &busy)
	if busy.Error != "MAC_TUN_BUSY" {
		t.Fatal("concurrent lease accepted")
	}
	client.Close()
	select {
	case <-finished:
	case <-time.After(3 * time.Second):
		t.Fatal("EOF did not stop lease")
	}
	select {
	case <-worker.Done():
	default:
		t.Fatal("worker survives parent pipe")
	}
	if len(s.lease) != 0 {
		t.Fatal("exclusive lease not released")
	}
}
func TestUnactionableStatusDoesNotSpawnRootWorker(t *testing.T) {
	s := &service{lease: make(chan struct{}, 1), spawn: func(context.Context, request, string) (sessionWorker, error) {
		t.Error("status spawned a root worker")
		return nil, io.EOF
	}}
	server, client := net.Pipe()
	defer client.Close()
	go s.serve(context.Background(), server, peer{UID: 501, Path: fixturePath()})
	_ = client.SetDeadline(time.Now().Add(3 * time.Second))
	_ = writeFrame(client, request{Action: "status"})
	var ready reply
	if readFrame(client, &ready) != nil || ready.State != "available" {
		t.Fatal("helper availability not reported")
	}
}
