# Quick Reference: Creating New Projects

## Create a new project from template

### macOS / Linux
```bash
# In examples folder
./scripts/create-project-from-template.sh myproject

# In custom location
./scripts/create-project-from-template.sh myproject /path/to/folder

# Add to VS Code config
./scripts/add-project-to-vscode-config.sh myproject
```

### Windows
```powershell
# In examples folder
.\scripts\create-project-from-template.ps1 myproject

# In custom location
.\scripts\create-project-from-template.ps1 myproject C:\Path\To\Folder

# Add to VS Code config
.\scripts\add-project-to-vscode-config.ps1 myproject
```

## Build and Run

```bash
# Desktop
dotnet build examples/myproject/myproject.csproj
dotnet run --project examples/myproject/myproject.csproj

# Web
dotnet build examples/myproject/myprojectWeb.csproj
python scripts/serve-web-example.py examples/myproject

# Android
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --type release --architecture android \
    --install --interactive --path examples/myproject

# iOS
dotnet run --project tools/SokolApplicationBuilder -- \
    --task build --type release --architecture ios \
    --install --interactive --orientation landscape \
    --path examples/myproject
```

## VS Code Integration

After adding to config with the script:

- **Build**: `Ctrl+Shift+B` (Windows/Linux) or `Cmd+Shift+B` (macOS)
- **Debug Desktop**: `F5` → Select "Desktop (Sokol)" → Choose project
- **Debug Web**: `F5` → Select "Browser (Sokol)" → Choose project

## Project Structure

```
myproject/
├── myproject.csproj          # Desktop/Mobile
├── myprojectWeb.csproj       # WebAssembly
├── Source/
│   ├── Program.cs           # Entry points
│   └── myproject-app.cs     # Main app logic
├── Assets/                   # Images, data files
├── shaders/                  # Shader files
└── wwwroot/                  # Web files
```

## Full documentation

See [PROJECT_TEMPLATE.md](../docs/PROJECT_TEMPLATE.md) for complete documentation.
