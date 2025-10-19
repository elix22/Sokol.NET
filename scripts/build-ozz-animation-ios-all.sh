#!/bin/bash
# Build script for ozz-animation library for all iOS architectures
# Usage: ./build-ozz-animation-ios-all.sh [build_type] [deployment_target]
# Example: ./build-ozz-animation-ios-all.sh Release 12.0
# Example: ./build-ozz-animation-ios-all.sh Debug 12.0

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"
DEPLOYMENT_TARGET="${2:-12.0}"

echo "=========================================="
echo "Building ozz-animation for all iOS architectures"
echo "Build Type: $BUILD_TYPE"
echo "Deployment Target: $DEPLOYMENT_TARGET"
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
    "$SCRIPT_DIR/build-ozz-animation-ios.sh" "$ARCH" "$BUILD_TYPE" "$DEPLOYMENT_TARGET"
done

echo ""
echo "=========================================="
echo "All iOS builds completed successfully!"
echo "=========================================="