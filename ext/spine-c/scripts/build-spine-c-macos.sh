#!/bin/bash
# Build script for spine-c library on macOS
# Usage: ./build-spine-c-macos.sh [architecture] [build_type]
# Example: ./build-spine-c-macos.sh arm64 Release
# Example: ./build-spine-c-macos.sh x86_64 Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SPINE_C_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$SPINE_C_DIR/build-xcode-macos"

# Parse arguments
ARCH="${1:-arm64}"
BUILD_TYPE="${2:-Release}"

echo "=========================================="
echo "Building spine-c for macOS"
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
    -DCMAKE_OSX_DEPLOYMENT_TARGET="11.0"

# Build
cmake --build . --config "$BUILD_TYPE"

echo "=========================================="
echo "Build complete!"
echo "Output: $BUILD_DIR/$BUILD_TYPE/libspine-c.dylib"
echo "=========================================="

# Verify the library was created
if [ -f "$BUILD_DIR/$BUILD_TYPE/libspine-c.dylib" ]; then
    echo "✓ Successfully built libspine-c.dylib"
    file "$BUILD_DIR/$BUILD_TYPE/libspine-c.dylib"
else
    echo "✗ Failed to build libspine-c.dylib"
    exit 1
fi
