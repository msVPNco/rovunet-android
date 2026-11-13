# Validation — 0.1.7 Beta 1

The current run is Windows x64, .NET10.0.14 / SDK10.0.403. Native fixtures are pinned Windows engines; no Darwin execution is substituted by these results.

| Suite | Passing checks | Scope |
|---|---:|---|
| UI | 1439 | Real rendered Avalonia controls; four languages, three themes, narrow/desktop widths; QR decode, clipboard, injected channel launch, hidden source URLs, status colors and stale-session results |
| Updater/store | 56 | Metadata/RID/OS/version/hash/redirect/route/cancel negatives; actual loopback HTTPS CONNECT without bypassing TLS; corrupt/oversized/stale writer preservation |
| DNS | 21 | Separate resolver, server-path and HTTPS states; cancellation, history and same-profile application |
| Mac domain | 123 | Import, trust, controller/session and capability behavior |
| Proxy | 23 | Simulated native configuration ownership/recovery; no real SCPreferences claim |
| Protocol | 89 | Parser/editor preservation and actual pinned Windows core configuration validation |
| Network | 21 | Real loopback VLESS TCP HTTP/SOCKS/IPv6/reconnect and three encrypted SS2022 methods; TLS rejection |
| Feeds/banks | 46 | Mahsa decoder/atomic replacement/failure preservation/hidden-source contract; bank inventory |
| Shared regression | 239 | Unchanged pinned portable Core contracts |
| Total | 2057 | Zero fixture failures |

Additional evidence: four fresh Go TUN control tests from the exact source; fresh isolated Linux namespace TCP/UDP, IPv4/IPv6, DNS and EOF cleanup using unchanged Linux binaries. These are not Darwin helper authentication or routed-traffic acceptance. Four shell scripts pass bash syntax validation; an independent fixture verifies eleven executable plus six dependency hashes in the Unix manifest writer. Actual Apple signing/notarization was not run.

The private supplied encrypted responses are test inputs only and are excluded from source/packages/logs. One ciphertext fails the supplied AES key/IV; another decrypts but contains two TLS_UNVERIFIED entries. Strict atomic failure preserves old data; valid protocol rows separately survive the same decoder. No raw private config or ads URL is requested or reported.

A separate live public feed observation on 2026-11-12: Mahsa fails TLS_UNVERIFIED; Anonymous has 111 accepted and 72 rejected with explicit reasons, including TLS policy and unsupported options. This one live failure is recorded separately from the passing deterministic suites; source contents may change. No real remote server sessions were initiated from these feed checks.

Final package evidence checks both PKG and DMG: extracted bytes against staged app, independent XAR/cpio checksums, modes and BOM, Mach-O architecture/load paths, resource/code seals, four tamper cases, version/source commit and unchanged native/helper hashes. Counts/hashes are in the accompanying VERIFICATION.json. Build completion is distinct from native acceptance.

Native gates: login-Keychain lock/deny/unlock/upgrade, Installer/Finder/AppKit/tray, physical Intel and exact changed ARM package, real per-family TCP/UDP/DNS/IPv6/reconnect, TUN sleep/interface/logout/error recovery and owner-authorized publisher signing. The CI template in ci/ has not been installed or dispatched. Persistent Kill Switch follows successful native TUN recovery.

Historical detailed family evidence is preserved in history/pre-0.1.7/TESTING.md and CAPABILITIES.md. No historical native result is relabeled as a fresh run.

## 2026-11-13 Mac review scope

- Exact stock arm64 PKG: native Installer upgrade succeeded; stored profiles preserved; unlock screen observed. Password entry/Allow choice were not reliably observed, so no credential-handling claim is made.
- Stock 0.1.7.1: REALITY 1 real TUN HTTPS 200, domain and pinned-IP requests; utun90 route.
- Connected-card change: 1442 headless UI checks on macOS, 0 failures, including four new ordering regressions. These are separate from native UI/network evidence.
- Local review integrity: Apple codesign strict/deep succeeds; independent verification covers 34 Mach-O files and 488 resources; all 30 engine-directory files equal the stock package.
- Intel, clean install, fresh TUN-helper installation, new Keychain ACL decisions, all-protocol matrix and long-duration recovery remain untested this session.


## Native owner review — 2026-11-13, Local Review 3

Installed exact arm64 package: Rovunet-macOS-0.1.7.4-Local-Review-3-arm64.pkg.
This is a local modified review based on source snapshot 21d41a565acca8dad64ac9d17adefc3fd13f70ad, not a published release. macOS 27.2.1 (26B131), Apple M3 Pro / Mac15,6, arm64, 18 GiB.
Native/UI results belong to this package; older dev6 results are not reused as acceptance.
The staged, installed and expanded package app each contain 491 files with identical SHA256 content maps. Apple codesign --verify --deep --strict passes. Ad hoc integrity is not Developer ID trust or notarization.
Managed suites: 610 checks, zero failures. Final UI suite: 1539 checks, zero failures. Total deterministic suite: 2149; remote feed/path acceptance is separate.
24 supplied atlas paths were attempted with system-wide TUN; 9 passed both TLS-verified domain and pinned-address HTTPS via utun90. Pass: REALITY 1/2, VLESS Encryption, Naive SOCKS5/SSH, StunTLS SSH, direct SSH, AmneziaWG, Shadowsocks 2022. 14 other paths failed real traffic; Slipstream SSH failed handshake before TUN. These are configuration/network-specific observations, not proof entire protocol families are universally unsupported.
Group test ran only 14 non-DNS paths, 12 succeeded; the 10 DNS profiles were not rerun. Stop/cancel and update disabled/success/failure feedback observed natively. Headless tests also cover no-fast-success DNS consent, refusal, cancellation and stale-result suppression.
Normal disconnect restored en0; controlled UI SIGKILL restored en0 within the first ~1.02-second observation, DNS/proxy matched baseline, direct HTTPS 200. Two full quit/relaunch cycles preserved data without repeated password prompt. Login Keychain lock/deny, logout, sleep/Wi-Fi change and reboot remain untested.
Original beta1 DMG mounted read-only, native Finder displayed Rovunet and Applications icons; no drag replacement was performed over the final installed review. Original package welcome/readme stale 0.1.6 version corrected in source templates.
Live feeds: Mahsa TLS_UNVERIFIED safely rejected; Anonymous 108 accepted / 69 rejected. No TLS bypass. Four private supplied feed bodies imported 11/11/1/1 profiles with no rejects; private bodies are excluded from delivery.
Performance samples are short UI-process measurements under a review/automation session, not battery-life benchmarks. Charging was not altered. Sustained energy, large-list performance, Intel native acceptance, full IPv6/UDP remote parity, updater production publication and persistent kill switch remain open.
Formal Developer ID Application/Installer and notarization deferred: no valid certificate/account entitlement available, owner requested deferral if membership/country restrictions are relevant. Do not call the ad hoc build trusted distribution, and do not disable Gatekeeper.


## Final local review 0.1.7.6 — 2026-11-13
Final UI1647 + latest managed639 =2286 checks, zero failures. Network26 includes real native Xray certificate pin correct/wrong/revoked and destination HTTPS validation. Source import and update flows fixed; Patterniha restored; observed persisted source counts Mahsa9/Anonymous166/Patterniha34. Mac-only per-profile certificate approval is bound to profile ID/content and certificate hash. AnyTLS/TUIC custom pin unsupported; no global TLS bypass.
Initial 24-path TUN matrix belongs to0.1.7.4; final0.1.7.6 retested12 paths,9 pass and WS/XHTTP/Trojan fail. Full DNS retest stopped at owner's request to preserve Codex connectivity. Final short local-only Soar test kept route=en0, received HTTPS200 headers but timed out at3s before body. No native Codex process exclusion implemented.
New build required owner-completed Keychain approval (SecurityAgent automation blocked); clean relaunch of the same final build preserved233 profiles without another prompt. Final long-session idle RSS1410MiB; fresh idle204MiB,CPU0.1% one core. Memory retention and active-session energy remain open.
Known visual issue: long unbroken certificate fingerprint can widen/clip RTL warning text. Owner explicitly queued7-second live latency for the next update; not implemented now. Formal Developer ID/notarization remains deferred. See NATIVE-REVIEW-20261113-FA.txt and UI-OWNER-REVIEW-FA.txt.

Local review 0.1.7.10, 2026-11-13. Completed settings/resolver/profile edits return to their origin after a successful save. Search uses close while empty or applied, and execute for a changed query, with Enter support. Import completion still goes Home. GUI retains a session AuthorizationRef for system proxy operations, passed only over inherited IPC to each guardian. Repeated external-form export/import and preferences reads passed in a signed test app, with no system writes. Actual prompt suppression across reconnect/expiry remains a native acceptance item. UI1707 and Network29 passed. The pinned Mihomo Darwin process-table offsets matched controlled TCP/UDP loopback sockets on this host. TUN data path and engine self-capture acceptance remain open.

Local review 0.1.7.11, 2026-11-13. Collection connection feedback is derived from the active, pending or failed child in that collection, updated without rebuilding the list. Add-existing and Update use the same state-bound removal plan and confirmation, with full encrypted-byte preservation on cancellation. Panel status delegates freshness to the shared StatusSnapshot evaluator, with sample time and monotonic session receipt; a restarted window treats old receipts as unverified. Runtime test assembly locations and hashes are recorded by the UI harness, with optional expected-hash assertions. Real TUN, repeated system-proxy authorization prompts, physical network recovery and battery acceptance remain open.

Local review 0.1.7.12, 2026-11-13. All macOS UI controls use the platform default font; technical labels and diagnostics use Menlo. Rendering preference is OpenGL with Software fallback. A native isolated rendering fixture showed correct full-width content and circular controls with Software and fresh Metal; the owner's long-lived Metal window had stretched content and a right-side gap. The fallback is a local mitigation pending extended resize/display/energy validation. TUN native binaries remain from the accepted same-architecture package. Supersedes the intermediate 0.1.7.10 and 0.1.7.11 installers locally.


## Review 0.1.7.12 handoff, 2026-11-13

Exact installed bundle 0.1.7.12 remains a partial local review. Package SHA256 7e82768015643420d4bc160e54bab571e59c307cf85efe343804ce75c42feaad; source ZIP a4fc62b7c5eae0d5002a3ea0631a1956d6b24b411dae698228b0497484d33046. All 491 installed/staged/expanded payload files match; ad hoc deep/strict integrity passes, not Developer ID/notarization. Source snapshot contains 1358 files and matched the checkout before this handoff and subsequent owner follow-up.

Fresh positive scope: 2387 checks across nine suites, plus 99 additional warning-layout checks (actual localized warning, four languages/three themes/320 width) and 3 installer architecture checks =2489. Headless/fixture/TestVault results are not physical UI, operational panel or Keychain acceptance. Intentional wrong assembly hash exits 1; initial harness loader/path/signature and wrong warning-key attempts are retained and excluded from acceptance.

MAC-0179-REMOVE-01, FRESHNESS-01 and PROVENANCE-01 are fixed with scoped evidence. Full endpoint/override/clone/reappearance binding remains open. ACT03-MACOS-01/02/03/04 remain partial. During an already active owner session, Trojan produced valid HTTPS bodies through utun90 and its local HTTP proxy; this is limited fresh evidence, not a full TUN matrix or HY2 acceptance. Initial short Telegram website TLS timeout and later same-session success are retained separately. No test started/stopped system TUN or wrote system proxy preferences.

System-proxy repeated authorization is OPEN. The owner's password dialog requests changes to system settings. Read-only authdb and authd show system.services.systemconfiguration.network -> authenticate-admin-nonshared timeout30; credentials expired at31.434/35.368 seconds. Retaining AuthorizationRef does not solve expiry. Touch ID versus password cause remains unproven; no policy, password storage or Keychain protection was changed.

Native UI performance remains OPEN: home CPU delta41.39%, settings1.23%, home repeat41.45% of one core with the same active connection. Two-minute RSS541-544MiB; sample physical footprint672MiB/peak698MiB. This is not a battery benchmark, leak diagnosis or proof of a renderer cause. Long-duration energy, display changes, recovery/sleep/Wi-Fi/reboot/Keychain deny, Intel, kill switch and native Codex exclusion remain open.

Owner follow-up after freezing the 0.1.7.12 report: pause marquee animation/shared clock without window focus and preserve position on return. Live latency was initially off; it is now observed enabled with refreshed active-card values. Fresh GUI probes were not propagated to the connection-status banner, explaining contradictory unverified text. These code fixes belong to candidate0.1.7.13, not retroactive 0.1.7.12 success. Native acceptance/install of that candidate must be recorded separately; preserve the active connection.


## Installed follow-up 0.1.7.13, 2026-11-13
The owner disconnected the active tunnel and authorized installation. Bundle/receipt now identify0.1.7.13. All491 installed/staged/expanded payload files match, ad hoc deep/strict integrity passes, native helper unchanged. Exact staged DLL UI suite1750 passes; 14 added checks cover focus/clock lifecycle and session-bound live status. Native observation showed live Trojan1239ms at19:03:46 and matching verified hero/collection/footer. Later owner TUN activation was observed, not initiated by the test harness. Marquee pause is installed; native focus/energy A/B remains an acceptance gate. An older network-change notice was also still present and is an open follow-up. Main report0.1.7.12 evidence and its source ZIP are frozen separately; this install does not retroactively fix that artifact.

## 0.1.7.14 local evidence

Exact packaged assemblies passed 1804 headless UI checks, 67 feed, 123 domain, 29 network, and 106 protocol checks. The native-bundle proxy harness passed 34 checks. An ordinary user's real SCPreferences lock on a private test file was denied before any write. The first plain-dotnet proxy harness failed in native authorization setup and is retained. A separate real-window same-page A/B measured only rendering with fixed synthetic state. See TEST-RESULTS.json, linked logs and per-run hashes in the local manager package. The four transport fixtures each exercised HTTPS and UDP on IPv4/IPv6 and DNS on TCP/UDP. The incorrect certificate pin was rejected.
