#!/bin/bash
# Build script for ozz-animation library for Web/Emscripten
# Usage: ./build-ozz-animation-web.sh [build_type]
# Example: ./build-ozz-animation-web.sh Release
# Example: ./build-ozz-animation-web.sh Debug

set -e  # Exit on any error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OZZ_DIR="$SOKOL_CHARP_ROOT/ext/ozz-animation"

# Parse arguments
BUILD_TYPE="${1:-Release}"

# Set Emscripten version
EMSCRIPTEN_VERSION="3.1.34"

echo "=========================================="
echo "Building ozz-animation for Web/Emscripten"
echo "Build Type: $BUILD_TYPE"
echo "Emscripten Version: $EMSCRIPTEN_VERSION"
echo "=========================================="

# Check if ozz-animation directory exists
if [ ! -d "$OZZ_DIR" ]; then
    echo "Error: ozz-animation directory not found at $OZZ_DIR"
    echo "Please ensure the submodule is initialized: git submodule update --init --recursive"
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

# Make emsdk executable if it isn't already
chmod +x "$EMSDK_PATH"

# Activate Emscripten SDK with the specified version
echo "Installing Emscripten SDK version $EMSCRIPTEN_VERSION..."
"$EMSDK_PATH" install "$EMSCRIPTEN_VERSION"

echo "Activating Emscripten SDK version $EMSCRIPTEN_VERSION..."
"$EMSDK_PATH" activate "$EMSCRIPTEN_VERSION"

# Set up environment variables for Emscripten
echo "Setting up Emscripten environment..."
source "$SOKOL_CHARP_ROOT/tools/emsdk/emsdk_env.sh"

echo "Using Emscripten: $(emcc --version | head -n 1)"

# Determine build directory based on build type
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
BUILD_DIR="$OZZ_DIR/build-emscripten-$BUILD_TYPE_LOWER"

# Clean up existing build directory for a fresh build
echo "Cleaning build directory: $BUILD_DIR"
rm -rf "$BUILD_DIR"

# Create build directory
echo "Creating build directory: $BUILD_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake using Emscripten toolchain
echo "Configuring CMake..."
emcmake cmake .. \
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
DEST_DIR="$OZZ_DIR/bin/emscripten/wasm32/$BUILD_TYPE_LOWER"
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