<p align="center"><img src="assets/rovunet-logo.png" alt="Rovunet" width="200"></p>

# Rovunet

[English](README.md) · [فارسی](README.fa.md) · [Download](https://github.com/msVPNco/rovunet-android/releases/latest) · [Telegram](https://t.me/RovunetVPN) · [Server panel](https://github.com/msVPNco/rovunet-panel)

An Android multi-protocol VPN client that keeps a server's connection paths together in named collections. Import supported standard links or Rovunet bundles, connect a selected path directly, and run a separate HTTPS test to compare paths through the actual tunnel.

The interface supports Persian, English, Simplified Chinese and Russian. Settings and their dialogs use translated Android resources; some inherited advanced editor and scanner text still appears in English.

## Download and install

**0.3.0-alpha11 Full · versionCode 17 · Android 7.0 or later.**

[Download the Full APK](https://github.com/msVPNco/rovunet-android/releases/download/v0.3.0-alpha11/Rovunet-0.3.0-alpha11-Full.apk) · [Release notes and all files](https://github.com/msVPNco/rovunet-android/releases/tag/v0.3.0-alpha11)

Install over the previous Full version to retain profiles. The package ID remains app.veyra.client and the existing signing identity is retained. Export a backup before upgrading. There is no Google Play, App Store or Rovunet desktop GUI release yet.

The APK is a non-debuggable Full Release build, still signed with the existing debug certificate for upgrade compatibility. Production signing and physical-device validation remain pending. Automated tests do not establish reliability on every phone or network.

APK SHA-256:

```text
0640b684cf1f07e6645aa536698a4b3820f2973b19e17fe5083017d80b825e98
```

## What changed

- Connect starts the selected path without a prerequisite ping, HTTPS test or resolver-pool scan. Without a selected path, it chooses a suitable direct path from the selected collection; DNS fallback still requires consent.
- Waiting for destruction of the previous service is bounded. Expired or cancelled requests cannot start later, and stale callbacks cannot revive an abandoned attempt.
- Foreground notification starts before blocking setup work. Actual protocol/VPN readiness completes Connected; idle zero traffic alone does not disconnect it. Protocol health and stalled-traffic monitoring continue after setup.
- Includes alpha10's in-place language/RTL switching, translated settings and private, locally stored stability reports. Reports are shared only through an explicit user action.

[Alpha11 technical record](docs/releases/alpha11.md) · [Current status and remaining work](docs/STATUS.md)

## Protocols and configuration

Includes the DNS tunnel families (DNSTT, NoizDNS, VayDNS, Slipstream and Soar), SSH and supported wrappers, NaiveProxy, SOCKS5, DoH, VLESS WebSocket, Tor, and core profiles for REALITY, XHTTP, Hysteria2 and single-peer WireGuard. Availability depends on the backend, device architecture and server configuration; no protocol is guaranteed to bypass a particular network.

Imports include supported standard VLESS/Hysteria2 links, single-peer WireGuard INI and native rovunet:// bundles. Current native export is v33; earlier formats and veyra:// / slipnet:// aliases remain import-compatible. WireGuard URI dialects, AmneziaWG and arbitrary core JSON are not advertised as supported imports.

Connected describes tunnel readiness, while HTTPS results describe the last explicit measurement. A failed historical test does not block manual connection. HTTPS latency is not download speed or a long-term stability guarantee.

The [server panel](https://github.com/msVPNco/rovunet-panel) is separate. Client support does not mean the panel automatically installs every supported protocol. Subscription refresh and automatic failover between collection paths are not included.

## Source, build and verification

The APK and both source assets use application commit [91f2c0e](https://github.com/msVPNco/rovunet-android/commit/91f2c0e9a9fd94eb72e30236bc965293b5303762). Later release-documentation changes do not rebuild that APK.

- **source-light.zip**: application code, resources, tests, licenses and available pinned native source trees; precompiled .so and .aar files omitted.
- **source.zip**: the same application snapshot and available native sources, including tracked precompiled assets. It is not a fully self-contained native rebuild kit.
- **SOURCE-MANIFEST.json** inside each ZIP records exact revisions and one unavailable upstream dnstt-mobile source tree. No substitute revision is presented as the original source.
- Release assets also include SHA256SUMS.txt, APK-SHA256.txt, SIGNER-SHA256.txt, a Persian guide and VERIFICATION.json.

[Build and tests](docs/BUILDING.md) · [Source provenance](docs/releases/alpha08-native-sources.md) · [Successful exact-commit CI](https://github.com/msVPNco/rovunet-android/actions/runs/41984862024) · [Release procedure](docs/RELEASING.md)

## Documentation and support

- [User guide — English and Persian](docs/USER_GUIDE.md)
- [Current status and roadmap](docs/STATUS.md)
- [Core formats and limitations](VEYRA-CORE.md)
- [Security reporting](SECURITY.md)
- [Telegram community](https://t.me/RovunetVPN)

For a problem report, include app version, Android version, protocol, steps and whether ordinary sites open. Review diagnostics before sharing; do not post config links, credentials or device identifiers publicly.

## Optional development support

Support is voluntary and is not required to use the app. The address is also available with a copy button in Settings and About.

- Asset: **USDT**
- Network: **Tron (TRC20)**
- Address: `TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

Send only USDT on Tron (TRC20) to this address.

## License and attribution

AGPL-3.0; see [LICENSE](LICENSE) and [NOTICE](NOTICE.md). Xray-core is distributed under MPL-2.0; source pins and native dependency metadata are retained under tools/veyra.

Rovunet is based on [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full by Anonvector. It is an independent project and is not affiliated with the SlipNet project.
