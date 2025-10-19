# OzzUtil Build Scripts

This directory contains build scripts for the `ozzutil` library for all supported platforms.

## Prerequisites

Before building `ozzutil`, you must first build the `ozz-animation` libraries for your target platform(s):

```bash
# Build ozz-animation for macOS (required before building ozzutil)
./build-ozz-animation-macos.sh arm64 Release
./build-ozz-animation-macos.sh x86_64 Release

# Build ozz-animation for other platforms
./build-ozz-animation-web.sh Release
./build-ozz-animation-ios.sh arm64 Release
./build-ozz-animation-android.sh arm64-v8a Release
./build-ozz-animation-linux.sh x86_64 Release
```

## Platform-Specific Build Scripts

### macOS
```bash
# Build for specific architecture
./build-ozzutil-macos.sh arm64 Release
./build-ozzutil-macos.sh x86_64 Debug

# Output: ext/ozzutil/libs/macos/{arch}/{build_type}/libozzutil.dylib
```

### Windows
```powershell
# Build for Windows x64
.\build-ozzutil-windows.ps1 Release
.\build-ozzutil-windows.ps1 Debug

# Output: ext/ozzutil/libs/windows/x64/{build_type}/ozzutil.dll
```

### Linux
```bash
# Build for specific architecture
./build-ozzutil-linux.sh x86_64 Release
./build-ozzutil-linux.sh arm64 Debug

# Output: ext/ozzutil/libs/linux/{arch}/{build_type}/libozzutil.so
```

### Web/Emscripten
```bash
# Build for Web/WebAssembly
./build-ozzutil-web.sh Release
./build-ozzutil-web.sh Debug

# Output: ext/ozzutil/libs/emscripten/x86/{build_type}/ozzutil.a
```

### iOS
```bash
# Build iOS framework for specific architecture
./build-ozzutil-ios.sh arm64 Release      # Device
./build-ozzutil-ios.sh x86_64 Debug       # Simulator

# Build all iOS architectures
./build-ozzutil-ios-all.sh Release

# Output: ext/ozzutil/libs/ios/{build_type}/ozzutil.framework
```

### Android
```bash
# Build for specific Android ABI
./build-ozzutil-android.sh arm64-v8a Release
./build-ozzutil-android.sh armeabi-v7a Release
./build-ozzutil-android.sh x86_64 Release
./build-ozzutil-android.sh x86 Release

# Build all Android ABIs
./build-ozzutil-android-all.sh Release

# Output: ext/ozzutil/libs/android/{abi}/{build_type}/libozzutil.so
```

## Convenience Scripts

### Build All Platforms
```bash
# Build for all supported platforms on current system
./build-ozzutil-all.sh Release

# This will build:
# - macOS: arm64 + x86_64 (if on macOS)
# - iOS: arm64 + x86_64 (if on macOS) 
# - Web/Emscripten (if on macOS/Linux)
# - Android: all ABIs (if on macOS/Linux)
# - Windows: x64 (if on Windows)
# - Linux: x86_64 + arm64 (if on Linux)
```

### Build Multiple Architectures
```bash
# iOS - Build all architectures
./build-ozzutil-ios-all.sh Release

# Android - Build all ABIs
./build-ozzutil-android-all.sh Release
```

## Library Types by Platform

- **macOS**: Dynamic library (`.dylib`) - Code signed automatically
- **Windows**: Dynamic library (`.dll`)
- **Linux**: Dynamic library (`.so`)
- **Web/Emscripten**: Static library (`.a`)
- **iOS**: Framework (`.framework`) containing static library
- **Android**: Dynamic library (`.so`)

## Build Requirements

### General
- CMake 3.16 or later
- C++17 compatible compiler
- Pre-built ozz-animation libraries for target platform

### Platform-Specific
- **macOS/iOS**: Xcode with command line tools
- **Windows**: Visual Studio 2022 or later
- **Linux**: GCC or Clang with C++17 support
- **Web**: Emscripten SDK (managed automatically by local emsdk)
- **Android**: Android NDK (set ANDROID_NDK_HOME or ANDROID_HOME)

## Dependencies

The ozzutil library depends on:
- **sokol**: Graphics library (automatically linked)
- **ozz-animation**: 3D skeletal animation library
  - libozz_base.a
  - libozz_geometry.a  
  - libozz_animation.a
  - libozz_animation_offline.a
  - libozz_options.a
- **ozz-animation framework**: mesh.cc from samples/framework

## Troubleshooting

### Common Issues

1. **Missing ozz-animation libraries**
   ```
   Error: ozz-animation libraries not found
   ```
   Solution: Build ozz-animation first for your target platform.

2. **Missing Android NDK**
   ```
   Error: Android NDK not found
   ```
   Solution: Set `ANDROID_NDK_HOME` environment variable or install Android NDK via Android Studio.

3. **Missing Emscripten**
   ```
   Error: Local emsdk not found
   ```
   Solution: Initialize the emsdk submodule: `git submodule update --init --recursive`

### Build Verification

After building, verify the library was created:

```bash
# macOS
ls -lh ext/ozzutil/libs/macos/arm64/release/libozzutil.dylib

# Windows  
dir ext\ozzutil\libs\windows\x64\release\ozzutil.dll

# Linux
ls -lh ext/ozzutil/libs/linux/x86_64/release/libozzutil.so

# Web
ls -lh ext/ozzutil/libs/emscripten/x86/release/ozzutil.a

# iOS
ls -lah ext/ozzutil/libs/ios/release/ozzutil.framework/

# Android
ls -lh ext/ozzutil/libs/android/arm64-v8a/release/libozzutil.so
```

## Integration

The built ozzutil libraries are used by the sokol-charp C# binding system and provide 3D skeletal animation functionality powered by ozz-animation.