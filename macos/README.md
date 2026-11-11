# Rovunet macOS — 0.1.6 Beta

Native Mac interface for Rovunet, with Apple Silicon arm64 and Intel x64 PKG/DMG packages. Requires **macOS 15 or newer**. Apple Silicon's dev.6 functional base was tested and accepted by the owner; **Intel: limited testing**. This Beta updates version and packaging metadata while retaining that functional base.

[Download 0.1.6 Beta](https://github.com/msVPNco/rovunet-app/releases/tag/macos-v0.1.6-beta) · [فارسی](docs/releases/beta016/RELEASE-FA.md) · [English](docs/releases/beta016/RELEASE-EN.md) · [Русский](docs/releases/beta016/RELEASE-RU.md) · [简体中文](docs/releases/beta016/RELEASE-ZH-CN.md)

## Install or update

Quit Rovunet with Command-Q, run the PKG for your processor, then open Rovunet from Applications. Alternatively, open the DMG and drag Rovunet to Applications. Keep existing profiles and Keychain items. An installed compatible TUN service of the same architecture is reused. First-time system-wide VPN setup is in Settings → Install / update TUN service → System-wide VPN (TUN).

M1/M2/M3/M4 and newer M-series Macs use **AppleSilicon-arm64**. Intel Macs use **Intel-x64**. Display size and Air/Pro do not need different packages.

## Features and scope

Compact 14-inch layout, four languages/themes, localized four-state menu-bar mark, subscriptions with usage/expiry, fixed save/apply actions, per-profile selection modes, DNS resolver discovery and separate real tunnel verification, system proxy, opt-in TUN and local proxy for compatible apps such as Telegram. SSH and Slipstream ask for first-use server identity approval. Manual connection is independent of scans and ping.

[Capability matrix](docs/CAPABILITIES.md) · [Validation scope](docs/TESTING.md) · [Remaining work](docs/BACKLOG.md). See the release's VERIFICATION.json and SHA256SUMS.txt for exact artifact evidence. Hardware owner acceptance covers the Apple Silicon functional base, not an exhaustive protocol or Intel hardware test.

## Build and corresponding source

Keep macos/ beside the unchanged Windows source at **562a3d71670ceb7de909146bf68116d5cc614496**: portable Core projects use explicit source links. The release source archive includes that exact dependency tree. Do not substitute the repository's newer Windows code without revalidating compatibility.

Tools: .NET SDK 10.0.403, Go 1.27.3, PowerShell 7, Python 3, Git. Avalonia 12.1.6 and ZXing 0.16.11 are locked. Native archives, sources and notices are pinned; all eleven engines and private dependencies are recorded per architecture. Native source is supplied separately with the release.

```powershell
./scripts/build.ps1 -Rid osx-arm64
./scripts/build.ps1 -Rid osx-x64
dotnet run --project tests/Rovunet.SharedRegression -c Release
dotnet run --project tests/Rovunet.UiTests -c Release -- artifacts/ui
```

All outputs remain under macos/artifacts. The PKG installs to /Applications without running the GUI as root. Profiles remain encrypted in the existing application store, with the key in the user's Keychain. Failed unlock does not replace saved data.

AGPL-3.0; upstream component licenses, source pins and attribution are retained. macOS releases use macos-v tags and do not replace repository-wide Latest.
