

#!/bin/bash

# Android Build Script
# Usage: ./build-android.sh [install] [build_type]
# Examples:
#   ./build-android.sh                    # Build debug, no install
#   ./build-android.sh true               # Build debug, install on device
#   ./build-android.sh release            # Build release, no install
#   ./build-android.sh true release       # Build release, install on device

# Function to install APK on connected Android device
install_apk_on_device() {
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
        echo "Installing APK on this device..."
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

    # Find the APK file
    local apk_path=""
    if [ "$BUILD_TYPE" = "release" ] && [ -f "app/build/outputs/apk/release/app-release.apk" ]; then
        apk_path="app/build/outputs/apk/release/app-release.apk"
    elif [ "$BUILD_TYPE" = "debug" ] && [ -f "app/build/outputs/apk/debug/app-debug.apk" ]; then
        apk_path="app/build/outputs/apk/debug/app-debug.apk"
    else
        # Search for APK files
        apk_path=$(find . -name "*.apk" -type f 2>/dev/null | head -1)
    fi

    if [ -z "$apk_path" ]; then
        echo "Error: Could not find APK file!"
        return 1
    fi

    echo "Found APK: $apk_path"

    # Install the APK
    echo "Installing APK on device..."
    if adb -s "$selected_device_id" install -r "$apk_path"; then
        echo "APK installed successfully!"

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
        echo "Error: Failed to install APK on device!"
        return 1
    fi
}

# Parse command line arguments
INSTALL_APP=false
BUILD_TYPE="debug"

if [ $# -ge 1 ]; then
    case "$1" in
        "true"|"1"|"yes")
            INSTALL_APP=true
            echo "Will install APK on device after build"
            ;;
        "debug"|"release")
            BUILD_TYPE="$1"
            echo "Building $BUILD_TYPE version"
            ;;
        *)
            echo "Usage: $0 [install] [build_type]"
            echo "  install: true/1/yes to install on device after build"
            echo "  build_type: debug (default) or release"
            echo "Examples:"
            echo "  $0                    # Build debug, no install"
            echo "  $0 true               # Build debug, install on device"
            echo "  $0 release            # Build release, no install"
            echo "  $0 true release       # Build release, install on device"
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
    echo "Will install APK on device after build"
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
cp -r ./scripts/Android  .

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

# # compile shaders first
 dotnet msbuild -t:CompileShaders -p:DefineConstants="__ANDROID__"

dotnet publish -r linux-bionic-arm64 -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"
dotnet publish -r linux-bionic-arm -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"
dotnet publish -r linux-bionic-x64 -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants="__ANDROID__"

pwd
cd Android/native-activity
pwd

# Build the appropriate version
if [ "$BUILD_TYPE" = "release" ]; then
    ./gradlew assembleRelease

    # Sign the release APK with Android debug key
    echo "Signing release APK..."
    # Find apksigner in Android SDK
    APKSIGNER_PATH=$(find /Users/elialoni/Library/Android/sdk -name "apksigner" -type f 2>/dev/null | head -1)
    ZIPALIGN_PATH=$(find /Users/elialoni/Library/Android/sdk -name "zipalign" -type f 2>/dev/null | head -1)

    if [ -z "$APKSIGNER_PATH" ]; then
        echo "Warning: apksigner not found in Android SDK. Trying jarsigner..."
        if ! command -v jarsigner &> /dev/null; then
            echo "Warning: jarsigner not found. Please install Java JDK."
            echo "Release APK will remain unsigned."
        elif ! command -v keytool &> /dev/null; then
            echo "Warning: keytool not found. Please install Java JDK."
            echo "Release APK will remain unsigned."
        elif [ -f "app/build/outputs/apk/release/app-release-unsigned.apk" ]; then
            # Use Android debug keystore for signing
            DEBUG_KEYSTORE="$HOME/.android/debug.keystore"
            if [ ! -f "$DEBUG_KEYSTORE" ]; then
                echo "Creating Android debug keystore..."
                keytool -genkey -v -keystore "$DEBUG_KEYSTORE" -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
            fi

            echo "Signing APK with Android debug key..."
            # Copy unsigned APK to final name first
            cp "app/build/outputs/apk/release/app-release-unsigned.apk" "app/build/outputs/apk/release/app-release.apk"

            # Sign the APK
            jarsigner -keystore "$DEBUG_KEYSTORE" -storepass android -keypass android -digestalg SHA-256 -sigalg SHA256withRSA "app/build/outputs/apk/release/app-release.apk" androiddebugkey

            if [ $? -eq 0 ]; then
                echo "✅ APK signed successfully with jarsigner!"
                # Remove the unsigned APK
                rm -f "app/build/outputs/apk/release/app-release-unsigned.apk"
            else
                echo "❌ Warning: Failed to sign APK. Using unsigned APK."
            fi
        else
            echo "Warning: Unsigned APK not found at expected location."
        fi
    elif [ -f "app/build/outputs/apk/release/app-release-unsigned.apk" ]; then
        # Use Android debug keystore for signing
        DEBUG_KEYSTORE="$HOME/.android/debug.keystore"
        if [ ! -f "$DEBUG_KEYSTORE" ]; then
            echo "Creating Android debug keystore..."
            keytool -genkey -v -keystore "$DEBUG_KEYSTORE" -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
        fi

        echo "Signing APK with Android debug key using apksigner..."

        # Sign the APK with apksigner
        "$APKSIGNER_PATH" sign --ks "$DEBUG_KEYSTORE" --ks-pass pass:android --key-pass pass:android --out "app/build/outputs/apk/release/app-release.apk" "app/build/outputs/apk/release/app-release-unsigned.apk"

        if [ $? -eq 0 ]; then
            echo "✅ APK signed successfully with apksigner!"
            # Remove the unsigned APK
            rm -f "app/build/outputs/apk/release/app-release-unsigned.apk"
        else
            echo "❌ Warning: Failed to sign APK with apksigner. Using unsigned APK."
        fi
    else
        echo "Warning: Unsigned APK not found at expected location."
    fi
else
    ./gradlew assembleDebug -PcmakeArgs="-DAPP_NAME=$APP_NAME"
fi

# Install APK on device if requested
if [ "$INSTALL_APP" = true ]; then
    install_apk_on_device
fi