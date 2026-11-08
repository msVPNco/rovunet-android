# Alpha16.1 repository compatibility — owner review pending

Unpublished Android compatibility build: **0.3.0-alpha16.1 / code33**.
Public alpha16/code32 and all its existing release assets remain unchanged.
The owner requested the necessary compatibility work after renaming the shared
repository to `msVPNco/rovunet-app`; general Android feature work remains paused.

## Scope

- Use the canonical shared repository for update API, downloads and Settings.
- Discover the newest compatible published Android APK from the paginated shared
  release feed. Desktop Latest, unsupported desktop tags, incompatible Full/Lite
  packages, draft releases and excluded previews do not become Android updates.
- Keep strict repository/tag/filename ownership, size/checksum checks and the
  existing installed-package/version/production-signer verification.
- Explicitly marked unsigned, private, debug, candidate, review and test APK names
  are excluded. Unresolved architecture ambiguity is not guessed.
- No tunnel, scanner, power policy, account status, card layout or translation
  behavior changes. Package and fixed production signing identity are preserved.

## Validation status

Local portable/JVM checks passed **93 JUnit cases**, including ten new updater
regression cases, plus parser, version and session/lifecycle entry points.
Four-language resource parity, editor localization, Kotlin safety and the device
result gate pass. This does not claim a fresh Android build, emulator pass,
physical-network result or signed review APK yet. Record final CI and binary
identity in the delivered verification report after those steps finish.

Public-release body navigation was updated by a separate documentation-only
workflow. It asserted that published asset identities and global Latest stayed
unchanged; that is not an application build or an APK publication.

## Owner checks for the eventual signed review APK

1. Install directly over the existing public app without uninstalling or clearing
   data. Existing code32 embeds the old download prefix, so repository redirects
   cannot install this fix through its old allowlist.
2. Check that profiles, language, card appearance and manual connection work.
3. Open Settings → releases/downloads; it must open `msVPNco/rovunet-app`.
4. Check updates with previews disabled and enabled. A current review build may
   correctly find no newer published Android APK; there is no public code33 yet.
5. Desktop/preview/flavour ordering is covered with synthetic release fixtures;
   no fake public desktop release is created for testing.

Do not merge application release changes, tag or publish this new APK until the
owner explicitly accepts the exact delivered file. The alpha16 release approval
does not authorize this different binary.
