# Architecture and dependency audit

Current correction and acceptance scope: [dev.5 notes](DEV5-FA.md) and [scanner references](DNS-REFERENCE.md). The owner reported dev.1 launch and own-profile connection; the new native system-proxy path remains unvalidated on a Mac.

Version: 0.1.0-dev.4; checked 2026-11-11.

| Provenance role | Exact commit |
|---|---|
| Windows installer source | 562a3d71670ceb7de909146bf68116d5cc614496 |
| Windows publication | 723d68abbd9288a73fac583b3a2ee22e7fd8de74 |
| Android APK source / code 39 | e6359aae2e2e19164ad7e9ec87267f09a0c1152d |
| Android archive/publication | f5f362a610244361438ec5e7091917a5c27ceb7d |

A publication commit is not a fresh rebuild of the application or native engines.

## Existing Windows boundaries

| Project | Actual dependencies | macOS disposition |
|---|---|---|
| Rovunet.Core | net10.0; BCL JSON, crypto, HTTP/TLS, sockets, DNS; no WPF/DPAPI/Registry imports | Compile the same source through a separate Mac project; all obj/bin stay under macos |
| Rovunet.Platform | net10.0-windows; Windows ACL, DPAPI, WinINET/Registry, Job Object and private transient config | Separate Mac platform code; never reference the Windows platform project |
| Rovunet.Agent | Windows executable paths/pins, process ownership, proxy lease, HTTPS probes and Xray stats | Adapt reviewed Runtime to architecture-specific Darwin engines and framed anonymous stdio |
| Rovunet.Desktop | WPF, WinForms tray, Win32 pickers, Dispatcher, templates and generated controls | Real Avalonia controls; shared Windows locale JSON, Vazirmatn, portable subscription/public-feed code |

Core contains Windows engine-pin constants, but the Mac launcher does not use them.
Each Mac engine is verified against its own architecture/hash manifest. A signed
build must refresh hashes after signing engines and sign the outer app last.

## Mac process/data boundaries

Desktop (C# / Avalonia) -> framed anonymous stdio -> Agent (C#)
-> unprivileged Darwin supervisor (Go) -> Xray / Naive / transport helper.
Desktop uses a separately owned native worker for bounded resolver scans.
Profiles/subscriptions are an AES-GCM document; the random encryption key is in
macOS Keychain. Failed Keychain access or a corrupted store blocks subsequent saves.

The UI and managed Agent are unprivileged. Optional TUN delegates only bounded native control to an installed root launchd helper; see native/tun/README.md. System Proxy uses a separate encrypted-journal guardian. Each engine gets
its own process group; the supervisor observes parent death and terminates the
group. Darwin lifecycle, parent death, network changes and sleep/wake still need
physical Mac validation. Credentials enter via stdin or 0600 temporary files, not
argv/logs. Naive configs are removed after startup; process-stamped abandoned
configs are cleaned on the next use. TLS checks and scoped SSH trust are retained.

## Dependencies and supported target floor

| Dependency | Pin / evidence | Boundary |
|---|---|---|
| .NET | SDK 10.0.403, runtime 10.0.14 | Supported target macOS 15+; no Mac execution accepted |
| Avalonia | 12.1.6, package locks | net10.0; native backend; Metal first, OpenGL/software fallback; no Mac workload |
| ZXing.Net | 0.16.11 | QR via Skia; no System.Drawing dependency |
| Xray | 26.10.31; a4a3dff5b20c2faae567b742c172c7b95e75c083 | Both Darwin binaries inspected; minimum 13.0; system libraries/frameworks |
| NaiveProxy | v154.0.8037.49-5 | Both Darwin binaries inspected; minimum 13.0; system libraries/frameworks |
| Transport helper and supervisor | Go 1.27.3, CGO disabled, vendored source | Both Darwin targets compiled; execution pending |
| Slipstream | v0.1.1; c7b265c11dd13d6dfd601bc81d54d61ee9bdde23 | Both Darwin clients bundled with private OpenSSL 3.5.9 |
| AmneziaWG | Mihomo 3f27ef39f8c96bbfbc8b0b9bb10f5c9ef60149d8; released Android adapter | CGO-free independent gateway, arm64/x64, corresponding source supplied |
| Tor/lyrebird | Official Tor expert bundle 16.0.1 | Both Darwin architectures; original geoip/bridge defaults/licenses |

Upstream Slipstream absolute Homebrew OpenSSL paths are relocated to @loader_path.
Pinned private libssl/libcrypto are bundled and hashed. Both OpenSSL bottles target
macOS 15. Re-signing follows load-command relocation. Tor uses its bundled libevent.

The supported application floor is macOS 15, considering current .NET 10 support,
not just the lowest engine load command. .NET metadata lists 15/26/27 and records
14 out of support. All published native library architectures/dependencies/minimum
versions are inspected again before packaging.

The owner's target is a 14-inch November 2023 MacBook Pro, Apple M3 Pro, 18 GB RAM,
macOS 27.0.1, identified from the owner's image. No serial number is recorded.
This Mac is not connected to the build host. Intel acceptance remains separate.

## System networking phase

Dev.2 adds an optional HTTP/HTTPS/SOCKS system proxy through SCPreferencesCreateWithAuthorization. An independent ordinary-user Agent guardian holds the macOS authorization reference and encrypted per-service journal, observes EOF/owner/engine death, and restores only still-owned channel groups. It follows newly available services without reasserting over user edits. Startup/manual recovery handles abandoned journals. The UI option is enabled for private Mac acceptance; fake-backend tests are not native validation. TUN/UDP routing remains separate.

For device-wide VPN, research favors a small native packet-tunnel system extension
while preserving C# UI/Core/Agent. Apple TN3134 restricts a Mac packet-tunnel app
extension to App Store distribution; direct Developer ID distribution uses a
system extension. Entitlements, provider loading, packaging, IPC and actual routing
remain unimplemented. A privileged helper alternative needs a narrow authenticated
API, owned leases and recovery. No unrestricted root shell or per-app routing
promise is included; some managed per-app configurations require MDM.

## Primary references

- [Pinned Windows source](https://github.com/msVPNco/rovunet-app/tree/562a3d71670ceb7de909146bf68116d5cc614496/windows)
- [Windows release](https://github.com/msVPNco/rovunet-app/releases/tag/windows-v0.1.0-beta)
- [Android release](https://github.com/msVPNco/rovunet-app/releases/tag/v0.3.0-alpha18.1)
- [Avalonia macOS backend](https://docs.avaloniaui.net/docs/platform-specific-guides/macos)
- [Avalonia package](https://www.nuget.org/packages/Avalonia/12.1.6)
- [.NET 10 OS support](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
- [Apple TN3134](https://developer.apple.com/documentation/technotes/tn3134-network-extension-provider-deployment)
- [Avalonia distribution](https://docs.avaloniaui.net/docs/deployment/macos)


## Mac dialect and packaging

MacProtocols, MacCoreOptions and MacAmnezia extend the pinned Core without editing
Windows. MacProfileEditing preserves unchanged wire text, credentials and source
identity and uses conservative three-way subscription merges. MacExternalConfigs
accepts bounded outbound schemas only and reports JSON_OUTBOUND_ONLY. Global
routing/DNS/inbounds are not silently installed. Encrypted sharing retains the
released AES-GCM format and its one-URI-per-line constraint.

Tor bootstrap is separate from Xray/local readiness and has a three-minute bound.
The bridge list is bounded and cannot inject Tor directives. Logs are drained and
not exposed. Standalone DoH protects DNS only and uses direct data, clearly labeled.

DMGs are HFS+ UDIF created by the pinned go-apfs-v2 library from an explicit tree,
including Unix native executable modes and an /Applications symlink. Finder layout
and its code-drawn background are generated locally. rcodesign ad-hoc signatures
and complete managed resource seals are integrity-checked; Apple verification and
notarization are separate. Tool/source pins are in packaging/tool-pins.json.

## dev.3 storage and native service

The GUI owns a session-cached Keychain key. Agent/ProxyGuardian receive copies through anonymous pipes; only the GUI calls Security.framework for login-Keychain storage. Secret wire buffers and key copies are cleared. The TUN helper never receives this key or a profile. Its signed native client is verified by console UID, kernel PID and hardened CodeDirectory hash. The managed Wire DTO is a Mac-only copy with TUN/key IPC fields; the rest of Core remains linked to the pinned Windows source. Import limits remain 2 MiB; an encrypted state document can hold up to 16 MiB of scan results. See DNS-REFERENCE.md for source comparison and TESTING.md for scope.


## dev.4 correction boundaries

Only M3/arm64 is repackaged here. Managed code is rebuilt; native dev.3 engines
are reused and the exact TUN client/policy bytes remain service-compatible.
Legacy Keychain consent uses public SecAccess/SecTrustedApplication APIs with
exactly the current GUI; a nonsecret per-build/path receipt avoids repeat ACL
writes. All NativeMenu roots/submenus remain stable. Scanner editor-return saves
the existing profile, including its subscription override, instead of cloning it.
The optional app Installer is payload-only, root:wheel, fixed /Applications and
arm64/macOS 15+. It never launches the GUI as root or contains private data.
