#!/bin/bash
#
# Build OpenSSL for Android (all ABIs)
# Usage: ./build-openssl-android.sh
#
# Environment variables:
#   OPENSSL_VERSION - OpenSSL version to build (default: 3.0.15)
#   OUTPUT_DIR - Output directory (default: ~/android-openssl/android-ssl)
#   ANDROID_NDK_HOME - Path to Android NDK
#

set -e

OPENSSL_VERSION="${OPENSSL_VERSION:-3.0.15}"
OUTPUT_DIR="${OUTPUT_DIR:-$HOME/android-openssl/android-ssl}"
ANDROID_API=24

# Find NDK
if [ -z "$ANDROID_NDK_HOME" ]; then
    if [ -d "$HOME/Library/Android/sdk/ndk" ]; then
        ANDROID_NDK_HOME=$(ls -d "$HOME/Library/Android/sdk/ndk"/*/ 2>/dev/null | head -1 | sed 's:/$::')
    elif [ -d "$ANDROID_HOME/ndk" ]; then
        ANDROID_NDK_HOME=$(ls -d "$ANDROID_HOME/ndk"/*/ 2>/dev/null | head -1 | sed 's:/$::')
    fi
fi

if [ -z "$ANDROID_NDK_HOME" ] || [ ! -d "$ANDROID_NDK_HOME" ]; then
    echo "Error: Android NDK not found. Set ANDROID_NDK_HOME environment variable."
    exit 1
fi

echo "Using NDK: $ANDROID_NDK_HOME"
echo "OpenSSL version: $OPENSSL_VERSION"
echo "Output directory: $OUTPUT_DIR"

# Create working directory
WORK_DIR=$(mktemp -d)
cd "$WORK_DIR"

echo "Working directory: $WORK_DIR"

# Download OpenSSL
echo "Downloading OpenSSL ${OPENSSL_VERSION}..."
case "$OPENSSL_VERSION" in
    3.0.15) openssl_sha=23c666d0edf20f14249b3d8f0368acaee9ab585b09e1de82107c66e1f3ec9533 ;;
    *) echo "Add the verified source checksum before changing the OpenSSL version." >&2; exit 1 ;;
esac
archive="openssl-${OPENSSL_VERSION}.tar.gz"
curl --fail --location --retry 3 --output "$archive" "https://github.com/openssl/openssl/releases/download/openssl-${OPENSSL_VERSION}/$archive"
if command -v sha256sum >/dev/null; then
    printf '%s  %s\n' "$openssl_sha" "$archive" | sha256sum -c -
else
    printf '%s  %s\n' "$openssl_sha" "$archive" | shasum -a 256 -c -
fi
tar xzf "openssl-${OPENSSL_VERSION}.tar.gz"
cd "openssl-${OPENSSL_VERSION}"

# Detect host platform
case "$(uname -s)" in
    Darwin*)
        HOST_TAG="darwin-x86_64"
        ;;
    Linux*)
        HOST_TAG="linux-x86_64"
        ;;
    *)
        echo "Unsupported host platform"
        exit 1
        ;;
esac

TOOLCHAIN="$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/$HOST_TAG"

# Build function
build_openssl() {
    local ABI=$1
    local TARGET=$2
    local ARCH=$3

    echo ""
    echo "=========================================="
    echo "Building OpenSSL for $ABI"
    echo "=========================================="

    export ANDROID_NDK_ROOT="$ANDROID_NDK_HOME"
    export PATH="$TOOLCHAIN/bin:$PATH"

    local OUTPUT="$OUTPUT_DIR/$ABI"
    mkdir -p "$OUTPUT"

    make clean 2>/dev/null || true

    # Use neutral --prefix/--openssldir/--libdir so the build-machine path is
    # not baked into OPENSSLDIR/ENGINESDIR/MODULESDIR in the compiled binary.
    # Install via DESTDIR then copy the needed headers and static libs to $OUTPUT.
    ./Configure "$TARGET" \
        -D__ANDROID_API__=$ANDROID_API \
        --prefix=/usr/local \
        --openssldir=/etc/ssl \
        --libdir=lib \
        no-shared \
        no-tests \
        no-ui-console

    make -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
    local DESTDIR_TMP="$OUTPUT/destdir"
    make install_sw DESTDIR="$DESTDIR_TMP"
    mkdir -p "$OUTPUT/lib" "$OUTPUT/include"
    cp -rp "$DESTDIR_TMP/usr/local/include/." "$OUTPUT/include/"
    find "$DESTDIR_TMP/usr/local/lib" -name "*.a" -exec cp {} "$OUTPUT/lib/" \;
    rm -rf "$DESTDIR_TMP"

    echo "OpenSSL for $ABI installed to $OUTPUT"
}

# Build for each ABI
build_openssl "arm64-v8a" "android-arm64" "aarch64"
build_openssl "armeabi-v7a" "android-arm" "arm"
build_openssl "x86_64" "android-x86_64" "x86_64"
build_openssl "x86" "android-x86" "x86"

# Cleanup
cd /
rm -rf "$WORK_DIR"

echo ""
echo "=========================================="
echo "OpenSSL build complete!"
echo "Output directory: $OUTPUT_DIR"
echo "=========================================="
