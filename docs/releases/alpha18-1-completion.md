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

## Validation record (to be finalized after CI)

Local JVM/parser/lifecycle regressions, real local encrypted TCP+UDP relays, and
AWG v1/v2/v3.1 userspace server/client tests are run before submission. Android
packaging, device/UI/storage and minified JNI gates must pass on the final commit.
Local fixtures are not proof of carrier reachability, censorship resistance,
physical-phone battery use, or repair of the owner's panel WG/Hy2 peer.

AnyTLS/TUIC and further protocols remain subsequent work after current protocol
compatibility. Existing reviewed theme, Slipstream and native stop changes are
retained. README design and desktop source layout must be preserved.
