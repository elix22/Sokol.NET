#!/bin/bash
# Master build script for assimp library for all platforms
# Usage: ./build-all.sh [build_type]
# Example: ./build-all.sh Release
# Example: ./build-all.sh Debug

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_TYPE="${1:-Release}"

echo "=========================================="
echo "Building assimp for all platforms"
echo "Build Type: $BUILD_TYPE"
echo "=========================================="

# Track build status
BUILDS_SUCCEEDED=()
BUILDS_FAILED=()
BUILDS_SKIPPED=()

# Detect current platform
PLATFORM=$(uname)

# Function to run a build script
run_build() {
    local script_name=$1
    local description=$2
    local platform_check=$3
    
    echo ""
    echo "=========================================="
    echo "Building: $description"
    echo "=========================================="
    
    # Check platform requirement if specified
    if [ -n "$platform_check" ] && [ "$PLATFORM" != "$platform_check" ]; then
        echo "⊘ Skipping $description (requires $platform_check, current: $PLATFORM)"
        BUILDS_SKIPPED+=("$description")
        return 0
    fi
    
    # Check if script exists
    if [ ! -f "$SCRIPT_DIR/$script_name" ]; then
        echo "⚠ Script not found: $script_name"
        BUILDS_FAILED+=("$description (script not found)")
        return 1
    fi
    
    # Make script executable
    chmod +x "$SCRIPT_DIR/$script_name"
    
    # Run the build script
    if "$SCRIPT_DIR/$script_name" "$BUILD_TYPE"; then
        echo "✓ $description completed successfully"
        BUILDS_SUCCEEDED+=("$description")
        return 0
    else
        echo "✗ $description failed"
        BUILDS_FAILED+=("$description")
        return 1
    fi
}

# Function to run architecture-specific build
run_build_with_arch() {
    local script_name=$1
    local description=$2
    local arch=$3
    local platform_check=$4
    
    echo ""
    echo "=========================================="
    echo "Building: $description ($arch)"
    echo "=========================================="
    
    # Check platform requirement if specified
    if [ -n "$platform_check" ] && [ "$PLATFORM" != "$platform_check" ]; then
        echo "⊘ Skipping $description ($arch) (requires $platform_check, current: $PLATFORM)"
        BUILDS_SKIPPED+=("$description ($arch)")
        return 0
    fi
    
    # Check if script exists
    if [ ! -f "$SCRIPT_DIR/$script_name" ]; then
        echo "⚠ Script not found: $script_name"
        BUILDS_FAILED+=("$description ($arch) (script not found)")
        return 1
    fi
    
    # Make script executable
    chmod +x "$SCRIPT_DIR/$script_name"
    
    # Run the build script with architecture argument
    if "$SCRIPT_DIR/$script_name" "$arch" "$BUILD_TYPE"; then
        echo "✓ $description ($arch) completed successfully"
        BUILDS_SUCCEEDED+=("$description ($arch)")
        return 0
    else
        echo "✗ $description ($arch) failed"
        BUILDS_FAILED+=("$description ($arch)")
        return 1
    fi
}

# macOS builds
if [ "$PLATFORM" = "Darwin" ]; then
    run_build_with_arch "build-assimp-macos.sh" "macOS" "arm64" "Darwin" || true
    run_build_with_arch "build-assimp-macos.sh" "macOS" "x86_64" "Darwin" || true
fi

# Linux builds
if [ "$PLATFORM" = "Linux" ]; then
    run_build "build-assimp-linux.sh" "Linux x86_64" "Linux" || true
fi

# Windows builds (if on Windows with bash/WSL)
if [ "$PLATFORM" = "MINGW"* ] || [ "$PLATFORM" = "MSYS"* ] || [ "$PLATFORM" = "CYGWIN"* ]; then
    echo ""
    echo "Note: For Windows builds, please run build-assimp-windows.ps1 in PowerShell:"
    echo "  powershell.exe -File $SCRIPT_DIR/build-assimp-windows.ps1 x64 $BUILD_TYPE"
    BUILDS_SKIPPED+=("Windows x64 (use PowerShell)")
fi

# Android builds (check for Android NDK)
if [ -n "$ANDROID_NDK" ] || [ -n "$ANDROID_NDK_HOME" ]; then
    run_build_with_arch "build-assimp-android.sh" "Android" "arm64-v8a" "" || true
    run_build_with_arch "build-assimp-android.sh" "Android" "armeabi-v7a" "" || true
    run_build_with_arch "build-assimp-android.sh" "Android" "x86_64" "" || true
else
    echo "⊘ Skipping Android builds (ANDROID_NDK not set)"
    BUILDS_SKIPPED+=("Android (ANDROID_NDK not set)")
fi

# iOS builds (macOS only)
if [ "$PLATFORM" = "Darwin" ]; then
    run_build "build-assimp-ios.sh" "iOS XCFramework" "Darwin" || true
fi

# WebAssembly builds (check for Emscripten)
if [ -n "$EMSCRIPTEN" ] || [ -n "$EMSDK" ] || command -v emcc &> /dev/null; then
    run_build "build-assimp-web.sh" "WebAssembly" "" || true
else
    echo "⊘ Skipping WebAssembly build (Emscripten SDK not activated)"
    BUILDS_SKIPPED+=("WebAssembly (Emscripten not found)")
fi

# Print summary
echo ""
echo "=========================================="
echo "Build Summary"
echo "=========================================="

if [ ${#BUILDS_SUCCEEDED[@]} -gt 0 ]; then
    echo ""
    echo "✓ Successful builds (${#BUILDS_SUCCEEDED[@]}):"
    for build in "${BUILDS_SUCCEEDED[@]}"; do
        echo "  - $build"
    done
fi

if [ ${#BUILDS_FAILED[@]} -gt 0 ]; then
    echo ""
    echo "✗ Failed builds (${#BUILDS_FAILED[@]}):"
    for build in "${BUILDS_FAILED[@]}"; do
        echo "  - $build"
    done
fi

if [ ${#BUILDS_SKIPPED[@]} -gt 0 ]; then
    echo ""
    echo "⊘ Skipped builds (${#BUILDS_SKIPPED[@]}):"
    for build in "${BUILDS_SKIPPED[@]}"; do
        echo "  - $build"
    done
fi

echo ""
echo "=========================================="

# Exit with error if any builds failed
if [ ${#BUILDS_FAILED[@]} -gt 0 ]; then
    echo "Some builds failed. See above for details."
    exit 1
else
    echo "All applicable builds completed successfully!"
    exit 0
fi
