# Next update after alpha17

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
