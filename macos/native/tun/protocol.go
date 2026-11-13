// Rovunet's bounded TUN control protocol. No profile, command, path or raw
// Mihomo configuration can be supplied to the privileged service.
package main

import (
	"bytes"
	"encoding/binary"
	"encoding/json"
	"errors"
	"io"
	"path/filepath"
	"regexp"
	"strings"
)

const version = "0.1.0-dev.4"
const socketPath = "/var/run/rovunet-tun/control.sock"
const policyPath = "/Library/PrivilegedHelperTools/org.rovunet.tun.policy.json"
const maxFrame = 16384

type request struct {
	Action        string
	SocksPort     int
	SocksUser     string
	SocksPassword string
}
type reply struct {
	State     string
	Error     string
	Version   string
	Interface string
	IPv6      bool
	DNS       bool
	UDP       bool
}
type workerRequest struct {
	Proxy request
	Base  string
}

var token = regexp.MustCompile(`^[0-9a-f]{64}$`)

func (r request) validate() error {
	if r.Action == "status" || r.Action == "stop" {
		if r.SocksPort != 0 || r.SocksUser != "" || r.SocksPassword != "" {
			return errors.New("MAC_TUN_INVALID_REQUEST")
		}
		return nil
	}
	if r.Action != "start" || r.SocksPort < 1 || r.SocksPort > 65535 || !token.MatchString(r.SocksUser) || !token.MatchString(r.SocksPassword) {
		return errors.New("MAC_TUN_INVALID_REQUEST")
	}
	return nil
}
func readFrame(r io.Reader, value any) error {
	var header [4]byte
	if _, e := io.ReadFull(r, header[:]); e != nil {
		return e
	}
	size := binary.LittleEndian.Uint32(header[:])
	if size == 0 || size > maxFrame {
		return errors.New("MAC_TUN_INVALID_REQUEST")
	}
	body := make([]byte, size)
	if _, e := io.ReadFull(r, body); e != nil {
		return e
	}
	// Reject duplicate properties, including differently cased aliases.
	decoder := json.NewDecoder(bytes.NewReader(body))
	start, e := decoder.Token()
	if e != nil || start != json.Delim('{') {
		return errors.New("MAC_TUN_INVALID_REQUEST")
	}
	seen := map[string]bool{}
	for decoder.More() {
		key, e := decoder.Token()
		if e != nil {
			return e
		}
		name, ok := key.(string)
		if !ok || seen[strings.ToLower(name)] {
			return errors.New("MAC_TUN_INVALID_REQUEST")
		}
		seen[strings.ToLower(name)] = true
		var discard json.RawMessage
		if e = decoder.Decode(&discard); e != nil {
			return e
		}
	}
	if _, e = decoder.Token(); e != nil {
		return e
	}
	if decoder.Decode(new(any)) != io.EOF {
		return errors.New("MAC_TUN_INVALID_REQUEST")
	}
	decoder = json.NewDecoder(bytes.NewReader(body))
	decoder.DisallowUnknownFields()
	return decoder.Decode(value)
}
func writeFrame(w io.Writer, value any) error {
	data, e := json.Marshal(value)
	if e != nil || len(data) > maxFrame {
		return errors.New("MAC_TUN_INVALID_REQUEST")
	}
	var h [4]byte
	binary.LittleEndian.PutUint32(h[:], uint32(len(data)))
	if _, e = w.Write(h[:]); e != nil {
		return e
	}
	_, e = w.Write(data)
	return e
}
func appBase(clientPath string) (string, error) {
	clean := filepath.Clean(clientPath)
	suffix := filepath.Join("Contents", "MacOS", "engines", "rovunet-tun")
	if !filepath.IsAbs(clean) || !strings.HasSuffix(clean, suffix) || strings.ContainsAny(clean, ",\r\n\x00") || len(clean) > 2048 {
		return "", errors.New("MAC_TUN_APP_PATH_INVALID")
	}
	return filepath.Dir(filepath.Dir(clean)), nil
}
func enginePaths(base string) []string {
	names := []string{"xray", "naive", "rovunet-transports", "rovunet-extra", "rovunet-awg", "slipstream-client", "tor/tor", "tor/pluggable_transports/lyrebird"}
	out := []string{}
	for _, name := range names {
		out = append(out, filepath.ToSlash(filepath.Join(base, "engines", name)))
	}
	return out
}
