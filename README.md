<p align="center">
  <img src="assets/rovunet-logo.png" alt="Rovunet" width="200">
</p>

# Rovunet

[English](README.md) · [فارسی](README.fa.md) · [Downloads](https://github.com/msVPNco/rovunet-android/releases/tag/v0.3.0-alpha06) · [Telegram](https://t.me/RovunetVPN)

An Android multi-protocol VPN client.

Rovunet groups imported connection paths into named collections, verifies HTTPS
through the real tunnel, and asks before trying DNS fallback. The light interface
supports Persian, English, Simplified Chinese and Russian in the main workflows;
some inherited advanced text still falls back to English.

## Current build

0.3.0-alpha06 Full, versionCode 12. Experimental and debug-signed; real-device
testing remains incomplete. [Alpha06 is published as a public prerelease](https://github.com/msVPNco/rovunet-android/releases/tag/v0.3.0-alpha06). No store listing is available.

Download the Full APK, Android source package and Persian guide from the release's Assets. The release is pinned to commit `bb8d78b82fba44188add499d5984996747379eec`; later documentation changes do not rebuild that APK.

APK SHA-256: `0ec8ecfced19bcce40f39933c89ad4a01d1c0e2ef88cfb3022d780da7482b590`.

Telegram group: [t.me/RovunetVPN](https://t.me/RovunetVPN)

Read [release notes and usage](ROVUNET-0.3.0-NOTES.txt) for exact behavior,
resolver-search limits and validation. The package ID remains app.veyra.client
to preserve in-place upgrades from earlier test builds.

The matching server panel is [rovunet-panel](https://github.com/msVPNco/rovunet-panel).

## Protocols and imports

Includes the inherited DNS/SSH/Tor/Naive paths plus Xray REALITY, XHTTP,
Hysteria2 and single-peer WireGuard. Imports standard supported links and
WireGuard INI. Native rovunet:// v32 retains collection metadata. veyra:// and
slipnet:// aliases remain import-compatible.

## Build and tests

See [core build notes](VEYRA-CORE.md) and tools/veyra/run-jvm-tests.sh.
Build Full Android with Gradle and the prepared native dependencies. Do not
publish the debug signing key as a production release key.

## Documentation

- [Current status and roadmap](docs/STATUS.md): verified scope and remaining work
- [Alpha06 release record](docs/releases/alpha06.md)

- [Guide](docs/GUIDE.md): tunnel types, building, CLI client and configuration
- [User guide](docs/USER_GUIDE.md) (English and Persian)
- [Security policy](SECURITY.md)

## Optional support

If you would like to help improve Rovunet, you can make a voluntary donation.
The app also provides this address with a copy button in Settings and About.

- Asset: **USDT**
- Network: **Tron (TRC20)**
- Address: `TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

Send only USDT on Tron (TRC20) to this address.

## License

AGPL-3.0; see [NOTICE](NOTICE.md). Xray-core is distributed under MPL-2.0; its
source pin and native dependency metadata are kept under tools/veyra.

Rovunet is based on [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full by Anonvector. It is an independent project and is not affiliated with the SlipNet project.
