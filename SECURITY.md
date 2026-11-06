# Security policy

Rovunet is a multi-protocol Android VPN client. Current supported-release information and known limitations are recorded in [STATUS](docs/STATUS.md). The previous public alpha11 uses its historical debug signing identity. Alpha14/code 25 is being prepared; managed production signing and a validated migration remain publication prerequisites. Private candidate packages have a different identity. See [upgrade constraints](docs/UPGRADE.md).

## Report a vulnerability privately

Do not put credentials, private keys, configuration URIs, device identifiers or unpublished exploit details in a public issue or the public Telegram group.

If GitHub offers **Security → Report a vulnerability** for this repository, use that private reporting form. Otherwise, ask for a private maintainer contact in [the Telegram group](https://t.me/RovunetVPN), without disclosing the vulnerability or affected user's data in the group. Share technical details only after a private channel has been established. No separate private reporting email is currently advertised.

Include the app/source version, affected protocol or component, impact and minimal reproduction steps with synthetic credentials. Review and redact any attached report. The app's stability-report sharing is optional; it is not automatic vulnerability reporting.

## Artifact checks and scope

Download the APK from the repository's Releases. Compare its SHA-256 with the published checksums and retain the expected signer identity. A matching hash shows that a file matches the published artifact; it does not prove the application or server is free of vulnerabilities.

Saved SSH/TLS identity approval, encrypted backups and profile sharing controls have different roles. An embedded-key profile lock does not guarantee secrecy against another holder of the app. Existing native binaries and the unavailable pinned upstream source revision are disclosed in [the provenance report](docs/releases/alpha08-native-sources.md).

Do not use a public profile to expose server administration credentials. Server operators remain responsible for server updates and access control; client fixes do not update the server panel or its services.

## Stored credentials and diagnostics

Current stored profile secrets use Android Keystore-backed encryption; unreadable rows require explicit recovery. A password export is a separate portability mechanism and excludes locked profiles in bulk export. Device binding may change with signing identity; do not recommend uninstalling the only readable copy.

Safe connection reports accept typed events/codes, configured Soar policy and UI counter snapshots, not endpoint/account/credential fields. They are local and optional to share. Inherited verbose backend logs have a different scope and may contain sensitive data; review/redact them first.

An explicit unsigned Release preparation artifact cannot be installed and is not a public APK. Production signing checks remain required for ordinary signed releases; unsigned preparation is not a debug-signing fallback.
