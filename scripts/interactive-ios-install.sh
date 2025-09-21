#!/bin/bash

# Interactive iOS Install Script
# Usage: interactive-ios-install.sh <project_path>

PROJECT_PATH="$1"

if [ -z "$PROJECT_PATH" ]; then
    echo "❌ Error: Project path not specified"
    exit 1
fi

# Setup cache directory
CACHE_DIR="$HOME/.sokol-charp-cache"
PROJECT_CACHE_FILE="$CACHE_DIR/$(basename "$PROJECT_PATH" .csproj).teamid"

# Function to get cached team ID
get_cached_team_id() {
    if [ -f "$PROJECT_CACHE_FILE" ]; then
        cached_team_id=$(cat "$PROJECT_CACHE_FILE" 2>/dev/null)
        if [ -n "$cached_team_id" ] && [[ "$cached_team_id" =~ ^[A-Z0-9]{10}$ ]]; then
            echo "$cached_team_id"
            return 0
        fi
    fi
    return 1
}

# Function to save team ID to cache
save_team_id() {
    local team_id="$1"
    mkdir -p "$CACHE_DIR"
    echo "$team_id" > "$PROJECT_CACHE_FILE"
}

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
physical_devices=$(ios-deploy --detect --no-wifi 2>/dev/null | grep -E "^\[....\] Found" | sed 's/\[....\] Found //' | sed 's/ (.*)//')

# Get available simulators
simulators=$(xcrun simctl list devices available | grep -E "iPhone|iPad" | grep -v "unavailable" | sed 's/.*(\([A-F0-9\-]*\)) (.*)/\1/')

# Combine all devices
declare -a all_devices
declare -a device_types
counter=1

# Add physical devices
if [ -n "$physical_devices" ]; then
    while IFS= read -r device; do
        device_id=$(echo "$device" | cut -d' ' -f1)
        device_name=$(echo "$device" | cut -d' ' -f2-)
        all_devices[$counter]="$device_id"
        device_types[$counter]="physical:$device_name"
        ((counter++))
    done <<< "$physical_devices"
fi

# Add simulators
if [ -n "$simulators" ]; then
    while IFS= read -r sim_id; do
        sim_name=$(xcrun simctl list devices available | grep "$sim_id" | sed 's/.*iPhone/iPhone/' | sed 's/.*iPad/iPad/' | sed 's/ (.*)//')
        all_devices[$counter]="$sim_id"
        device_types[$counter]="simulator:$sim_name"
        ((counter++))
    done <<< "$simulators"
fi

total_devices=$((counter - 1))

# Get development team ID (check cache first)
development_team=$(get_cached_team_id)
if [ $? -eq 0 ]; then
    echo "✅ Using cached Development Team ID: $development_team"
    echo "   (Delete $PROJECT_CACHE_FILE to reset)"
else
    echo ""
    echo "🔑 iOS Development Team ID Required"
    echo "==================================="
    echo "Enter your Apple Developer Team ID (found in developer.apple.com/account):"
    echo -n "Development Team ID: "
    read development_team

    if [ -z "$development_team" ]; then
        echo "❌ Development Team ID is required for iOS builds"
        exit 1
    fi

    # Validate team ID format (should be 10 characters, alphanumeric)
    if [[ ! "$development_team" =~ ^[A-Z0-9]{10}$ ]]; then
        echo "⚠️  Team ID format looks incorrect (should be 10 alphanumeric characters)"
        echo "   Continuing anyway, but this may cause build failures..."
    fi

    # Save to cache
    save_team_id "$development_team"
    echo "💾 Team ID cached for future use"
fi

if [ "$total_devices" -eq 0 ]; then
    echo "❌ No iOS devices or simulators found!"
    echo "Please connect an iOS device or ensure Xcode simulators are available."
    exit 1
elif [ "$total_devices" -eq 1 ]; then
    device="${all_devices[1]}"
    device_type="${device_types[1]}"
    device_name=$(echo "$device_type" | cut -d: -f2)
    device_category=$(echo "$device_type" | cut -d: -f1)

    echo "✅ Found single $device_category: $device_name"
    echo "🚀 Building and installing..."

    if [ "$device_category" = "physical" ]; then
        dotnet msbuild "$PROJECT_PATH" -t:BuildIOSInstall -p:IOSDeviceId="$device" -p:IOSDevelopmentTeam="$development_team"
    else
        dotnet msbuild "$PROJECT_PATH" -t:BuildIOSSimulator -p:IOSSimulatorId="$device" -p:IOSDevelopmentTeam="$development_team"
    fi
else
    echo "📱 Multiple iOS devices/simulators detected ($total_devices total):"
    echo "================================================================="

    for i in $(seq 1 $total_devices); do
        device="${all_devices[$i]}"
        device_type="${device_types[$i]}"
        device_name=$(echo "$device_type" | cut -d: -f2)
        device_category=$(echo "$device_type" | cut -d: -f1)

        if [ "$device_category" = "physical" ]; then
            echo "$i) 📱 Physical: $device_name (ID: $device)"
        else
            echo "$i) 📱 Simulator: $device_name (ID: $device)"
        fi
    done

    echo ""

    # Use different input methods based on environment
    if [ -t 0 ] && [ -t 1 ]; then
        # Interactive terminal - direct input
        while true; do
            echo -n "Select device/simulator (1-$total_devices): "
            read selection

            if [[ "$selection" =~ ^[0-9]+$ ]] && [ "$selection" -ge 1 ] && [ "$selection" -le "$total_devices" ]; then
                selected_device="${all_devices[$selection]}"
                selected_type="${device_types[$selection]}"
                device_name=$(echo "$selected_type" | cut -d: -f2)
                device_category=$(echo "$selected_type" | cut -d: -f1)

                echo "✅ Selected $device_category: $device_name"
                echo "🚀 Building and installing..."

                if [ "$device_category" = "physical" ]; then
                    dotnet msbuild "$PROJECT_PATH" -t:BuildIOSInstall -p:IOSDeviceId="$selected_device" -p:IOSDevelopmentTeam="$development_team"
                else
                    dotnet msbuild "$PROJECT_PATH" -t:BuildIOSSimulator -p:IOSSimulatorId="$selected_device" -p:IOSDevelopmentTeam="$development_team"
                fi
                break
            else
                echo "❌ Invalid selection. Please enter a number between 1 and $total_devices."
            fi
        done
    else
        # Non-interactive (VS Code) - use GUI dialog on macOS
        if command -v osascript >/dev/null 2>&1; then
            # Build device list for dialog
            device_list=""
            for i in $(seq 1 $total_devices); do
                device="${all_devices[$i]}"
                device_type="${device_types[$i]}"
                device_name=$(echo "$device_type" | cut -d: -f2)
                device_category=$(echo "$device_type" | cut -d: -f1)

                if [ -n "$device_list" ]; then
                    device_list="$device_list, "
                fi

                if [ "$device_category" = "physical" ]; then
                    device_list="$device_list$i) 📱 $device_name"
                else
                    device_list="$device_list$i) 📱 Simulator: $device_name"
                fi
            done

            selection=$(osascript -e "tell app \"System Events\" to display dialog \"Select iOS Device/Simulator:\" & return & \"$device_list\" default answer \"1\" with title \"iOS Device Selection\"" -e "text returned of result" 2>/dev/null)

            if [[ "$selection" =~ ^[0-9]+$ ]] && [ "$selection" -ge 1 ] && [ "$selection" -le "$total_devices" ]; then
                selected_device="${all_devices[$selection]}"
                selected_type="${device_types[$selection]}"
                device_name=$(echo "$selected_type" | cut -d: -f2)
                device_category=$(echo "$selected_type" | cut -d: -f1)

                echo "✅ Selected $device_category: $device_name"
                echo "🚀 Building and installing..."

                if [ "$device_category" = "physical" ]; then
                    dotnet msbuild "$PROJECT_PATH" -t:BuildIOSInstall -p:IOSDeviceId="$selected_device" -p:IOSDevelopmentTeam="$development_team"
                else
                    dotnet msbuild "$PROJECT_PATH" -t:BuildIOSSimulator -p:IOSSimulatorId="$selected_device" -p:IOSDevelopmentTeam="$development_team"
                fi
            else
                echo "❌ Invalid selection or dialog cancelled."
                exit 1
            fi
        else
            # Fallback - use first device with warning
            selected_device="${all_devices[1]}"
            selected_type="${device_types[1]}"
            device_name=$(echo "$selected_type" | cut -d: -f2)
            device_category=$(echo "$selected_type" | cut -d: -f1)

            echo "⚠️  Non-interactive mode: Using first $device_category: $device_name"
            echo "🚀 Building and installing..."

            if [ "$device_category" = "physical" ]; then
                dotnet msbuild "$PROJECT_PATH" -t:BuildIOSInstall -p:IOSDeviceId="$selected_device"
            else
                dotnet msbuild "$PROJECT_PATH" -t:BuildIOSSimulator -p:IOSSimulatorId="$selected_device"
            fi
        fi
    fi
fi