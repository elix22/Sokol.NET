#!/bin/bash

# Build sokol.a using system Emscripten but with .NET-compatible settings
# This ensures better ABI compatibility while using a working Emscripten

echo "🔧 Building sokol.a with system Emscripten (3.1.34) matching .NET runtime..."

# Use system Emscripten (which is working)
export PATH="/Users/elialoni/Development/emsdk/upstream/emscripten:$PATH"

# Apply .NET-compatible settings
export PYTHONUTF8=1
export EM_WORKAROUND_PYTHON_BUG_34780=1

# Show which Emscripten we're using
echo "📍 Using Emscripten from: $(which emcc)"
echo "🚀 Emscripten version:"
emcc --version

# Clean previous build
echo "🧹 Cleaning previous build..."
rm -rf build-emscripten-system
rm -f libs/emscripten/x86/sokol.a

# Build with system Emscripten but .NET-compatible flags
echo "🔨 Building with system Emscripten..."

# Use explicit flags that match .NET's Emscripten usage
export EMCC_CFLAGS="-O2 -DNDEBUG"
export EMCC_LDFLAGS="-O2"

emcmake cmake -B build-emscripten-system -S ext/ \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_C_FLAGS="$EMCC_CFLAGS" \
    -DCMAKE_CXX_FLAGS="$EMCC_CFLAGS"

cmake --build build-emscripten-system --config Release

# Check if build succeeded
if [ -f "build-emscripten-system/sokol.a" ]; then
    echo "✅ Build succeeded!"
    
    # Create output directory
    mkdir -p libs/emscripten/x86/
    
    # Copy the library
    cp build-emscripten-system/sokol.a libs/emscripten/x86/sokol.a
    
    echo "📦 Library copied to: libs/emscripten/x86/sokol.a"
    echo "🎉 sokol.a compiled with system Emscripten 3.1.34!"
    
    # Show library info
    echo "📊 Library info:"
    ls -lh libs/emscripten/x86/sokol.a
    file libs/emscripten/x86/sokol.a
    
    # Show which symbols are exported
    echo ""
    echo "📋 Exported symbols (sample):"
    nm libs/emscripten/x86/sokol.a | grep -E "(sapp_|simgui_)" | head -10
else
    echo "❌ Build failed!"
    echo "🔍 Check the build output above for errors"
    exit 1
fi

echo ""
echo "🚀 Ready to test with the new sokol.a!"
echo "💡 The library is now compiled with Emscripten 3.1.64 which should be more compatible"
echo "🧪 Test by building your project: cd examples/cube && dotnet build cubeweb.csproj"
echo ""
echo "⚠️  Note: If you still experience callback corruption:"
echo "    - This suggests the issue might not be Emscripten version related"
echo "    - Could be memory alignment, calling convention, or other ABI issues"
