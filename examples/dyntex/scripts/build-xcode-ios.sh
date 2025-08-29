
#!/bin/bash

# Get architecture
ARCH=$(uname -m)
APPNAME=cube
rm -rf build-xcode-ios
mkdir -p build-xcode-ios
cd  build-xcode-ios
cmake -G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 -DCMAKE_OSX_ARCHITECTURES="arm64" ../../../ext
cmake --build . --config Release

mkdir -p "../ios/frameworks"
cp -rf Release-iphoneos/sokol.framework ../ios/frameworks/

cd ..
rm -rf build-xcode-ios

# compile shaders first
dotnet msbuild -t:CompileShaders -p:DefineConstants="__IOS__"

dotnet publish -r  ios-arm64 -c Release -p:BuildAsLibrary=true  -p:DefineConstants="__IOS__"

mkdir -p ios/frameworks/${APPNAME}.framework
install_name_tool -rpath @executable_path @executable_path/Frameworks bin/Release/net9.0/ios-arm64/publish/${APPNAME}.dylib 
install_name_tool -id @rpath/${APPNAME}.framework/${APPNAME} bin/Release/net9.0/ios-arm64/publish/${APPNAME}.dylib 
lipo -create  bin/Release/net9.0/ios-arm64/publish/${APPNAME}.dylib -output ios/frameworks/${APPNAME}.framework/${APPNAME}
cp -f scripts/Info.plist ios/frameworks/${APPNAME}.framework/Info.plist

cp -f scripts/CMakeLists.txt ios/CMakeLists.txt
cp -f scripts/main.m ios/main.m
rm -rf ios/build-xcode-ios
mkdir -p ios/build-xcode-ios
cd ios/build-xcode-ios
cmake .. -G Xcode
