#!/bin/bash

# Interactive Android Install Script
# Usage: interactive-android-install.sh <project_path>

PROJECT_PATH="$1"

if [ -z "$PROJECT_PATH" ]; then
    echo "❌ Error: Project path not specified"
    exit 1
fi

echo "🔍 Checking for Android devices..."

# Get connected devices
devices=$(adb devices | grep -E '\tdevice$' | cut -f1)
device_count=$(echo "$devices" | wc -l | xargs)

if [ "$device_count" -eq 0 ]; then
    echo "❌ No Android devices connected!"
    echo "Please connect an Android device and enable USB debugging."
    exit 1
elif [ "$device_count" -eq 1 ]; then
    device=$(echo "$devices" | head -n1)
    echo "✅ Found single device: $device"
    echo "🚀 Building and installing..."
    dotnet msbuild "$PROJECT_PATH" -t:BuildAndroidInstall -p:AndroidDeviceId="$device"
else
    echo "📱 Multiple devices detected ($device_count devices):"
    echo "======================================================"
    
    # Create array and display options
    declare -a device_array
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
        
        device_array[$counter]="$dev"
        ((counter++))
    done
    
    echo ""
    
    # Use different input methods based on environment
    if [ -t 0 ] && [ -t 1 ]; then
        # Interactive terminal - direct input
        while true; do
            echo -n "Select device (1-$device_count): "
            read selection
            
            if [[ "$selection" =~ ^[0-9]+$ ]] && [ "$selection" -ge 1 ] && [ "$selection" -le "$device_count" ]; then
                selected_device="${device_array[$selection]}"
                echo "✅ Selected device: $selected_device"
                echo "🚀 Building and installing..."
                dotnet msbuild "$PROJECT_PATH" -t:BuildAndroidInstall -p:AndroidDeviceId="$selected_device"
                break
            else
                echo "❌ Invalid selection. Please enter a number between 1 and $device_count."
            fi
        done
    else
        # Non-interactive (VS Code) - use GUI dialog on macOS
        if command -v osascript >/dev/null 2>&1; then
            # Build device list for dialog
            device_list=""
            for i in $(seq 1 $device_count); do
                if [ -n "$device_list" ]; then
                    device_list="$device_list, "
                fi
                device_list="$device_list$i) ${device_array[$i]}"
            done
            
            selection=$(osascript -e "tell app \"System Events\" to display dialog \"Select Android Device:\" & return & \"$device_list\" default answer \"1\" with title \"Android Device Selection\"" -e "text returned of result" 2>/dev/null)
            
            if [[ "$selection" =~ ^[0-9]+$ ]] && [ "$selection" -ge 1 ] && [ "$selection" -le "$device_count" ]; then
                selected_device="${device_array[$selection]}"
                echo "✅ Selected device: $selected_device"
                echo "🚀 Building and installing..."
                dotnet msbuild "$PROJECT_PATH" -t:BuildAndroidInstall -p:AndroidDeviceId="$selected_device"
            else
                echo "❌ Invalid selection or dialog cancelled."
                exit 1
            fi
        else
            # Fallback - use first device with warning
            selected_device="${device_array[1]}"
            echo "⚠️  Non-interactive mode: Using first device: $selected_device"
            echo "🚀 Building and installing..."
            dotnet msbuild "$PROJECT_PATH" -t:BuildAndroidInstall -p:AndroidDeviceId="$selected_device"
        fi
    fi
fi