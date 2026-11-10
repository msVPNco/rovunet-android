# Android alpha18.1 — unpublished follow-up

The owner authorized implementation on 2026-11-10 after testing alpha18/code37.
The earlier observation hold is superseded. Keep README presentation and desktop
sources unchanged. This follow-up reserves versionCode 38 for an installable,
production-signed owner APK. No merge, tag or release before explicit acceptance.

## Changes requiring review

- A common physical-route resolver distinguishes an active VPN from its radio.
  Public physical-default information, the documented compatibility network type
  and a still-valid remembered route prevent temporary VPN creation from looking
  like radio loss. Ambiguous new routes are not guessed. Both collection testing
  and smart mode use the same resolver; service callbacks ignore secondary radios.
- Collection interruption keys survive localization instead of becoming a generic
  operation failure. Redacted typed diagnostics record starts, interruptions and
  physical-route changes. Actual native teardown ownership still gates the next
  test: no pretend success or abandoned Hev owner.
- A shared, serialized active-path monitor performs one small verified HTTPS
  request per roughly 30-second visible cycle (one fallback target on failure).
  It stops with the screen's resumed lifecycle and adds no wake lock. Collection
  tests suspend it. Manual selection remains independent of measurement success.
  The active result replaces stale measurements and displays sample age.
- Smart and failover modes share these samples instead of duplicating probes.
  Three spaced poor cycles trigger review; a spike, duplicate cached read or old
  evidence after idle does not. Optional improvement still requires a slow current
  median, at least halved latency and a 120-second elective dwell. Candidate cores
  remain private/independent, at most five and one at a time, with a 45-second
  discovery budget and per-session app-UID bypass permission. A separate process
  alone is not permission to bypass a live VPN. Non-Xray independent candidate
  tests remain unsupported; a genuinely failed path can use a bounded serialized
  recovery batch. Failed replacements can restore the prior path once. No
  recursive unlimited retry loop is used.
- The already-published canonical update fix from alpha16.1 remains. New errors
  distinguish network, APK integrity, signing identity and storage; download
  metadata bypasses caches. Returning from Android's install-source permission
  screen resumes the installer with OS confirmation. Old alpha15.3 binaries still
  reject renamed URLs and require one manual upgrade; new code cannot patch an
  already-installed old updater remotely.
- AMOLED uses black gaps, dark uses a slate-navy gradient, and light uses pearl
  surfaces/dark text. Child cards have soft elevation rather than black trays;
  selection/connection uses a lighter desaturated surface with explicit status.
  Header/footer remain mostly opaque floating slate-lilac, and connected mint is
  restrained. The list can scroll beneath the header without hiding initial rows.
- Hysteria2/WireGuard startup markers and bounded, typed Xray runtime failure
  categories improve diagnosis without exporting raw engine output. A ready
  SOCKS listener is not a successful UDP handshake or proof of Internet access.
  No claim is made that the owner's physical-network failures are fixed.

## Validation and remaining evidence

Local JVM policy, import/export, lifecycle and core-configuration regressions run;
full GitHub verify and Android device/minified suites must pass on the final source.
New policy tests cover VPN-created route hiding, secondary radios, actual handover,
offline state, spaced failure/slow evidence, idle resets and canonical updates from
alpha15.3. Device coverage checks interruption messages in all four languages and
captures real themed home screens. Prior local checks did not run real carrier
network traffic, physical battery measurements or Android installation upgrades.

Physical acceptance must include repeated 20-path collection tests, manual-mode
invariance, fresh latency/stale failure display, actual Wi-Fi/mobile handover,
bounded smart recovery/cancellation, installation permissions, all three themes,
and lower-end-device smoothness. Preserve the passing alpha18 Slipstream behavior
and measure four-worker costs; do not advertise a fourfold speed increase.

Inherited upstream Rust formatting/Clippy failures documented for alpha18 remain
outside this Kotlin/UI follow-up. Native versions, the preserved legacy encrypted
configuration codec and the production signing identity are unchanged.
