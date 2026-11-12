# Android alpha20 / code41 — owner review in progress

Baseline: main `c8da349ab4f4e4b413cbaa5b1d4a4a9a18adf866`, public Android
alpha19/code40, APK source `e8c93787fc4703cf4fea6c52ae056fae2724b105`.
Owner instruction 2026-11-12: implement the reviewed Android audit and prior
applicable requests, self-test, deliver the same-production-signer APK for phone
testing, then wait. No public release, tag or merge is authorized for alpha20.
Desktop, panel and root README layouts are outside this change.

## Prior work, with acceptance and next action preserved

| ID / origin | Requirement | Status and evidence | Remaining acceptance / next action |
| --- | --- | --- | --- |
| OLD-01 alpha18/19 | Existing subscription Update and reimport without deletion; preserve identities, overrides, other sources and rows on failure | Implemented in alpha19; SubscriptionDeviceTest and SubscriptionUiDeviceTest remain required in the device gate | Rerun exact candidate; phone report is separate |
| OLD-02 owner collection report | Direct paths first; no resolver/Soar test or prompt after direct success; fallback only after direct failure and consent | Alpha19 policy and historical-result correction retained; Alpha19MaintenanceTest, CollectionPhase regressions and device subscription/collection tests | Physical-phone complete/cancel/restart and stale-result review remain open |
| OLD-03 marquee clarification | Overflow text moves within its own single-line bounds; short labels static; preserve RTL, buttons and lifecycle limits | Alpha19 implementation and required CardInteraction device marquee cases retained | Rerun; weaker-phone cost unmeasured |
| OLD-04 compatibility/form request | SS2022, cs/fm, Hy2 hopping, AWG creation, VLESS encryption/heartbeat/KCP and external WireGuard links | Alpha19 implementation retained; CoreForm and interoperability suites | Extend explicitly rejected dialects only with fixtures and preserved semantics |
| OLD-05 protocol roadmap | AnyTLS, TUIC v5, limited Mieru and AWG, import/form/runtime/data evidence separately | Already present in alpha19; not newly added here; local encrypted interoperability and device startup/shutdown checks retained | Carrier/phone reachability unproven for untested endpoints |
| OLD-06 field failures | Explain own-panel WireGuard/Hy2 data failure by comparison with a healthy peer | Unresolved; physical server/client/network evidence unavailable in this environment | Owner-authorized controlled server inspection and physical phone capture; AWG success does not close this |
| OLD-07 lifecycle | Safe cancellation, stale-session ownership, selected vs active card, manual choice, bounded recovery, no disruption by candidates | Existing session, Hev, SSH, collection, independent-candidate tests retained | Rerun; Wi-Fi/mobile and low-end phone acceptance outstanding |
| OLD-08 consumption | Measure idle/screen-off/Doze battery and bytes, including four independent Slipstream workers | Physical measurement not available; no saving claim | Physical phone baseline/candidate under same duration/network/workload, before and after |
| OLD-09 visual/language | Three calm coordinated themes, four languages, opaque frosted bars, no decorative loops | Existing theme/localization tests retained; new footer/error matrix added | Physical legibility/touch/large-font check by owner |
| OLD-10 updater request, deferred then authorized by current iteration | Download updates through active VPN; preserve trusted repository/TLS/hash/signer | Implemented in candidate: metadata, checksums, APK and allowed HTTPS CDN redirects use same pinned route; bounded retry/range; stop on route change/cancel | Exact-candidate tests and owner real GitHub download; chained tunnel unavailable fails explicitly, never direct fallback |
| OLD-11 footer request | English legal attribution in all four languages, tiny readable centered LTR | Shared 11sp/16sp footer, original author and licenses retained; four-language/three-theme test | Owner visual acceptance; no Windows version text copied |
| OLD-12 Patterniha | Remove built-in provider, preserve manual imports | Already removed from Android; existing provider tests retained | No destructive cleanup of user data |
| NEW-UI owner 2026-11-12 | Failed result text red, analogous to green verified latency | Error text/details use theme error color, independent from green active-connection label | UI matrix checks both simultaneous states |
| OTHER-01 Windows-only requests | Mahsa direct AES source/migration, hidden Mahsa/Anonymous URLs, Windows Patterniha | Belongs to Windows owner; not implicitly transferred to Android | Coordinate separately; do not edit desktop |

Historical NEXT-ITERATION sections are not active regressions or new release
permission. Previous explicit cancellations stay cancelled. This ledger is not
a claim that every past request or physical acceptance is complete.

## Audit additions from 02-ROVUNET-ANDROID-PROMPT-FA.txt

| ID / section | Finding / classification | Action and evidence | Open dependency / acceptance |
| --- | --- | --- | --- |
| AUD-01 §3 identity | Existing production pin/package and offline-publication gate confirmed | Keep package app.veyra.client, certificate 1e40bd201489a8209043e346255772823d9d52ee21b468bdef2c152d239bc416; version41; inspect final signed artifact independently | Final hashes/CI recorded with delivered bytes; no new key |
| AUD-02 §4 source | Known dnstt-mobile exact-source gap persists | Read exact gitlink b265e04c40835ec1bacd447fff79169e6c2c2233; GitHub commit lookup returns404 on 2026-11-12, git remote unavailable; preserve checksum-pinned inherited binary | Recover exact tree or explicitly reviewed interoperable replacement; not fully self-contained/reproducible |
| AUD-03 §5 credentials/recovery | Keystore AES-GCM envelope, fail-closed read, guarded repair and encrypted backup already exist | DeviceSecrets, ProfileSecrets, repository and CredentialRecoveryTest inspected; keep schema50 and no lock/binding bypass | Device suite rerun; compromised-process resistance not claimed |
| AUD-04 §5 diagnostics | Typed bounded diagnostic exporter already excludes arbitrary endpoint/credential messages | ConnectionDiagnostics and redaction regressions retained | Raw engine log is not safe-report output; physical report optional |
| AUD-05 §6 input | Three document-picker paths read arbitrary files on UI thread before limits; confirmed source issue | Shared IO/cancellable/byte-bounded strict UTF-8 reader for main import, SSH key and DoH list; 2MiB/128KiB caps; failure does not invoke apply callback | Byte limit, invalid UTF-8 and cancellation unit tests; provider open itself can depend on external provider responsiveness |
| AUD-06 §6 parser | Main parser limits 2MiB/1000 rows, JSON depth guards, explicit effective-option rejection and existing mixed-bundle review exist | Retain existing import corpus/option/payload tests; no arbitrary script/YAML acceptance | New protocol dialects require separate fixtures; no blanket claim all arbitrary input audited |
| AUD-07 §7 VPN boundary | App self-exclusion is deliberate to prevent recursive core traffic; it also bypassed updater | Do not remove self-exclusion; explicitly carry updater traffic via authenticated local SOCKS with remote hostname resolution; HTTPS/TLS validation retained; no direct fallback on connected failure | Full backend IPv4/IPv6/DNS packet-capture matrix, real handover/crash/Always-on/OS lockdown tests still need phone/network; no global kill-switch claim |
| AUD-08 §8 protocols | Name/menu/import is not carrier proof | Existing local TCP/UDP, shutdown and device tests remain gates; capability table in alpha19-completion.md remains valid scope | Panel WG/Hy2 and actual mobile endpoints remain separate, no silent conversion |
| AUD-09 §9 scanner | Direct-first/consent/TCP_ONLY and isolated-worker policies already implemented | Existing phase, transport-selection, Soar fixture and four-worker tests rerun; keep concurrency caps | Device battery/stability and carrier resolver availability unmeasured |
| AUD-10 §10 consumption/account/UI | No measurement justifies a battery-improvement claim | Preserve visible-only probes, unknown/stale account data and manual choice; no new periodic full-feed job | Physical screen-off baseline/candidate; maintain account contract v1 unchanged |
| AUD-11 P2 protocol expansion | Target families already present | No extra engine or new protocol in alpha20; existing pinned native inputs | Future expansion based on actual need and interoperability evidence |
| AUD-12 P3 build/release | Existing exact-artifact/signature checks retained; test TLS certificate is ephemeral | Required tests expanded, test certificate/key generated only in memory; no fixture private key committed or shipped; no publication workflow added | Prepare signed owner APK and evidence; wait for owner acceptance before merge/tag/release |

The document's P0 labels are audit priorities, not proof of a critical exploit.
Unverified findings are not declared repaired. Source acquisition, physical
measurement, live-server investigation and app code changes have distinct gates.
No purchase, cloud deployment, server change, repository history rewrite or
production signing rotation is part of this iteration.

## Native/source inventory and license boundary

Production preparation still uses pinned Xray63ca2e0c1d31b89f67700c33842431f541fedd25,
reviewed checked-in Hev/JNI and launcher, Slipstream60a5e38c7414b35e4c2fddf8d4d89a4341470c6a
plus the versioned Android patch, and constrained Mihomo3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8
for AWG/AnyTLS/TUIC/Mieru. Remaining engines come from the SHA256-pinned SlipNet
2.5.6 APK in prepare-native.py. Each final packaged library hash belongs in the
artifact inventory, with rebuilt and reused classifications kept separate.
Existing source/license pins are retained, including lyrebird's Tor GitLab URL.
The app assets now also include Rovunet's complete AGPL-3.0 text and original
NOTICE, alongside the existing Xray/Mihomo license and pinned-source notices.
No claim of binary reproducibility or all-source completeness is made.

## Review, upgrade and rollback

No uninstall or data clear: production-signed code41 installs over code40 with
unchanged package/signer and schema50. Actual owner's phone upgrade remains an
acceptance gate. Debug/minified candidate tests have their own package/signing
and are not a public upgrade. A new physical-phone result must identify its APK.

Before installation keep an authorized encrypted backup where allowed, and retain
the old APK. Ordinary Android installation does not promise downgrade to code40.
The safe software rollback is a corrective higher-versionCode build using the
same signer and compatible storage; do not delete or weaken protected records.
Git revert/source rollback is separate from installed-app downgrade.

Final evidence: exact application commit, CI runs/artifact hash, native inventory,
APK SHA256 and signing verification will accompany the owner APK. Until recorded,
checks below are pending rather than passed. No new physical Wi-Fi/mobile,
weaker-phone, overnight/Doze, battery or carrier acceptance is implied.
