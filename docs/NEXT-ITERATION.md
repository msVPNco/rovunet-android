# Rovunet next iteration

## Current alpha18.1 completion

The owner explicitly authorized implementation and release on 2026-11-10. This supersedes the review-only holds recorded below for this iteration. Subscription card updates, optional names, provider usage/expiry, supported cs/fm, SS2022, Hy2 hopping and AmneziaWG are implemented. See [completion and validation limits](releases/alpha18-1-completion.md) and [provenance](releases/alpha18-1-provenance.json). Earlier planning below is historical; it is not evidence of an additional regression.

Next priority: validate physical-phone whole-card cancellation, low-end UI, screen-off battery/data and the reported panel WG/Hy2 endpoints; finish remaining existing-format options with explicit rejection. Then AnyTLS and TUIC, followed by Mieru. AmneziaWG is already included this iteration. Do not repeat the previously reported Hev alert without new evidence.

## Planned Android follow-up — 2026-11-11

Backlog recording only: this entry does not authorize application implementation, builds or publication. Recheck each reported gap against the implementation before changing it; preserve existing desktop work and README formatting.

1. Complete Android manual creation/editing for engine-supported options: Shadowsocks 2022 methods with key validation; Hy2 `mport`/`hop-interval`; supported `cs`/`fm` per protocol; AmneziaWG creation from scratch with compatible single-peer parameters; and supported advanced VLESS encryption, WebSocket heartbeat and mKCP settings. Extend existing family forms rather than duplicating protocol buttons. Preserve effective settings through import, edit, save and export; reject unsupported settings explicitly.
2. Add external `wireguard://` link handling to the Android import flow, with the same validation and user review as other imports.
3. Validate the reported panel WireGuard/Hy2 paths on a physical phone, comparing a known-working profile and checking server/client agreement and actual data transfer. Source-format compatibility alone does not establish reachability or the root cause.
4. Complete physical-phone whole-collection tests and cancellation, repeated connect/disconnect, Wi-Fi/mobile handover, lower-end UI responsiveness, and screen-off/idle battery and data measurements. Keep manual selection intact and distinguish automated/emulator evidence from physical-device results.
5. After existing-format completeness and validation, consider AnyTLS and TUIC, then Mieru. AmneziaWG engine/import support already shipped in alpha18.1; its remaining form/field validation work is not a new protocol implementation.

### P1 — Existing subscription update/re-import failure

Owner report on alpha18.1: updating an existing panel subscription displays a generic failure; importing the supplied current feed is blocked until the previous subscription is deleted, after which import succeeds. The panel now provides a primary Android feed and a separate `.windows.json` feed. Screenshots show the generic update error and unavailable account information. Account-status failure may be separate; do not assume a shared root cause.

A read-only HTTP check at report time returned 200/application-json for both feeds, each with the expected version/name/items schema and unique item IDs (24 primary items, 22 Windows items). This proves basic HTTP/schema availability only, not full app parser acceptance, device network behavior, successful update reconciliation or account-status correctness. Do not publish the supplied private URLs, tokens or profile credentials.

Reproduce old-subscription-to-current-feed updates and re-import without deleting prior subscriptions. Investigate saved-source URL versus edited URL handling, duplicate-source/collection identity checks, item reconciliation, changed feed membership and format compatibility. Treat these as investigation targets, not established causes. Verify platform-feed distinctions without silently merging incompatible sources. Also check status URL derivation and status retrieval for platform-suffixed feeds.

Acceptance: existing subscriptions can update; re-import of an existing source offers a clear update/review flow; a changed URL can be reviewed and migrated safely where appropriate; unrelated subscriptions and user edits are preserved; removals retain review requirements; failure leaves existing profiles intact. Add redacted actionable error categories instead of only the generic failure, and verify UI outcome/closure after success. No deletion workaround or forced removal of duplicate protection. Use synthetic fixtures for regressions, including changed item sets, source changes, platform feeds and unavailable status metadata. This record requests a future fix, not an immediate implementation/build/release.

### Android card/profile overflow text — owner request, 2026-11-11

For collection cards and configuration/profile cards, when a display label is wider than its own text container, keep it on one line and show the full text with a scrolling marquee. Do not clip/ellipsize it or wrap it onto two lines. Labels that fit remain stationary. Keep action buttons, card geometry and touch targets intact; constrain scrolling to the label container. Support Persian/RTL, English/LTR and mixed-direction names, and larger font settings. Run marquee work only for overflowing labels on visible cards while the app is visible; suspend it off-screen/background to limit CPU/battery cost. This is a specific requested text animation, not permission for unrelated continuous decorative animation.

Record for the next Android update; implementation/build/publication is not authorized by this backlog entry.

### P1 — Restore fast-first collection-test DNS fallback policy

Owner report, 2026-11-11: a whole-card test appears to give Soar a latency result before asking whether to test DNS paths. Required policy: first test non-DNS/direct candidates; only if no usable verified direct path exists offer DNS fallback with consent. Successful direct testing must not automatically offer or start DNS tests. Explicit manual testing of a DNS profile remains available.

Confirmed source discrepancy in published alpha18.1 APK source `e6359aae2e2e19164ad7e9ec87267f09a0c1152d`: `MainViewModel.runCollection` calls `CollectionPhase.completion` with `includeDnsAfterSuccess = !connectAfter && !dnsOnly`; `CollectionPhase.completion` consequently returns OFFER_DNS after a successful whole-card direct test. This violates the requested fallback-only policy.

Do not claim that Soar is definitely being probed during phase one: `ProfileCollections.isDns` includes SOAR/SOAR_SSH and the phase-one candidate filter excludes DNS types. A separate source-supported display hypothesis is that only current candidates' results are made historical/reset, while excluded DNS results remain in the result map; a prior fresh Soar measurement can remain visible during a new direct-only run. Also inspect imported runtime tunnel types and alternate single-profile/monitor test entry points. Reproduce before assigning the Soar symptom a root cause.

Acceptance: no Soar/Soar+SSH, DNSTT/NoizDNS/VayDNS/Slipstream variants are probed or scanned during the first phase; correct classification follows the actual transport rather than a display name. A successful verified direct path completes that phase without the DNS fallback prompt. If direct candidates finish without a usable path, offer the consent-based DNS phase; distinguish cancellation, incomplete budget and unresolved identity approval from confirmed failure. Keep old measurements with age/source markers, and label deferred DNS paths as not tested in the current run instead of implying fresh success. Preserve manual choices, cancellation/cleanup and deliberate manual DNS tests. Add regressions for direct success/failure, prior Soar cache, imported types and phase boundaries. Backlog only: no implementation/build/release in this recording.

Track capability status separately as import, manual form, runtime engine, verified data transfer and published version; do not count a menu label as complete support.

For the separate server investigation, first inspect the server environment, then test a compatible new protocol independently and obtain an importable phone configuration. Panel integration or replacement of existing Hy2/WireGuard outputs is a later decision based on physical-phone data transfer; no panel/server changes are made by this documentation update.

## Historical planning and observations

The owner authorized implementation on a separate review branch, GitHub checks and a test APK on 2026-11-10; merging and release still require acceptance. See [alpha18 review](releases/alpha18-review.md). Keep the published alpha17/code36 APK unchanged. Current decisions below supersede older planning notes, including the old request to move Patterniha beside other providers.

| Priority | Item | Scope and acceptance |
| --- | --- | --- |
| P0 | Fix intermittent Disconnecting / Hev native stop stall | Recurred in alpha17/code36: report attempt 13 stops at STOP_HEV_BEGIN (+484 ms), with no STOP_HEV_END about 45 seconds later. Screen was on. Earlier screen-state reporting correction did not fix this stall. Reproduce stalled SOCKS handshakes, active TCP/UDP and rapid collection-test cancellation; fix native cancellation and verify complete resource teardown before another engine starts. Never merely hide Disconnecting or declare success while an old native owner remains. Root cause is not yet established. |
| P1 | Remove built-in Patterniha provider | Replaces the previous cosmetic regrouping request. Its modified configurations can require a companion client/local endpoint that this app does not provide. Remove the built-in entry in the next update; preserve existing user-imported profiles unless explicitly deleted. Do not weaken TLS or globally reject legitimate local/private manual imports. Provider failures are not proof of the Hev stall's cause. |
| P1 | Diagnose Slipstream and Slipstream+SSH connectivity | Separate DNS resolution, QUIC readiness, SOCKS/SSH authentication and actual data transfer. A local listener or TUN alone is not tunnel success. Compare installed client/server versions and actual selected resolvers. Investigate IPv4-only socket handling as a hypothesis, not an established cause. Add support for the tunnel's public certificate/pin through panel export, app import and native client; it is separate from the SSH host key and HTTPS certificate. Missing optional pin support alone does not explain the failed connections. |
| P1 | Four parallel full Slipstream resolver tests | Owner requested concurrency 4 for both Slipstream variants. Create four genuinely isolated full E2E workers before raising the current cap of 1: the shared native singleton cannot safely be reused by concurrent tests. Candidate tests must neither stop nor reuse the active connection. Measure completion time, stability, CPU, memory and data on a physical phone; do not promise fourfold speed or raise the cap to 10 without evidence. |
| P2 | Extend Chain to Xray-backed transports | Plan support for SSH carried over an outer Xray transport, including Hy2/WireGuard where the first hop is reachable. Current Chain validation and startup do not support XRAY. Validate capabilities per transport and independent lifecycle/port ownership rather than merely enabling the enum. The reverse direction, carrying Hy2/WireGuard through SSH, requires UDP relay/IP tunneling beyond TCP-only SSH forwarding and is a separate feasibility item. No claim that chaining fixes an unreachable first hop or automatically increases speed. |
| P2 | Investigate intermittent subscription error-display UI test timeout | Keep the failure visible; reproduce and correct timing/lifecycle behavior without hiding failures or removing coverage. |
| Investigation | Hysteria2 and WireGuard physical-network failures | Still reported; local interoperability tests do not establish operation on the affected mobile network. Collect protocol-specific diagnostics before promising a fix. |
| P2 | Align AMOLED, dark and light themes | Owner approved the shared visual direction from the Windows reference for future theme alignment; implementation is authorized for the unpublished owner-review build. Approved Soft Pop option 1: gradual slate-navy at the top to black at the bottom, soft raised pastel lilac/mint controls with short press feedback, a taller translucent header similar to the bottom panel, subtle borders and restrained shadows. Pastel Clay option 2 is deferred to later updates. Dark keeps a very dark navy background; light uses off-white with white cards. Keep the same layout, icons and status meanings across themes, with accessible contrast: lilac/purple accents, mint green for connected, amber for warnings and red for errors. Reserve the purple gradient for the selected card, simplify connection-button rings and align the title R accent with the main lilac. Preserve visual continuity with Android; adapt to each platform rather than copying the desktop layout. Respect explicit theme selection and Follow system. Any display power-saving benefit applies to OLED hardware only. |

## Unpublished alpha18.1 follow-up — owner acceptance pending

The owner authorized the follow-up and the additional real subscription cases.
Current implementation is on `fix/alpha18-owner-followup` / draft PR #18; see
[the follow-up scope and validation limits](releases/alpha18-1-review.md).
It includes physical-route/interruption handling, shared visible-only latency,
bounded smart recovery, calm three-theme refinement, update UX, WebSocket
heartbeat/KCP sharing options, single-key VLESS encryption, WireGuard sharing
URIs and collision-free native VLESS subscription identities with legacy-row
preservation. Local JVM regressions pass; the final commit still requires GitHub
device/minified checks and a production-signed owner APK. Imported endpoints are
not thereby proven reachable. No panel/server changes, merge, tag or release are
authorized by this record. The older policy text below is historical planning.

## Owner observation hold and planned follow-up — 2026-11-10

The owner originally requested one or two days of observation. Later on 2026-11-10 the owner explicitly authorized implementation of the prioritized follow-up, GitHub tests and another signed owner APK. The observation hold is superseded; merge and release still require acceptance. The previous alpha18/code37 signed owner APK had source `83484cca3afaf8b17dbdb0f882f0a72271534fdc`, SHA-256 `c9664716fca637bde27ee1b2a524b5b653c3ee57502b5b57d337a3f5021ccf98`. Those are historical bytes, not evidence for the alpha18.1 follow-up.

| Priority | Planned item | Scope and acceptance |
| --- | --- | --- |
| P1 | Collection test stops after a few measurements | Investigate the owner's alpha18 report: the app did not hang, but the collection run stopped with a generic error. Recorded Hev teardown completed promptly in the supplied attempts; this is not proof that the original stall can never recur. Audit physical-route detection independently of the app's temporary VPN; a route snapshot becoming unknown during VPN setup is a code-supported hypothesis, not a proven phone root cause. Record redacted collection stop reasons and preserve specific messages through the UI error mapper. Retain valid completed results; individual endpoint failures must not abort unrelated tests. Stop safely when native ownership is genuinely unresolved. Reproduce on device before claiming a fix. |
| P2 | Approved calm frosted UI refinement | Floating rounded header with no opaque full-width backing; header/footer use restrained, relatively opaque frosted slate-lilac, with no legible underlying text. Ordinary child cards are distinct smoky blue-grey, with soft diffuse shadows and hairline borders instead of heavy black backplates. Selected child cards use a visibly lighter, desaturated lavender-grey fill. Connected state adds a calm mint outline and explicit text; selection alone must not imply connection. Reduce vivid purple across the collection/header/footer/buttons, preserve the gradual slate-navy-to-dark backdrop, and retain the approved geometry. |
| P2 | Fresh active-path latency and bounded automatic recovery | Plan a lightweight verified HTTPS latency sample about every 30 seconds while the app is visible, with measurement age and stale/failed states. The active-path probe uses its own tunnel. Only after sustained deterioration, test a small bounded shortlist outside the live tunnel and consider switching within the chosen collection. Manual mode never switches automatically. Screen-off and battery behavior, isolation, comparison fairness and cancellation must be validated before implementation is accepted. |

### Visual review acceptance

- The latest desaturated preview supersedes earlier vivid-purple and black-framed previews. The newer preview only softens mint further and illustrates an active `HTTPS 801 ms · 5s ago` label; those numbers are mock data, not a measurement or working feature.
- Use consistent hierarchy: background darkest, ordinary cards smoky blue, selected card lighter muted lavender-grey, header/footer subdued slate-lilac. Preserve spacing, touch targets, RTL, text scaling and the owner's README format.
- Distinguish selected, connected, warning, failed and historical states through words/icons as well as color. Mint means connected; a purple/lilac selection by itself does not.
- Check contrast in actual rendered themes (target at least 4.5:1 for ordinary text and 3:1 for large text and meaningful controls), disabled-action clarity, readable footer content, scroll insets and reduced-motion preferences. Generated imagery does not establish those ratios or accessibility compliance.
- Keep gradients/shadows static and press feedback short. Do not add continuous decorative animation or blur work merely to imitate a generated image; measure smoothness on a lower-end physical phone.

### Historical monitoring proposal — subsequent implementation needs acceptance

1. **What is measured:** end-to-end HTTPS response latency in milliseconds through the active path, not ICMP ping, throughput, or a guarantee that every site works. Show the age of the last successful sample. A new failed sample must not leave an old value looking fresh or green. Manual mode may refresh this display but has no automatic connection replacement.
2. **Cadence and cost:** start with approximately 30-second visible-app sampling, one small verified request per cycle, no overlap, bounded deadline and response size, cancellation on disconnect/session change. Reuse recent suitable measurements instead of duplicating independent health probes. Tentatively accumulate the last three cycles for a median/streak; do not fire three new full tests every 30 seconds solely for the badge. This needs battery/data measurements. When screen-off/idle, back off or suspend elective latency sampling, avoid a continuous wake lock for this feature, and retain event-driven handling of real service failure.
3. **Trigger:** a proposed starting point is three consecutive failed or poor samples, assessed separately from a confirmed tunnel disconnect. Confirm a failed test destination against another controlled target before condemning a healthy tunnel. A single spike or a transient network handover does not trigger a switch. The exact count/deadlines remain a phone-test tuning decision.
4. **Modes:** FAILOVER replaces only a failed path; SMART can also replace a persistently slow one; MANUAL never switches. Restrict all automatic candidates to the selected collection and honor expiry, credentials, identity approval, manual resolver/transport choices and existing consent for DNS fallback.
5. **Outside-tunnel candidate tests:** use independent engines and private endpoints; explicitly protect/bind their underlying sockets and DNS, or use a verified per-session self-exclusion path. Prove traffic really bypasses the current VPN rather than assuming a separate process does. Do not create a competing Android VPN/TUN, stop the current native owner, weaken TLS, or silently fall back to testing through the current exit. If a protocol cannot be tested independently, show that limitation and do not interrupt a healthy live path to manufacture a measurement.
6. **Bounded discovery:** only scan a small rotating shortlist (tentatively 3–5 candidates) after a trigger, with capped concurrency, timeout, data and CPU budgets; never recheck hundreds or thousands of subscription entries every 30 seconds. Do not confuse the separately approved four isolated Slipstream resolver workers with a general permission to run all transport engines in parallel.
7. **Switch hysteresis:** keep the prior policy as a proposed starting point: current median at least 2000 ms, candidate median at most half the current value, a recent confirmed candidate result, and at least 120 seconds since the last elective switch. This means 50% lower latency, not 50% higher throughput. Compare similar time windows, same destination and network, and separate setup time from request latency. A genuinely disconnected tunnel can recover without waiting for the elective dwell timer.
8. **Safe transition:** re-check ownership and mode immediately before switching; manual selection/disconnect wins and cancels background work. Perform orderly teardown/setup, verify the replacement actually passes data, and retain bounded recovery to a previously usable path if replacement fails. Switching is not seamless and may interrupt existing TCP sessions; avoid oscillation and misleading connected states.
9. **Limitations at the alpha18/code37 planning point:** source review showed a 180-second smart cycle with three-sample live verification and outside-tunnel candidates limited to Xray-backed profiles plus per-session bypass permission. The follow-up changes this implementation as recorded above. Hy2/WireGuard field failures remain unexplained by the supplied safe report; support for importing a sharing format is not proof of carrier-network connectivity.
10. **Validation before a later APK:** real Wi-Fi/mobile handover, low-end phone responsiveness, unchanged manual choice, no stale-callback teardown of a later connection, isolated candidate traffic, failed candidate cleanup, stale badges, screen-off battery/data cost and repeated group-test completion. Implementation was subsequently authorized; retain the requirement for explicit acceptance before merge or release.

## When to propose another version

- Severity takes precedence over item count. The repeated connection-blocking Hev stall already warrants proposing a focused maintenance update; do not delay its correction solely to bundle Chain or other features.
- A reproducible security/trust failure, data loss, or inability to connect/disconnect merits prompt release planning. Otherwise bundle several validated user-impact improvements.
- A proposal is not release authorization. Implement only when authorized, validate the correction and regressions, provide the exact candidate APK to the owner, and publish only after acceptance and explicit release approval.
- During backlog review, notify only on a new material release recommendation or a meaningful change to an existing recommendation; do not repeat unchanged alerts.
- Preserve the current README layout, four-language presentation and existing desktop work.

## Historical source-only planning record

# Source-only connection improvements

No APK, release, version bump or signing change is included in this iteration.

## Implemented

- Selecting a path explicitly makes Connect use that path directly, without a collection scan. Selection is held in the current screen session; it does not depend on the asynchronous database active-profile update. A removed manual target does not silently fall back to another path.
- Selecting a collection enables the existing automatic connection workflow. Test collection remains a separate action. Testing the selected collection preserves the manual choice.
- Collection cards distinguish automatic, manually selected and currently connected paths. While connected, the summary never borrows another member's HTTPS measurement. Measurements are labelled as the last test, not a live speed measurement.
- Connection setup failures retain allowlisted categories without exposing raw exception messages or credentials. This improves diagnosis; it does not establish the cause of the reported SSH test failure.
- Update checks use this project's latest GitHub release. Numeric alpha/beta/rc revisions are compared correctly. Settings provides an explicit releases/downloads link. This opens the release page, not an automatic APK installer or signature-verification system.
- New UI labels have English, Persian, Simplified Chinese and Russian resources.

## Next: connection correctness and device validation

1. Reproduce manual SSH success versus collection-test failure on the same network. Check service teardown/startup ordering and stale callbacks before changing lifecycle code. Compare SSH and VLESS throughput using the same server, destination and sample size.
2. Use protocol-specific setup diagnostics, followed by a common tunnel HTTPS check with more than one controlled destination. A handshake alone is not proof of working Internet access. Keep latency, setup duration, throughput and stability separate.
3. Exercise manual selection, automatic selection, cancellation, screen-off, Wi-Fi/mobile transitions and app recreation on devices. Verify no stale test can disconnect a newer manual connection.
4. Complete routing consistency, translated failure messages, DNS progress/cancellation and redacted diagnostic export. Add the branded monochrome notification icon; evaluate launcher badges separately because launcher support varies.

## Subsequent app/panel work

- Subscription updates with stable server/account identifiers, atomic replacement, preservation of user edits and recovery on invalid downloads.
- Controlled failover within the selected collection, with DNS-fallback consent preserved, bounded attempts and no rapid switching loop.
- Network-local success history, optional bounded throughput tests, and explicit battery/data budgets.
- Per-user credentials and revocation on the panel; resource limits and concurrent-load tests before making capacity claims. Shared credentials must not be treated as isolated users.
- Signed update validation, protected release keys, published certificate fingerprint and a migration plan from the current signing setup. A published SHA-256 file alone does not authenticate an update.
- Store readiness, privacy documentation, complete localization and accessibility. Desktop/iOS ports are separate engineering and distribution projects, not an Android APK conversion.

## Validation

Eight JVM test entry points passed locally: import/export, collections, collection inference/undo, duplicates, bounded test phases, failure redaction, connection choice and release comparison. Real panel-fixture and device/native lifecycle tests were not run in this iteration.

Android `:app:compileFullDebugKotlin` succeeded against the prepared native dependencies. Resource XML parsing and duplicate-key checks passed for all four languages. This was compilation only: no APK assembly, release signing or publication. Push CI is deliberately skipped for this source-only commit to avoid the existing APK-building workflow.

## Follow-up after alpha07

- Removed the redundant Automatic selection menu item at the user's request; tapping the collection still selects automatic mode. This source change is not yet packaged in a new APK.
- User reports manual SSH opens sites/apps with good speed and StunTLS also opens sites/apps despite failed automatic tests. These are user field observations, not reproduced device tests. Investigate false-negative setup results and separate historical test errors from live connection status; do not assume server failure or persistent SSH slowness.

