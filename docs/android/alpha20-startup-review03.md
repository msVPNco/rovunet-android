# Android alpha20 owner review03 / code43 — signed owner APK ready; phone acceptance pending

Owner-authorized startup cover and local-load optimization, 2026-11-13. No merge, tag or public release before acceptance of the exact new signed APK. Codes41/42 and their evidence stay historical; code43 installs over them using the fixed production identity.

- Reuse the existing Rovunet R geometry/colors in a single Canvas, a static calm themed background, centered wordmark and small localized loading indicator. Four languages and Light/Dark/AMOLED. The cover scrolls if vertical space is constrained. Only functional loading motion; no decorative loop or minimum display timer.
- Initial state is loading, eliminating the empty home/FAB flash. The main screen is composed as soon as real profiles and saved selection are ready; the cover exits with a 140ms fade. Early preference loading also uses this cover. Failures retain the existing error path and remove loading state.
- Avoid serializing and SHA-256 hashing two full profile rows for every list read. The mapper freezes the same canonical snapshot and retains certificate validation; immutable row identity supports UI equality without hashing. Exact digests are calculated once, lazily, when reviewed deletion actually needs them. Stored/canonical comparison and transactional stale-row refusal remain in place. No disk plaintext cache or weakened credential encryption.
- Read saved UI history concurrently with Room/profile decryption. Preserve saved/manual selection before connection controls become available. Optional update-check networking and home account-status work wait until profiles are ready.
- New portable tests cover deferred digest work, equality, serialization exclusion and eager-constructor compatibility. Instrumentation covers four-language/three-theme cover behavior, immediate content availability without a minimum wait, and real mapper/DAO stale-delete protection. Existing selection/history, encrypted rows and subscription regressions remain required.

This removes known extra work from the startup path; no physical-phone timing improvement, percentage, battery or data claim is made. Android Keystore decryption and certificate validation still occur. Owner phone startup timing remains an acceptance item.

Mahsa/privacy fixes from code42 remain included. Telegram channel wording is still recorded for the later requested correction. Desktop, panel and root README layouts remain untouched.

Initial visual previews of the first passing build showed inconsistent symbol appearance in Dark/AMOLED even though the surrounding tile existed. The revised cover draws the same R geometry, underline and background together, and the matrix test checks actual teal glyph pixels in every language/theme before capturing screenshots. That earlier signed working copy was not delivered and is not owner acceptance evidence.

## Final review03 evidence

- Source `4714442846226c2c9c44444d43794ef25953761b`, tree `e577b6e333d0d521126fb66cb779ae84b6daa23c`. Passing [PR run 43372323547](https://github.com/msVPNco/rovunet-app/actions/runs/43372323547) checked out `51092656f11e4190e3eb083ac10e3975d477d110` with the identical tree: 154 portable/JUnit, 86 Debug emulator, 2 minified Release JNI cases.
- Three final raw PNGs were inspected and each contains 16,564 opaque teal glyph pixels in the same expected bounds; screenshot hashes are recorded in [verification JSON](alpha20-review03-verification.json). These are synthetic emulator views, not phone captures.
- `Rovunet-v0.3.0-alpha20-Full-review03.apk`, 158698991 bytes, code43, SHA-256 `930687063ffd7109cddff9ec798d988d1c2f129a806a8f861ff2da55d5d82d12`. Same pinned production signer, verified APIs24–36, non-debuggable, 39 native libraries / 25 aligned 64-bit ELF files and actual gomobile DEX. Every unsigned CI ZIP entry is unchanged after signing.
- Independent push run 43372317753 failed the unchanged local Mieru TCP fixture and the existing onboarding-persistence 15s timeout; the exact-source PR run passed all gates. Verify-only retry 131722552639 passed without source changes; the failed Android attempt remains explicitly failed. These intermittent failures need separate investigation; no protocol or onboarding fix is claimed here.
- Evidence documentation after this source does not change application, build or test inputs. No public release, tag or merge.
