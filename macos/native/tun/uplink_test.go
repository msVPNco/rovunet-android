package main

import (
	"io"
	"net"
	"path/filepath"
	"strings"
	"testing"
)

func TestEveryTransportEngineHasAnExactRule(t *testing.T) {
	base, err := appBase(fixturePath())
	if err != nil {
		t.Fatal(err)
	}
	paths := make(map[string]bool)
	for _, path := range enginePaths(base) {
		paths[path] = true
	}
	for _, transport := range []string{"xray", "naive", "rovunet-transports", "rovunet-extra", "rovunet-awg", "slipstream-client", "tor/tor", "tor/pluggable_transports/lyrebird"} {
		if !paths[filepath.ToSlash(filepath.Join(base, "engines", transport))] {
			t.Errorf("missing egress rule for %s", transport)
		}
	}
}

func TestUplinkRequiresPasswordAuthentication(t *testing.T) {
	r := validRequest()
	for _, tc := range []struct {
		label  string
		method byte
		status byte
		want   string
	}{
		{"valid", 2, 0, ""}, {"wrong-credential", 2, 1, "MAC_TUN_SOCKS_AUTH_FAILED"}, {"unexpected-noauth", 0, 0, "MAC_TUN_SOCKS_AUTH_FAILED"},
	} {
		t.Run(tc.label, func(t *testing.T) {
			a, b := net.Pipe()
			defer a.Close()
			defer b.Close()
			done := make(chan struct{})
			go func() {
				defer close(done)
				g := make([]byte, 3)
				if _, err := io.ReadFull(a, g); err != nil {
					return
				}
				if string(g) != string([]byte{5, 1, 2}) {
					return
				}
				a.Write([]byte{5, tc.method})
				if tc.method != 2 {
					return
				}
				x := make([]byte, 131)
				if _, err := io.ReadFull(a, x); err != nil {
					return
				}
				if x[0] != 1 || x[1] != 64 || string(x[2:66]) != r.SocksUser || x[66] != 64 || string(x[67:]) != r.SocksPassword {
					return
				}
				a.Write([]byte{1, tc.status})
			}()
			err := authenticateUplink(b, r)
			var got string
			if err != nil {
				got = err.Error()
			}
			if got != tc.want || strings.Contains(got, r.SocksUser) {
				t.Errorf("unexpected result: %s", got)
			}
			b.Close()
			<-done
		})
	}
}

func TestCleanupFailureIsReported(t *testing.T) {
	done := make(chan struct{})
	close(done)
	c := &childWorker{input: nopCloser{}, done: done, waitErr: io.ErrUnexpectedEOF}
	if err := c.Close(); err == nil || err.Error() != "MAC_TUN_RESTORE_PENDING" {
		t.Fatal("failed cleanup reported as restored")
	}
}

type nopCloser struct{}

func (nopCloser) Write(p []byte) (int, error) { return len(p), nil }
func (nopCloser) Close() error                { return nil }
