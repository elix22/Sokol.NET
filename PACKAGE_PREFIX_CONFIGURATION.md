# Package Prefix Configuration

This document explains how to configure custom package prefixes for Android and iOS applications.

## Overview

Previously, the package name/bundle identifier was hardcoded as `com.elix22`. Now you can customize this for each project by setting properties in your project's `Directory.Build.props` file.

## Configuration

### Android Package Prefix

The `AndroidPackagePrefix` property controls the package name prefix for Android applications.

**Default value:** `com.elix22`

**Example:**
```xml
<PropertyGroup>
   <!-- Package Prefix (e.g., com.yourcompany will create com.yourcompany.appname) -->
   <AndroidPackagePrefix>com.yourcompany</AndroidPackagePrefix>
</PropertyGroup>
```

This will generate Android package names like:
- `com.yourcompany.cube` (for the cube example)
- `com.yourcompany.dyntex` (for the dyntex example)

### iOS Bundle Prefix

The `IOSBundlePrefix` property controls the bundle identifier prefix for iOS applications.

**Default value:** `com.elix22`

**Example:**
```xml
<PropertyGroup>
   <!-- Bundle Identifier Prefix (e.g., com.yourcompany will create com.yourcompany.appname-ios-app) -->
   <IOSBundlePrefix>com.yourcompany</IOSBundlePrefix>
</PropertyGroup>
```

This will generate iOS bundle identifiers like:
- `com.yourcompany.cube-ios-app` (for the cube example)
- `com.yourcompany.dyntex-ios-app` (for the dyntex example)

## Complete Example

Here's a complete example of a `Directory.Build.props` file with custom package prefixes:

```xml
<Project>
   <!-- ... other properties ... -->

   <!-- Android Configuration -->
   <PropertyGroup>
      <!-- Package Prefix (e.g., com.yourcompany will create com.yourcompany.appname) -->
      <AndroidPackagePrefix>com.mycompany</AndroidPackagePrefix>
      
      <!-- SDK Versions -->
      <AndroidMinSdkVersion>26</AndroidMinSdkVersion>
      <AndroidTargetSdkVersion>34</AndroidTargetSdkVersion>
      
      <!-- ... other Android properties ... -->
   </PropertyGroup>

   <!-- iOS Configuration -->
   <PropertyGroup>
      <!-- Bundle Identifier Prefix (e.g., com.yourcompany will create com.yourcompany.appname-ios-app) -->
      <IOSBundlePrefix>com.mycompany</IOSBundlePrefix>
      
      <!-- Minimum iOS version -->
      <IOSMinVersion>14.0</IOSMinVersion>
      
      <!-- ... other iOS properties ... -->
   </PropertyGroup>
</Project>
```

## Where These Are Used

### Android
- **build.gradle**: Sets the `applicationId` and `namespace`
- **Package installation**: Used when uninstalling/installing APK or AAB files
- **App launching**: Used when launching the app after installation

### iOS
- **CMakeLists.txt**: Sets the `MACOSX_BUNDLE_GUI_IDENTIFIER`
- **Info.plist**: Used for the bundle identifier
- **App uninstallation**: Used when uninstalling existing app before installation

## Notes

1. **Package Name Format**:
   - Android: `{AndroidPackagePrefix}.{projectName}`
   - iOS: `{IOSBundlePrefix}.{projectName}-ios-app`

2. **Backward Compatibility**: If you don't specify these properties, the default `com.elix22` prefix will be used, maintaining backward compatibility with existing projects.

3. **Valid Package Names**:
   - Should follow reverse domain name notation (e.g., `com.company`)
   - Android: Can contain lowercase letters, numbers, and underscores
   - iOS: Can contain alphanumeric characters, hyphens, and periods

4. **Per-Project Configuration**: Each project can have its own `Directory.Build.props` file with different package prefixes.

## Migration Guide

If you have existing projects and want to customize the package prefix:

1. Open your project's `Directory.Build.props` file
2. Add the `AndroidPackagePrefix` and/or `IOSBundlePrefix` properties to the appropriate PropertyGroup sections
3. Rebuild your Android/iOS applications
4. **Important**: If you had previously installed apps with the old package name, you'll need to uninstall them manually first, as the new package name will be different

Example uninstall commands:
```bash
# Android
adb uninstall com.elix22.yourapp

# iOS (using ios-deploy)
ios-deploy --uninstall_only --bundle_id com.elix22.yourapp-ios-app
```

## Troubleshooting

### "App not installed" error on Android
This can happen if there's a package name mismatch. Uninstall the old app first:
```bash
adb uninstall com.elix22.yourapp
```

### iOS installation fails with code signing error
Make sure your bundle identifier matches what's configured in your Apple Developer account and provisioning profiles. If you change the bundle prefix, you may need to update your provisioning profiles.
