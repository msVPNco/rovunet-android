# Android alpha20 owner review03 / code43 — implementation awaiting exact-build checks

Owner-authorized startup cover and local-load optimization, 2026-11-13. No merge, tag or public release before acceptance of the exact new signed APK. Codes41/42 and their evidence stay historical; code43 installs over them using the fixed production identity.

- Reuse the existing Rovunet R vector, a static calm themed background, centered wordmark and small localized loading indicator. Four languages and Light/Dark/AMOLED. The cover scrolls if vertical space is constrained. Only functional loading motion; no decorative loop or minimum display timer.
- Initial state is loading, eliminating the empty home/FAB flash. The main screen is composed as soon as real profiles and saved selection are ready; the cover exits with a 140ms fade. Early preference loading also uses this cover. Failures retain the existing error path and remove loading state.
- Avoid serializing and SHA-256 hashing two full profile rows for every list read. The mapper freezes the same canonical snapshot and retains certificate validation; immutable row identity supports UI equality without hashing. Exact digests are calculated once, lazily, when reviewed deletion actually needs them. Stored/canonical comparison and transactional stale-row refusal remain in place. No disk plaintext cache or weakened credential encryption.
- Read saved UI history concurrently with Room/profile decryption. Preserve saved/manual selection before connection controls become available. Optional update-check networking and home account-status work wait until profiles are ready.
- New portable tests cover deferred digest work, equality, serialization exclusion and eager-constructor compatibility. Instrumentation covers four-language/three-theme cover behavior, immediate content availability without a minimum wait, and real mapper/DAO stale-delete protection. Existing selection/history, encrypted rows and subscription regressions remain required.

This removes known extra work from the startup path; no physical-phone timing improvement, percentage, battery or data claim is made. Android Keystore decryption and certificate validation still occur. Owner phone startup timing remains an acceptance item.

Mahsa/privacy fixes from code42 remain included. Telegram channel wording is still recorded for the later requested correction. Desktop, panel and root README layouts remain untouched.
