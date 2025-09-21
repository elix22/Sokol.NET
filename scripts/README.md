# Cross-Platform Android Install Test

This directory contains both Bash and PowerShell versions of the Android installation scripts:

## Scripts Available

### Bash Scripts (macOS/Linux)
- `interactive-android-install.sh` - Main installation script (Release)
- `interactive-android-install-debug.sh` - Main installation script (Debug)
- `list-android-devices.sh` - Device listing script

### PowerShell Scripts (Windows)  
- `interactive-android-install.ps1` - Main installation script (Release)
- `interactive-android-install-debug.ps1` - Main installation script (Debug)
- `list-android-devices.ps1` - Device listing script

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

## Requirements

- **All Platforms**: Android SDK Platform Tools (adb) in PATH
- **Windows**: PowerShell 5.0+ (included in Windows 10/11)
- **macOS/Linux**: Bash shell (standard)