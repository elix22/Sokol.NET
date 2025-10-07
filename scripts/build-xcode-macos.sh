#!/bin/bash

# Get architecture
ARCH=$(uname -m)
rm -rf build-xcode-macos
mkdir -p build-xcode-macos
cd build-xcode-macos

# Configure
cmake ../ext -GXcode

# Build Debug
echo "Building Debug configuration..."
cmake --build . --config Debug

# Build Release
echo "Building Release configuration..."
cmake --build . --config Release

echo "========================================="
echo "Build completed successfully!"
echo "Debug build: ../libs/macos/$ARCH/debug/libsokol.dylib"
echo "Release build: ../libs/macos/$ARCH/release/libsokol.dylib"
echo "========================================="
echo "Note: Libraries are automatically copied by CMake post-build commands"

cd ..
rm -rf build-xcode-macos
