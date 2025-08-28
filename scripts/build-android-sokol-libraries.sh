# ANDROID_ABIS=("armeabi-v7a" "arm64-v8a" "x86" "x86_64")
ANDROID_ABIS=("armeabi-v7a" "arm64-v8a" "x86_64")
# Loop through each ABI
for ABI in "${ANDROID_ABIS[@]}"
do
    echo "Building for ABI: $ABI"
    
    # Create and enter build directory
    BUILD_DIR="build-android-${ABI}"
    rm -rf $BUILD_DIR
    mkdir -p $BUILD_DIR
    cd $BUILD_DIR

    # Configure and build $ANDROID_NDK is an environment variable and must be configured with the full Android NDK path
    cmake ../ext \
        -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
        -DANDROID_ABI=$ABI \
        -DANDROID_PLATFORM=android-26 \
        -DCMAKE_BUILD_TYPE=Release
    
    cmake --build .
    
   cd ..
    mkdir -p "libs/android/${ABI}"
    cp -f "${BUILD_DIR}/libsokol.so" "libs/android/${ABI}/libsokol.so"
    
    if [ ! -f "libs/android/${ABI}/libsokol.so" ]; then
        echo "Error: Failed to copy libsokol.so for ${ABI}"
        exit 1
    fi
    
    rm -rf $BUILD_DIR
done

