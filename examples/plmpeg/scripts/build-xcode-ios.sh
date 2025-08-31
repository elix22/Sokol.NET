
#!/bin/bash

# iOS Build Script
# Usage: ./build-xcode-ios.sh <project-file.csproj> [development-team]
# Example: ./build-xcode-ios.sh cube.csproj ABC123DEF4

# Check if project file is provided as argument
if [ $# -lt 1 ]; then
    echo "Usage: $0 <project-file.csproj> [development-team]"
    echo "Example: $0 cube.csproj"
    echo "Example: $0 cube.csproj ABC123DEF4"
    exit 1
fi

unamestr=$(uname)

# Switch-on alias expansion within the script
shopt -s expand_aliases

#Alias the sed in-place command for OSX and Linux - incompatibilities between BSD and Linux sed args
if [[ "$unamestr" == "Darwin" ]]; then
	alias aliassedinplace='sed -i ""'
else
	#For Linux, notice no space after the '-i'
	alias aliassedinplace='sed -i""'
fi


# Get the project file name and extract APPNAME
PROJECT_FILE=$1
APPNAME=$(basename "$PROJECT_FILE" .csproj)

# Get development team if provided
DEVELOPMENT_TEAM=""
if [ $# -ge 2 ]; then
    DEVELOPMENT_TEAM=$2
    echo "Using development team: $DEVELOPMENT_TEAM"
fi

# Get architecture
ARCH=$(uname -m)
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
install_name_tool -rpath @executable_path @executable_path/Frameworks bin/Release/net9.0/ios-arm64/publish/lib${APPNAME}.dylib 
install_name_tool -id @rpath/${APPNAME}.framework/${APPNAME} bin/Release/net9.0/ios-arm64/publish/lib${APPNAME}.dylib 
lipo -create  bin/Release/net9.0/ios-arm64/publish/lib${APPNAME}.dylib -output ios/frameworks/${APPNAME}.framework/${APPNAME}
cp -f scripts/Info.plist ios/frameworks/${APPNAME}.framework/Info.plist
aliassedinplace "s*TEMPLATE_PROJECT_NAME*$APPNAME*g" "ios/frameworks/${APPNAME}.framework/Info.plist"

cp -f scripts/CMakeLists.txt ios/CMakeLists.txt
aliassedinplace "s*TEMPLATE_PROJECT_NAME*$APPNAME*g" "ios/CMakeLists.txt"


rm -rf ios/build-xcode-ios
mkdir -p ios/build-xcode-ios
cd ios/build-xcode-ios

# Build cmake command with optional development team
CMAKE_CMD="cmake .. -G Xcode"
if [ -n "$DEVELOPMENT_TEAM" ]; then
    CMAKE_CMD="$CMAKE_CMD -DDEVELOPMENT_TEAM=$DEVELOPMENT_TEAM"
fi
echo "Running: $CMAKE_CMD"
eval $CMAKE_CMD
