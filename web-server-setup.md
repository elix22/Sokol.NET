# WebAssembly Local Server & VS Code Integration

This guide describes how to run and debug WebAssembly examples in your browser using the custom dotnet-serve tool included with Sokol.NET.

## Prerequisites

- **.NET 10.0 SDK** or later
- **wasm-tools-net8 workload**: `dotnet workload install wasm-tools-net8`
- **VS Code** (recommended for launch configurations)

## Using dotnet-serve

Sokol.NET includes a custom build of [dotnet-serve](https://github.com/natemcmaster/dotnet-serve) located in `tools/dotnet-serve`. This lightweight HTTP server is specifically configured for serving WebAssembly applications with the proper headers and CORS settings.

### Manual Usage

After building a WebAssembly example, you can serve it manually:

```bash
# Navigate to the example's AppBundle directory
cd examples/cube/bin/Debug/net8.0/browser-wasm/AppBundle

# Run dotnet-serve from the tools directory
dotnet run --project /path/to/Sokol.NET/tools/dotnet-serve/src/dotnet-serve/dotnet-serve.csproj -- \
  -d . \
  -p 8080 \
  -h "Cross-Origin-Opener-Policy: same-origin" \
  -h "Cross-Origin-Embedder-Policy: require-corp" \
  -h "Cache-Control: no-store, no-cache, must-revalidate, max-age=0" \
  -h "Pragma: no-cache" \
  -o
```

The `-o` flag automatically opens your default browser.

### VS Code Integration (Recommended)

The easiest way to run WebAssembly examples is through VS Code:

1. **Open the project** in VS Code
2. **Press F5** or go to **Run and Debug** (Ctrl+Shift+D / Cmd+Shift+D)
3. Select **"Browser (Sokol)"** configuration
4. Choose the example you want to run
5. Specify a port (default: 8080)

VS Code will automatically:
- Build the WebAssembly example
- Start dotnet-serve with proper headers
- Serve from the correct AppBundle directory

### Required HTTP Headers

WebAssembly applications require specific headers for SharedArrayBuffer and threading support:

- `Cross-Origin-Opener-Policy: same-origin`
- `Cross-Origin-Embedder-Policy: require-corp`
- `Cache-Control: no-store, no-cache, must-revalidate, max-age=0` (for development)
- `Pragma: no-cache` (for development)

The dotnet-serve configuration in `.vscode/launch.json` automatically sets these headers.

## Building WebAssembly Examples

### Using VS Code Tasks

Use the **Command Palette** (Cmd+Shift+P / Ctrl+Shift+P) → **Tasks: Run Task** → Select a `prepare-<example>-web` task.

### Using Command Line

```bash
# Prepare (compile shaders and assets)
dotnet run --project tools/SokolApplicationBuilder -- \
  --task prepare --architecture web --path examples/cube

# Build
cd examples/cube
dotnet build cube.csproj
```

## Troubleshooting

### Port Already in Use
If port 8080 is already in use, specify a different port:
- In VS Code: You'll be prompted to enter a port number
- Command line: Change the `-p` parameter value

### Browser Security Errors
If you see errors related to SharedArrayBuffer or cross-origin isolation:
- Ensure the required headers are being sent (check browser DevTools → Network tab)
- Clear browser cache (especially after updating builds)
- Try incognito/private browsing mode

### Build Errors
- Make sure you've run the prepare task first (compiles shaders)
- Verify wasm-tools workload is installed: `dotnet workload list`
- Check that .NET 10.0 SDK is installed: `dotnet --version`

### Cache Issues
WebAssembly files can be aggressively cached by browsers. If changes aren't reflected:
- Use Ctrl+Shift+R (Cmd+Shift+R on macOS) for hard refresh
- Clear browser cache
- Use private/incognito browsing during development
- The development headers include cache-busting directives

## More Information

- **dotnet-serve Documentation**: See `tools/dotnet-serve/README.md`
- **WebAssembly Browser Guide**: See `docs/WEBASSEMBLY_BROWSER_GUIDE.md`
- **Browser Cache Issues**: See `docs/Browser-Cache-Issues.md`
- **Main Documentation**: See `docs/README.md`

---

**Quick Start**: Press F5 in VS Code, select "Browser (Sokol)", choose an example, and start debugging!
