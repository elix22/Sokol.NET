# iOS Configuration Properties

This document describes the iOS-specific properties that can be configured in `Directory.Build.props` for iOS deployments.

## Overview

iOS properties allow you to configure deployment settings directly in your project's `Directory.Build.props` file, making it easier to maintain consistent configurations across builds without specifying command-line arguments every time.

## Property Group Structure

Organize iOS properties in a dedicated PropertyGroup section in your `Directory.Build.props`:

```xml
<Project>
   <!-- iOS Configuration -->
   <PropertyGroup>
      <IOSMinVersion>14.0</IOSMinVersion>
      <IOSScreenOrientation>landscape</IOSScreenOrientation>
      <IOSRequiresFullScreen>false</IOSRequiresFullScreen>
      <IOSStatusBarHidden>true</IOSStatusBarHidden>
      <IOSDevelopmentTeam>YOUR_TEAM_ID</IOSDevelopmentTeam>
   </PropertyGroup>
</Project>
```

## Available Properties

### IOSMinVersion
**Type:** String  
**Default:** `14.0`  
**Description:** Minimum iOS version required to run the application.

**Example:**
```xml
<IOSMinVersion>15.0</IOSMinVersion>
```

**Common values:**
- `14.0` - iOS 14 (default)
- `15.0` - iOS 15
- `16.0` - iOS 16
- `17.0` - iOS 17

---

### IOSScreenOrientation
**Type:** String  
**Default:** `both`  
**Description:** Controls the allowed screen orientations for the application.

**Example:**
```xml
<IOSScreenOrientation>landscape</IOSScreenOrientation>
```

**Valid values:**
- `portrait` - Portrait mode only (UIInterfaceOrientationPortrait, UIInterfaceOrientationPortraitUpsideDown)
- `landscape` - Landscape mode only (UIInterfaceOrientationLandscapeLeft, UIInterfaceOrientationLandscapeRight)
- `both` - All orientations allowed (default)

**Note:** Command-line `--orientation` parameter overrides this setting.

---

### IOSRequiresFullScreen
**Type:** Boolean  
**Default:** `false`  
**Description:** When set to `true`, the app requires full screen and hides the home indicator on devices with no home button.

**Example:**
```xml
<IOSRequiresFullScreen>true</IOSRequiresFullScreen>
```

**Info.plist mapping:** `UIRequiresFullScreen`

---

### IOSStatusBarHidden
**Type:** Boolean  
**Default:** `false`  
**Description:** Controls whether the status bar is hidden when the app launches.

**Example:**
```xml
<IOSStatusBarHidden>true</IOSStatusBarHidden>
```

**Info.plist mapping:** `UIStatusBarHidden`

---

### IOSDevelopmentTeam
**Type:** String  
**Default:** (empty)  
**Description:** Your Apple Developer Team ID (10 alphanumeric characters). This is required for code signing and device deployment.

**Example:**
```xml
<IOSDevelopmentTeam>AB12CD34EF</IOSDevelopmentTeam>
```

**How to find your Team ID:**
1. Visit [developer.apple.com/account](https://developer.apple.com/account)
2. Look for "Team ID" in your membership details
3. Or use: `security find-certificate -a -c "Apple Development" | grep "subj"`

**Priority:**
1. Command-line `--development-team` flag (highest priority)
2. `IOSDevelopmentTeam` from Directory.Build.props
3. Cached Team ID from previous builds
4. Interactive prompt (in `--interactive` mode)

**Note:** Once provided, the Team ID is cached locally in `~/.Sokol.NET-cache/{projectName}.teamid` for convenience.

---

## Complete Example

Here's a complete example with iOS configuration alongside Android configuration:

```xml
<Project>
   <PropertyGroup>
      <BaseIntermediateOutputPath>obj\$(MSBuildProjectName)</BaseIntermediateOutputPath>
      <!-- Other general properties... -->
   </PropertyGroup>

   <!-- Android Configuration -->
   <PropertyGroup>
      <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
      <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
      <AndroidFullscreen>true</AndroidFullscreen>
      <AndroidScreenOrientation>landscape</AndroidScreenOrientation>
      <AndroidKeepScreenOn>true</AndroidKeepScreenOn>
   </PropertyGroup>

   <!-- iOS Configuration -->
   <PropertyGroup>
      <IOSMinVersion>14.0</IOSMinVersion>
      <IOSScreenOrientation>landscape</IOSScreenOrientation>
      <IOSRequiresFullScreen>false</IOSRequiresFullScreen>
      <IOSStatusBarHidden>true</IOSStatusBarHidden>
      <!-- <IOSDevelopmentTeam>YOUR_TEAM_ID</IOSDevelopmentTeam> -->
   </PropertyGroup>
</Project>
```

## Build Commands

### Build and install on iOS device:
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture ios \
  --install \
  --interactive \
  --path examples/cube
```

### Override orientation at build time:
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture ios \
  --orientation portrait \
  --install \
  --path examples/cube
```

### Specify Development Team via command line:
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture ios \
  --development-team AB12CD34EF \
  --install \
  --path examples/cube
```

## Property Reading Behavior

- The build system reads **ALL** `<PropertyGroup>` elements in Directory.Build.props
- Properties can be split across multiple PropertyGroup sections
- Later definitions override earlier ones
- Properties are read and displayed during the build process

**Example output:**
```
📋 Read 5 iOS properties from Directory.Build.props
   - IOSMinVersion: 14.0
   - IOSScreenOrientation: landscape
   - IOSRequiresFullScreen: false
   - IOSStatusBarHidden: true
   - IOSDevelopmentTeam: AB12CD34EF
```

## Notes

1. **Development Team ID** is the most important property for iOS deployment. Without it, you cannot sign the app for device installation.

2. **Caching**: Team IDs are cached after first use. Delete `~/.Sokol.NET-cache/{projectName}.teamid` to reset.

3. **Command-line Override**: Most command-line flags take precedence over Directory.Build.props settings.

4. **Info.plist Generation**: These properties are used to generate the Info.plist file during the Xcode project setup.

5. **Orientation Priority**:
   - If `--orientation` is specified on command line, it's used
   - Otherwise, `IOSScreenOrientation` from Directory.Build.props is used
   - Default is `both` if neither is specified

## See Also

- [ANDROID_PROPERTIES.md](./ANDROID_PROPERTIES.md) - Android configuration properties
- [iOS Deployment Guide](https://developer.apple.com/documentation/xcode/running-your-app-in-simulator-or-on-a-device)
