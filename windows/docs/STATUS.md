# Rovunet Windows 0.1.0 Beta

Public Windows x64 beta based on reviewed preview20/final4. WPF/.NET10, four languages, compact/wide layouts, touch-size cards, separate unprivileged agent, local HTTP/SOCKS5 and optional Windows system proxy. No device-wide TUN or forced per-process routing.

Subscriptions support provider account usage/expiry separately from device counters. Profile editors, public-source import, in-place updates, tray connect/disconnect, select-then-connect cards, staged DNS compatibility/server tests, cancellation, checkpoints and multi-selection are implemented. Six banks are bundled; full60370 addresses and sample128 are distinct. Resolver transports are UDP/TCP/DoT/DoH where the selected family supports them.

The owner reports successful connection and data transfer on all tested paths except Hysteria2/WireGuard. This is owner-reported use, not a guarantee for every server/network. Those two engines remain known limitations. Public-feed downloads were intermittently unavailable; some custom protocol options remain unsupported and are rejected explicitly. TLS/SSH and protected restrictions stay enabled.

The beta changes version, packaging and documentation; connection code and engines retain the reviewed baseline. Exact source/asset hashes and beta verification accompany the release. Unsigned installer; target Windows10 22H2/build19045 or Windows11 x64. Program Files/UAC and physical touch/DPI/hardware suspend need broader testing. See TESTING.md and BACKLOG.md.