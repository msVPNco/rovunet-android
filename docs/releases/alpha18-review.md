# Android alpha18 — unpublished owner review

The owner authorized implementation, an isolated branch/PR, GitHub checks and a
review APK on 2026-11-10. No merge, release or public tag is authorized. Preserve
the alpha17 release bytes, the fixed Android package/signer, README presentation
and desktop source. Version code 37 is reserved for this review.

The pinned legacy `libslipstream.so` configuration codec is retained byte-for-byte.
The patched client is a separate `librovunet_slipstream.so` with unique JNI entry
points and SONAME; it exports no legacy key-retrieval function. Rebuilding a
client must not silently replace the encryption identity of old shared links.

## Review scope

- Sticky Hev session cancellation: a handshake timeout change cannot reactivate
  cancelled I/O. The native owner is still joined before reuse; Disconnecting is
  not hidden and the old native thread is never abandoned behind a new engine.
- Remove Patterniha's built-in catalog entry without deleting imported profiles.
- Four process-isolated Slipstream resolver workers, including Slipstream+SSH.
  A candidate cannot stop the live singleton; cancelled workers must die before
  their slot is reused. Probe processes do not initialize shared diagnostics or
  schedule subscription work.
- Optional exact public certificate pin for Slipstream, separate from SSH trust.
  The editor, stored profiles and v33 optional credential metadata preserve it.
  Room migration 49 to 50 adds only this public field. Old links without a pin
  retain their compatibility behavior; do not claim verified identity for them.
- Bounded QUIC readiness before announcing standalone Slipstream connection.
  This does not require a particular HTTP probe for a manual connection.
- Initial Chain support is deliberately a pair: directly reachable Xray outer
  transport followed by SSH. Core SOCKS credentials are per session; the final
  packet engine connects to SSH, not accidentally to the outer core. Reverse
  UDP-over-SSH and arbitrary Xray placements remain unsupported.
- Approved Soft Pop direction: soft raised lilac/mint controls, press-only spring
  feedback, gradual slate-navy-to-black background, taller translucent header and
  matching bottom panel. Light/dark appearances retain shared layout and explicit
  theme choice. Pastel Clay is deferred to later updates. No perpetual decorative
  animation or claimed LCD battery saving is introduced.

## Evidence so far

Local real-native loopback tests passed 25 stops: idle, silent SOCKS greeting,
partial CONNECT, active TCP data and UDP session setup. A deterministic test of
the production I/O yielder passes before/during cancellation and rejects a mutant
with the sticky guards removed. These strengthen shutdown behavior; they do not
establish that the exact physical-phone 45/120-second stall has been reproduced.

The initial CI verify stage passed. Android native build/toolchain issues are
being corrected and the complete current revision is not yet device-validated.
Required additional gates include four worker processes and cancellation, actual
DNS/QUIC byte transfer with matching pin, rejection of a mismatched pin, import
round trips, chain policy, existing VPN regressions and minified JNI tests.

## Physical-phone acceptance still required

Check rapid collection-test stop/start, manual disconnect, Slipstream and
Slipstream+SSH with working local resolvers, certificate mismatch, the new theme
in Persian RTL/English/light/dark/system mode, and a long screen-off connection.
Hysteria2/WireGuard network failures remain investigation items; local core
interoperability success is not proof they work on the affected mobile network.
Panel certificate export and installed client/server version matching require
coordination with the panel maintainer; this app PR does not modify the panel.
