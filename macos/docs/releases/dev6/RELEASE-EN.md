# Rovunet macOS 0.1.0-dev.6 — prerelease

Native **Apple Silicon arm64** and **Intel x64** installers provide the same application features. Both require **macOS 15 or later**. Choose by processor; Air/Pro branding and display size do not require separate builds.

- Added AnyTLS, TUIC and single-binding Mieru (`mierus://`) import, editors and native engines.
- Added one per-collection Connection selection menu with Manual, Switch on failure and Smart. Switching uses bounded candidates, confirmed probes, minimum dwell and restoration after a rejected replacement.
- HTTPS subscription input from paste/file/QR opens subscription review. Provider-reported usage, remaining quota and expiration are retained; missing information stays unknown.
- Slipstream receives the server certificate automatically and requests first-use fingerprint approval. Stored identity is not silently replaced.
- Added status colors, correct connected-card actions, local proxies for apps such as Telegram and the Iranian Lion and Sun flag while preserving the Mac interface.

Quit the previous app with Command–Q, then install the matching PKG. Keep existing profiles and Keychain data. Do not reinstall a compatible same-architecture TUN service. A drag-to-Applications DMG is also provided.

These candidates have development signatures. Developer ID/notarization, execution on the exact native Macs, Keychain/Installer behavior and routed TUN traffic remain unverified. Full/multiple-binding Mieru documents, arbitrary chains, a persistent kill switch and process-enforced app routing are unfinished. DNS scans are limited to 2,048 addresses per batch.
