#!/bin/bash
# Build script for ozz-animation library on Android
# Usage: ./build-ozz-animation-android.sh [architecture] [build_type] [api_level]
# Example: ./build-ozz-animation-android.sh arm64-v8a Release 21
# Example: ./build-ozz-animation-android.sh armeabi-v7a Debug 21

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OZZ_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"

# Parse arguments
ARCH="${1:-arm64-v8a}"
BUILD_TYPE="${2:-Release}"
API_LEVEL="${3:-21}"

echo "=========================================="
echo "Building ozz-animation for Android"
echo "Architecture: $ARCH"
echo "Build Type: $BUILD_TYPE"
echo "API Level: $API_LEVEL"
echo "=========================================="

# Check if ozz-animation directory exists
if [ ! -d "$OZZ_DIR" ]; then
    echo "Error: ozz-animation directory not found at $OZZ_DIR"
    echo "Please ensure the submodule is initialized: git submodule update --init --recursive"
    exit 1
fi

# Check if Android NDK is available
if [ -z "$ANDROID_NDK_ROOT" ] && [ -z "$ANDROID_NDK_HOME" ] && [ -z "$NDK_ROOT" ]; then
    echo "Error: Android NDK not found. Please set ANDROID_NDK_ROOT, ANDROID_NDK_HOME, or NDK_ROOT environment variable"
    exit 1
fi

# Set NDK path (try different common environment variables)
NDK_PATH=""
if [ ! -z "$ANDROID_NDK_ROOT" ]; then
    NDK_PATH="$ANDROID_NDK_ROOT"
elif [ ! -z "$ANDROID_NDK_HOME" ]; then
    NDK_PATH="$ANDROID_NDK_HOME"
elif [ ! -z "$NDK_ROOT" ]; then
    NDK_PATH="$NDK_ROOT"
fi

echo "Using Android NDK: $NDK_PATH"

# Determine build directory based on architecture and build type
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
BUILD_DIR="$OZZ_DIR/build-android-$ARCH-$BUILD_TYPE_LOWER"

# Clean up existing build directory for a fresh build
echo "Cleaning build directory: $BUILD_DIR"
rm -rf "$BUILD_DIR"

# Create build directory
echo "Creating build directory: $BUILD_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake using Android NDK toolchain
echo "Configuring CMake for Android..."
cmake .. \
    -DCMAKE_TOOLCHAIN_FILE="$NDK_PATH/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="$ARCH" \
    -DANDROID_NATIVE_API_LEVEL="$API_LEVEL" \
    -DANDROID_STL=c++_shared \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -Dozz_build_samples=OFF \
    -Dozz_build_howtos=OFF \
    -Dozz_build_tests=OFF \
    -Dozz_build_tools=OFF \
    -DBUILD_SHARED_LIBS=OFF

# Build
echo "Building..."
cmake --build . --config "$BUILD_TYPE" -- -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)

# Create destination directory
DEST_DIR="$OZZ_DIR/bin/android/$ARCH/$BUILD_TYPE_LOWER"
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
    echo "✓ Successfully built ozz-animation libraries for Android $ARCH"
    ls -lh "$DEST_DIR"/*.a
else
    echo "✗ Failed to build ozz-animation libraries"
    echo "Available files in build directory:"
    find "$BUILD_DIR" -name "*.a" -type f 2>/dev/null || echo "No .a files found"
    exit 1
fi