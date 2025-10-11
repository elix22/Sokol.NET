# Sokol Library Build System

This directory contains scripts and G#### Linux
- Architecture: x86_64
- Runner: ubuntu-latest
- Compiler: GCC
- Dependencies: X11, XCursor, Xi, ALSA, Mesa GL
- Output: .so files
- Note: ARM64 builds should be done natively on ARM64 hardwareActions workflows for building the sokol native libraries across multiple platforms.

## Supported Platforms

- **Windows**: x64 (Debug & Release)
- **macOS**: arm64, x86_64 (Debug & Release)
- **Linux**: x86_64 (Debug & Release)
- **Web (Emscripten)**: x86 (Debug & Release)

> **Note**: Android and iOS libraries are compiled on-the-fly during project build and are not included in the CI/CD pipeline.

> **Note**: Linux ARM64 (aarch64) is not included in CI/CD due to cross-compilation complexity. ARM64 Linux builds should be done natively on ARM64 hardware if needed.

## Local Build Scripts

### macOS
```bash
./scripts/build-xcode-macos.sh
```
Builds both Debug and Release configurations for the current architecture (arm64 or x86_64).
Output: `libs/macos/{arch}/debug/` and `libs/macos/{arch}/release/`

### Windows
```powershell
.\scripts\build-vs2022-windows.ps1
```
Builds both Debug and Release configurations using Visual Studio 2022.
Output: `libs/windows/{arch}/debug/` and `libs/windows/{arch}/release/`

### Linux
```bash
./scripts/build-linux-library.sh
```
Builds both Debug and Release configurations using GCC/Clang.
Output: `libs/linux/{arch}/debug/` and `libs/linux/{arch}/release/`

### Web (Emscripten)
```bash
./scripts/build-web-library.sh
```
Builds both Debug and Release static libraries using Emscripten 3.1.34.
Output: `libs/emscripten/x86/debug/` and `libs/emscripten/x86/release/`

## GitHub Actions CI/CD

The project includes automated builds via GitHub Actions defined in `.github/workflows/build-libraries.yml`.

### Triggers
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual workflow dispatch

### Build Matrix

#### Windows
- Architecture: x64
- Runner: windows-latest
- Compiler: Visual Studio 2022
- Output: DLL + LIB files

#### macOS
- Architectures: arm64, x86_64
- Runner: macos-latest
- Output: dylib files

#### Linux
- Architectures: x86_64, aarch64
- Runner: ubuntu-latest
- Compiler: GCC (native) / GCC cross-compiler (aarch64)
- Dependencies: X11, XCursor, Xi, ALSA, Mesa GL
- Output: .so files

#### Web (Emscripten)
- Version: 3.1.34
- Runner: ubuntu-latest
- Output: Static library (.a) files

### Artifacts

The workflow produces the following artifacts:

1. **Per-platform artifacts** (30-day retention):
   - `windows-x64`
   - `macos-arm64`
   - `macos-x86_64`
   - `linux-x86_64`
   - `emscripten-x86`

2. **Combined artifact** (90-day retention):
   - `sokol-libraries-all-platforms` - All libraries in proper directory structure

3. **Release archives** (90-day retention, only on main branch):
   - `sokol-libraries.tar.gz`
   - `sokol-libraries.zip`

### Downloading Built Libraries

After a successful workflow run:

1. Go to the Actions tab in GitHub
2. Click on the completed workflow run
3. Scroll down to "Artifacts" section
4. Download `sokol-libraries-all-platforms` for all libraries
5. Or download individual platform artifacts as needed

The downloaded artifacts maintain the same directory structure as local builds:
```
libs/
├── windows/
│   └── x64/
│       ├── debug/
│       │   ├── sokol.dll
│       │   └── sokol.lib
│       └── release/
│           ├── sokol.dll
│           └── sokol.lib
├── macos/
│   ├── arm64/
│   │   ├── debug/libsokol.dylib
│   │   └── release/libsokol.dylib
│   └── x86_64/
│       ├── debug/libsokol.dylib
│       └── release/libsokol.dylib
├── linux/
│   └── x86_64/
│       ├── debug/libsokol.so
│       └── release/libsokol.so
└── emscripten/
    └── x86/
        ├── debug/sokol.a
        └── release/sokol.a
```

## Manual Workflow Dispatch

You can manually trigger the build workflow from GitHub:

1. Go to Actions tab
2. Select "Build Sokol Libraries" workflow
3. Click "Run workflow" button
4. Select branch to build from
5. Click "Run workflow"

This is useful for testing or building from a feature branch.

## Requirements

### Local Build Requirements

#### macOS
- Xcode Command Line Tools
- CMake 3.16+

#### Windows
- Visual Studio 2022
- CMake 3.16+

#### Linux
- GCC or Clang
- CMake 3.16+
- Development packages: `libx11-dev`, `libxcursor-dev`, `libxi-dev`, `libasound2-dev`, `libgl1-mesa-dev`

#### Web
- Emscripten SDK 3.1.34 (managed by `tools/emsdk` submodule)

### GitHub Actions Requirements

All dependencies are automatically installed by the workflow. No additional setup required.

## Troubleshooting

### Build Failures

If a build fails locally or in CI:

1. Check that all git submodules are initialized:
   ```bash
   git submodule update --init --recursive
   ```

2. Ensure CMake version is 3.16 or higher:
   ```bash
   cmake --version
   ```

3. For Linux, ensure all development packages are installed:
   ```bash
   sudo apt-get install build-essential cmake libx11-dev libxcursor-dev libxi-dev libasound2-dev libgl1-mesa-dev
   ```

4. For Web builds, ensure Emscripten SDK is properly initialized:
   ```bash
   cd tools/emsdk
   ./emsdk install 3.1.34
   ./emsdk activate 3.1.34
   source ./emsdk_env.sh
   ```

### Architecture-specific Issues

#### Linux ARM64 (Not in CI/CD)
ARM64 Linux builds are **not included in the CI/CD pipeline** due to cross-compilation complexity and repository limitations. 

**For ARM64 Linux builds:**
- Build natively on ARM64 hardware (e.g., Raspberry Pi, AWS Graviton)
- Or use QEMU user-mode emulation for cross-compilation locally

**Native ARM64 build (recommended):**
```bash
# On ARM64 Linux machine
./scripts/build-linux-library.sh
```

**Cross-compilation (advanced, may have issues):**
Cross-compiling from x86_64 to ARM64 requires multi-arch setup which is unreliable on GitHub Actions. If you need to cross-compile locally, you can attempt it at your own risk.

#### GCC vs Clang Compiler Differences
The CMakeLists.txt automatically handles compiler-specific flags:
- Clang-only flags (like `-Wincompatible-pointer-types-discards-qualifiers`) are only applied when using Clang
- GCC uses its own compatible warning flags
- This is handled automatically by detecting `CMAKE_C_COMPILER_ID`

#### X11 Macro Conflicts on Linux
On Linux, X11 headers define a `Status` macro that conflicts with struct members in cimgui:
- The `ext/sokol.c` file undefines the X11 `Status` macro before including cimgui
- This is done conditionally only on Linux (`#if defined(__linux__)`)
- Prevents compilation errors without affecting X11 functionality

#### macOS Universal Binaries
To create a universal binary (both arm64 and x86_64):
```bash
lipo -create \
  libs/macos/arm64/release/libsokol.dylib \
  libs/macos/x86_64/release/libsokol.dylib \
  -output libs/macos/universal/libsokol.dylib
```

## Contributing

When modifying the build system:

1. Test your changes locally first using the appropriate build script
2. Ensure the GitHub Actions workflow still passes
3. Update this README if adding new platforms or changing the build process
4. Verify that artifact structure remains consistent

## License

Same as the main sokol-charp project.
