# Android alpha20 owner review 02 / code42 — implementation and validation pending

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

Final CI/source/APK identity will be appended after successful validation and signing. The public APK keeps app.veyra.client and the pinned production certificate; code42 permits installation over delivered code41. No release publication.

## Deferred owner correction

Telegram is a channel, not a group. Correct the community label in all four languages in the next authorized UI correction, preserving the existing project link. This was explicitly requested for later and is not counted as implemented in this feed fix.
