# Capability / evidence matrix

Windows x64 local0.1.2-preview.5. Exact current engine versions/hashes are in engines/*.json and final BUILD-PROVENANCE; two Mihomo helpers include upstream6780eeeb. The historical public0.1.1 matrix is not the local capability matrix. Only validated dialects/options are admitted; no protocol-wide boolean guarantees all providers.

The evidence column identifies the available scenario. Final delivered-binary reruns are reported separately in delivery/VERIFICATION.json. A previous successful test is not silently reassigned to new hashes. "Private UDP" means the authenticated helper association, not the app's public SOCKS endpoint. Public SOCKS UDP, TUN and kill switch remain absent.

| Family | Import / structured edit / core config | TCP evidence / limit | UDP evidence / limit | Recovery / identity evidence |
|---|---|---|---|---|
| VLESS WS/REALITY/XHTTP and validated TCP/gRPC/HTTPUpgrade | yes / yes / pinned Xray | controlled Windows WS HTTPS; prior owner remote use for selected paths | public local SOCKS TCP only | actual Agent stop/port release; TLS/unknown-option regressions |
| SSH / native TLS-SSH | yes / yes / scoped config | controlled encrypted SSH and chain; prior owner use | no public UDP | scoped host key and changed-key refusal; no TOFU bypass |
| SOCKS5 | yes / yes / yes | two/four-layer controlled TCP, exact hop count | no public UDP | failed outer layer does not fall back direct |
| Trojan/VMess | yes / yes / validated dialects | parser/runtime baseline; no new external-provider acceptance | no public UDP | unknown TLS/transport settings rejected |
| Shadowsocks incl three SS2022 methods and PSK identity chains | yes / yes / pinned engine config | existing config/roundtrip; provider acceptance remains untested | no public UDP | exact no-op/override retention, key-length rejection |
| NaiveProxy +/- SSH | yes / yes / pinned native | existing controlled and prior owner use; auth/TLS required | TCP only | owner-only transient; scoped SSH when combined |
| DNSTT/NoizDNS/VayDNS +/- SSH | yes / yes / family adapters | controlled resolver/SSH/HTTPS baseline and owner reports | family-specific DNS transport, no public SOCKS UDP | explicit TCP preserved; pool/global fallback isolated |
| Soar +/- SSH | yes / yes / separate session/resolver plan | existing controlled Go/Windows fixture baseline; owner reports | family-compatible DNS transport | cancellation and override/discovery fixtures |
| Slipstream +/- SSH | yes / yes / certificate-bound | prior controlled baseline + owner report after certificate supplied; timing not generalized | family-specific carrier | wrong/missing certificate refused, no expiry bypass |
| AmneziaWG1/2/3.1 | yes / yes / distinct helper | controlled encrypted Windows Agent -> Xray -> helper -> peer | authenticated helper TCP/UDP fixtures | cancel/stop/port release; private UDP scoped to TCP owner; external provider not claimed |
| AnyTLS/TUICv5 | yes / yes / distinct helper | real encrypted helper peer; Windows Agent negative unknown-root validation | authenticated helper private UDP | wrong credential/TLS/options; remote provider acceptance pending |
| Mieru TCP/UDP transport | yes / yes / single binding | controlled real Windows Agent/helper peer | authenticated helper private UDP | stop and auth ownership; external provider pending |
| Standalone DoH | yes / yes / verified resolver | controlled name resolution + TCP forwarding; not arbitrary traffic encryption | DNS HTTPS semantics | no system/plain DNS fallback for destination; reject unknown CA |
| Tor/Snowflake/bridges | yes / yes / pinned expert bundle | config/bootstrap/cancel tested; host external bootstrap failed150s in prior evidence | no general UDP | job/session cleanup; not labeled working Internet merely because bundled |
| Compatible chains2–4 | yes / yes / bounded layers | controlled hop count, no direct fallback, scoped SSH layer identity | transport compatibility restricted | reverse teardown; unknown nested/duplicate/forbidden layer combinations rejected |
| Hysteria2 / stock WireGuard | yes / yes / legacy Xray dialects | prior known external failure; not declared fixed | carrier is UDP, not verified general app UDP | earlier owner deferral conflicts with new W1.1; retained openly, no silent downgrade into AWG |

Additional acceptance gates: Program Files/UAC on a clean target, physical13–15inch/touch/mixedDPI/suspend/interface change, multi-hour sessions and CPU/RAM/GPU/power/performance p50/p95 across providers remain unmeasured. Unknown measurements are null. Current host Windows11 Education build26300 is the actual tested machine. Existing Windows10 compatibility checks are not a physical Windows10 test or a claim of vendor support for every old Windows10 edition; see the official .NET10 supportedOS list https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md .
