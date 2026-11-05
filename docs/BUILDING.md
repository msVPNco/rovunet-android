# Build and verification — alpha11

The published Full APK is built from application commit 91f2c0e9a9fd94eb72e30236bc965293b5303762, versionCode 17. Release documentation may be newer; it does not change that artifact.

## Source assets and provenance

Both source ZIPs expand available pinned Git submodules and record their revisions in SOURCE-MANIFEST.json. The lightweight ZIP omits precompiled .so/.aar files. The larger ZIP retains tracked precompiled assets; generated dependencies and signing material are not a self-contained build environment.

The original dnstt-mobile tree at b265e04c40835ec1bacd447fff79169e6c2c2233 was unavailable upstream. It has not been replaced with a different revision. See [native source provenance](releases/alpha08-native-sources.md). Automatic GitHub source archives do not expand submodules; use the explicit release assets for available expanded sources.

## Android build

Use Linux, JDK 21, Android SDK platform 36, suitable build tools, NDK 29.0.14206865, Python 3 and Go. Follow [core setup](../VEYRA-CORE.md) for exact native pins.

```sh
python3 tools/veyra/prepare-native.py
./gradlew :app:assembleFullDebug -PveyraPrebuiltNative=true
```

The preparation script validates the upstream Full APK before recovering unchanged native assets and builds the pinned core/launcher. The prebuilt-native flag avoids rebuilding unchanged inherited engines; it is not a claim that the original unavailable source can be rebuilt.

Release builds require a locally provided keystore.properties and signing key:

```sh
./gradlew :app:assembleFullRelease -PveyraPrebuiltNative=true
```

Never add a private signing key, passwords or real user configurations to Git. New signing keys do not upgrade an existing installation automatically. The public alpha11 keeps the previous debug certificate solely for compatibility; production-key migration remains work to plan.

## Automated checks

[Exact application-commit CI](https://github.com/msVPNco/rovunet-android/actions/runs/41984862024) passed verification. Its Android build job was skipped; the published APK was compiled and verified locally.

The versioned workflow pins the panel contract revision using tools/veyra/MANAGER_REF. Its preparation step verifies Kotlin JAR hashes and builds the pinned Xray test binary. The same fixture is used by the Go exporter and Kotlin importer.

Entry points:

```sh
tools/veyra/prepare-jvm-ci.sh
tools/veyra/run-jvm-tests.sh
python3 tools/veyra/integration.py
```

These scripts require the workflow's tooling/fixture environment; use [.github/workflows/build.yml](../.github/workflows/build.yml) for exact paths and preparation. See tools/veyra/results/alpha11 for the recorded manager and JVM results.

Coverage includes actual connection-manager lifecycle regressions, cancellation/late events, attempt watchdogs, profile choice, settings resource coverage, contract fixtures, real TLS/proxy tests, generated routing and four-protocol TCP/UDP loopback.

## Device validation

No Android phone or emulator was connected during this release work. Successful compilation, local traffic and CI are not a substitute for Android foreground-service scheduling, battery policy, network changes, real DPI conditions or long sessions. Physical-device and reported ANR/Connecting verification remains pending.
