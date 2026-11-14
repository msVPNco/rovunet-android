package ca

import (
	"bytes"
	"crypto/sha256"
	"crypto/x509"
	"encoding/hex"
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"time"

	"github.com/metacubex/tls"
)

// Set once before constructing this isolated child's single outbound.
var RovunetCertificateDirectory string
var RovunetCertificateEndpoint string

type rovunetIdentity struct {
	Version     int    `json:"version"`
	PublicCA    bool   `json:"publicCa"`
	Certificate []byte `json:"certificate"`
}

func rovunetAutomaticCertificate(config *tls.Config, option Option) {
	if RovunetCertificateDirectory == "" || RovunetCertificateEndpoint == "" || config.InsecureSkipVerify || option.Fingerprint != "" || option.NameCertVerify != "" || option.ZeroTrust {
		return
	}
	directory, endpoint := RovunetCertificateDirectory, RovunetCertificateEndpoint
	host := config.ServerName
	digest := sha256.Sum256([]byte(endpoint + "|" + strings.ToLower(host)))
	path := filepath.Join(directory, hex.EncodeToString(digest[:])+".json")
	// Go verifies CertificateVerify before invoking VerifyConnection. This callback
	// owns all remaining chain, name, time and previously stored identity checks.
	config.InsecureSkipVerify = true
	config.VerifyConnection = func(state tls.ConnectionState) error {
		if len(state.PeerCertificates) == 0 || len(state.PeerCertificates) > 16 {
			return errors.New("invalid certificate chain")
		}
		cert := state.PeerCertificates[0]
		now := time.Now()
		if config.Time != nil {
			now = config.Time()
		}
		if len(cert.Raw) > 32768 || now.Before(cert.NotBefore) || now.After(cert.NotAfter) {
			return errors.New("certificate not valid")
		}
		if err := cert.VerifyHostname(host); err != nil {
			return errors.New("certificate name mismatch")
		}
		intermediates := x509.NewCertPool()
		for _, parent := range state.PeerCertificates[1:] {
			intermediates.AddCert(parent)
		}
		_, chainError := cert.Verify(x509.VerifyOptions{DNSName: host, Roots: config.RootCAs, Intermediates: intermediates, CurrentTime: now})
		publicCA := chainError == nil
		known, err := rovunetReadIdentity(path)
		if os.IsNotExist(err) {
			created, err := rovunetWriteIdentity(path, rovunetIdentity{Version: 1, PublicCA: publicCA, Certificate: cert.Raw}, false)
			if err != nil {
				return err
			}
			if created {
				return nil
			}
			// Another process published first. Its policy/identity wins; never replace it.
			known, err = rovunetReadIdentity(path)
		}
		if err != nil {
			return err
		}
		previous, err := x509.ParseCertificate(known.Certificate)
		if err != nil {
			return err
		}
		if !known.PublicCA || !publicCA {
			if sha256.Sum256(previous.RawSubjectPublicKeyInfo) != sha256.Sum256(cert.RawSubjectPublicKeyInfo) {
				return errors.New("server certificate identity changed")
			}
			return nil
		}
		// Public CA renewal may change the key. A later untrusted key cannot use this
		// renewal path, and a private TOFU pin can never become a public cache entry.
		if bytes.Equal(known.Certificate, cert.Raw) {
			return nil
		}
		_, err = rovunetWriteIdentity(path, rovunetIdentity{Version: 1, PublicCA: true, Certificate: cert.Raw}, true)
		return err
	}
}

func rovunetReadIdentity(path string) (rovunetIdentity, error) {
	var identity rovunetIdentity
	info, err := os.Lstat(path)
	if err != nil {
		return identity, err
	}
	if !info.Mode().IsRegular() || info.Size() > 65536 {
		return identity, errors.New("invalid certificate store")
	}
	data, err := os.ReadFile(path)
	if err != nil {
		return identity, err
	}
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.DisallowUnknownFields()
	if err = decoder.Decode(&identity); err != nil {
		return identity, err
	}
	if identity.Version != 1 || len(identity.Certificate) == 0 || len(identity.Certificate) > 32768 {
		return identity, errors.New("invalid certificate store")
	}
	if _, err = x509.ParseCertificate(identity.Certificate); err != nil {
		return identity, err
	}
	return identity, nil
}

func rovunetWriteIdentity(path string, identity rovunetIdentity, replacePublic bool) (bool, error) {
	if err := os.MkdirAll(filepath.Dir(path), 0700); err != nil {
		return false, err
	}
	file, err := os.CreateTemp(filepath.Dir(path), ".certificate-")
	if err != nil {
		return false, err
	}
	temporary := file.Name()
	defer os.Remove(temporary)
	if err = json.NewEncoder(file).Encode(identity); err == nil {
		err = file.Sync()
	}
	closeErr := file.Close()
	if err != nil {
		return false, err
	}
	if closeErr != nil {
		return false, closeErr
	}
	if replacePublic {
		err = os.Rename(temporary, path)
	} else {
		err = os.Link(temporary, path)
	}
	if os.IsExist(err) && !replacePublic {
		return false, nil
	}
	if err != nil {
		return false, err
	}
	if runtime.GOOS == "windows" {
		return true, nil
	}
	directory, err := os.Open(filepath.Dir(path))
	if err != nil {
		return false, err
	}
	defer directory.Close()
	return true, directory.Sync()
}
