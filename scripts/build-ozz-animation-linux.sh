#!/bin/bash
# Build script for ozz-animation library on Linux
# Usage: ./build-ozz-animation-linux.sh [build_type]
# Example: ./build-ozz-animation-linux.sh Release
# Example: ./build-ozz-animation-linux.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OZZ_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"
BUILD_DIR="$OZZ_DIR/build-linux"

# Parse arguments
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building ozz-animation for Linux"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Check if ozz-animation directory exists
if [ ! -d "$OZZ_DIR" ]; then
    echo "Error: ozz-animation directory not found at $OZZ_DIR"
    echo "Please ensure the submodule is initialized: git submodule update --init --recursive"
    exit 1
fi

# Clean up existing build directory for a fresh build
echo "Cleaning build directory: $BUILD_DIR"
rm -rf "$BUILD_DIR"

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -Dozz_build_samples=OFF \
    -Dozz_build_howtos=OFF \
    -Dozz_build_tests=OFF \
    -Dozz_build_tools=OFF \
    -DBUILD_SHARED_LIBS=OFF

# Build
cmake --build . --config "$BUILD_TYPE" -- -j$(nproc)

# Create destination directory
DEST_DIR="$OZZ_DIR/bin/linux/x64/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')"
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

cp "$BUILD_DIR/src/animation/runtime/libozz_animation${POSTFIX}.a" "$DEST_DIR/libozz_animation.a" 2>/dev/null || true
cp "$BUILD_DIR/src/base/libozz_base${POSTFIX}.a" "$DEST_DIR/libozz_base.a" 2>/dev/null || true
cp "$BUILD_DIR/src/geometry/runtime/libozz_geometry${POSTFIX}.a" "$DEST_DIR/libozz_geometry.a" 2>/dev/null || true
cp "$BUILD_DIR/src/animation/offline/libozz_animation_offline${POSTFIX}.a" "$DEST_DIR/libozz_animation_offline.a" 2>/dev/null || true
cp "$BUILD_DIR/src/options/libozz_options${POSTFIX}.a" "$DEST_DIR/libozz_options.a" 2>/dev/null || true

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR"
echo "=========================================="

# Verify the libraries were created
if [ -f "$DEST_DIR/libozz_animation.a" ] && [ -f "$DEST_DIR/libozz_base.a" ]; then
    echo "✓ Successfully built ozz-animation libraries"
    ls -lh "$DEST_DIR"/*.a
else
    echo "✗ Failed to build ozz-animation libraries"
    echo "Available files in build directory:"
    find "$BUILD_DIR" -name "*.a" -type f 2>/dev/null || echo "No .a files found"
    exit 1
fi