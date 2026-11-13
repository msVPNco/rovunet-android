> Historical code42 owner candidate. Current startup candidate: [review03 / code43](alpha20-startup-review03.md); the Mahsa/privacy implementation below remains included.

# Android alpha20 owner review 02 / code42 — signed owner APK ready; phone acceptance pending

This follow-up supersedes the incomplete public-feed behavior of the delivered code41 owner APK. Code41 evidence and bytes remain unchanged. No alpha20 public release, tag or merge is authorized before owner acceptance of the exact new signed APK.

## Implemented scope

- Mahsa uses its direct MahsaFreeConfig app/sub.txt source. Existing Hiddify aliases resolve to that source before any request. The saved identity and collection are preserved on successful migration; failures do not migrate or overwrite the saved source.
- Decode Base64, AES-128-CBC with the specified ASCII key and IV, PKCS7 padding, strict UTF-8 and bounded JSON; collect only config strings from mci/mtn, deduplicate and validate with the existing importer. Never request ads_url or nested feed URLs.
- Download/decryption/schema failures and empty/no-usable results preserve prior profiles. Mixed valid/unsupported entries require an explicit preview and confirmation; card Update opens that review instead of silently replacing the set. TLS checks are unchanged.
- On successful confirmed refresh, remove only unmodified retired members of the same public source. Preserve manual profiles, unrelated subscriptions, local overrides, pinned/locked/unreadable profiles and chain references.
- Built-in source review and saved public subscription management omit raw source address fields, reveal/copy controls and source hostname. Both Mahsa and Anonymous keep their names and fetch/update actions. A new user-provided custom source still has an entry field.
- Patterniha remains absent from the built-in catalog and its fetch UI; user-imported configurations are preserved.

## Evidence scope

Portable tests, repository transaction instrumentation and Compose privacy tests are required for this exact candidate. Privacy coverage spans four languages and three themes. Only synthetic fixtures are committed. The current live Mahsa snapshot was inspected privately: ten unique links, seven accepted, three rejected by the existing importer (two TLS, one format). This is parsing evidence, not reachability or physical-phone acceptance.

Final identity is recorded in [alpha20-review02-verification.json](alpha20-review02-verification.json). The public APK keeps app.veyra.client and the pinned production certificate; code42 permits installation over delivered code41. No release publication.

## Deferred owner correction

Telegram is a channel, not a group. Correct the community label in all four languages in the next authorized UI correction, preserving the existing project link. This was explicitly requested for later and is not counted as implemented in this feed fix.

## Final owner candidate

- Source: `abdc9badcb9a9d8a718ddd2a7a7ccf89d2d94b6f`; tree `c888c1f9983de9d4e5b071268aae67211f301c1a`.
- Passing PR run [43289444004](https://github.com/msVPNco/rovunet-app/actions/runs/43289444004) checked out `f78a83a013dfc22f2be674b1417315359d1f7e43` with the identical tree. 152 portable/JUnit, 84 Debug emulator and 2 minified Release JNI cases passed.
- The first attempt failed because the new privacy test method inferred a non-Unit return. Its class did not run; that attempt is not passing evidence. The correction changes only the test return type, and both privacy cases passed on the final run.
- Full APK: `Rovunet-v0.3.0-alpha20-Full-review02.apk`, 158698991 bytes, code42, non-debuggable `app.veyra.client`. SHA-256: `8fd303ff3dbbf25a3909766898d651012e3e2125c29fcc3b6bb987067ff8a799`.
- Fixed signer SHA-256: `1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416`; verified for Android APIs 24–36 using independent official Build Tools 36.0.0. Every unsigned CI ZIP entry remains byte-identical after signing. 39 native libraries / 25 aligned 64-bit ELF files and actual gomobile DEX definitions checked. Slipstream checks ran against rebuilt CI outputs and transfer through the unchanged signed payload; no fabricated local build-output comparison is claimed.
- This file and the verification JSON are evidence-only changes after the passing build source. The owner must still accept the exact signed APK before release, tag or merge.
