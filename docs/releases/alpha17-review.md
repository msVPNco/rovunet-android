Owner accepted review03/code36 on 2026-11-09. The sections below are historical review-stage notes; current publication record: [alpha17](alpha17.md).

# Alpha17 owner review (not published)

Android 0.3.0-alpha17 / versionCode 35. Owner resumed Android development explicitly.
This branch must not be merged, tagged or released before acceptance of its signed APK.
README styling and desktop trees remain unchanged.

## Review scope

- Restore alpha15.4 Soar scanner scheduling and repeated handshake loop. BOTH again waits
  for both transports. Shared cancellation and current native resource cleanup remain.
  Automated local Soar fixtures cannot establish Iranian-network resolver availability.
- Language selection precedes navigation/import/permission UI on a fresh installation.
  Existing installations and explicit language/theme choices remain. Missing theme defaults
  to Follow system.
- Expanded collections use individual lazy rows. Public feeds accept up to 8 MiB and
  10,000 entries with explicit unsupported/duplicate counts. Private panel feeds retain
  their strict parser. Large collection tests rotate stable-ID batches of at most 50.
- Free connection sheet keeps the working Tor/Snowflake profile and offers personal
  Proton config import. Anonymous/Mahsa download actual public subscriptions into a
  collection after consent/review. Mahsa follows only its fixed allowlisted public child
  URLs; any failed child aborts the update. Generic public URLs never follow nested URLs.
  Updates remain explicit and never enable unattended imports. These entries
  are not an endorsement of relay operators or a blanket service-license clearance.
- Collection modes are opt-in: manual (default), replace on failure, smart selection.
  Manual connection never requires a successful ping. Smart selection tests at most five
  candidates per round. A healthy path changes only above 2000 ms HTTPS latency, with
  at least 50% lower median latency and a two-minute minimum stay. Three samples on the
  same reachable HTTPS destination are required; no throughput result is called ping.
- Independent candidate cores currently support Xray profiles only. Each owns its own
  private authenticated SOCKS endpoint, child process and cleanup. Parallel tests require
  explicit exclusion of this app UID from the active VPN and a non-VPN physical network.
  Other native singleton backends are tried sequentially only after failure. Reconnection
  can interrupt calls/downloads; this is not transparent stream migration.
- Screen-off healthy sessions do not run smart candidate scans. Wi-Fi locks require actual
  Wi-Fi and are not retained for screen-off modern Android. Cellular is not requested or
  disabled. Existing DNS continuity policy remains. Real battery savings require phone
  measurement; simultaneous radio indicators alone do not prove app-caused cellular use.

## Validation

CI includes portable JVM/parser/security tests, four-language checks, private Debug
instrumentation, minified JNI checks and a public unsigned Full artifact. Native inputs
are reused/rebuilt exactly as the existing pinned preparation workflow specifies; this
is not a complete native-engine rebuild. Sign the exact CI unsigned artifact offline
with the pinned production certificate, then deliver it to the owner. Public release
remains blocked on owner acceptance. Exact build/run/hash are supplied with delivery.

## Owner phone acceptance

1. In-place upgrade preserves profiles, chosen language, theme, signing and account data.
2. A clean install asks language first, follows system theme, then requests permissions.
3. Compare Soar Simple/Advanced/E2E/Prism against alpha15.4 on the same network/list.
4. Import a reviewed public source; inspect counts, unsupported links and large-list scroll.
5. Confirm manual choice remains manual; test failure recovery and high-latency switching.
6. Check an overnight idle session and Wi-Fi/mobile handoff using Android battery data.


## Review02: owner phone feedback

Code35 supersedes the code34 test APK; it is still not approved for public release.
Tor and Soar engines/scanner code are unchanged from the tested code34 revision.

- Opening a collection also selects that collection without connecting. Global and
  local connection actions resolve remembered choices inside its membership only.
- Only a debounced change of the selected physical route/address invalidates probes.
  Secondary network callbacks and duplicate availability events do not cancel tests.
- Fresh verified results sort ahead of history; ping chips show a measurement time
  and explicit fresh/previous text. Interrupted work never restores an old green chip.
- Collection budgets include fallback HTTPS attempts and teardown; the next batch
  advances only past completed results. Unsafe teardown/owner changes still stop work
  with the existing explicit reason rather than reusing native resources unsafely.
- Expanded path gaps are reduced while retaining individual lazy rows and colors.
- Domestic routing links to an explicit whole-app exclusion action. It preserves the
  selected app list and atomically selects DISALLOW/enabled only after confirmation.
  A real separate-UID fixture checks exclusion; no claim is made that every banking
  app accepts an active VPN. Android lockdown can prevent bypassed apps using internet.
- The provider catalog uses public link lists also published for proxy clients.
  No Hiddify engine code, WARP wrapper or blanket relay-ownership claim is included.
  The existing Xray engine now accepts standard SIP002/legacy Shadowsocks links with
  authenticated AEAD ciphers only, including editable secret-preserving roundtrips.
  Insecure ciphers, unsupported plugins and unverified TLS remain rejected.
  Intentionally empty published Mahsa children are accepted as empty, unlike failed
  downloads, which abort the update.
  Prototype fixtures check consent, deduplication, same-source refresh and no partial
  replacement on child download failure. Real provider availability is network-dependent.

Validation is pending on the review02 commit until its CI results are recorded with
its exact signed APK. README formatting and desktop sources remain untouched.


## Review03: broader imports and a predictable main stop control

Code36 is an unpublished owner-test successor to code35. The main control now always
stops/cancels the live session, independently of the selected card. Card-specific connect
still targets that card; browsing never changes an active connection. A device regression
checks stop from another selected card and a subsequent explicit connect.

The bounded teardown investigation found a definite reporting bug: releaseLocks recorded
screenOn=false regardless of the actual screen. It now samples PowerManager.isInteractive.
The reported 120-second Hev/native stop has not been reproduced or proved to be caused by
hardware, CPU sleep or the native join. No continuous wake lock or fake disconnected state
was added. Existing native ownership/cleanup barriers remain. Don't claim this fixes the
physical-phone stall; retain it as a targeted follow-up if it recurs.

The importer separates cosmetic URI fragments from endpoint/query validation. Existing
public source links are preserved for stable refresh identity. VLESS common no-op aliases,
verified-TLS Hy2, Trojan and AEAD VMess share the existing pinned Xray backend. TCP, WebSocket,
gRPC and HTTPUpgrade are supported where valid; XHTTP remains VLESS. Nonzero VMess alterId,
unsafe ciphers, disabled TLS verification, unknown effective options and unsupported
transports are rejected with a category. Manual connection still never depends on a probe.
Editor, native export, endpoint extraction and collection ranking cover the new formats.

Public previews show accepted/rejected counts per protocol and typed rejection reasons.
An all-rejected preview cannot be applied, create a source or replace existing profiles.
Public updates stay explicit. Proton's duplicate personal-import shortcut was removed from
the free catalog and replaced with Patterniha's public subscription after consent. Its
MIT-licensed repository documents provider-client-specific features; unsupported options
are reported rather than silently stripped. No WARP/security overlay, relay ownership
clearance or Iranian-network connectivity guarantee is implied.
Source: https://github.com/patterniha/Free-Configs (MIT; copyright 2026 patterniha).

JSON imports extract supported Xray/sing-box outbounds into the app's own profiles, with
an explicit review notice that file-level DNS, routing and listeners are not applied.
Other outbounds are reported as excluded. Custom trust files, proxy chaining, effective
unknown headers and unsupported options are rejected instead of weakened. This is a
connection converter, not an arbitrary external-config runtime. Private panel v1 JSON
still uses its existing strict envelope validation. There is no automatic execution of
scripts or plugins from any import.

Validation: pending exact-source CI and owner review delivery. Verify includes the portable
interop/security corpus and actual TCP/UDP loopback for Trojan, VMess/WS, VLESS/gRPC and
VLESS/HTTPUpgrade in addition to the five existing protocol modes. Device tests include
new-core smoke, main-stop scope and all-rejected-preview protection. Preserve all README
layout and desktop trees; leave Tor/Soar native and scanner behavior unchanged.
