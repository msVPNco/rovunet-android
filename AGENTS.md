# Rovunet maintenance instructions

## Shared application repository

Canonical repository: `msVPNco/rovunet-app` (the existing repository was renamed;
repository ID 1445854036). Android, Windows and future macOS clients belong here,
not in a separate `rovunet-windows` repository. See `docs/PLATFORMS.md`.

Historical alpha17 acceptance: the owner resumed development and accepted the exact alpha17/code36 APK on
2026-11-09 (source bbb30d7146c3f308f37943a0e3c9456b185167f0, SHA-256
eb5ac7bd474b37e361e8ca126c8e2f7d445faaf876ddd7b9ecd158548324a871).
That historical publication reused those bytes. Record later feedback for the next update,
including moving Patterniha into the same source group as Anonymous and Mahsa.
Do not move the Android tree or overwrite desktop work. Preserve the owner's
README layout, header styling, language order and tables.

Published Android tags retain `v<Android version>`. New desktop release tags use
`windows-v<version>` and `macos-v<version>`. GitHub Latest is repository-wide;
platform updaters and platform download links must select compatible assets from
their own channel, never assume Latest is an Android release.

## Historical alpha18 owner-review authorization

On 2026-11-10 the owner explicitly authorized the agreed post-alpha17 backlog and
approved Soft Pop mockup on a separate branch, a draft PR, GitHub checks and an
installable review APK. This supersedes earlier backlog-only planning. No merge,
release or public tag before the owner tests and accepts that exact APK. The
Patterniha decision is removal from the built-in catalog, superseding relocation.
See `docs/releases/alpha18-review.md`. Preserve the README layout and desktop work.

## Alpha18.1 completion publication authorization

Current completion target: **0.3.0-alpha18.1 / code39**, app source `e6359aae2e2e19164ad7e9ec87267f09a0c1152d`, signed APK SHA-256 `a649930c42e236226c903a43c5a8acc315864c87a2cb1a5b8902fc487f554e21`. See `docs/releases/alpha18-1-provenance.json` for exact passing CI, source and artifact identity. The earlier code37/38 review binaries remain historical and must not be relabeled as code39.

On 2026-11-10 the owner explicitly authorized implementing the agreed subscription
UX/metadata, existing-protocol compatibility (including cs/fm and Shadowsocks2022)
and AmneziaWG completion, running GitHub checks, signing and releasing this update.
This supersedes the earlier review-only hold for this exact iteration. Keep the
fixed production signer and disclose that local/emulator tests do not establish
new physical-phone/carrier/battery acceptance. Keep alpha18.1 with a higher build
code because code38 was already delivered as alpha18.1; never replace its bytes.
AnyTLS/TUIC remain subsequent work. Preserve README formatting and desktop code.

## Owner review before publication

From 2026-11-07, deliver each new installable APK to the owner for testing on
their phone before any public release. A successful CI run is not publication
approval. Do not publish, tag a public release or merge release changes until
the owner explicitly accepts that APK. Update all current GitHub guides,
four-language release information and artifact provenance together when that
release is approved. Prioritize remaining product-facing SlipNet/SlipGate
branding; retain legal attribution and installation/signing identity.

## Fixed production signing identity

The owner explicitly authorized a new production signer on 2026-11-06, before
user deployment. Public alpha14/versionCode 25 is the first release with it.

- Public package: `app.veyra.client`.
- Certificate SHA-256: `1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`.
- Canonical pin: `config/release-signing-certificate.sha256`.
- Read `docs/SIGNING-IDENTITY.md` and `docs/RELEASING.md` before any public build.
- Future direct APK releases MUST reuse this exact private key. A missing local
  keystore or reset workspace is NOT authorization to generate another key.
- Recover the existing key from the owner's private backups named
  `Rovunet-production-signing-2026.p12` and
  `Rovunet-production-signing-RECOVERY-PRIVATE.txt`; verify the recovered
  certificate against the versioned pin. Never print the password in logs.
- Keep private signing/recovery files outside source control, source archives and
  all public release assets. Only the public certificate/fingerprint is public.
- Private candidates use a separate package/signer and are never public upgrades.
  Historical alpha11's signer is different; do not promise in-place migration.
- Do not rotate the production key or change the pin without an explicit owner
  decision and a reviewed installation/migration plan. Play App Signing is not
  yet configured; its upload key is not necessarily the installed app signer.

## Product and privacy invariants

Preserve explicit/stored/imported choices. Manual connection must not be gated
on a ping, a fixed probe destination, or a resolver scan. Trust approval remains
separate and unknown server identities are never silently accepted.

Do not add the owner's personal names, addresses or identifiers to shipped code,
documentation, sample profiles or generated artifacts. The project Telegram link
and voluntarily published donation address are intentional public project data.

Record the exact build/source commit and actual test scope. Never describe
skipped checks, emulator-only evidence or reused native binaries as fresh
physical-network validation or a complete native rebuild.

## Windows 0.1.0 Beta publication authorization

On 2026-11-11 the owner explicitly requested GitHub publication as Windows0.1.0 Beta after reporting successful traffic on tested paths except Hysteria2/WireGuard. The beta changes version/packaging/public documentation from reviewed preview20/final4; connection implementation is identical. Source commit562a3d71670ceb7de909146bf68116d5cc614496; installer SHA256 bd1a46ab066cbb6f93850d9398b4c1a651407297550238ac6c2b6daf37bf0d3a. Publish only the listed verified assets under windows-v0.1.0-beta, marked prerelease, preserving Android Latest and all Android application/signing/build inputs. This permission is for this Windows iteration only. See docs/releases/windows-0.1.0-beta-provenance.json.
