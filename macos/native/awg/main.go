// Rovunet's private, stdin-only AmneziaWG adapter. Not a general Mihomo config loader.
package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"os/signal"
	"strconv"
	"strings"
	"syscall"

	"github.com/metacubex/mihomo/component/auth"
	"github.com/metacubex/mihomo/config"
	C "github.com/metacubex/mihomo/constant"
	"github.com/metacubex/mihomo/hub/executor"
	logrus "github.com/sirupsen/logrus"
)

type runtimeConfig struct {
	Inbounds []struct {
		Listen   string `json:"listen"`
		Port     int    `json:"port"`
		Protocol string `json:"protocol"`
		Settings struct {
			Auth     string `json:"auth"`
			Accounts []struct {
				User string `json:"user"`
				Pass string `json:"pass"`
			} `json:"accounts"`
		} `json:"settings"`
	} `json:"inbounds"`
	Outbounds []struct {
		Protocol string         `json:"protocol"`
		Settings map[string]any `json:"settings"`
	} `json:"outbounds"`
	Routing struct {
		Rules []struct {
			Type    string   `json:"type"`
			Tag     string   `json:"outboundTag"`
			Domains []string `json:"domain"`
			IPs     []string `json:"ip"`
			Network string   `json:"network"`
		} `json:"rules"`
	} `json:"routing"`
}

func convert(data []byte) ([]byte, string, auth.Authenticator, error) {
	var r runtimeConfig
	if err := json.Unmarshal(data, &r); err != nil {
		return nil, "", nil, err
	}
	if len(r.Inbounds) != 1 || len(r.Outbounds) != 2 || r.Outbounds[0].Protocol != "amneziawg" || r.Outbounds[1].Protocol != "freedom" {
		return nil, "", nil, errors.New("invalid runtime")
	}
	in := r.Inbounds[0]
	if in.Listen != "127.0.0.1" || in.Protocol != "socks" || in.Port < 1 || in.Port > 65535 {
		return nil, "", nil, errors.New("invalid inbound")
	}
	var users []auth.AuthUser
	if in.Settings.Auth == "password" {
		if len(in.Settings.Accounts) != 1 {
			return nil, "", nil, errors.New("invalid auth")
		}
		a := in.Settings.Accounts[0]
		if a.User == "" || a.Pass == "" {
			return nil, "", nil, errors.New("invalid auth")
		}
		users = []auth.AuthUser{{User: a.User, Pass: a.Pass}}
	} else if in.Settings.Auth != "noauth" {
		return nil, "", nil, errors.New("invalid auth")
	}
	proxy := r.Outbounds[0].Settings
	if proxy["type"] != "wireguard" || proxy["amnezia-wg-option"] == nil {
		return nil, "", nil, errors.New("invalid outbound")
	}
	proxy["name"] = "proxy"
	rules := []string{}
	for _, rule := range r.Routing.Rules {
		target := "proxy"
		if rule.Tag == "direct" {
			target = "DIRECT"
		} else if rule.Tag != "proxy" {
			return nil, "", nil, errors.New("invalid rule target")
		}
		if rule.Type != "field" {
			return nil, "", nil, errors.New("invalid rule")
		}
		for _, domain := range rule.Domains {
			if !strings.HasPrefix(domain, "domain:") || strings.ContainsAny(domain, ",\r\n") {
				return nil, "", nil, errors.New("invalid domain rule")
			}
			rules = append(rules, "DOMAIN-SUFFIX,"+strings.TrimPrefix(domain, "domain:")+","+target)
		}
		for _, ip := range rule.IPs {
			if strings.ContainsAny(ip, ",\r\n") {
				return nil, "", nil, errors.New("invalid IP rule")
			}
			kind := "IP-CIDR"
			if strings.Contains(ip, ":") {
				kind = "IP-CIDR6"
			}
			rules = append(rules, kind+","+ip+","+target)
		}
		if rule.Network != "" {
			if rule.Network != "tcp,udp" || len(rule.Domains)+len(rule.IPs) != 0 {
				return nil, "", nil, errors.New("invalid network rule")
			}
			rules = append(rules, "MATCH,"+target)
		}
	}
	rules = append(rules, "MATCH,proxy")
	cfg := map[string]any{"mode": "rule", "allow-lan": false, "bind-address": "127.0.0.1", "ipv6": true, "log-level": "silent", "find-process-mode": "off", "geo-auto-update": false,
		"profile": map[string]bool{"store-selected": false, "store-fake-ip": false}, "dns": map[string]bool{"enable": false}, "ntp": map[string]bool{"enable": false}, "tun": map[string]bool{"enable": false},
		"proxies": []any{proxy}, "rules": rules}
	if len(r.Routing.Rules) > 0 {
		cfg["sniffer"] = map[string]any{"enable": true, "parse-pure-ip": true, "override-destination": false, "sniff": map[string]any{"HTTP": map[string]any{"ports": []int{80, 8080}}, "TLS": map[string]any{"ports": []int{443}}, "QUIC": map[string]any{"ports": []int{443}}}}
	}
	out, err := json.Marshal(cfg)
	return out, "127.0.0.1:" + strconv.Itoa(in.Port), auth.NewAuthenticator(users), err
}

func run() error {
	// Library logs can contain peer endpoints. Only fixed lifecycle markers leave this child.
	logrus.SetOutput(io.Discard)
	data, err := io.ReadAll(io.LimitReader(os.Stdin, 4*1024*1024+1))
	if err != nil || len(data) > 4*1024*1024 {
		return errors.New("input")
	}
	raw, address, authenticator, err := convert(data)
	if err != nil {
		return err
	}
	dir, err := os.MkdirTemp("", "rovunet-awg-")
	if err != nil {
		return err
	}
	defer os.RemoveAll(dir)
	C.SetHomeDir(dir)
	cfg, err := config.Parse(raw)
	if err != nil {
		return err
	}
	executor.ApplyConfig(cfg, true)
	defer executor.Shutdown()
	listener, err := newLocalListener(address, authenticator)
	if err != nil {
		return err
	}
	defer listener.Close()
	fmt.Fprintln(os.Stdout, "ROVUNET_AWG_READY")
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, syscall.SIGTERM, syscall.SIGINT)
	defer signal.Stop(stop)
	<-stop
	return nil
}
func main() {
	if run() != nil {
		fmt.Fprintln(os.Stdout, "ROVUNET_AWG_FAILED")
		os.Exit(1)
	}
}

