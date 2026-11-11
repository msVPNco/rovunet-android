# Rovunet 0.3.0-alpha10 — device-validation candidate

Version code **16**. Historical intermediate build, superseded by [alpha11](alpha11.md); its localization and stability changes are included in that public release.

## Reported symptoms and evidence

- The Android Wait/Close app dialog indicates an ANR (an unresponsive main thread), not necessarily an unhandled exception. No trace from the reporting phone has been received, so its root cause is **unconfirmed**.
- Alpha07 Debug universal APK: 127,201,161 bytes. Alpha09 Full Release universal APK: 96,434,069 bytes. Both contain the exact same 33 native libraries (same entry names and SHA-256 bytes). The difference comes from R8 shrinking DEX code and stronger ZIP compression of unchanged native libraries, plus resource shrinking. It was not caused by the lightweight source archive.
- Changing the UI language called Activity.recreate(), rebuilding the Activity and its composition. Most inherited settings titles/descriptions were outside the translation resources.

## Changes

- Live Compose resource/configuration/layout-direction providers. Changing language dismisses the chooser and updates resources in place; no Activity recreation or navigation reset. LocalContext remains the Activity, preserving permission and navigation integrations. Legacy rv() UI labels now use Compose resources instead of creating a configuration context per text.
- Settings, nested dialogs, country and worker choices, formatted descriptions, accessibility labels, About and notification-channel labels use four-language Android resources. Russian count labels use plurals. Numeric/network fields remain LTR. This is not a claim that all inherited editor/scanner text is translated.
- Blocking bridge shutdown on suspend setup/error paths and native traffic reads run on IO. Speed-counter publication uses a short epoch-protected lock; reset cannot wait on native calls, and a pre-reset sample is discarded. These remove main-thread blocking paths, but do not prove the phone ANR is resolved.
- Private stability diagnostics capture one bounded symbol-only main-thread snapshot per visible UI stall, an uncaught exception class/frames without exception messages, and Android process-exit reason codes on API 30+. No addresses, credentials, identifiers or ordinary connection logs are copied. No automatic upload. No watchdog wakeups while UI is backgrounded. The Settings report dialog allows deliberate sharing through Android.

## Verification

- Resource coverage checks fail on untranslated settings text, missing language keys, mismatched format arguments or missing Russian plural forms.
- JVM tests cover foreground/debugger/time guards, one report per stall and redaction/bounds, together with the existing contract, proxy, TLS, lifecycle and routing regressions.
- Full Release compilation, package/signature/native-library comparison are recorded with the candidate artifacts. No emulator or physical device is attached. Long-duration phone validation, rapid language/RTL switching and the actual ANR reproduction remain pending.

Existing native binaries and signing identity are retained. The alpha09 native-source provenance limitation remains; no source code has been substituted for the unavailable pinned dnstt-mobile tree.
