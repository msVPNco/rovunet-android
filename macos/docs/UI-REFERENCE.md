# Windows reference and Mac comparison checklist

Current correction and acceptance scope: [dev.3 notes](DEV3-FA.md). The owner reported dev.1 launch and own-profile connection; the new native system-proxy path remains unvalidated on a Mac.

The source of truth is the released Windows tree, not an Android redesign:
`PortraitShell.cs`, `PortraitCards.cs`, `HeroPanel.cs`, `Appearance18.xaml`,
`DesktopStyles.xaml`, `PageNavigation.cs`, `Locales/`, plus `Branding.cs`,
`StickyHero19.cs`, `SettingsPanel.cs`, `Tools.cs`, and `MenuPages.cs`.

The published Windows executable was copied to the Mac reference area and run
only with its synthetic `--ui-portrait-preview` mode. Its screenshots contain
simulated connection states, never evidence of network traffic. The original
Windows source/build directories were not modified. This reference run produced
five actual WPF screenshots. Its six failed checks all concern the existing RTL
brand-corner assertion; this is recorded, not presented as a passing Windows suite.

## Shell and navigation contract

- Wide threshold: 920 logical pixels; sidebar: 260.
- Owner's 14-inch Mac: default window 560 × 760 logical pixels, minimum
  480 × 560. Clamp to the screen's available work area after accounting for
  display scaling. Remember the normal window size on macOS.
- Windows home header: brand in compact mode, Home in wide mode; four flags,
  search, settings and general menu. Header logo is the geometric turquoise R
  plus `ovunet`, copied from `Branding.cs`, not the tray shield artwork.
- Sidebar order: Home; General caption; Settings; Connection details;
  Manage collections; Backup & export; Safe diagnostics; Tools caption;
  DNS Scanner. Footer: Help, About, version.
- General menu: Settings, Connection details, Manage collections,
  Backup & export, Safe diagnostics, Exit.
- Add flyout: link/file, subscription, manual profile, public sources.
- Hero: expanded 148, collapsed 88; compensate lost height in the scroll
  content and shrink the power ring; keep the status strip visible.
- Collection: one collection with paths, its own metadata, provider quota,
  test progress/results, independent expand/test/connect/menu actions.
- Path: name, host, protocol, timestamp/result, edit, more and delete. First
  selection is separate from connecting an already-selected path. Explicit
  connect buttons do not open the editor. Export respects sharing restrictions.
- Path cards remain 60 pixels high. Long names, hosts, protocol labels, result
  values, collection names and hero/status descriptions use a clipped single
  line. An overflowing value waits 1.25 seconds, then moves at 26 logical pixels
  per second with a second copy for a continuous train. Short values remain
  stationary. Hover pauses, leaving resumes; a tooltip exposes the complete text.
  Text is remeasured after content/size changes. Persian starts at the right;
  technical values retain LTR. No responsive second result row is added.
- Collection menu: connect, paths, test, stability, stop, path selection,
  sorting submenu, TCP check, update for subscriptions, rename/move,
  export/QR, delete. Path menu preserves connection/manage/transfer sections.
- Internal navigation uses a back stack; Escape returns, preserving page
  controls. macOS native menus, Command shortcuts, Dock identity, file picker,
  clipboard and tray use Avalonia's native APIs and need actual Mac acceptance.

## Screen inventory

| Screen / state | Required reference | Development disposition |
|---|---|---|
| Home narrow/wide, empty, selected, connected, connecting/error | Windows hero, cards, fixed footer | Actual Avalonia controls; 4 languages × 3 themes × 4 sizes rendered; connected states still need Mac comparison |
| Add flyout and general menu | Same entries/order | Flyout and page menu implemented; native popup placement pending Mac validation |
| Search | Name/protocol filtering | Implemented; selection/connect distinction preserved |
| Settings | Connection, DNS, probes, appearance, diagnostics, recovery | Same section order; unsupported macOS controls explicit; full equivalence not complete |
| Connection details | Proxies, active route/protocol, probe/traffic | Implemented basic details; graph, all history and duration layout pending |
| Import link/file/clipboard/QR | Review before commit | Implemented workflow; original tab layout and QR clipboard-image path still require adaptation |
| Subscription preview/update | Update on same collection card | Implemented strict import/merge; panel status contract not complete |
| Manual protocol chooser | Windows Web/TCP, DNS, UDP sections and protocol order | Implemented |
| VLESS advanced editor | Existing encryption/cs/fm/transports/WS heartbeat/mKCP fields | Real shared form logic; UI visibility and raw editor adapted |
| VMess/Trojan/SS, Hy2, WG editors | Existing fields/options | Forms adapted; Mac network acceptance pending |
| SSH/native/DNS advanced editor | Existing security and DNS options | Partial dedicated controls; raw options preserved; full SSH TLS/key/WS editor pending |
| Collection management | Tests, sort, regroup/undo, duplicates | Implemented baseline; test progress and automatic collection choice need further parity work |
| Backup, encrypted export, QR sharing | Confirm secrets; retain locks | Implemented |
| DNS scanner and results | Simple/advanced/E2E/Prism, stop/resume, multi-select | Core/worker paths adapted; complete E2E/banks/filter UX pending |
| SSH trust and Slipstream certificate | Explicit fingerprint/certificate | Trust dialog and certificate picker written; native Mac approval untested; Slipstream engine withheld |
| Help/About/diagnostics | Existing menu locations, safe output | Implemented development metadata; final help copy pending parity |

## Appearance and GPU

The three owner images define light, blue-dark and black theme references.
Windows Soft Pop tokens, radii, source logo, green connection state and purple
add action are retained. The owner-requested black background uses true black.
Vazirmatn is the exact bundled Persian font. Segoe UI cannot be assumed on a Mac;
Helvetica Neue/PingFang are explicit native fallbacks, not redistributed Windows
fonts. Addresses/technical values stay left-to-right within Persian UI.

Avalonia is configured with Metal first on macOS, then OpenGL/software fallback.
The Windows development host reports an NVIDIA GeForce RTX 4060. Headless Skia
screenshots do **not** measure that GPU or establish Metal frame rate, power use,
Retina quality or macOS animation smoothness. Native GPU acceptance is pending.

Remaining visual work includes pixel/spacing comparison of every internal page,
the complete raised/glass button templates, touch/trackpad behavior, reduced
motion/high-contrast behavior, focus restoration and remaining long-value layouts
outside the compact cards. The
current UI is a working port in progress, not an approved exact visual match.

The marquee shares one UI clock, measures text only when needed and moves render
transforms rather than relaying out cards on each frame. Offscreen/hidden values
do not advance. This describes implementation, not a measured Mac GPU benchmark.

Dev.2 uses shared mutable theme brushes for retained editor/pages, semantic button resources for hover/pressed/disabled colors, and resized minimum power-button dimensions. Six bank choices, compact scanner options and readable/copyable diagnostics are covered by headless UI tests. Native Mac smoothness remains unmeasured.

## dev.3 DNS and Finder corrections

Family-specific DNS fields, encrypted credential editing, explicit transport labels and separate settings/results views preserve the reference behavior. Result controls include filter/sort, multi-selection, export, pool/global settings and independent HTTPS proof. The DMG sets icvl=icnv and iconSize=128. Native menu commands are explicit and tested in all four locale models. These are cross-host checks; actual Finder dragging and macOS menu rendering remain pending.
