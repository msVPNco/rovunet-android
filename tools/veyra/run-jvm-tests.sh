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
source_root=app/src/main/java/app/slipnet
output_dir=$(mktemp -d)
trap 'rm -rf "$output_dir"' EXIT
java -cp "$KOTLIN_JARS/*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -classpath "$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" -d "$output_dir/tests.jar" \
 "$source_root"/data/export/*.kt "$source_root"/domain/model/ConnectionState.kt "$source_root"/domain/model/ChainValidation.kt "$source_root"/domain/model/ServerProfile.kt "$source_root"/domain/model/SoarConfig.kt "$source_root"/domain/model/PingResult.kt "$source_root"/domain/model/ProfileRanking.kt "$source_root"/domain/model/ProfileCollections.kt "$source_root"/domain/model/ProfileDuplicates.kt "$source_root"/domain/model/ProbeCache.kt "$source_root"/domain/model/ProbePolicy.kt "$source_root"/domain/model/CollectionPhase.kt "$source_root"/domain/model/CollectionInference.kt "$source_root"/domain/model/RegroupHistory.kt "$source_root"/domain/model/ConnectionChoice.kt \
 "$source_root"/core/*.kt "$source_root"/util/BundleCrypto.kt "$source_root"/util/ConnectionDiagnostics.kt tools/veyra/jvm-stubs/*.kt tools/veyra/*Test.kt tools/veyra/CoreFixture.kt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" PortableLinkTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" ImportTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" CoreTestKt "${CORE_FIXTURES:-$output_dir}"
if [[ -n "${CORE_TEST_JAR:-}" ]]; then cp "$output_dir/tests.jar" "$CORE_TEST_JAR"; fi

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" LifecycleTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" CoreFormTestKt
if [[ -n "${TEST_TLS_STORE:-}" ]]; then
  java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" TunnelProbeTestKt
fi

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" CollectionTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" ProbeFailureTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" CollectionInferenceTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" DuplicatesTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" CollectionPhaseTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" ConnectionChoiceTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" ReleaseVersionTestKt

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" RepairDataTestKt
bash tools/veyra/run-session-regression.sh

if [[ -n "${TEST_TLS_STORE:-}" ]]; then bash tools/veyra/run-network-regression.sh; fi

java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" CoreReadinessTestKt
java -cp "$output_dir/tests.jar:$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar" StabilityTestKt
