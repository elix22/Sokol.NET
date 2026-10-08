#!/bin/bash
# Build libsokol_net.so for Android (API 26, c++_static — the Sokol.NET app template's settings).
# Output: plugins/Net/libs/android/<abi>/release/libsokol_net.so   (64-bit ABIs 16 KB aligned)
# Requires: ANDROID_NDK pointing to the NDK (CI pins r27.2.12479018, as the other plugins).
# Usage:  ./plugins/Net/scripts/build-android.sh [abi…]   (default: armeabi-v7a arm64-v8a x86_64)
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"
[ -n "${ANDROID_NDK:-}" ] && [ -d "$ANDROID_NDK" ] || { echo "Error: set ANDROID_NDK to the Android NDK directory"; exit 1; }
apply_patches

ABIS=("$@"); [ ${#ABIS[@]} -eq 0 ] && ABIS=(armeabi-v7a arm64-v8a x86_64)
HOST_TAG=$(ls "$ANDROID_NDK/toolchains/llvm/prebuilt/" | head -1)
READELF="$ANDROID_NDK/toolchains/llvm/prebuilt/$HOST_TAG/bin/llvm-readelf"

for ABI in "${ABIS[@]}"; do
    echo "===== sokol_net — Android $ABI ====="
    B="$BUILD_ROOT/android-$ABI"
    ARGS=( -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake"
           -DANDROID_ABI="$ABI" -DANDROID_PLATFORM=android-26 -DANDROID_STL=c++_static
           -DANDROID_SUPPORT_FLEXIBLE_PAGE_SIZES=ON )
    build_deps "$B" "${ARGS[@]}"
    build_plugin "$B" "${ARGS[@]}"
done

# Google Play (Android 15+): the 64-bit ABIs must be 16 KB page aligned — same gate as the CI.
for ABI in "${ABIS[@]}"; do
    case "$ABI" in arm64-v8a|x86_64) ;; *) continue ;; esac
    so="$PLUGIN_ROOT/libs/android/$ABI/release/libsokol_net.so"
    align=$("$READELF" -l "$so" | awk '/LOAD/ {print $NF; exit}')
    [ "$align" = "0x4000" ] || { echo "ERROR: $so is not 16 KB aligned ($align)"; exit 1; }
    echo "16 KB alignment OK: $ABI"
done
