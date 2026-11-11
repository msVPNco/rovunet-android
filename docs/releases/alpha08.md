# Rovunet 0.3.0-alpha08 — Full

Version code: **14**. Application ID: `app.veyra.client`.

This release repairs the connection-test lifecycle, preserves imported credentials, adds server identity verification, and downloads verified updates inside the app. Selecting an individual path still connects directly; collection testing stays an explicit operation.

## Changes

- Each connection request has an identity. Late disconnect, error and service-destruction events cannot replace a newer request's state.
- Canceled setup, health checks, network recovery and delayed work are joined before another setup or cleanup touches native backends. Tests keep one service alive between paths and wait for cleanup acknowledgements.
- First use of an unknown SSH key or self-signed certificate requires explicit fingerprint approval during a **manual connection**. Tests do not silently approve keys. Changed identities are rejected. Public TLS certificates and hostnames are validated. TLS setup has a deadline. Saved approvals can be reset in Settings; verify fingerprints with the server administrator before accepting them.
- Chain startup enforces expiration and device restrictions on every member. Database transactions protect activation and chain membership during deletion.
- Editing preserves SSH destinations, timestamps and listener settings. Pin and display order were already preserved before this release.
- Native export format v33 adds lossless SSH/SOCKS credentials, including `|` and Unicode; v1–v32 remain readable. Hidden-resolver policy survives re-export. Naive credentials are percent encoded.
- Imports and cryptographic export work run off the UI thread. An import can be canceled.
- Test errors show the last-test time separately from connection status. Editing connection settings invalidates cached measurements.
- HTTPS uses a second independent 204 destination when the primary destination fails; normal certificate validation and the direct/DNS phase budgets remain in force. Setup and HTTPS deadlines vary for direct, Naive and DNS/Tor paths. A working tunnel is not guaranteed to reach either destination on every network.
- VLESS and DNSTT/NoizDNS SOCKS use the shared domestic TCP routing helper. Core paths receive equivalent direct rules, including native UDP. Core recovery retains those rules. This needs real-device testing alongside split tunneling and custom DNS.
- HTTP sharing requires configured proxy credentials, rejects missing or wrong authentication, and closes active sockets on stop. If local authentication is enabled, Android's credential-less HTTP proxy attachment is not used; applications continue through TUN.
- Update checking, progress, download, SHA-256/package/version/signer verification and Android's installation confirmation are available inside the app. Errors no longer appear as “up to date”. Installation permission is requested only when installing a downloaded update.
- VPN status notifications use the Rovunet R/shield mark. Launcher badges depend on the launcher, notification channel and user settings.

## Verification

See [repair verification](alpha08-verification.md). Checks cover the actual importer/exporter, manager callbacks, proxy sockets, TLS trust and native core behavior. These are container/loopback checks; they do not certify Android lifecycle behavior on a physical phone, all networks, throughput or censorship resistance.

The Full APK reuses the existing native binaries and signing identity for compatibility. A protected production signing key and store distribution remain separate release work. An APK's SHA-256 identifies its exact bytes; a certificate fingerprint identifies its signer. Neither guarantees that Play Protect will recognize a new publisher.

## Downloads and source

Use the assets on [the release page](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha08). Keep `APK-SHA256.txt` with the APK. The source asset contains application sources, licenses and the available pinned native source trees; see its source manifest for any retrieval limitations. Build instructions and pinned dependencies remain in the repository. [Native source provenance and the unavailable DNSTT wrapper revision](alpha08-native-sources.md) are recorded explicitly.

Subscriptions, automatic switching between collection paths, complete translation of inherited advanced screens and desktop/iOS clients are not included in this repair release.

Telegram: https://t.me/RovunetVPN

Optional development support: USDT on Tron/TRC20 only: `TG3Mhou3pdq9KaFgJSHjDiDx2ahhJuHvbJ`. Donations are not required to use the app.
