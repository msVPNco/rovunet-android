2026-11-11: Owner authorizes Windows 0.1.1 Beta as PRE-RELEASE, title Rovunet Windows 0.1.1 Beta · x64. Connected group/card promoted visually without mutating saved/manual ordering; fresh active HTTPS latency green and every30s, migrated on once with existing opt-out setting usable thereafter. No publicly trusted code-signing certificate found in Windows personal stores. Signing request discussed; public signing not fabricated, no paid/account enrollment authorized. AmneziaWG remains not integrated; official core feasibility only. Continue testing, package and publish this new release without rewriting old release/tag or Android Latest.

Historical:
0.1.1-preview.2: Default selects all 60370 addresses, separate inventory/test counts and country sampling preserved. Installer 520x680, launch on by default, cooperative target-owned previous-process exit via existing WPF SessionEnding and recovery; no force kill. Independent tests do not touch owner profiles. AmneziaWG support requested, feasibility investigation pending; no connection support claimed yet. Official references: https://github.com/amnezia-vpn/amneziawg-go and https://docs.amnezia.org/documentation/amnezia-wg/ .

Historical baseline:
## Final-source additional coverage

Portable domain suite now has 249 assertions, including native-envelope atomicity, unsupported inner-format admission, SS2022 key lengths/chains and edit preservation. Product UI now tests the saved-link page/menu and pinned-engine configuration acceptance for the three SS2022 methods, including 16/32-byte AES128 keys. These additions supersede earlier pre-package counts where noted. Actual final-payload counts and all-pass results must be read from delivery/VERIFICATION.json.

Primary format references: https://shadowsocks.org/doc/sip022.html and https://xtls.github.io/en/config/outbounds/shadowsocks.html . Rendering reference: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/tilebrush-overview . No unchanged native engine or remote-server success is inferred from these references.

# Local UI review validation · 0.1.1-preview.1

Current-source fixtures are distinct from the historical results below. The approved21 native WPF suite checks three palettes, narrow/wide views, all four language headers at multiple widths, physical flag centering, sidebar placement, fixed subcard dimensions, moving RTL/LTR letters, stationary short names, scroll collapse/stability, text-motion settings and pure monitor-fit calculations. Synthetic endpoints and simulated connection states are used for layout screenshots; they are not network evidence.

Regression suites: scanner20, product20, card20, trust-telemetry, inner-pages and general smoke. Product/trust suites include isolated local engine/configuration/counter behavior. Domain tests cover import, account values, protection and routing policy. Final packaged WindowsSmoke performs local HTTPS through the real agent/core; it does not test remote owner servers or change Windows proxy without its explicit extra flag. Setup preview and exercise cover the actual bundled installer with a separate test identity, install/upgrade/rollback/uninstall and Finish.

Exact pass counts, source revision, SHA256 and final payload reports are saved beside the private installer, outside the source tree. Failed development attempts remain in evidence and are not presented as passes. Actual Program Files/UAC, a physical touchscreen, mixed-monitor DPI movement and hardware sleep are outside this UI review's test scope. The unchanged native binaries are reused by their existing pins; no new native engine build is claimed.

Pre-package current-source run: approved21 190, scanner20 25, product20 42, card20 10, trust-telemetry 71, inner-pages 269 and smoke 101: **708 assertions passed**. Portable domain logic: **239 passed, 0 failed**. These are development-build results; final payload and setup results are recorded in the adjacent delivery verification file rather than embedded into a source snapshot before that snapshot is built.

Owner clarification about vertically stretched text: the repeat brush now maps equal-sized absolute rectangles with no stretching. A native raster assertion in both Persian and English compares glyph ink width, height, baseline and coverage between original and repeated text; diagnostic legacy-fill images are kept separately. This adds two assertions to approved21 (192) and brings the seven-suite final-payload assertion target to 710; delivered-byte results remain in the delivery report.

## Historical public Beta verification (not rerun claims)

# Windows beta testing

Reviewed preview20/final4 baseline, actual Windows automated assertions:

| Suite | Assertions |
| --- | ---: |
| scanner20 / product20 / card20 |25 /42 /10 |
| scanner19 / desktop18 / desktop17 |92 /228 /225 |
| flow / inner pages |52 /269 |
| trust-telemetry / extra protocol network / smoke |71 /57 /101 |
| busy-sleep / agent loss / recovery / replacement-sleep |9 /9 /9 /11 |
| suspend / cancel / pool lifecycle |13 /10 /21 |
| setup preview / isolated install-upgrade-rollback-uninstall |50 /30 |
| Local Windows total |1334 |
| Domain logic |239 |

Eight public-feed assertions passed across separate attempts, but each complete run contained an intermittent download failure. A single all-pass download run is not claimed. The pinned engine accepted144 configurations from two earlier feed snapshots; this proves configuration compatibility, not144 working servers.

Actual DNS tests used two resolvers in each of Soar,DNSTT,NoizDNS,VayDNS,Slipstream. Managed Simple/E2E completed and reached servers. Independent HTTPS succeeded in four families; Slipstream exceeded its20-second HTTPS deadline. The owner later reported working traffic on all tested paths except Hysteria2/WireGuard, including Slipstream. Private profiles/URLs/network reports are not published.

Installer exercises used a separate identity/directory and did not replace the normal installation. Program Files elevation and physical touch/DPI/suspend are not implied. Beta-specific compilation, version, regression, installer and source/hash verification are recorded separately. Baseline evidence is reused only with source equivalence; native engines are pinned reused binaries, not newly rebuilt. No code-signing certificate or Android private key is used.

Pinned-engine finding: this exact Xray build rejects 32-byte keys for AES128, despite broader current documentation about some Go implementations. The parser follows SIP022's 16-byte AES128 and 32-byte AES256/ChaCha requirements; oversized AES128 keys are rejected before launch. The failed candidate is retained in development evidence. Final product coverage has 48 assertions, approved21 has 192; final seven-suite target is 716, plus domain 249, setup 80 and local HTTPS transport verification.



