# Slipstream first-contact identity discovery

This helper obtains only the public server leaf through the profile's own
DNS resolvers and completes the QUIC TLS handshake. It opens no application
streams. The GUI reviews and explicitly approves the SHA-256 fingerprint.
The ordinary Slipstream client then uses that exact certificate with `--cert`.

Protocol reference is Mygod/slipstream-rust c7b265c11dd13d6dfd601bc81d54d61ee9bdde23,
the same v0.1.1 native client bundled with Rovunet. DNS framing is base32, 57-byte
labels, TXT responses and EDNS. The ALPN is `picoquic_sample`; MTU is computed
as floor((240 - domain length) / 1.6), as in that client.

The vendored QUIC implementation is github.com/metacubex/quic-go
v0.61.1-0.20260727080200-2548683b76f4, with its pinned TLS dependencies.
Four transport-size adjustments are confined to this discovery executable:
`MinInitialPacketSize` is 64, the default initial size is 120, the coalescing
threshold is 24, and transport-parameter validation uses the private minimum.
Slipstream's DNS transport uses small QUIC packets and advertises a 900-byte
receive limit, unlike ordinary QUIC over UDP. TLS signature verification is
preserved. These changes do not apply to any other Rovunet engine.

An untrusted first-contact certificate is returned only after successful TLS
handshake and validity checks. It is never persisted by this executable.
The GUI owns approval and encrypted storage. Known pins are used directly by
the ordinary native client and remain authoritative until explicitly reviewed.

Linux fixture archive is the official v0.1.1 release at
https://github.com/Mygod/slipstream-rust/releases/download/v0.1.1/slipstream-linux-x86_64.tar.gz
SHA-256 5572d407d685a4a87e4de5a7121566d48ac719068e50ad69705793dc6d9ff682.
The fixture runs only on loopback and targets a closed local port; certificate
discovery neither requires nor establishes application data transfer.
