package ca

import (
	"crypto/sha256"
	"crypto/x509"
	"encoding/hex"
	"encoding/pem"
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

func rovunetAutomaticCertificate(config *tls.Config, option Option) {
	if RovunetCertificateDirectory == "" || RovunetCertificateEndpoint == "" || config.InsecureSkipVerify || option.Fingerprint != "" || option.NameCertVerify != "" || option.ZeroTrust {
		return
	}
	directory, endpoint := RovunetCertificateDirectory, RovunetCertificateEndpoint
	host := config.ServerName
	digest := sha256.Sum256([]byte(endpoint + "|" + strings.ToLower(host)))
	path := filepath.Join(directory, hex.EncodeToString(digest[:])+".pem")
	// Go verifies the peer's CertificateVerify signature before VerifyConnection.
	// All chain/name/time and stored-key checks are performed by this callback.
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
		if known, err := rovunetReadPin(path); err == nil {
			return rovunetSameKey(known, cert)
		} else if !os.IsNotExist(err) {
			return err
		}
		intermediates := x509.NewCertPool()
		for _, parent := range state.PeerCertificates[1:] {
			intermediates.AddCert(parent)
		}
		if _, err := cert.Verify(x509.VerifyOptions{DNSName: host, Roots: config.RootCAs, Intermediates: intermediates, CurrentTime: now}); err == nil {
			return nil // Preserve ordinary CA renewal; only private certificates need TOFU pins.
		}
		return rovunetPersistPin(path, cert)
	}
}

func rovunetSameKey(known, peer *x509.Certificate) error {
	if sha256.Sum256(known.RawSubjectPublicKeyInfo) != sha256.Sum256(peer.RawSubjectPublicKeyInfo) {
		return errors.New("server certificate identity changed")
	}
	return nil
}

func rovunetReadPin(path string) (*x509.Certificate, error) {
	info, err := os.Lstat(path)
	if err != nil {
		return nil, err
	}
	if !info.Mode().IsRegular() || info.Size() > 32768 {
		return nil, errors.New("invalid certificate store")
	}
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}
	block, rest := pem.Decode(data)
	if block == nil || block.Type != "CERTIFICATE" || len(strings.TrimSpace(string(rest))) != 0 {
		return nil, errors.New("invalid certificate store")
	}
	return x509.ParseCertificate(block.Bytes)
}

func rovunetPersistPin(path string, cert *x509.Certificate) error {
	if err := os.MkdirAll(filepath.Dir(path), 0700); err != nil {
		return err
	}
	file, err := os.CreateTemp(filepath.Dir(path), ".certificate-")
	if err != nil {
		return err
	}
	temporary := file.Name()
	defer os.Remove(temporary)
	data := pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: cert.Raw})
	if _, err = file.Write(data); err == nil {
		err = file.Sync()
	}
	closeErr := file.Close()
	if err != nil {
		return err
	}
	if closeErr != nil {
		return closeErr
	}
	if err = os.Link(temporary, path); err != nil {
		if !os.IsExist(err) {
			return err
		}
		known, err := rovunetReadPin(path)
		if err != nil {
			return err
		}
		return rovunetSameKey(known, cert)
	}
	if runtime.GOOS == "windows" {
		return nil
	} // Windows does not support fsync on a directory handle.
	directory, err := os.Open(filepath.Dir(path))
	if err != nil {
		return err
	}
	defer directory.Close()
	return directory.Sync()
}
