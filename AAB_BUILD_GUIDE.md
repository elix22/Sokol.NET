# Android App Bundle (AAB) Build Guide

## Overview

The SokolApplicationBuilder now supports building Android App Bundles (AAB) in addition to APK files. AAB is the recommended format for publishing apps on Google Play Store as it provides smaller download sizes and better optimization for different device configurations.

## Quick Start (VS Code)

The easiest way to build and install AAB files is using the built-in VS Code tasks:

1. Press `Cmd+Shift+P` (macOS) or `Ctrl+Shift+P` (Windows/Linux)
2. Type "Run Task"
3. Select one of the AAB tasks:
   - **Android AAB: Install (Cube)** - Build and install Cube example as AAB
   - **Android AAB: Install (Dyntex)** - Build and install Dyntex example as AAB
   - **Android AAB: Install (CImGui)** - Build and install CImGui example as AAB
   - **Android AAB: Install (LoadPNG)** - Build and install LoadPNG example as AAB
   - **Android AAB: Install (Instancing)** - Build and install Instancing example as AAB
   - **Android AAB: Install (PlMpeg)** - Build and install PlMpeg example as AAB
   - **Android AAB: Install (Drawcallperf)** - Build and install Drawcallperf example as AAB

These tasks will:
- Automatically detect connected Android devices
- Show a device selector if multiple devices are connected
- Allow you to **install on a specific device OR all connected devices at once**
- Build the AAB file
- Convert to APK using bundletool
- Install on the selected device(s)
- Launch the app on each device

## Prerequisites

### Required Tools

1. **Java JDK** - Required for signing and bundletool
   ```bash
   java -version  # Should be installed
   ```

2. **bundletool** (for local AAB testing) - Optional but recommended for installing AAB on devices
   - Download from: https://github.com/google/bundletool/releases
   - Place in one of these locations:
     - `<project-root>/tools/bundletool.jar`
     - Anywhere in your `ANDROID_SDK_ROOT` or `ANDROID_HOME`

3. **Android Debug Keystore** - Created automatically if not present at `~/.android/debug.keystore`

## Building AAB Files

### Basic Usage

To build an AAB instead of an APK, add the `--subtask aab` parameter:

```bash
# Build debug AAB
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --path /path/to/your/project

# Build release AAB
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --type release \
  --path /path/to/your/project
```

### Build and Install

To build and install on a connected device:

```bash
# Build debug AAB and install
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --install \
  --path /path/to/your/project

# Build release AAB and install on specific device
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --type release \
  --install \
  --device <device-id> \
  --path /path/to/your/project
```

## Output Locations

After building, AAB files are located at:

- **Debug AAB**: `<project>/Android/native-activity/app/build/outputs/bundle/debug/app-debug.aab`
- **Release AAB**: `<project>/Android/native-activity/app/build/outputs/bundle/release/app-release.aab`

## Testing AAB Locally

### Option 1: Using SokolApplicationBuilder (Automatic)

The builder automatically converts AAB to APK and installs when you use `--install`:

```bash
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --install \
  --path /path/to/your/project
```

#### Installing on Multiple Devices

When you have multiple Android devices connected, you can:

1. **Interactive Mode** - Select a specific device or install on all devices:
   ```bash
   dotnet run --project tools/SokolApplicationBuilder \
     --task build \
     --architecture android \
     --subtask aab \
     --install \
     --interactive \
     --path /path/to/your/project
   ```
   
   The device selector will show:
   ```
   📱 Multiple devices detected (2 devices):
   ======================================================
   1) 03a824947d25 (Xiaomi Redmi 6A)
   2) R8YW60MZRDV (samsung SM-X200)
   3) All devices
   
   Select device (1-3): 3
   ✅ Selected all devices (2 devices)
   ```

2. **Install on all devices automatically** - The "All devices" option will:
   - Build the AAB once
   - Convert to APK for each device
   - Install on each device sequentially
   - Launch the app on each device
   - Show a summary of successful/failed installations

This is particularly useful for:
- Testing on multiple device types simultaneously
- QA testing across different Android versions
- Deploying to a device farm or test lab

### Option 2: Manual Testing with bundletool

If you want to manually test the AAB:

1. **Generate APKs from AAB**:
   ```bash
   java -jar bundletool.jar build-apks \
     --bundle=app-release.aab \
     --output=app.apks \
     --mode=universal
   ```

2. **Extract universal APK**:
   ```bash
   unzip app.apks universal.apk
   ```

3. **Install APK**:
   ```bash
   adb install -r universal.apk
   ```

## Publishing to Google Play

### Steps to Publish

1. **Build a signed release AAB**:
   ```bash
   dotnet run --project tools/SokolApplicationBuilder \
     --task build \
     --architecture android \
     --subtask aab \
     --type release \
     --path /path/to/your/project
   ```

2. **Sign with your production keystore** (if not using debug):
   ```bash
   jarsigner -keystore /path/to/your/keystore.jks \
     -storepass your_store_pass \
     -keypass your_key_pass \
     app-release.aab \
     your_key_alias
   ```

3. **Upload to Google Play Console**:
   - Go to https://play.google.com/console
   - Select your app
   - Navigate to "Release" → "Production" (or Testing tracks)
   - Click "Create new release"
   - Upload your `app-release.aab`

### Signing Notes

- **Debug builds**: Automatically signed with Android debug keystore (`~/.android/debug.keystore`)
- **Release builds**: Signed with debug keystore by default (for testing only)
- **Production**: You should sign with your production keystore before uploading to Google Play

## APK vs AAB Comparison

| Feature | APK | AAB |
|---------|-----|-----|
| Google Play requirement | ❌ Old method | ✅ Required for new apps |
| File size | Larger | Smaller (optimized per device) |
| Direct installation | ✅ Yes | ⚠️ Requires conversion |
| Local testing | Easy | Requires bundletool |
| Play Store benefits | None | Dynamic delivery, instant apps |

## Troubleshooting

### "bundletool not found"

**Problem**: AAB can't be installed on device because bundletool is missing.

**Solutions**:
1. Download bundletool from: https://github.com/google/bundletool/releases
2. Place it in `<project-root>/tools/bundletool.jar`
3. Or set `ANDROID_SDK_ROOT` environment variable

### "Failed to sign AAB"

**Problem**: jarsigner failed to sign the AAB.

**Solutions**:
1. Ensure Java JDK is installed (not just JRE)
2. Check that `~/.android/debug.keystore` exists
3. For production, use your own keystore:
   ```bash
   jarsigner -keystore your-keystore.jks \
     -storepass your-pass \
     app-release.aab \
     your-alias
   ```

### "No devices found"

**Problem**: Can't install AAB because no Android device is connected.

**Solutions**:
1. Connect your Android device via USB
2. Enable USB debugging in Developer Options
3. Run `adb devices` to verify connection
4. Accept USB debugging prompt on device

### Multiple devices connected

**Problem**: Multiple devices connected and builder uses wrong one.

**Solution**: Specify device ID:
```bash
# List devices
adb devices

# Build with specific device
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --install \
  --device <device-id> \
  --path /path/to/your/project
```

## Examples

### Example 1: Quick debug build and test
```bash
cd /path/to/your/project
dotnet run --project ../../tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --install
```

### Example 2: Release build for Google Play
```bash
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --type release \
  --path /path/to/your/project

# Output: Android/native-activity/app/build/outputs/bundle/release/app-release.aab
# Upload this to Google Play Console
```

### Example 3: Build both APK and AAB
```bash
# Build APK
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --path /path/to/your/project

# Build AAB
dotnet run --project tools/SokolApplicationBuilder \
  --task build \
  --architecture android \
  --subtask aab \
  --path /path/to/your/project
```

## Best Practices

1. **Development**: Use APK for faster iteration
   - APK builds are faster
   - No bundletool required
   - Direct installation

2. **Testing**: Use AAB before release
   - Test the same format you'll publish
   - Verify on multiple devices
   - Check app size optimization

3. **Production**: Always use AAB
   - Required by Google Play
   - Smaller downloads for users
   - Better device optimization

4. **Signing**: Use proper keystores
   - Development: Debug keystore is fine
   - Testing: Debug keystore acceptable
   - Production: Use your production keystore

## Additional Resources

- [Android App Bundle Documentation](https://developer.android.com/guide/app-bundle)
- [bundletool Guide](https://developer.android.com/tools/bundletool)
- [Google Play Publishing Guide](https://developer.android.com/distribute/best-practices/launch/launch-checklist)
