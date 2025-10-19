#!/bin/bash
# Build script for ozzutil library for Web/Emscripten
# Usage: ./build-ozzutil-web.sh [build_type]
# Example: ./build-ozzutil-web.sh Release
# Example: ./build-ozzutil-web.sh Debug

set -e  # Exit on any error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OZZUTIL_DIR="$SOKOL_CHARP_ROOT/ext/ozzutil"

# Parse arguments
BUILD_TYPE="${1:-Release}"

# Set Emscripten version
EMSCRIPTEN_VERSION="3.1.34"

echo "=========================================="
echo "Building ozzutil for Web/Emscripten"
echo "Build Type: $BUILD_TYPE"
echo "Emscripten Version: $EMSCRIPTEN_VERSION"
echo "=========================================="

# Check if ozzutil directory exists
if [ ! -d "$OZZUTIL_DIR" ]; then
    echo "Error: ozzutil directory not found at $OZZUTIL_DIR"
    exit 1
fi

# Check if ozz-animation libraries exist
OZZ_ANIMATION_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"
if [ ! -d "$OZZ_ANIMATION_DIR/bin/emscripten/wasm32/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')" ]; then
    echo "Error: ozz-animation libraries not found. Please build ozz-animation first:"
    echo "  ./build-ozz-animation-web.sh $BUILD_TYPE"
    exit 1
fi

# Path to local emsdk
EMSDK_PATH="$SOKOL_CHARP_ROOT/tools/emsdk/emsdk"

# Check if local emsdk exists
if [ ! -f "$EMSDK_PATH" ]; then
    echo "Error: Local emsdk not found at $EMSDK_PATH"
    echo "Please ensure the submodule is initialized: git submodule update --init --recursive"
    exit 1
fi

# Activate Emscripten environment
cd "$(dirname "$EMSDK_PATH")"
source ./emsdk_env.sh

# Switch to specific Emscripten version
./emsdk install $EMSCRIPTEN_VERSION
./emsdk activate $EMSCRIPTEN_VERSION
source ./emsdk_env.sh

echo "Using Emscripten: $(which emcc)"
echo "Emscripten version: $(emcc --version | head -n 1)"

# Build directory
BUILD_DIR="$OZZUTIL_DIR/build-emscripten"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with Emscripten
emcmake cmake .. \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DEMSCRIPTEN=1

# Build
emmake make -j$(nproc)

# Create destination directory
DEST_DIR="$OZZUTIL_DIR/libs/emscripten/x86/$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')"
mkdir -p "$DEST_DIR"

# Copy library to destination
echo "Copying library to $DEST_DIR..."
cp "$BUILD_DIR/ozzutil.a" "$DEST_DIR/ozzutil.a" 2>/dev/null || \
find "$BUILD_DIR" -name "ozzutil.a" -exec cp {} "$DEST_DIR/ozzutil.a" \; 2>/dev/null || \
cp "$BUILD_DIR/libozzutil.a" "$DEST_DIR/ozzutil.a" 2>/dev/null || \
find "$BUILD_DIR" -name "libozzutil.a" -exec cp {} "$DEST_DIR/ozzutil.a" \; 2>/dev/null || true

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR"
echo "=========================================="

# Verify the library was created
if [ -f "$DEST_DIR/ozzutil.a" ]; then
    echo "✓ Successfully built ozzutil library for Web/Emscripten"
    ls -lh "$DEST_DIR"/*.a
    
    # Show library contents
    echo ""
    echo "Library contents:"
    ar -t "$DEST_DIR/ozzutil.a" 2>/dev/null || echo "Could not list library contents"
else
    echo "✗ Failed to build ozzutil library"
    echo "Available files in build directory:"
    find "$BUILD_DIR" -name "*.a" -type f 2>/dev/null || echo "No .a files found"
    exit 1
fi