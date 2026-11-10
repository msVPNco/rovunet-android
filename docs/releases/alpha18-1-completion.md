# Android alpha18.1 / code39 release completion

Owner authorized implementation and publication on 2026-11-10. This supersedes the
previous review-only restriction for this iteration; it does not assert a new
physical-phone test pass. Keep code38's delivered review APK unchanged.

- Card Update now prepares/applies the saved subscription in place, shows progress
  and a localized outcome, and rejects repeated clicks. Native ownership/update
  safety remains: updating existing profiles requires a disconnected session.
  Locally edited and retired profiles are preserved; partial imports are reported.
- Collection names are optional. Explicit names and saved names win, then a safe
  provider title, then the hostname. Private URL paths/tokens are never fallback names.
- `subscription-userinfo` volume/expiry is cached in the same successful update
  transaction. Both directions count; `total=0` is treated as the provider's
  unlimited convention. Missing fields remain unknown. The UI labels provider
  snapshots and freshness; this is not independently audited metering. No new
  polling job, wake lock or periodic full-subscription fetch is introduced.
- VLESS/Trojan/VMess preserve verified-TLS `cs` and the supported `fm` schema.
  Embedded scripts and unsupported mask/options remain explicit rejections.
  AEAD Shadowsocks 2022 validates key sizes; Hy2 supports URI port lists/hopping.
- AmneziaWG `.conf` and the supported WireGuard URI dialect preserve AWG options,
  including version-3 header protection. An isolated stdin-only GPL-3.0 Mihomo
  adapter at `3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8` runs only for AWG profiles.
  It uses the existing lifecycle supervisor and authenticated loopback proxy;
  imported scripts, general Mihomo YAML, remote providers/controllers and kernel
  TUN configuration are not executed. One peer and one address per IP family are
  supported; unsupported multi-peer/ranged-keepalive fields are not discarded.
- Android / desktop updater compatibility keeps the version name alpha18.1 and
  increments code38 to39, avoiding a version-name rollback for owner test devices.

## Validation record

Final application source `e6359aae2e2e19164ad7e9ec87267f09a0c1152d`, tree
`3e890261420f7bbc87bf55cc324f240caffdc490`. Push CI 42905223390 and independent
PR CI 42905231496 passed the required verify and Android-device jobs: 144 JVM,
Gradle unit tests, 18 real local encrypted TCP/UDP modes (200 TCP requests each,
concurrency 40), 73 Debug emulator cases and two minified Release JNI cases.
AWG 1/2/3.1 fixtures exchange actual encrypted TCP/UDP and verify shutdown; the
Android case verifies packaged authenticated endpoint ownership/cancellation.
The public Full APK is assembled unsigned by that CI and signed offline with the
fixed production key; exact artifact, APK hashes and certificate are in
[provenance](alpha18-1-provenance.json). Public copy records contain only headers,
padding and signatures; all payload bytes are checked against the CI input.

The first Hy2 hopping fixture used independent QUIC connection-ID tables and
could fail after a port change; it now uses shared-listener UDP fan-in and checks
an actual remote-port change. A busy-dialog test had an inverted enabled-state
assertion; disabled controls are now required. Theme snapshots complete onboarding
with a real click in a separate live window and still require persisted completion;
the original short-timeout cause was not independently established. No coverage
was removed and skipped device results are not accepted as success.

Local fixtures/emulators are not proof of carrier reachability, censorship
resistance, physical-phone battery use, or repair of the owner's panel WG/Hy2 peer.
No new physical-phone acceptance is claimed. AnyTLS/TUIC/Mieru remain subsequent
work after existing compatibility and field validation. README design, historical
records and desktop source layout are preserved. Full/light explicit source ZIPs
include available pinned expanded sources plus Xray/Mihomo; the unavailable old
dnstt-mobile tree remains disclosed rather than silently substituted.
