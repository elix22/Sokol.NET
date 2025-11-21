# Multi-Device Installation Guide

## Overview

The SokolApplicationBuilder now supports installing APK/AAB files on multiple Android devices simultaneously. This feature is available for both APK and AAB builds through the interactive device selector.

## Features

- **Interactive Device Selection**: Choose from a list of connected devices
- **Device Information Display**: Shows manufacturer and model for easy identification
- **All Devices Option**: Install on all connected devices with a single command
- **Detailed Progress**: Real-time feedback for each device during installation
- **Summary Report**: Shows success/failure count when installing on multiple devices
- **Automatic App Launch**: Launches the app on each device after installation

## Usage

### VS Code Tasks (Recommended)

All Android tasks now support multi-device installation:

**APK Tasks:**
- Android: Install (Cube/Dyntex/CImGui/LoadPNG/Instancing/PlMpeg/Drawcallperf)
- Android: Install Debug (Cube/Dyntex/CImGui/LoadPNG/Instancing/PlMpeg/Drawcallperf)

**AAB Tasks:**
- Android AAB: Install (Cube/Dyntex/CImGui/LoadPNG/Instancing/PlMpeg/Drawcallperf)

### Command Line

```bash
# APK installation with interactive device selection
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --install \
  --interactive \
  --path /path/to/project

# AAB installation with interactive device selection
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --install \
  --interactive \
  --path /path/to/project
```

## Interactive Device Selection

When you run a build with `--interactive` flag and multiple devices are connected, you'll see:

```
📱 Multiple devices detected (2 devices):
======================================================
1) 03a824947d25 (Xiaomi Redmi 6A)
2) R8YW60MZRDV (samsung SM-X200)
3) All devices

Select device (1-3): 
```

### Options

1. **Select a specific device**: Enter `1` or `2` to install on that device only
2. **Install on all devices**: Enter `3` to install on all connected devices

## Installation Summary

When installing on multiple devices, you'll see progress for each device:

```
📱 Installing on device: 03a824947d25
✅ APK installed successfully on 03a824947d25!
✅ App launched successfully on 03a824947d25!

📱 Installing on device: R8YW60MZRDV
✅ APK installed successfully on R8YW60MZRDV!
✅ App launched successfully on R8YW60MZRDV!

📊 Installation Summary: 2 succeeded, 0 failed (Total: 2 devices)
```

## Use Cases

### QA Testing
Test your app on multiple device types and Android versions simultaneously:
```bash
# Connect: Xiaomi (Android 8), Samsung (Android 13), Pixel (Android 14)
# Run VS Code task: Android: Install (YourApp)
# Select: All devices
# Result: App installed and launched on all 3 devices
```

### Device Farm Testing
Deploy to multiple test devices in your CI/CD pipeline:
```bash
# Jenkins/GitHub Actions can use --interactive with piped input
echo "3" | dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --install \
  --interactive \
  --path /path/to/project
```

### Development Workflow
Keep your personal phone and tablet in sync:
```bash
# Daily development: select device 1 (phone)
# Before commit: select "All devices" to test on both
```

## Single Device Mode

If only one device is connected, the tool automatically selects it without prompting:

```
✅ Found single device: R8YW60MZRDV (samsung SM-X200)
```

## Specifying a Device

You can also specify a device ID directly to skip the interactive prompt:

```bash
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --install \
  --device R8YW60MZRDV \
  --path /path/to/project
```

## Non-Interactive Mode

Without the `--interactive` flag, the tool will use the first device with a warning:

```
⚠️  Using first device: 03a824947d25
warning: Multiple devices found. Using the first one. Use --device <device_id> to specify which device to use, or use --interactive for device selection.
```

## Technical Details

### APK Installation
- Builds APK once
- Installs on each selected device using `adb install -r`
- Launches app using `adb shell monkey`

### AAB Installation
- Builds AAB once
- For each device:
  - Gets device specifications (ABI, SDK version)
  - Converts AAB to universal APK using bundletool
  - Installs APK on device
  - Launches app
  - Cleans up temporary files

### Error Handling
- Each device installation is independent
- If one device fails, installation continues on remaining devices
- Final summary shows success/failure count
- Failed device installations are logged with details

## Troubleshooting

### No devices detected
```
❌ No Android devices found. Please connect a device and enable USB debugging.
```
**Solution**: Connect device via USB and enable Developer Options + USB Debugging

### Bundletool not found (AAB only)
```
❌ bundletool not found. AAB files cannot be directly installed on devices.
```
**Solution**: Download bundletool.jar and place in:
- `<project-root>/tools/bundletool.jar`, OR
- Anywhere in your Android SDK directory

### Installation failed on specific device
```
❌ Failed to install APK on device 03a824947d25!
```
**Solution**: 
- Check device storage space
- Verify USB debugging is enabled
- Try uninstalling existing app first: `adb -s 03a824947d25 uninstall com.elix22.yourapp`

## Benefits

✅ **Time Saving**: Install on 5 devices in the time it takes to install on 1  
✅ **QA Efficiency**: Test across multiple Android versions simultaneously  
✅ **Consistency**: Same build deployed to all devices  
✅ **Automation Ready**: Works in CI/CD pipelines with piped input  
✅ **User Friendly**: Clear device identification with manufacturer/model info  
✅ **Robust**: Handles failures gracefully, continues with remaining devices  

## Migration from Scripts

Previous workflow using shell scripts:
```bash
./scripts/interactive-android-install.sh examples/cube/cube.csproj
```

New integrated workflow:
```bash
# Use VS Code task: "Android: Install (Cube)"
# OR command line with same functionality:
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --install \
  --interactive \
  --path examples/cube
```

**Shell scripts are now deprecated** - all functionality is integrated into SokolApplicationBuilder.
