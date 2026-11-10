# Local source changes

DNSTT starts from 51bdba3948d44b3175bc88ea40b2116bb9daff07 (AGPL-3.0).
2026-11-08: dnstt-server/lib/server.go transfers ownership of close(ch) from
Run's defer to its single sender recvLoop goroutine. A repeated race-detector
fixture caught a send-after-close during server shutdown. The client code is
unchanged. This server package is used by local tests; the production transport
host does not import the server library. The same source is in vendor.

NoizDNS is based on the pinned source at 17ac5cf4e45cd09fa6f4bde09362e334dc4b8b55.
soar-mobile is copied from Rovunet's source tree; its own legal attribution is
retained in the top-level Windows NOTICE and accompanying AGPL licence.

Windows preview 6: noizdns/mobile reports completion of the authenticated Noise/smux
session through a bounded, cancellable wait, instead of inferring remote readiness from
a local listener. CC0 VayDNS adds plain DNS-over-TCP using its existing length-framed
stream transport, and its DoT dialer observes the supplied context. The same modifications
are present under deps and vendor. VayDNS and DNSTT/NoizDNS direct and SSH fixtures verify
HTTPS over a TCP resolver; UDP fallback packets to that resolver are counted.

Local resolver/pool review: vaydns.go is a new host-level bounded sequential
resolver selection wrapper; it does not modify the CC0 client or vendored copy.
Each failed authenticated startup is closed before trying the next candidate.
The first successful Noise session is retained. This is startup fallback, not
mid-session seamless failover or fanout. Keepalive/TCP/TLS options stay intact.
fixture-vaydns.mod/sum builds only the pinned CC0 test server; ttlcache/v3 v3.4.0
is test-only (MIT notice in licenses/Go/Fixture-ttlcache-MIT.txt). The production
module/vendor graph was not rewritten. Additional local Go tests are AGPL-3.0.
