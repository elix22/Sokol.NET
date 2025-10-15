# Application Version Configuration Guide

## Overview

You can now define a unified application version for your project that will be automatically applied across all platforms (Desktop, Web, Android, iOS) through the `AppVersion` property in `Directory.Build.props`.

## Quick Start

Add the following property to your project's `Directory.Build.props`:

```xml
<PropertyGroup>
   <!-- Application Version (used across all platforms) -->
   <AppVersion>1.0</AppVersion>
</PropertyGroup>
```

## Supported Platforms

The `AppVersion` property is automatically used by:

- **Android**: Sets `android:versionName` and `android:versionCode` in AndroidManifest.xml
- **iOS**: Sets `CFBundleShortVersionString` and `CFBundleVersion` in Info.plist
- **macOS Desktop**: Sets `CFBundleShortVersionString` and `CFBundleVersion` in app bundle Info.plist
- **Web**: Sets version metadata in the web application

## Version Format

The version should follow standard semantic versioning format:

```
<major>.<minor>.<patch>
```

Examples:
- `1.0` - Simple version (major.minor)
- `1.2.3` - Full semantic version (major.minor.patch)
- `2.0.0` - Major release version

**Note**: For Android, the `versionCode` is automatically derived from the major version number (e.g., "1.2.3" → versionCode = 1).

## Complete Example

Here's a complete `Directory.Build.props` with version configuration:

```xml
<Project>
   <PropertyGroup>
      <BaseIntermediateOutputPath>obj\$(MSBuildProjectName)</BaseIntermediateOutputPath>
      
      <!-- Application Version (used across all platforms) -->
      <AppVersion>1.2.3</AppVersion>
      
      <!-- Other configurations... -->
   </PropertyGroup>

   <!-- Android Configuration -->
   <PropertyGroup>
      <AndroidPackagePrefix>com.mycompany</AndroidPackagePrefix>
      <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
      <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
   </PropertyGroup>

   <!-- iOS Configuration -->
   <PropertyGroup>
      <IOSBundlePrefix>com.mycompany</IOSBundlePrefix>
      <IOSMinVersion>14.0</IOSMinVersion>
   </PropertyGroup>
</Project>
```

## How It Works by Platform

### Android

When building for Android, `SokolApplicationBuilder` reads the `AppVersion` property and:

1. Sets `android:versionName` to the full version string (e.g., "1.2.3")
2. Sets `android:versionCode` to the major version number (e.g., 1)

Example generated manifest:
```xml
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
    android:versionCode="1"
    android:versionName="1.2.3">
```

### iOS

When building for iOS, `SokolApplicationBuilder` reads the `AppVersion` property and:

1. Sets `MACOSX_BUNDLE_SHORT_VERSION_STRING` to the full version (e.g., "1.2.3")
2. Sets `MACOSX_BUNDLE_BUNDLE_VERSION` to the full version (e.g., "1.2.3")

These values are injected into the CMakeLists.txt template and appear in the final Info.plist.

### macOS Desktop

When building macOS desktop app bundles, `SokolApplicationBuilder`:

1. Sets `CFBundleShortVersionString` to the full version (e.g., "1.2.3")
2. Sets `CFBundleVersion` to the major version number (e.g., 1)

This appears in the `.app` bundle's Info.plist file.

### Web

When building for web, `SokolApplicationBuilder` uses the version for metadata and manifest files.

## Default Values

If `AppVersion` is not specified in `Directory.Build.props`, the default value of `"1.0"` is used automatically.

This ensures backward compatibility with existing projects.

## Building Your Application

After setting the version, build your application normally:

```bash
# Android
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture android --path examples/myapp

# iOS
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture ios --path examples/myapp

# macOS Desktop
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture desktop --rid osx-arm64 --path examples/myapp

# Web
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture web --path examples/myapp
```

The version will be automatically applied to all platforms.

## Updating Your Version

To update your application version:

1. Edit `Directory.Build.props`
2. Change the `<AppVersion>` value
3. Rebuild your application

```xml
<!-- Before -->
<AppVersion>1.0</AppVersion>

<!-- After -->
<AppVersion>1.1</AppVersion>
```

## Best Practices

### Semantic Versioning

Follow semantic versioning conventions:

- **Major version** (1.x.x): Incompatible API changes or major features
- **Minor version** (x.1.x): New functionality, backward compatible
- **Patch version** (x.x.1): Bug fixes, backward compatible

### Version Incrementation

- Increment **patch** for bug fixes: 1.0.0 → 1.0.1
- Increment **minor** for new features: 1.0.1 → 1.1.0
- Increment **major** for breaking changes: 1.1.0 → 2.0.0

### Platform-Specific Considerations

#### Android Version Codes

Android requires integer version codes for app updates. The build system automatically extracts the major version number:

- Version "1.2.3" → versionCode 1
- Version "2.0.0" → versionCode 2

**Important**: Increment the major version when publishing updates to Android stores to ensure proper update detection.

#### iOS Build Numbers

iOS uses `CFBundleVersion` for App Store submission. The build system uses the full version string for both `CFBundleShortVersionString` and `CFBundleVersion`.

For App Store submissions, you may need to increment versions for each build.

## Verification

To verify your version is correctly applied:

### Android
Check the generated `Android/native-activity/app/src/main/AndroidManifest.xml`:
```xml
<manifest ... android:versionName="1.2.3" android:versionCode="1">
```

### iOS
Check the build output for:
```
📋 Read N iOS properties from Directory.Build.props
   - AppVersion: 1.2.3
```

Or check the generated Xcode project's Info.plist after building.

### macOS Desktop
After building, check the app bundle's Info.plist:
```bash
plutil -p examples/myapp/output/myapp.app/Contents/Info.plist | grep Version
```

## Troubleshooting

### Version Not Applied

**Problem**: The version doesn't appear in the built application.

**Solution**:
1. Verify `<AppVersion>` is in a `<PropertyGroup>` element
2. Ensure the property is not commented out
3. Rebuild `SokolApplicationBuilder` if you recently updated it
4. Clean and rebuild your application

### Android Version Code Issues

**Problem**: App won't update on Android devices.

**Solution**: Increment the major version number (e.g., from 1.x.x to 2.0.0) to generate a new versionCode.

### iOS Version Conflicts

**Problem**: App Store Connect rejects build due to version conflict.

**Solution**: Increment your `AppVersion` for each new build submitted to App Store.

## Migration from Hardcoded Versions

If you have existing applications with hardcoded versions, follow these steps:

1. **Identify Current Versions**: Check your current version numbers across platforms
2. **Add AppVersion Property**: Add the unified version to `Directory.Build.props`
3. **Rebuild**: Rebuild your application using `SokolApplicationBuilder`
4. **Verify**: Check that the version appears correctly on all platforms
5. **Remove Old Versions**: If you had any custom version configurations, they can be removed

## Related Configuration

The `AppVersion` property works alongside other configuration options:

- **AndroidPackagePrefix**: Android package name prefix (e.g., "com.mycompany")
- **IOSBundlePrefix**: iOS bundle identifier prefix (e.g., "com.mycompany")

See [PACKAGE_PREFIX_CONFIGURATION.md](PACKAGE_PREFIX_CONFIGURATION.md) for details.

## Summary

| Property | Description | Default | Example |
|----------|-------------|---------|---------|
| `AppVersion` | Application version used across all platforms | `1.0` | `1.2.3` |

The version is automatically applied to:
- Android: `versionName` and `versionCode`
- iOS: `CFBundleShortVersionString` and `CFBundleVersion`
- macOS: `CFBundleShortVersionString` and `CFBundleVersion`
- Web: Version metadata

This provides a single source of truth for your application version across all deployment targets.
