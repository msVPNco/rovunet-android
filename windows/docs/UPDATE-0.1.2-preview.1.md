# Windows 0.1.2-preview.1 — local update

AmneziaWG is implemented as a separate pinned GPL-3.0 Mihomo-based userspace child; no kernel driver/TUN is installed. Android's AmneziaConfig and macOS MacAmnezia schema were compared read-only. Supports one peer, at most one interface address per IP family, supplied AllowedIPs/DNS/MTU/keepalive/preshared key and supported AWG J/S/H/I plus v3.1 options. Unknown fields, scripts, duplicates and malformed values are rejected. Packet specifications are validated by the pinned engine at startup. WireGuard sharing URIs with AWG fields, INI files and validated native Xray wrappers are accepted.

The child accepts runtime data only on stdin, uses a random authenticated loopback SOCKS hop, is hash-pinned and belongs to the agent's Windows process job. The existing Xray HTTP/SOCKS and traffic counters remain the public local endpoints. Local-ready never means HTTPS verified. Manual connect stays independent of probe results. Existing standard WireGuard/Hysteria2 code is unchanged.

Actual local Windows verification: 34 domain assertions, 249 existing domain regressions; encrypted TCP/UDP AWG 1, 2 and 3.1 fixtures; the Windows agent/Xray/AWG chain exchanges real payload, reports traffic, stops, releases ports, cancels startup and leaves the system proxy unchanged (10 checks per fixture version). The Windows helper termination test uses process termination/job ownership, not Unix graceful-signal claims. No external provider, physical sleep or real remote AWG server acceptance is claimed.

SSH/SOCKS5/NaiveProxy manual forms remove unrelated import tabs and Trojan/VMess/Shadowsocks shortcuts after protocol selection. The page title names the protocol. The main link/QR entry points remain available. SOCKS5 defaults to 1080, SSH to 22 and NaiveProxy to 443; explicitly edited ports are preserved when switching types.

This is a local preview, not authorization for a new GitHub release. The published 0.1.1 Beta and Android/macOS workspaces remain unchanged. See TESTING.md for final delivered-package checks and BACKLOG.md for remaining hardware/provider validation.
