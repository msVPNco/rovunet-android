# Validation scope — 0.1.6 Beta

Owner acceptance: Apple Silicon dev.6 functional base, reported 2026-11-11. Beta changes are version, installer and publication metadata only. The rebuilt Beta packages receive fresh extraction, digest, Mach-O, dependency, executable-mode, resource-seal and tamper checks. Host suites are rerun after the version bump. Native protocol leaf tests from dev.6 and earlier unchanged TUN/resolver tests are reused and labelled. Intel: limited testing; no physical Intel Mac execution. Hardware acceptance is not silently generalized to every protocol or every M-series model.

The records below describe the dev.6 base and are historical where their version is named.

# Validation scope — 0.1.0-dev.6

Run the macos/tests programs on Windows using the local, pinned native test fixtures. Machine-readable results with their actual counts accompany the delivery. UI runs use Avalonia's headless backend and real rendered controls; they do not exercise AppKit. DNS/Agent/network fixtures run only on loopback. Full default-bank count and filtering are offline UI checks.

Keychain tests cover a denied acquisition, no implicit retry, a subsequent explicit unlock, cache isolation and retaining encrypted files if their key is missing. The proxy test verifies that consecutive refreshes with unchanged network services do not take a privileged lock. DNS tests cover separate UDP/TCP proof, no fake tunnel from a DNS response, restored candidates, profile-specific history, cancelled tests, pool/global drafts and same-profile editing. A read-only native status request checks a pre-existing TUN service; the native code is unchanged.

Verify both extracted installers against the staged app, the PKG payload modes and BOM, arm64 engines, resource seals and exact TUN helper bytes. The system Installer UI, login-Keychain prompts, signing trust, AppKit and an actual M3 connection are not exercised on Windows. Developer ID and notarization are absent.

Advanced/Prism are exercised with the DNS and authenticated server operations concurrently, independent cancellation and separate measured latencies. End-to-end starts with an authenticated server check. A failed optional HTTPS target must not erase that check. The fixed confirm action is tested after scrolling a long form, including validation and saving to the originating profile or preference list.

The unchanged native resolver engine also passed 14 loopback cases in six Go test groups (DNSTT / NoizDNS with and without SSH, Soar, Prism, diagnostic timing and DNS response shapes). The Agent reports authenticated DNS sessions from the ready event and times HTTPS separately after local startup. The exact macOS binaries are packaged and structurally checked; the loopback suites ran on Windows.

Dev.6 adds checks of the generated PNG pixels, transparent background and outline, native template setting, actual tray updates through all four states, bootstrap status, persistent menu identity and late/cancelled connection progress. These run in Avalonia Headless on Windows.

Fresh dev.6 host suites: UI 968, DNS 21, Mac domain 71, proxy 23, protocol 89, network 21, feeds/banks 11, portable domain 239, all passing. UI includes dynamic profile menus, cancelling pending connects, local proxy port validation and Telegram links, first-use Slipstream approval/rejection/change/cancellation, and the three supplemental editors. Native AppKit and opening the Telegram app are not exercised here.

Six supplemental native leaf tests validate the private adapter's whitelist/TLS requirement and exchange actual encrypted data through AnyTLS, TUIC and Mieru servers on loopback. The 89 managed protocol checks also submit all three managed runtime configurations, including optional fields, to the compiled native adapter's --check mode. These tests exercise Windows builds of the same adapter; the packaged Darwin executable is architecture/seal checked.

Five Slipstream discovery leaf cases run under Linux/WSL: binary TXT framing, invalid input without dialing, cancellation without dialing, plus valid and expired certificates from the official v0.1.1 Slipstream server. The helper completes a verified-signature QUIC handshake but opens no application stream. This proves discovery interoperability, not target-Mac Slipstream data transfer. Temporary fixture private keys remain outside shipped source.

The earlier four TUN unit cases and fourteen resolver cases are reused evidence from dev.5 for unchanged source and binaries. VERIFICATION.json distinguishes them from the fresh dev.6 checks. All installer/DMG checks are host-side structural, extraction, hash, mode and tamper checks; Apple Installer and Apple codesign verification remain pending.


Final dev.6 host counts after selection and subscription work: UI 1000; Mac domain 123; DNS 21; proxy 23; protocol 89; network 21; feed/bank 17; portable domain 239. The automatic controller fixtures cover bounded candidates and collection isolation, actual-exit/three-failure decisions, transient failures, dwell/improvement thresholds, live-confirmation rollback, TUN-safe serial rollback, hidden-window behavior, network changes and manual ownership/cancellation. These are simulated sessions, not a native-Mac failover test.

The feed suite includes four parses of privately retained response bodies (two client headers for each of the two owner sources) and two fresh downloads through the actual SubscriptionClient: 11 VLESS paths and 1 Shadowsocks path, zero rejections. URLs, tokens, names and response bodies are not included in the test report. UI checks verify paste-to-subscription routing in all four languages. No connection to the supplied configuration servers was performed.

The final Iran language flag uses 35 native vector paths from the retained public-domain Lion and Sun SVG. Rendered artwork and quota/expiry labels are checked. Remaining quota is derived only when both usage and limit are supplied; missing fields stay unknown. Cancelling an established automatic monitor drains an in-flight framed probe without cancelling its live Agent; an explicit disconnect terminates that owner. Candidate cursors survive retries during the app session.


Intel packaging supplement: inspect test-results/dev6-intel. All 491 files of both extracted Intel installers match the staged x64 app. All 34 Mach-O files contain the target architecture; eleven engine hashes, six dependency hashes, Installer/BOM executable modes, development resource seals and four tamper cases are validated independently. Seven Go engine metadata records target darwin/amd64 with GOAMD64=v1. Three fresh Installer-plan unit tests pass. The original 1,544 dev.6 shared host checks are reused by matching application-source hashes; they are not Intel-macOS runtime tests. The Intel TUN client and embedded service package exactly match the earlier Intel dev.3 delivery.
