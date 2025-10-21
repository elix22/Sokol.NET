#!/bin/bash
# Build script for assimp library for Web/Emscripten
# Usage: ./build-assimp-web.sh [build_type]
# Example: ./build-assimp-web.sh Release
# Example: ./build-assimp-web.sh Debug

set -e  # Exit on any error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSIMP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SOKOL_CHARP_ROOT="$(cd "$ASSIMP_DIR/../.." && pwd)"

# Parse arguments
BUILD_TYPE="${1:-Release}"

# Set Emscripten version
EMSCRIPTEN_VERSION="3.1.34"

echo "=========================================="
echo "Building assimp for Web/Emscripten"
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
BUILD_DIR="$ASSIMP_DIR/build-emscripten-$BUILD_TYPE_LOWER"

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
    -DBUILD_SHARED_LIBS=OFF \
    -DASSIMP_BUILD_TESTS=OFF \
    -DASSIMP_BUILD_ASSIMP_TOOLS=OFF \
    -DASSIMP_BUILD_SAMPLES=OFF \
    -DASSIMP_BUILD_ZLIB=ON \
    -DASSIMP_NO_EXPORT=ON \
    -DASSIMP_BUILD_ALL_IMPORTERS_BY_DEFAULT=OFF \
    -DASSIMP_BUILD_OBJ_IMPORTER=ON \
    -DASSIMP_BUILD_FBX_IMPORTER=ON \
    -DASSIMP_BUILD_GLTF_IMPORTER=ON \
    -DASSIMP_BUILD_COLLADA_IMPORTER=ON

# Build
echo "Building..."
cmake --build . --config "$BUILD_TYPE"

echo "=========================================="
echo "Build complete!"
echo "Output: $BUILD_DIR/lib/libassimp.a"
echo "=========================================="

# Verify the library was created
if [ -f "$BUILD_DIR/lib/libassimp.a" ]; then
    echo "✓ Successfully built libassimp.a"
    ls -lh "$BUILD_DIR/lib/libassimp.a"
else
    echo "✗ Failed to build libassimp.a"
    exit 1
fi

# Copy to libs folder
LIBS_DIR="$SCRIPT_DIR/../libs/emscripten/x86"
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
OUTPUT_DIR="$LIBS_DIR/$BUILD_TYPE_LOWER"

echo ""
echo "=========================================="
echo "Copying library to libs folder..."
echo "=========================================="

# Create output directory if it doesn't exist
mkdir -p "$OUTPUT_DIR"

# Copy the library without the "lib" prefix (WebAssembly expects "assimp.a" not "libassimp.a")
cp "$BUILD_DIR/lib/libassimp.a" "$OUTPUT_DIR/assimp.a"

# Verify the copy
if [ -f "$OUTPUT_DIR/assimp.a" ]; then
    echo "✓ Successfully copied to $OUTPUT_DIR/assimp.a"
    ls -lh "$OUTPUT_DIR/assimp.a"
else
    echo "✗ Failed to copy library"
    exit 1
fi

echo ""
echo "=========================================="
echo "Build and installation complete!"
echo "=========================================="
