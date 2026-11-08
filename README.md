<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="140"></p>

<h1 align="center">Rovunet for Android</h1>

<p align="center"><b>Several routes to your own server, in one Android app</b><br>DNS tunnels, SSH, HTTPS proxies and Xray profiles in named collections, with direct connection and real HTTPS tests.</p>

<p align="center">
  <a href="https://github.com/msVPNco/rovunet-android/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/msVPNco/rovunet-android?sort=date&display_name=tag&label=release&color=0E7C86"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Android%207%2B-14304D">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0-6D4AFF"></a>
  <a href="https://t.me/RovunetVPN"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-RovunetVPN-2F6FEB?logo=telegram&logoColor=white"></a>
</p>

<p align="center"><b>English</b> · <a href="README.fa.md">فارسی</a> · <a href="README.ru.md">Русский</a> · <a href="README.zh-CN.md">简体中文</a></p>

<p align="center">📥 <a href="https://github.com/msVPNco/rovunet-android/releases/latest">Download</a> · 📘 <a href="docs/guides/Rovunet-App-Guide-EN.pdf">Complete guide (PDF)</a> · <a href="docs/USER_GUIDE.md">User guide</a> · <a href="https://github.com/msVPNco/rovunet-panel">Server panel</a></p>

---

**A multi-protocol Android VPN client for several routes to your own server.** Rovunet brings DNS tunnels, SSH transports, HTTPS proxies and Xray-backed profiles into one interface. Related paths stay together in named collections: connect a selected path directly, or separately measure HTTPS through the actual tunnel.

## Release status

The current release is **0.3.0-alpha15.4 Full, version code 31**. It retains the [production signing identity](docs/SIGNING-IDENTITY.md) introduced in alpha14, so install it over public alpha14 without uninstalling. Historical alpha11 and private Rovunet Test use different identities. [Download Full APK](https://github.com/msVPNco/rovunet-android/releases/latest/download/Rovunet-v0.3.0-alpha15.4-Full.apk) · [Release record](docs/releases/alpha15.4.md) · [Current status](docs/STATUS.md).

Alpha15.4 fixes Soar scanning, subscription actions, menus, duplicate imports, test budgets and status feedback. The connection-selection guide and scanner use four-language resources. [CI](https://github.com/msVPNco/rovunet-android/actions/runs/42395755189) passed **60 JVM, 51 Debug emulator and 2 minified Release JNI tests**, with no device skips. All 33 native libraries match the 15.3 reviewed APK; this is not a complete native rebuild. The owner approved publication of the exact reviewed APK; no detailed new physical-network measurements were supplied.

**Upgrading public alpha14/alpha15:** their older updater does not recognize dotted alpha revisions. Download the [Full APK](https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha15.4/Rovunet-v0.3.0-alpha15.4-Full.apk) directly and install over the existing app without uninstalling or clearing data. Alpha15.4 understands subsequent dotted revisions. Package and production signer are unchanged.

## Quick start

1. **Download** the Full APK from [Releases](https://github.com/msVPNco/rovunet-android/releases/latest) and install it. Public alpha14 and later install in place, without uninstalling.
2. **Open** Rovunet, choose language and theme, and grant the Android VPN permission when asked.
3. **Import**: Subscriptions → paste your HTTPS subscription link → **Import**, or **+ → Import** for a link, QR code or file.
4. **Connect**: expand the collection, tap a path to select it and tap it again to connect.

## Subscriptions, sharing and Soar scanning

HTTPS subscriptions have **Update** and **Import** actions. Import adds a new source once and closes the dialog after success; errors preserve the form, duplicate source URLs are rejected, and updates preserve local changes. Existing-source updates require VPN disconnection; removals require review. Optional refresh runs approximately every 12 hours under suitable network/battery conditions. Collection QR/PNG sharing is supported when the payload fits; larger collections use files.

Soar scanning offers Quick, Advanced, E2E and Prism. Quick/Prism use authenticated Soar handshakes with the configured Ed25519 key; E2E also checks HTTP 204 or the SSH account with existing trust approval. Advanced is diagnostic DNS only. UDP and TCP are tested independently; Soar scanner defaults to both without rewriting saved connection choices. Imported candidates survive discarded foreign/legacy proof.

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

Imports include `rovunet://` bundles, compatible `veyra://`/`slipnet://` aliases, supported VLESS/Hysteria2 links and single-peer WireGuard INI. Native export is v33. Unknown future versions are explicitly rejected instead of partially imported. Arbitrary core JSON, AmneziaWG and every WireGuard URI dialect are not advertised as supported.

## Connect, measure and update

Import and review a profile, expand its collection, select a path and tap it again or use the collection control. A remembered path is already selected when you return. Grant Android VPN permission. Before approving an unknown SSH/self-signed identity, compare its fingerprint with your operator; background work never silently approves a new identity.

Tests are separate from connection. A failed historical measurement does not block manual connection. Connected means technical tunnel readiness, not guaranteed access to every service. HTTPS latency is not download speed or a long-term stability guarantee.

The in-app updater downloads compatible GitHub release APKs and checks checksum, package, version and signer before opening Android's installer. No silent installation or alternative update mirror is provided. Changing signer does not automatically create a compatible upgrade.

**Do not uninstall the only readable copy of your profiles.** Alpha11 public and Rovunet Test have different identities. Encrypted bulk export excludes locked profiles; protected or device-bound profiles may require original links or reissue after a signing change. Follow the actual release's installation notes. [Migration](docs/adr/0002-signing-transition.md).

## Server and limits

[Rovunet Panel](https://github.com/msVPNco/rovunet-panel) manages supported Linux services and exports configs. Client support does not imply automatic panel installation; some `core-plan` paths require manual deployment. A server and valid credentials are needed; the app does not provide free accounts.

Automatic collection failover, download-throughput testing and desktop/store versions are not included. Soar automatic-mode differences and remote SOCKS data flow on the reported real network still require field evidence. No protocol guarantees access through every network.

## Guides

| | |
|---|---|
| 📘 Complete guide (PDF) | [English](docs/guides/Rovunet-App-Guide-EN.pdf) · [فارسی](docs/guides/Rovunet-App-Guide-FA.pdf) · [Русский](docs/guides/Rovunet-App-Guide-RU.pdf) · [简体中文](docs/guides/Rovunet-App-Guide-ZH.pdf) |
| [User guide](docs/USER_GUIDE.md) | Installation, import, connection, tests, Soar, credentials and troubleshooting |
| [Current status](docs/STATUS.md) · [Upgrade](docs/UPGRADE.md) | Release state, signer and migration notes |

## Source, verification and support

Explicit source ZIPs contain app code, resources, tests, licenses and available pinned native sources. Light ZIPs omit tracked precompiled `.so`/`.aar` assets. `SOURCE-MANIFEST.json` records revisions, reused binaries and an unavailable old `dnstt-mobile` tree. Neither ZIP is a complete independent native rebuild kit.

[Build](docs/BUILDING.md) · [Release procedure](docs/RELEASING.md) · [Security](SECURITY.md) · [Core formats](docs/history/VEYRA-CORE.md).

For support, provide app/Android versions, protocol, reproduction steps and the local safe report. Do not publicly post configuration links, passwords, private keys or device identifiers. Community: [t.me/RovunetVPN](https://t.me/RovunetVPN).

## Optional development support

If you would like to help improve Rovunet, donations are voluntary and never required to use it. **USDT on Tron/TRC20 only:**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## License and attribution

AGPL-3.0: [LICENSE](LICENSE), [NOTICE](NOTICE.md). Xray-core is under MPL-2.0; source pins and notices are retained.

Based on [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full by Anonvector. Rovunet is an independent project, not affiliated with SlipNet.
