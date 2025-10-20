#!/usr/bin/env bash
set -e

echo "========================================="
echo "Extracting libsokol.so from APK builds"
echo "========================================="

# Function to extract libraries from an APK
extract_from_apk() {
    local APK_PATH="$1"
    local EXAMPLE_NAME="$2"
    
    if [ ! -f "$APK_PATH" ]; then
        echo "APK not found: $APK_PATH"
        return 1
    fi
    
    echo "Extracting from: $APK_PATH"
    
    # Create temporary directory
    TEMP_DIR=$(mktemp -d)
    
    # Extract APK
    unzip -q "$APK_PATH" -d "$TEMP_DIR"
    
    # Find and copy sokol libraries
    if [ -d "$TEMP_DIR/lib" ]; then
        for ABI in armeabi-v7a arm64-v8a x86_64; do
            if [ -f "$TEMP_DIR/lib/$ABI/libsokol.so" ]; then
                # Create directory structure
                mkdir -p "libs/android/$ABI/release"
                mkdir -p "libs/android/$ABI/debug"
                
                # Copy library
                cp "$TEMP_DIR/lib/$ABI/libsokol.so" "libs/android/$ABI/release/libsokol.so"
                cp "$TEMP_DIR/lib/$ABI/libsokol.so" "libs/android/$ABI/debug/libsokol.so"
                cp "$TEMP_DIR/lib/$ABI/libsokol.so" "libs/android/$ABI/libsokol.so"
                
                echo "✓ Extracted $ABI library from $EXAMPLE_NAME"
            fi
        done
    fi
    
    # Cleanup
    rm -rf "$TEMP_DIR"
}

# Look for existing APK builds
APK_FOUND=false

# Check cube example (most common)
if [ -f "examples/cube/output/Android/release/cube-release.apk" ]; then
    extract_from_apk "examples/cube/output/Android/release/cube-release.apk" "cube"
    APK_FOUND=true
elif [ -f "examples/cube/output/Android/debug/cube-debug.apk" ]; then
    extract_from_apk "examples/cube/output/Android/debug/cube-debug.apk" "cube"
    APK_FOUND=true
fi

# Check other examples if cube not found
if [ "$APK_FOUND" = false ]; then
    for EXAMPLE in examples/*/output/Android/release/*.apk; do
        if [ -f "$EXAMPLE" ]; then
            EXAMPLE_NAME=$(basename $(dirname $(dirname $(dirname "$EXAMPLE"))))
            extract_from_apk "$EXAMPLE" "$EXAMPLE_NAME"
            APK_FOUND=true
            break
        fi
    done
fi

if [ "$APK_FOUND" = false ]; then
    echo "❌ No APK files found. Please build an APK first using:"
    echo "   Android: Build APK task in VS Code"
    echo "   or: dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture android --path examples/cube"
    exit 1
fi

echo ""
echo "========================================="
echo "✅ Successfully extracted sokol libraries!"
echo "Libraries available at:"
ls -la libs/android/*/release/libsokol.so 2>/dev/null || echo "No libraries found"
echo "========================================="