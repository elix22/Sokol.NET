#!/bin/bash
# Build script for spine-c library for Web/Emscripten
# Usage: ./build-spine-c-web.sh [build_type]
# Example: ./build-spine-c-web.sh Release
# Example: ./build-spine-c-web.sh Debug

set -e  # Exit on any error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SPINE_C_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SOKOL_CHARP_ROOT="$(cd "$SPINE_C_DIR/../.." && pwd)"

# Parse arguments
BUILD_TYPE="${1:-Release}"

# Set Emscripten version
EMSCRIPTEN_VERSION="3.1.34"

echo "=========================================="
echo "Building spine-c for Web/Emscripten"
echo "Build Type: $BUILD_TYPE"
echo "Emscripten Version: $EMSCRIPTEN_VERSION"
echo "=========================================="

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
BUILD_DIR="$SPINE_C_DIR/build-emscripten-$BUILD_TYPE_LOWER"

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
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE"

# Build
echo "Building..."
cmake --build . --config "$BUILD_TYPE"

echo "=========================================="
echo "Build complete!"
echo "Output: $BUILD_DIR/spine-c.a"
echo "=========================================="

# Verify the library was created
if [ -f "$BUILD_DIR/spine-c.a" ]; then
    echo "✓ Successfully built spine-c.a"
    ls -lh "$BUILD_DIR/spine-c.a"
else
    echo "✗ Failed to build spine-c.a"
    exit 1
fi
