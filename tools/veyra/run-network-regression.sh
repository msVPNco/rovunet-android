#!/usr/bin/env bash
set -euo pipefail
: "${KOTLIN_JARS:?}" "${TEST_TLS_STORE:?}"
cd "$(dirname "$0")/../.."
target=$(mktemp -d)
trap 'rm -rf "$target"' EXIT
cp_path="$KOTLIN_JARS/kotlin-stdlib-2.2.21.jar:$KOTLIN_JARS/kotlinx-coroutines-core-jvm-1.8.0.jar:$KOTLIN_JARS/jsch-2.27.7.jar:$KOTLIN_JARS/json.jar:$KOTLIN_JARS/bcprov-jdk18on-1.80.jar"
sources=app/src/main/java/app/slipnet/tunnel
java -cp "$KOTLIN_JARS/*" org.jetbrains.kotlin.cli.jvm.K2JVMCompiler -no-stdlib -no-reflect -classpath "$cp_path" -d "$target/tests.jar" \
 app/src/main/java/app/slipnet/util/ConnectionDiagnostics.kt tools/veyra/network-stubs/*.kt tools/veyra/NetworkRegression.kt tools/veyra/SocksBridgeRegression.kt tools/veyra/SocksDnsFallbackRegression.kt tools/veyra/SpkiRegression.kt tools/veyra/SshStartupRegression.kt tools/veyra/jvm-stubs/BuildConfig.kt app/src/main/java/app/slipnet/domain/model/ServerProfile.kt "$sources"/HttpProxyServer.kt "$sources"/Socks5ProxyInstance.kt "$sources"/DnsFallback.kt "$sources"/LocalProxyAuth.kt "$sources"/ProxyAuthLimiter.kt "$sources"/RateLimiter.kt "$sources"/TlsSocketFactory.kt "$sources"/StartupCancellation.kt "$sources"/SshStartup.kt "$sources"/DnsUtils.kt "$sources"/SshTunnelInstance.kt "$sources"/NaiveSocksProxy.kt "$sources"/PayloadSocketFactory.kt "$sources"/ProxyHttpConnect.kt "$sources"/ProxyWebSocket.kt "$sources"/TransportTrust.kt "$sources"/ValidatedTls.kt "$sources"/DomainRouter.kt "$sources"/DirectRouting.kt "$sources"/ProtocolSniffer.kt app/src/main/java/app/slipnet/core/CoreLink.kt
java -cp "$target/tests.jar:$cp_path" NetworkRegressionKt

java -cp "$target/tests.jar:$cp_path" SocksBridgeRegressionKt

java -cp "$target/tests.jar:$cp_path" SpkiRegressionKt

java -cp "$target/tests.jar:$cp_path" SshStartupRegressionKt

java -cp "$target/tests.jar:$cp_path" SocksDnsFallbackRegressionKt
