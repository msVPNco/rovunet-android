<p align="center">
  <img src="assets/rovunet-logo.png" alt="Rovunet" width="200">
</p>

# Rovunet

An independent Android multi-protocol VPN client based on [SlipNet](https://github.com/anonvector/SlipNet) 2.5.6 Full. Not affiliated with the SlipNet project.

Rovunet groups imported connection paths into named collections, verifies HTTPS
through the real tunnel, and asks before trying DNS fallback. The light interface
supports Persian, English, Simplified Chinese and Russian in the main workflows;
some inherited advanced text still falls back to English.

## Current build

0.3.0-alpha04 Full, versionCode 10. Experimental and debug-signed; real-device
testing is still in progress. No store listing or public release exists yet.

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

- [Guide](docs/GUIDE.md): tunnel types, building, CLI client and configuration
- [User guide](docs/USER_GUIDE.md) (English and Persian)
- [Security policy](SECURITY.md)

## Support

Rovunet is unpaid volunteer work. If it helps you, donations are welcome:

- **USDT (Tron, TRC20):** `TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`

Send only USDT on the Tron (TRC20) network to this address. The same
address is shown in the app under Settings → Support.

## License

AGPL-3.0. Rovunet is based on SlipNet by Anonvector and is not affiliated
with it; see [NOTICE](NOTICE.md). Xray-core is distributed under MPL-2.0; its
source pin and native dependency metadata are kept under tools/veyra.
