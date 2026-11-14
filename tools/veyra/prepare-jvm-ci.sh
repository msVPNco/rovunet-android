#!/usr/bin/env bash
set -euo pipefail
: "${KOTLIN_JARS:?}" "${XRAY:?}" "${CORE_LAUNCHER:?}" "${TEST_TLS_STORE:?}"
mkdir -p "$KOTLIN_JARS"
maven=https://repo.maven.apache.org/maven2
for coordinate in \
 org/jetbrains/kotlin/kotlin-compiler/2.2.21/kotlin-compiler-2.2.21 \
 org/jetbrains/kotlin/kotlin-stdlib/2.2.21/kotlin-stdlib-2.2.21 \
 org/jetbrains/kotlin/kotlin-script-runtime/2.2.21/kotlin-script-runtime-2.2.21 \
 org/jetbrains/kotlin/kotlin-reflect/1.6.10/kotlin-reflect-1.6.10 \
 org/jetbrains/kotlinx/kotlinx-coroutines-core-jvm/1.8.0/kotlinx-coroutines-core-jvm-1.8.0 \
 com/github/mwiede/jsch/2.27.7/jsch-2.27.7 \
 org/bouncycastle/bcprov-jdk18on/1.80/bcprov-jdk18on-1.80 \
 com/google/code/gson/gson/2.13.2/gson-2.13.2 \
 junit/junit/4.13.2/junit-4.13.2 \
 org/hamcrest/hamcrest-core/1.3/hamcrest-core-1.3 \
 org/jetbrains/annotations/13.0/annotations-13.0; do
 [[ -s "$KOTLIN_JARS/${coordinate##*/}.jar" ]] || curl --fail --retry 3 -L "$maven/$coordinate.jar" -o "$KOTLIN_JARS/${coordinate##*/}.jar"
done
[[ -s "$KOTLIN_JARS/json.jar" ]] || curl --fail --retry 3 -L "$maven/org/json/json/20250517/json-20250517.jar" -o "$KOTLIN_JARS/json.jar"
checksum_file="$PWD/tools/veyra/jars.sha256"
(cd "$KOTLIN_JARS" && sha256sum -c "$checksum_file")
keytool -genkeypair -alias probe -keyalg RSA -storetype PKCS12 -keystore "$TEST_TLS_STORE" -storepass test-only -keypass test-only -dname CN=localhost -ext SAN=dns:localhost -validity 2
core_source=$(mktemp -d)
trap 'rm -rf "$core_source"' EXIT
git clone https://github.com/XTLS/Xray-core.git "$core_source"
git -C "$core_source" checkout 63ca2e0c1d31b89f67700c33842431f541fedd25
python3 tools/native/patch-xray-certificates.py "$core_source"
(cd "$core_source" && go test ./transport/internet/tls -run TestRovunet -count=1)
(cd "$core_source" && go build -trimpath -ldflags='-s -w -checklinkname=0' -o "$XRAY" ./main)
cc -O2 app/src/main/cpp/veyra-core-launcher.c -o "$CORE_LAUNCHER"

: "${TLS_TARGET:?}"
go build -o "$TLS_TARGET" tools/veyra/tls_target.go

ROVUNET_AWG_HOST="$(dirname "$XRAY")/awg" python3 tools/native/build-awg.py
