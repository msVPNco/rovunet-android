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

## Attribution

Original SlipNet copyright and license notices are retained in LICENSE and
[upstream documentation](README-UPSTREAM.md). Rovunet is not an official
SlipNet release. Xray-core is distributed under MPL-2.0; its source pin and
native dependency metadata are retained under tools/veyra.
