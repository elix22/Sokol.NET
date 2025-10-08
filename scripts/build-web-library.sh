
#!/bin/bash
# Build script for Emscripten on macOS using Bash
# Uses local emsdk submodule

set -e  # Exit on any error

# Set Emscripten version
EMSCRIPTEN_VERSION="3.1.34"

# Path to local emsdk
EMSDK_PATH="./tools/emsdk/emsdk"

# Check if local emsdk exists
if [ ! -f "$EMSDK_PATH" ]; then
    echo "Error: Local emsdk not found at $EMSDK_PATH. Ensure the submodule is initialized: git submodule update --init --recursive"
    exit 1
fi

# Make emsdk executable if it isn't already
chmod +x "$EMSDK_PATH"

# Activate Emscripten SDK with the specified version
echo "Installing Emscripten SDK version $EMSCRIPTEN_VERSION..."
"$EMSDK_PATH" install "$EMSCRIPTEN_VERSION"

echo "Activating Emscripten SDK version $EMSCRIPTEN_VERSION..."
"$EMSDK_PATH" activate "$EMSCRIPTEN_VERSION"

# Set up environment variables for Emscripten
echo "Setting up Emscripten environment..."
source "./tools/emsdk/emsdk_env.sh"

# Clean up any existing build directories
echo "Cleaning up build directories..."
rm -rf build-emscripten-debug build-emscripten-release

# Build Debug configuration
echo "========================================="
echo "Building Debug configuration..."
echo "========================================="
mkdir -p build-emscripten-debug
emcmake cmake -B build-emscripten-debug -S ext/ -DCMAKE_BUILD_TYPE=Debug
cmake --build build-emscripten-debug

# Build Release configuration
echo "========================================="
echo "Building Release configuration..."
echo "========================================="
mkdir -p build-emscripten-release
emcmake cmake -B build-emscripten-release -S ext/ -DCMAKE_BUILD_TYPE=Release
cmake --build build-emscripten-release

# Clean up build directories
echo "Cleaning up build directories..."
rm -rf build-emscripten-debug build-emscripten-release

echo "========================================="
echo "Build completed successfully!"
echo "Debug build: libs/emscripten/x86/debug/sokol.a"
echo "Release build: libs/emscripten/x86/release/sokol.a"
echo "========================================="