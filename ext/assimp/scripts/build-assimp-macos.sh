#!/bin/bash
# Build script for assimp library on macOS
# Usage: ./build-assimp-macos.sh [architecture] [build_type]
# Example: ./build-assimp-macos.sh arm64 Release
# Example: ./build-assimp-macos.sh x86_64 Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSIMP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$ASSIMP_DIR/build-xcode-macos"

# Parse arguments
ARCH="${1:-arm64}"
BUILD_TYPE="${2:-Release}"

echo "=========================================="
echo "Building assimp for macOS"
echo "Architecture: $ARCH"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -G Xcode \
    -DCMAKE_OSX_ARCHITECTURES="$ARCH" \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DCMAKE_OSX_DEPLOYMENT_TARGET="11.0" \
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
cmake --build . --config "$BUILD_TYPE"

# Create destination directory
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
DEST_DIR="$ASSIMP_DIR/libs/macos/$ARCH/$BUILD_TYPE_LOWER"
mkdir -p "$DEST_DIR"

# Copy library to destination (actual file, not symlink)
echo "Copying library to $DEST_DIR..."

# Find the actual versioned library file (not symlinks)
VERSIONED_LIB=$(find "$BUILD_DIR/bin/$BUILD_TYPE" -name "libassimp.[0-9]*.dylib" -type f 2>/dev/null | head -n 1)

if [ -z "$VERSIONED_LIB" ]; then
    # Try alternative location
    VERSIONED_LIB=$(find "$BUILD_DIR/$BUILD_TYPE" -name "libassimp.[0-9]*.dylib" -type f 2>/dev/null | head -n 1)
fi

if [ -n "$VERSIONED_LIB" ] && [ -f "$VERSIONED_LIB" ]; then
    # Copy the actual library file as libassimp.dylib (without version)
    cp "$VERSIONED_LIB" "$DEST_DIR/libassimp.dylib"
    echo "Copied $(basename "$VERSIONED_LIB") as libassimp.dylib"
else
    # Fallback: try to copy libassimp.dylib directly
    LIB_PATH="$BUILD_DIR/bin/$BUILD_TYPE/libassimp.dylib"
    if [ -f "$LIB_PATH" ]; then
        cp "$LIB_PATH" "$DEST_DIR/libassimp.dylib"
    else
        LIB_PATH="$BUILD_DIR/$BUILD_TYPE/libassimp.dylib"
        if [ -f "$LIB_PATH" ]; then
            cp "$LIB_PATH" "$DEST_DIR/libassimp.dylib"
        else
            echo "Error: libassimp.dylib not found"
            echo "Searched paths:"
            echo "  - $BUILD_DIR/bin/$BUILD_TYPE/libassimp*.dylib"
            echo "  - $BUILD_DIR/$BUILD_TYPE/libassimp*.dylib"
            exit 1
        fi
    fi
fi

echo "=========================================="
echo "Build complete!"
echo "Output: $DEST_DIR/libassimp.dylib"
echo "=========================================="

# Verify the library was created
if [ -f "$DEST_DIR/libassimp.dylib" ]; then
    echo "✓ Successfully built libassimp.dylib"
    file "$DEST_DIR/libassimp.dylib"
    ls -lh "$DEST_DIR"
else
    echo "✗ Failed to build libassimp.dylib"
    exit 1
fi
