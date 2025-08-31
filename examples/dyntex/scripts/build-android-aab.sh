#!/bin/bash

# Android AAB Build Script
# Usage: ./build-android-aab.sh [install] [build_type]
# Examples:
#   ./build-android-aab.sh                    # Build debug AAB, no install
#   ./build-android-aab.sh true               # Build debug AAB, install on device
#   ./build-android-aab.sh release            # Build release AAB, no install
#   ./build-android-aab.sh true release       # Build release AAB, install on device

# Function to install AAB on connected Android device
install_aab_on_device() {
    # Get APP_NAME from csproj file or parent directory if not set
    if [ -z "$APP_NAME" ]; then
        if [ -f "../*.csproj" ]; then
            # Extract app name from csproj filename (remove .csproj extension)
            APP_NAME=$(ls ../*.csproj | head -1 | sed 's/\.csproj$//')
        else
            # Fallback to directory name
            APP_NAME=$(basename "$(cd .. && pwd)")
        fi
    fi
    # Check if adb is installed
    if ! command -v adb &> /dev/null; then
        echo "Error: adb is not installed!"
        echo "Install Android SDK platform tools to get adb"
        return 1
    fi

    echo "Detecting connected Android devices..."

    # Get list of connected devices
    local devices_output
    devices_output=$(adb devices | grep -v "List of devices" | grep -v "^$")

    if [ -z "$devices_output" ]; then
        echo "Error: No Android devices found!"
        echo "Make sure your device is connected and USB debugging is enabled."
        return 1
    fi

    # Parse devices into array
    local devices=()
    local device_ids=()

    while IFS= read -r line; do
        # Extract device ID (first column)
        local device_id=$(echo "$line" | awk '{print $1}')
        local device_status=$(echo "$line" | awk '{print $2}')

        if [ -n "$device_id" ] && [ "$device_status" = "device" ]; then
            devices+=("$device_id")
            device_ids+=("$device_id")
        fi
    done <<< "$devices_output"

    local num_devices=${#devices[@]}

    if [ $num_devices -eq 0 ]; then
        echo "Error: No valid Android devices found!"
        return 1
    fi

    local selected_device_id=""

    if [ $num_devices -eq 1 ]; then
        # Only one device, use it automatically
        selected_device_id="${device_ids[0]}"
        echo "Found device: ${devices[0]}"
        echo "Installing AAB on this device..."
    else
        # Multiple devices, let user choose
        echo "Found $num_devices connected devices:"
        echo ""

        for i in "${!devices[@]}"; do
            # Get device model name
            local device_model=$(adb -s "${devices[$i]}" shell getprop ro.product.model 2>/dev/null | tr -d '\r')
            if [ -z "$device_model" ]; then
                device_model="Unknown Device"
            fi
            echo "$((i+1)). ${devices[$i]} ($device_model)"
        done

        echo ""
        local choice=""
        while true; do
            read -p "Select device (1-$num_devices): " choice
            if [[ "$choice" =~ ^[0-9]+$ ]] && [ "$choice" -ge 1 ] && [ "$choice" -le $num_devices ]; then
                selected_device_id="${device_ids[$((choice-1))]}"
                local device_model=$(adb -s "$selected_device_id" shell getprop ro.product.model 2>/dev/null | tr -d '\r')
                echo "Selected device: $selected_device_id ($device_model)"
                break
            else
                echo "Invalid choice. Please enter a number between 1 and $num_devices."
            fi
        done
    fi

    # Find the AAB file
    local aab_path=""
    if [ "$BUILD_TYPE" = "release" ] && [ -f "app/build/outputs/bundle/release/app-release.aab" ]; then
        aab_path="app/build/outputs/bundle/release/app-release.aab"
    elif [ "$BUILD_TYPE" = "debug" ] && [ -f "app/build/outputs/bundle/debug/app-debug.aab" ]; then
        aab_path="app/build/outputs/bundle/debug/app-debug.aab"
    else
        # Search for AAB files
        aab_path=$(find . -name "*.aab" -type f 2>/dev/null | head -1)
    fi

    if [ -z "$aab_path" ]; then
        echo "Error: Could not find AAB file!"
        return 1
    fi

    echo "Found AAB: $aab_path"

    # Convert AAB to APK and install
    echo "Converting AAB to APK for device installation..."

    # Create a temporary directory for the conversion
    local temp_dir=$(mktemp -d)
    local apk_path="$temp_dir/app.apk"

    # Try to use bundletool if available
    local bundletool_path=""

    # First check local tools folder
    if [ -f "../../tools/bundletool.jar" ]; then
        bundletool_path="../../tools/bundletool.jar"
    # Then check Android SDK
    elif [ -n "$(find /Users/elialoni/Library/Android/sdk -name "bundletool*.jar" -type f 2>/dev/null | head -1)" ]; then
        bundletool_path=$(find /Users/elialoni/Library/Android/sdk -name "bundletool*.jar" -type f 2>/dev/null | head -1)
    fi

    if [ -n "$bundletool_path" ] && command -v java &> /dev/null; then
        echo "Using bundletool to convert AAB to APK..."

        # Get device specifications for bundletool
        local device_spec="$temp_dir/device-spec.json"
        adb -s "$selected_device_id" shell getprop | grep -E "(ro.product.cpu.abi|ro.build.version.sdk)" > "$temp_dir/device_props.txt"

        # Create device spec file
        cat > "$device_spec" << EOF
{
  "supportedAbis": ["$(adb -s "$selected_device_id" shell getprop ro.product.cpu.abi | tr -d '\r')"],
  "supportedLocales": ["en-US"],
  "deviceFeatures": [],
  "glExtensions": [],
  "screenDensity": 420,
  "sdkVersion": $(adb -s "$selected_device_id" shell getprop ro.build.version.sdk | tr -d '\r')
}
EOF

        # Convert AAB to APK using bundletool (universal mode for all architectures)
        java -jar "$bundletool_path" build-apks \
            --bundle="$aab_path" \
            --output="$temp_dir/app.apks" \
            --mode=universal

        if [ $? -eq 0 ]; then
            # Extract the universal APK from the .apks file
            unzip -q "$temp_dir/app.apks" -d "$temp_dir"
            if [ -f "$temp_dir/universal.apk" ]; then
                apk_path="$temp_dir/universal.apk"
                echo "✅ AAB converted to APK successfully!"
            else
                echo "❌ Failed to extract universal APK from bundle"
                rm -rf "$temp_dir"
                return 1
            fi
        else
            echo "❌ Failed to convert AAB to APK using bundletool"
            rm -rf "$temp_dir"
            return 1
        fi
    else
        echo "Warning: bundletool not found. AAB files cannot be directly installed on devices."
        echo "To install AAB files, you need to:"
        echo "1. Install bundletool: https://developer.android.com/tools/bundletool"
        echo "2. Or upload to Google Play Console for testing"
        echo "3. Or use internal app sharing"
        rm -rf "$temp_dir"
        return 1
    fi

    # Install the converted APK
    echo "Installing APK on device..."
    if adb -s "$selected_device_id" install -r "$apk_path"; then
        echo "✅ AAB installed successfully on device!"

        # Get package name from build.gradle or use default
        local package_name="com.elix22.$APP_NAME"

        # Try to get package name from build.gradle
        if [ -f "app/build.gradle" ]; then
            package_name=$(grep -o "applicationId = '[^']*'" app/build.gradle 2>/dev/null | sed "s/applicationId = '//;s/'//" | head -1)
        fi

        if [ -z "$package_name" ]; then
            package_name="com.elix22.$APP_NAME"
        fi

        echo "Launching app (package: $package_name)..."

        # Try multiple methods to launch the app
        local launch_success=false

        # Method 1: Try monkey command (most reliable for native activities)
        if adb -s "$selected_device_id" shell monkey -p "$package_name" -c android.intent.category.LAUNCHER 1 2>/dev/null | grep -q "Events injected"; then
            echo "App launched successfully using monkey!"
            launch_success=true
        fi

        # Method 2: Try am start with explicit NativeActivity if monkey failed
        if [ "$launch_success" = false ]; then
            if adb -s "$selected_device_id" shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -n "$package_name/android.app.NativeActivity" 2>/dev/null; then
                echo "App launched successfully using am start with NativeActivity!"
                launch_success=true
            fi
        fi

        # Method 3: Try generic intent if specific methods failed
        if [ "$launch_success" = false ]; then
            if adb -s "$selected_device_id" shell am start -a android.intent.action.MAIN -c android.intent.category.LAUNCHER 2>/dev/null; then
                echo "App launched successfully using generic intent!"
                launch_success=true
            fi
        fi

        if [ "$launch_success" = false ]; then
            echo "Warning: Could not launch app automatically"
            echo "Package name used: $package_name"
            echo "You can launch it manually from the device"
            echo "Or try: adb -s $selected_device_id shell monkey -p $package_name 1"
        fi
    else
        echo "❌ Error: Failed to install APK on device!"
        return 1
    fi

    # Clean up temporary files
    rm -rf "$temp_dir"
}

# Parse command line arguments
INSTALL_APP=false
BUILD_TYPE="debug"

if [ $# -ge 1 ]; then
    case "$1" in
        "true"|"1"|"yes")
            INSTALL_APP=true
            echo "Will install AAB on device after build"
            ;;
        "debug"|"release")
            BUILD_TYPE="$1"
            echo "Building $BUILD_TYPE AAB"
            ;;
        *)
            echo "Usage: $0 [install] [build_type]"
            echo "  install: true/1/yes to install on device after build"
            echo "  build_type: debug (default) or release"
            echo "Examples:"
            echo "  $0                    # Build debug AAB, no install"
            echo "  $0 true               # Build debug AAB, install on device"
            echo "  $0 release            # Build release AAB, no install"
            echo "  $0 true release       # Build release AAB, install on device"
            exit 1
            ;;
    esac
fi

if [ $# -ge 2 ]; then
    case "$2" in
        "debug"|"release")
            BUILD_TYPE="$2"
            ;;
        "true"|"1"|"yes")
            INSTALL_APP=true
            ;;
    esac
fi

echo "Build type: $BUILD_TYPE"
if [ "$INSTALL_APP" = true ]; then
    echo "Will install AAB on device after build"
fi

# Get the app name from the current directory or csproj file
if [ -f "*.csproj" ]; then
    # Extract app name from csproj filename (remove .csproj extension)
    APP_NAME=$(ls *.csproj | head -1 | sed 's/\.csproj$//')
else
    # Fallback to directory name
    APP_NAME=$(basename "$(pwd)")
fi

pwd
cp -r ./Android  .

# Rename NativeActivity references to actual app name
echo "Configuring Android app for: $APP_NAME"

# Update package name in AndroidManifest.xml
if [ -f "Android/native-activity/app/src/main/AndroidManifest.xml" ]; then
    sed -i.bak "s/android:label=\"NativeActivity\"/android:label=\"$APP_NAME\"/" Android/native-activity/app/src/main/AndroidManifest.xml
    rm -f Android/native-activity/app/src/main/AndroidManifest.xml.bak
fi

# Update app name in build.gradle
if [ -f "Android/native-activity/app/build.gradle" ]; then
    sed -i.bak "s/applicationId = 'com\.example\.native_activity'/applicationId = 'com.elix22.$APP_NAME'/" Android/native-activity/app/build.gradle
    sed -i.bak "s/namespace 'com\.example\.native_activity'/namespace 'com.elix22.$APP_NAME'/" Android/native-activity/app/build.gradle
    rm -f Android/native-activity/app/build.gradle.bak
fi

# Update app name in strings.xml
if [ -f "Android/native-activity/app/src/main/res/values/strings.xml" ]; then
    sed -i.bak "s/NativeActivity/$APP_NAME/" Android/native-activity/app/src/main/res/values/strings.xml
    rm -f Android/native-activity/app/src/main/res/values/strings.xml.bak
fi

# Update CMakeLists.txt template
if [ -f "Android/native-activity/app/src/main/cpp/CMakeLists.txt" ]; then
    sed -i.bak "s/NativeActivity/$APP_NAME/" Android/native-activity/app/src/main/cpp/CMakeLists.txt
    rm -f Android/native-activity/app/src/main/cpp/CMakeLists.txt.bak
fi

# Update MainActivity name in Java/Kotlin files if they exist
find Android/native-activity -name "*.java" -o -name "*.kt" | while read -r file; do
    sed -i.bak "s/NativeActivity/$APP_NAME/" "$file"
    rm -f "${file}.bak"
done

echo "Android app configured for $APP_NAME"

# Compile shaders first
dotnet msbuild -t:CompileShaders -p:DefineConstants="__ANDROID__"

dotnet publish -r linux-bionic-arm64 -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"
dotnet publish -r linux-bionic-arm -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"
dotnet publish -r linux-bionic-x64 -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:DefineConstants="__ANDROID__"

pwd
cd Android/native-activity
pwd

# Build the appropriate AAB version
if [ "$BUILD_TYPE" = "release" ]; then
    ./gradlew bundleRelease -PcmakeArgs="-DAPP_NAME=$APP_NAME"

    # Sign the release AAB with Android debug key
    echo "Signing release AAB..."
    # Find apksigner in Android SDK
    APKSIGNER_PATH=$(find /Users/elialoni/Library/Android/sdk -name "apksigner" -type f 2>/dev/null | head -1)

    if [ -f "app/build/outputs/bundle/release/app-release.aab" ]; then
        # Use Android debug keystore for signing
        DEBUG_KEYSTORE="$HOME/.android/debug.keystore"
        if [ ! -f "$DEBUG_KEYSTORE" ]; then
            echo "Creating Android debug keystore..."
            keytool -genkey -v -keystore "$DEBUG_KEYSTORE" -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
        fi

        echo "Signing AAB with Android debug key using jarsigner..."

        # Sign the AAB with jarsigner (AAB files use JAR signing)
        jarsigner -keystore "$DEBUG_KEYSTORE" -storepass android -keypass android -digestalg SHA-256 -sigalg SHA256withRSA "app/build/outputs/bundle/release/app-release.aab" androiddebugkey

        if [ $? -eq 0 ]; then
            echo "✅ AAB signed successfully with jarsigner!"
        else
            echo "❌ Warning: Failed to sign AAB. Using unsigned AAB."
        fi
    else
        echo "Warning: AAB not found at expected location."
    fi
else
    ./gradlew bundleDebug -PcmakeArgs="-DAPP_NAME=$APP_NAME"
fi

# Install AAB on device if requested
if [ "$INSTALL_APP" = true ]; then
    install_aab_on_device
fi
