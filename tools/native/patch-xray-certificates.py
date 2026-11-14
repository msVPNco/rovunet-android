#!/usr/bin/env python3
"""Apply the Android automatic-certificate overlay to the existing pinned core."""
from pathlib import Path
import sys

def patch(source):
    source = Path(source)
    shared = Path(__file__).with_name('handshake') / 'certificates.go'
    text = shared.read_text(encoding='utf-8').replace('package ca\n', 'package tls\n', 1)
    text = text.replace('"github.com/metacubex/tls"', '"crypto/tls"')
    text = text.replace('var RovunetCertificateDirectory string', 'var RovunetCertificateDirectory = os.Getenv("ROVUNET_CERTIFICATE_STORE")')
    text = text.replace('var RovunetCertificateEndpoint string', 'var RovunetCertificateEndpoint = os.Getenv("ROVUNET_CERTIFICATE_ENDPOINT")')
    text = text.replace('func rovunetAutomaticCertificate(config *tls.Config, option Option)', 'func rovunetAutomaticCertificate(config *tls.Config)')
    text = text.replace(' || option.Fingerprint != "" || option.NameCertVerify != "" || option.ZeroTrust', ' || config.VerifyConnection != nil')
    assert 'option.' not in text
    target = source / 'transport/internet/tls'
    (target / 'rovunet.go').write_text(text, encoding='utf-8')
    for name in ('publish_unix.go', 'publish_other.go'):
        (target / ('rovunet_' + name)).write_text((shared.parent/name).read_text(encoding='utf-8').replace('package ca\n', 'package tls\n', 1), encoding='utf-8')
    config = target / 'config.go'
    text = config.read_text(encoding='utf-8')
    needle = '\n\treturn config\n}\n\n// Option for building TLS config.'
    replacement = '\n\trovunetAutomaticCertificate(config)\n\treturn config\n}\n\n// Option for building TLS config.'
    if replacement not in text:
        assert text.count(needle) == 1
        config.write_text(text.replace(needle, replacement), encoding='utf-8')
    # uTLS uses a different ConnectionState type. Preserve post-proof verification
    # when copying the standard TLS config to the fingerprinted client.
    config = target / 'tls.go'
    text = config.read_text(encoding='utf-8')
    needle = '\n\treturn config\n}'
    marker = '\n\tif c.VerifyConnection != nil {\n\t\tconfig.VerifyConnection = func(state utls.ConnectionState) error {\n\t\t\treturn c.VerifyConnection(tls.ConnectionState{ServerName: state.ServerName, PeerCertificates: state.PeerCertificates, VerifiedChains: state.VerifiedChains, HandshakeComplete: state.HandshakeComplete, DidResume: state.DidResume})\n\t\t}\n\t}\n\treturn config\n}'
    if marker not in text:
        # The pinned file has one config conversion function returning this value.
        assert text.count(needle) == 1
        config.write_text(text.replace(needle, marker), encoding='utf-8')
    (target/'rovunet_test.go').write_bytes((shared.parent/'xray_test.go').read_bytes())

if __name__ == '__main__':
    patch(sys.argv[1])
