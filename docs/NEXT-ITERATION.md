# Next update after alpha17

- Owner request, 2026-11-09: move Patterniha beside Anonymous and Mahsa in the provider group; do not change the owner-accepted alpha17 APK for this cosmetic adjustment.
- Investigate the inherited intermittent subscription error-display UI test timeout without hiding failures.
- If the physical-phone 120-second Hev stop recurs, use its redacted report; do not claim the screen-state reporting fix resolved it.

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
