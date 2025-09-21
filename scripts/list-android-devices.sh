#!/bin/bash

# List connected Android devices for VS Code input selection
# Returns device IDs that can be used with adb

# Get list of connected devices (excluding header and offline devices)
devices=$(adb devices | grep -E '\tdevice$' | cut -f1)

if [ -z "$devices" ]; then
    echo "No connected Android devices found."
    echo "Please connect an Android device and enable USB debugging."
    exit 1
fi

echo "Connected Android devices:"
echo "========================="

# Enhanced device listing with model names when possible
for device in $devices; do
    # Try to get device model name
    model=$(adb -s "$device" shell getprop ro.product.model 2>/dev/null | tr -d '\r')
    manufacturer=$(adb -s "$device" shell getprop ro.product.manufacturer 2>/dev/null | tr -d '\r')
    
    if [ -n "$model" ] && [ -n "$manufacturer" ]; then
        echo "Device ID: $device ($manufacturer $model)"
    else
        echo "Device ID: $device"
    fi
done

echo ""
echo "To use a specific device, copy the Device ID and use it in the VS Code task prompt."