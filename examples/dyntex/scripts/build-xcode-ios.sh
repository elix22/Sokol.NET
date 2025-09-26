
#!/bin/bash

# iOS Build Script
# Usage: ./build-xcode-ios.sh <project-file.csproj> [development-team] [compile] [install]
# Example: ./build-xcode-ios.sh cube.csproj
# Example: ./build-xcode-ios.sh cube.csproj ABC123DEF4
# Example: ./build-xcode-ios.sh cube.csproj ABC123DEF4 true
# Example: ./build-xcode-ios.sh cube.csproj ABC123DEF4 true true

# Check if project file is provided as argument
if [ $# -lt 1 ]; then
    echo "Usage: $0 <project-file.csproj> [development-team] [compile] [install]"
    echo "Example: $0 cube.csproj"
    echo "Example: $0 cube.csproj ABC123DEF4"
    echo "Example: $0 cube.csproj ABC123DEF4 true"
    echo "Example: $0 cube.csproj ABC123DEF4 true true"
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


# Function to install app on connected iOS device
install_app_on_device() {
    local app_bundle_path=$1

    # Check if ios-deploy is installed
    if ! command -v ios-deploy &> /dev/null; then
        echo "Error: ios-deploy is not installed!"
        echo "Install it using: npm install -g ios-deploy"
        echo "Or using Homebrew: brew install ios-deploy"
        return 1
    fi

    echo "Detecting connected iOS devices..."

    # Get list of connected devices
    local devices_output
    devices_output=$(ios-deploy -c 2>/dev/null | grep -E "^\[....\]")

    if [ -z "$devices_output" ]; then
        echo "Error: No iOS devices found!"
        echo "Make sure your device is connected and unlocked."
        return 1
    fi

    # Parse devices into array
    local devices=()
    local device_ids=()
    local device_names=()

    while IFS= read -r line; do
        # Extract device ID and name
        local device_id=$(echo "$line" | sed -n 's/.*\[\([^]]*\)\].*/\1/p')
        local device_name=$(echo "$line" | sed -n 's/.*\]\s*\(.*\)\s*(.*/\1/p')

        if [ -n "$device_id" ] && [ -n "$device_name" ]; then
            devices+=("$device_id: $device_name")
            device_ids+=("$device_id")
            device_names+=("$device_name")
        fi
    done <<< "$devices_output"

    local num_devices=${#devices[@]}

    if [ $num_devices -eq 0 ]; then
        echo "Error: No valid iOS devices found!"
        return 1
    fi

    local selected_device_id=""

    if [ $num_devices -eq 1 ]; then
        # Only one device, use it automatically
        selected_device_id="${device_ids[0]}"
        echo "Found device: ${devices[0]}"
        echo "Installing app on this device..."
    else
        # Multiple devices, let user choose
        echo "Found $num_devices connected devices:"
        echo ""

        for i in "${!devices[@]}"; do
            echo "$((i+1)). ${devices[$i]}"
        done

        echo ""
        local choice=""
        while true; do
            read -p "Select device (1-$num_devices): " choice
            if [[ "$choice" =~ ^[0-9]+$ ]] && [ "$choice" -ge 1 ] && [ "$choice" -le $num_devices ]; then
                selected_device_id="${device_ids[$((choice-1))]}"
                echo "Selected device: ${devices[$((choice-1))]}"
                break
            else
                echo "Invalid choice. Please enter a number between 1 and $num_devices."
            fi
        done
    fi

    # Install the app
    echo "Installing app on device..."
    if ios-deploy --id "$selected_device_id" --bundle "$app_bundle_path" --no-wifi; then
        echo "App installed successfully on device!"
    else
        echo "Error: Failed to install app on device!"
        return 1
    fi
}

# Get the project file name and extract APPNAME
PROJECT_FILE=$1
APPNAME=$(basename "$PROJECT_FILE" .csproj)

# Get development team if provided
DEVELOPMENT_TEAM=""
if [ $# -ge 2 ]; then
    DEVELOPMENT_TEAM=$2
    echo "Using development team: $DEVELOPMENT_TEAM"
fi

# Get compile flag if provided
COMPILE_PROJECT=false
if [ $# -ge 3 ]; then
    if [ "$3" = "true" ] || [ "$3" = "1" ] || [ "$3" = "yes" ]; then
        COMPILE_PROJECT=true
        echo "Will compile Xcode project after generation"
    fi
fi

# Get install flag if provided
INSTALL_APP=false
if [ $# -ge 4 ]; then
    if [ "$4" = "true" ] || [ "$4" = "1" ] || [ "$4" = "yes" ]; then
        INSTALL_APP=true
        echo "Will install app on device after compilation"
    fi
fi

ROOT_FOLDER=$(pwd)
mkdir -p ios/sokol-ios
cd  ios/sokol-ios
cmake -G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 -DCMAKE_OSX_ARCHITECTURES="arm64" $ROOT_FOLDER/../../ext
cmake --build . --config Release

mkdir -p "../frameworks"
cp -rf Release-iphoneos/sokol.framework ../frameworks/

cd $ROOT_FOLDER

# compile shaders first
dotnet msbuild -t:CompileShaders -p:DefineConstants="__IOS__"

dotnet publish -r  ios-arm64 -c Release -p:BuildAsLibrary=true  -p:DefineConstants="__IOS__"

mkdir -p ios/frameworks/${APPNAME}.framework
install_name_tool -rpath @executable_path @executable_path/Frameworks bin/Release/net10.0/ios-arm64/publish/lib${APPNAME}.dylib 
install_name_tool -id @rpath/${APPNAME}.framework/${APPNAME} bin/Release/net10.0/ios-arm64/publish/lib${APPNAME}.dylib 
lipo -create  bin/Release/net10.0/ios-arm64/publish/lib${APPNAME}.dylib -output ios/frameworks/${APPNAME}.framework/${APPNAME}
cp -f scripts/Info.plist ios/frameworks/${APPNAME}.framework/Info.plist
aliassedinplace "s*TEMPLATE_PROJECT_NAME*$APPNAME*g" "ios/frameworks/${APPNAME}.framework/Info.plist"

cp -f scripts/CMakeLists.txt ios/CMakeLists.txt
aliassedinplace "s*TEMPLATE_PROJECT_NAME*$APPNAME*g" "ios/CMakeLists.txt"

cp -f scripts/main.m ios/main.m

mkdir -p ios/build-xcode-ios-app
cd ios/build-xcode-ios-app

# Build cmake command with optional development team
CMAKE_CMD="cmake .. -G Xcode"
if [ -n "$DEVELOPMENT_TEAM" ]; then
    CMAKE_CMD="$CMAKE_CMD -DDEVELOPMENT_TEAM=$DEVELOPMENT_TEAM"
fi
echo "Running: $CMAKE_CMD"
eval $CMAKE_CMD

# Compile the Xcode project if requested
if [ "$COMPILE_PROJECT" = true ]; then
    echo "Compiling Xcode project..."
    xcodebuild -configuration Release -sdk iphoneos -arch arm64
    if [ $? -eq 0 ]; then
        echo "Xcode project compiled successfully!"
        APP_BUNDLE_PATH="$(pwd)/Release-iphoneos/${APPNAME}-ios-app.app"
        echo "App bundle location: $APP_BUNDLE_PATH"

        # Check if app bundle exists
        if [ ! -d "$APP_BUNDLE_PATH" ]; then
            echo "Warning: App bundle not found at expected location"
            echo "Searching for app bundle in build directory..."

            # Try to find the app bundle
            FOUND_APP=$(find "$(pwd)" -name "*.app" -type d 2>/dev/null | head -1)
            if [ -n "$FOUND_APP" ]; then
                APP_BUNDLE_PATH="$FOUND_APP"
                echo "Found app bundle at: $APP_BUNDLE_PATH"
            else
                echo "Error: Could not find app bundle!"
                return 1
            fi
        fi

        # Install app on device if requested
        if [ "$INSTALL_APP" = true ]; then
            install_app_on_device "$APP_BUNDLE_PATH"
        fi
    else
        echo "Error: Xcode project compilation failed!"
        exit 1
    fi
else
    echo "Xcode project generated. To compile manually, run:"
    echo "cd ios/build-xcode-ios-app && xcodebuild -configuration Release -sdk iphoneos -arch arm64"
fi
