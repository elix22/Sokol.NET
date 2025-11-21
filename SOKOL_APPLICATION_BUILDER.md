# SokolApplicationBuilder

SokolApplicationBuilder is a command-line tool for building, preparing, and deploying Sokol C# applications across multiple platforms.

## Overview

SokolApplicationBuilder provides a unified interface for:
- **Preparing projects** (compiling shaders + building)
- **Building applications** for Desktop, Android, iOS, and Web
- **Managing shader compilation** with platform-specific configurations
- **Creating app bundles** and packages for distribution

## Installation

The tool is located at `tools/SokolApplicationBuilder` and can be run using:

```bash
dotnet run --project tools/SokolApplicationBuilder -- [arguments]
```

## Usage

### Basic Syntax

```bash
dotnet run --project tools/SokolApplicationBuilder -- --task <task> [options]
```

## Tasks

### 1. Prepare Task

The **prepare** task combines shader compilation and project building in a single operation. This is the recommended way to prepare a project for debugging or running.

**Syntax:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task prepare \
    --architecture <desktop|web|android|ios> \
    --path <project-path>
```

**What it does:**
1. Compiles all shaders in the project using `sokol-shdc`
2. Builds the project using `dotnet build`
3. Outputs the compiled application ready for execution

**Example - Desktop:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task prepare \
    --architecture desktop \
    --path examples/cube
```

**Example - Web:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task prepare \
    --architecture web \
    --path examples/cube
```

**Output:**
```
🚀 Preparing project...
Project: cube
🎨 Compiling shaders...
✅ Shaders compiled successfully
📦 Building project...
✅ Project built successfully
🎉 cube is ready!
```

**VS Code Integration:**

The prepare task is integrated with VS Code tasks and launch configurations:

`.vscode/tasks.json`:
```json
{
    "label": "prepare-cube",
    "type": "shell",
    "command": "dotnet",
    "args": [
        "run",
        "--project",
        "${workspaceFolder}/tools/SokolApplicationBuilder",
        "--",
        "--task",
        "prepare",
        "--architecture",
        "desktop",
        "--path",
        "${workspaceFolder}/examples/cube"
    ]
}
```

`.vscode/launch.json`:
```json
{
    "name": "Desktop (Sokol)",
    "type": "coreclr",
    "request": "launch",
    "preLaunchTask": "prepare-${input:exampleName}",
    "program": "${workspaceFolder}/examples/${input:exampleName}/bin/Debug/net10.0/${input:exampleName}.dll"
}
```

When debugging (F5), VS Code will:
1. Prompt for example name
2. Run the prepare task automatically
3. Launch the debugger with full breakpoint support

### 2. Build Task

The **build** task creates release builds and app bundles for distribution.

**Syntax:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type <debug|release> \
    --architecture <desktop|android|ios|web> \
    --rid <runtime-identifier> \
    --path <project-path> \
    [--install] \
    [--interactive]
```

**Desktop Example:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture desktop \
    --rid osx-arm64 \
    --path examples/cube
```

**Android Example:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture android \
    --path examples/cube \
    --install \
    --interactive
```

**iOS Example:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture ios \
    --path examples/cube \
    --install \
    --interactive \
    --orientation landscape
```

### 3. Register Task

Registers the tool for use with `dotnet sokol-build` command.

**Syntax:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- --task register
```

After registration, you can use:
```bash
dotnet sokol-build --task prepare --architecture desktop --path examples/cube
```

## Options

| Option | Description | Values | Required |
|--------|-------------|--------|----------|
| `--task` | Task to execute | `prepare`, `build`, `register` | Yes |
| `--architecture` | Target platform | `desktop`, `web`, `android`, `ios` | For prepare/build |
| `--path` | Project path | Path to .csproj directory | For prepare/build |
| `--type` | Build configuration | `debug`, `release` | For build task |
| `--rid` | Runtime identifier | `osx-arm64`, `osx-x64`, `win-x64`, `linux-x64` | For desktop builds |
| `--install` | Install on device after build | Flag | Optional |
| `--interactive` | Interactive device selection | Flag | Optional |
| `--orientation` | App orientation (iOS) | `portrait`, `landscape` | Optional |
| `--subtask` | Build subtask (Android) | `aab` (Android App Bundle) | Optional |

## Platform-Specific Details

### Desktop (macOS, Windows, Linux)

**Supported Runtime Identifiers:**
- `osx-arm64` - macOS Apple Silicon
- `osx-x64` - macOS Intel
- `win-x64` - Windows 64-bit
- `linux-x64` - Linux 64-bit

**Output:**
- Debug: `bin/Debug/net10.0/`
- Release: `bin/Release/net10.0/{rid}/publish/`

### Web (Browser/WASM)

**Architecture:** `web`

**Process:**
1. Compiles shaders with `--web` flag
2. Builds with `browser-wasm` target framework
3. Outputs to `bin/Debug/net8.0/browser-wasm/AppBundle/`

**Requirements:**
- .NET 8.0+ SDK with WASM workload
- Emscripten (for native libraries)

**Running:**
Use `dotnet-serve` or any HTTP server to serve the AppBundle:
```bash
dotnet run --project tools/dotnet-serve/src/dotnet-serve/dotnet-serve.csproj -- \
    -d examples/cube/bin/Debug/net8.0/browser-wasm/AppBundle \
    -p 8080
```

### Android

**Architecture:** `android`

**Output Formats:**
- APK: `--subtask` not specified
- AAB: `--subtask aab`

**Device Installation:**
```bash
# Interactive device selection
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture android \
    --path examples/cube \
    --install \
    --interactive
```

**Requirements:**
- Android SDK
- Java Development Kit (JDK)
- Android NDK

### iOS

**Architecture:** `ios`

**Device Installation:**
```bash
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture ios \
    --path examples/cube \
    --install \
    --interactive \
    --orientation landscape
```

**Requirements:**
- macOS with Xcode
- iOS device or simulator
- Apple Developer account (for device deployment)

## Shader Compilation

The prepare task automatically handles shader compilation using `sokol-shdc`.

**Shader Location:**
- Shaders must be in the project directory
- Typically named `{projectname}.glsl`
- Example: `examples/cube/cube.glsl`

**Platform-Specific Compilation:**

| Architecture | Shader Backends | Defines |
|--------------|-----------------|---------|
| Desktop | `glsl430:metal_macos:hlsl5` | None |
| Web | `glsl300es` | `__WEB__` |
| Android | `glsl300es` | `__ANDROID__` |
| iOS | `metal_ios` | `__IOS__` |

**Output:**
- Desktop/Android/iOS: `{projectname}.gen.cs`
- Web: `{projectname}Web.gen.cs`

## Project Structure Requirements

For the prepare task to work correctly:

```
examples/cube/
├── cube.csproj          # Standard .csproj
├── cubeweb.csproj       # Web-specific .csproj (optional)
├── cube.glsl            # Shader source
├── Program.cs           # Application code
└── bin/                 # Build output (generated)
```

**Project Detection:**
- The tool automatically detects the correct `.csproj` file
- For web builds, it looks for `{name}Web.csproj` or `{name}web.csproj`
- Falls back to `{name}.csproj` if web-specific project not found

## Error Handling

The tool provides clear error messages:

```
❌ Shader compilation failed
❌ Build failed
❌ Project file not found: {path}
❌ No shader files found in project directory
```

**Common Issues:**

1. **Shader compilation fails:**
   - Check shader syntax in `.glsl` file
   - Ensure `sokol-shdc` is in `tools/bin/{platform}/`
   - Verify shader file exists in project directory

2. **Build fails:**
   - Run `dotnet restore` in project directory
   - Check for compilation errors in project code
   - Verify target framework is installed

3. **Project not found:**
   - Verify `--path` points to directory containing `.csproj`
   - Ensure project name matches directory name

## Integration with VS Code

### Launch Configurations

Two simple launch configurations handle all examples:

```json
{
    "configurations": [
        {
            "name": "Desktop (Sokol)",
            "preLaunchTask": "prepare-${input:exampleName}",
            "program": "${workspaceFolder}/examples/${input:exampleName}/bin/Debug/net10.0/${input:exampleName}.dll"
        },
        {
            "name": "Browser (Sokol)",
            "preLaunchTask": "prepare-${input:exampleName}-web"
        }
    ]
}
```

### Task Definitions

Each example has two prepare tasks:

```json
{
    "tasks": [
        {
            "label": "prepare-cube",
            "command": "dotnet",
            "args": ["run", "--project", "tools/SokolApplicationBuilder", "--", 
                     "--task", "prepare", "--architecture", "desktop", 
                     "--path", "examples/cube"]
        },
        {
            "label": "prepare-cube-web",
            "command": "dotnet",
            "args": ["run", "--project", "tools/SokolApplicationBuilder", "--", 
                     "--task", "prepare", "--architecture", "web", 
                     "--path", "examples/cube"]
        }
    ]
}
```

### Debugging Workflow

1. Press **F5** in VS Code
2. Select **"Desktop (Sokol)"** or **"Browser (Sokol)"**
3. Select example from dropdown (e.g., "cube", "dyntex")
4. PrepareTask runs automatically:
   - ✅ Compiles shaders
   - ✅ Builds project
5. Debugger launches with breakpoint support

**Benefits:**
- Single prompt for example selection
- Automatic shader compilation
- Automatic project building
- Full debugging support with breakpoints
- No manual build steps required

## Implementation Details

### Source Code Structure

```
tools/SokolApplicationBuilder/
├── Program.cs                    # Entry point, task dispatcher
├── SokolApplicationBuilder.csproj
└── Source/
    ├── PrepareTask.cs           # Prepare task implementation
    ├── BuildTask.cs             # Build task implementation
    ├── RegisterTask.cs          # Registration task
    ├── ShaderCompileTask.cs     # Shader compilation
    └── Utils.cs                 # Helper utilities
```

### PrepareTask Implementation

The `PrepareTask` class (`Source/PrepareTask.cs`) implements the two-phase preparation:

**Phase 1: Shader Compilation**
- Detects shader files in project directory
- Runs `sokol-shdc` with architecture-specific flags
- Uses MSBuild API: `dotnet msbuild -t:CompileShaders`

**Phase 2: Project Building**
- Determines correct `.csproj` file (web vs desktop)
- Runs `dotnet build` with appropriate configuration
- Validates build success

**Key Methods:**
- `Execute()` - Main execution flow
- `GetProjectName(string path)` - Smart project detection
- MSBuild integration for shader compilation

## Extending the Tool

### Adding a New Task

1. Create a new task class in `Source/`:
```csharp
public class MyNewTask : TaskBase
{
    public override bool Execute()
    {
        // Implementation
        return true;
    }
}
```

2. Register in `Program.cs`:
```csharp
case "mynew":
    task = new MyNewTask
    {
        BuildEngine = buildEngine,
        // Set properties
    };
    break;
```

### Adding Platform Support

Update `PrepareTask.cs` to handle new architecture:
```csharp
case "myplatform":
    projectName = GetProjectName(path);
    defines = "__MYPLATFORM__";
    shaderBackend = "custom_backend";
    break;
```

## See Also

- [BUILD_SYSTEM.md](BUILD_SYSTEM.md) - Native library build system
- [QUICK_BUILD.md](QUICK_BUILD.md) - Quick build reference
- [WEBASSEMBLY_BROWSER_GUIDE.md](WEBASSEMBLY_BROWSER_GUIDE.md) - Web deployment guide
- [ANDROID_PROPERTIES.md](ANDROID_PROPERTIES.md) - Android configuration
- [IOS_PROPERTIES.md](IOS_PROPERTIES.md) - iOS configuration

## Troubleshooting

### Prepare Task Not Working

1. **Check SokolApplicationBuilder builds:**
   ```bash
   cd tools/SokolApplicationBuilder
   dotnet build
   ```

2. **Verify project structure:**
   - Project directory should contain `.csproj` and `.glsl` files
   - Project name should match directory name

3. **Check sokol-shdc availability:**
   ```bash
   # macOS
   ./tools/bin/osx/arm64/sokol-shdc --version
   
   # Windows
   .\tools\bin\win\sokol-shdc.exe --version
   ```

### VS Code Integration Issues

1. **Task not found error:**
   - Verify task label in `tasks.json` matches `preLaunchTask` in `launch.json`
   - Task labels must be literal strings (e.g., `"prepare-cube"`)

2. **Double prompts:**
   - Ensure both `launch.json` and `tasks.json` use the same `${input:exampleName}` variable
   - Task label must match: `"prepare-${input:exampleName}"` → resolves to `"prepare-cube"`

3. **Debugger not attaching:**
   - Verify `program` path in `launch.json` points to built `.dll`
   - Check that prepare task completed successfully
   - Ensure build output is in Debug configuration

## Command Reference

### Quick Commands

```bash
# Prepare for desktop debugging
dotnet run --project tools/SokolApplicationBuilder -- \
    --task prepare --architecture desktop --path examples/cube

# Prepare for web debugging
dotnet run --project tools/SokolApplicationBuilder -- \
    --task prepare --architecture web --path examples/cube

# Build release desktop app
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --type release --architecture desktop \
    --rid osx-arm64 --path examples/cube

# Build and install Android app
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --type release --architecture android \
    --path examples/cube --install --interactive

# Register tool globally
dotnet run --project tools/SokolApplicationBuilder -- --task register
```

## Performance Notes

- **Prepare task**: Typically completes in 1-3 seconds
- **Shader compilation**: Usually < 1 second per shader
- **Project building**: Depends on project size, typically 1-2 seconds for examples
- **Incremental builds**: Much faster when code hasn't changed

## Version History

- **Current**: Added PrepareTask for streamlined debug workflow
- **Previous**: Separate shader compilation and build tasks
- **Legacy**: Manual shader compilation required

## Contributing

When adding new functionality:
1. Follow existing task patterns in `Source/` directory
2. Use `Utils.RunShellCommand()` for consistent output formatting
3. Add comprehensive error handling with clear messages
4. Update this documentation
5. Test on all target platforms
