#!/bin/bash
# Build script for spine-c on macOS using Xcode
# Builds both Debug and Release configurations

set -e  # Exit on any error

# Get the directory where the script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
SPINE_C_DIR="${SCRIPT_DIR}/../examples/spine_simple/libs/spine-c"

# Change to spine-c directory
cd "${SPINE_C_DIR}"

# Get architecture
ARCH=$(uname -m)
echo "Building for architecture: ${ARCH}"

# Clean up any existing build directory
rm -rf build-xcode-macos
mkdir -p build-xcode-macos
cd build-xcode-macos

# Configure with Xcode generator
echo "========================================="
echo "Configuring CMake project..."
echo "========================================="
cmake .. -GXcode

# Build Debug configuration
echo "========================================="
echo "Building Debug configuration..."
echo "========================================="
cmake --build . --config Debug

# Build Release configuration
echo "========================================="
echo "Building Release configuration..."
echo "========================================="
cmake --build . --config Release

echo "========================================="
echo "Build completed successfully!"
echo "Debug build: ../bin/macos/${ARCH}/debug/libspine-c.dylib"
echo "Release build: ../bin/macos/${ARCH}/release/libspine-c.dylib"
echo "========================================="
echo "Note: Libraries are automatically copied by CMake post-build commands"

# Clean up build directory
cd ..
rm -rf build-xcode-macos

echo "Done!"
