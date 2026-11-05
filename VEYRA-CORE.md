# Rovunet — core protocol integration

Current release: [alpha11](docs/releases/alpha11.md). This document retains the original integration history; current install, test and release behavior is documented in [the user guide](docs/USER_GUIDE.md) and [build notes](docs/BUILDING.md). Native export is now v33; references to v31 below describe the initial integration.

Base: SlipNet 2.5.6 Full, commit 2ed00fd0072ad0b558e30ec6b5e8b30948f98894.
Includes the earlier Veyra foundation. This file supersedes the earlier foundation's
statements that REALITY, XHTTP, Hysteria2 and WireGuard have no implementation.

## Implemented

- VLESS RAW/TCP + REALITY (including Vision); XHTTP with TLS, REALITY or no TLS.
- Hysteria2 links (`hysteria2://` and `hy2://`), TLS and optional Salamander.
- Standard WireGuard `[Interface]` / `[Peer]` text, one peer, unique device keys,
  addresses, allowed IPs, DNS, MTU and persistent keepalive. Uses Xray's user-space
  WireGuard implementation: no root or second Android VPN/TUN is required.
- Existing DNS, SSH, Naive, SOCKS5 and custom VLESS WS engines remain present.
- Standard links and WireGuard files round-trip through native Veyra v31 bundles.
  Room database migration 44→45 adds `coreLink`, keeping previous profile fields.
- Xray is one separate executable process, not a second Go runtime loaded by JNI.
  One active core, one loopback SOCKS port, one existing Android VPN/TUN.
- Standard SOCKS5 UDP ASSOCIATE for core profiles; legacy custom FWD_UDP remains
  unchanged for older engines. Existing QUIC-disable preference still applies.
- Connect, disconnect, network-change restart and cleanup dispatch include Xray.
- Configuration passes over stdin, not command arguments or log text. Output is
  drained without exposing raw core logs. Fixed diagnostic stage events only.
- A small native supervisor terminates the core on app-process death and propagates
  stop signals. It does not tie core lifetime to a Java worker thread.

Core: Xray 26.10.31, pinned 63ca2e0c1d31b89f67700c33842431f541fedd25, unchanged source.
Official source: https://github.com/XTLS/Xray-core
Core license and source attribution are packaged in app assets.

## Deliberate limits

As of alpha04, the New Profile menu has REALITY, XHTTP, Hysteria2 and WireGuard tiles. Structured forms create and edit core profiles and validate them through CoreLink before saving. Existing core profiles retain their identity and metadata. Opening REALITY/XHTTP from the legacy VLESS WebSocket editor saves a separate profile, preserving the original and any chains that reference it. The server must independently support the selected protocol. Xray is not enabled as a chain layer.
Separate XHTTP downloadSettings, Hysteria port-hopping/pinning/insecure TLS options,
multi-peer WireGuard, wg-quick scripts and unspecified URI dialects are rejected,
not silently translated. `wireguard://` dialects are not supported; use standard
WireGuard configuration text. WireGuard is not AmneziaWG.

New core listener is loopback-only. Proxy-only LAN sharing is not added for core
profiles. Remote subscription fetching and automatic switching between profiles
are not added here. Sharing one WireGuard private key across devices is not made
safe by this app; issue one profile per device. The manager now has `core-plan`
for offline generation of server configs and unique device profiles; it does not
install or supervise new live server services yet.

## Build using verified upstream native assets

Linux: JDK21, SDK platform36, build-tools35, NDK29.0.14206865, Git, Python3 and Go
with automatic toolchain download. Set JAVA_HOME, ANDROID_HOME and optionally
ANDROID_NDK_HOME / GO. Then:

    python3 tools/veyra/prepare-native.py
    ./gradlew :app:assembleFullDebug -PveyraPrebuiltNative=true

The script verifies the SHA256 of the exact upstream Full universal APK, reuses
its existing native libraries, compiles the checked-in Java bindings, and builds
Xray + the supervisor for arm64-v8a, armeabi-v7a and x86_64. The sources.jar in the
upstream checkout omits SoarClient.setDiscoveryCandidates; the script restores
that declaration from the existing Go method and verified release JNI symbol.
No old native engine is substituted with a stub or removed.

The build's `veyraPrebuiltNative` switch skips rebuilding the unchanged upstream
Rust/C engines. Source-native builds additionally require every original source tree, including the currently unavailable pinned dnstt-mobile revision; a complete independent native rebuild is not claimed.
The upstream x86_64 Full release lacks the optional Naive/Tor binaries; ARM32 and ARM64 contain them. This inherited limitation remains; 32-bit x86 is excluded.
The Full AAR and native binaries are generated; do not manually copy the old
`golibs.aar` as the Full AAR (its bindings are incomplete).

## Validation scope

Automated checks and measured loopback results are in `tools/veyra/results/`.
The integration harness uses the actual Kotlin URI→Xray JSON converter and two
real Xray processes with a local HTTP server and UDP echo service. Test-only TLS
roots are explicitly trusted; certificate verification is not disabled. REALITY
uses a concurrent local Go TLS1.3 target. These are not independent server-core
interoperability tests or measurements of Iranian/Chinese/Russian networks.

Test commands:

    XRAY=/path/to/xray KOTLIN_JARS=/path/to/jars CORE_LAUNCHER=/path/to/launcher \
      CORE_TEST_JAR=/tmp/core-tests.jar tools/veyra/run-jvm-tests.sh
    go build -o /tmp/tls-target tools/veyra/tls_target.go
    TLS_TARGET=/tmp/tls-target XRAY=/path/to/xray KOTLIN_JARS=/path/to/jars \
      CORE_TEST_JAR=/tmp/core-tests.jar python3 tools/veyra/integration.py
    # For independent WireGuard devices:
    PROTOCOLS=wireguard WG_DEVICES=16 TLS_TARGET=/tmp/tls-target XRAY=/path/to/xray \
      KOTLIN_JARS=/path/to/jars CORE_TEST_JAR=/tmp/core-tests.jar \
      python3 tools/veyra/integration.py
    python3 tools/veyra/test-launcher.py
    python3 tools/veyra/test-migration.py

The JVM harness replaces Android Base64/build flags/injection with test shims;
JNI profile encryption is deliberately excluded. The real password bundle
cryptography, parsers and profile exporters are exercised.

No Android device or emulator was connected (`adb devices` empty). Device smoke
instrumentation source is included in CoreDeviceSmokeTest; run it with an attached
device via `connectedFullDebugAndroidTest -PveyraPrebuiltNative=true`.
A successful APK build is not proof of device runtime stability. Battery use,
background restrictions, Wi-Fi/mobile handoff, prolonged sessions and DPI
resilience remain unverified. No live server deployment was performed.


Alpha05 adds HTTPS-over-SOCKS verification via the actual service, three samples, explicit TCP-only diagnostics, measured ranking and path guidance. See VEYRA-NEXT-STEPS.txt for measurement semantics, limitations, reproducible verification scope and desktop/publication plans.
