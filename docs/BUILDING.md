# Build and verification — alpha16

Target: 0.3.0-alpha16, versionCode 32. A source/build record is not a public release. Exact source commit/tree, APK hashes, package/signer and executed checks belong in VERIFICATION.json next to the final assets. Alpha11 records remain historical.

## Source and native provenance

Explicit source ZIPs expand available pinned trees and record revisions in SOURCE-MANIFEST.json. Light archives omit tracked precompiled .so/.aar files; larger archives retain them. Generated dependencies and signing material are not a self-contained build environment. One original dnstt-mobile tree remains unavailable; no other revision is substituted. Automatic GitHub source archives do not expand submodules.

Use Linux, JDK 21, SDK platform 36, build tools, NDK 29.0.14206865, Python 3 and module-compatible Go/Rust tooling. The repository Gradle wrapper is authoritative. Prepare pinned dependencies following [core setup](history/VEYRA-CORE.md):

```sh
python3 tools/veyra/prepare-native.py
```

Preparation validates the inherited Full APK before recovering unchanged engines and builds the pinned core/launcher. `veyraPrebuiltNative=true` avoids rebuilding unchanged engines; it is not a claim of a fresh complete native rebuild. Building OpenSSL from source uses the versioned script, not an unverified downloaded archive.

## Private validation package

```sh
bash gradlew :app:assembleFullDebug :app:assembleFullDebugAndroidTest :app:testFullDebugUnitTest \
  -PveyraPrebuiltNative=true -ProvunetDeviceCandidate=true \
  -Dorg.gradle.jvmargs="-Xmx6g -Dfile.encoding=UTF-8" --max-workers=2
```

This produces app.veyra.client.candidate / Rovunet Test, code 32, version 0.3.0-alpha16-review01. It is separate from the public app. Do not publish a private/debug signer as a production update.

## Signed public Release

Provide private keystore.properties outside Git, a managed keystore and the approved certificate SHA-256. [Signing procedure](RELEASING.md).

```sh
bash gradlew :app:assembleFullRelease -PveyraPrebuiltNative=true \
  -ProvunetReleaseCertSha256=APPROVED_64_HEX_CERTIFICATE_SHA256
```

`verifyPublicationSigning` rejects absent/mismatched production identity and Android Debug. Never publish key files/passwords or remove this check to make a build pass.

## Unsigned preparation for an offline signer

```sh
bash gradlew :app:assembleFullRelease -PveyraPrebuiltNative=true \
  -ProvunetUnsignedPublication=true
```

This explicit mode leaves Release unsigned and adds `-unsigned` to its filename. It cannot be combined with private-candidate mode and does not fall back to a debug key. It is a non-installable preparation artifact, not a final Full APK. The offline signer must verify source/manifest/native hashes, align/sign with the approved identity, then verify package/version/certificate and installer migration before publication. Ordinary signed builds still enforce the production gate.

## Checks

```sh
python3 tools/veyra/check-settings-localization.py
python3 tools/quality/check-editor-localization.py
python3 tools/quality/check-kotlin-safety.py
python3 tools/veyra/check-device-gate.py
bash gradlew :app:testFullDebugUnitTest -PveyraPrebuiltNative=true -ProvunetDeviceCandidate=true
```

`tools/veyra/run-jvm-tests.sh` also executes portable contracts and real network fixtures using the tooling environment described in .github/workflows/build.yml. It needs KOTLIN_JARS and, for network/core checks, XRAY, CORE_LAUNCHER, TEST_TLS_STORE and TLS_TARGET. CI also verifies the pinned panel exporter contract and protocol loopback fixtures. Do not call a skipped job a pass.

Before running the device gate, also build the minified private Release and its framework-only JNI instrumentation:

```sh
bash gradlew :app:assembleFullRelease :app:assembleFullReleaseAndroidTest \
  -PveyraPrebuiltNative=true -ProvunetDeviceCandidate=true \
  -ProvunetMinifiedBindingTests=true \
  -Dorg.gradle.jvmargs="-Xmx6g -Dfile.encoding=UTF-8" --max-workers=2
```

The default Debug tests use AndroidJUnitRunner. The Release JNI driver uses Android's framework Instrumentation and Java-only shared assertions, with no AndroidX/JUnit/Kotlin runtime dependency in that test APK. `run-device-gate.py` first checks all 55 Debug cases, then installs the minified private Release and matching test APK and requires both JNI cases. It checks actual DEX definitions and rejects incomplete, failed or skipped results. Never substitute a Debug APK as evidence that R8 retained the JNI ABI. The shared gomobile consumer rules must remain in the reconstructed AAR and the app's Release configuration.

For a disposable emulator with VPN consent, build the private application/tests and run `tools/veyra/run-device-gate.py` with ANDROID_SERIAL. The gate rejects skipped, failed, incomplete or missing required tests. It exercises cross-UID VPN traffic/MTU, SSH identity and cancellation, scanner lifecycle, migration/recovery, choices/history and UI/RTL. Local fixture success does not prove real Iranian-network availability or every architecture/backend.

Before delivering an APK, verify ZIP integrity, manifest, apksigner output, all native entries and `tools/quality/check-apk-native.py`. Only record checks actually executed on the final source and distinguish the private validation binary from the unsigned/public Release.

Application source: `ea5558fea6c6a2e9f2b94ef1ce8a230bd2308ba4`, tree `e60347cdd9deb4b433ff88fe340ad44898b3059c`. [Source CI](https://github.com/msVPNco/rovunet-android/actions/runs/42471804819) passed 83 portable/JUnit, 86 Gradle unit, 55 Debug emulator and two minified Release JNI cases. The source-identical PR retry also passed after one initial Soar fixture failure; its cause is unestablished. The separately dispatched complete native/signing build was skipped. Pinned Xray, launcher and project Hev/JNI were rebuilt; remaining engines reuse the SHA-256-pinned SlipNet 2.5.6 APK.

Publication preserves the exact accepted signed APK (SHA-256 `3950b602865c55a6dfc8bec4cbb13da4585dca0c28ad4842afd6d8b273618b7a`). Documentation and release tooling are separate from tested application/build/test inputs. The one-version publication workflow restores those approved bytes using the pinned CI artifact and public copy/signature record; it has no signing key and performs no application rebuild. Source archives contain tracked files and the same available, pinned expanded native trees as alpha15.4. No complete standalone native toolchain or measured battery saving is claimed.

CI download setup can be reproduced with `python3 tools/veyra/prepare-gradle.py` and `-ProvunetVerifiedBuildInputs=true`. The verified compiler repository preserves the pinned Kotlin 2.2.21 bits; it does not change dependency versions.
