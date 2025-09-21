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

### iOS Scripts (macOS only)
- `interactive-ios-install.sh` - iOS installation script (Release)
- `interactive-ios-install-debug.sh` - iOS installation script (Debug)
- `list-ios-devices.sh` - iOS device and simulator listing
- `clear-ios-cache.sh` - Clear cached iOS Development Team IDs

### Node.js Scripts (All Platforms)
- `serve-web-browser.js` - Node.js web server for VS Code launch configurations

## iOS Task System

The iOS tasks in `.vscode/tasks.json` use macOS-specific configurations:

```json
{
    "label": "iOS: Install (Example)",
    "type": "shell",
    "osx": {
        "command": "${workspaceFolder}/scripts/interactive-ios-install.sh",
        "args": ["examples/cube/cube.csproj"]
    }
}

// Debug version uses interactive-ios-install-debug scripts
{
    "label": "iOS: Install Debug (Example)",
    "type": "shell",
    "osx": {
        "command": "${workspaceFolder}/scripts/interactive-ios-install-debug.sh",
        "args": ["examples/cube/cube.csproj"]
    }
}
```

## Features

Both Android and iOS script versions provide the same functionality:
- ✅ Automatic single device/simulator installation
- ✅ Interactive device/simulator selection for multiple devices
- ✅ Device model name detection
- ✅ Colored output and emojis
- ✅ Error handling and user feedback

## iOS Development Team ID Caching

The iOS installation scripts automatically cache your Apple Development Team ID to avoid repetitive prompting:

### How It Works
- **First Run**: Prompts for Team ID and caches it per project
- **Subsequent Runs**: Uses cached Team ID automatically
- **Cache Location**: `~/.sokol-charp-cache/{project-name}.teamid`
- **Per-Project**: Each example project maintains its own cache

### Cache Management
```bash
# Clear all cached team IDs
./scripts/clear-ios-cache.sh

# Clear specific project cache
rm ~/.sokol-charp-cache/cube.teamid

# Clear entire cache directory
rm -rf ~/.sokol-charp-cache/
```

### Cache Format
- **File**: `{project-name}.teamid` (e.g., `cube.teamid`)
- **Content**: 10-character alphanumeric Team ID
- **Validation**: Format checking before caching

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
- **iOS (macOS only)**: Xcode command line tools, ios-deploy (`brew install ios-deploy`)
- **WebAssembly**: Node.js (for browser launch configurations)
- **Web Serving**: Python 3.x or PowerShell (for manual web tasks)