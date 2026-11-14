package main

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/netip"
	"os"
	"strconv"
	"strings"
	"time"

	"github.com/metacubex/mihomo/adapter"
	"github.com/metacubex/mihomo/component/ca"
	C "github.com/metacubex/mihomo/constant"
	logrus "github.com/sirupsen/logrus"
)

func probeMapping(data []byte) (map[string]any, error) {
	var out struct {
		Protocol string         `json:"protocol"`
		Settings map[string]any `json:"settings"`
		Stream   map[string]any `json:"streamSettings"`
	}
	if err := json.Unmarshal(data, &out); err != nil {
		return nil, err
	}
	var proxy map[string]any
	switch out.Protocol {
	case "tuic", "amneziawg":
		if !validProxy(out.Protocol, out.Settings) {
			return nil, errors.New("invalid proxy")
		}
		proxy = out.Settings
	case "hysteria":
		if out.Settings["version"] != float64(2) || out.Stream["network"] != "hysteria" {
			return nil, errors.New("unsupported Hysteria")
		}
		hs, ok := out.Stream["hysteriaSettings"].(map[string]any)
		if !ok {
			return nil, errors.New("missing Hysteria auth")
		}
		ts, ok := out.Stream["tlsSettings"].(map[string]any)
		if !ok || ts["allowInsecure"] == true || ts["cipherSuites"] != nil {
			return nil, errors.New("TLS verification required")
		}
		// Port hopping/finalmask profiles need the original Xray transport. Do not
		// silently simplify an imported mask or measure another transport instead.
		if out.Stream["finalmask"] != nil {
			return nil, errors.New("unsupported probe mask")
		}
		proxy = map[string]any{"type": "hysteria2", "server": out.Settings["address"], "port": out.Settings["port"], "password": hs["auth"], "sni": ts["serverName"], "skip-cert-verify": false}
	case "wireguard":
		addresses, ok := out.Settings["address"].([]any)
		if !ok {
			return nil, errors.New("missing addresses")
		}
		peers, ok := out.Settings["peers"].([]any)
		if !ok || len(peers) != 1 {
			return nil, errors.New("one peer required")
		}
		peer, ok := peers[0].(map[string]any)
		if !ok {
			return nil, errors.New("invalid peer")
		}
		endpoint, ok := peer["endpoint"].(string)
		if !ok {
			return nil, errors.New("missing endpoint")
		}
		host, portText, err := net.SplitHostPort(endpoint)
		if err != nil {
			return nil, err
		}
		port, err := strconv.Atoi(portText)
		if err != nil {
			return nil, err
		}
		proxy = map[string]any{"type": "wireguard", "private-key": out.Settings["secretKey"], "public-key": peer["publicKey"], "server": host, "port": port, "udp": true, "mtu": out.Settings["mtu"]}
		if peer["preSharedKey"] != nil {
			proxy["pre-shared-key"] = peer["preSharedKey"]
		}
		if peer["allowedIPs"] != nil {
			proxy["allowed-ips"] = peer["allowedIPs"]
		}
		for _, value := range addresses {
			text, ok := value.(string)
			if !ok {
				return nil, errors.New("invalid address")
			}
			prefix, err := netip.ParsePrefix(text)
			if err != nil {
				return nil, err
			}
			if prefix.Addr().Is4() {
				proxy["ip"] = prefix.Addr().String()
			} else {
				proxy["ipv6"] = prefix.Addr().String()
			}
		}
	default:
		return nil, errors.New("unsupported protocol")
	}
	proxy["name"] = "proxy"
	return proxy, nil
}

type probeError struct {
	stage string
	cause error
}

func (e *probeError) Error() string { return e.cause.Error() }
func probeFailure(err error) string {
	stage := "runtime"
	if failure, ok := err.(*probeError); ok {
		stage = failure.stage
		err = failure.cause
	}
	text := strings.ToLower(err.Error())
	kind := "failed"
	switch {
	case strings.Contains(text, "permission") || strings.Contains(text, "not permitted"):
		kind = "permission"
	case strings.Contains(text, "deadline") || strings.Contains(text, "timed out"):
		kind = "timeout"
	case strings.Contains(text, "certificate") || strings.Contains(text, "tls") || strings.Contains(text, "crypto_error"):
		kind = "certificate"
	case strings.Contains(text, "authentication"):
		kind = "auth"
	case strings.Contains(text, "socket") || strings.Contains(text, "bind") || strings.Contains(text, "address"):
		kind = "socket"
	case strings.Contains(text, "key") || strings.Contains(text, "peer"):
		kind = "identity"
	case strings.Contains(text, "network") || strings.Contains(text, "route"):
		kind = "network"
	case strings.Contains(text, "unsupported") || strings.Contains(text, "invalid") || strings.Contains(text, "missing"):
		kind = "configuration"
	}
	return stage + ":" + kind
}

func runProbe() error {
	logrus.SetOutput(io.Discard)
	directory, err := os.MkdirTemp("", "rovunet-probe-")
	if err != nil {
		return &probeError{"storage", err}
	}
	defer os.RemoveAll(directory)
	C.SetHomeDir(directory)
	data, err := io.ReadAll(io.LimitReader(os.Stdin, 65537))
	if err != nil || len(data) > 65536 {
		return errors.New("input")
	}
	proxy, err := probeMapping(data)
	if err != nil {
		return &probeError{"config", err}
	}
	ca.RovunetCertificateDirectory = os.Getenv("ROVUNET_CERTIFICATE_STORE")
	ca.RovunetCertificateEndpoint = fmt.Sprint(proxy["server"]) + ":" + fmt.Sprint(proxy["port"])
	parsed, err := adapter.ParseProxy(proxy)
	if err != nil {
		return &probeError{"adapter", err}
	}
	defer parsed.Close()
	handshaker, ok := parsed.Adapter().(interface{ RovunetHandshake(context.Context) error })
	if !ok {
		return errors.New("unsupported handshake")
	}
	ctx, cancel := context.WithTimeout(context.Background(), 8*time.Second)
	defer cancel()
	started := time.Now()
	if err := handshaker.RovunetHandshake(ctx); err != nil {
		return &probeError{"handshake", err}
	}
	return json.NewEncoder(os.Stdout).Encode(map[string]any{"kind": "protocol-handshake", "latencyMs": time.Since(started).Milliseconds()})
}
