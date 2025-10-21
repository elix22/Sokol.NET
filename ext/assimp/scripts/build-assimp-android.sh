#!/bin/bash
# Build script for assimp library for Android
# Usage: ./build-assimp-android.sh [abi] [build_type]
# Example: ./build-assimp-android.sh arm64-v8a Release
# Example: ./build-assimp-android.sh armeabi-v7a Debug
# Supported ABIs: arm64-v8a, armeabi-v7a, x86, x86_64

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSIMP_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Parse arguments
ANDROID_ABI="${1:-arm64-v8a}"
BUILD_TYPE="${2:-Release}"
BUILD_DIR="$ASSIMP_DIR/build-android-$ANDROID_ABI"

echo "=========================================="
echo "Building assimp for Android"
echo "ABI: $ANDROID_ABI"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Check for Android NDK
if [ -z "$ANDROID_NDK" ]; then
    if [ -z "$ANDROID_NDK_HOME" ]; then
        echo "Error: ANDROID_NDK or ANDROID_NDK_HOME environment variable not set"
        echo "Please set one of these to your Android NDK path"
        exit 1
    fi
    ANDROID_NDK="$ANDROID_NDK_HOME"
fi

if [ ! -d "$ANDROID_NDK" ]; then
    echo "Error: Android NDK not found at: $ANDROID_NDK"
    exit 1
fi

echo "Using Android NDK: $ANDROID_NDK"

# Determine API level
ANDROID_NATIVE_API_LEVEL="${ANDROID_NATIVE_API_LEVEL:-21}"
echo "API Level: $ANDROID_NATIVE_API_LEVEL"

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
cmake .. \
    -DCMAKE_TOOLCHAIN_FILE="$ANDROID_NDK/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI="$ANDROID_ABI" \
    -DANDROID_NATIVE_API_LEVEL="$ANDROID_NATIVE_API_LEVEL" \
    -DANDROID_STL=c++_shared \
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE" \
    -DBUILD_SHARED_LIBS=ON \
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
    -DASSIMP_ANDROID_JNIIOSYSTEM=OFF \
    -DASSIMP_IGNORE_GIT_HASH=ON \
    -DAI_CONFIG_ANDROID_JNI_ASSIMP_MANAGER_SUPPORT=OFF \
    -DCMAKE_C_FLAGS="-Dfopen64=fopen -Dftello64=ftell -Dfseeko64=fseek -fno-omit-frame-pointer -Os" \
    -DCMAKE_CXX_FLAGS="-Dfopen64=fopen -Dftello64=ftell -Dfseeko64=fseek -fno-omit-frame-pointer -fno-strict-aliasing -Os -ffunction-sections -fdata-sections" \
    -DCMAKE_SHARED_LINKER_FLAGS="-Wl,--gc-sections"

# Build
cmake --build . --config "$BUILD_TYPE" -- -j$(nproc)

# Create destination directory
BUILD_TYPE_LOWER=$(echo "$BUILD_TYPE" | tr '[:upper:]' '[:lower:]')
DEST_DIR="$ASSIMP_DIR/libs/android/$ANDROID_ABI/$BUILD_TYPE_LOWER"
mkdir -p "$DEST_DIR"

# Copy library to destination
echo "Copying library to $DEST_DIR..."
LIB_PATH="$BUILD_DIR/bin/libassimp.so"

if [ -f "$LIB_PATH" ]; then
    cp "$LIB_PATH" "$DEST_DIR/"
else
    # Try alternative location
    LIB_PATH="$BUILD_DIR/libassimp.so"
    if [ -f "$LIB_PATH" ]; then
        cp "$LIB_PATH" "$DEST_DIR/"
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
    echo "✓ Successfully built libassimp.so for $ANDROID_ABI"
    ls -lh "$DEST_DIR/libassimp.so"
else
    echo "✗ Failed to build libassimp.so"
    exit 1
fi
