# Rovunet 0.3.0-alpha11 — connection repair candidate

Version code **17**, based on alpha10 commit `a032e3178811348ff4042b225528d93d529766bd`.

## Reproduction and scope

The alpha10 production connection manager was executed against the JVM Android adapter with a previous service that never acknowledges destruction. After 11 seconds, the UI was still Connecting and the next CONNECT had never been dispatched. This reproduces an unbounded lifecycle wait; it does not identify the exact physical-device trace from the report.

The same scenario on the repaired production manager ends in a specific Error after 10 seconds. The old ownership barrier stays closed, the expired request cannot start after a late destruction callback, and a later retry can recover. Repeated stop announcements for an already-destroyed session cannot create a fresh orphaned barrier. Destruction callbacks publish manager state on its Main scope.

## Connection behavior

- Connect starts the selected profile directly. Without an explicit profile, the selected collection's first suitable non-DNS path is chosen using existing ordering and cached measurements. No new ping or HTTPS scan is run as a prerequisite. Historical test failure does not prevent a manual connection.
- A DNS-only collection still asks for fallback consent. Accepting for Connect starts the configured DNS path directly. Explicit Ping/Test actions retain their own resolver discovery and HTTPS validation workflow.
- Manual connection bypasses the DNS pool pre-scan and uses the configured resolvers. Full pool verification belongs to explicit tests. Expiration, device restrictions, credentials and SSH/TLS identity verification still apply.
- The Android foreground notification starts before joins, native locks, cleanup and hostname resolution. A fresh service does not unconditionally stop/load every unrelated native backend. Previous ownership is still joined and serialized before replacement.
- The service's actual protocol/VPN-ready callback completes Connected directly, independent of probe results or a collector timing race. Startup has a session-scoped deadline (90 seconds for ordinary direct paths, 120 for DNS, 180 for Tor/unknown restored starts; chain timing follows the first layer). Expired starts cannot later become Connected.
- Traffic and protocol health monitoring remains after setup. Zero cumulative traffic on an idle phone is no longer a disconnect condition. Real backend failures and outgoing-without-incoming TUN stalls remain monitored. No single website is required for a normal connection.
- Settings' stability report now includes bounded, fixed connection stage events without addresses, credentials or profile names.

## Verification and limits

Production-manager regressions cover protocol readiness, unacknowledged destruction, late callbacks, canceled queued starts, timeout isolation, retry and an orphaned-stop regression. Choice tests cover direct selection without measurements, failed historical measurements and the DNS consent boundary. Watchdog tests cover expiration, cancellation, replacement and completed-attempt isolation. Existing JVM, real TLS/proxy, generated routing and four-protocol loopback coverage remains enabled.

The APK is built as Full Release with the existing signer and unchanged native engines. Physical phone/Android service scheduling and the reported network are not available here. This remains a device-validation candidate; no claim is made that every physical-device connection issue is resolved. Alpha10 localization and live language switching are retained. Deferred update-message wording and reset-button alignment are not part of this repair.
