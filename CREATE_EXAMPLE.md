# Creating New Examples from Template

## Overview

Sokol.NET provides a convenient way to create new example projects based on a template. This allows you to quickly start developing new examples with the correct project structure.

## Using VS Code Tasks

### Quick Start

1. Press `Cmd+Shift+P` (macOS) or `Ctrl+Shift+P` (Windows/Linux)
2. Type "Tasks: Run Task"
3. Select "Create New Example"
4. Enter the name for your new example when prompted
5. The new example will be created in the `examples/` folder

### Naming Rules

- Name must start with a letter
- Can contain only letters, numbers, and underscores
- Examples: `my_example`, `test123`, `cool_graphics_demo`
- Invalid examples: `123test` (starts with number), `my-example` (contains hyphen)

## Using Command Line

You can also create examples directly from the command line:

```bash
dotnet run --project tools/SokolApplicationBuilder -- --task create --project <example_name>
```

Example:
```bash
dotnet run --project tools/SokolApplicationBuilder -- --task create --project my_example
```

## Automatic Configuration

When you create a new example, the tool automatically:

1. **Creates project structure** from the template
2. **Adds to `Sokol.NET.sln`** - Both desktop and web project files
3. **Updates `.vscode/launch.json`** - Adds debug configurations for Desktop and Browser
4. **Updates `.vscode/tasks.json`** - Adds prepare tasks for Desktop, Web, Android, and iOS

This means you can immediately start debugging and building your new example using VS Code!

## What Gets Created

The template will create a new example with the following structure:

```
examples/my_example/
├── Source/
│   ├── Program.cs              # Entry point for all platforms
│   └── my_example-app.cs       # Main application logic
├── shaders/                    # Place your GLSL shaders here
├── Assets/                     # Place your textures, models, etc.
├── wwwroot/                    # Web-specific files
├── my_example.csproj          # Desktop project file
├── my_exampleWeb.csproj       # Web project file
├── Directory.Build.props
├── Directory.Build.targets
└── runtimeconfig.template.json
```

## Next Steps After Creation

After creating a new example, the tool will show you next steps:

1. **Add shaders**: Place your `.glsl` shader files in the `shaders/` folder
2. **Add assets**: Place textures, models, and other resources in the `Assets/` folder
3. **Edit application**: Modify `Source/my_example-app.cs` with your application logic

### Running and Debugging

Since your project is now configured in VS Code, you can easily run and debug:

**Using VS Code (Recommended):**
1. Press `F5` or go to Run → Start Debugging
2. Select `Desktop (Sokol)` or `Browser (Sokol)`
3. Choose your new example from the list
4. The project will automatically build and launch

**Using Command Line:**
```bash
# Desktop
dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture desktop --path examples/my_example

# Web
dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture web --path examples/my_example

# Android
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture android --type release --path examples/my_example --install

# iOS
dotnet run --project tools/SokolApplicationBuilder -- --task build --architecture ios --type release --path examples/my_example --install
```

## Template Structure

The template (`templates/template_example/`) provides:

- Basic Sokol.NET application setup
- Platform entry points (Desktop, Android, iOS, Web)
- Simple initialization and frame callback
- Proper project configuration for all platforms

## Error Handling

If an example with the same name already exists, you'll get an error message:
```
ERROR: Example 'my_example' already exists at '/path/to/examples/my_example'
```

In this case, either choose a different name or delete the existing example first.

## Customization

After creation, you can customize:

1. **Class names**: The template automatically renames `TemplateApp` to `YourExampleApp` (PascalCase)
2. **Project files**: All `.csproj` files are renamed to match your example name
3. **Build configuration**: Modify `Directory.Build.props` for custom build settings

## Tips

- Start simple with the template and gradually add complexity
- Look at existing examples in the `examples/` folder for inspiration
- Use the shader compilation guide in `docs/SHADER_GUIDE.md` for shader development
- Test on desktop first before building for other platforms

## Deleting an Example

⚠️ **WARNING: This operation permanently deletes the example and CANNOT be undone!**

To delete an example you've created:

### Via VS Code Task (Recommended)

1. Press `Cmd+Shift+P` (macOS) or `Ctrl+Shift+P` (Windows/Linux)
2. Type "Tasks: Run Task"
3. Select **"Delete Example"**
4. Choose the example to delete from the dropdown
5. **Confirm by typing the exact example name** when prompted

### Via Command Line

```bash
dotnet run --project tools/SokolApplicationBuilder -- --task delete --project example_name
```

You will be prompted to confirm the deletion by typing the example name. This safety measure prevents accidental deletions.

### What Gets Deleted

When you delete an example, the following are permanently removed:

- ✗ Project folder: `examples/example_name/`
- ✗ Solution entries in `Sokol.NET.sln`
- ✗ Launch configuration in `.vscode/launch.json`
- ✗ Prepare tasks in `.vscode/tasks.json`
- ✗ Input options in `.vscode/tasks.json`

**There is no undo operation.** Make sure you have backups of any important code before deleting an example.
