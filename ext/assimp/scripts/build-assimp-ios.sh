#!/bin/bash
# Build script for assimp library for iOS
# Builds a dynamic framework for iOS device only
# Usage: ./build-assimp-ios.sh [build_type]
# Example: ./build-assimp-ios.sh Release
# Example: ./build-assimp-ios.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSIMP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Parse arguments
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building assimp for iOS (device only)"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Check if we're on macOS
if [ "$(uname)" != "Darwin" ]; then
    echo "Error: iOS builds require macOS"
    exit 1
fi

# Build for device (arm64)
echo ""
echo "Building for iOS device (arm64)..."
BUILD_DIR_DEVICE="$ASSIMP_DIR/build-ios-device"
rm -rf "$BUILD_DIR_DEVICE"
mkdir -p "$BUILD_DIR_DEVICE"
cd "$BUILD_DIR_DEVICE"

cmake .. \
    -G Xcode \
    -DCMAKE_SYSTEM_NAME=iOS \
    -DCMAKE_OSX_DEPLOYMENT_TARGET=13.0 \
    -DCMAKE_OSX_ARCHITECTURES=arm64 \
    -DCMAKE_XCODE_ATTRIBUTE_ONLY_ACTIVE_ARCH=NO \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DBUILD_SHARED_LIBS=ON \
    -DASSIMP_BUILD_FRAMEWORK=OFF \
    -DASSIMP_BUILD_TESTS=OFF \
    -DASSIMP_BUILD_ASSIMP_TOOLS=OFF \
    -DASSIMP_BUILD_SAMPLES=OFF \
    -DASSIMP_BUILD_ZLIB=ON \
    -DASSIMP_NO_EXPORT=ON \
    -DASSIMP_BUILD_ALL_IMPORTERS_BY_DEFAULT=OFF \
    -DASSIMP_BUILD_OBJ_IMPORTER=ON \
    -DASSIMP_BUILD_FBX_IMPORTER=ON \
    -DASSIMP_BUILD_GLTF_IMPORTER=ON \
    -DASSIMP_BUILD_COLLADA_IMPORTER=ON \
    -DASSIMP_BUILD_ZLIB=OFF \
    -DCMAKE_C_FLAGS="-Wno-implicit-function-declaration -Wno-macro-redefined" \
    -DCMAKE_CXX_FLAGS="-Wno-macro-redefined" \
    -DCMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED=NO \
    -DCMAKE_XCODE_ATTRIBUTE_DEVELOPMENT_TEAM=""

cmake --build . --config "$BUILD_TYPE" -- -sdk iphoneos

# Find the built dylib
echo ""
echo "Locating built library..."
DYLIB_DEVICE=$(find "$BUILD_DIR_DEVICE" -name "libassimp*.dylib" -type f | head -n 1)

if [ -z "$DYLIB_DEVICE" ]; then
    echo "Error: Device dylib not found"
    echo "Searching in: $BUILD_DIR_DEVICE"
    find "$BUILD_DIR_DEVICE" -name "libassimp*" -type f
    exit 1
fi

echo "Found device library: $DYLIB_DEVICE"

# Create framework structure
echo ""
echo "Creating framework structure..."
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
DEST_DIR="$ASSIMP_DIR/libs/ios/$BUILD_TYPE_LOWER"
mkdir -p "$DEST_DIR"

FRAMEWORK_PATH="$DEST_DIR/assimp.framework"
rm -rf "$FRAMEWORK_PATH"
mkdir -p "$FRAMEWORK_PATH/Headers"

# Copy the dylib as framework binary
cp "$DYLIB_DEVICE" "$FRAMEWORK_PATH/assimp"

# Fix the install name to use framework path instead of dylib path
install_name_tool -id "@rpath/assimp.framework/assimp" "$FRAMEWORK_PATH/assimp"

# Remove any versioned references
install_name_tool -change "@rpath/libassimp.6.dylib" "@rpath/assimp.framework/assimp" "$FRAMEWORK_PATH/assimp" 2>/dev/null || true

# Copy headers
cp -R "$ASSIMP_DIR/include/assimp/"* "$FRAMEWORK_PATH/Headers/"

# Create Info.plist
cat > "$FRAMEWORK_PATH/Info.plist" << 'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>assimp</string>
    <key>CFBundleIdentifier</key>
    <string>net.sf.assimp</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>assimp</string>
    <key>CFBundlePackageType</key>
    <string>FMWK</string>
    <key>CFBundleShortVersionString</key>
    <string>5.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
</dict>
</plist>
EOF

echo "=========================================="
echo "Build complete!"
echo "Output: $FRAMEWORK_PATH"
echo "=========================================="

# Verify the framework was created
if [ -d "$FRAMEWORK_PATH" ] && [ -f "$FRAMEWORK_PATH/assimp" ]; then
    echo "✓ Successfully built assimp.framework for iOS (device)"
    echo ""
    echo "Framework info:"
    ls -lh "$FRAMEWORK_PATH/assimp"
    file "$FRAMEWORK_PATH/assimp"
else
    echo "✗ Failed to build assimp.framework"
    exit 1
fi
