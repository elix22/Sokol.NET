# Output Path Implementation Summary

## Overview
Implemented automatic output copying for all platforms (Android, iOS, Desktop, Web). Build artifacts are **always** copied to an organized output folder structure.

### Key Behavior
- **Default**: Copies build artifacts to `{ProjectPath}/output/` (e.g., `examples/cube/output/Android/release/cube-release.apk`)
- **Custom**: Use `--output /path` to specify a different output location
- **Automatic**: No need to specify `--output` - builds always get copied to project's output folder
- **Organized**: Consistent folder structure across all platforms

## Implementation Details

### 1. Android (✅ Complete)
**File**: `tools/SokolApplicationBuilder/Source/AndroidAppBuilder.cs`

**Changes**:
- Added `CopyToOutputPath()` method (lines 2042-2077)
- Integrated into build flow after signing, before installation (lines 396-410)

**Output Structure**:
```
{OutputPath}/
└── Android/
    ├── release/
    │   ├── {appName}-release.apk
    │   └── {appName}-release.aab
    └── debug/
        ├── {appName}-debug.apk
        └── {appName}-debug.aab
```

**Usage**:
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture android \
  --type release \
  --output /path/to/output \
  --path examples/cube
```

### 2. iOS (✅ Complete)
**File**: `tools/SokolApplicationBuilder/Source/IOSAppBuilder.cs`

**Changes**:
- Added `CopyToOutputPath()` method (lines 1409-1459)
- Added `CopyDirectory()` helper method (lines 1461-1478)
- Integrated into build flow after compilation, before installation (lines 161-169)

**Output Structure**:
```
{OutputPath}/
└── iOS/
    └── release/
        └── {appName}-release.app/
            ├── Info.plist
            ├── {appName} (executable)
            └── (other app bundle contents)
```

**Usage**:
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture ios \
  --type release \
  --output /path/to/output \
  --path examples/cube
```

### 3. Desktop (✅ Complete)
**File**: `tools/SokolApplicationBuilder/Source/DesktopAppBuilder.cs`

**Changes**:
- Modified output path logic to always build to project bin folder (lines 52-57)
- Added `CopyToOutputPath()` method (lines 700-806)
- Added `CopyDirectory()` helper method (lines 808-824)
- Integrated into build flow after app bundle creation (lines 111-119)

**Output Structure**:
```
{OutputPath}/
└── Desktop/
    ├── macOS/
    │   ├── release/
    │   │   └── {appName}.app/
    │   └── debug/
    │       └── {appName}.app/
    ├── Windows/
    │   ├── release/
    │   │   ├── {appName}.exe
    │   │   ├── *.dll
    │   │   └── Assets/
    │   └── debug/
    │       └── ...
    └── Linux/
        ├── release/
        │   ├── {appName}
        │   ├── *.so
        │   └── Assets/
        └── debug/
            └── ...
```

**Usage**:
```bash
# macOS
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture desktop \
  --rid osx-arm64 \
  --type release \
  --output /path/to/output \
  --path examples/cube

# Windows
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture desktop \
  --rid win-x64 \
  --type release \
  --output /path/to/output \
  --path examples/cube

# Linux
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture desktop \
  --rid linux-x64 \
  --type release \
  --output /path/to/output \
  --path examples/cube
```

### 4. Web (✅ Already Implemented)
**File**: `tools/SokolApplicationBuilder/Source/WebAppBuilder.cs`

**Existing Implementation** (lines 85-88):
```csharp
if (opts.OutputPath != "")
{
    opts.OutputPath = Path.Combine(opts.OutputPath, "Web", buildType);
}
```

**Output Structure**:
```
{OutputPath}/
└── Web/
    ├── Release/
    │   ├── index.html
    │   ├── *.wasm
    │   └── (other web files)
    └── Debug/
        └── ...
```

**Usage**:
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build \
  --architecture web \
  --type release \
  --output /path/to/output \
  --path examples/cube
```

## Design Pattern

### Build Flow
1. **Build** to default location (project bin folder)
2. **Sign** (if applicable - Android release, iOS)
3. **Copy** to output folder (always happens - see below)
4. **Install** (if `--install` flag specified)

### Output Organization
All platforms **always copy** build artifacts to an organized output folder:

**Default behavior** (no `--output` specified):
```
{ProjectPath}/output/
├── Android/{buildType}/{appName}-{buildType}.{apk|aab}
├── iOS/{buildType}/{appName}-{buildType}.app/
├── Desktop/{Platform}/{buildType}/{appName}{.exe}/
└── Web/{buildType}/
```

**Custom output path** (with `--output /path/to/output`):
```
/path/to/output/
├── Android/{buildType}/{appName}-{buildType}.{apk|aab}
├── iOS/{buildType}/{appName}-{buildType}.app/
├── Desktop/{Platform}/{buildType}/{appName}{.exe}/
└── Web/{buildType}/
```

### Benefits
- **Always Available**: Build artifacts are always copied to a clean, organized location
- **Project Output Folder**: By default, everything goes to `{ProjectPath}/output/` for easy access
- **Organized**: All platform builds in one location with clear hierarchy
- **Descriptive**: File names include build type for easy identification
- **CI/CD Friendly**: Easy to collect artifacts from known location (use `--output` for custom paths)
- **Non-Intrusive**: Original build outputs remain in project directories
- **Flexible**: Use default project output folder or specify custom path

## VS Code Tasks Integration

All platforms support the `--output` option in VS Code tasks:

```json
{
    "label": "Android: Build APK with Output",
    "type": "shell",
    "command": "dotnet",
    "args": [
        "run",
        "--project",
        "${workspaceFolder}/tools/SokolApplicationBuilder",
        "--",
        "--task", "build",
        "--architecture", "android",
        "--type", "${input:androidBuildType}",
        "--output", "${workspaceFolder}/builds",
        "--path", "${workspaceFolder}/${input:examplePath}"
    ]
}
```

## Testing

### Android
```bash
# Build APK to default project output folder
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture android --type release \
  --path examples/cube

# Expected: examples/cube/output/Android/release/cube-release.apk

# Build APK with custom output path
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture android --type release \
  --output ./test-output --path examples/cube

# Expected: ./test-output/Android/release/cube-release.apk
```

### iOS
```bash
# Build iOS app to default project output folder
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture ios --type release \
  --path examples/cube

# Expected: examples/cube/output/iOS/release/cube-release.app/

# Build iOS app with custom output path
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture ios --type release \
  --output ./test-output --path examples/cube

# Expected: ./test-output/iOS/release/cube-release.app/
```

### Desktop
```bash
# Build macOS app to default project output folder
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture desktop --rid osx-arm64 --type release \
  --path examples/cube

# Expected: examples/cube/output/Desktop/macOS/release/cube.app/

# Build macOS app with custom output path
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture desktop --rid osx-arm64 --type release \
  --output ./test-output --path examples/cube

# Expected: ./test-output/Desktop/macOS/release/cube.app/
```

### Web
```bash
# Build web app to default project output folder
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture web --type release \
  --path examples/cube

# Expected: examples/cube/output/Web/Release/

# Build web app with custom output path
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture web --type release \
  --output ./test-output --path examples/cube

# Expected: ./test-output/Web/Release/
```

## Summary

✅ **Android**: Copies APK/AAB files to organized output directory
✅ **iOS**: Copies .app bundle to organized output directory
✅ **Desktop**: Copies .app bundle (macOS) or executable + dependencies (Windows/Linux)
✅ **Web**: Already builds directly to organized output directory

All platforms now support the `--output` option with consistent behavior and organized output structure.
