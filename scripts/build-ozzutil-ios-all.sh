#!/bin/bash
# Build script for ozzutil framework for all iOS architectures
# Usage: ./build-ozzutil-ios-all.sh [build_type]
# Example: ./build-ozzutil-ios-all.sh Release
# Example: ./build-ozzutil-ios-all.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building ozzutil for all iOS architectures"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# iOS architectures to build
ARCHITECTURES=("arm64" "x86_64")

for ARCH in "${ARCHITECTURES[@]}"; do
    echo ""
    if [ "$ARCH" = "arm64" ]; then
        echo "Building for iOS Device ($ARCH)..."
    else
        echo "Building for iOS Simulator ($ARCH)..."
    fi
    "$SCRIPT_DIR/build-ozzutil-ios.sh" "$ARCH" "$BUILD_TYPE"
done

echo ""
echo "=========================================="
echo "All iOS builds completed successfully!"
echo "=========================================="

# Show built frameworks
echo ""
echo "Built frameworks:"
OZZUTIL_DIR="$(cd "$SCRIPT_DIR/../ext/ozzutil" && pwd)"
if [ -d "$OZZUTIL_DIR/libs/ios" ]; then
    find "$OZZUTIL_DIR/libs/ios" -name "ozzutil.framework" -type d | sort
else
    echo "No frameworks found in $OZZUTIL_DIR/libs/ios"
fi