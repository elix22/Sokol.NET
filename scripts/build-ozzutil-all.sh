#!/bin/bash
# Master build script for ozzutil library
# Usage: ./build-ozzutil-all.sh [build_type]
# Example: ./build-ozzutil-all.sh Release
# Example: ./build-ozzutil-all.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building ozzutil for all platforms"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Detect current platform
PLATFORM="unknown"
case "$(uname -s)" in
    Darwin*)
        PLATFORM="macos"
        ;;
    Linux*)
        PLATFORM="linux"
        ;;
    CYGWIN*|MINGW32*|MSYS*|MINGW*)
        PLATFORM="windows"
        ;;
esac

echo "Detected platform: $PLATFORM"

# Build for current platform first
case "$PLATFORM" in
    "macos")
        echo ""
        echo "Building for macOS..."
        "$SCRIPT_DIR/build-ozzutil-macos.sh" arm64 "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-macos.sh" x86_64 "$BUILD_TYPE"
        
        echo ""
        echo "Building for iOS..."
        "$SCRIPT_DIR/build-ozzutil-ios.sh" arm64 "$BUILD_TYPE"
        
        echo ""
        echo "Building for Web/Emscripten..."
        "$SCRIPT_DIR/build-ozzutil-web.sh" "$BUILD_TYPE"
        
        echo ""
        echo "Building for Android..."
        "$SCRIPT_DIR/build-ozzutil-android.sh" arm64-v8a "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-android.sh" armeabi-v7a "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-android.sh" x86_64 "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-android.sh" x86 "$BUILD_TYPE"
        ;;
        
    "linux")
        echo ""
        echo "Building for Linux..."
        "$SCRIPT_DIR/build-ozzutil-linux.sh" x86_64 "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-linux.sh" arm64 "$BUILD_TYPE"
        
        echo ""
        echo "Building for Web/Emscripten..."
        "$SCRIPT_DIR/build-ozzutil-web.sh" "$BUILD_TYPE"
        
        echo ""
        echo "Building for Android..."
        "$SCRIPT_DIR/build-ozzutil-android.sh" arm64-v8a "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-android.sh" armeabi-v7a "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-android.sh" x86_64 "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozzutil-android.sh" x86 "$BUILD_TYPE"
        ;;
        
    "windows")
        echo ""
        echo "Building for Windows..."
        powershell.exe -ExecutionPolicy Bypass -File "$SCRIPT_DIR/build-ozzutil-windows.ps1" "$BUILD_TYPE"
        
        echo ""
        echo "Note: Web and mobile builds require Linux or macOS"
        ;;
        
    *)
        echo "Error: Unsupported platform: $PLATFORM"
        echo "Please run the individual build scripts manually"
        exit 1
        ;;
esac

echo ""
echo "=========================================="
echo "All builds complete for $PLATFORM!"
echo "=========================================="

# Show summary of built libraries
echo ""
echo "Built libraries summary:"
OZZUTIL_DIR="$(cd "$SCRIPT_DIR/../ext/ozzutil" && pwd)"
if [ -d "$OZZUTIL_DIR/libs" ]; then
    find "$OZZUTIL_DIR/libs" -name "*ozzutil*" -type f | sort
else
    echo "No libraries found in $OZZUTIL_DIR/libs"
fi