# Alpha15: preserve the gomobile JNI ABI in minified APKs

Status: correction under validation; not a published release.

The uploaded alpha14/code25 stability report contains three
`REASON_CRASH_NATIVE` exits with signal/status 6. Its previous-process connection
report ends after `TRANSPORT_START` for NoizDNS. It has no native backtrace, so it
cannot by itself establish a specific failing native function or every crash.

An independently verified packaging defect was found in the exact published
alpha14 APK (SHA-256
`5c6886548347fca94f3c5291d843bf482814e8b16ebb234353c0f60b767c889a`).
R8 removed `go.Seq.Ref`, the reference callbacks, the `(int refnum)` constructors
of all four gomobile clients and the generated Go exception constructor. These
are required across JNI even when Java/Kotlin has no direct call to them.
`check-gomobile-apk.py` checks actual DEX definitions and rejects that APK.

`prepare-native.py` rebuilt the AAR from the matching generated Java sources but
omitted its consumer `proguard.txt`. The app's native-method-only rules could not
preserve the non-native JNI ABI. Upstream gomobile's `buildAAR` emits keep rules
for `go.**` and every bound package. The replacement preparation must do the same.

The fix supplies one shared `app/gomobile-consumer-rules.pro` to the app minifier
and to the reconstructed AAR. R8 stays enabled. No MTU, resolver transport, network
timeout, TLS/SSH trust or server configuration is changed.

The device gate now builds a minified private Release as well as Debug, checks
its DEX ABI, replaces the Debug private app with that Release under the same test
signer, and runs real gomobile constructor/reference/error tests in a fresh
process. These operations use synthetic values without remote servers. The
existing full Debug suite remains required. A passing Debug suite alone is not
enough evidence for this fix. API30/x86_64 success will still not claim validation
of the reported API37/ARM device or every real resolver.

Alpha15 is code26 and retains the pinned production identity for any eventual
public APK. Private CI artifacts remain a separate package and are not published
as a public update. Alpha14 assets/tags are immutable. The owner reported the
traffic-total symptom resolved; no counter or formatting change is included.

Sources: [gomobile buildAAR](https://github.com/golang/mobile/blob/master/cmd/gomobile/bind_androidapp.go),
[Android native exits](https://developer.android.com/reference/android/app/ApplicationExitInfo).
