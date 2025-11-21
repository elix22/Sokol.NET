# Quick Build Reference

## 🚀 One-Command Builds

### Current Platform (Local Development)

**macOS:**
```bash
./scripts/build-xcode-macos.sh
```

**Windows:**
```powershell
.\scripts\build-vs2022-windows.ps1
```

**Linux:**
```bash
./scripts/build-linux-library.sh
```

**Web:**
```bash
./scripts/build-web-library.sh
```

## 📦 Output Locations

All builds output to `libs/{platform}/{arch}/{debug|release}/`

Examples:
- `libs/macos/arm64/release/libsokol.dylib`
- `libs/windows/x64/debug/sokol.dll`
- `libs/linux/x86_64/release/libsokol.so`
- `libs/emscripten/x86/debug/sokol.a`

## ☁️ GitHub Actions

### Automatic Builds
- Triggered on push/PR to `main` or `develop`
- Builds all platforms: Windows (x64), macOS (arm64, x86_64), Linux (x86_64, aarch64), Web

### Manual Build
1. Go to: GitHub → Actions → "Build Sokol Libraries"
2. Click "Run workflow"
3. Select branch
4. Click "Run workflow" button

### Download Built Libraries
1. Go to completed workflow run
2. Scroll to "Artifacts" section
3. Download `sokol-libraries-all-platforms`

## 🛠️ First-Time Setup

### macOS
```bash
xcode-select --install  # Install Xcode Command Line Tools
brew install cmake      # Install CMake (optional, if not already installed)
git submodule update --init --recursive  # Initialize submodules
```

### Windows
- Install Visual Studio 2022 with C++ Desktop Development
- Install CMake (https://cmake.org/download/)
- Run from Developer Command Prompt or PowerShell:
```powershell
git submodule update --init --recursive
```

### Linux
```bash
sudo apt-get update
sudo apt-get install build-essential cmake \
  libx11-dev libxcursor-dev libxi-dev libasound2-dev libgl1-mesa-dev
git submodule update --init --recursive
```

### Web
```bash
git submodule update --init --recursive
cd tools/emsdk
./emsdk install 3.1.34
./emsdk activate 3.1.34
source ./emsdk_env.sh  # On Windows: emsdk_env.bat
cd ../..
```

## 🔧 CMake Direct Commands

If you prefer using CMake directly:

### Debug Build
```bash
mkdir build && cd build
cmake ../ext -DCMAKE_BUILD_TYPE=Debug
cmake --build . --config Debug
```

### Release Build
```bash
mkdir build && cd build
cmake ../ext -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release
```

### Specific Architecture (macOS)
```bash
cmake ../ext -DCMAKE_OSX_ARCHITECTURES=arm64  # or x86_64
```

### Linux ARM64 (Native Build Only)
```bash
# ARM64 builds should be done natively on ARM64 hardware
# CI/CD does not include ARM64 cross-compilation due to complexity
# On ARM64 Linux machine:
./scripts/build-linux-library.sh
```

## 🧹 Clean Build

Remove build artifacts:
```bash
# Clean all build directories
rm -rf build-* libs/

# Keep Android/iOS (they build on-demand)
# Android: libs/android/*
# iOS: libs/ios/*
```

## 📝 Notes

- **Android & iOS**: Built automatically during project compilation, not via these scripts
- **Debug vs Release**: Debug has symbols for debugging, Release is optimized
- **Multi-arch**: macOS supports both arm64 and x86_64, build both for universal compatibility
- **CI/CD Artifacts**: Retained for 30-90 days depending on type

## 🐛 Common Issues

**"CMake not found"**
→ Install CMake: macOS: `brew install cmake`, Linux: `apt-get install cmake`, Windows: Download from cmake.org

**"Git submodule errors"**
→ Run: `git submodule update --init --recursive`

**"Emscripten not found"**
→ Initialize emsdk: `cd tools/emsdk && ./emsdk install 3.1.34 && ./emsdk activate 3.1.34`

**"Missing X11 libraries" (Linux)**
→ Install dev packages: `sudo apt-get install libx11-dev libxcursor-dev libxi-dev libasound2-dev libgl1-mesa-dev`

## 📚 More Information

See [BUILD_SYSTEM.md](BUILD_SYSTEM.md) for detailed documentation.
