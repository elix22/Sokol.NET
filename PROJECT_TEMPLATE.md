# Sokol C# Project Template System

This document describes how to create new Sokol C# projects from the `clear` template.

## Quick Start

Create a new project using the template script:

### macOS / Linux

```bash
# Create a new project in the examples folder
./scripts/create-project-from-template.sh myproject

# Create a new project in a custom location
./scripts/create-project-from-template.sh myproject /path/to/target/folder
```

Then add it to VS Code configuration:

```bash
./scripts/add-project-to-vscode-config.sh myproject
```

### Windows

```powershell
# Create a new project in the examples folder
.\scripts\create-project-from-template.ps1 myproject

# Create a new project in a custom location
.\scripts\create-project-from-template.ps1 myproject C:\Path\To\Target\Folder
```

Then add it to VS Code configuration:

```powershell
.\scripts\add-project-to-vscode-config.ps1 myproject
```

### Using Python (All Platforms)

```bash
# Create a new project
python3 scripts/create-project-from-template.py myproject

# Add to VS Code configuration
python3 scripts/add-project-to-vscode-config.py myproject
```

## What Gets Created

The template creates a complete Sokol C# project with:

### Project Structure
```
myproject/
├── myproject.csproj           # Desktop/Mobile project file
├── myprojectWeb.csproj        # WebAssembly project file
├── Directory.Build.props      # Build properties
├── Directory.Build.targets    # Build targets
├── runtimeconfig.template.json
├── Source/
│   ├── Program.cs            # Entry point with Desktop/Android/iOS support
│   └── myproject-app.cs      # Main application code
├── Assets/                    # Assets folder (images, etc.)
├── shaders/                   # Shader source files
└── wwwroot/                   # Web-specific files
    ├── index.html
    ├── main.js
    └── sokol_js_lib.js
```

### Features Included

- **Multi-platform support**: Desktop (Windows, macOS, Linux), WebAssembly, Android, iOS
- **Native AOT compilation**: Optimized native binaries
- **Sokol integration**: Full access to Sokol graphics API
- **Asset management**: Automatic asset copying
- **Shader compilation**: Integrated shader pipeline
- **Hot reload support**: Fast development iteration

## The Template (`clear` project)

The `clear` project is a minimal Sokol application that:
- Initializes the Sokol graphics API
- Clears the screen with an animated color
- Provides platform-specific entry points
- Demonstrates basic render loop structure

It serves as an excellent starting point for new projects.

## Usage Examples

### Example 1: Create a game project (macOS/Linux)

```bash
# Create the project
./scripts/create-project-from-template.sh spaceshooter

# Add to VS Code config
./scripts/add-project-to-vscode-config.sh spaceshooter

# Build and run
cd examples/spaceshooter
dotnet build spaceshooter.csproj
dotnet run --project spaceshooter.csproj
```

### Example 1: Create a game project (Windows)

```powershell
# Create the project
.\scripts\create-project-from-template.ps1 spaceshooter

# Add to VS Code config
.\scripts\add-project-to-vscode-config.ps1 spaceshooter

# Build and run
cd examples\spaceshooter
dotnet build spaceshooter.csproj
dotnet run --project spaceshooter.csproj
```

### Example 2: Create a visualization tool

```bash
# macOS/Linux
./scripts/create-project-from-template.sh datavis
./scripts/add-project-to-vscode-config.sh datavis

# Windows
.\scripts\create-project-from-template.ps1 datavis
.\scripts\add-project-to-vscode-config.ps1 datavis

# Build for web (all platforms)
cd examples/datavis
dotnet build datavisWeb.csproj
```

### Example 3: Create in custom location

```bash
# macOS/Linux
./scripts/create-project-from-template.sh myapp ~/Projects/sokol-projects

# Windows
.\scripts\create-project-from-template.ps1 myapp C:\Projects\sokol-projects

# This creates: ~/Projects/sokol-projects/myapp/ (or C:\Projects\sokol-projects\myapp\)
```

## Manual Integration with VS Code

If you don't use the automatic script, you need to manually add your project to:

### 1. `.vscode/tasks.json`

Add these tasks (replace `myproject` with your project name):

```json
{
    "label": "compile-myproject-shaders",
    "command": "dotnet",
    "type": "process",
    "args": [
        "msbuild",
        "examples/myproject/myproject.csproj",
        "-t:CompileShaders"
    ],
    "problemMatcher": "$msCompile"
},
{
    "label": "build-myproject",
    "command": "dotnet",
    "type": "process",
    "args": [
        "build",
        "examples/myproject/myproject.csproj"
    ],
    "group": "build",
    "problemMatcher": "$msCompile"
},
{
    "label": "prepare-myproject",
    "dependsOrder": "sequence",
    "dependsOn": [
        "compile-myproject-shaders",
        "build-myproject"
    ]
}
```

Add your project to the inputs:

```json
{
    "id": "examplePath",
    "options": [
        // ... existing options ...
        "examples/myproject"
    ]
}
```

### 2. `.vscode/launch.json`

Add your project to the inputs:

```json
{
    "id": "exampleName",
    "options": [
        // ... existing options ...
        "myproject"
    ]
}
```

## Building Your Project

### Desktop

```bash
# Debug build
dotnet build examples/myproject/myproject.csproj

# Release build
dotnet build -c Release examples/myproject/myproject.csproj

# Run
dotnet run --project examples/myproject/myproject.csproj
```

### WebAssembly

```bash
# Build
dotnet build examples/myproject/myprojectWeb.csproj

# Serve (from workspace root)
python scripts/serve-web-example.py examples/myproject
```

### Android

```bash
# Build APK
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture android \
    --path examples/myproject

# Install on device
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture android \
    --install \
    --interactive \
    --path examples/myproject
```

### iOS

```bash
# Build IPA
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture ios \
    --orientation landscape \
    --path examples/myproject

# Install on device
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture ios \
    --install \
    --interactive \
    --orientation landscape \
    --path examples/myproject
```

## Customizing Your Project

### 1. Update Application Logic

Edit `Source/myproject-app.cs`:

```csharp
[UnmanagedCallersOnly]
private static unsafe void Init()
{
    // Initialize your application
    sg_setup(new sg_desc()
    {
        environment = sglue_environment(),
        logger = { func = &slog_func }
    });
    
    // Your initialization code here
}

[UnmanagedCallersOnly]
private static unsafe void Frame()
{
    // Your render code here
    sg_begin_pass(new sg_pass { 
        action = state.pass_action, 
        swapchain = sglue_swapchain() 
    });
    
    // Draw calls go here
    
    sg_end_pass();
    sg_commit();
}
```

### 2. Add Shaders

Create shader files in `shaders/` and they'll be automatically compiled:

```glsl
// shaders/myshader.glsl
@vs vs
// vertex shader
@end

@fs fs
// fragment shader
@end

@program myprogram vs fs
```

### 3. Add Assets

Place assets in the `Assets/` folder. They'll be automatically copied to the output directory.

### 4. Modify Window Properties

Edit the `sokol_main()` function in your app file:

```csharp
return new SApp.sapp_desc()
{
    init_cb = &Init,
    frame_cb = &Frame,
    event_cb = &Event,
    cleanup_cb = &Cleanup,
    width = 1280,           // Custom width
    height = 720,           // Custom height
    sample_count = 4,       // MSAA samples
    window_title = "My Awesome App",
    icon = { sokol_default = true },
    logger = { func = &slog_func }
};
```

## Tips and Best Practices

1. **Keep the template simple**: The `clear` project is intentionally minimal
2. **Use consistent naming**: Follow C# naming conventions
3. **Test on multiple platforms**: Desktop, Web, and Mobile have different constraints
4. **Commit early**: Initialize git in your project folder
5. **Add .gitignore**: Exclude `bin/`, `obj/`, and build artifacts

## Troubleshooting

### "Template not found"
Make sure you're running the script from the workspace root or scripts directory.

### "Target directory already exists"
The script will ask if you want to overwrite. Say 'yes' to replace, or choose a different name.

### "Build fails with missing libraries"
Make sure the Sokol libraries are built:
- Windows: Run `scripts/build-vs2022-windows.ps1`
- macOS: Run `scripts/build-xcode-macos.sh`
- Linux: Run `scripts/build-linux-library.sh`
- Web: Run `scripts/build-web-library.sh`

### "VS Code tasks not showing up"
Reload VS Code window: `Ctrl+Shift+P` (or `Cmd+Shift+P`) -> "Developer: Reload Window"

## Advanced: Creating Custom Templates

To create a new template:

1. Create a complete project in `examples/`
2. Test it thoroughly on all target platforms
3. Update the `create-project-from-template.py` script to add your template
4. Document any template-specific setup requirements

## See Also

- [Build System Documentation](docs/BUILD_SYSTEM.md)
- [Android Device Selection](docs/ANDROID_DEVICE_SELECTION.md)
- [iOS Device Selection](docs/ios-device-selection.md)
- [WebAssembly Browser Guide](docs/WEBASSEMBLY_BROWSER_GUIDE.md)
