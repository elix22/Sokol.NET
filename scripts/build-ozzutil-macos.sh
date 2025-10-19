#!/bin/bash
# Build script for ozzutil library on macOS
# Usage: ./build-ozzutil-macos.sh [architecture] [build_type]
# Example: ./build-ozzutil-macos.sh arm64 Release
# Example: ./build-ozzutil-macos.sh x86_64 Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OZZUTIL_DIR="$SOKOL_CHARP_ROOT/ext/ozzutil"
BUILD_DIR="$OZZUTIL_DIR/build-xcode-macos"

# Parse arguments
ARCH="${1:-arm64}"
BUILD_TYPE="${2:-Release}"

echo "=========================================="
echo "Building ozzutil for macOS"
echo "Architecture: $ARCH"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Check if ozzutil directory exists
if [ ! -d "$OZZUTIL_DIR" ]; then
    echo "Error: ozzutil directory not found at $OZZUTIL_DIR"
    exit 1
fi

# Check if ozz-animation libraries exist
OZZ_ANIMATION_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"
if [ ! -d "$OZZ_ANIMATION_DIR/bin/macos/$ARCH/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')" ]; then
    echo "Error: ozz-animation libraries not found. Please build ozz-animation first:"
    echo "  ./build-ozz-animation-macos.sh $ARCH $BUILD_TYPE"
    exit 1
fi

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -G Xcode \
    -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="11.0"

# Build
cmake --build . --config "$BUILD_TYPE"

# Create destination directory
DEST_DIR="$OZZUTIL_DIR/libs/macos/$ARCH/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')"
mkdir -p "$DEST_DIR"

# Copy library to destination
echo "Copying library to $DEST_DIR..."
cp "$BUILD_DIR/$BUILD_TYPE/libozzutil.dylib" "$DEST_DIR/libozzutil.dylib" 2>/dev/null || \
cp "$BUILD_DIR/libozzutil.dylib" "$DEST_DIR/libozzutil.dylib" 2>/dev/null || \
cp "$BUILD_DIR/Debug/libozzutil.dylib" "$DEST_DIR/libozzutil.dylib" 2>/dev/null || true

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR"
echo "=========================================="

# Verify the library was created
if [ -f "$DEST_DIR/libozzutil.dylib" ]; then
    echo "✓ Successfully built ozzutil library"
    ls -lh "$DEST_DIR"/*.dylib
    
    # Sign the library for macOS
    codesign --force --sign - "$DEST_DIR/libozzutil.dylib"
    echo "✓ Library signed successfully"
else
    echo "✗ Failed to build ozzutil library"
    echo "Available files in build directory:"
    find "$BUILD_DIR" -name "*.dylib" -type f 2>/dev/null || echo "No .dylib files found"
    exit 1
fi