# Rovunet maintenance instructions

## Shared application repository

Canonical repository: `msVPNco/rovunet-app` (the existing repository was renamed;
repository ID 1445854036). Android, Windows and future macOS clients belong here,
not in a separate `rovunet-windows` repository. See `docs/PLATFORMS.md`.

Android feature development is paused by the owner. The explicitly requested
repository-rename/update compatibility fix was completed in alpha16.1; keep new
feedback in the backlog until the owner resumes development. Do not move the Android tree
or overwrite desktop work as part of this rename. Preserve the owner's README
layout, header styling, language order and tables when editing links or prose.

Published Android tags retain `v<Android version>`. New desktop release tags use
`windows-v<version>` and `macos-v<version>`. GitHub Latest is repository-wide;
platform updaters and platform download links must select compatible assets from
their own channel, never assume Latest is an Android release.

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
