#!/bin/bash
# Master build script for ozz-animation library
# Usage: ./build-ozz-animation-all.sh [build_type]
# Example: ./build-ozz-animation-all.sh Release
# Example: ./build-ozz-animation-all.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building ozz-animation for all platforms"
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

echo "Current platform: $PLATFORM"

# Build for current platform
case "$PLATFORM" in
    macos)
        echo "Building for macOS (arm64 and x86_64)..."
        "$SCRIPT_DIR/build-ozz-animation-macos.sh" arm64 "$BUILD_TYPE"
        "$SCRIPT_DIR/build-ozz-animation-macos.sh" x86_64 "$BUILD_TYPE"
        ;;
    linux)
        echo "Building for Linux..."
        "$SCRIPT_DIR/build-ozz-animation-linux.sh" "$BUILD_TYPE"
        ;;
    windows)
        echo "Building for Windows..."
        powershell.exe -ExecutionPolicy Bypass -File "$SCRIPT_DIR/build-ozz-animation-windows.ps1" -BuildType "$BUILD_TYPE"
        ;;
    *)
        echo "Warning: Unknown platform $PLATFORM, skipping native build"
        ;;
esac

# Build for Web (works on all platforms)
echo "Building for Web/Emscripten..."
"$SCRIPT_DIR/build-ozz-animation-web.sh" "$BUILD_TYPE"

# Build for mobile platforms (if available)
if command -v xcodebuild >/dev/null 2>&1; then
    echo "Building for iOS..."
    "$SCRIPT_DIR/build-ozz-animation-ios-all.sh" "$BUILD_TYPE"
else
    echo "Xcode not found, skipping iOS build"
fi

if [ ! -z "$ANDROID_NDK_ROOT" ] || [ ! -z "$ANDROID_NDK_HOME" ] || [ ! -z "$NDK_ROOT" ]; then
    echo "Building for Android..."
    "$SCRIPT_DIR/build-ozz-animation-android-all.sh" "$BUILD_TYPE"
else
    echo "Android NDK not found, skipping Android build"
fi

echo "=========================================="
echo "All builds completed successfully!"
echo "=========================================="