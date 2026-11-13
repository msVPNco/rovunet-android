**Android alpha release:** [alpha21 Full / code46](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha21) — account trust, independent caches and Room50→51. Public alpha release; existing public alpha20 remains available. [Evidence and upgrade limits](../releases/alpha21.md).

<p align="center"><img src="../../assets/rovunet-logo.png" alt="Rovunet" width="140"></p>

Alpha20 Full / code43 adds a themed Rovunet startup cover and removes redundant startup digest work; direct encrypted Mahsa fetching, public-source URL privacy, secure active-tunnel update downloads, bounded cancellable file import, red failure labels and small English legal attribution. It also fixes SOCKS domain-length handling in the constrained auxiliary engine. Alpha19 capabilities are retained; this version adds no new protocol.

Existing capabilities retained in code43 include subscription updates on the collection card, optional names, provider-reported usage/expiry, supported `cs`/`fm`, Shadowsocks 2022, Hy2 port hopping and single-peer AmneziaWG import. It retains the calm Soft Pop themes, visible-only latency monitoring, bounded smart recovery, isolated Slipstream resolver workers and native stop improvements. Patterniha is removed from the built-in catalog; existing imports are preserved.

Automated local/emulator checks do not establish physical-phone battery savings, overnight/Doze stability, carrier reachability or repair of the reported panel WireGuard/Hy2 endpoints. Owner accepted this exact APK for publication; no detailed phone/carrier/battery measurements were supplied. AnyTLS, TUIC v5 and single-binding Mieru are included; see alpha20 details. No new Lite or desktop binary is published.

<h1 align="center">Rovunet for Android</h1>

<p align="center"><b>Several routes to your own server, in one Android app</b><br>DNS tunnels, SSH, HTTPS proxies and Xray profiles in named collections, with direct connection and real HTTPS tests.</p>

<p align="center">
  <a href="https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha20"><img alt="Release" src="https://img.shields.io/github/v/release/msVPNco/rovunet-app?sort=date&display_name=tag&filter=v*&label=release&color=0E7C86"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Android%207%2B-14304D">
  <a href="../../LICENSE"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0-6D4AFF"></a>
  <a href="https://t.me/RovunetVPN"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-RovunetVPN-2F6FEB?logo=telegram&logoColor=white"></a>
</p>

<p align="center"><b>English</b> · <a href="README.fa.md">فارسی</a> · <a href="README.ru.md">Русский</a> · <a href="README.zh-CN.md">简体中文</a></p>

<p align="center"><a href="../../README.md">← All Rovunet apps</a></p>

<p align="center">📥 <a href="https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha20">Download</a> · 📘 <a href="../guides/Rovunet-App-Guide-EN.pdf">Complete guide (PDF)</a> · <a href="../USER_GUIDE.md">User guide</a> · <a href="https://github.com/msVPNco/rovunet-panel">Server panel</a></p>

---

**A multi-protocol Android VPN client for several routes to your own server.** Rovunet brings DNS tunnels, SSH transports, HTTPS proxies and Xray-backed profiles into one interface. Related paths stay together in named collections: connect a selected path directly, or separately measure HTTPS through the actual tunnel.

This is the shared Rovunet app repository for Android, Windows and future macOS work. Android alpha20 is published; Windows has its own 0.1.0 Beta; see its platform page for its supported capabilities. [Platform and release channels](../PLATFORMS.md).

## Release status

The current release is **0.3.0-alpha20 Full, version code 43**. It retains the [production signing identity](../SIGNING-IDENTITY.md) introduced in alpha14, so install it over public alpha14 without uninstalling. Historical alpha11 and private Rovunet Test use different identities. [Download Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) · [Release record](../releases/alpha20.md) · [Current status](../STATUS.md).

**Subscriptions:** choose Update on the collection card to update in place; progress blocks repeated clicks and the outcome stays on Home. Existing-profile replacement still requires a disconnected VPN. Names are optional: explicit/saved name, safe provider title, then hostname. `subscription-userinfo` reports upload + download, volume and expiry from the same response; missing fields stay unknown. Provider snapshots show their age and are not independent metering. No additional periodic full-feed downloads are introduced. Panel status retains its separate contract.

**Imports:** supported VLESS/VMess/Trojan `cs` and `fm` are preserved with verified TLS; unsupported masks/scripts/settings are rejected explicitly. Shadowsocks 2022 validates key lengths. Hy2 supports bounded `mport` lists and `hop-interval`. Standard WireGuard sharing URIs and single-peer AmneziaWG `.conf`/supported URI parameters are accepted. AWG runs in a separate authenticated userspace engine; one address per IP family, no scripts, multi-peer or ranged keepalive. Arbitrary core/Mihomo YAML is not executed.

**Direct upgrade for public alpha14–alpha16 (through code32):** download the [Full APK](https://github.com/msVPNco/rovunet-app/releases/download/v0.3.0-alpha20/Rovunet-v0.3.0-alpha20-Full.apk) and install over the existing app **without uninstalling or clearing data**. The old updater embeds the former repository URL; web redirects cannot repair that check. Package and production signer are unchanged.

## Quick start

1. **Download** the Full APK from [Releases](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha20) and install it. Public alpha14 and later install in place, without uninstalling.
2. **Open** Rovunet, choose language and theme, and grant the Android VPN permission when asked.
3. **Import**: Subscriptions → paste your HTTPS subscription link → **Import**, or **+ → Import** for a link, QR code or file.
4. **Connect**: expand the collection, tap a path to select it and tap it again to connect.

## Subscriptions, sharing and Soar scanning

HTTPS subscriptions have **Update** and **Import** actions. Import adds a new source once and closes the dialog after success; errors preserve the form, reimporting a saved URL refreshes the existing source, and updates preserve local changes. Existing-source updates require VPN disconnection; removals require review. Optional refresh runs approximately every 12 hours under suitable network/battery conditions. Collection QR/PNG sharing is supported when the payload fits; larger collections use files.

Panel v1.19 account status shows remaining GiB and time in the existing collection card. Amber means ≤20% remaining or ≤3 days; red means ≤5% or ≤24 hours. Unlimited, unknown and expired are distinct. A 404 or stale/incomplete counting is unavailable information, not proof of expiry. Status refresh runs while Home is visible and never blocks manual connection. Server-side metering and limits remain authoritative.

Soar scanning offers Quick, Advanced, E2E and Prism. Repeated protocol handshakes and BOTH waiting for UDP/TCP are restored to alpha15.4 behaviour. Preserve explicit TCP-only and existing trust approval. Local fixtures do not prove resolver availability on a particular network.

Requires Android 7.0/API 24 or later. Build targets: ARM32, ARM64, x86_64; inherited Naive/Tor binaries are unavailable on x86_64. No 32-bit x86 or new Lite asset is promised. No Rovunet Google Play, App Store or desktop GUI release is available.

## Features

| Area | Current functionality |
| --- | --- |
| Collections | Group paths using collection/account/server metadata; remember explicit choices and retain completed measurements. |
| Direct connection | First tap selects a path, tapping the selected path connects. Collection expansion, editing and menus do not connect. No prerequisite ping or resolver-pool scan. |
| HTTPS tests | Actual HTTPS/204 inside the tunnel, certificate validation, a limited alternative target, success ratio and latency spread. DNS fallback in collection tests requires consent. |
| Interface | Light, dark and AMOLED themes, distinct collection/path surfaces, green current-network HTTPS badges, four-language resources, quick controls and traffic counters. |
| Configuration | Supported link/text/file/QR import with preview, collection export, sharing, password-encrypted backups and explicit recovery of unreadable credentials. Sharing/device restrictions remain enforced. |
| Security | SSH identity approval, validated TLS and saved self-signed identities, encrypted stored credentials, authenticated private Xray SOCKS endpoints and cancellable setup. |
| Diagnostics | Local safe stage/code reports and traffic snapshots, optionally shared by the user; no automatic upload and no endpoint/account/credential fields in the safe report. |

Persian, English, Simplified Chinese and Russian cover the main interface, settings and profile-editor resources. Scanner labels, mapped errors and the connection guide are localized; protocol names and configuration text remain technical. Addresses, protocol names and configuration syntax are not translated.

## Protocols

| Family | Available paths |
| --- | --- |
| SSH | Direct SSH, compatible TLS/STunTLS configurations, HTTP CONNECT and WebSocket; password or supported key authentication. |
| DNS tunnels | DNSTT, NoizDNS, VayDNS, Slipstream and Soar, including supported SSH combinations and backend-specific resolver discovery/tuning. |
| HTTPS/proxies | NaiveProxy, NaiveProxy + SSH, remote SOCKS5 and DoH. Plain SOCKS5 does not encrypt transport; DoH alone encrypts DNS, not all traffic. |
| VLESS/core | VLESS WebSocket, REALITY, XHTTP, Hysteria2 and standard single-peer WireGuard through the integrated core. |
| Tor | Full-package Tor and supported Snowflake/obfs4/Meek/custom bridges, subject to backend and architecture availability. |

**New Soar and Soar+SSH profiles default to UDP + TCP.** Existing/imported choices are preserved; Automatic, UDP + TCP, TCP only and UDP only remain independent options. SSH over a base tunnel also requires the server to allow the destination and SSH port.


## Connect, measure and update

Import and review a profile, expand its collection, select a path and tap it again or use the collection control. A remembered path is already selected when you return. Grant Android VPN permission. Before approving an unknown SSH/self-signed identity, compare its fingerprint with your operator; background work never silently approves a new identity.

Tests are separate from connection. A failed historical measurement does not block manual connection. Connected means technical tunnel readiness, not guaranteed access to every service. HTTPS latency is not download speed or a long-term stability guarantee.

The in-app updater downloads compatible GitHub release APKs and checks checksum, package, version and signer before opening Android's installer. No silent installation or alternative update mirror is provided. Changing signer does not automatically create a compatible upgrade.

**Do not uninstall the only readable copy of your profiles.** Alpha11 public and Rovunet Test have different identities. Encrypted bulk export excludes locked profiles; protected or device-bound profiles may require original links or reissue after a signing change. Follow the actual release's installation notes. [Migration](../adr/0002-signing-transition.md).

## Server and limits

[Rovunet Panel](https://github.com/msVPNco/rovunet-panel) manages supported Linux services and exports configs. Client support does not imply automatic panel installation; some `core-plan` paths require manual deployment. A server and valid credentials are needed; the app does not provide free accounts.

Automatic recovery is opt-in and bounded; manual mode never switches automatically. Download-throughput benchmarks and new desktop/store binaries are not included. Soar automatic-mode differences and remote SOCKS data flow on the reported real network still require field evidence. No protocol guarantees access through every network.

## Guides

| | |
|---|---|
| 📘 Complete guide (PDF) | [English](../guides/Rovunet-App-Guide-EN.pdf) · [فارسی](../guides/Rovunet-App-Guide-FA.pdf) · [Русский](../guides/Rovunet-App-Guide-RU.pdf) · [简体中文](../guides/Rovunet-App-Guide-ZH.pdf) |
| [User guide](../USER_GUIDE.md) | Installation, import, connection, tests, Soar, credentials and troubleshooting |
| [Current status](../STATUS.md) · [Upgrade](../UPGRADE.md) | Release state, signer and migration notes |

The PDF guides retain their alpha15.4 edition and layout; use the current user guide and [alpha20 notes](../releases/alpha20.md) for the new status, menu and idle behavior.

## Source, verification and support

Explicit source ZIPs contain app code, resources, tests, licenses and available pinned native sources. Light ZIPs omit tracked precompiled `.so`/`.aar` assets. `SOURCE-MANIFEST.json` records revisions, reused binaries and an unavailable old `dnstt-mobile` tree. Neither ZIP is a complete independent native rebuild kit.

[Build](../BUILDING.md) · [Release procedure](../RELEASING.md) · [Security](../../SECURITY.md) · [Core formats](../history/VEYRA-CORE.md).

For support, provide app/Android versions, protocol, reproduction steps and the local safe report. Do not publicly post configuration links, passwords, private keys or device identifiers. Community: [t.me/RovunetVPN](https://t.me/RovunetVPN).

## Optional development support

If you would like to help improve Rovunet, donations are voluntary and never required to use it. **USDT on Tron/TRC20 only:**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## License and attribution

AGPL-3.0: [LICENSE](../../LICENSE), [NOTICE](../../NOTICE.md). Xray-core is under MPL-2.0; source pins and notices are retained.

Based on [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full by Anonvector. Rovunet is an independent project, not affiliated with SlipNet.
