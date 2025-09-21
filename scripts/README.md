# Cross-Platform Android Install Test

This directory contains both Bash and PowerShell versions of the Android installation scripts:

## Scripts Available

### Bash Scripts (macOS/Linux)
- `interactive-android-install.sh` - Main installation script (Release)
- `interactive-android-install-debug.sh` - Main installation script (Debug)
- `list-android-devices.sh` - Device listing script
- `serve-web-example.py` - Web server for WebAssembly examples

### PowerShell Scripts (Windows)  
- `interactive-android-install.ps1` - Main installation script (Release)
- `interactive-android-install-debug.ps1` - Main installation script (Debug)
- `list-android-devices.ps1` - Device listing script
- `serve-web-example.ps1` - Web server for WebAssembly examples

### Node.js Scripts (All Platforms)
- `serve-web-browser.js` - Node.js web server for VS Code launch configurations

## How VS Code Tasks Work

The tasks in `.vscode/tasks.json` use platform-specific configurations:

```json
{
    "label": "Android: Install (Example)",
    "type": "shell",
    "windows": {
        "command": "powershell",
        "args": ["-ExecutionPolicy", "Bypass", "-File", "${workspaceFolder}/scripts/interactive-android-install.ps1", "examples/cube/cube.csproj"]
    },
    "linux": {
        "command": "${workspaceFolder}/scripts/interactive-android-install.sh",
        "args": ["examples/cube/cube.csproj"]
    },
    "osx": {
        "command": "${workspaceFolder}/scripts/interactive-android-install.sh", 
        "args": ["examples/cube/cube.csproj"]
    }
}

// Debug version uses interactive-android-install-debug scripts
{
    "label": "Android: Install Debug (Example)",
    "windows": {
        "command": "powershell",
        "args": ["-ExecutionPolicy", "Bypass", "-File", "${workspaceFolder}/scripts/interactive-android-install-debug.ps1", "examples/cube/cube.csproj"]
    },
    // ... similar structure for debug
}
```

## Features

Both script versions provide the same functionality:
- ✅ Automatic single device installation
- ✅ Interactive device selection for multiple devices
- ✅ Device model name detection
- ✅ Colored output and emojis
- ✅ Error handling and user feedback

## Web Server Scripts

The web server scripts provide local hosting for WebAssembly examples:

### Python/PowerShell Servers (serve-web-example.py/.ps1)
- ✅ Automatic browser opening
- ✅ Serves from built wwwroot directory  
- ✅ Error handling for missing builds
- ✅ Cross-platform support (Python/PowerShell)
- 🎯 Used by VS Code "Serve Web: Example" tasks

### Node.js Server (serve-web-browser.js)
- ✅ Compatible with VS Code launch configurations
- ✅ Automatic browser opening with proper MIME types
- ✅ WebAssembly CORS headers (COEP/COOP)
- ✅ Security: prevents directory traversal
- ✅ Graceful shutdown handling
- 🎯 Used by VS Code "Example (Browser)" launch configs

## VS Code Integration

### Launch Configurations
The `.vscode/launch.json` file provides browser launch configurations:
- **Desktop**: `Run Example (Desktop)` - Launches .NET desktop versions
- **Browser**: `Example (Browser)` - Builds WebAssembly and opens in browser

Example browser launch configuration:
```json
{
    "name": "Cube (Browser)",
    "type": "node",
    "request": "launch", 
    "program": "${workspaceFolder}/scripts/serve-web-browser.js",
    "args": ["cube"],
    "cwd": "${workspaceFolder}",
    "console": "integratedTerminal",
    "preLaunchTask": "prepare-cube-web"
}
```

### Task System
Tasks handle building, preparation, and serving:
- `build-example-web` - Builds WebAssembly version
- `prepare-example-web` - Builds + runs shaders for web
- `Serve Web: Example` - Starts web server for manual testing

## Requirements

- **All Platforms**: Android SDK Platform Tools (adb) in PATH
- **Windows**: PowerShell 5.0+ (included in Windows 10/11)
- **macOS/Linux**: Bash shell (standard)
- **WebAssembly**: Node.js (for browser launch configurations)
- **Web Serving**: Python 3.x or PowerShell (for manual web tasks)