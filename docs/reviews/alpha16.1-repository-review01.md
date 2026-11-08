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

## Validation and exact delivered artifact

Application source: `af237e5e53692862dffb82fe3033e16e9fc83204`, tree
`4248606f432bd11d441c6a1ce12bdbebb7bc3f6e`.
[Source CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487016569)
and [PR CI](https://github.com/msVPNco/rovunet-app/actions/runs/42487026297)
passed verification and Android device checks: 93 portable/JUnit cases, the
Gradle unit-test task, 55 Debug emulator cases and two minified Release JNI cases.
Four-language parity, editor localization, Kotlin safety and real loopback/panel
contracts passed. The optional separately dispatched complete native/signing
build was skipped, not counted as a pass.

The privately delivered `Rovunet-0.3.0-alpha16.1-code33-review01.apk` is
97,282,769 bytes. SHA-256:
`133a0a4f1aa97ddd9eb42c8ec5b0be8ca53c00ed5e33811e0a585ab773e3fc28`.
It retains `app.veyra.client`, code33 and the pinned production certificate
`1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`.
V2/v3 signatures, identical unsigned-CI ZIP payload, 16KB ZIP/ELF64 alignment,
JNI definitions and the canonical repository in DEX passed. No obsolete repository
URL remains in DEX. Pinned Xray, launcher and project Hev/JNI were rebuilt in CI;
other transports reuse the checksum-pinned SlipNet 2.5.6 payload. There is no claim
of a complete native rebuild or physical-phone validation.

The live canonical GitHub release fixture also selected the actual code32 APK
and its correct digest, with previews both enabled and disabled. Documentation
completion after these runs does not rebuild the APK; application/build/test
inputs must remain identical to the source above. Full verification and the
exact production R8 mapping accompany the private review delivery.

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
