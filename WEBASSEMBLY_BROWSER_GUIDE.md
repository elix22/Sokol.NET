# WebAssembly Browser Development Guide

Run your Sokol examples directly in the browser using WebAssembly with automatic build and serve capabilities.

## Available Browser Launch Configurations

### VS Code Launch Configurations (F5 or Run menu)
- **Run Cube (Browser)** - Build and serve cube example in browser
- **Run Dyntex (Browser)** - Build and serve dyntex example in browser  
- **Run CImGui (Browser)** - Build and serve cimgui example in browser
- **Run LoadPNG (Browser)** - Build and serve loadpng example in browser
- **Run Instancing (Browser)** - Build and serve instancing example in browser
- **Run PlMpeg (Browser)** - Build and serve plmpeg example in browser

### VS Code Tasks (Ctrl+Shift+P → "Tasks: Run Task")

#### WebAssembly Build Tasks
- **build-cube-web** - Build cube example for WebAssembly
- **build-dyntex-web** - Build dyntex example for WebAssembly
- **build-cimgui-web** - Build cimgui example for WebAssembly
- **build-loadpng-web** - Build loadpng example for WebAssembly
- **build-instancing-web** - Build instancing example for WebAssembly
- **build-plmpeg-web** - Build plmpeg example for WebAssembly

#### Prepare Tasks (Build + Compile Shaders)
- **prepare-cube-web** - Compile shaders and build cube web
- **prepare-dyntex-web** - Compile shaders and build dyntex web
- **prepare-cimgui-web** - Compile shaders and build cimgui web
- **prepare-loadpng-web** - Compile shaders and build loadpng web
- **prepare-instancing-web** - Compile shaders and build instancing web
- **prepare-plmpeg-web** - Compile shaders and build plmpeg web

#### Web Server Tasks
- **Serve Web: Cube** - Serve cube example on local web server
- **Serve Web: Dyntex** - Serve dyntex example on local web server
- **Serve Web: CImGui** - Serve cimgui example on local web server
- **Serve Web: LoadPNG** - Serve loadpng example on local web server
- **Serve Web: Instancing** - Serve instancing example on local web server
- **Serve Web: PlMpeg** - Serve plmpeg example on local web server

## How to Use

### Option 1: One-Click Launch (Recommended)
1. **Open VS Code Launch Panel**: Press `F5` or use Run menu
2. **Select Browser Configuration**: Choose "Run [Example] (Browser)"
3. **Automatic Process**: 
   - Compiles shaders
   - Builds WebAssembly version
   - Starts local web server
   - Opens browser automatically

### Option 2: Manual Steps
1. **Build**: Run "prepare-[example]-web" task
2. **Serve**: Run "Serve Web: [Example]" task
3. **Browse**: Navigate to http://localhost:8000

## Platform Support

The WebAssembly browser functionality works on all platforms:

- **Windows**: Uses PowerShell scripts (`.ps1`)
- **macOS**: Uses Python scripts (`.py`) 
- **Linux**: Uses Python scripts (`.py`)

VS Code automatically selects the correct script based on your operating system.

## Technical Details

### Build Process
1. **Shader Compilation**: Compiles GLSL shaders for WebGL
2. **WebAssembly Build**: Uses `Microsoft.NET.Sdk.WebAssembly` 
3. **Output**: Creates `bin/Debug/net8.0/wwwroot/` with web assets
4. **Framework**: .NET 8.0 with WebAssembly runtime

### Web Server
- **Port**: localhost:8000
- **Technology**: Python HTTP server or PowerShell equivalent
- **Features**: Automatic browser opening, error handling
- **Files Served**: WebAssembly binaries, JavaScript runtime, HTML page

### Browser Requirements
- **Modern Browser**: Chrome, Firefox, Safari, Edge
- **WebAssembly Support**: All modern browsers support WASM
- **WebGL Support**: Required for graphics rendering

## Example Workflows

### Quick Development Test
```bash
# Press F5 → Select "Run Cube (Browser)"
# Output:
# 🚀 Serving examples/cube at http://localhost:8000
# 📁 Serving directory: examples/cube/bin/Debug/net8.0/wwwroot
# [Browser opens automatically]
```

### Manual Build and Serve
```bash
# Run task: prepare-cube-web
# Run task: Serve Web: Cube
# Output:
# 🚀 Serving examples/cube at http://localhost:8000
# [Browser opens automatically]
```

### Build Only (No Server)
```bash
# Run task: build-cube-web
# Output files: examples/cube/bin/Debug/net8.0/wwwroot/
```

## Troubleshooting

### Common Issues

**❌ "wwwroot not found"**
- **Solution**: Build the web project first using build task
- **Cause**: WebAssembly project not built yet

**❌ "Port 8000 already in use"**
- **Solution**: Stop other web servers or kill process using port 8000
- **Command**: `lsof -ti:8000 | xargs kill` (macOS/Linux)

**❌ "adb command not found" (Wrong error)**
- **Solution**: This is normal for web projects, ignore Android-related errors
- **Cause**: Some build scripts check for Android tools

**❌ Browser doesn't open automatically**
- **Solution**: Manually navigate to http://localhost:8000
- **Cause**: Firewall or browser permissions

### Performance Tips

- **Use Debug builds** for development (faster build times)
- **Use Release builds** for performance testing
- **Stop server** when switching between examples to free port 8000