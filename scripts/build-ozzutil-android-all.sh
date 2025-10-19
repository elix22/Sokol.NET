#!/bin/bash
# Build script for ozzutil library for all Android architectures
# Usage: ./build-ozzutil-android-all.sh [build_type]
# Example: ./build-ozzutil-android-all.sh Release
# Example: ./build-ozzutil-android-all.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building ozzutil for all Android architectures"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Android ABIs to build
ANDROID_ABIS=("arm64-v8a" "armeabi-v7a" "x86_64" "x86")

for ABI in "${ANDROID_ABIS[@]}"; do
    echo ""
    echo "Building for Android $ABI..."
    "$SCRIPT_DIR/build-ozzutil-android.sh" "$ABI" "$BUILD_TYPE"
done

echo ""
echo "=========================================="
echo "All Android builds completed successfully!"
echo "=========================================="

# Show built libraries
echo ""
echo "Built libraries:"
OZZUTIL_DIR="$(cd "$SCRIPT_DIR/../ext/ozzutil" && pwd)"
if [ -d "$OZZUTIL_DIR/libs/android" ]; then
    find "$OZZUTIL_DIR/libs/android" -name "libozzutil.so" -type f | sort
else
    echo "No libraries found in $OZZUTIL_DIR/libs/android"
fi