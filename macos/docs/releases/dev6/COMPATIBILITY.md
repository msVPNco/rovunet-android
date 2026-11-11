# Rovunet macOS 0.1.0-dev.6 compatibility

| Package | Processor | Mac families | Required OS |
|---|---|---|---|
| AppleSilicon-arm64 | Apple Silicon, including M1/M2/M3 and Pro/Max/Ultra variants | Compatible MacBook Air/Pro, iMac, Mac mini, Mac Studio and Mac Pro | macOS 15 or later |
| Intel-x64 | Intel x86_64 | Intel MacBook Air/Pro, iMac/iMac Pro, Mac mini and Mac Pro models that support the required OS | macOS 15 or later |

Choose by processor, not display size or the Air/Pro product name. Apple menu → About This Mac shows Chip on Apple Silicon and Processor on Intel. A single native package covers the supported models within its architecture; Rosetta is not needed when the matching package is installed.

The actual bundled OpenSSL dylibs declare macOS 15 as their minimum for both architectures. This release does not include macOS 14/13, 32-bit Intel or PowerPC packages. The framework's broader support list does not lower these binaries' deployment requirement. Sources: [Apple processor identification](https://support.apple.com/en-ca/116943), [Apple's macOS 15 compatibility list](https://support.apple.com/en-us/120282), [Avalonia platform support](https://docs.avaloniaui.net/docs/supported-platforms).

Both bundles contain the eleven native engines and their own architecture's TUN service. Installed dev.3 TUN clients/services remain byte-identical within each architecture. Do not transfer a TUN service between architectures.

These are development-signed prerelease candidates. Source and archive validation on Windows/Linux is recorded in VERIFICATION.json. Actual execution, Installer/Gatekeeper acceptance, Keychain prompts and routed traffic on these exact native Mac packages remain unverified. Developer ID signing and notarization are absent.
