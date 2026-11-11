# Resolver finder comparison — dev.5

The Windows chat on 10 November calls for an independent DNS check and real server test in Advanced/Prism, visible reasons when the second check is unavailable, transport-specific evidence, and different Soar/Slipstream behavior. On 11 November it clarified the difference between the 60,370-entry Default bank and a test plan. The final Windows files reviewed include ResolverFinder, ScannerSession19, ScannerActions19, ScannerCheckpoint19, ResolverBankPicker, ResolverCatalog and ResolverControls20 in v0.1.1-beta/source, without changing them.

Mac provides offline search across the complete inventory, explicit source/target counts and bounded test plans; a bank entry is not a verified working server. Resume reuses frozen candidates and only missing transport results. The saved fingerprint belongs to the exact profile draft. Source selection never applies addresses automatically. Advanced, Prism and E2E expose a separate, initially unchecked Scan tunnel control. Stage one checks DNS capability, Prism proof, or an authenticated server session respectively. In Advanced/Prism, as soon as the first qualifying address has been checked on the requested wires, the operator can start the second test while DNS continues. The tunnel uses the same profile and only a passing transport. New eligible results can join a user-started bounded batch. The two stop controls, counters and timings are independent. The default criterion is an authenticated server session, never a local listening port. Full HTTPS is optional; failure of the destination does not erase a proven server session. Simple runs automatically; E2E starts at the server handshake without requiring a DNS capability pass. E2E plans contain at most 16 addresses; each manual server batch handles up to 16 individual address/transport pairs. Apply accepts up to 8 compatible selections (only one for DoH). Leaving the scanner cancels both tests. Confirming a chosen result can stop both phases, drain them, and apply to the original profile in a single action. The fixed header check mark saves the originating draft. Settings lists receive the checked addresses as a draft rather than editing a profile.


## Protocol sources and the corresponding editor fields

The profile type `sayedns` is labeled **NoizDNS** by Rovunet. No separate `TNoiseDNS` identifier occurs in the supported format. The public SlipNet project is the client reference, not the origin of every tunnel.

| Family | Profile-specific settings checked against the engine |
| --- | --- |
| [DNSTT](https://www.bamsoftware.com/software/dnstt/) and [NoizDNS](https://github.com/anonvector/noizdns) | Server key, resolver transport, authoritative flag, payload limit, fanout/round robin and spread. Slot 27 is authoritative; 38 is a separate NoizDNS-only option. |
| [VayDNS](https://github.com/net2share/vaydns) and [SlipNet's pinned derivative](https://github.com/anonvector/vaydns) | DNSTT compatibility, record type, QNAME length, requests per second, idle and keepalive seconds, UDP timeout in milliseconds, maximum data labels and client-ID bytes. DNSTT compatibility fixes the effective ID to 8 bytes; disabling it restores the previous editable value. UDP timeout is shown only with UDP. |
| [Soar](https://github.com/getlantern/soar) | Server identity, TCP policy, resolver discovery and source. The country is for built-in discovery; custom entries belong to the custom source. A single-candidate test disables discovery/fallback so that the route is attributable. |
| [Slipstream client used by SlipNet](https://github.com/anonvector/slipstream-rust) | QUIC session with a trusted server certificate, UDP resolver, keepalive, congestion and authoritative mode. No Prism or Noise public-key control. |

**Simple** automatically checks DNS and the profile server (Soar goes directly to the authenticated server once). **Advanced** records the separate NS, TXT, random A, long TXT, NXDOMAIN and EDNS diagnostics. **Prism** repeats the family-specific check on each transport up to its threshold; an ordinary public-key-derived Prism response is not an authenticated private-key session. Soar uses its own authenticated exchange. **E2E** starts the real client directly; there is no initial DNS compatibility gate. When selected, full HTTPS runs through that very session; Soar E2E uses full HTTPS and Soar's other modes use handshake. The manual control can repeat E2E; it is not a prerequisite for its first result.

## Prior comparison (dev.4)
# DNS reference comparison â€” 2026-11-11

The owner's Rovunet has evolved beyond its SlipNet foundation. Its pinned
Windows and Android implementations remain the product reference. SlipNet is
used to investigate original behavior, not to replace Rovunet or regress it.

Primary code inspected:
- Rovunet Windows 562a3d71670ceb7de909146bf68116d5cc614496:
  windows/src/Rovunet.Desktop/{DnsProfiles,ResolverFinder,ScannerSession19,
  ScannerCheckpoint19,ResolverControls20}.cs, Core/{ResolverScan,GlobalResolvers,
  DnsPool}.cs, and windows/transports/resolver_scan.go.
- Rovunet Android e6359aae2e2e19164ad7e9ec87267f09a0c1152d:
  app/src/main/java/app/slipnet/data/repository/ResolverScannerRepositoryImpl.kt
  and presentation/scanner/DnsScannerViewModel.kt.
- SlipNet 2ed00fd0072ad0b558e30ec6b5e8b30948f98894:
  https://github.com/anonvector/SlipNet/blob/2ed00fd0072ad0b558e30ec6b5e8b30948f98894/app/src/main/java/app/slipnet/presentation/scanner/DnsScannerViewModel.kt
- The Windows development chat and its local progress log were consulted read-only.
  Its prior work separates DNS proof, authenticated tunnel readiness and HTTPS,
  uses direct E2E for Simple Soar, preserves Stop/Resume/checkpoints and explicit
  TCP, and treats Local as the active network's DNS rather than 127.0.0.1.

Mac changes follow those behaviors. A DNS response cannot populate HTTPS proof;
E2E starts a separate full-profile Agent with no system proxy/TUN and no automatic
identity acceptance or direct retry. Advanced/Prism results can start/resume that
phase independently. Domain/profile fingerprints prevent stale results from
being applied to an edited profile. Global/pool overrides are optional and
connection-time only. Manual connection without scanning remains available.

Current Mac scope is a maximum of 2,048 candidates per batch and four E2E workers.
Windows large-bank paging/100,000 candidates and all advanced presentation
controls have not been claimed as complete Mac parity.

Related primary investigations:
- https://github.com/dmwyatt/granz/issues/100 â€” repeated Keychain prompts with
  build-specific ad-hoc identity. Mac mitigation is session caching/one GUI owner,
  not broadening the Keychain ACL; stable Developer ID is still pending.
- https://github.com/AvaloniaUI/Avalonia/issues/20297 â€” native menu command/enabled
  interaction. The Mac workaround binds explicit commands and tests CanExecute;
  it is not a claim that every version has the same upstream bug.
- https://wiki.metacubex.one/en/config/inbound/tun/ â€” TUN and DNS options.
- https://github.com/dmgbuild/dmgbuild/blob/f8511fd951cf251425aae2486ae5e235603083ef/src/dmgbuild/core.py
  â€” explicit icvl=icnv is needed in addition to icon view options for Finder.

Installer structure was also compared with deploymenttheory/go-macos-pkg pkg/flatpkg/build.go and pkg/cpio/writer.go: scripts-only packages omit Payload/Bom, Scripts is a gzip odc-cpio stored as an unencoded XAR member, and PackageInfo is zlib encoded. See https://github.com/deploymenttheory/go-macos-pkg/tree/main/pkg/flatpkg and https://github.com/deploymenttheory/go-macos-pkg/blob/main/pkg/cpio/writer.go . Actual Apple Installer acceptance is still pending.


dev.4 adds the same-profile editor return/apply flow, including unsaved fields
and override bookkeeping. The Local action reads active macOS DNS, and retains
a loopback address only if the system itself explicitly reports one. It does
not substitute 127.0.0.1 for an unknown resolver. Result focus and checkbox
selection are separate; failed results cannot be selected. The previous menu
identity replacement also violated Avalonia.Native 12.1.6's IAvnMenu.Update guard;
roots/submenus are now reused and populated before tray attachment.

Pinned upstream code read for these checks: [NoizDNS mobile client](https://github.com/anonvector/noizdns/blob/17ac5cf4e45cd09fa6f4bde09362e334dc4b8b55/mobile/mobile.go), [VayDNS client used by the reference](https://github.com/anonvector/vaydns/blob/45f18f16af40b6ad666e8119be08701b21395ee2/client/client.go), [DNSTT client](https://github.com/anonvector/dnstt/blob/51bdba3948d44b3175bc88ea40b2116bb9daff07/dnstt-client/lib/client.go), [Soar v0.2.4](https://github.com/getlantern/soar/tree/v0.2.4), and [Slipstream Rust client](https://github.com/anonvector/slipstream-rust/tree/60a5e38c7414b35e4c2fddf8d4d89a4341470c6a/crates/slipstream-client/src).
