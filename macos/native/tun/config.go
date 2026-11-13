package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/netip"
	"os"
	"runtime"
	"strconv"
	"sync"

	"github.com/metacubex/mihomo/config"
	C "github.com/metacubex/mihomo/constant"
	"github.com/metacubex/mihomo/hub/executor"
	LC "github.com/metacubex/mihomo/listener/config"
	singtun "github.com/metacubex/mihomo/listener/sing_tun"
	"github.com/metacubex/mihomo/tunnel"
)

func configuration(r request, base string, dnsServers []string) ([]byte, error) {
	if e := r.validate(); e != nil || r.Action != "start" {
		return nil, errors.New("MAC_TUN_INVALID_REQUEST")
	}
	if _, e := appBase(base + "/engines/rovunet-tun"); e != nil {
		return nil, e
	}
	rules := []string{}
	for _, path := range enginePaths(base) {
		rules = append(rules, "PROCESS-PATH,"+path+",DIRECT")
	}
	// The DNS tunnel transports themselves speak DNS to a configured remote
	// resolver. Their process rules must run before ordinary port-53 traffic
	// is handed to the proxied DNS resolver. sing-tun still owns its two
	// virtual resolver addresses.
	rules = append(rules, "DST-PORT,53,ROVUNET-DNS", "MATCH,ROVUNET")
	if dnsServers == nil {
		dnsServers = []string{"https://1.1.1.1/dns-query#ROVUNET", "https://8.8.8.8/dns-query#ROVUNET"}
	}
	cfg := map[string]any{"mode": "rule", "allow-lan": false, "ipv6": true, "log-level": "silent", "find-process-mode": "always", "geo-auto-update": false,
		"profile": map[string]bool{"store-selected": false, "store-fake-ip": false}, "ntp": map[string]bool{"enable": false}, "tun": map[string]bool{"enable": false},
		"dns":     map[string]any{"enable": true, "ipv6": true, "enhanced-mode": "redir-host", "listen": "", "use-hosts": false, "use-system-hosts": false, "default-nameserver": []string{"1.1.1.1"}, "nameserver": dnsServers, "fallback": []string{}, "proxy-server-nameserver": []string{}, "respect-rules": false, "fallback-filter": map[string]any{"geoip": false, "geosite": []string{}}},
		"proxies": []any{map[string]any{"name": "ROVUNET", "type": "socks5", "server": "127.0.0.1", "port": r.SocksPort, "username": r.SocksUser, "password": r.SocksPassword, "udp": true}, map[string]any{"name": "ROVUNET-DNS", "type": "dns"}}, "rules": rules}
	return json.Marshal(cfg)
}
func freeDevice() string {
	interfaces, _ := net.Interfaces()
	used := map[string]bool{}
	for _, i := range interfaces {
		used[i.Name] = true
	}
	prefix := "utun"
	if runtime.GOOS != "darwin" {
		prefix = "rovunet"
	}
	for n := 90; n < 190; n++ {
		name := prefix + strconv.Itoa(n)
		if !used[name] {
			return name
		}
	}
	return ""
}
func startDataPlane(r request, base string, dnsServers []string) (io.Closer, reply, error) {
	device := freeDevice()
	if device == "" {
		return nil, reply{}, errors.New("MAC_TUN_DEVICE_BUSY")
	}
	return openDataPlane(r, base, dnsServers, tunOptions(device))
}

func tunOptions(device string) LC.Tun {
	dnsMode := "disabled"
	if runtime.GOOS == "darwin" {
		dnsMode = "native"
	}
	return LC.Tun{Enable: true, Device: device, Stack: C.TunGvisor, DNSMode: dnsMode, AutoRoute: true, AutoDetectInterface: true, StrictRoute: true, MTU: 1500,
		Inet4Address: []netip.Prefix{netip.MustParsePrefix("198.18.0.1/30")}, Inet6Address: []netip.Prefix{netip.MustParsePrefix("fdfe:dcba:9876::1/126")}, UDPTimeout: 120, DisableICMPForwarding: true}
}

// Production callers supply the options constructed above. The packet fixture
// uses a connected file descriptor and no host routes or DNS registration.
func openDataPlane(r request, base string, dnsServers []string, options LC.Tun) (io.Closer, reply, error) {
	raw, e := configuration(r, base, dnsServers)
	if e != nil {
		return nil, reply{}, e
	}
	if e = checkUplink(r); e != nil {
		return nil, reply{}, e
	}
	cfg, e := config.Parse(raw)
	if e != nil {
		return nil, reply{}, errors.New("MAC_TUN_CONFIG_FAILED")
	}
	executor.ApplyConfig(cfg, true)
	_ = os.Setenv("SKIP_SYSTEM_IPV6_CHECK", "1")
	listener, e := singtun.New(options, tunnel.Tunnel)
	if e != nil {
		executor.Shutdown()
		return nil, reply{}, fmt.Errorf("MAC_TUN_START_FAILED: %w", e)
	}
	return &plane{closer: listener}, reply{State: "active", Version: version, Interface: options.Device, IPv6: true, DNS: true, UDP: true}, nil
}

type plane struct {
	closer io.Closer
	once   sync.Once
	err    error
}

func (p *plane) Close() error {
	p.once.Do(func() { p.err = p.closer.Close(); executor.Shutdown() })
	return p.err
}
