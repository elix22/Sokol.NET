#!/bin/bash

# Smart Android Install Script
# Usage: smart-android-install.sh <project_path>

PROJECT_PATH="$1"

if [ -z "$PROJECT_PATH" ]; then
    echo "❌ Error: Project path not specified"
    exit 1
fi

echo "🔍 Checking for Android devices..."

# Get connected devices
devices=$(adb devices | grep -E '\tdevice$' | cut -f1)
device_count=$(echo "$devices" | wc -l | xargs)

echo "Found $device_count device(s)"

if [ "$device_count" -eq 0 ]; then
    echo "❌ No Android devices connected!"
    echo "Please connect an Android device and enable USB debugging."
    exit 1
elif [ "$device_count" -eq 1 ]; then
    device=$(echo "$devices" | head -n1)
    echo "✅ Using single connected device: $device"
    echo "🚀 Building and installing to device..."
    dotnet msbuild "$PROJECT_PATH" -t:BuildAndroidInstall -p:AndroidDeviceId="$device"
else
    echo "📱 Multiple devices detected ($device_count devices):"
    echo "======================================================"
    
    counter=1
    for dev in $devices; do
        # Try to get device model name
        model=$(adb -s "$dev" shell getprop ro.product.model 2>/dev/null | tr -d '\r')
        manufacturer=$(adb -s "$dev" shell getprop ro.product.manufacturer 2>/dev/null | tr -d '\r')
        
        if [ -n "$model" ] && [ -n "$manufacturer" ]; then
            echo "$counter) $dev ($manufacturer $model)"
        else
            echo "$counter) $dev"
        fi
        ((counter++))
    done
    
    echo ""
    echo "🔧 Please use the 'Android: Install to Device' task for manual device selection."
    echo "   1. Run 'Android: List Devices' to see device details"
    echo "   2. Copy a Device ID from the list"
    echo "   3. Run 'Android: Install to Device (Example)' and paste the ID"
    exit 1
fi