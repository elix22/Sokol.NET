#!/bin/bash
# Build script for assimp library for Linux
# Usage: ./build-assimp-linux.sh [build_type]
# Example: ./build-assimp-linux.sh Release
# Example: ./build-assimp-linux.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSIMP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Parse arguments
BUILD_TYPE="${1:-Release}"
BUILD_DIR="$ASSIMP_DIR/build-linux"

echo "=========================================="
echo "Building assimp for Linux"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DBUILD_SHARED_LIBS=ON \
    -DASSIMP_BUILD_TESTS=OFF \
    -DASSIMP_BUILD_ASSIMP_TOOLS=OFF \
    -DASSIMP_BUILD_SAMPLES=OFF \
    -DASSIMP_BUILD_ZLIB=OFF \
    -DASSIMP_NO_EXPORT=ON \
    -DASSIMP_BUILD_ALL_IMPORTERS_BY_DEFAULT=OFF \
    -DASSIMP_BUILD_OBJ_IMPORTER=ON \
    -DASSIMP_BUILD_FBX_IMPORTER=ON \
    -DASSIMP_BUILD_GLTF_IMPORTER=ON \
    -DASSIMP_BUILD_COLLADA_IMPORTER=ON

# Build
cmake --build . --config "$BUILD_TYPE" -- -j$(nproc)

# Create destination directory
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
DEST_DIR="$ASSIMP_DIR/libs/linux/x86_64/$BUILD_TYPE_LOWER"
mkdir -p "$DEST_DIR"

# Copy library to destination
echo "Copying library to $DEST_DIR..."
LIB_PATH="$BUILD_DIR/bin/libassimp.so"

if [ -f "$LIB_PATH" ]; then
    cp -P "$BUILD_DIR/bin/"libassimp.so* "$DEST_DIR/"
else
    # Try alternative location
    LIB_PATH="$BUILD_DIR/libassimp.so"
    if [ -f "$LIB_PATH" ]; then
        cp -P "$BUILD_DIR/"libassimp.so* "$DEST_DIR/"
    else
        echo "Error: libassimp.so not found"
        echo "Searched paths:"
        echo "  - $BUILD_DIR/bin/libassimp.so"
        echo "  - $BUILD_DIR/libassimp.so"
        exit 1
    fi
fi

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR/libassimp.so"
echo "=========================================="

# Verify the library was created
if [ -f "$DEST_DIR/libassimp.so" ]; then
    echo "✓ Successfully built libassimp.so"
    file "$DEST_DIR/libassimp.so"
    ls -lh "$DEST_DIR"
else
    echo "✗ Failed to build libassimp.so"
    exit 1
fi
