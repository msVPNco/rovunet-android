# Alpha08 repair verification

Base: `144df0e5dd61ba70afde74c98987de62bc8cfcee` on `main`.

| Audit issue | Repair | Verification |
|---|---|---|
| 1. Stale service callbacks | Request identities; stamped repository events; scoped callbacks | Real manager regression with deterministic Android adapters |
| 2. Overlapping setup/teardown | Join canceled setup/runtime groups and teardown; process-wide lifecycle mutex | Manager stop barrier regression; Android compilation; device stress test pending |
| 3. Unverified SSH/TLS peers | Explicit SSH/self-signed approvals, changed-key rejection, CA and hostname verification | Real trust repository and TLS sockets against local certificates |
| 4. Unbounded TLS handshake | Shared absolute setup deadline; live streams retain normal idle behavior | Deliberately stalled local TLS peer |
| 5. Chain restriction bypass | Check expiration/device binding for every member before native startup | Chain expiration regression; service compilation |
| 6. Unauthenticated HTTP sharing | HTTP Basic challenge and fail-closed partial credentials | Real authenticated SOCKS upstream and HTTP CONNECT clients |
| 7. Edit data loss | Preserve SSH host, creation/connection times and listener settings | Source inspection and Android compilation; pin/order already worked |
| 8. Stale manual selection | Clear on import/regroup/undo/deletion | Selection regression and source inspection |
| 9. Credential delimiter corruption | v33 Base64 JSON credential extension | Actual exporter/importer roundtrips including pipe-only and Unicode secrets |
| 10. Naive URI credentials | Percent-encode credentials and bracket IPv6 | Source inspection and compilation |
| 11. Resolver visibility loss | Preserve hidden-resolver policy during export/re-export | Actual exporter/importer regression; native lock encryption still needs device |
| 12. Incomplete domestic routing | Shared direct TCP relay and native core rules | CIDR/domain/relay tests; generated rules executed on real Xray |
| 13. Non-atomic activation | Room transactions | KSP/Android compilation; device database concurrency pending |
| 14. Deleting chain members | Validate chain references and deletion in Room transactions | Source inspection and KSP compilation |
| 15. UI-thread import/crypto | Worker dispatcher, main-thread result publication, cancellation | Android compilation; physical-device responsiveness pending |
| 16. Historical errors look live | Separate timestamped last-test error below the title | Source/resource review and compilation; device layout pending |
| 17. Single HTTPS target | Verified alternate 204 host within existing phase budgets | Existing TLS/HTTP probe tests; target availability varies by network |
| 18. Leaked HTTP connections | Track/close both sides; epoch isolation across restart | Real idle CONNECT relay sockets and handler shutdown |
| 19. Malformed lock hashes crash | Strict format guard and constant-time comparison | Real hash parser against malformed/oversized inputs |
| 20. Stale result cache after edit | Invalidate by effective connection settings | Cache regression: edits invalidate, UI metadata changes preserve |

The regression harness executes production Kotlin classes with small Android adapters. It is not an Android emulator test. Native core tests use the repository's pinned Xray commit and local endpoints. A four-protocol loopback run covers REALITY, XHTTP, Hysteria2 and WireGuard TCP/UDP. Application compilation and APK verification are recorded with the packaged release assets.

No physical phone, operator network or live server from a user's configuration is accessed by these tests. No success is claimed for GitHub Actions until a completed run is observed.
