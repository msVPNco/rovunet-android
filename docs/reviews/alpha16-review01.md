# Alpha16 review01 — awaiting phone review

VersionCode 32; the public package and production certificate remain fixed.
This branch is a review candidate. CI success does not authorize publication or
merging the release. The accepted APK must be published unchanged after approval.

## Changes

- Soar Prism stops at the configured passing threshold and reports the number
  actually attempted. UDP/TCP proof arrives independently; a slow second path no
  longer hides a successful first path. BOTH and explicit TCP-only settings stay.
- Resolver lists use fixed worker pools. Native Soar clients share eight slots;
  DNS sockets share 64. Advanced capability checks share one resolver deadline;
  an unfinished diagnostic is displayed as incomplete. UDP cancellation wakes an
  event-driven selector; resource cleanup runs on IO instead of closing a socket
  synchronously in the cancellation handler. TCP cancellation closes its owned socket.
- Unchanged traffic counters no longer cause repeated disk writes. Screen-off
  idle sampling backs off to 30/60 seconds; DNS and explicit cover-traffic timing
  retain CPU wake protection. Idle non-DNS transports may sleep after a minute.
- VLESS WebSocket Ping receives a masked Pong, resource ownership survives late
  workers, and bridge stop closes tracked sockets and executors. Closing the
  remote direction also closes the local client; padding is cancelled on teardown.
- Unbound upstream sockets declare Android's default underlying network. Only
  changes to the selected physical default or its properties request recovery.
  This declaration does not force either radio off.
- Panel v1.19 status is separate from configuration feeds and shared by a
  collection. Its quota and expiry are advisory; manual connection stays available.
  A foreground refresh uses bounded downloads and stops when home is not visible.
  Cache identity follows the URL, revision and account period. A 404 or an old,
  incomplete sample is unavailable information, not an expired account.
- Existing collection surfaces remain. Status text and a thin quota bar use amber
  at <=20% or <=3 days, red at <=5% or <=24 hours, and gray for untrusted metadata.
  Language and help precede subscriptions in the main overflow menu.

## Verification boundaries

Local portable/JVM checks pass 83 JUnit cases, plus parser and lifecycle checks.
Four-language resource parity and the Kotlin safety ratchet pass. Android build,
Room migration, Compose, real loopback Soar and cancellation checks are required
before delivering an APK; their final results and artifact identity belong in the
review delivery record. Device tests use a disposable emulator, not a phone or an
Iranian network. Native provenance must distinguish pinned reused transports
from freshly compiled project cores. No battery percentage saving is established.

## Phone acceptance

1. Upgrade over alpha15.4 without uninstalling; check saved profiles, manual TCP
   choices, subscriptions, language and the existing card appearance.
2. On the same resolver list/network, compare start-to-first-result and total time
   in Soar Simple, Prism, Advanced and E2E; test a TCP-only resolver and stop/restart.
3. Switch Wi-Fi/mobile while both are enabled. Check actual data passage and the
   safe report's DEFAULT_NETWORK. A mobile indicator alone is not proof of VPN use.
4. Leave VLESS connected overnight with the screen off; separately test a DNS
   transport. Compare battery settings and safe report UTC/counter sample times,
   wake-lock durations, reconnections and next-morning traffic. Screen-off and
   ordinary notification callbacks do not measure battery energy themselves.
5. Import volume-limited, time-only and unlimited test accounts. Check shared
   remaining allowance, warning colors, renewal, stopped-account status, metadata
   404 and return from the background. Missing metadata must not prevent connect.
