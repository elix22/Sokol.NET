# Android Device Installation Guide

Installing Android applications is now simple and straightforward with automatic device selection.

For web/browser testing, see the browser launch configurations in VS Code for WebAssembly examples.

**✅ Cross-Platform Support**: Works on Windows, macOS, and Linux

## Available Tasks

### Device Information Tasks
- **Android: List Devices** - Shows detailed device information including model names

### Android Installation Tasks (Release Build - Optimized)
- **Android: Install (Cube)** - Build and install cube example with automatic device selection
- **Android: Install (Dyntex)** - Build and install dyntex example with automatic device selection
- **Android: Install (CImGui)** - Build and install cimgui example with automatic device selection
- **Android: Install (LoadPNG)** - Build and install loadpng example with automatic device selection
- **Android: Install (Instancing)** - Build and install instancing example with automatic device selection
- **Android: Install (PlMpeg)** - Build and install plmpeg example with automatic device selection

### Android Debug Installation Tasks (Debug Build - For Development)
- **Android: Install Debug (Cube)** - Build and install cube example (debug) with automatic device selection
- **Android: Install Debug (Dyntex)** - Build and install dyntex example (debug) with automatic device selection
- **Android: Install Debug (CImGui)** - Build and install cimgui example (debug) with automatic device selection
- **Android: Install Debug (LoadPNG)** - Build and install loadpng example (debug) with automatic device selection
- **Android: Install Debug (Instancing)** - Build and install instancing example (debug) with automatic device selection
- **Android: Install Debug (PlMpeg)** - Build and install plmpeg example (debug) with automatic device selection

## How to Use

### Simple Installation Process
Choose the appropriate task based on your needs:

**For Development/Debugging**: Use `Android: Install Debug (Example)` tasks
**For Testing/Performance**: Use `Android: Install (Example)` tasks (Release)

Both task types automatically handle device selection the same way.

## Build Configuration Differences

### Release Build (`Android: Install`)
- **Optimized**: Faster performance, smaller APK size
- **No Debug Info**: Harder to debug if issues occur
- **Best For**: Testing, performance evaluation, distribution

### Debug Build (`Android: Install Debug`)  
- **Debug Symbols**: Full debugging information included
- **Easier Debugging**: Can attach debuggers, better crash reports
- **Larger APK**: More debug information means bigger file size
- **Best For**: Development, troubleshooting, step-by-step debugging

**The task automatically handles:**
- **Single Device**: Installs immediately ✅
- **Multiple Devices**: Prompts you to choose which device 📱
- **No Devices**: Shows helpful error message ❌

## Platform Support

The Android device selection works seamlessly across all platforms:

- **Windows**: Uses PowerShell scripts (`.ps1`)
- **macOS**: Uses Bash scripts (`.sh`) 
- **Linux**: Uses Bash scripts (`.sh`)

VS Code automatically selects the correct script based on your operating system.

## Example Workflows

### Single Device (Automatic)
```bash
# Release build
Android: Install (Cube)
# Output:
# 🔍 Checking for Android devices...
# ✅ Found single device: 03a824947d25
# 🚀 Building and installing...
# [Build and install proceeds automatically]

# Debug build
Android: Install Debug (Cube)
# Output:
# 🔍 Checking for Android devices...
# ✅ Found single device: 03a824947d25
# 🚀 Building and installing (Debug)...
# [Build and install proceeds automatically]
```

### Multiple Devices (Interactive Selection)
```bash
# Release build
Android: Install (Cube)
# Output:
# 🔍 Checking for Android devices...
# 📱 Multiple devices detected (2 devices):
# ======================================================
# 1) 03a824947d25 (Xiaomi Redmi 6A)
# 2) R8YW60MZRDV (samsung SM-X200)
# 
# Select device (1-2): 1
# ✅ Selected device: 03a824947d25
# 🚀 Building and installing...
# [Build and install proceeds]

# Debug build
Android: Install Debug (Cube)
# Output:
# 🔍 Checking for Android devices...
# 📱 Multiple devices detected (2 devices):
# ======================================================
# 1) 03a824947d25 (Xiaomi Redmi 6A)
# 2) R8YW60MZRDV (samsung SM-X200)
# 
# Select device (1-2): 1
# ✅ Selected device: 03a824947d25
# 🚀 Building and installing (Debug)...
# [Build and install proceeds]
```

### No Devices
```bash
Android: Install (Cube) # or Android: Install Debug (Cube)
# Output:
# 🔍 Checking for Android devices...
# ❌ No Android devices connected!
# Please connect an Android device and enable USB debugging.
```

## Device ID Format

Device IDs are typically:
- Serial numbers (e.g., `03a824947d25`)
- IP addresses for wireless debugging (e.g., `192.168.1.100:5555`)
- USB device identifiers

## Troubleshooting

- **No devices shown**: Ensure USB debugging is enabled and devices are properly connected
- **Installation fails**: Verify the device ID is correct and the device is still connected
- **Permission denied**: Check that developer options and USB debugging are enabled

## Alternative Methods

You can also set the device ID directly in command line:
```bash
dotnet msbuild examples/cube/cube.csproj -t:BuildAndroidInstall -p:AndroidDeviceId=03a824947d25
```