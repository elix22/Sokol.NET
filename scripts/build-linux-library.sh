#!/bin/bash
# Build script for Linux using GCC/Clang

set -e  # Exit on any error

# Get architecture
ARCH=$(uname -m)

# Clean up any existing build directories
echo "Cleaning up build directories..."
rm -rf build-linux

# Create build directory
mkdir -p build-linux
cd build-linux

# Configure with CMake
echo "Configuring CMake..."
cmake ../ext -DCMAKE_BUILD_TYPE=Release

# Build Debug
echo "========================================="
echo "Building Debug configuration..."
echo "========================================="
cmake ../ext -DCMAKE_BUILD_TYPE=Debug
cmake --build . --config Debug

# Create debug directory
mkdir -p "../libs/linux/$ARCH/debug"

# Copy Debug build
echo "Copying Debug build..."
cp libsokol.so "../libs/linux/$ARCH/debug/"

# Build Release
echo "========================================="
echo "Building Release configuration..."
echo "========================================="
cmake ../ext -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release

# Create release directory
mkdir -p "../libs/linux/$ARCH/release"

# Copy Release build
echo "Copying Release build..."
cp libsokol.so "../libs/linux/$ARCH/release/"

cd ..

# Clean up build directories
echo "Cleaning up build directories..."
rm -rf build-linux

echo "========================================="
echo "Build completed successfully!"
echo "Debug build: libs/linux/$ARCH/debug/libsokol.so"
echo "Release build: libs/linux/$ARCH/release/libsokol.so"
echo "========================================="
