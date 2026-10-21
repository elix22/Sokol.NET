
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
  
  <!-- Target Android SDK version (default: 36 — Google Play requires Android 16 / API 36+) -->
  <AndroidTargetSdkVersion>36</AndroidTargetSdkVersion>
</PropertyGroup>
```

**Important for Google Play Submission:**

- **API 36 Required**: Google Play requires new apps and updates to target Android 16 (API 36) or higher.
  `AndroidTargetSdkVersion` drives both `targetSdk` and `compileSdk` in the generated Gradle project, so
  lowering it below 36 will make Play reject the upload.
- **NDK 27+ Required**: Google Play requires 16KB page size support for apps targeting Android 15+ (API 35+)
- **Automatic Selection**: The build system automatically selects NDK 27+ when available, even if older NDK versions are present in environment variables
- **Recommended**: Install NDK 29 or later for best compatibility
- All native libraries are automatically built with 16KB alignment (`-Wl,-z,max-page-size=16384`)
- System libraries (like `libc++_shared.so`) from NDK 27+ are properly aligned for 16KB pages

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
- `android.permission.WAKE_LOCK`
- `android.permission.INTERNET`

#### Limiting a permission to old Android versions (`:maxSdk=NN`)

Append `:maxSdk=NN` to emit `android:maxSdkVersion="NN"` — the permission then applies only up to
that API level. Use it for permissions a newer Android replaced, so modern devices are never asked
for something they do not need:

```xml
<AndroidPermissions>android.permission.WRITE_EXTERNAL_STORAGE:maxSdk=28;android.permission.INTERNET</AndroidPermissions>
```

```xml
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" android:maxSdkVersion="28"/>
```

The cap also bounds the runtime request (below), so the app never prompts for it above API 28.

#### Runtime ("dangerous") permissions are requested for you

A manifest entry is enough for *normal* permissions, but Android 6+ (API 23+) also requires a runtime
prompt for dangerous ones (camera, microphone, location, contacts, Bluetooth on API 31+, …). The
builder recognises those and injects the request into `SokolNativeActivity.java`, so declaring the
permission is all you need — there is no app code to write.

Each request is bounded by the permission's own API window:

- **`:maxSdk=NN`** → requested only when `SDK_INT <= NN`.
- **Permissions introduced after `minSdk`** (e.g. the API-31 Bluetooth permissions) → requested only
  from the level that introduced them.

> ⚠️ **Why the bounds matter.** Requesting a permission outside its window is not harmlessly ignored:
> Android opens and immediately closes the permission dialog **showing the user nothing**, and the
> app sees a plain denial. Any UI that waits for the user's answer then waits for an answer that can
> never arrive.

#### Asking later instead of at launch (`AndroidPermissionsNoAutoRequest`)

A permission the app requests itself, at a moment that makes sense to the user, is listed here too.
It stays in the manifest but is left out of the launch-time prompt:

```xml
<AndroidPermissions>android.permission.BLUETOOTH_SCAN;android.permission.POST_NOTIFICATIONS</AndroidPermissions>
<AndroidPermissionsNoAutoRequest>android.permission.POST_NOTIFICATIONS</AndroidPermissionsNoAutoRequest>
```

Typical use: notifications (`plugins/Notifications` asks for `POST_NOTIFICATIONS` when the app calls
`Notifications.RequestPermission()`).

### Bluetooth / BLE permissions

Android 12 (API 31) replaced the Bluetooth permission model outright, so an app supporting anything
older needs **two** sets. Declare the modern three and the builder adds the legacy ones for you
whenever `AndroidMinSdkVersion` is below 31:

```xml
<AndroidPermissions>android.permission.BLUETOOTH_SCAN;android.permission.BLUETOOTH_CONNECT;android.permission.BLUETOOTH_ADVERTISE</AndroidPermissions>
```

```xml
<uses-permission android:name="android.permission.BLUETOOTH_SCAN" android:usesPermissionFlags="neverForLocation"/>
<uses-permission android:name="android.permission.BLUETOOTH_CONNECT"/>
<uses-permission android:name="android.permission.BLUETOOTH_ADVERTISE"/>
<uses-permission android:name="android.permission.BLUETOOTH" android:maxSdkVersion="30"/>
<uses-permission android:name="android.permission.BLUETOOTH_ADMIN" android:maxSdkVersion="30"/>
<uses-permission android:name="android.permission.ACCESS_FINE_LOCATION" android:maxSdkVersion="30"/>
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" android:maxSdkVersion="30"/>
```

| API level | What Bluetooth needs | Requested at runtime |
|-----------|----------------------|----------------------|
| 23–30 | `BLUETOOTH` + `BLUETOOTH_ADMIN` (install-time) and the location pair | `ACCESS_FINE_LOCATION` + `ACCESS_COARSE_LOCATION` |
| 31+ | `BLUETOOTH_SCAN` / `BLUETOOTH_CONNECT` / `BLUETOOTH_ADVERTISE` | all three |

Notes:

- **Location is not optional on API 23–30.** Without it a BLE scan starts successfully and simply
  never reports a result — indistinguishable from "nothing is nearby". `ACCESS_FINE_LOCATION` is the
  one that carries it (either satisfies the scan check on API 23–28, and from API 29 only FINE does);
  `ACCESS_COARSE_LOCATION` is declared alongside it because that is what Android's BLE guidance
  prescribes. Both are added only alongside `BLUETOOTH_SCAN` — an app that merely connects is never
  asked for location.
- **`neverForLocation`** is added to `BLUETOOTH_SCAN` so API 31+ can scan *without* location access.
  It is an assertion that you never derive the user's physical location from scan results — if your
  app does, remove it and request location on API 31+ as well.
- **You can declare the legacy entries yourself** (with `:maxSdk=30`) if you prefer the file to state
  the full set explicitly; anything already declared is left exactly as written.
- ⚠️ **Below API 31 the system Location *setting* also gates scanning**, separately from the
  permission. With Location switched off, a scan returns nothing even though Bluetooth is on and the
  permission is granted — so an app should check `LocationManager.isLocationEnabled()` and tell the
  user, rather than appearing to work and finding no devices.

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

**⚠️ Android 16 (API 36) large screens**: apps targeting API 36 have `screenOrientation` ignored on
displays whose smallest width is ≥ 600dp (tablets, unfolded foldables, desktop windowing) — the app is
freely rotatable and resizable there. Phones are unaffected. A locked-orientation app must therefore
still lay out correctly in the other orientation on a tablet.

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
      <AndroidTargetSdkVersion>36</AndroidTargetSdkVersion>
      
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
  <AndroidTargetSdkVersion>36</AndroidTargetSdkVersion>
  
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
  <AndroidTargetSdkVersion>36</AndroidTargetSdkVersion>
  
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
   - AndroidTargetSdkVersion: 36
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

## Native Library Configuration (`AndroidNativeLibrary_*`)

Use these properties to bundle third-party prebuilt shared libraries (`.so` files) alongside your application. This is needed when your native code depends on a library that is distributed as a prebuilt binary (e.g., a vendor SDK, `c++_shared`, a physics engine, etc.).

### `AndroidNativeLibrary_[Name]Path`

**Required.** Path (relative to your project's `Directory.Build.props` file, or absolute) to the directory that contains ABI subdirectories with the prebuilt `.so` files.

Expected directory layout:
```
<path>/
  arm64-v8a/
    lib<name>.so          ← or release/lib<name>.so / debug/lib<name>.so
  armeabi-v7a/
    lib<name>.so
  x86_64/
    lib<name>.so
```

The build system searches each ABI folder and, optionally, `release/` and `debug/` subdirectories within it.

### `AndroidNativeLibrary_[Name]LibraryName`

**Optional.** Override the actual `.so` filename when the library name cannot be used directly as a C identifier (e.g. `c++_shared` contains `+` characters). Specify the name **without** the `lib` prefix and `.so` suffix.

If omitted, the `[Name]` part of the property key is lower-cased and used as the library name.

### What the build system does automatically

When one or more `AndroidNativeLibrary_*Path` properties are present:

1. **Copies** the `.so` files to `app/src/main/jniLibs/<ABI>/` — Gradle automatically packages anything in this directory into the APK.
2. **Updates `CMakeLists.txt`** — adds `link_directories` pointing to the `jniLibs/<ABI>/` folder and appends the library names to `target_link_libraries(sokol ...)`.
3. **Switches the STL** in `build.gradle` from `c++_static` to `c++_shared` (required when linking a shared STL).
4. **Injects `System.loadLibrary()`** calls into `SokolNativeActivity.java` so the libraries are loaded in order before `sokol`.

### Example

```xml
<PropertyGroup>
  <!-- Simple case: library name == key name (lower-cased) -->
  <!-- Looks for  ../../ext/camerac/libs/android/arm64-v8a/libcamerac.so  etc. -->
  <AndroidNativeLibrary_cameracPath>../../ext/camerac/libs/android</AndroidNativeLibrary_cameracPath>

  <!-- Name-override case: key cannot contain '+', so use LibraryName override -->
  <!-- Looks for  ext/mediapipe/android/arm64-v8a/libc++_shared.so  etc. -->
  <AndroidNativeLibrary_cppsharedPath>ext/mediapipe/android</AndroidNativeLibrary_cppsharedPath>
  <AndroidNativeLibrary_cppsharedLibraryName>c++_shared</AndroidNativeLibrary_cppsharedLibraryName>
</PropertyGroup>
```

### Build output

```
📦 Copying native libraries to jniLibs directory...
   ✅ Copied libcamerac.so → app/src/main/jniLibs/arm64-v8a/libcamerac.so
   ✅ Copied libc++_shared.so → app/src/main/jniLibs/arm64-v8a/libc++_shared.so
🔧 Updated CMakeLists.txt for native libraries: camerac, c++_shared
```

---

## See Also

- [Android Permissions Documentation](https://developer.android.com/reference/android/Manifest.permission)
- [Android Features Documentation](https://developer.android.com/guide/topics/manifest/uses-feature-element)
- [Android SDK Versions](https://developer.android.com/tools/releases/platforms)
