#!/bin/bash

# Android Device Selection Handler
# This script checks for multiple devices and prompts for selection if needed

# Check if device ID was provided as argument
if [ "$1" ]; then
    echo "Using specified device: $1"
    export ANDROID_DEVICE_ID="$1"
    exit 0
fi

# Get list of connected devices
devices=$(adb devices | grep -E '\tdevice$' | cut -f1)
device_count=$(echo "$devices" | wc -l | xargs)

if [ "$device_count" -eq 0 ]; then
    echo "❌ No Android devices connected!"
    echo "Please connect an Android device and enable USB debugging."
    exit 1
elif [ "$device_count" -eq 1 ]; then
    device=$(echo "$devices" | head -n1)
    echo "✅ Using single connected device: $device"
    export ANDROID_DEVICE_ID="$device"
    exit 0
else
    echo "📱 Multiple Android devices detected ($device_count devices):"
    echo "=================================================="
    
    # Create numbered list with device info
    counter=1
    declare -a device_array
    
    for device in $devices; do
        # Try to get device model name
        model=$(adb -s "$device" shell getprop ro.product.model 2>/dev/null | tr -d '\r')
        manufacturer=$(adb -s "$device" shell getprop ro.product.manufacturer 2>/dev/null | tr -d '\r')
        
        if [ -n "$model" ] && [ -n "$manufacturer" ]; then
            echo "$counter) $device ($manufacturer $model)"
        else
            echo "$counter) $device"
        fi
        
        device_array[$counter]="$device"
        ((counter++))
    done
    
    echo ""
    echo -n "Select device (1-$device_count): "
    
    # Check if we're in an interactive terminal
    if [ -t 0 ]; then
        read selection
    else
        # Non-interactive mode - use zenity, osascript, or default to first device
        if command -v osascript >/dev/null 2>&1; then
            # macOS - use AppleScript dialog
            selection=$(osascript -e "tell app \"System Events\" to display dialog \"Select Android Device:\" & return & \"$(for i in $(seq 1 $device_count); do echo "$i) ${device_array[$i]}"; done | tr '\n' ' ')\" default answer \"1\" with title \"Android Device Selection\"" -e "text returned of result" 2>/dev/null)
        elif command -v zenity >/dev/null 2>&1; then
            # Linux - use zenity
            options=""
            for i in $(seq 1 $device_count); do
                options="$options $i ${device_array[$i]}"
            done
            selection=$(zenity --list --title="Select Android Device" --column="Number" --column="Device" $options 2>/dev/null)
        else
            # Fallback - just use first device
            echo "⚠️  Non-interactive mode detected. Using first device automatically."
            selection=1
        fi
    fi
    
    # Validate selection
    if [[ "$selection" =~ ^[0-9]+$ ]] && [ "$selection" -ge 1 ] && [ "$selection" -le "$device_count" ]; then
        selected_device="${device_array[$selection]}"
        echo "✅ Selected device: $selected_device"
        export ANDROID_DEVICE_ID="$selected_device"
        exit 0
    else
        echo "❌ Invalid selection. Please run the command again."
        exit 1
    fi
fi