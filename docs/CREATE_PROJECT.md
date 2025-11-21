# Creating a New Sokol.NET Project

This guide explains how to create a standalone Sokol.NET project outside of the repository using the built-in project template.

## Overview

The `Create New Project` task allows you to create a fully-configured Sokol.NET project anywhere on your filesystem. Unlike examples (which live inside the repository), projects are independent applications that you can develop and distribute separately.

## Quick Start

### Using Visual Studio Code

1. Open the Sokol.NET repository in VS Code
2. Open **Command Palette** (`Cmd+Shift+P` on macOS, `Ctrl+Shift+P` on Windows/Linux)
3. Select **Tasks: Run Task**
4. Choose **Create New Project**
5. Enter your project name (e.g., `my_game`)
6. Enter destination path (e.g., `/Users/yourname/Projects` or `C:\Users\yourname\Projects`)

The project will be created with all necessary configuration files and can be opened independently in VS Code.

### Using Command Line

```bash
cd Sokol.NET

# Create a new project
dotnet run --project tools/SokolApplicationBuilder -- \
  --task createproject \
  --project my_game \
  --destination /path/to/projects
```

This creates `/path/to/projects/my_game/` with a complete project structure.

## Project Structure

The created project contains:

```
my_game/
├── .vscode/
│   ├── launch.json       # Configured for Desktop and Browser debugging
│   ├── tasks.json        # Build and prepare tasks
│   └── settings.json     # VS Code settings
├── Assets/               # Place your assets here (images, models, etc.)
├── shaders/              # GLSL shader files
├── Source/
│   ├── my_game-app.cs   # Your main application code
│   ├── Program.cs        # Entry point
│   ├── imgui/           # ImGui bindings (copied from Sokol.NET/src/imgui)
│   └── sokol/           # Sokol API bindings (copied from Sokol.NET/src/sokol)
├── wwwroot/              # Web assets (for WebAssembly)
├── Directory.Build.props # Build configuration
├── my_game.csproj       # Desktop project file
└── my_gameWeb.csproj    # WebAssembly project file
```

> **Note**: The `imgui/` and `sokol/` folders are automatically copied from the Sokol.NET repository's `src` directory during project creation. This ensures your project always uses the latest API bindings from the repository.

## Requirements

### Project Name
- Must start with a letter
- Can contain only letters, numbers, and underscores
- Examples: `my_game`, `CoolApp`, `MyProject2024`

### Destination Path
- Must be an **existing directory**
- Must be **outside** the Sokol.NET repository
- Cannot already contain a project with the same name

## Developing Your Project

### 1. Open the Project

```bash
cd /path/to/projects/my_game
code .
```

### 2. Run Desktop Version

Press **F5** in VS Code and select **Desktop (Sokol)**.

Or via command line:
```bash
dotnet build my_game.csproj
dotnet run -p my_game.csproj
```

### 3. Run Browser Version

Press **F5** in VS Code and select **Browser (Sokol)**, then choose a port.

Or via command line:
```bash
# Build WebAssembly version
dotnet build my_gameWeb.csproj

# Serve the application (requires dotnet-serve or similar)
dotnet run --project $(cat ~/.sokolnet_config/sokolnet_home)/tools/dotnet-serve/src/dotnet-serve/dotnet-serve.csproj -- \
  -d bin/Debug/net8.0/browser-wasm/AppBundle -p 8080 \
  -h 'Cross-Origin-Opener-Policy: same-origin' \
  -h 'Cross-Origin-Embedder-Policy: require-corp'
```

### 4. Build for Android

```bash
# From Sokol.NET repository
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture android --type release \
  --path /path/to/projects/my_game --install --interactive
```

### 5. Build for iOS

```bash
# From Sokol.NET repository
dotnet run --project tools/SokolApplicationBuilder -- \
  --task build --architecture ios --type release \
  --path /path/to/projects/my_game --install --interactive
```

## Adding Shaders

1. Create shader files in `shaders/` folder:
   - `my_shader.glsl` for unified GLSL (recommended)
   - Or separate `my_shader.vert` and `my_shader.frag`

2. Shaders are automatically compiled during build using `sokol-shdc`

3. Include generated C# files in your application:
   ```csharp
   using static MyShader;
   ```

See [SHADER_GUIDE.md](SHADER_GUIDE.md) for detailed shader documentation.

## Customizing Your Project

### Application Configuration

Edit `Directory.Build.props` to configure:
- Application version
- Package name/identifier
- Android/iOS specific settings
- Target frameworks

Example:
```xml
<PropertyGroup>
  <AppVersion>1.0.0</AppVersion>
  <ApplicationTitle>My Awesome Game</ApplicationTitle>
  <ApplicationId>com.yourcompany.mygame</ApplicationId>
</PropertyGroup>
```

### Adding NuGet Packages

**Add NuGet packages to `Directory.Build.props`** in an `<ItemGroup>` section:
```xml
<ItemGroup>
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  <PackageReference Include="YourPackage" Version="1.0.0" />
</ItemGroup>
```

This ensures the packages are available across all project configurations (Desktop, Web, Android, iOS).

### Adding Native Libraries

**For Native Libraries** (Assimp, Spine, Ozz), see example projects in the Sokol.NET repository:
- `examples/assimp_simple` - Assimp configuration
- `examples/spine_simple` - Spine configuration
- `examples/ozz_shdfeatures` - Ozz-animation configuration

### App Icons

See [APP_ICON.md](APP_ICON.md) and [ICON_QUICKSTART.md](ICON_QUICKSTART.md) for adding custom icons.

## Working with Multiple Projects

You can create multiple independent projects:

```bash
# Create a game
dotnet run --project tools/SokolApplicationBuilder -- \
  --task createproject --project my_game --destination ~/Projects

# Create a tool
dotnet run --project tools/SokolApplicationBuilder -- \
  --task createproject --project model_viewer --destination ~/Projects

# Create another project
dotnet run --project tools/SokolApplicationBuilder -- \
  --task createproject --project particle_editor --destination ~/Tools
```

Each project is completely independent with its own:
- Version control (can initialize its own git repo)
- Dependencies and configuration
- Build outputs
- Assets and resources

## Differences: Projects vs Examples

| Feature | Examples | Projects |
|---------|----------|----------|
| **Location** | Inside `examples/` folder | Anywhere on your filesystem |
| **Purpose** | Demonstrate features | Build your own applications |
| **Repository** | Part of Sokol.NET repo | Independent (can have own git repo) |
| **Solution** | Added to `Sokol.NET.sln` | Standalone |
| **Updates** | Updated with Sokol.NET | Independent versioning |
| **Distribution** | View-only | Full ownership and distribution |

## Troubleshooting

### "Destination path cannot be inside the Sokol.NET repository"

Ensure your destination is outside the repository:
```bash
# ❌ Wrong - inside repository
--destination ~/Sokol.NET/my_projects

# ✅ Correct - outside repository
--destination ~/Projects
--destination ~/MyProjects
--destination /Users/yourname/Development/Games
```

### "Project already exists"

Choose a different project name or delete the existing project:
```bash
rm -rf /path/to/projects/my_game
```

### "Template directory not found"

Ensure you've run the register script:
```bash
./register.sh  # macOS/Linux
# or
register.bat   # Windows
```

### Build Errors

Make sure the Sokol.NET repository is properly set up:
```bash
cd ~/Sokol.NET  # or wherever you cloned Sokol.NET
git submodule update --init --recursive
```

## Next Steps

After creating your project:

1. **Explore the template code** in `Source/my_game-app.cs`
2. **Add your assets** to the `Assets/` folder
3. **Write shaders** in the `shaders/` folder
4. **Customize the build** in `Directory.Build.props`
5. **Check out examples** in the Sokol.NET repository for inspiration

## Related Documentation

- [Creating Examples](CREATE_EXAMPLE.md) - Creating examples within the repository
- [Shader Guide](SHADER_GUIDE.md) - Writing cross-platform shaders
- [VS Code Run Guide](VSCODE_RUN_GUIDE.md) - Running applications
- [Build System](BUILD_SYSTEM.md) - Understanding the build system
- [Project Template](PROJECT_TEMPLATE.md) - Template structure details

---

**Happy coding!** 🚀
