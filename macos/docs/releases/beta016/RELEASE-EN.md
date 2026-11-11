# Rovunet macOS 0.1.6 Beta

**Pre-release · macOS 15+ · Apple Silicon arm64 / Intel x64**

Apple Silicon's functional base was tested and accepted by the owner on a real Mac. **Intel: limited testing** (host and package checks; no physical Intel Mac run). This release retains the dev.6 features and updates the application and installer versions to 0.1.6 Beta.

- Compact Mac interface with three themes and four languages, fixed save/apply actions, scrolling overflow text and a localized menu-bar mark: white disconnected, yellow connecting, green connected, red error.
- Profile-specific DNS discovery: Simple, Advanced, E2E and Prism; DNS response, authenticated server reachability and optional HTTPS remain separate. A second tunnel test can run alongside discovery; selected results apply directly to the originating configuration.
- Optional system-wide TUN, system proxy and local proxy for apps such as Telegram. Existing compatible same-architecture TUN installations are reused.
- SSH and Slipstream discover server identity and ask for first-use trust. Manual connection does not require ping or a scan.
- HTTPS subscriptions and imports, reported traffic/expiry, per-collection Manual / Switch on failure / Smart modes; AnyTLS, TUIC and single-binding Mieru in addition to the existing protocol families.

## Choose a package

M1, M2, M3, M4 and newer M-series Macs: **AppleSilicon-arm64**. Intel Macs: **Intel-x64**. Both require macOS 15 or newer; Air/Pro and screen size do not require different packages. PKG is recommended for installation; DMG is an alternative.

## Install or update

1. Quit the old Rovunet with Command-Q. Run the PKG for your processor, then open Rovunet from Applications. Alternatively, mount the DMG, drag Rovunet to Applications and eject the disk.
2. Keep your existing profiles and Keychain items. If profiles are locked, use the app's explicit Unlock action.
3. For first-time system-wide VPN, open Settings, install the TUN service, then enable System-wide VPN (TUN). A compatible installed service does not need reinstalling. Installation and actual network setting changes may request the Mac administrator's approval.
4. To apply DNS results, open Find DNS from that profile's editor, select the results and use the fixed check mark at the top; save the form with the top check mark or Command-S.

Full Mieru documents/multiple bindings, arbitrary chains and process-enforced routing for apps without proxy support are not included. There is no promise that every protocol works on every network. SHA256SUMS.txt, VERIFICATION.json and corresponding source archives record exact artifacts and test scope.
