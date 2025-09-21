#!/bin/bash

# List connected iOS devices and simulators for VS Code input selection
# Returns device IDs that can be used with ios-deploy

echo "🔍 Checking for iOS devices and simulators..."

# Check if ios-deploy is available
if ! command -v ios-deploy >/dev/null 2>&1; then
    echo "❌ ios-deploy not found!"
    echo "Install with: brew install ios-deploy"
    echo ""
    echo "Note: ios-deploy requires Xcode command line tools."
    exit 1
fi

# Get connected physical devices
echo "📱 Physical iOS Devices:"
echo "======================"
physical_devices=$(ios-deploy --detect --no-wifi 2>/dev/null | grep -E "^\[....\] Found" | sed 's/\[....\] Found //' | sed 's/ (.*)//')

if [ -n "$physical_devices" ]; then
    echo "$physical_devices" | while read -r device; do
        device_id=$(echo "$device" | cut -d' ' -f1)
        device_name=$(echo "$device" | cut -d' ' -f2-)
        echo "Device ID: $device_id ($device_name)"
    done
else
    echo "No physical iOS devices connected."
fi

echo ""
echo "📱 iOS Simulators:"
echo "=================="

# Get available simulators
simulators=$(xcrun simctl list devices available | grep -E "iPhone|iPad" | grep -v "unavailable" | sed 's/.*(\([A-F0-9\-]*\)) (.*)/\1/')

if [ -n "$simulators" ]; then
    echo "$simulators" | while read -r sim_id; do
        sim_name=$(xcrun simctl list devices available | grep "$sim_id" | sed 's/.*iPhone/iPhone/' | sed 's/.*iPad/iPad/' | sed 's/ (.*)//')
        echo "Simulator ID: $sim_id ($sim_name)"
    done
else
    echo "No iOS simulators available."
fi

echo ""
echo "To use a specific device/simulator, copy the Device ID/Simulator ID and use it in the VS Code task prompt."
echo ""
echo "Note: Physical devices require trust and pairing with Xcode."
echo "      Simulators can be launched directly."