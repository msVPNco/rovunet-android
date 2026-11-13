# Remaining work — 0.1.7 Beta 1

All original requests, origins, priorities, dependencies, acceptance criteria and next steps are retained individually in TASK-REGISTRY.json and the two tables in WORK-STATUS-FA.md. Nothing was dropped because it was absent from the new attachment.

P0: native login-Keychain/upgrade/concurrency acceptance, native restricted-helper authentication and TUN recovery, exact Intel package acceptance. Existing controls are retained and host defects repaired; device-dependent results remain unknown.

P1: native family-by-family traffic and trust/identity rotation, DNS two-stage results and persistence, live failover, native menu/Finder/14-inch visuals, remaining advanced editor dialect parity. Mahsa source currently contains TLS_UNVERIFIED rows, so strict atomic update fails safely; do not weaken identity verification to make a public feed appear successful. Follow the documented low OpenSSL advisory with a validated update when available.

The attachment promotes persistent Kill Switch to P1 only after M1.2 native TUN recovery is proven. Distribution identity/signing is also P1: all-engine signing/helper-policy regeneration and notary stages are prepared but require a Mac and existing publisher credentials. Neither item is declared complete.

P2 extensions remain open: forced process routing (local proxy for compatible apps already exists), arbitrary multi-profile chains, full Mieru/multiple bindings and legitimate protected-link support, bounded queued large-bank scanning if needed. These need versioned contracts and independent runtime/negative acceptance; no decorative controls claim implementation.

P3: older macOS support if requested and wider hardware/CPU/RSS/energy measurements. Current minimum remains macOS15, with Metal-first UI unchanged.

The completed managed changes and final package evidence are described in STATUS.md and delivery VERIFICATION.json. Public 0.1.6 remains unchanged; this candidate has no public publication authorization. Historical backlog and stop notes are under history/pre-0.1.7/.

## 2026-11-13 observed native UX review

- Connected/recent path promotion implemented in Local Review 1; validate native reordering and scroll on successful connection.
- Installer 0.1.7.1 displayed stale 0.1.6 welcome/readme/product metadata. Source templates corrected; local review installer uses an explicit local-review label.
- Path name and latency marquee overlap/clipping at narrow native window widths, in English and Persian. Review clipping, column minimums and compact actions.
- Collection usage/unknown/update area consumes excessive vertical space; prefer compact summary and progressive disclosure.
- Floating Add and persistent bottom status obscure lower cards. Verify viewport padding across window sizes.
- Existing error/notice text remains in previous language after switching. Store message keys and arguments for rerender.
- Distinguish selected card from connected card and make single-click vs second-click connection behavior clearer.
- Upgrade unlock copy covers Allow/Always Allow but those OS choices were not reliably observed; test explicit denial, retry and next launch without resetting production Keychain data.


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

## Remaining after 0.1.7.14

Real host routing and recovery under independent macOS, sleep/wake, Wi-Fi handoff, abrupt worker death and reboot. Production private panel/status endpoints, ordinary WireGuard, native Intel, long-term CPU/energy and distribution signing. The existing system-proxy writer still needs the protected system right. Do not expose a new privileged proxy operation through the generic stdin client. Acceptance requires an authenticated application caller and service, fixed loopback operations and per-session ownership, journal and CAS, explicit installation consent and independent recovery tests. No weakening of authorizationdb or keychain protection was used.
