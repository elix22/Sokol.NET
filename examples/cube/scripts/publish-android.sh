

#!/bin/bash

# Android Publish Script
# Usage: ./publish-android.sh [install]
# Example: ./publish-android.sh
# Example: ./publish-android.sh true

# Function to install APK on connected Android device
install_apk_on_device() {
    # Get APP_NAME from parent directory if not set
    if [ -z "$APP_NAME" ]; then
        APP_NAME=$(basename "$(cd .. && pwd)")
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
    if [ -f "app/build/outputs/apk/debug/app-debug.apk" ]; then
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
            package_name=$(grep -o 'applicationId "[^"]*"' app/build.gradle 2>/dev/null | sed 's/applicationId "//;s/"//' | head -1)
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

# Get install flag if provided
INSTALL_APP=false
if [ $# -ge 1 ]; then
    if [ "$1" = "true" ] || [ "$1" = "1" ] || [ "$1" = "yes" ]; then
        INSTALL_APP=true
        echo "Will install APK on device after build"
    fi
fi

# Get the app name from the current directory
APP_NAME=$(basename "$(pwd)")

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
./gradlew assembleDebug -PcmakeArgs="-DAPP_NAME=$APP_NAME"

# Install APK on device if requested
if [ "$INSTALL_APP" = true ]; then
    install_apk_on_device
fi