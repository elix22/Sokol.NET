#!/bin/bash
# Build libsokol_links.so for Android — standalone Links plugin library.
# Output: libs/android/<abi>/release/libsokol_links.so
# Run from any directory; the script resolves the repo root automatically.
#
# Requires: ANDROID_NDK environment variable pointing to the Android NDK.
# Usage:    ./plugins/Links/scripts/build-android.sh
#
# The plugin links against the pre-built libsokol.so for each ABI
# (sapp_android_get_native_activity). Build the main sokol library first:
#   ./scripts/build-android-sokol-libraries.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Links/native"

if [ -z "$ANDROID_NDK" ]; then
    echo "Error: ANDROID_NDK environment variable is not set."
    echo "Example: export ANDROID_NDK=/path/to/android-ndk"
    exit 1
fi
if [ ! -d "$ANDROID_NDK" ]; then
    echo "Error: ANDROID_NDK directory not found: $ANDROID_NDK"
    exit 1
fi

ANDROID_ABIS=("armeabi-v7a" "arm64-v8a" "x86_64")

echo "========================================="
echo "sokol_links — Android build"
echo "NDK:  $ANDROID_NDK"
echo "ABIs: ${ANDROID_ABIS[*]}"
echo "========================================="

for ABI in "${ANDROID_ABIS[@]}"; do
    echo ""
    echo "----- ABI: $ABI -----"

    SOKOL_SO="$REPO_ROOT/libs/android/$ABI/release/libsokol.so"
    if [ ! -f "$SOKOL_SO" ]; then
        echo "Error: libsokol.so not found at $SOKOL_SO"
        echo "       Run ./scripts/build-android-sokol-libraries.sh first."
        exit 1
    fi

    BUILD_DIR="$REPO_ROOT/build-sokol-links-android-$ABI"
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"

    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" \
        -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI="$ABI" \
        -DANDROID_PLATFORM=android-26 \
        -DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON \
        -DCMAKE_BUILD_TYPE=Release
    # ^ 16 KB ELF alignment for the 64-bit ABIs (Google Play, Android 15+ devices): NDK r27 — the CI's — still
    #   defaults to 4 KB; r28+ default to 16 KB. 32-bit stays 4 KB. Same flag as the builder's app template.

    cmake --build "$BUILD_DIR"

    rm -rf "$BUILD_DIR"

    echo "  -> plugins/Links/libs/android/$ABI/release/libsokol_links.so"
done

echo ""
echo "========================================="
echo "sokol_links — Android done!"
for ABI in "${ANDROID_ABIS[@]}"; do
    echo "  $ABI: plugins/Links/libs/android/$ABI/release/libsokol_links.so"
done
echo "========================================="
