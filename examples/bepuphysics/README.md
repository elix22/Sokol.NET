# BepuPhysics Example

A high-performance 3D physics simulation demo using [BepuPhysics v2](https://github.com/bepu/bepuphysics2) integrated with Sokol.NET. This example demonstrates real-time physics simulation with thousands of dynamic rigid bodies rendered using GPU instancing.

## Features

- Real-time physics simulation with up to 512K instances
- Dynamic spawning of spheres and cubes
- GPU instancing for efficient rendering
- Interactive camera controls
- ImGui-based statistics and controls
- Cross-platform support (Desktop, Web, iOS, Android)

## Performance Notes

Due to the computational intensity of physics simulations with thousands of bodies, **Release mode is strongly recommended** for optimal performance across all platforms.

### Desktop

For best performance on Desktop platforms (Windows, macOS, Linux), run in **Release mode**:

```bash
dotnet run --project bepuphysics.csproj -c Release
```

Running in Debug mode will result in significantly slower physics updates and lower frame rates.

### iOS/Android

Mobile platforms also benefit greatly from Release mode compilation. Use the appropriate build tasks configured in the project, ensuring Release configuration is selected.

**Recommended:** Always build and install mobile versions in Release mode for acceptable performance.

### Web (WebAssembly)

The Web version experiences performance limitations even when compiled with optimizations. To build the WebAssembly version:

```bash
dotnet publish bepuphysicsWeb.csproj
```

**Note:** Physics simulation on Web is considerably slower than native platforms, even with AOT compilation. Expect reduced frame rates and potentially fewer simultaneous physics bodies. This is due to WebAssembly's inherent performance characteristics compared to native code.

## Controls

- **Mouse drag**: Rotate camera
- **Mouse wheel**: Zoom in/out
- **ImGui UI**: Toggle statistics, adjust physics parameters

## Building

### Desktop (Quick Run)
```bash
# Debug mode (slower)
dotnet run --project bepuphysics.csproj

# Release mode (recommended)
dotnet run --project bepuphysics.csproj -c Release
```

### Web
```bash
# Publish NativeAOT WebAssembly build , 
dotnet publish bepuphysicsWeb.csproj

# Serve from wwwroot directory
```

### iOS/Android
Use the provided VS Code tasks or SokolApplicationBuilder tool with Release configuration:
```bash
dotnet run --project ../../tools/SokolApplicationBuilder -- \
    --task build \
    --type release \
    --architecture [ios|android] \
    --path ./
```

## Technical Details

- **Physics Engine**: BepuPhysics v2
- **Renderer**: Sokol.NET (sokol_gfx)
- **UI**: Dear ImGui via cimgui
- **Instance Limit**: 512,000 simultaneous rigid bodies
- **Rendering**: GPU instancing for both cubes and spheres


## Known Limitations

1. **Web Performance**: WebAssembly performance is significantly slower than native platforms
2. **Physics Complexity**: Large numbers of colliding bodies can impact frame rate
3. **Memory**: High instance counts require substantial memory allocation

## Related Documentation

See the main [Sokol.NET documentation](../../docs/README.md) for:
- Build system details
- Platform-specific configuration
- Shader compilation
- Project structure
