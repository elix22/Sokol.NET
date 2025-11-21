# iOS Device and Simulator Selection

This guide explains how to build, install, and run Sokol.NET examples on iOS devices and simulators.

## Prerequisites

### Required Tools
- **Xcode**: Latest version with command line tools
- **ios-deploy**: For installing to physical iOS devices
  ```bash
  brew install ios-deploy
  ```
- **Xcode Simulator**: Included with Xcode

### Setup
1. Install Xcode from the Mac App Store
2. Install command line tools:
   ```bash
   xcode-select --install
   ```
3. Install ios-deploy:
   ```bash
   brew install ios-deploy
   ```

## Device Detection

### Physical iOS Devices
- Connect your iOS device via USB
- Trust the computer when prompted on the device
- Enable Developer Mode in Settings → Privacy & Security (iOS 16+)

### iOS Simulators
- Xcode automatically provides simulators
- No additional setup required

## VS Code Tasks

### Available Tasks
- **iOS: List Devices** - Show all connected devices and available simulators
- **iOS: Install (Example)** - Build and install example (Release)
- **iOS: Install Debug (Example)** - Build and install example (Debug)

### Examples Available
- **iOS: Install (Cube)** - Basic 3D cube example
- **iOS: Install (Dyntex)** - Dynamic texture example
- **iOS: Install (CImGui)** - Dear ImGui integration
- **iOS: Install (LoadPNG)** - PNG loading example
- **iOS: Install (Instancing)** - GPU instancing example
- **iOS: Install (PlMpeg)** - Video playback example

## How It Works

### Device Selection
When multiple devices/simulators are available, you'll be prompted to choose:

```
📱 Multiple iOS devices/simulators detected (3 total):
=================================================================
1) 📱 Physical: iPhone 15 Pro (ID: abc123...)
2) 📱 Simulator: iPhone 15 Pro (ID: def456...)
3) 📱 Simulator: iPad Pro (ID: ghi789...)

Select device/simulator (1-3): 1
✅ Selected physical: iPhone 15 Pro
🚀 Building and installing...
```

### Build Process
1. **Physical Devices**: Uses `BuildIOSInstall` target with device ID
2. **Simulators**: Uses `BuildIOSSimulator` target with simulator ID
3. **Debug/Release**: Configuration specified in task

### Interactive vs Non-Interactive
- **Terminal**: Direct number input
- **VS Code**: macOS dialog box for selection
- **Fallback**: Uses first device if dialog unavailable

## Troubleshooting

### Common Issues

#### "ios-deploy not found"
```bash
brew install ios-deploy
```

#### "No iOS devices found"
- Ensure device is connected via USB
- Trust the computer on the device
- Check if device appears in Xcode → Window → Devices and Simulators

#### "Device not trusted"
- Unlock device and tap "Trust" when prompted
- Enable Developer Mode (iOS 16+): Settings → Privacy & Security → Developer Mode

#### "Simulator not available"
- Open Xcode and create a simulator: Xcode → Window → Devices and Simulators
- Ensure Xcode command line tools are installed

### Device IDs
- **Physical**: UDID format (e.g., `abc123def456...`)
- **Simulator**: UUID format (e.g., `def456-789a-...`)

## Advanced Usage

### Manual Installation
```bash
# List devices
./scripts/list-ios-devices.sh

# Install to specific device
./scripts/interactive-ios-install.sh examples/cube/cube.csproj

# Install debug version
./scripts/interactive-ios-install-debug.sh examples/cube/cube.csproj
```

### MSBuild Targets
- `BuildIOSInstall` - Install to physical device
- `BuildIOSSimulator` - Install to simulator
- Properties: `IOSDeviceId`, `IOSSimulatorId`, `Configuration`

## Platform Notes

- **macOS Only**: iOS development requires macOS and Xcode
- **No Windows/Linux**: iOS development is macOS-exclusive
- **Free**: No Apple Developer Program required for basic development
- **Distribution**: Requires Apple Developer Program for App Store/TestFlight

---
**See also:**
- [Android Device Selection](ANDROID_DEVICE_SELECTION.md)
- [WebAssembly Setup](web-server-setup.md)