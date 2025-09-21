# WebAssembly Local Server & VS Code Integration

This guide describes how to run and debug WebAssembly examples in your browser using the integrated Node.js server and VS Code launch configurations.

## Dependencies

### Required
- **Node.js** (v12+ recommended, v18+ preferred)
  - Download: https://nodejs.org
  - Works on Windows, macOS, Linux
- **.NET 8.0+** (for building WebAssembly examples)
  - Download: https://dotnet.microsoft.com
- **VS Code** (for launch configurations)
  - Download: https://code.visualstudio.com

### No Additional npm Packages Required
- The server uses only built-in Node.js modules: `http`, `fs`, `path`, `net`, `child_process`

## Installation Instructions

### Windows
1. Download and install Node.js from [nodejs.org](https://nodejs.org)
2. Download and install .NET 8.0+ from [dotnet.microsoft.com](https://dotnet.microsoft.com)
3. Install VS Code from [code.visualstudio.com](https://code.visualstudio.com)
4. No further setup required

### macOS
1. Install Node.js (Homebrew: `brew install node` or download from [nodejs.org](https://nodejs.org))
2. Install .NET 8.0+ from [dotnet.microsoft.com](https://dotnet.microsoft.com)
3. Install VS Code from [code.visualstudio.com](https://code.visualstudio.com)
4. No further setup required

### Linux
1. Install Node.js (`sudo apt install nodejs npm` or use your distro's package manager)
2. Install .NET 8.0+ from [dotnet.microsoft.com](https://dotnet.microsoft.com)
3. Install VS Code from [code.visualstudio.com](https://code.visualstudio.com)
4. No further setup required

## How It Works

- The Node.js server script (`scripts/serve-web-browser.js`) serves the built WebAssembly example from the correct `wwwroot` directory.
- The server automatically detects your platform and opens the browser:
  - Windows: `start`
  - macOS: `open`
  - Linux: `xdg-open`
- Handles port conflicts and provides proper HTTP headers for WebAssembly and media files.
- Supports range requests for video/audio streaming.

## VS Code Launch Configurations

- Launch configs are defined in `.vscode/launch.json`.
- Select any `* (Browser)` configuration and press F5 to build and launch the example in your browser.
- No manual server management required.

## Troubleshooting
- If you see `EADDRINUSE`, another server is running on the same port. The script will automatically try the next available port.
- If the browser does not open, check that Node.js is installed and available in your PATH.

## More Info
See this guide in the workspace docs folder:
`docs/web-server-setup.md`

For general project setup, see the main README in the root folder.

---
**Quick Links:**
- [Node.js Download](https://nodejs.org)
- [.NET Download](https://dotnet.microsoft.com)
- [VS Code Download](https://code.visualstudio.com)
