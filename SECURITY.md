# Security policy

Rovunet is a multi-protocol Android VPN client. Current supported-release information and known limitations are recorded in [STATUS](docs/STATUS.md). The public alpha11 retains the existing debug signing identity for upgrade compatibility; production signing and physical-device validation are pending.

## Report a vulnerability privately

Do not put credentials, private keys, configuration URIs, device identifiers or unpublished exploit details in a public issue or the public Telegram group.

If GitHub offers **Security → Report a vulnerability** for this repository, use that private reporting form. Otherwise, ask for a private maintainer contact in [the Telegram group](https://t.me/RovunetVPN), without disclosing the vulnerability or affected user's data in the group. Share technical details only after a private channel has been established. No separate private reporting email is currently advertised.

Include the app/source version, affected protocol or component, impact and minimal reproduction steps with synthetic credentials. Review and redact any attached report. The app's stability-report sharing is optional; it is not automatic vulnerability reporting.

## Artifact checks and scope

Download the APK from the repository's Releases. Compare its SHA-256 with the published checksums and retain the expected signer identity. A matching hash shows that a file matches the published artifact; it does not prove the application or server is free of vulnerabilities.

Saved SSH/TLS identity approval, encrypted backups and profile sharing controls have different roles. An embedded-key profile lock does not guarantee secrecy against another holder of the app. Existing native binaries and the unavailable pinned upstream source revision are disclosed in [the provenance report](docs/releases/alpha08-native-sources.md).

Do not use a public profile to expose server administration credentials. Server operators remain responsible for server updates and access control; client fixes do not update the server panel or its services.
