
#!/bin/bash

# Get architecture
ARCH=$(uname -m)
rm -rf build-xcode-ios
mkdir -p build-xcode-ios
cd  build-xcode-ios
cmake -G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 -DCMAKE_OSX_ARCHITECTURES="arm64" ../ext
cmake --build . --config Release

mkdir -p "../libs/ios/arm64"
cp -rf Release-iphoneos/sokol.framework ../libs/ios/arm64/

cd ..
rm -rf build-xcode-ios
