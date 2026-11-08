# Veyra foundation — 0.1.0-alpha01

Based on SlipNet commit 2ed00fd0072ad0b558e30ec6b5e8b30948f98894.
An independent experimental fork; original LICENSE and upstream attribution remain.

## Import contract

| Input | Implemented behavior |
|---|---|
| veyra://, slipnet:// | Same versioned native payload; all existing profiles retained |
| veyra-enc://, slipnet-enc:// | Existing per-profile encryption; compatibility depends on native build encryption key |
| veyra-bundle-enc://, slipnet-bundle-enc:// | Password-protected bundle; both aliases accepted |
| vless:// | Existing Kotlin engine: WebSocket, TLS or none only; REALITY/XHTTP/flow rejected |
| ssh://user:password@host:port#name | Direct password SSH, no query parameters |
| socks5://[user:password@]host:port#name | Direct SOCKS5, no query parameters |
| naive+https://user:password@host:port#name | Direct NaiveProxy; full build required |
| VMess, Trojan, Hysteria2, WireGuard | Not implemented in this increment |

SSH, SOCKS5 and Naive are explicitly documented URI dialects, not a promise of universal subscription compatibility. Percent-encode credentials; IPv6 must use brackets. Plain multiline imports supported; remote subscription URLs and base64 subscription envelopes are not added. Import limits: 2 MiB and 1000 lines (these are not runtime/load limits).

VLESS uses system TLS trust plus HTTPS hostname verification. Self-signed origins will need a trusted certificate/front; no insecure bypass. The existing engine cannot reproduce browser fingerprints (`fp`) or Xray behavior. Standard links no longer enable fragmentation implicitly; `fragment` is explicit. VLESS here remains TCP-only. Do not advertise it as full V2Ray/Xray support.

## Independent app identity

Application ID: app.veyra.client (lite: app.veyra.client.lite); namespace stays app.slipnet for source compatibility. Launcher label/icon and release version changed. The vector icon is an initial implementation of the approved visual direction. Some inherited screen strings remain SlipNet and need a branding pass. No domain/trademark clearance is implied. Upstream automatic APK update checks are disabled until Veyra has its own release repository.

## Diagnostics and fixes

A copyable safe connection report records only elapsed times and fixed event codes in a 256-event RAM buffer. It accepts no profile names, endpoint addresses, credentials or error text. Available from the debug log sheet; raw debug logs remain separate and may contain private details. This is not crash persistence, ANR diagnosis, telemetry or a demonstrated fix for freezes.

The VLESS VPN branch now forwards the selected security mode, matching proxy-only mode. TLS certificate trust/hostname checking fixed in probe and actual connection paths. Probe success still does not prove authenticated end-to-end internet connectivity.

## Validation

Run `KOTLIN_JARS=/path/to/jars tools/veyra/run-jvm-tests.sh`.
Requires Kotlin compiler/stdlib/script-runtime 2.2.21, kotlin-reflect 1.6.10, kotlinx-coroutines-core-jvm 1.8.0 and annotations 13.0. JVM harness compiles the actual import/export/model sources. Android Base64, build flags and injection annotations are replaced with narrow test shims; JNI profile encryption deliberately throws and is not claimed tested. Real password bundle cryptography is tested.

Checks cover mixed imports, IPv6 and credential encoding, invalid ports/options, prefix aliases, native round-trip, encrypted bundle round-trip/wrong password, unsupported VLESS modes, input bounds and diagnostic buffer bounds.

APK validation is incomplete: `./gradlew :app:assembleFullDebug` stopped downloading Gradle with `Network is unreachable`. Android SDK/NDK setup and native submodules are also required. No APK, device test, throughput/load result, or censorship-resilience guarantee is included.

Next: complete Android build in a provisioned environment; test mixed-profile connect/disconnect and network changes on hardware; instrument service lifecycle/stall cases; integrate Xray without conflicting native Go runtimes. Test concurrent clients separately on the server; URI compatibility does not solve credential sharing or capacity limits.
