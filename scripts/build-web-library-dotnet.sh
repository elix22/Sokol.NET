#!/bin/bash

# Build sokol.a using .NET's internal Emscripten 3.1.34
# This ensures ABI compatibility with .NET WebAssembly builds

echo "🔧 Building sokol.a with .NET's Emscripten 3.1.34..."

# .NET's Emscripten paths (from the diagnostic output)
DOTNET_EMSCRIPTEN_PATH="/usr/local/share/dotnet/packs/Microsoft.NET.Runtime.Emscripten.3.1.34.Sdk.osx-arm64/8.0.19/tools"
DOTNET_EMSCRIPTEN_BIN="$DOTNET_EMSCRIPTEN_PATH/bin"
DOTNET_EMSCRIPTEN_EMSCRIPTEN="$DOTNET_EMSCRIPTEN_PATH/emscripten"
DOTNET_PYTHON="/usr/local/share/dotnet/packs/Microsoft.NET.Runtime.Emscripten.3.1.34.Python.osx-arm64/8.0.19/tools/bin/python3"
DOTNET_NODE="/usr/local/share/dotnet/packs/Microsoft.NET.Runtime.Emscripten.3.1.34.Node.osx-arm64/8.0.19/tools/bin/node"
DOTNET_CACHE="/usr/local/share/dotnet/packs/Microsoft.NET.Runtime.Emscripten.3.1.34.Cache.osx-arm64/8.0.19/tools/emscripten/cache"

# Verify .NET Emscripten exists
if [ ! -f "$DOTNET_EMSCRIPTEN_BIN/emcc" ]; then
    echo "❌ .NET Emscripten not found at: $DOTNET_EMSCRIPTEN_BIN/emcc"
    echo "� Checking alternative locations..."
    
    # Check if emcc is in the emscripten subdirectory
    if [ -f "$DOTNET_EMSCRIPTEN_EMSCRIPTEN/emcc" ]; then
        echo "✅ Found emcc at: $DOTNET_EMSCRIPTEN_EMSCRIPTEN/emcc"
        DOTNET_EMSCRIPTEN_BIN="$DOTNET_EMSCRIPTEN_EMSCRIPTEN"
    else
        echo "🔍 Searching for .NET Emscripten installation..."
        find /usr/local/share/dotnet/packs -name "emcc" 2>/dev/null | head -5
        echo "�💡 Make sure you have the wasm-tools workload installed:"
        echo "   dotnet workload install wasm-tools"
        exit 1
    fi
fi

# Set environment variables to use .NET's Emscripten
export PATH="$DOTNET_EMSCRIPTEN_BIN:$DOTNET_EMSCRIPTEN_EMSCRIPTEN:$PATH"
export EMSDK_PYTHON="$DOTNET_PYTHON"
export DOTNET_EMSCRIPTEN_LLVM_ROOT="$DOTNET_EMSCRIPTEN_BIN"
export DOTNET_EMSCRIPTEN_BINARYEN_ROOT="$DOTNET_EMSCRIPTEN_PATH"
export DOTNET_EMSCRIPTEN_NODE_JS="$DOTNET_NODE"
export EM_CACHE="$DOTNET_CACHE"
export EM_FROZEN_CACHE="True"
export PYTHONUTF8=1
export EM_WORKAROUND_PYTHON_BUG_34780=1

# Show which Emscripten we're using
echo "📍 Using Emscripten from: $(which emcc)"
echo "🚀 Emscripten version:"
emcc --version

# Clean previous build
echo "🧹 Cleaning previous build..."
rm -rf build-emscripten-dotnet
rm -f libs/emscripten/x86/sokol.a

# Build with .NET's Emscripten
echo "🔨 Building with .NET's Emscripten..."
emcmake cmake -B build-emscripten-dotnet -S ext/
cmake --build build-emscripten-dotnet

# Check if build succeeded
if [ -f "build-emscripten-dotnet/libsokol.a" ]; then
    echo "✅ Build succeeded!"
    
    # Create output directory
    mkdir -p libs/emscripten/x86/
    
    # Copy the library
    cp build-emscripten-dotnet/libsokol.a libs/emscripten/x86/sokol.a
    
    echo "📦 Library copied to: libs/emscripten/x86/sokol.a"
    echo "🎉 sokol.a compiled with .NET's Emscripten 3.1.34!"
    
    # Show library info
    echo "📊 Library info:"
    ls -lh libs/emscripten/x86/sokol.a
    file libs/emscripten/x86/sokol.a
else
    echo "❌ Build failed!"
    echo "🔍 Check the build output above for errors"
    exit 1
fi

echo ""
echo "🚀 Ready to build your .NET project with ABI-compatible sokol.a!"
echo "💡 Run: cd examples/cube && dotnet build cubeweb.csproj"
