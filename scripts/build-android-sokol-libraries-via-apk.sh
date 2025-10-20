#!/usr/bin/env bash
set -e

echo "============================================="
echo "Building Android sokol.so libraries via APK"
echo "============================================="

# Get absolute paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
EXAMPLE_PATH="$WORKSPACE_DIR/examples/clear"

echo "Workspace: $WORKSPACE_DIR"
echo "Using example: $EXAMPLE_PATH"
echo "Building both debug and release configurations"
echo ""

# Change to workspace directory
cd "$WORKSPACE_DIR"

# Clean up any previous builds completely
echo "🧹 Cleaning previous builds..."
rm -rf "$EXAMPLE_PATH/output/Android" 2>/dev/null || true
rm -rf "$EXAMPLE_PATH/Android" 2>/dev/null || true
rm -rf "libs/android" 2>/dev/null || true
echo ""

# Prepare example once
echo "🔧 Preparing example (compiling shaders and setting up project)..."
dotnet run --project "$WORKSPACE_DIR/tools/SokolApplicationBuilder" \
    -- \
    --task prepare \
    --architecture android \
    --path "$EXAMPLE_PATH"

if [ $? -ne 0 ]; then
    echo "❌ Failed to prepare example"
    exit 1
fi
echo ""

# Function to build APK and extract libraries
build_and_extract_libraries() {
    local build_type="$1"
    echo "========================================="
    echo "🔨 Building $build_type APK"
    echo "========================================="
    
    # Clean Android directory for clean build
    rm -rf "$EXAMPLE_PATH/Android" 2>/dev/null || true
    rm -rf "$EXAMPLE_PATH/output/Android/$build_type" 2>/dev/null || true
    
    # Build the APK
    dotnet run --project "$WORKSPACE_DIR/tools/SokolApplicationBuilder" \
        -- \
        --task build \
        --type "$build_type" \
        --architecture android \
        --path "$EXAMPLE_PATH"

    if [ $? -ne 0 ]; then
        echo "❌ Failed to build $build_type APK"
        return 1
    fi

    echo "✅ $build_type APK build completed successfully!"
    
    # Find the generated APK
    local APK_FILE="$EXAMPLE_PATH/output/Android/$build_type/clear-$build_type.apk"
    
    if [ ! -f "$APK_FILE" ]; then
        echo "❌ Could not find generated $build_type APK file: $APK_FILE"
        return 1
    fi

    echo "📦 Found $build_type APK: $APK_FILE"
    
    # Extract sokol libraries from the APK
    echo "📤 Extracting sokol.so libraries from $build_type APK..."

    # Create temporary directory
    local TEMP_DIR=$(mktemp -d)
    echo "Using temporary directory: $TEMP_DIR"

    # Extract APK contents
    echo "Extracting $build_type APK contents..."
    unzip -q "$APK_FILE" -d "$TEMP_DIR"

    # Check if lib directory exists
    if [ ! -d "$TEMP_DIR/lib" ]; then
        echo "❌ No lib directory found in $build_type APK"
        echo "APK contents:"
        ls -la "$TEMP_DIR/"
        rm -rf "$TEMP_DIR"
        return 1
    fi

    echo "📋 Available architectures in $build_type APK:"
    ls -la "$TEMP_DIR/lib/"
    echo ""

    # Extract libraries for each architecture
    local extracted_count=0
    for ABI_DIR in "$TEMP_DIR/lib"/*; do
        if [ -d "$ABI_DIR" ]; then
            local ABI=$(basename "$ABI_DIR")
            
            if [ -f "$ABI_DIR/libsokol.so" ]; then
                echo "🔄 Processing $ABI architecture ($build_type)..."
                
                # Create directory structure
                mkdir -p "libs/android/$ABI/$build_type"
                
                # Copy library to specific build type location
                cp "$ABI_DIR/libsokol.so" "libs/android/$ABI/$build_type/libsokol.so"
                
                # Get file info
                local FILE_SIZE=$(ls -lh "libs/android/$ABI/$build_type/libsokol.so" | awk '{print $5}')
                echo "  ✅ Extracted $ABI $build_type library (${FILE_SIZE})"
                
                extracted_count=$((extracted_count + 1))
            else
                echo "  ⚠️  No libsokol.so found for $ABI in $build_type APK"
            fi
        fi
    done

    # Cleanup temporary directory
    rm -rf "$TEMP_DIR"
    
    echo ""
    echo "✅ Successfully extracted $extracted_count $build_type libraries"
    echo ""
    
    return 0
}

# Build debug libraries
build_and_extract_libraries "debug"
DEBUG_SUCCESS=$?

# Build release libraries  
build_and_extract_libraries "release"
RELEASE_SUCCESS=$?

# Final summary
echo ""
echo "============================================="
if [ $DEBUG_SUCCESS -eq 0 ] && [ $RELEASE_SUCCESS -eq 0 ]; then
    echo "✅ Successfully built and extracted sokol.so libraries!"
    echo "============================================="
    echo ""
    echo "📊 Summary:"
    echo "  - Built debug and release APKs from clear example"
    echo "  - Extracted libraries for all available architectures"
    echo ""
    echo "📁 Libraries available at:"
    for ABI_DIR in "libs/android"/*; do
        if [ -d "$ABI_DIR" ]; then
            ABI=$(basename "$ABI_DIR")
            if [ -f "$ABI_DIR/debug/libsokol.so" ]; then
                DEBUG_SIZE=$(ls -lh "$ABI_DIR/debug/libsokol.so" | awk '{print $5}')
                echo "  - libs/android/$ABI/debug/libsokol.so (${DEBUG_SIZE})"
            fi
            if [ -f "$ABI_DIR/release/libsokol.so" ]; then
                RELEASE_SIZE=$(ls -lh "$ABI_DIR/release/libsokol.so" | awk '{print $5}')
                echo "  - libs/android/$ABI/release/libsokol.so (${RELEASE_SIZE})"
            fi
        fi
    done
    echo ""
    echo "🧹 Cleaning up temporary APKs..."
    rm -rf "$EXAMPLE_PATH/output/Android" 2>/dev/null || true
    echo ""
    echo "🎉 Android sokol.so libraries are ready for use!"
    echo "   Debug and release versions are properly separated!"
else
    echo "❌ Build process failed!"
    echo "============================================="
    if [ $DEBUG_SUCCESS -ne 0 ]; then
        echo "  Debug build failed"
    fi
    if [ $RELEASE_SUCCESS -ne 0 ]; then
        echo "  Release build failed"  
    fi
    exit 1
fi