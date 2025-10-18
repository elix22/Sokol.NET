#!/bin/bash
# Build script for spine-c library for iOS
# Usage: ./build-spine-c-ios.sh [sdk] [build_type]
# Example: ./build-spine-c-ios.sh iphoneos Release
# Example: ./build-spine-c-ios.sh iphonesimulator Debug
# SDKs: iphoneos (device), iphonesimulator (simulator)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SPINE_C_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Parse arguments
SDK="${1:-iphoneos}"
BUILD_TYPE="${2:-Release}"
BUILD_DIR="$SPINE_C_DIR/build-xcode-ios-$SDK"

echo "=========================================="
echo "Building spine-c for iOS"
echo "SDK: $SDK"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Determine architecture based on SDK
if [ "$SDK" = "iphoneos" ]; then
    ARCH="arm64"
else
    ARCH="x86_64;arm64"  # Simulator supports both
fi

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -G Xcode \
    -DCMAKE_SYSTEM_NAME=iOS \
    -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
    -DCMAKE_OSX_SYSROOT="$SDK" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="13.0" \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DIOS=TRUE

# Build
cmake --build . --config "$BUILD_TYPE"

echo "=========================================="
echo "Build complete!"
echo "Output: $BUILD_DIR/$BUILD_TYPE/libspine-c.a"
echo "=========================================="

# Verify the library was created
if [ -f "$BUILD_DIR/$BUILD_TYPE-$SDK/libspine-c.a" ]; then
    echo "✓ Successfully built libspine-c.a for iOS ($SDK)"
    file "$BUILD_DIR/$BUILD_TYPE-$SDK/libspine-c.a"
elif [ -f "$BUILD_DIR/$BUILD_TYPE/libspine-c.a" ]; then
    echo "✓ Successfully built libspine-c.a for iOS ($SDK)"
    file "$BUILD_DIR/$BUILD_TYPE/libspine-c.a"
else
    echo "✗ Failed to build libspine-c.a"
    exit 1
fi
