#!/bin/bash
# Master build script for spine-c library - builds for all platforms
# Usage: ./build-all.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=========================================="
echo "Building spine-c for all platforms"
echo "=========================================="

# Detect current platform
PLATFORM="$(uname -s)"
case "$PLATFORM" in
    Darwin*)
        echo "Running on macOS"
        
        # Build for macOS (both architectures)
        echo ""
        echo "Building for macOS (arm64)..."
        "$SCRIPT_DIR/build-spine-c-macos.sh" arm64 Release
        
        echo ""
        echo "Building for macOS (x86_64)..."
        "$SCRIPT_DIR/build-spine-c-macos.sh" x86_64 Release
        
        # Build for iOS if on macOS
        echo ""
        echo "Building for iOS (device)..."
        "$SCRIPT_DIR/build-spine-c-ios.sh" iphoneos Release
        
        echo ""
        echo "Building for iOS (simulator)..."
        "$SCRIPT_DIR/build-spine-c-ios.sh" iphonesimulator Release
        ;;
        
    Linux*)
        echo "Running on Linux"
        
        # Build for Linux
        echo ""
        echo "Building for Linux..."
        "$SCRIPT_DIR/build-spine-c-linux.sh" Release
        ;;
        
    MINGW*|MSYS*|CYGWIN*)
        echo "Running on Windows"
        
        # Build for Windows (call PowerShell script)
        echo ""
        echo "Building for Windows (x64)..."
        powershell.exe -ExecutionPolicy Bypass -File "$SCRIPT_DIR/build-spine-c-windows.ps1" -Architecture x64 -BuildType Release
        ;;
        
    *)
        echo "Unknown platform: $PLATFORM"
        exit 1
        ;;
esac

# Build for Web (if Emscripten is available)
if command -v emcc &> /dev/null; then
    echo ""
    echo "Building for Web/Emscripten..."
    "$SCRIPT_DIR/build-spine-c-web.sh" Release
else
    echo ""
    echo "Skipping Web build (Emscripten not found)"
fi

# Build for Android (if Android NDK is available)
if [ -n "$ANDROID_NDK" ] || [ -n "$ANDROID_NDK_HOME" ]; then
    echo ""
    echo "Building for Android (arm64-v8a)..."
    "$SCRIPT_DIR/build-spine-c-android.sh" arm64-v8a Release
    
    echo ""
    echo "Building for Android (armeabi-v7a)..."
    "$SCRIPT_DIR/build-spine-c-android.sh" armeabi-v7a Release
else
    echo ""
    echo "Skipping Android build (ANDROID_NDK not set)"
fi

echo ""
echo "=========================================="
echo "All builds complete!"
echo "=========================================="
