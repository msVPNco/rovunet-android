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
  Prototype fixtures check consent, deduplication, same-source refresh and no partial
  replacement on child download failure. Real provider availability is network-dependent.

Validation is pending on the review02 commit until its CI results are recorded with
its exact signed APK. README formatting and desktop sources remain untouched.
