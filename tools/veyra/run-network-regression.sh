#!/usr/bin/env bash
set -euo pipefail
: "${KOTLIN_JARS:?}" "${TEST_TLS_STORE:?}"
cd "$(dirname "$0")/../.."
target=$(mktemp -d)
trap 'rm -rf "$target"' EXIT
cp_path="$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/json.jar"
sources=app/src/main/java/app/slipnet/tunnel
java -cp "$KOTLIN_JARS/*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -classpath "$cp_path" -d "$target/tests.jar" \
 tools/veyra/network-stubs/*.kt tools/veyra/NetworkRegression.kt "$sources"/HttpProxyServer.kt "$sources"/RateLimiter.kt "$sources"/TlsSocketFactory.kt "$sources"/TransportTrust.kt "$sources"/ValidatedTls.kt "$sources"/DomainRouter.kt "$sources"/DirectRouting.kt "$sources"/ProtocolSniffer.kt app/src/main/java/app/slipnet/core/CoreLink.kt
java -cp "$target/tests.jar:$cp_path" NetworkRegressionKt
