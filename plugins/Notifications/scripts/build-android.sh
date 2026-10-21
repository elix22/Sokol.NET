#!/bin/bash
# Build libsokol_notifications.so for Android — standalone Notifications plugin library.
# Output: libs/android/<abi>/{debug,release}/libsokol_notifications.so
# Run from any directory; the script resolves the repo root automatically.
#
# Requires: ANDROID_NDK environment variable pointing to the Android NDK.
# Usage:    ./plugins/Notifications/scripts/build-android.sh
#
# The plugin links against the pre-built libsokol.so for each ABI.
# Build the main sokol library first:  ./scripts/build-android-sokol-libraries.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
PLUGIN_NATIVE="$REPO_ROOT/plugins/Notifications/native"

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
echo "sokol_notifications — Android build"
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

    BUILD_DIR="$REPO_ROOT/build-sokol-notifications-android-$ABI"
    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"

    # SOKOL_LIB_DIR is auto-derived inside CMakeLists.txt from the plugin's location.
    cmake -S "$PLUGIN_NATIVE" -B "$BUILD_DIR" \
        -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
        -DANDROID_ABI="$ABI" \
        -DANDROID_PLATFORM=android-26 \
        -DCMAKE_BUILD_TYPE=Release

    cmake --build "$BUILD_DIR"

    rm -rf "$BUILD_DIR"

    echo "  -> plugins/Notifications/libs/android/$ABI/release/libsokol_notifications.so"
done

echo ""
echo "========================================="
echo "sokol_notifications — Android done!"
for ABI in "${ANDROID_ABIS[@]}"; do
    echo "  $ABI: plugins/Notifications/libs/android/$ABI/release/libsokol_notifications.so"
done
echo "========================================="
echo ""
echo "Next: rebuild the APK so libsokol_notifications.so is packaged alongside libsokol.so."
