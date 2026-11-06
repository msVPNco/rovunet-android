<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="180"></p>

[English](README.md) · [فارسی](README.fa.md) · [简体中文](README.zh-CN.md) · [Русский](README.ru.md)

# Rovunet

**A multi-protocol Android VPN client for several routes to your own server.** Rovunet brings DNS tunnels, SSH transports, HTTPS proxies and Xray-backed profiles into one interface. Related paths stay together in named collections: connect a selected path directly, or separately measure HTTPS through the actual tunnel.

[Releases](https://github.com/msVPNco/rovunet-android/releases) · [Telegram](https://t.me/RovunetVPN) · [Server panel](https://github.com/msVPNco/rovunet-panel) · [User guide](docs/USER_GUIDE.md)

## Release status

The current release is **0.3.0-alpha15 Full, version code 26**. It retains the [production signing identity](docs/SIGNING-IDENTITY.md) introduced in alpha14, so install it over public alpha14 without uninstalling. Historical alpha11 and private Rovunet Test use different identities. [Download Full APK](https://github.com/msVPNco/rovunet-android/releases/latest/download/Rovunet-v0.3.0-alpha15-Full.apk) · [Release record](docs/releases/alpha15.md) · [Current status](docs/STATUS.md).

Alpha15 repairs the Release/R8 packaging of gomobile: required Go/JNI classes, constructors, fields and callbacks are preserved. CI passed 50 JVM tests, 31 private Debug device tests and two additional JNI checks on minified private Release. The final public APK also passes DEX ABI, signature and native-alignment checks. Device evidence is API 30/x86_64; it is not a guarantee for every real network.

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

Persian, English, Simplified Chinese and Russian cover the main interface, settings and profile-editor resources. Some inherited advanced scanner text and backend errors can remain English. Addresses, protocol names and configuration syntax are not translated.

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

Subscription refresh, automatic collection failover, download-throughput testing and desktop/store versions are not included. Soar automatic-mode differences and remote SOCKS data flow on the reported real network still require field evidence. No protocol guarantees access through every network.

## Source, verification and support

Explicit source ZIPs contain app code, resources, tests, licenses and available pinned native sources. Light ZIPs omit tracked precompiled `.so`/`.aar` assets. `SOURCE-MANIFEST.json` records revisions, reused binaries and an unavailable old `dnstt-mobile` tree. Neither ZIP is a complete independent native rebuild kit.

[Build](docs/BUILDING.md) · [Release procedure](docs/RELEASING.md) · [Security](SECURITY.md) · [Core formats](VEYRA-CORE.md).

For support, provide app/Android versions, protocol, reproduction steps and the local safe report. Do not publicly post configuration links, passwords, private keys or device identifiers. Community: [t.me/RovunetVPN](https://t.me/RovunetVPN).

## Optional development support

If you would like to help improve Rovunet, donations are voluntary and never required to use it. **USDT on Tron/TRC20 only:**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## License and attribution

AGPL-3.0: [LICENSE](LICENSE), [NOTICE](NOTICE.md). Xray-core is under MPL-2.0; source pins and notices are retained.

Based on [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full by Anonvector. Rovunet is an independent project, not affiliated with SlipNet.
