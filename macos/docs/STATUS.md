# Status — 0.1.6 Beta

Public prerelease preparation authorized by the owner on 2026-11-11. The owner accepted the Apple Silicon dev.6 functional base on a real Mac. This release changes app/installer versions and publication metadata only; it does not claim a second hardware run of the rebuilt bytes. Intel has limited testing: cross-host suites and independent package/architecture checks, no physical Intel Mac test. Both targets require macOS 15+. Windows/Android application sources remain unchanged. Same-architecture dev.3 native TUN clients and service packages remain byte-identical.

The following dev.6 records are retained as historical implementation and validation scope.

# Status — 0.1.0-dev.6 / Apple Silicon and Intel

This candidate targets the 14-inch M3 Pro on macOS 15 or newer. Windows and Android were read-only. Installer identity, Keychain service/account, encrypted files and the dev.3 native TUN client/service remain compatible. PKG installs the GUI in /Applications without running it as root.

The Windows chat “ویندوز 1” (10 November, additional bank clarification 11 November) and final Windows v0.1.1-beta scanner source were compared. DNS evidence stays attached to its address, wire and originating profile; persistence and return destinations are covered by fixtures. The offline default inventory has 60,370 entries. A single native Mac test plan remains bounded to 2,048 candidates. HTTPS proof remains distinct from server readiness. No internet-wide sweep or external tunnel was used for acceptance.

Existing login keys are never replaced or re-permissioned on read. Normal startup requests neither interactive Keychain access nor privileged proxy recovery. If access is unavailable, the profile store stays locked and the owner can initiate one unlock attempt. Service installation, actual proxy changes and the user’s native security choices remain under macOS control. First-party reference: https://developer.apple.com/documentation/technotes/tn3137-on-mac-keychains

Installer, Keychain dialogs, localized AppKit menu and real routed traffic still need acceptance on this exact M3 package. No Developer ID or notarization. See TESTING.md and DEV6-FA.md.

Forms and settings expose a fixed check mark with the original validation. A running DNS search and the optional server test can overlap in Advanced/Prism. Server reachability and optional HTTPS are separately measured and reported.

The owner reports Tor, the menu-bar icon/menu and DNS working in dev.5. Dev.6 adds a full-color status mark (white disconnected, yellow connecting/bootstrap, green connected, red connection error), a contrast outline, and protection against late progress from a superseded/cancelled attempt. The exact new AppKit rendering still requires a target-Mac check.

AnyTLS, TUIC and single-binding Mieru (`mierus://`) now have strict import, subscription/bundle roundtrip, forms and real runtime routing through a separately pinned Mihomo adapter. This follows Android SupplementalCore 5e66f167c85aecac26008d930a7a24df6f2a31f2. All three passed encrypted data exchanges on loopback with actual protocol servers on Windows. Full `mieru://` documents and multi-binding profiles remain explicitly unsupported, matching the referenced Android subset. The auxiliary engine and Slipstream discovery helper are fresh Darwin arm64 builds; the nine earlier native engines are reused unchanged.

Slipstream automatically discovers a server's public certificate through its own DNS resolver and asks for first-use fingerprint approval. It opens no data stream before approval; the normal client remains pinned with --cert. Stored identities are scoped to the tunnel domain, survive profile duplication and are not silently replaced. SSH already discovers its host key and asks for approval. The profile menu now refreshes Connect/Disconnect/Cancel using the selected profile's actual state.

The Mac UI adds local proxy mode for Telegram, Firefox and other proxy-capable apps, persistent loopback ports and a Telegram configuration link. Saving this mode turns off system proxy and TUN; app proxy settings control which apps use the connection. It does not enforce process routing for apps without their own proxy support. Existing Mac styling is preserved.


The single Connection selection parent in both card menus contains Manual, Switch on failure and Smart, persisted per collection. Its controller follows Android SmartPathPolicy and SmartConnectionController at the supplemental pin: at most five candidates, three-sample median, 30-second visible monitoring, three-failure recovery, 2-second slow threshold, 120-second minimum stay and at least 2x improvement. Manual commands cancel the owner. Unknown SSH/Slipstream identities require prior manual approval. Candidates cannot cross collections or physical-network stamps. Where independent physical probes are unavailable (TUN or shared WG peers), one bounded serial trial per minimum-stay interval restores the old path if improvement is not confirmed. These lifecycle decisions are tested using owned-session fixtures; native Mac switching remains pending.

Single HTTPS subscription input now routes from ordinary paste/file/QR import to subscription review, following Android MainViewModel. Requests accept JSON/plain text, preserve verified HTTPS and bounded redirects, read safe profile-title metadata and discard cancelled/stale previews. Both owner-provided subscriptions were fetched with the actual client on Windows and admitted 11 and 1 profiles with zero rejections. Tokens and raw bodies are outside shipped source/evidence.

The artifact is named AppleSilicon-arm64 to describe its generic ARM64 architecture, including M2 compatibility with macOS 15+. The M3 is the primary acceptance target; no M2 execution has been performed.


پرچم زبان فارسی با نشان شیر و خورشید جایگزین شد. کارت اشتراک، حجم مصرف‌شده/کل، باقی‌مانده و تاریخ انقضای اعلام‌شده را نمایش می‌دهد؛ اتمام حجم یا زمان بر پایهٔ گزارش پنل مشخص می‌شود. اطلاعات نامشخص، نامحدود فرض نمی‌شود. این اطلاعات گزارش پنل است و شمارندهٔ دستگاه جای آن را نمی‌گیرد.


The release preparation now includes native osx-x64 PKG/DMG alongside the frozen osx-arm64 installers. All application .cs/.csproj/locale JSON sources match the frozen dev.6 source manifest. Intel adds fresh AMD64 v1 builds of the same supplemental adapter and Slipstream discovery helper; the earlier nine engines and same-architecture dev.3 TUN client/service remain unchanged. The Installer generator selects arm64 or x86_64 explicitly and refuses unknown RIDs. Both complete bundles require macOS 15 because their bundled OpenSSL libraries carry that deployment minimum.

Each architecture independently passed complete extraction/hash comparisons, Mach-O/dependency/mode checks, development-resource seal checks and four tamper cases. Three new Installer plan tests cover both architectures and unknown input. Shared dev.6 Windows/Linux runtime and UI evidence is reused unchanged, not rerun on an Intel Mac. Developer ID, notarization and physical Mac acceptance remain pending. The combined release directory is a local prerelease draft; no GitHub mutation was made.
