#!/bin/bash
# Build script for spine-c library for all Android architectures
# Usage: ./build-spine-c-android-all.sh [build_type]
# Example: ./build-spine-c-android-all.sh Release
# Example: ./build-spine-c-android-all.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building spine-c for all Android architectures"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Android ABIs to build (matching sokol library architectures)
ANDROID_ABIS=("arm64-v8a" "armeabi-v7a" "x86_64")

for ABI in "${ANDROID_ABIS[@]}"; do
    echo ""
    echo "Building for Android $ABI..."
    "$SCRIPT_DIR/build-spine-c-android.sh" "$ABI" "$BUILD_TYPE"
done

echo ""
echo "=========================================="
echo "All Android builds completed successfully!"
echo "=========================================="

# Show built libraries
echo ""
echo "Built libraries:"
SPINE_C_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
if [ -d "$SPINE_C_DIR" ]; then
    find "$SPINE_C_DIR" -name "libspine-c.so" -path "*/build-android-*/*" -type f | sort
else
    echo "No libraries found in $SPINE_C_DIR"
fi

echo ""
echo "Library details:"
find "$SPINE_C_DIR" -name "libspine-c.so" -path "*/build-android-*/*" -type f -exec ls -lh {} \;