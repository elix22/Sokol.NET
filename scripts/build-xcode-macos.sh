#!/bin/bash

# Get architecture
ARCH=$(uname -m)
rm -rf build-xcode-macos
mkdir -p build-xcode-macos
cd build-xcode-macos
# Configure and build
cmake ../ext -GXcode
cmake --build . --config Release

# Create directory
mkdir -p "../NativeLibs/macos/$ARCH"
cp -f Release/libsokol.dylib ../NativeLibs/macos/$ARCH/libsokol.dylib
cd ..
rm -rf build-xcode-macos
