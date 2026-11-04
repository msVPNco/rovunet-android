#!/usr/bin/env bash
set -euo pipefail
: "${KOTLIN_JARS:?}"
cd "$(dirname "$0")/../.."
target=$(mktemp -d)
trap 'rm -rf "$target"' EXIT
cp_path="$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar"
java -cp "$KOTLIN_JARS/*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -classpath "$cp_path" -d "$target/classes" \
 tools/veyra/session-stubs/*.kt tools/veyra/SessionManagerRegression.kt app/src/main/java/app/slipnet/service/VpnConnectionManager.kt app/src/main/java/app/slipnet/domain/model/ConnectionState.kt app/src/main/java/app/slipnet/domain/model/ConnectionSessions.kt app/src/main/java/app/slipnet/domain/model/ServiceCommandPolicy.kt
mkdir -p "$target/classes/META-INF/services"
printf '%s\n' audit.AuditDispatcherFactory > "$target/classes/META-INF/services/kotlinx.coroutines.internal.MainDispatcherFactory"
java -cp "$target/classes:$cp_path" SessionManagerRegressionKt
