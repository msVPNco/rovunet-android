<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="140"></p>

<h1 align="center">Rovunet</h1>

<p align="center"><b>Several routes to your own server, on every device</b><br>DNS tunnels, SSH, HTTPS proxies and Xray profiles in named collections, with direct connection and real HTTPS tests.</p>

<p align="center">
  <a href="https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha20"><img alt="Android release" src="https://img.shields.io/github/v/release/msVPNco/rovunet-app?sort=date&display_name=tag&filter=v*&label=android&color=0E7C86"></a>
  <img alt="Platforms" src="https://img.shields.io/badge/platforms-Android%20%C2%B7%20Windows%20%C2%B7%20Linux%20%C2%B7%20macOS-14304D">
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0-6D4AFF"></a>
  <a href="https://t.me/RovunetVPN"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-RovunetVPN-2F6FEB?logo=telegram&logoColor=white"></a>
</p>

<p align="center"><b>English</b> · <a href="README.fa.md">فارسی</a> · <a href="README.ru.md">Русский</a> · <a href="README.zh-CN.md">简体中文</a></p>

<p align="center">📥 <a href="#apps">Download</a> · 📘 <a href="docs/guides/Rovunet-App-Guide-EN.pdf">Complete guide (PDF)</a> · <a href="docs/USER_GUIDE.md">User guide</a> · <a href="https://github.com/msVPNco/rovunet-panel">Server panel</a></p>

---

**Rovunet is a family of VPN clients for reaching your own server by several different routes.** It brings DNS tunnels, SSH transports, HTTPS proxies and Xray-backed profiles into one interface. Related paths stay together in named collections: connect a selected path directly, or separately measure HTTPS through the actual tunnel. Every client lives in this one repository; the matching server side is [Rovunet Panel](https://github.com/msVPNco/rovunet-panel).

**Android alpha release:** [alpha21 Full / code46](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha21) — account trust, independent caches and Room50→51. Public alpha release; existing public alpha20 remains available. [Evidence and upgrade limits](docs/releases/alpha21.md).

<a id="apps"></a>

## Apps

| Platform | Status | Download and details |
| --- | --- | --- |
| **Android** | Public release: **0.3.0-alpha20 Full, code 43** | [Download](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha20) · [Android page](docs/android/README.md) |
| **Windows** | **0.1.1 Beta · x64 · Pre-release** | [Download](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.1-beta) · [Windows page](windows/README.md) |
| **Linux** | Planned | No release yet |
| **macOS** | **0.1.6 Beta · Apple Silicon / Intel · Pre-release** | [Download](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.6-beta) · [Mac page](docs/macos/README.md) · Intel: limited testing |

Each platform has its own tags, files, checksums and signing. The repository-wide “Latest” badge on GitHub is not a per-platform feed, so always download from the release that names your platform. Rules for tags and updates: [Platforms and release channels](docs/PLATFORMS.md). Nothing in this table is a claim of desktop protocol support or installer testing until a release says so.

## What Rovunet does

Described for Android today; other platforms are listed above only once they have a release.

- **Collections**: group related paths by server and account; explicit choices and finished measurements are remembered.
- **Direct connection**: tap a path to select it, tap again to connect. No mandatory ping or resolver scan first.
- **Real HTTPS tests**: an actual HTTPS/204 request through the tunnel with certificate validation, success ratio and latency spread. A failed earlier measurement does not block a manual connection.
- **Many protocols**: SSH, DNS tunnels (DNSTT, NoizDNS, VayDNS, Slipstream, Soar), NaiveProxy, SOCKS5, DoH, VLESS WebSocket, REALITY, XHTTP, Hysteria2, WireGuard and Tor.
- **Subscriptions and sharing**: HTTPS subscriptions, `rovunet://` links, QR codes, files and password-encrypted backups.
- **Four languages**: Persian, English, Russian and Simplified Chinese.

Full lists, limits and caveats are on the [Android page](docs/android/README.md). A server and valid credentials are required; the apps do not provide free accounts, and no protocol guarantees access through every network.

## Get started on Android

1. **Download** the Full APK from the [release page](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha20) and install it. Public alpha14 and later install in place, without uninstalling.
2. **Open** Rovunet, choose language and theme, and grant the Android VPN permission when asked.
3. **Import**: Subscriptions → paste your HTTPS subscription link → **Import**, or **+ → Import** for a link, QR code or file.
4. **Connect**: expand the collection, tap a path to select it and tap it again to connect.

## Guides

| | |
|---|---|
| 📘 Complete guide (PDF) | [English](docs/guides/Rovunet-App-Guide-EN.pdf) · [فارسی](docs/guides/Rovunet-App-Guide-FA.pdf) · [Русский](docs/guides/Rovunet-App-Guide-RU.pdf) · [简体中文](docs/guides/Rovunet-App-Guide-ZH.pdf) |
| [User guide](docs/USER_GUIDE.md) | Installation, import, connection, tests, Soar, credentials and troubleshooting |
| [Android page](docs/android/README.md) | Release status, features, protocols and limits |
| [Platforms](docs/PLATFORMS.md) · [Current status](docs/STATUS.md) · [Upgrade](docs/UPGRADE.md) | Release channels, release state, signer and migration notes |

The PDF guides retain their alpha15.4 edition and layout; use the current user guide and [alpha20 notes](docs/releases/alpha20.md) for the new status, menu and idle behavior.

## Source and support

[Build](docs/BUILDING.md) · [Release procedure](docs/RELEASING.md) · [Security](SECURITY.md).

For support, provide the app and system versions, protocol, reproduction steps and the local safe report. Do not publicly post configuration links, passwords, private keys or device identifiers. Community: [t.me/RovunetVPN](https://t.me/RovunetVPN).

## Optional development support

If you would like to help improve Rovunet, donations are voluntary and never required to use it. **USDT on Tron/TRC20 only:**

`TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

## License and attribution

AGPL-3.0: [LICENSE](LICENSE), [NOTICE](NOTICE.md). Xray-core is under MPL-2.0; source pins and notices are retained.

Based on [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full by Anonvector. Rovunet is an independent project, not affiliated with SlipNet.

