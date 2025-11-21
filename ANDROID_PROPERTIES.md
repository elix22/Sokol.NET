
# Android Properties Configuration

## Overview

SokolApplicationBuilder supports dynamic AndroidManifest.xml generation based on properties defined in your project's `Directory.Build.props` file. This allows you to configure Android-specific settings such as permissions, SDK versions, and application properties without manually editing the manifest file.

## How It Works

When building for Android, SokolApplicationBuilder automatically:
1. Reads Android-related properties from `Directory.Build.props` in your project folder
2. Generates `AndroidManifest.xml` dynamically based on these properties
3. Falls back to sensible defaults if no properties are specified

## Supported Properties

### SDK Versions

```xml
<PropertyGroup>
  <!-- Minimum Android SDK version (default: 26) -->
  <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
  
  <!-- Target Android SDK version (default: 34) -->
  <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
</PropertyGroup>
```

### Permissions

Specify Android permissions as a semicolon-separated list:

```xml
<PropertyGroup>
  <AndroidPermissions>android.permission.RECORD_AUDIO;android.permission.WAKE_LOCK;android.permission.INTERNET;android.permission.WRITE_EXTERNAL_STORAGE</AndroidPermissions>
</PropertyGroup>
```

**Common Permissions:**
- `android.permission.INTERNET` - Network access
- `android.permission.RECORD_AUDIO` - Microphone access
- `android.permission.WAKE_LOCK` - Keep device awake
- `android.permission.WRITE_EXTERNAL_STORAGE` - Write to storage
- `android.permission.READ_EXTERNAL_STORAGE` - Read from storage
- `android.permission.CAMERA` - Camera access
- `android.permission.ACCESS_FINE_LOCATION` - GPS location
- `android.permission.VIBRATE` - Vibration control

**Default Permissions** (if not specified):
- `android.permission.RECORD_AUDIO`
- `android.permission.WAKE_LOCK`
- `android.permission.INTERNET`
- `android.permission.WRITE_EXTERNAL_STORAGE`

### Features

Specify Android hardware/software features as a semicolon-separated list. Append `:not-required` for optional features:

```xml
<PropertyGroup>
  <!-- Optional: Hardware features -->
  <AndroidFeatures>android.hardware.microphone:not-required;android.hardware.camera:not-required</AndroidFeatures>
</PropertyGroup>
```

**Common Features:**
- `android.hardware.camera` - Camera required
- `android.hardware.camera:not-required` - Camera optional
- `android.hardware.microphone` - Microphone required
- `android.hardware.microphone:not-required` - Microphone optional
- `android.hardware.touchscreen` - Touchscreen required
- `android.hardware.location.gps` - GPS required

### Application Properties

```xml
<PropertyGroup>
  <!-- Allow backup (default: false) -->
  <AndroidAllowBackup>false</AndroidAllowBackup>
  
  <!-- Full backup content (default: false) -->
  <AndroidFullBackupContent>false</AndroidFullBackupContent>
  
  <!-- Keep screen on (default: true) -->
  <AndroidKeepScreenOn>true</AndroidKeepScreenOn>
  
  <!-- Fullscreen mode - hide system UI (status bar, navigation bar) (default: false) -->
  <AndroidFullscreen>false</AndroidFullscreen>
  
  <!-- Note: AndroidHasCode is automatically set to true (includes SokolNativeActivity.java) -->
  
  <!-- Screen orientation (default: unspecified) -->
  <!-- Values: portrait, landscape, reverseLandscape, reversePortrait, sensorLandscape, -->
  <!--         sensorPortrait, sensor, fullSensor, nosensor, user, fullUser, locked, unspecified, behind -->
  <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
</PropertyGroup>
```

**Note**: The `--orientation` command-line flag takes precedence over `AndroidScreenOrientation` in `Directory.Build.props`.

### Screen Orientation

Control how your app handles device orientation changes:

```xml
<PropertyGroup>
  <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
</PropertyGroup>
```

**Available Values:**

| Value | Description |
|-------|-------------|
| `unspecified` | System chooses orientation (default) |
| `portrait` | Portrait orientation only |
| `landscape` | Landscape orientation only |
| `reverseLandscape` | Landscape orientation rotated 180° from normal landscape |
| `reversePortrait` | Portrait orientation rotated 180° from normal portrait |
| `sensorLandscape` | Landscape orientation, but can be normal or reverse based on sensor |
| `sensorPortrait` | Portrait orientation, but can be normal or reverse based on sensor |
| `sensor` | Orientation determined by device orientation sensor |
| `fullSensor` | Any of the 4 orientations determined by sensor |
| `nosensor` | Orientation determined without sensor (ignores physical rotation) |
| `user` | User's current preferred orientation |
| `fullUser` | All orientations user prefers |
| `locked` | Locks to current rotation |
| `behind` | Same orientation as activity below it |

**Common Use Cases:**

```xml
<!-- Landscape game -->
<AndroidScreenOrientation>landscape</AndroidScreenOrientation>

<!-- Portrait app that adapts to sensor -->
<AndroidScreenOrientation>sensorPortrait</AndroidScreenOrientation>

<!-- Fully rotating app -->
<AndroidScreenOrientation>fullSensor</AndroidScreenOrientation>
```

**Note**: The `--orientation` command-line flag takes precedence over this property.

### Fullscreen Mode

For truly immersive fullscreen experience (games, video players, VR apps), set `AndroidFullscreen` to `true`:

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <AndroidFullscreen>true</AndroidFullscreen>
</PropertyGroup>
```

When enabled, this will:
- Hide the status bar (top bar with clock, battery, notifications)
- Hide the navigation bar (bottom/side bar with back/home buttons)
- Create an edge-to-edge immersive experience using sticky immersive mode
- Uses `View.SYSTEM_UI_FLAG_FULLSCREEN`, `View.SYSTEM_UI_FLAG_HIDE_NAVIGATION`, and `View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY` flags
- Automatically sets `android:hasCode="true"` because it uses a custom `SokolNativeActivity` to enable immersive mode

**Technical Implementation**:
- Uses a custom Java activity (`com.sokol.app.SokolNativeActivity`) that extends `android.app.NativeActivity`
- For Android 11+ (API 30+): Uses modern `WindowInsetsController` API
- For Android 4.4-10 (API 19-29): Uses legacy `setSystemUiVisibility()` with deprecation suppressed
- Applies Android's immersive sticky mode for optimal fullscreen experience
- Automatically re-applies immersive mode when the app regains focus

**Note**: Users can temporarily reveal system UI by swiping from the edge of the screen. The UI will auto-hide after a few seconds (sticky immersive mode).

## Complete Example

Here's a complete example `Directory.Build.props` with Android configuration in a separate PropertyGroup:

```xml
<Project>
   <PropertyGroup>
      <BaseIntermediateOutputPath>obj\$(MSBuildProjectName)</BaseIntermediateOutputPath>
      
      <!-- Host OS Detection -->
      <IsWindowsHost>$([MSBuild]::IsOSPlatform('Windows'))</IsWindowsHost>
      <IsLinuxHost>$([MSBuild]::IsOSPlatform('Linux'))</IsLinuxHost>
      <IsOSXHost>$([MSBuild]::IsOSPlatform('OSX'))</IsOSXHost>
      <OSArch>$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)</OSArch>
      
      <!-- Host OS Mapping -->
      <HostOS Condition="'$(IsWindowsHost)'=='true'">windows</HostOS>
      <HostOS Condition="'$(IsLinuxHost)'=='true'">linux</HostOS>
      <HostOS Condition="'$(IsOSXHost)'=='true'">osx</HostOS>
      
      <!-- Sokol Shader Compiler Path -->
      <SokolShdcPath>../../tools/bin/$(HostOS)/$(OSArch)/sokol-shdc</SokolShdcPath>
      <SokolShdcPath Condition="'$(IsWindowsHost)'=='true'">$(SokolShdcPath).exe</SokolShdcPath>
   </PropertyGroup>

   <!-- Android Configuration -->
   <PropertyGroup>
      <!-- SDK Versions -->
      <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
      <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
      
      <!-- Permissions (semicolon-separated) -->
      <AndroidPermissions>android.permission.RECORD_AUDIO;android.permission.WAKE_LOCK;android.permission.INTERNET;android.permission.WRITE_EXTERNAL_STORAGE;android.permission.CAMERA</AndroidPermissions>
      
      <!-- Features (semicolon-separated, append :not-required for optional features) -->
      <AndroidFeatures>android.hardware.microphone:not-required;android.hardware.camera:not-required</AndroidFeatures>
      
      <!-- Application Properties -->
      <AndroidFullscreen>true</AndroidFullscreen>
      <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
      <AndroidKeepScreenOn>true</AndroidKeepScreenOn>
      <AndroidAllowBackup>false</AndroidAllowBackup>
      <AndroidFullBackupContent>false</AndroidFullBackupContent>
   </PropertyGroup>
   <!-- Note: AndroidHasCode is automatically set to true (includes SokolNativeActivity.java) -->

   <!-- Include source files -->
   <ItemGroup>
        <Compile Include="../../src/sokol/*.cs" >
        <Link>Sokol\%(Filename)%(Extension)</Link>
      </Compile>
      <Compile Include="../../src/sokol/generated/*.cs">
        <Link>Sokol\%(Filename)%(Extension)</Link>
      </Compile>
   </ItemGroup>

   <!-- Shader compilation -->
   <ItemGroup>
      <ShaderFiles Include="shaders/**/*.glsl" />
   </ItemGroup>

   <ItemGroup>
    <AssetsSourceFiles Include="Assets/**/*.*" />
  </ItemGroup>
</Project>
```

## Example: Game with Audio and Camera

For a game that needs audio recording and camera access:

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <!-- SDK Versions -->
  <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
  <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
  
  <!-- Permissions -->
  <AndroidPermissions>android.permission.INTERNET;android.permission.RECORD_AUDIO;android.permission.CAMERA;android.permission.WAKE_LOCK</AndroidPermissions>
  
  <!-- Features - optional, app works without camera/microphone -->
  <AndroidFeatures>android.hardware.camera:not-required;android.hardware.microphone:not-required</AndroidFeatures>
  
  <!-- Application Properties -->
  <AndroidKeepScreenOn>true</AndroidKeepScreenOn>
  <AndroidAllowBackup>false</AndroidAllowBackup>
</PropertyGroup>
```

## Example: Fullscreen Immersive Game

For an immersive fullscreen game without system UI:

```xml
<!-- Android Configuration -->
<PropertyGroup>
  <!-- SDK Versions -->
  <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
  <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
  
  <!-- Permissions -->
  <AndroidPermissions>android.permission.INTERNET;android.permission.WAKE_LOCK</AndroidPermissions>
  
  <!-- Application Properties -->
  <AndroidFullscreen>true</AndroidFullscreen>
  <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
  <AndroidKeepScreenOn>true</AndroidKeepScreenOn>
</PropertyGroup>
```

## Build Output

When building, you'll see output like:

```
📋 Read 7 Android properties from Directory.Build.props
   - AndroidMinSdkVersion: 26
   - AndroidTargetSdkVersion: 34
   - AndroidPermissions: android.permission.RECORD_AUDIO;android.permission.WAKE_LOCK;...
   - AndroidAllowBackup: false
   - AndroidFullBackupContent: false
   - AndroidHasCode: false
   - AndroidKeepScreenOn: true
✅ Generated AndroidManifest.xml with properties from Directory.Build.props
```

## No Configuration Required

If you don't include any Android properties in `Directory.Build.props`, SokolApplicationBuilder will use sensible defaults suitable for most Sokol.NET applications.

## Benefits

1. **Single Source of Truth**: All Android configuration in one place
2. **Version Control Friendly**: Track manifest changes through `Directory.Build.props`
3. **Maintainable**: Easy to update permissions and settings across multiple projects
4. **Flexible**: Override any setting per project
5. **Safe Defaults**: Works out of the box without configuration

## See Also

- [Android Permissions Documentation](https://developer.android.com/reference/android/Manifest.permission)
- [Android Features Documentation](https://developer.android.com/guide/topics/manifest/uses-feature-element)
- [Android SDK Versions](https://developer.android.com/tools/releases/platforms)
