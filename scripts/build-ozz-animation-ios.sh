#!/bin/bash
# Build script for ozz-animation library on iOS
# Usage: ./build-ozz-animation-ios.sh [architecture] [build_type] [deployment_target]
# Example: ./build-ozz-animation-ios.sh arm64 Release 12.0
# Example: ./build-ozz-animation-ios.sh x86_64 Debug 12.0  (for simulator)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OZZ_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"

# Parse arguments
ARCH="${1:-arm64}"
BUILD_TYPE="${2:-Release}"
DEPLOYMENT_TARGET="${3:-12.0}"

echo "=========================================="
echo "Building ozz-animation for iOS"
echo "Architecture: $ARCH"
echo "Build Type: $BUILD_TYPE"
echo "Deployment Target: $DEPLOYMENT_TARGET"
echo "=========================================="

# Check if ozz-animation directory exists
if [ ! -d "$OZZ_DIR" ]; then
    echo "Error: ozz-animation directory not found at $OZZ_DIR"
    echo "Please ensure the submodule is initialized: git submodule update --init --recursive"
    exit 1
fi

# Determine platform based on architecture
PLATFORM=""
PLATFORM_NAME=""
if [ "$ARCH" = "arm64" ]; then
    PLATFORM="OS64"
    PLATFORM_NAME="iphoneos"
elif [ "$ARCH" = "x86_64" ]; then
    PLATFORM="SIMULATOR64"
    PLATFORM_NAME="iphonesimulator"
else
    echo "Error: Unsupported architecture $ARCH. Supported: arm64, x86_64"
    exit 1
fi

# Determine build directory based on architecture and build type
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
BUILD_DIR="$OZZ_DIR/build-ios-$ARCH-$BUILD_TYPE_LOWER"

# Clean up existing build directory for a fresh build
echo "Cleaning build directory: $BUILD_DIR"
rm -rf "$BUILD_DIR"

# Create build directory
echo "Creating build directory: $BUILD_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake for iOS
echo "Configuring CMake for iOS..."
cmake .. \
    -G Xcode \
    -DCMAKE_SYSTEM_NAME=iOS \
    -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="$DEPLOYMENT_TARGET" \
    -DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO \
    -DCMAKE_IOS_INSTALL_COMBINED=YES \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -Dozz_build_samples=OFF \
    -Dozz_build_howtos=OFF \
    -Dozz_build_tests=OFF \
    -Dozz_build_tools=OFF \
    -DBUILD_SHARED_LIBS=OFF

# Build
echo "Building..."
cmake --build . --config "$BUILD_TYPE"

# Create destination directory
DEST_DIR="$OZZ_DIR/bin/ios/$ARCH/$BUILD_TYPE_LOWER"
mkdir -p "$DEST_DIR"

# Copy libraries to destination
echo "Copying libraries to $DEST_DIR..."
# ozz uses postfix naming: _r for release, _d for debug
POSTFIX=""
if [ "$BUILD_TYPE" = "Release" ]; then
    POSTFIX="_r"
elif [ "$BUILD_TYPE" = "Debug" ]; then
    POSTFIX="_d"
fi

cp "$BUILD_DIR/src/animation/runtime/$BUILD_TYPE-$PLATFORM_NAME/libozz_animation${POSTFIX}.a" "$DEST_DIR/libozz_animation.a" 2>/dev/null || true
cp "$BUILD_DIR/src/base/$BUILD_TYPE-$PLATFORM_NAME/libozz_base${POSTFIX}.a" "$DEST_DIR/libozz_base.a" 2>/dev/null || true
cp "$BUILD_DIR/src/geometry/runtime/$BUILD_TYPE-$PLATFORM_NAME/libozz_geometry${POSTFIX}.a" "$DEST_DIR/libozz_geometry.a" 2>/dev/null || true
cp "$BUILD_DIR/src/animation/offline/$BUILD_TYPE-$PLATFORM_NAME/libozz_animation_offline${POSTFIX}.a" "$DEST_DIR/libozz_animation_offline.a" 2>/dev/null || true
cp "$BUILD_DIR/src/options/$BUILD_TYPE-$PLATFORM_NAME/libozz_options${POSTFIX}.a" "$DEST_DIR/libozz_options.a" 2>/dev/null || true

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR"
echo "=========================================="

# Verify the libraries were created
if [ -f "$DEST_DIR/libozz_animation.a" ] && [ -f "$DEST_DIR/libozz_base.a" ]; then
    echo "✓ Successfully built ozz-animation libraries for iOS $ARCH"
    ls -lh "$DEST_DIR"/*.a
else
    echo "✗ Failed to build ozz-animation libraries"
    echo "Available files in build directory:"
    find "$BUILD_DIR" -name "*.a" -type f 2>/dev/null || echo "No .a files found"
    exit 1
fi