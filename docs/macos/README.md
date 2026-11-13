[English](README.md) · [فارسی](README.fa.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md)

[Download 0.1.7-beta](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.7-beta) · [Frozen source](https://github.com/msVPNco/rovunet-app/tree/macos-v0.1.7-beta/macos) · [Main project](../../README.md)

# Rovunet macOS 0.1.7-beta

**Local Preview · Pre-release · internal build 0.1.7.14 · macOS 15+**

This limited preview has positive Apple Silicon UI and local HTTP/SOCKS proxy evidence. **Intel has NOT been tested on physical Intel hardware.** Global TUN, kill switch and recovery remain experimental. The public version follows 0.1.6-beta; the internal build is still 0.1.7.14.

## Download

| Mac | Installer and checksum | Test scope |
| --- | --- | --- |
| Apple Silicon · arm64 | [PKG](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-AppleSilicon-arm64.pkg) · [SHA-256](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/SHA256SUMS.txt) | UI and local proxy tested on an Apple Silicon Mac; global TUN acceptance outstanding |
| Intel · x64 | [PKG · NOT-TESTED](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-Intel-x64-NOT-TESTED.pkg) · [SHA-256](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/SHA256SUMS-Intel.txt) | **Not tested on a physical Intel Mac** |

Choose arm64 for M-series Macs, x64 for Intel Macs. Air/Pro and screen size do not require different installers. This release provides PKG installers; no DMG asset is supplied.

Both app/helper signatures are local **ad hoc**. The PKGs have **no Developer ID Installer signature or Apple notarization**. Do not disable Gatekeeper or weaken system-wide security settings.

## Features in this build

- Compact Mac interface, three themes and four languages; visible save/apply/back actions on internal pages.
- Collection controls and progress/outcome feedback; direct manual connection remains independent of ping and DNS scans.
- Active-path latency feedback and scrolling overflow text scheduled for visible application content.
- HTTPS subscriptions and imports; provider-reported usage/expiry remain distinct from independently verified traffic.
- SSH/Slipstream first-use identity trust, optional local/system proxy and experimental opt-in TUN.

## Install, update or return to an earlier version

1. Download the PKG matching your processor and verify it with the linked checksum file.
2. Disconnect Rovunet normally, quit it with Command-Q, then install over the existing app and open it from Applications. Administrator approval can be needed for installation/helper setup or system network changes.
3. Preserve application data, subscriptions and Keychain entries. If locked, use the explicit Unlock action. Keep the previous installer for rollback; disconnect and quit normally before reinstalling it, without clearing data.

TUN is opt-in. Use the matching bundled dev.4 helper when needed; an old dev.3 helper alone does not establish compatibility or acceptance of this build. Local proxy testing does not require enabling TUN.

## Validation and open issues

The Apple Silicon build has 491 installed-file checks and 2,163 scoped assertions across UI, feed, networking, domain, protocol and proxy suites. Agent tests on the owner's Mac included a Canada HY2 full-body HTTP/SOCKS transfer on direct Wi-Fi and a separate isolated packet DNS/HTTPS test. These are agent test results, not a new owner confirmation or acceptance of global TUN.

Intel was cross-built from the same frozen application source. All 34 Mach-O files include x86_64; local signatures/resources and exact extraction of 492 packaged files passed integrity checks. **Intel installation, launch, UI, networking and TUN on physical hardware are untested.** [Intel verification](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/INTEL-VERIFICATION.json).

VLESS WS TLS still has a user-reported failure under investigation. The subsequent bootstrap fix is **not included** in this preview. DNS-based transports are not universally verified; ordinary WireGuard, global-TUN recovery/kill-switch acceptance, private panel status v2 compatibility and long-term energy work remain open. Full Mieru documents/multiple bindings, arbitrary chains and process-enforced per-app exclusion are not promised.

## Source and release record

[Source ZIP](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-source.zip) · [Release notes and hashes](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.7-beta) · [Optional patch from internal 0.1.7.13](https://github.com/msVPNco/rovunet-app/releases/download/macos-v0.1.7-beta/Rovunet-macOS-0.1.7-beta-from-local-0.1.7.13.patch)

Frozen application source: `ab577440f17193fd43cfbe761f63e2d2bffdacd8`. Public beta numbering and internal build numbering are separate. GitHub Latest is repository-wide; always use the macOS-tagged release. Never post private configuration links or credentials in an issue.
