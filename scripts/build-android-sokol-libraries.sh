#!/usr/bin/env bash
set -e

echo "========================================="
echo "Building libsokol.so for Android"
echo "========================================="

# Check if ANDROID_NDK is set
if [ -z "$ANDROID_NDK" ]; then
    echo "Error: ANDROID_NDK environment variable is not set"
    echo "Please set ANDROID_NDK to point to your Android NDK installation"
    echo "Example: export ANDROID_NDK=/path/to/android-ndk"
    exit 1
fi

if [ ! -d "$ANDROID_NDK" ]; then
    echo "Error: ANDROID_NDK directory does not exist: $ANDROID_NDK"
    exit 1
fi

echo "Using Android NDK: $ANDROID_NDK"
echo ""

# Define ABIs to build
# ANDROID_ABIS=("armeabi-v7a" "arm64-v8a" "x86" "x86_64")
ANDROID_ABIS=("armeabi-v7a" "arm64-v8a" "x86_64")

# Build for each ABI
for ABI in "${ANDROID_ABIS[@]}"
do
    echo "========================================="
    echo "Building for ABI: $ABI"
    echo "========================================="
    
    # Create and enter build directory
    BUILD_DIR="build-android-${ABI}"
    rm -rf $BUILD_DIR
    mkdir -p $BUILD_DIR
    cd $BUILD_DIR

    # Configure and build $ANDROID_NDK is an environment variable and must be configured with the full Android NDK path
    echo "Configuring CMake project..."
    cmake ../ext \
        -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
        -DANDROID_ABI=$ABI \
        -DANDROID_PLATFORM=android-26 \
        -DCMAKE_BUILD_TYPE=Release
    
    echo ""
    echo "Building Release configuration..."
    cmake --build .
    
    cd ..
    
    # Copy to both release and debug directories (same binary for now)
    # This ensures external libraries can find libsokol.so regardless of their build type
    mkdir -p "libs/android/${ABI}/release"
    mkdir -p "libs/android/${ABI}/debug"
    
    cp -f "${BUILD_DIR}/libsokol.so" "libs/android/${ABI}/release/libsokol.so"
    cp -f "${BUILD_DIR}/libsokol.so" "libs/android/${ABI}/debug/libsokol.so"
    
    # Also copy to the root ABI directory for backward compatibility
    cp -f "${BUILD_DIR}/libsokol.so" "libs/android/${ABI}/libsokol.so"
    
    if [ ! -f "libs/android/${ABI}/release/libsokol.so" ]; then
        echo "Error: Failed to copy libsokol.so for ${ABI}"
        exit 1
    fi
    
    echo "Copied library to:"
    echo "  - libs/android/${ABI}/release/libsokol.so"
    echo "  - libs/android/${ABI}/debug/libsokol.so"
    echo "  - libs/android/${ABI}/libsokol.so (backward compatibility)"
    echo ""
    
    rm -rf $BUILD_DIR
done

echo ""
echo "========================================="
echo "Build completed successfully!"
echo "Libraries built for:"
for ABI in "${ANDROID_ABIS[@]}"
do
    echo "  - $ABI: libs/android/${ABI}/release/libsokol.so"
done
echo "========================================="
echo "Done!"

