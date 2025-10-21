#!/bin/bash
# Build script for assimp library for all Android architectures
# Usage: ./build-assimp-android-all.sh [build_type]
# Example: ./build-assimp-android-all.sh Release
# Example: ./build-assimp-android-all.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building assimp for all Android architectures"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Android ABIs to build (matching sokol library architectures)
ANDROID_ABIS=("arm64-v8a" "armeabi-v7a" "x86_64")

for ABI in "${ANDROID_ABIS[@]}"; do
    echo ""
    echo "Building for Android $ABI..."
    "$SCRIPT_DIR/build-assimp-android.sh" "$ABI" "$BUILD_TYPE"
done

echo ""
echo "=========================================="
echo "All Android builds completed successfully!"
echo "=========================================="

# Show built libraries
echo ""
echo "Built libraries:"
ASSIMP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')

for ABI in "${ANDROID_ABIS[@]}"; do
    LIB_PATH="$ASSIMP_DIR/libs/android/$ABI/$BUILD_TYPE_LOWER/libassimp.so"
    if [ -f "$LIB_PATH" ]; then
        echo "  ✓ $ABI: $LIB_PATH"
        ls -lh "$LIB_PATH"
    else
        echo "  ✗ $ABI: Not found"
    fi
done
