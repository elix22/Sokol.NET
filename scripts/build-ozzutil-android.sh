#!/bin/bash
# Build script for ozzutil library on Android
# Usage: ./build-ozzutil-android.sh [abi] [build_type]
# Example: ./build-ozzutil-android.sh arm64-v8a Release
# Example: ./build-ozzutil-android.sh armeabi-v7a Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OZZUTIL_DIR="$SOKOL_CHARP_ROOT/ext/ozzutil"
BUILD_DIR="$OZZUTIL_DIR/build-android"

# Parse arguments
ABI="${1:-arm64-v8a}"
BUILD_TYPE="${2:-Release}"

echo "=========================================="
echo "Building ozzutil for Android"
echo "ABI: $ABI"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Check if ozzutil directory exists
if [ ! -d "$OZZUTIL_DIR" ]; then
    echo "Error: ozzutil directory not found at $OZZUTIL_DIR"
    exit 1
fi

# Check if ozz-animation libraries exist
OZZ_ANIMATION_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"
if [ ! -d "$OZZ_ANIMATION_DIR/bin/android/$ABI/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')" ]; then
    echo "Error: ozz-animation libraries not found. Please build ozz-animation first:"
    echo "  ./build-ozz-animation-android.sh $ABI $BUILD_TYPE"
    exit 1
fi

# Find Android NDK
ANDROID_NDK_ROOT=""

# Check common NDK locations
NDK_LOCATIONS=(
    "$ANDROID_HOME/ndk-bundle"
    "$ANDROID_HOME/ndk/25.1.8937393"
    "$ANDROID_HOME/ndk/24.0.8215888"
    "$ANDROID_HOME/ndk/23.2.8568313"
    "$ANDROID_NDK_HOME"
    "$ANDROID_NDK"
    "/opt/android-ndk"
    "/usr/local/android-ndk"
)

for ndk_path in "${NDK_LOCATIONS[@]}"; do
    if [ -d "$ndk_path" ] && [ -f "$ndk_path/build/cmake/android.toolchain.cmake" ]; then
        ANDROID_NDK_ROOT="$ndk_path"
        break
    fi
done

if [ -z "$ANDROID_NDK_ROOT" ]; then
    echo "Error: Android NDK not found. Please set ANDROID_NDK_HOME or ANDROID_HOME environment variable"
    exit 1
fi

echo "Using Android NDK: $ANDROID_NDK_ROOT"

# Create build directory
BUILD_DIR_ABI="$BUILD_DIR/$ABI"
rm -rf "$BUILD_DIR_ABI"
mkdir -p "$BUILD_DIR_ABI"
cd "$BUILD_DIR_ABI"

# Configure with CMake for Android
cmake ../.. \
    -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK_ROOT/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="$ABI" \
    -DANDROID_PLATFORM=android-21 \
    -DANDROID_STL=c++_shared \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DANDROID=TRUE

# Build
make -j$(nproc)

# Create destination directory
DEST_DIR="$OZZUTIL_DIR/libs/android/$ABI/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')"
mkdir -p "$DEST_DIR"

# Copy library to destination
echo "Copying library to $DEST_DIR..."
cp "$BUILD_DIR_ABI/libozzutil.so" "$DEST_DIR/libozzutil.so" 2>/dev/null || \
find "$BUILD_DIR_ABI" -name "libozzutil.so" -exec cp {} "$DEST_DIR/libozzutil.so" \; 2>/dev/null || true

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR"
echo "=========================================="

# Verify the library was created
if [ -f "$DEST_DIR/libozzutil.so" ]; then
    echo "✓ Successfully built ozzutil library for Android $ABI"
    ls -lh "$DEST_DIR"/*.so
    
    # Show library info
    echo ""
    echo "Library info:"
    file "$DEST_DIR/libozzutil.so"
    
    # Check dependencies (if readelf is available)
    if command -v readelf >/dev/null 2>&1; then
        echo ""
        echo "Library dependencies:"
        readelf -d "$DEST_DIR/libozzutil.so" 2>/dev/null | grep NEEDED || echo "Could not read dependencies"
    fi
else
    echo "✗ Failed to build ozzutil library"
    echo "Available files in build directory:"
    find "$BUILD_DIR_ABI" -name "*.so" -type f 2>/dev/null || echo "No .so files found"
    exit 1
fi