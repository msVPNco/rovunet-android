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