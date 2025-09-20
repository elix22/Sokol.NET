
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

# Remove and create build directory
echo "Preparing build directory..."
rm -rf build-emscripten
mkdir -p build-emscripten

# Configure with emcmake
echo "Configuring with emcmake..."
emcmake cmake -B build-emscripten -S ext/

# Build
echo "Building..."
cmake --build build-emscripten

echo "Build completed successfully!"