# IBL Environment Map Workflow

## Performance Warning

⚠️ **Runtime panorama-to-cubemap conversion is SLOW (10-15 seconds per HDR/EXR file)**

This is unavoidable due to the computational complexity:
- **Diffuse Irradiance**: 64×64×6 faces × 256 samples/pixel = ~6 million ray samples
- **Specular GGX**: 256×256×6 × 8 mips × 128+ samples/pixel = ~40 million ray samples

Even with native C++ and OpenMP parallelization, this takes significant time.

## Recommended Workflow

### 1. **Offline Pre-Filtering** (Industry Standard)

Use offline tools to pre-filter panoramas once, then load the results instantly:

#### Tools:
- **[cmftStudio](https://github.com/dariomanesku/cmftStudio)** - Free, cross-platform, excellent quality
- **[IBLBaker](https://github.com/derkreature/IBLBaker)** - Command-line, automated workflow
- **[glTF-IBL-Sampler](https://github.com/KhronosGroup/glTF-IBL-Sampler)** - Official Khronos tool

#### Process:
```bash
# Example using cmftStudio:
1. Load your HDR/EXR panorama
2. Set diffuse size: 64×64
3. Set specular size: 256×256 (8 mip levels)
4. Export as:
   - 6 EXR files for diffuse (PX, NX, PY, NY, PZ, NZ)
   - 6 EXR files per mip for specular (48 files total)
```

### 2. **Runtime Conversion** (Current Implementation)

**Use only for:**
- Quick prototyping
- Testing different environments
- When you can't pre-filter offline

**Current Performance:**
- Desktop (Apple M1/M2): ~12 seconds
- Desktop (Intel/AMD): ~15-20 seconds  
- WebAssembly: **30-60+ seconds** (no multi-threading)

## Implementation Status

### ✅ Currently Working
- Runtime EXR panorama loading
- Native C++ panorama-to-cubemap conversion
- Diffuse irradiance (cosine-weighted hemisphere)
- Specular GGX (importance sampling, roughness-based)

### 🚧 TODO: Pre-Filtered Cubemap Loading
```csharp
// Load pre-filtered cubemaps directly (instant loading)
LoadPreFilteredCubemap(
    diffuseFaces: new[] { "diffuse_px.exr", "diffuse_nx.exr", ... },
    specularMips: new[] {
        new[] { "specular_mip0_px.exr", ... }, // Mip 0: 256×256
        new[] { "specular_mip1_px.exr", ... }, // Mip 1: 128×128
        // ... 8 mip levels total
    }
);
```

## Industry Examples

All modern engines use pre-filtered cubemaps:

| Engine | Approach |
|--------|----------|
| **Unity** | Skybox baking + Reflection Probes (pre-filtered) |
| **Unreal** | Reflection Capture Actors (pre-filtered) |
| **glTF Viewer** | Pre-filtered KTX2 cubemaps from glTF-IBL-Sampler |
| **Godot** | ReflectionProbe + baked cubemaps |

## File Sizes

**Panorama (Source):**
- HDR (RGBE): 2-6 MB (1024×512)
- EXR (float): 6-12 MB (1024×512)

**Pre-Filtered Cubemaps:**
- Diffuse (64×64×6): ~100 KB (EXR)
- Specular (256×256×6×8): ~10-15 MB (EXR with mips)
- Total: ~15 MB (one-time storage, instant loading)

**Runtime Savings:**
- Pre-filtered: **Instant load** (< 100ms GPU upload)
- Runtime conversion: **10-15 seconds** CPU processing

## Migration Path

1. **Phase 1** (Current): Runtime conversion for testing
2. **Phase 2** (Next): Add `LoadPreFilteredCubemap()` support
3. **Phase 3** (Future): Provide pre-filtered cubemaps for all sample environments

## References

- [Khronos glTF IBL Specification](https://github.com/KhronosGroup/glTF/tree/master/extensions/2.0/Khronos/KHR_lights_punctual)
- [Real Shading in Unreal Engine 4](https://blog.selfshadow.com/publications/s2013-shading-course/karis/s2013_pbs_epic_notes_v2.pdf)
- [Image Based Lighting (LearnOpenGL)](https://learnopengl.com/PBR/IBL/Diffuse-irradiance)
