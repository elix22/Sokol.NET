
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

# compile shaders first
 dotnet msbuild -t:CompileShaders -p:DefineConstants="__IOS__"

dotnet publish -r  ios-arm64 -c Release -p:BuildAsLibrary=true  -p:DefineConstants="__IOS__"
mkdir RotatingCubeExample.framework
install_name_tool -rpath @executable_path @executable_path/Frameworks RotatingCubeExample/bin/Release/net9.0/ios-arm64/publish/libRotatingCubeExample.dylib 
install_name_tool -id @rpath/RotatingCubeExample.framework/RotatingCubeExample RotatingCubeExample/bin/Release/net9.0/ios-arm64/publish/libRotatingCubeExample.dylib 
lipo -create  RotatingCubeExample/bin/Release/net9.0/ios-arm64/publish/libRotatingCubeExample.dylib -output RotatingCubeExample.framework/RotatingCubeExample
cp  RotatingCubeExample/Info.plist RotatingCubeExample.framework/Info.plist
cp -rf RotatingCubeExample.framework iOS/NativeAotTestApp
rm -rf RotatingCubeExample.framework
cp -rf  libs/ios/arm64/sokol.framework iOS/NativeAotTestApp