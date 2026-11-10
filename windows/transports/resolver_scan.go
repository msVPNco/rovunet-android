// SPDX-License-Identifier: AGPL-3.0-only
package main

import (
	"bufio"
	"context"
	"encoding/json"
	"math/rand/v2"
	"net/http"
	"net/netip"
	"os"
	"strings"
	"sync"
	"time"

	soar "soar-mobile/soar"
	wire "www.bamsoftware.com/git/dnstt.git/dns"
)

type scanRequest struct {
	Mode, Family, Zone, PublicKey                      string
	Candidates, Transports                             []string
	Workers, TimeoutMS, Count, Threshold, ResponseSize int
	QuerySize                                          int
	Prefilter                                          bool
}

type scanResult struct {
	Index                              int
	Transport                          string
	Dns, Handshake, Pass               bool
	Passed, Attempted, Completed, Edns int
	Checks                             []string
	CompletedChecks                    []string
	ElapsedMS                          int64
	DnsElapsedMS                       int64
	Error                              string
}

func (s scanRequest) valid() bool {
	if len(s.Candidates) == 0 || len(s.Candidates) > 2048 || len(s.Transports) == 0 || len(s.Transports) > 2 ||
		s.QuerySize < 0 || s.QuerySize > 4096 ||
		s.Workers < 1 || s.Workers > 50 || s.TimeoutMS < 200 || s.TimeoutMS > 90000 || s.Count < 1 || s.Count > 30 ||
		s.Threshold < 1 || s.Threshold > s.Count || s.ResponseSize != 0 && (s.ResponseSize < 200 || s.ResponseSize > 4096) {
		return false
	}
	if s.Mode != "simple" && s.Mode != "advanced" && s.Mode != "prism" && s.Mode != "e2e" {
		return false
	}
	n, err := wire.ParseName(s.Zone)
	if err != nil || len(n) == 0 || strings.TrimSuffix(s.Zone, ".") != s.Zone {
		return false
	}
	if len(s.Transports) == 2 && !(s.Transports[0] == "udp" && s.Transports[1] == "tcp") {
		return false
	}
	for _, mode := range s.Transports {
		if mode != "udp" && mode != "tcp" && mode != "tls" && mode != "https" {
			return false
		}
	}
	if s.Mode == "prism" && s.Family != "soar" {
		if _, err := prismKey(s.PublicKey); err != nil || s.Family != "dnstt" && s.Family != "sayedns" && s.Family != "vaydns" {
			return false
		}
	}
	return true
}

func soarHandshake(ctx context.Context, address, zone, key, transport string) bool {
	if transport != "udp" && transport != "tcp" {
		return false
	}
	if _, err := netip.ParseAddrPort(address); err != nil {
		return false
	}
	client, err := soar.NewClient(address, zone, key, "127.0.0.1:0")
	if err != nil {
		return false
	}
	client.SetDiscovery(false, "")
	if transport == "tcp" {
		client.SetTCPMode("only")
	} else {
		client.SetTCPMode("off")
	}
	if err = client.Start(); err != nil {
		return false
	}
	defer client.Stop()
	return waitSoar(ctx, client)
}

func waitSoar(ctx context.Context, client *soar.SoarClient) bool {
	timer := time.NewTicker(25 * time.Millisecond)
	defer timer.Stop()
	for {
		if client.IsEstablished() && client.VerifiedPathCount() > 0 {
			return true
		}
		select {
		case <-ctx.Done():
			return false
		case <-timer.C:
		}
	}
}

func repeatPrism(ctx context.Context, s scanRequest, row *scanResult, probe func(context.Context) bool) {
	for row.Attempted < s.Count && ctx.Err() == nil && row.Passed < s.Threshold && row.Passed+s.Count-row.Attempted >= s.Threshold {
		trial, cancel := context.WithTimeout(ctx, time.Duration(max(200, s.TimeoutMS/s.Threshold))*time.Millisecond)
		row.Attempted++
		if probe(trial) {
			row.Passed++
		}
		cancel()
	}
	row.Pass = row.Passed >= s.Threshold
}

func (s scanRequest) examine(ctx context.Context, client *http.Client, index int, transport string) scanResult {
	started := time.Now()
	row := scanResult{Index: index, Transport: transport}
	p := resolverProbe{address: s.Candidates[index], transport: transport, client: client}
	run, cancel := context.WithTimeout(ctx, time.Duration(s.TimeoutMS)*time.Millisecond)
	defer cancel()
	switch {
	case s.Mode == "e2e":
		// The separate read-only Windows probe runs the selected full profile.
		// No DNS prefilter is allowed to discard an E2E candidate.
		row.Pass = true
	case s.Mode == "prism":
		if s.Prefilter {
			fast, stop := context.WithTimeout(run, time.Duration(min(1000, s.TimeoutMS/3))*time.Millisecond)
			_, err := p.exchange(fast, randomLabel(6)+"."+s.Zone, wire.RRTypeA, 0)
			stop()
			row.Dns = err == nil
			if !row.Dns {
				row.Error = "DNS_FAILED"
				break
			}
		}
		if s.Family == "soar" {
			repeatPrism(run, s, &row, func(trial context.Context) bool {
				return soarHandshake(trial, p.address, s.Zone, s.PublicKey, transport)
			})
			row.Handshake = row.Passed > 0
		} else {
			key, _ := prismKey(s.PublicKey)
			repeatPrism(run, s, &row, func(trial context.Context) bool { return p.prism(trial, s.Zone, key, s.ResponseSize) })
			row.Dns = row.Dns || row.Passed > 0
		}
	case s.Mode == "simple" && s.Family == "soar":
		row.Handshake = soarHandshake(run, p.address, s.Zone, s.PublicKey, transport)
		row.Pass = row.Handshake
	default:
		p.diagnostic(run, s.Zone, &row, s.QuerySize)
		row.Pass = row.Dns
	}
	if !row.Pass && row.Error == "" {
		switch {
		case run.Err() != nil:
			row.Error = "TEST_TIMEOUT"
		case s.Mode == "prism":
			row.Error = "PRISM_FAILED"
		case s.Family == "soar" && s.Mode == "simple":
			row.Error = "DNS_TUNNEL_FAILED"
		default:
			row.Error = "DNS_FAILED"
		}
	}
	row.ElapsedMS = time.Since(started).Milliseconds()
	return row
}

func scanResolvers(ctx context.Context, s scanRequest, send func(scanResult)) {
	client, cleanup := scanHTTPClient()
	defer cleanup()
	type target struct {
		index     int
		transport string
	}
	jobs := make(chan target)
	var workers sync.WaitGroup
	for i := 0; i < s.Workers; i++ {
		workers.Add(1)
		go func() {
			defer workers.Done()
			for job := range jobs {
				if ctx.Err() != nil {
					return
				}
				send(s.examine(ctx, client, job.index, job.transport))
			}
		}()
	}
outer:
	for i := range s.Candidates {
		for _, mode := range s.Transports {
			select {
			case <-ctx.Done():
				break outer
			case jobs <- target{i, mode}:
			}
		}
	}
	close(jobs)
	workers.Wait()
}

func scanMain() int {
	input := bufio.NewScanner(os.Stdin)
	input.Buffer(make([]byte, 4096), 2<<20)
	var request scanRequest
	if !input.Scan() || json.Unmarshal(input.Bytes(), &request) != nil || !request.valid() {
		return 2
	}
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	go func() {
		for input.Scan() {
		}
		cancel()
	}()
	var mu sync.Mutex
	enc := json.NewEncoder(os.Stdout)
	scanResolvers(ctx, request, func(r scanResult) {
		mu.Lock()
		defer mu.Unlock()
		if enc.Encode(r) != nil {
			cancel()
		}
	})
	return 0
}

// Consume the published resolver inventory only when the profile explicitly selects app discovery.
func appDiscovery(text string) string {
	scanner := bufio.NewScanner(strings.NewReader(text))
	result := make([]string, 0, 5000)
	seen := map[netip.Addr]bool{}
	for scanner.Scan() {
		ip, err := netip.ParseAddr(strings.TrimSpace(scanner.Text()))
		if err != nil || seen[ip] {
			continue
		}
		seen[ip] = true
		if len(result) < 5000 {
			result = append(result, ip.String())
		} else if n := rand.IntN(len(seen)); n < len(result) {
			result[n] = ip.String()
		}
	}
	return strings.Join(result, ",")
}
