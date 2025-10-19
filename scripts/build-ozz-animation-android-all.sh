#!/bin/bash
# Build script for ozz-animation library for all Android architectures
# Usage: ./build-ozz-animation-android-all.sh [build_type] [api_level]
# Example: ./build-ozz-animation-android-all.sh Release 21
# Example: ./build-ozz-animation-android-all.sh Debug 21

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"
API_LEVEL="${2:-21}"

echo "=========================================="
echo "Building ozz-animation for all Android architectures"
echo "Build Type: $BUILD_TYPE"
echo "API Level: $API_LEVEL"
echo "=========================================="

# Android architectures to build
ARCHITECTURES=("armeabi-v7a" "arm64-v8a" "x86" "x86_64")

for ARCH in "${ARCHITECTURES[@]}"; do
    echo ""
    echo "Building for Android $ARCH..."
    "$SCRIPT_DIR/build-ozz-animation-android.sh" "$ARCH" "$BUILD_TYPE" "$API_LEVEL"
done

echo ""
echo "=========================================="
echo "All Android builds completed successfully!"
echo "=========================================="