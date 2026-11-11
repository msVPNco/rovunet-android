# Remaining work — 0.1.6 Beta

Apple Silicon functional base accepted by the owner. Intel remains limited-test Beta: collect native install/upgrade, Keychain, menu, DNS and routed-traffic feedback on a physical Intel Mac. Continue protocol-specific tests with actual servers, older/other Apple Silicon models and network changes. Full Mieru documents/multiple bindings, arbitrary chains and process-enforced per-app routing remain outside this release. Distribution identity work is tracked separately; this release makes no Developer ID/notarization claim.

Historical dev.6 follow-up checklist (owner acceptance above supersedes its general acceptance-pending wording):

# Remaining work — dev.6

1. On the M3 Pro, install over the previous build and exercise Allow / Always Allow / Deny, two complete quit/relaunch cycles, and locked login keychain. Verify old profiles remain decryptable.
2. From the same profile and from each DNS settings list, stop/resume a known resolver test, inspect DNS/server/HTTPS separately and apply. Confirm DNS preference priority on the next connection.
3. Verify native menu language, installed TUN detection and system routing/restoration. Test abandoned proxy recovery from its explicit button, including administrator cancellation.
4. Developer ID application/installer signing and Apple notarization are outstanding. Both architectures are packaged; native Intel acceptance is still pending. Full-bank network sweeps and unbounded CIDR expansion are not part of this version.

Verify the four-color menu-bar icon on the M3 in light/dark menu bars, including Tor bootstrap, cancellation and a failed connection followed by a successful one. The owner already reports the dev.5 menu and DNS behaving correctly.

Exercise the fresh AnyTLS, TUIC and Mieru adapter with the owner's real servers on M3, including TUN. Verify Slipstream first-use fingerprint approval, relaunch with a saved certificate and explicit review after certificate rotation. Verify Telegram's macOS URL handler and local-only proxy mode on the weak connection. Full Mieru documents/multiple bindings, arbitrary chains and process-enforced per-app routing are separate unfinished capabilities.


Verify the card's Connection selection submenu and its persisted mode on M3. Exercise explicit failure recovery, a cancelled candidate and restoration after a rejected Smart switch, including TUN. Confirm both supplied subscription links through the Mac's own network and execute their imported profiles. The current host evidence proves fetch/import and simulated automatic ownership, not target-Mac routing or live switching.


Before treating this as a stable public release, exercise each exact PKG on its native Mac architecture: install/upgrade, Keychain reopen, first-use identity review, same-profile DNS application, native menu and TUN connect/disconnect/restoration. Intel should also exercise the x64 supplemental engines. Developer ID application/Installer signatures and notarization remain outstanding. Older macOS 14/13 builds are not included; the present bundled OpenSSL requires macOS 15. A local prerelease manifest and four-language release notes accompany the combined artifacts.
