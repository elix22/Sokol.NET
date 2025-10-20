
#!/bin/bash

# Build script for sokol framework on iOS - builds both Release and Debug configurations
# Usage: ./build-ios-sokol-library.sh

set -e

echo "=========================================="
echo "Building sokol framework for iOS (arm64)"
echo "Building both Release and Debug configurations"
echo "=========================================="

# Get architecture
ARCH=$(uname -m)

# Clean previous build
rm -rf build-xcode-ios
mkdir -p build-xcode-ios
cd build-xcode-ios

# Configure CMake for iOS
echo "Configuring CMake for iOS..."
cmake -G Xcode \
    -DCMAKE_SYSTEM_NAME=iOS \
    -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 \
    -DCMAKE_OSX_ARCHITECTURES="arm64" \
    ../ext

# Build Release configuration
echo "Building Release configuration..."
cmake --build . --config Release

# Build Debug configuration  
echo "Building Debug configuration..."
cmake --build . --config Debug

# Create output directories
mkdir -p "../libs/ios/arm64/release"
mkdir -p "../libs/ios/arm64/debug"

# Copy Release framework
echo "Copying Release framework..."
cp -rf Release-iphoneos/sokol.framework ../libs/ios/arm64/release/

# Copy Debug framework
echo "Copying Debug framework..."
cp -rf Debug-iphoneos/sokol.framework ../libs/ios/arm64/debug/

# Cleanup
cd ..
rm -rf build-xcode-ios

echo "=========================================="
echo "Build complete!"
echo "Release framework: libs/ios/arm64/release/sokol.framework"
echo "Debug framework: libs/ios/arm64/debug/sokol.framework"
echo "=========================================="

# Verify frameworks were created
if [ -d "libs/ios/arm64/release/sokol.framework" ]; then
    echo "✓ Release framework created successfully"
    ls -lah "libs/ios/arm64/release/sokol.framework"
else
    echo "✗ Failed to create Release framework"
fi

if [ -d "libs/ios/arm64/debug/sokol.framework" ]; then
    echo "✓ Debug framework created successfully"  
    ls -lah "libs/ios/arm64/debug/sokol.framework"
else
    echo "✗ Failed to create Debug framework"
fi
