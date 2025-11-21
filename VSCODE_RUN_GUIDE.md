# Running Sokol.NET Applications from Visual Studio Code

This guide provides step-by-step instructions for running Sokol.NET applications on all supported platforms using Visual Studio Code's integrated debugging and task system.

## Table of Contents
- [Desktop (Windows, macOS, Linux)](#desktop-windows-macos-linux)
- [Web (WebAssembly)](#web-webassembly)
- [Android](#android)
- [iOS](#ios)

---

## Desktop (Windows, macOS, Linux)

Running desktop applications is the quickest way to test your Sokol.NET applications with native performance.

### Step 1: Open Run and Debug Panel

Press **F5** or click the **Run and Debug** icon in the left sidebar (play button with bug icon).

![Open Run and Debug](../screenshots/Desktop-run/Screenshot%202025-11-18%20at%2020.55.49.png)

### Step 2: Select Desktop Configuration

Click on the configuration dropdown at the top and select **"Desktop (Sokol)"**.

![Select Desktop Configuration](../screenshots/Desktop-run/Screenshot%202025-11-18%20at%2020.56.03.png)

### Step 3: Choose an Example

Click the green play button or press **F5**. You'll be prompted to select an example from the list.

![Choose Example](../screenshots/Desktop-run/Screenshot%202025-11-18%20at%2020.56.13.png)

Select any example (e.g., "cube", "cimgui", "GltfViewer") from the dropdown menu.

### Step 4: Application Running

The application will build and launch. You'll see the debug console output and the application window will open.

![Application Running](../screenshots/Desktop-run/Screenshot%202025-11-18%20at%2020.56.24.png)

**That's it!** The desktop application is now running. You can set breakpoints in your C# code and use all of VS Code's debugging features.

### Desktop Tips
- **Stop debugging**: Press **Shift+F5** or click the red square stop button
- **Restart**: Stop and press **F5** again to rebuild and relaunch
- **Console output**: View build and runtime output in the **Debug Console** tab
- **Hot reload**: Not available for NativeAOT, but you can quickly rebuild and relaunch

---

## Web (WebAssembly)

Running WebAssembly applications allows you to test browser-based deployments of your Sokol.NET applications.

### Step 1: Open Run and Debug Panel

Press **F5** or click the **Run and Debug** icon in the left sidebar.

![Open Run and Debug](../screenshots/Web-run/Screenshot%202025-11-18%20at%2020.59.06.png)

### Step 2: Select Browser Configuration

Click on the configuration dropdown and select **"Browser (Sokol)"**.

![Select Browser Configuration](../screenshots/Web-run/Screenshot%202025-11-18%20at%2020.59.16.png)

### Step 3: Choose Example and Port

First, select the example you want to run:

![Choose Example](../screenshots/Web-run/Screenshot%202025-11-18%20at%2020.59.24.png)

Then, enter the port number (default is **8080** or choose any available port):

### Step 4: Application Running in Browser

VS Code will:
1. Build the WebAssembly application
2. Start the dotnet-serve web server
3. Automatically open your default browser

![Application Running](../screenshots/Web-run/Screenshot%202025-11-18%20at%2020.59.41.png)

The application runs in your browser with full WebGL support. Check the browser console (F12) for any runtime messages.

### Web Tips
- **Custom port**: Use a different port if 8080 is already in use
- **CORS headers**: dotnet-serve automatically sets required headers for SharedArrayBuffer support
- **Cache issues**: Use **Ctrl+Shift+R** (Cmd+Shift+R on macOS) for hard refresh if changes don't appear
- **Server logs**: View dotnet-serve output in the VS Code **Terminal** tab
- **Browser DevTools**: Press **F12** to open browser developer tools for debugging
- **Stop server**: The server stops automatically when you stop debugging, or use **Shift+F5**

For more details on WebAssembly development, see [docs/web-server-setup.md](web-server-setup.md).

---

## Android

Running Android applications requires a connected device or emulator and proper Android SDK setup.

### Prerequisites
- Android SDK and NDK 25+ installed
- Android device connected via USB with **USB Debugging** enabled, or Android emulator running
- First-time setup: You may need to accept USB debugging authorization on your device
- **Important**: If you have a VPN app installed on your computer, it may interfere with device communication. It's advisable to disable the VPN during installation

### Step 1: Open Command Palette

Press **Cmd+Shift+P** (macOS) or **Ctrl+Shift+P** (Windows/Linux) to open the Command Palette.

![Open Command Palette](../screenshots/Android-run/Screenshot%202025-11-18%20at%2021.01.07.png)

### Step 2: Select Tasks: Run Task

Type "task" and select **"Tasks: Run Task"** from the dropdown.

![Select Run Task](../screenshots/Android-run/Screenshot%202025-11-18%20at%2021.01.30.png)

### Step 3: Choose Android Task

Select **"Android: Install APK"** or **"Android: Install AAB"** depending on your preference.

![Choose Android Task](../screenshots/Android-run/Screenshot%202025-11-18%20at%2021.01.42.png)

**APK vs AAB:**
- **APK**: Faster for development, universal binary
- **AAB**: Optimized for Play Store, generates device-specific APKs

### Step 4: Select Build Type

Choose between **"debug"** or **"release"** build:

![Select Build Type](../screenshots/Android-run/Screenshot%202025-11-18%20at%2021.04.27.png)

- **debug**: Faster builds, includes debug symbols, not optimized
- **release**: Optimized build, ready for distribution

### Step 5: Choose Example

Select the example you want to build and install:

![Choose Example](../screenshots/Android-run/Screenshot%202025-11-18%20at%2021.04.39.png)

### Step 6: Select Target Device

The build system will detect all connected Android devices and emulators. Select your target device:

![Select Device](../screenshots/Android-run/Screenshot%202025-11-18%20at%2021.04.51.png)

**Device format:** `[Manufacturer] [Model] (ID: [device-id])`

### Step 7: Build and Install

The build process will:
1. Compile the C# code to native ARM64 code
2. Package native libraries and assets
3. Create APK/AAB file
4. Install to the selected device
5. Automatically launch the application

![Build Output](../screenshots/Android-run/Screenshot%202025-11-18%20at%2021.04.59.png)

Monitor the build progress in the Terminal tab. The application will launch automatically on your device when installation completes.

### Android Tips
- **Multiple devices**: To install on multiple devices, see [docs/MULTI_DEVICE_INSTALL.md](MULTI_DEVICE_INSTALL.md)
- **List devices**: Run task **"Android: List Devices"** to see all connected devices
- **Build only**: Use **"Android: Build APK"** or **"Android: Build AAB"** tasks to build without installing
- **Build location**: Find APK/AAB files in `examples/[name]/output/Android/release/` or `examples/[name]/output/Android/debug/`
- **VPN interference**: Disable VPN apps on your computer if you experience connection issues with your device
- **Screen orientation**: Configure in project's `.csproj` file (see [docs/ANDROID_SCREEN_ORIENTATION.md](ANDROID_SCREEN_ORIENTATION.md))
- **App icon**: Customize icons with [docs/APP_ICON.md](APP_ICON.md)
- **Logcat**: Use `adb logcat` to view runtime logs from the device
- **Reinstall**: The task automatically uninstalls previous versions before installing

For more Android configuration options, see [docs/ANDROID_PROPERTIES.md](ANDROID_PROPERTIES.md).

---

## iOS

Running iOS applications requires a Mac with Xcode and a connected iOS device or simulator.

### Prerequisites
- macOS with Xcode 14+ installed
- iOS device connected via USB, or iOS Simulator
- Apple Developer account (free account works for device testing)
- First-time: You'll need to provide your **Team ID** from Apple Developer portal
- **Important**: If you have a VPN app installed on your Mac, it may interfere with device communication. It's advisable to disable the VPN during installation

### Step 1: Open Command Palette

Press **Cmd+Shift+P** to open the Command Palette.

![Open Command Palette](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.08.34.png)

### Step 2: Select Tasks: Run Task

Type "task" and select **"Tasks: Run Task"**.

![Select Run Task](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.08.42.png)

### Step 3: Choose iOS Task

Select **"iOS: Install"** to build and install to a device/simulator.

![Choose iOS Task](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.08.47.png)

Alternatively, select **"iOS: Build"** if you only want to build without installing.

### Step 4: Select Build Type

Choose between **"debug"** or **"release"** build:

![Select Build Type](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.08.57.png)

- **debug**: Faster builds for development
- **release**: Optimized builds for testing/distribution

### Step 5: Choose Example

Select the example application to build:

![Choose Example](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.09.09.png)

### Step 6: Enter Team ID (First Time Only)

If this is your first iOS build, you'll be prompted to enter your **Apple Developer Team ID**:

![Enter Team ID](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.09.40.png)

**Finding your Team ID:**
1. Visit [Apple Developer Portal](https://developer.apple.com/account)
2. Log in with your Apple ID
3. Go to **Membership** section
4. Your **Team ID** is displayed there (format: `XXXXXXXXXX`)

The Team ID is cached locally at `~/.Sokol.NET-cache/{projectName}.teamid` so you only need to enter it once per project.

### Step 7: Select Target Device

Choose from connected iOS devices or simulators:

![Select Device](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.10.45.png)

**Device format:**
- Physical devices: `[Device Name] (ID: [device-id])`
- Simulators: `[Simulator Name] (Simulator)`

### Step 8: Build Process

The build system will:
1. Compile C# to native ARM64 code
2. Generate Xcode project
3. Build with Xcode
4. Code sign with your certificate
5. Install to device/simulator

![Build Output](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.11.11.png)

Monitor the progress in the Terminal tab.

### Step 9: Trust Developer Certificate (First Time on Device)

If installing to a physical device for the first time, you'll need to trust the developer certificate on your device:

![Trust Certificate](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.11.20.png)

1. On your iOS device, go to **Settings** → **General** → **VPN & Device Management**
2. Tap on your developer certificate
3. Tap **"Trust [Your Developer Name]"**
4. Confirm trust

### Step 10: Application Running

Once trusted, the application will launch automatically:

![Application Running](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.11.32.png)

The app is now running on your iOS device or simulator!

### Step 11: Relaunch Anytime

After the first installation, you can launch the app directly from your device's home screen:

![Launch from Home Screen](../screenshots/iOS-run/Screenshot%202025-11-18%20at%2021.11.45.png)

### iOS Tips
- **List devices**: Run task **"iOS: List Devices"** to see all connected devices and simulators
- **Clear Team ID**: Delete `~/.Sokol.NET-cache/{projectName}.teamid` to enter a new Team ID
- **Simulator testing**: iOS Simulator doesn't require a developer certificate
- **VPN interference**: Disable VPN apps on your Mac if you experience connection issues with your device
- **Device logs**: Use **Console.app** on macOS or Xcode's device console to view logs
- **Screen orientation**: Configure in project's `.csproj` file (landscape/portrait)
- **Build location**: Find `.app` bundles in `examples/[name]/output/iOS/release/` or `examples/[name]/output/iOS/debug/`
- **Xcode debugging**: The build process creates an Xcode project in `examples/[name]/ios/build-xcode-ios-app/[name]-ios-app.xcodeproj` that you can open in Xcode for advanced debugging, profiling, and testing
- **App icon**: Customize with [docs/APP_ICON.md](APP_ICON.md)
- **Free account limits**: Free Apple Developer accounts can install to limited devices and apps expire after 7 days

For more iOS configuration options, see [docs/IOS_PROPERTIES.md](IOS_PROPERTIES.md) and [docs/ios-device-selection.md](ios-device-selection.md).

---

## General VS Code Tips

### Keyboard Shortcuts
- **F5**: Start debugging
- **Shift+F5**: Stop debugging
- **Ctrl+Shift+P** / **Cmd+Shift+P**: Command Palette
- **Ctrl+`** / **Cmd+`**: Toggle Terminal panel

### Task Explorer
- All available tasks are visible in the **Command Palette** → **Tasks: Run Task**
- Tasks are defined in `.vscode/tasks.json`
- Custom tasks can be added for your specific needs

### Build Output
- **Terminal tab**: Shows build and deployment output
- **Debug Console tab**: Shows application runtime output (desktop only)
- **Problems tab**: Shows compile-time errors and warnings

### Troubleshooting
- **Build fails**: Check the Terminal output for specific error messages
- **Device not found**: Ensure device is connected and USB debugging is enabled
- **Port in use (Web)**: Choose a different port number
- **Certificate issues (iOS)**: Verify Team ID and trust certificate on device

---

## Next Steps

- **Explore examples**: Try running different examples to see various features
- **Modify code**: Edit the C# source files and rebuild to see your changes
- **Read documentation**: Check the `docs/` folder for platform-specific guides
- **Create your own app**: Use the project templates to start your own Sokol.NET application

For more detailed information:
- [Build System Documentation](BUILD_SYSTEM.md)
- [Android Properties](ANDROID_PROPERTIES.md)
- [iOS Properties](IOS_PROPERTIES.md)
- [WebAssembly Guide](WEBASSEMBLY_BROWSER_GUIDE.md)

---

**Happy coding with Sokol.NET!** 🚀
