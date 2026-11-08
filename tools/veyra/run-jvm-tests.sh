#!/usr/bin/env bash
set -euo pipefail
# KOTLIN_JARS points to a directory containing Kotlin 2.2.21 compiler and dependencies.
: "${KOTLIN_JARS:?Set KOTLIN_JARS to the compiler jar directory}"
cd "$(dirname "$0")/../.."
if [[ "${CI:-}" == true ]]; then
  : "${ROVUNET_ANDROID_FIXTURE:?CI requires the generated panel fixture}"
  : "${TEST_TLS_STORE:?CI requires HTTPS tests}" "${XRAY:?CI requires core lifecycle tests}" "${CORE_LAUNCHER:?CI requires supervisor}"
  test -s "$ROVUNET_ANDROID_FIXTURE"
fi
python3 tools/veyra/check-settings-localization.py
python3 tools/quality/check-editor-localization.py
source_root=app/src/main/java/app/slipnet
output_dir=$(mktemp -d)
trap 'rm -rf "$output_dir"' EXIT
java -cp "$KOTLIN_JARS/*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -classpath "$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" -d "$output_dir/tests.jar" \
 "$source_root"/data/export/*.kt "$source_root"/domain/model/ConnectionState.kt "$source_root"/domain/model/ChainValidation.kt "$source_root"/domain/model/ServerProfile.kt "$source_root"/domain/model/SoarConfig.kt "$source_root"/domain/model/DataPathState.kt "$source_root"/domain/model/PingResult.kt "$source_root"/domain/model/ProfileRanking.kt "$source_root"/domain/model/ProfileCollections.kt "$source_root"/domain/model/ProfileDuplicates.kt "$source_root"/domain/model/ProbeCache.kt "$source_root"/domain/model/ConnectionHistory.kt "$source_root"/tunnel/ProxyAuthLimiter.kt "$source_root"/tunnel/StartupCancellation.kt "$source_root"/service/StartupTeardown.kt "$source_root"/tunnel/SshStartup.kt "$source_root"/tunnel/DnsFallback.kt "$source_root"/domain/model/ProbePolicy.kt "$source_root"/domain/model/CollectionPhase.kt "$source_root"/domain/model/CollectionInference.kt "$source_root"/domain/model/RegroupHistory.kt "$source_root"/domain/model/ConnectionChoice.kt "$source_root"/domain/model/AttemptWatchdog.kt "$source_root"/domain/model/PausedTimeout.kt \
 app/src/test/java/app/slipnet/domain/ConnectionHistoryTest.kt app/src/test/java/app/slipnet/domain/RegressionContractTest.kt app/src/test/java/app/slipnet/tunnel/*Test.kt app/src/test/java/app/slipnet/util/*Test.kt app/src/test/java/app/slipnet/data/export/*Test.kt "$source_root"/core/*.kt "$source_root"/util/BundleCrypto.kt "$source_root"/util/ConnectionDiagnostics.kt "$source_root"/util/ScanDiagnostics.kt "$source_root"/util/ScanWork.kt "$source_root"/util/DnsDatagram.kt "$source_root"/tunnel/RelayResources.kt "$source_root"/tunnel/WebSocketFrames.kt "$source_root"/service/VpnPowerPolicy.kt "$source_root"/data/subscription/AccountStatus.kt app/src/test/java/app/slipnet/core/*Test.kt app/src/test/java/app/slipnet/AccountStatusTest.kt app/src/test/java/app/slipnet/service/VpnPowerPolicyTest.kt app/src/test/java/app/slipnet/domain/ConnectionChoiceRegression.kt app/src/test/java/app/slipnet/domain/CollectionPhaseRegression.kt app/src/test/java/app/slipnet/domain/AttemptWatchdogRegression.kt tools/veyra/jvm-stubs/*.kt tools/veyra/network-stubs/Log.kt tools/veyra/*Test.kt tools/veyra/CoreFixture.kt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" PortableLinkTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" ImportTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" CoreTestKt "${CORE_FIXTURES:-$output_dir}"
if [[ -n "${CORE_TEST_JAR:-}" ]]; then cp "$output_dir/tests.jar" "$CORE_TEST_JAR"; fi

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" LifecycleTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" CoreFormTestKt
if [[ -n "${TEST_TLS_STORE:-}" ]]; then
  java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" TunnelProbeTestKt
fi

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" CollectionTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" ProbeFailureTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" CollectionInferenceTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" DuplicatesTestKt


java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" ReleaseVersionTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" RepairDataTestKt
bash tools/veyra/run-session-regression.sh

if [[ -n "${TEST_TLS_STORE:-}" ]]; then bash tools/veyra/run-network-regression.sh; fi

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" CoreReadinessTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" StabilityTestKt


java -cp "$output_dir/tests.jar:app/src/test/resources:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/gson-2.13.2.jar:$KOTLIN_JARS/junit-4.13.2.jar:$KOTLIN_JARS/hamcrest-core-1.3.jar" org.junit.runner.JUnitCore app.slipnet.core.UpdateManifestTest app.slipnet.AccountStatusTest app.slipnet.service.VpnPowerPolicyTest app.slipnet.util.ScanWorkTest app.slipnet.util.DnsDatagramTest app.slipnet.tunnel.RelayResourcesTest app.slipnet.tunnel.WebSocketFramesTest app.slipnet.util.ConnectionDiagnosticsTest app.slipnet.domain.ConnectionHistoryTest app.slipnet.domain.RegressionContractTest app.slipnet.tunnel.ProxyAuthLimiterTest app.slipnet.data.export.ImportCorpusTest app.slipnet.data.export.WireBase64Test app.slipnet.tunnel.SshStartupTest app.slipnet.tunnel.DnsFallbackTest app.slipnet.tunnel.ProxySocketFactoryTest
