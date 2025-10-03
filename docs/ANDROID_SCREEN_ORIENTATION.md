# AndroidScreenOrientation Property - Quick Reference

## Overview

The `AndroidScreenOrientation` property in `Directory.Build.props` allows you to control how your Android app handles device orientation. This property is read during the build process and applied to the generated `AndroidManifest.xml`.

## Usage

Add to your project's `Directory.Build.props` in the Android Configuration section:

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
</PropertyGroup>
```

## Precedence

The `--orientation` command-line flag takes precedence over the `AndroidScreenOrientation` property:
- Command-line flag specified → Uses command-line value
- Command-line flag not specified → Uses `AndroidScreenOrientation` from `Directory.Build.props`
- Neither specified → Defaults to `unspecified`

## All Supported Values

| Value | Android Constant | Description |
|-------|-----------------|-------------|
| `unspecified` | `SCREEN_ORIENTATION_UNSPECIFIED` | Default - system chooses orientation |
| `portrait` | `SCREEN_ORIENTATION_PORTRAIT` | Portrait orientation only |
| `landscape` | `SCREEN_ORIENTATION_LANDSCAPE` | Landscape orientation only |
| `reverseLandscape` | `SCREEN_ORIENTATION_REVERSE_LANDSCAPE` | Landscape rotated 180° from normal |
| `reversePortrait` | `SCREEN_ORIENTATION_REVERSE_PORTRAIT` | Portrait rotated 180° from normal |
| `sensorLandscape` | `SCREEN_ORIENTATION_SENSOR_LANDSCAPE` | Landscape (normal or reverse) based on sensor |
| `sensorPortrait` | `SCREEN_ORIENTATION_SENSOR_PORTRAIT` | Portrait (normal or reverse) based on sensor |
| `sensor` | `SCREEN_ORIENTATION_SENSOR` | Determined by device orientation sensor |
| `fullSensor` | `SCREEN_ORIENTATION_FULL_SENSOR` | Any of 4 orientations via sensor |
| `nosensor` | `SCREEN_ORIENTATION_NOSENSOR` | Determined without sensor |
| `user` | `SCREEN_ORIENTATION_USER` | User's current preferred orientation |
| `fullUser` | `SCREEN_ORIENTATION_FULL_USER` | All orientations user prefers |
| `locked` | `SCREEN_ORIENTATION_LOCKED` | Locks to current rotation |
| `behind` | `SCREEN_ORIENTATION_BEHIND` | Same as activity below it |

## Common Scenarios

### 1. Landscape Game

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
</PropertyGroup>
```

### 2. Portrait App with Sensor Support

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>sensorPortrait</AndroidScreenOrientation>
</PropertyGroup>
```

### 3. Fully Rotating App

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>fullSensor</AndroidScreenOrientation>
</PropertyGroup>
```

### 4. Fixed Portrait (No Rotation)

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>portrait</AndroidScreenOrientation>
</PropertyGroup>
```

### 5. Landscape with Reverse Support

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>sensorLandscape</AndroidScreenOrientation>
</PropertyGroup>
```

## Combined with Other Properties

### Fullscreen Landscape Game

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
  <AndroidFullscreen>true</AndroidFullscreen>
  <AndroidKeepScreenOn>true</AndroidKeepScreenOn>
</PropertyGroup>
```

### Portrait App with Camera

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>portrait</AndroidScreenOrientation>
  <AndroidPermissions>android.permission.CAMERA;android.permission.INTERNET</AndroidPermissions>
  <AndroidFeatures>android.hardware.camera:not-required</AndroidFeatures>
</PropertyGroup>
```

### Rotating Video Player

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidScreenOrientation>fullSensor</AndroidScreenOrientation>
  <AndroidFullscreen>true</AndroidFullscreen>
  <AndroidKeepScreenOn>true</AndroidKeepScreenOn>
</PropertyGroup>
```

## Testing

To verify your orientation setting, check the build output:

```
📋 Read 9 Android properties from Directory.Build.props
   - AndroidScreenOrientation: landscape
✅ Generated AndroidManifest.xml with properties from Directory.Build.props
```

Or inspect the generated manifest:

```bash
grep screenOrientation examples/cube/Android/native-activity/app/src/main/AndroidManifest.xml
```

Expected output:
```xml
android:screenOrientation="landscape"
```

## Troubleshooting

### Orientation Not Applied

**Problem**: App still rotates even though you set a fixed orientation

**Solution**: 
1. Ensure you've rebuilt the project after changing `Directory.Build.props`
2. Verify the property name is exactly `AndroidScreenOrientation` (case-sensitive)
3. Check for command-line `--orientation` flag that might override your setting
4. Inspect the generated `AndroidManifest.xml` to confirm the value

### Command-Line Override

**Problem**: Your `Directory.Build.props` setting is ignored

**Solution**: Check if you're using the `--orientation` flag in your build command. The command-line flag takes precedence:

```bash
# This will override Directory.Build.props
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture android --subtask apk \
  --orientation portrait \
  --path /path/to/project
```

To use the property from `Directory.Build.props`, remove the `--orientation` flag.

## Implementation Details

The orientation logic follows this priority:

1. **Command-line flag** (`--orientation portrait|landscape|both`)
2. **Directory.Build.props** (`<AndroidScreenOrientation>landscape</AndroidScreenOrientation>`)
3. **Default** (`unspecified`)

The property is read during the `ConfigureAndroidApp()` method and applied when generating the `AndroidManifest.xml` file.

## See Also

- [ANDROID_PROPERTIES.md](ANDROID_PROPERTIES.md) - Complete Android properties guide
- [Android Screen Orientation Documentation](https://developer.android.com/guide/topics/manifest/activity-element#screen)
- [Handling Configuration Changes](https://developer.android.com/guide/topics/resources/runtime-changes)
