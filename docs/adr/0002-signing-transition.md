# ADR 0002: private candidates and production signing

Status: private-candidate separation accepted; production identity not yet provisioned.

Private APKs use app.veyra.client.candidate and the existing candidate certificate, so review03/code 21 updates review02/code 20. These packages remain separate from existing public app.veyra.client installations. Production build verification rejects the Android Debug identity and checks the configured production certificate digest.

A debug-signed bridge cannot make Android accept an unrelated production signer for the same package. The ordinary installer still enforces signing identity. Android signing lineage may support a controlled key rotation where the old private key is available and the device/installer support it; this is not a universal migration for the API 24 minimum.

Before publishing production: inventory the certificate of every public release, verify possession of the required private key, provision and back up a production signing key outside the repository, settle Play App Signing versus direct-APK update identity, and test an upgrade on the oldest supported Android version. If a compatible rotation is unavailable, provide an explicit encrypted export/import migration into a separately installed production application. Never ask users to uninstall the only readable copy of their credentials first.

No production key or public release is created by review03. See ../RELEASING.md for certificate checks.

## Alpha14 review: confirmed signing and migration constraints

The supplied alpha11 APK (package `app.veyra.client`, versionCode 17) has signer
SHA-256 `7d9eac2c155ae6c6d57dffa27e325858375016e88f9de1546a1ab97196a1d248`.
The private alpha14-review01 APK (package `app.veyra.client.candidate`, versionCode 24)
has signer SHA-256 `6a0fd4885b0c01a4d36410b5c6d1e20eab2ea10549753672ac1a9269bd8193e3`.
Both certificate subjects say Android Debug; they are different keys. Android Studio
generates a debug key locally. The well-known debug alias/password/subject do not let
someone holding a different debug key sign an accepted update. Exposure of the exact
private signing key would, and debug signing remains unsuitable for production.

`DeviceIdUtil` derives bindings from `ANDROID_ID`. On Android 8+, this identifier is
scoped to the signing key, user and device, and may change when the APK signing key
changes. A migration must test device-bound profiles across the actual target signing
identities; being on the same physical phone is not enough. Never clear or automatically
rebind a publisher's device restriction during migration.

Encrypted export is not a universal backup: `exportAllProfilesEncrypted` excludes
locked profiles. Individual/collection export enforces sharing restrictions. Inventory
eligible exports and verify an import before uninstalling anything. Protected profiles
may need the original publisher's link or a panel reissue for the new device binding.
Android Keystore secrets do not provide a portable backup after uninstall.

References: [Android signing](https://developer.android.com/studio/publish/app-signing),
[ANDROID_ID contract](https://developer.android.com/reference/android/provider/Settings.Secure#ANDROID_ID),
[APK signing lineage](https://source.android.com/docs/security/features/apksigning/v3).
