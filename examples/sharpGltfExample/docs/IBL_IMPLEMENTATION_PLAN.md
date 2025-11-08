# IBL (Image-Based Lighting) Implementation Plan

## Executive Summary

This document provides a detailed plan for implementing Image-Based Lighting (IBL) in the sharpGltfExample project, based on careful analysis of the Khronos glTF-Sample-Viewer reference implementation.

**Goal:** Achieve physically accurate environment lighting using pre-filtered environment maps for both diffuse (irradiance) and specular (radiance) components, matching the quality of the reference viewer.

---

## Table of Contents

1. [IBL Overview](#ibl-overview)
2. [Reference Architecture Analysis](#reference-architecture-analysis)
3. [Required Assets](#required-assets)
4. [Implementation Phases](#implementation-phases)
5. [Technical Details](#technical-details)
6. [Testing Strategy](#testing-strategy)
7. [Performance Considerations](#performance-considerations)

---

## IBL Overview

### What is IBL?

Image-Based Lighting uses environment maps (cubemaps) to provide realistic ambient lighting from all directions. Instead of relying solely on directional/point lights, IBL captures the full lighting environment.

### Components

1. **Diffuse/Lambertian IBL**: Provides ambient indirect lighting (irradiance)
2. **Specular/GGX IBL**: Provides environment reflections based on roughness (radiance)
3. **BRDF LUT**: Lookup table for split-sum approximation (precomputed Fresnel/geometry terms)
4. **Sheen IBL** (optional): Environment lighting for fabric/sheen materials (Charlie distribution)

### Why IBL?

- **Realism**: Captures real-world lighting environments
- **Ambient Occlusion**: Natural shadowing in crevices
- **Reflections**: Proper metallic reflections that match the environment
- **Energy Conservation**: Physically accurate light distribution

---

## Reference Architecture Analysis

### From glTF-Sample-Renderer (JavaScript Reference)

#### 1. **Environment Map Structure**

Located in: `source/gltf/image_based_light.js`

```javascript
class ImageBasedLight {
    constructor() {
        this.specularEnvironmentTexture = undefined;  // GGX filtered cubemap (mipmapped)
        this.diffuseEnvironmentTexture = undefined;   // Lambertian irradiance cubemap
        this.sheenEnvironmentTexture = undefined;     // Charlie filtered cubemap (for sheen)
        this.levelCount = 1;                          // Mipmap levels
        this.iblIntensityScale = 1.0;                 // Brightness multiplier
        this.rotation = [0, 0, 0, 1];                 // Quaternion rotation
    }
}
```

**Key Insight**: Separate cubemaps for different BRDF components.

#### 2. **IBL Sampling (GLSL)**

Located in: `source/Renderer/shaders/ibl.glsl`

```glsl
// Diffuse contribution
vec3 getDiffuseLight(vec3 n) {
    vec4 textureSample = texture(u_LambertianEnvSampler, u_EnvRotation * n);
    textureSample.rgb *= u_EnvIntensity;
    return textureSample.rgb;
}

// Specular contribution with LOD based on roughness
vec4 getSpecularSample(vec3 reflection, float lod) {
    vec4 textureSample = textureLod(u_GGXEnvSampler, u_EnvRotation * reflection, lod);
    textureSample.rgb *= u_EnvIntensity;
    return textureSample;
}

// Split-sum approximation for GGX
vec3 getIBLGGXFresnel(vec3 n, vec3 v, float roughness, vec3 F0, float specularWeight) {
    float NdotV = clampedDot(n, v);
    vec2 brdfSamplePoint = clamp(vec2(NdotV, roughness), vec2(0.0), vec2(1.0));
    vec2 f_ab = texture(u_GGXLUT, brdfSamplePoint).rg;
    
    // Schlick fresnel approximation
    vec3 Fr = max(vec3(1.0 - roughness), F0) - F0;
    vec3 k_S = F0 + Fr * pow(1.0 - NdotV, 5.0);
    vec3 FssEss = specularWeight * (k_S * f_ab.x + f_ab.y);
    
    // Multiple scattering (Fdez-Aguera)
    float Ems = (1.0 - (f_ab.x + f_ab.y));
    vec3 F_avg = specularWeight * (F0 + (1.0 - F0) / 21.0);
    vec3 FmsEms = Ems * FssEss * F_avg / (1.0 - F_avg * Ems);
    
    return FssEss + FmsEms;
}

// Main specular IBL function
vec3 getIBLRadianceGGX(vec3 n, vec3 v, float roughness) {
    float lod = roughness * float(u_MipCount - 1);
    vec3 reflection = normalize(reflect(-v, n));
    vec4 specularSample = getSpecularSample(reflection, lod);
    return specularSample.rgb;
}
```

**Key Insights**:
- Roughness selects mip level for specular reflections (rough = blurry)
- Split-sum approximation separates environment lookup from BRDF evaluation
- Multiple scattering compensation improves energy conservation

#### 3. **Integration in PBR Shader**

Located in: `source/Renderer/shaders/pbr.frag` (lines 162-228)

```glsl
// Calculate IBL contributions
f_diffuse = getDiffuseLight(n) * baseColor.rgb;

// Specular for both dielectric and metallic
f_specular_metal = getIBLRadianceGGX(n, v, materialInfo.perceptualRoughness);
f_specular_dielectric = f_specular_metal;

// Apply Fresnel mixing
vec3 f_metal_fresnel_ibl = getIBLGGXFresnel(n, v, materialInfo.perceptualRoughness, 
                                             baseColor.rgb, 1.0);
f_metal_brdf_ibl = f_metal_fresnel_ibl * f_specular_metal;

vec3 f_dielectric_fresnel_ibl = getIBLGGXFresnel(n, v, materialInfo.perceptualRoughness, 
                                                   materialInfo.f0_dielectric, 
                                                   materialInfo.specularWeight);
f_dielectric_brdf_ibl = mix(f_diffuse, f_specular_dielectric, f_dielectric_fresnel_ibl);

// Final mixing based on metallic
color = mix(f_dielectric_brdf_ibl, f_metal_brdf_ibl, materialInfo.metallic);

// Apply ambient occlusion
#ifdef HAS_OCCLUSION_MAP
    float ao = texture(u_OcclusionSampler, getOcclusionUV()).r;
    color = color * (1.0 + u_OcclusionStrength * (ao - 1.0));
#endif
```

**Key Insights**:
- Separate calculations for metallic vs dielectric materials
- Fresnel term controls diffuse/specular balance
- AO modulates final IBL result

#### 4. **Texture Binding**

Located in: `source/Renderer/renderer.js` (lines 1299-1343)

```javascript
applyEnvironmentMap(state, texSlotOffset) {
    const environment = state.environment;
    
    // Diffuse (Lambertian) irradiance map
    this.webGl.setTexture(
        this.shader.getUniformLocation("u_LambertianEnvSampler"),
        environment,
        environment.diffuseEnvMap,
        texSlotOffset++
    );
    
    // Specular (GGX) radiance map (mipmapped)
    this.webGl.setTexture(
        this.shader.getUniformLocation("u_GGXEnvSampler"),
        environment,
        environment.specularEnvMap,
        texSlotOffset++
    );
    
    // GGX BRDF lookup table
    this.webGl.setTexture(
        this.shader.getUniformLocation("u_GGXLUT"),
        environment,
        environment.lut,
        texSlotOffset++
    );
    
    // Sheen (Charlie) environment map (optional)
    this.webGl.setTexture(
        this.shader.getUniformLocation("u_CharlieEnvSampler"),
        environment,
        environment.sheenEnvMap,
        texSlotOffset++
    );
    
    // Charlie BRDF LUT
    this.webGl.setTexture(
        this.shader.getUniformLocation("u_CharlieLUT"),
        environment,
        environment.sheenLUT,
        texSlotOffset++
    );
    
    // Uniforms
    this.shader.updateUniform("u_MipCount", environment.mipCount);
    this.shader.updateUniform("u_EnvRotation", rotMatrix3);
    this.shader.updateUniform("u_EnvIntensity", envIntensity);
    
    return texSlotOffset;
}
```

**Key Insights**:
- 5 texture samplers for complete IBL system
- Mip count determines roughness LOD range
- Environment rotation allows interactive adjustment

#### 5. **IBL Generation Pipeline**

Located in: `source/ibl_sampler.js`

The reference viewer includes a complete pipeline to generate IBL maps from HDR panoramas:

```javascript
class iblSampler {
    constructor(view) {
        this.textureSize = 256;              // Cubemap face resolution
        this.ggxSampleCount = 1024;          // Samples for specular filtering
        this.lambertianSampleCount = 2048;   // Samples for diffuse filtering
        this.lutResolution = 1024;           // BRDF LUT resolution
        this.mipmapCount = undefined;        // Calculated based on texture size
    }
    
    // Main generation steps:
    // 1. panoramaToCubemap()    - Convert equirectangular HDR to cubemap
    // 2. sampleDiffuse()        - Convolve for irradiance
    // 3. sampleSpecular()       - Pre-filter for GGX at each mip level
    // 4. sampleGGXLut()         - Generate BRDF integration LUT
    // 5. sampleCharlieLut()     - Generate Charlie BRDF LUT
}
```

**Process Overview**:
1. Load HDR panorama (equirectangular)
2. Convert to cubemap (6 faces)
3. Generate diffuse irradiance map (heavy convolution)
4. Generate specular radiance mipchain (pre-filter per roughness level)
5. Generate 2D BRDF lookup tables

---

## Required Assets

### 1. Environment Cubemaps

You need **pre-filtered** environment maps. These are NOT raw HDR images.

#### Option A: Use Existing Assets

The reference viewer uses assets from [glTF-Sample-Assets](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Environments):

**Example Structure**:
```
environments/
├── papermill/
│   ├── diffuse/
│   │   ├── diffuse_face0.ktx2  (or .jpg for cubemap faces)
│   │   ├── diffuse_face1.ktx2
│   │   ... (6 faces)
│   ├── specular/
│   │   └── specular.ktx2       (cubemap with mipmaps)
│   └── lut_ggx.png             (BRDF lookup table)
```

**Recommended Environments**:
- `papermill` - Outdoor industrial setting
- `footprint_court` - Indoor court
- `pisa` - Classic outdoor environment
- `helipad` - Neutral outdoor

**Format**: KTX2 with basis compression (optimal) or individual cubemap faces as PNG/JPG

#### Option B: Generate Your Own

Use tools like:
- **IBLBaker** (Unity): https://github.com/derkreature/IBLBaker
- **cmftStudio**: https://github.com/dariomanesku/cmftStudio
- **Khronos glTF-IBL-Sampler**: https://github.com/KhronosGroup/glTF-IBL-Sampler

From HDR panoramas (`.hdr` files) from:
- **HDRIHaven**: https://polyhaven.com/hdris
- **sIBL Archive**: http://www.hdrlabs.com/sibl/archive.html

### 2. BRDF Lookup Tables

**GGX BRDF LUT** (1024x1024):
- X-axis: `NdotV` (0 to 1)
- Y-axis: `roughness` (0 to 1)  
- R channel: Scale factor for F0
- G channel: Bias factor

**Charlie BRDF LUT** (optional, for sheen):
- Same structure as GGX
- Different distribution (fabric/velvet)

**Generation**: The reference viewer generates these at runtime. For C#, you can:
1. Pre-generate and load as PNG (recommended)
2. Port the generation code (compute shader or CPU)

**Download**: https://github.com/KhronosGroup/glTF-Sample-Viewer/tree/main/assets/images

---

## Implementation Phases

### Phase 1: Asset Preparation (Week 1)

#### Step 1.1: Download Reference Assets

```bash
# Create assets directory
mkdir -p Assets/environments/papermill

# Download from glTF-Sample-Assets or use existing reference viewer assets
# - diffuse cubemap (6 faces or single ktx2)
# - specular cubemap with mipmaps
# - lut_ggx.png (1024x1024)
# - lut_charlie.png (optional)
```

**Files Needed**:
- `papermill_diffuse_*.jpg` (6 faces) OR `papermill_diffuse.ktx2`
- `papermill_specular.ktx2` (with mipmaps)
- `lut_ggx.png`
- `lut_charlie.png` (optional)

#### Step 1.2: Verify Asset Format

**Check**:
- Diffuse map: Single mip level, lower resolution (e.g., 128x128 per face)
- Specular map: Multiple mip levels (e.g., 256x256 down to 4x4)
- LUT: 2D texture, RG or RGB format

**Tools**: 
- Use image viewer with mip level inspection
- Or write simple loader to verify dimensions

#### Step 1.3: Convert to Sokol-Compatible Format

If using KTX2, you may need to:
1. Extract to individual faces (use `ktx` command line tool)
2. Convert to PNG/JPG for simplicity
3. Or implement KTX2 loading (more complex)

**Recommended**: Start with PNG/JPG for easier debugging.

---

### Phase 2: Asset Loading Infrastructure (Week 1-2)

#### Step 2.1: Create EnvironmentMap Class

**File**: `Source/EnvironmentMap.cs`

```csharp
using System.Numerics;
using Sokol;
using static Sokol.SG;

public class EnvironmentMap
{
    // Cubemap textures
    public sg_image DiffuseCubemap { get; private set; }
    public sg_image SpecularCubemap { get; private set; }
    public sg_image SheenCubemap { get; private set; }  // Optional
    
    // BRDF lookup tables (2D textures)
    public sg_image GGX_LUT { get; private set; }
    public sg_image Charlie_LUT { get; private set; }  // Optional
    
    // Views for texture sampling
    public sg_view DiffuseCubemapView { get; private set; }
    public sg_view SpecularCubemapView { get; private set; }
    public sg_view SheenCubemapView { get; private set; }
    public sg_view GGX_LUTView { get; private set; }
    public sg_view Charlie_LUTView { get; private set; }
    
    // Samplers
    public sg_sampler CubemapSampler { get; private set; }
    public sg_sampler LUTSampler { get; private set; }
    
    // Properties
    public int MipCount { get; private set; }
    public float Intensity { get; set; } = 1.0f;
    public Matrix4x4 Rotation { get; set; } = Matrix4x4.Identity;
    
    public bool IsLoaded => DiffuseCubemap.id != 0 && SpecularCubemap.id != 0;
    
    public void Dispose()
    {
        if (DiffuseCubemap.id != 0) sg_destroy_image(DiffuseCubemap);
        if (SpecularCubemap.id != 0) sg_destroy_image(SpecularCubemap);
        if (SheenCubemap.id != 0) sg_destroy_image(SheenCubemap);
        if (GGX_LUT.id != 0) sg_destroy_image(GGX_LUT);
        if (Charlie_LUT.id != 0) sg_destroy_image(Charlie_LUT);
        
        // Destroy views and samplers...
    }
}
```

#### Step 2.2: Implement Cubemap Loader

**File**: `Source/EnvironmentMapLoader.cs`

```csharp
public static class EnvironmentMapLoader
{
    /// <summary>
    /// Load environment map from individual cubemap face files
    /// </summary>
    public static unsafe EnvironmentMap LoadFromFiles(
        string diffusePattern,   // e.g., "papermill_diffuse_{0}.png" (0-5)
        string specularMipPattern, // e.g., "papermill_specular_mip{0}_{1}.png" (mip, face)
        string ggxLutPath,
        string charlieLutPath = null)
    {
        var envMap = new EnvironmentMap();
        
        // 1. Load diffuse cubemap (single mip)
        envMap.DiffuseCubemap = LoadCubemap(diffusePattern, 1);
        
        // 2. Load specular cubemap (multiple mips)
        envMap.SpecularCubemap = LoadCubemapMipmapped(specularMipPattern, out int mipCount);
        envMap.MipCount = mipCount;
        
        // 3. Load BRDF LUTs (2D textures)
        envMap.GGX_LUT = LoadTexture2D(ggxLutPath);
        if (charlieLutPath != null)
            envMap.Charlie_LUT = LoadTexture2D(charlieLutPath);
        
        // 4. Create views
        envMap.DiffuseCubemapView = CreateCubemapView(envMap.DiffuseCubemap);
        envMap.SpecularCubemapView = CreateCubemapView(envMap.SpecularCubemap);
        envMap.GGX_LUTView = CreateTextureView(envMap.GGX_LUT);
        
        // 5. Create samplers
        envMap.CubemapSampler = CreateCubemapSampler();
        envMap.LUTSampler = CreateLUTSampler();
        
        return envMap;
    }
    
    private static unsafe sg_image LoadCubemap(string pattern, int mipLevels)
    {
        // Load 6 faces (+X, -X, +Y, -Y, +Z, -Z)
        // Standard cubemap ordering
        
        var desc = new sg_image_desc
        {
            type = SG_IMAGETYPE_CUBE,
            width = 0,  // Will be set after loading first face
            height = 0,
            num_slices = 6,
            num_mipmaps = mipLevels,
            pixel_format = SG_PIXELFORMAT_RGBA8,
            usage = SG_USAGE_IMMUTABLE,
            label = "ibl-diffuse-cubemap"
        };
        
        // Load each face
        for (int face = 0; face < 6; face++)
        {
            string path = string.Format(pattern, face);
            var imageData = LoadImageFile(path, out int width, out int height);
            
            if (face == 0)
            {
                desc.width = width;
                desc.height = height;
            }
            
            // Copy to desc.data.subimage[mip][face]
            // ... (implementation details)
        }
        
        return sg_make_image(desc);
    }
    
    private static sg_sampler CreateCubemapSampler()
    {
        return sg_make_sampler(new sg_sampler_desc
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            mipmap_filter = SG_FILTER_LINEAR,
            wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            wrap_v = SG_WRAP_CLAMP_TO_EDGE,
            wrap_w = SG_WRAP_CLAMP_TO_EDGE,
            label = "ibl-cubemap-sampler"
        });
    }
}
```

**Helper**: Use `StbImage` or similar for PNG/JPG loading:

```csharp
using StbImageSharp;

private static byte[] LoadImageFile(string path, out int width, out int height)
{
    ImageResult image = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);
    width = image.Width;
    height = image.Height;
    return image.Data;
}
```

#### Step 2.3: Integrate into Main State

**File**: `Source/Main.cs`

```csharp
public static unsafe partial class SharpGLTFApp
{
    public struct State
    {
        // ... existing fields ...
        
        // IBL
        public EnvironmentMap environmentMap;
        public bool useIBL;
        public float iblIntensity;
        public float iblRotationDegrees;
    }
    
    private static void InitEnvironment()
    {
        state.useIBL = true;
        state.iblIntensity = 1.0f;
        state.iblRotationDegrees = 0.0f;
        
        try
        {
            state.environmentMap = EnvironmentMapLoader.LoadFromFiles(
                "Assets/environments/papermill/diffuse_{0}.png",
                "Assets/environments/papermill/specular_mip{0}_{1}.png",
                "Assets/environments/papermill/lut_ggx.png",
                "Assets/environments/papermill/lut_charlie.png"
            );
            
            Info("[IBL] Environment map loaded successfully");
            Info($"[IBL] Mip count: {state.environmentMap.MipCount}");
        }
        catch (Exception ex)
        {
            Error($"[IBL] Failed to load environment: {ex.Message}");
            state.useIBL = false;
        }
    }
}
```

---

### Phase 3: Shader Integration (Week 2-3)

#### Step 3.1: Update Shader Uniforms

**File**: `shaders/pbr_vs_uniforms.glsl`

No changes needed for vertex shader.

**File**: `shaders/pbr_fs_uniforms.glsl`

```glsl
// Add after existing uniforms

// IBL uniforms
uniform float u_EnvIntensity;
uniform float u_EnvBlurNormalized;  // Optional blur for skybox
uniform int u_MipCount;
uniform mat4 u_EnvRotation;         // 3x3 rotation matrix (stored as mat4)

// IBL texture samplers (combined image+sampler in Sokol)
uniform samplerCube u_GGXEnvSampler;        // Binding 5
uniform samplerCube u_LambertianEnvSampler;  // Binding 6
uniform sampler2D u_GGXLUT;                  // Binding 7

// Optional sheen IBL (disable if texture slots conflict with morphing)
#ifndef MORPHING
uniform samplerCube u_CharlieEnvSampler;    // Binding 8
uniform sampler2D u_CharlieLUT;              // Binding 9
#endif
```

**Note**: Texture bindings must match those set in C# code.

#### Step 3.2: Update IBL Shader Code

**File**: `shaders/ibl.glsl`

Your existing `ibl.glsl` already has most functions. Verify it matches reference implementation:

```glsl
// Key functions (already present in your code):
// - getDiffuseLight(vec3 n)
// - getSpecularSample(vec3 reflection, float lod)
// - getIBLGGXFresnel(...)
// - getIBLRadianceGGX(...)
// - getIBLRadianceCharlie(...) - optional sheen
```

**Action**: Compare your `ibl.glsl` line-by-line with reference to ensure identical behavior.

#### Step 3.3: Integrate IBL into PBR Fragment Shader

**File**: `shaders/pbr.glsl`

Locate the lighting calculation section (after material info is computed):

```glsl
// After material setup, add:

#ifdef USE_IBL
    // Diffuse IBL contribution
    vec3 f_diffuse_ibl = getDiffuseLight(normal) * baseColor.rgb;
    
    // Specular IBL contribution
    vec3 f_specular_ibl = getIBLRadianceGGX(normal, viewDir, perceptualRoughness);
    
    // Fresnel for metallic surfaces
    vec3 f_metal_fresnel = getIBLGGXFresnel(
        normal, viewDir, perceptualRoughness, baseColor.rgb, 1.0
    );
    vec3 f_metal_brdf_ibl = f_metal_fresnel * f_specular_ibl;
    
    // Fresnel for dielectric surfaces
    vec3 f0_dielectric = vec3(0.04); // Standard dielectric F0
    vec3 f_dielectric_fresnel = getIBLGGXFresnel(
        normal, viewDir, perceptualRoughness, f0_dielectric, 1.0
    );
    vec3 f_dielectric_brdf_ibl = mix(f_diffuse_ibl, f_specular_ibl, f_dielectric_fresnel);
    
    // Mix based on metallic factor
    vec3 ibl_contribution = mix(f_dielectric_brdf_ibl, f_metal_brdf_ibl, metallic);
    
    // Apply ambient occlusion
    #ifdef HAS_OCCLUSION_MAP
        float ao = texture(u_OcclusionSampler, uv).r;
        ibl_contribution *= mix(1.0, ao, u_OcclusionStrength);
    #endif
    
    // Add to final color
    finalColor.rgb += ibl_contribution;
#endif
```

**Integration Point**: Add this after direct lighting calculations but before tone mapping.

#### Step 3.4: Add Shader Compilation Flag

**File**: `shaders/pbr.glsl` (top of file)

```glsl
// Configuration defines (set by C# code)
// #define USE_IBL 1        // Enable image-based lighting
// #define MORPHING 1       // Morph targets (conflicts with sheen IBL texture slots)
```

**C# Side**: When compiling shaders, add `USE_IBL 1` define if environment is loaded.

---

### Phase 4: C# Rendering Integration (Week 3)

#### Step 4.1: Update Shader Generation

**File**: `shaders/pbr.glsl` (shader compilation)

Add shader variant for IBL:

```bash
# Compile with IBL support
sokol-shdc --input pbr.glsl --output pbr-shader.cs --slang glsl430:hlsl5:metal_macos \
    --format sokol --defines "USE_IBL=1"
```

Or dynamically define in your build script.

#### Step 4.2: Bind IBL Textures

**File**: `Source/Mesh.cs` (or wherever you bind textures)

```csharp
private static unsafe void ApplyIBLTextures(ref sg_bindings bind)
{
    if (!state.useIBL || state.environmentMap == null || !state.environmentMap.IsLoaded)
    {
        // Bind default white cubemaps (already implemented)
        EnsureDefaultCubemap();
        bind.views[5] = _defaultWhiteCubemapView;
        bind.samplers[5] = _defaultCubemapSampler;
        bind.views[6] = _defaultWhiteCubemapView;
        bind.samplers[6] = _defaultCubemapSampler;
        bind.views[7] = _defaultWhiteTextureView;
        bind.samplers[7] = _defaultSampler;
        return;
    }
    
    var env = state.environmentMap;
    
    // Binding 5: GGX specular cubemap
    bind.views[5] = env.SpecularCubemapView;
    bind.samplers[5] = env.CubemapSampler;
    
    // Binding 6: Lambertian diffuse cubemap
    bind.views[6] = env.DiffuseCubemapView;
    bind.samplers[6] = env.CubemapSampler;
    
    // Binding 7: GGX BRDF LUT
    bind.views[7] = env.GGX_LUTView;
    bind.samplers[7] = env.LUTSampler;
    
    // Bindings 8-9: Sheen IBL (optional, skip if morphing)
    #ifndef MORPHING
    if (env.SheenCubemap.id != 0)
    {
        bind.views[8] = env.SheenCubemapView;
        bind.samplers[8] = env.CubemapSampler;
        bind.views[9] = env.Charlie_LUTView;
        bind.samplers[9] = env.LUTSampler;
    }
    #endif
}
```

**Update Existing Binding Code**:

In your mesh rendering, replace:

```csharp
// OLD:
bind.views[5] = _defaultWhiteCubemapView;
bind.samplers[5] = _defaultCubemapSampler;
bind.views[6] = _defaultWhiteCubemapView;
bind.samplers[6] = _defaultCubemapSampler;

// NEW:
ApplyIBLTextures(ref bind);
```

#### Step 4.3: Set IBL Uniforms

**File**: `Source/Frame.cs` (in rendering loop)

```csharp
// IBL parameters (after camera/model uniforms)
if (state.useIBL && state.environmentMap != null)
{
    // Create IBL uniform struct
    var iblParams = new ibl_params_t
    {
        u_EnvIntensity = state.iblIntensity,
        u_EnvBlurNormalized = 0.0f,  // 0 = sharp, 1 = maximum blur
        u_MipCount = state.environmentMap.MipCount,
        u_EnvRotation = CreateRotationMatrix(state.iblRotationDegrees)
    };
    
    sg_apply_uniforms(UB_ibl_params, SG_RANGE(ref iblParams));
}
else
{
    // Set defaults (no IBL)
    var iblParams = new ibl_params_t
    {
        u_EnvIntensity = 0.0f,
        u_MipCount = 1,
        u_EnvRotation = Matrix4x4.Identity
    };
    sg_apply_uniforms(UB_ibl_params, SG_RANGE(ref iblParams));
}

// Rendering flags (add USE_IBL)
var flags = new rendering_flags_t
{
    use_ibl = state.useIBL ? 1 : 0,
    // ... other flags
};
sg_apply_uniforms(UB_rendering_flags, SG_RANGE(ref flags));
```

**Helper Function**:

```csharp
private static Matrix4x4 CreateRotationMatrix(float degrees)
{
    float radians = degrees * (MathF.PI / 180.0f);
    return Matrix4x4.CreateRotationY(radians);
}
```

---

### Phase 5: UI Controls (Week 3)

#### Step 5.1: Add IBL Section to ImGui

**File**: `Source/UI.cs` (or equivalent)

```csharp
private static unsafe void RenderIBLControls()
{
    if (ImGui.CollapsingHeader("Image-Based Lighting (IBL)"))
    {
        ImGui.Checkbox("Enable IBL", ref state.useIBL);
        
        if (state.useIBL && state.environmentMap != null)
        {
            ImGui.SliderFloat("Intensity", ref state.iblIntensity, 0.0f, 5.0f);
            ImGui.SliderFloat("Rotation", ref state.iblRotationDegrees, 0.0f, 360.0f);
            
            ImGui.Separator();
            ImGui.Text($"Mip Levels: {state.environmentMap.MipCount}");
            ImGui.Text($"Status: Loaded");
        }
        else if (state.useIBL)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), "Environment not loaded!");
        }
        
        if (ImGui.Button("Reload Environment"))
        {
            // Reload environment map
            if (state.environmentMap != null)
            {
                state.environmentMap.Dispose();
            }
            InitEnvironment();
        }
    }
}
```

---

### Phase 6: Testing & Debugging (Week 4)

#### Step 6.1: Visual Validation

**Test Models** (in order of complexity):

1. **MetalRoughSpheres.gltf**
   - Grid of spheres with varying metallic/roughness
   - Easiest to verify IBL correctness
   - Expected: Smooth gradient of reflections

2. **DamagedHelmet.gltf**
   - Mixed metallic/rough materials
   - Should show clear reflections on metal parts

3. **FlightHelmet.gltf**
   - Complex materials with AO
   - Verifies AO integration with IBL

4. **GlassVaseFlowers.gltf** (if transmission works)
   - Verifies IBL works with transmission

**Expected Results**:
- Metallic surfaces show clear environment reflections
- Rough surfaces show blurred reflections
- Dielectric surfaces have subtle reflections at grazing angles
- Overall lighting looks natural and matches reference viewer

#### Step 6.2: Debug Visualization

Add debug modes to visualize IBL components:

```glsl
// In fragment shader (for debugging)
#ifdef DEBUG_IBL_DIFFUSE
    finalColor.rgb = getDiffuseLight(normal);
    return;
#endif

#ifdef DEBUG_IBL_SPECULAR
    finalColor.rgb = getIBLRadianceGGX(normal, viewDir, perceptualRoughness);
    return;
#endif

#ifdef DEBUG_IBL_FRESNEL
    vec3 F = getIBLGGXFresnel(normal, viewDir, perceptualRoughness, vec3(0.04), 1.0);
    finalColor.rgb = F;
    return;
#endif
```

**UI Controls**:

```csharp
ImGui.Combo("Debug Mode", ref state.debugMode, 
    "Normal\0IBL Diffuse\0IBL Specular\0IBL Fresnel\0");
```

#### Step 6.3: Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| Black/dark rendering | IBL textures not bound | Verify texture binding indices match shader |
| Overly bright | Intensity too high or HDR not converted | Check `u_EnvIntensity`, ensure LDR range |
| No reflections on metal | Metallic parameter wrong | Verify material parsing |
| Blocky reflections | Wrong mip level | Check `u_MipCount`, verify mipmap generation |
| Upside-down reflections | Cubemap face order wrong | Check +X,-X,+Y,-Y,+Z,-Z ordering |
| Seams on cubemap | Sampler not clamping | Ensure `CLAMP_TO_EDGE` on cubemap sampler |

---

## Technical Details

### Cubemap Face Ordering (OpenGL Convention)

```
0: +X (right)
1: -X (left)
2: +Y (top)
3: -Y (bottom)
4: +Z (front)
5: -Z (back)
```

### Mipmap Chain Structure

For a 256x256 specular cubemap:

```
Mip 0: 256x256  (roughness 0.0 - sharp reflections)
Mip 1: 128x128  (roughness ~0.14)
Mip 2: 64x64    (roughness ~0.29)
Mip 3: 32x32    (roughness ~0.43)
Mip 4: 16x16    (roughness ~0.57)
Mip 5: 8x8      (roughness ~0.71)
Mip 6: 4x4      (roughness ~0.86)
Mip 7: 2x2      (roughness 1.0 - completely blurred)
```

LOD selection in shader:
```glsl
float lod = roughness * float(u_MipCount - 1);
```

### BRDF LUT Format

**Dimensions**: 1024x1024 (or 512x512 minimum)

**Channels**:
- R: Scale for F0 (Fresnel)
- G: Bias term
- B: Unused (or Charlie LUT)

**Lookup**:
```glsl
vec2 brdfSamplePoint = vec2(NdotV, roughness);
vec2 f_ab = texture(u_GGXLUT, brdfSamplePoint).rg;
```

**Split-Sum Approximation**:
```
F_r = (F0 * f_ab.x + f_ab.y) * ∫L_i(ω) dω
```

### Memory Requirements

Example for one environment:

```
Diffuse cubemap:  128x128 x 6 faces x 4 bytes (RGBA8) = 384 KB
Specular cubemap: 256x256 x 6 faces x 8 mips x 4 bytes = ~2.7 MB
GGX LUT:          1024x1024 x 2 channels x 1 byte = 2 MB
Charlie LUT:      1024x1024 x 1 channel x 1 byte = 1 MB
Total: ~6 MB per environment
```

**Optimization**: Use compressed formats (BC6H for HDR, BC1/BC3 for LDR) to reduce by 4-6x.

---

## Performance Considerations

### Texture Sampling Cost

**Per-Fragment Operations**:
- 1 diffuse cubemap sample
- 1 specular cubemap sample (with trilinear filtering)
- 1 BRDF LUT sample

**Optimization**:
- Use mipmaps (automatic LOD reduces bandwidth)
- Consider texture compression
- For mobile: reduce resolution (128x128 specular, 64x64 diffuse)

### Shader Complexity

IBL adds:
- ~30-40 ALU instructions per fragment
- 3 texture samples (not counting optional sheen)

**Optimization**:
- Disable IBL for distant/small objects (use distance-based LOD)
- Pre-multiply `u_EnvIntensity` into textures offline
- Skip multiple scattering term for mobile (set `FmsEms = 0`)

### Mobile Considerations

For iOS/Android:

```csharp
#if MOBILE
const int DIFFUSE_SIZE = 64;
const int SPECULAR_SIZE = 128;
const int MAX_MIPS = 6;
const int LUT_SIZE = 512;
#else
const int DIFFUSE_SIZE = 128;
const int SPECULAR_SIZE = 256;
const int MAX_MIPS = 8;
const int LUT_SIZE = 1024;
#endif
```

---

## Testing Strategy

### Unit Tests

1. **Asset Loading**
   - Verify cubemap dimensions
   - Check mip level count
   - Validate LUT dimensions

2. **Shader Compilation**
   - Ensure `USE_IBL` variant compiles
   - Verify uniform locations

3. **Texture Binding**
   - Check correct samplers bound
   - Verify texture slots don't conflict

### Integration Tests

1. **Visual Comparison**
   - Render `MetalRoughSpheres.gltf`
   - Compare side-by-side with reference viewer
   - Check histogram similarity

2. **Performance Benchmarks**
   - Measure frame time with/without IBL
   - Profile texture bandwidth
   - Test on mobile devices

### Regression Tests

1. **Save reference screenshots** with IBL enabled
2. **Automated comparison** after code changes
3. **Monitor for:**
   - Brightness changes
   - Color shifts
   - Missing reflections

---

## Deliverables Checklist

### Phase 1: Assets
- [ ] Download papermill environment
- [ ] Convert to compatible format
- [ ] Verify mip levels and dimensions

### Phase 2: Loading
- [ ] `EnvironmentMap.cs` class
- [ ] `EnvironmentMapLoader.cs` implementation
- [ ] Integration into `Main.cs`
- [ ] Test asset loading

### Phase 3: Shaders
- [ ] Update `pbr_fs_uniforms.glsl`
- [ ] Verify `ibl.glsl` matches reference
- [ ] Integrate IBL into `pbr.glsl`
- [ ] Compile shader variants

### Phase 4: Rendering
- [ ] Bind IBL textures in `Mesh.cs`
- [ ] Set IBL uniforms in `Frame.cs`
- [ ] Add `USE_IBL` rendering flag

### Phase 5: UI
- [ ] IBL controls panel
- [ ] Intensity/rotation sliders
- [ ] Debug visualization modes

### Phase 6: Testing
- [ ] Test with MetalRoughSpheres
- [ ] Compare with reference viewer
- [ ] Performance profiling
- [ ] Document any issues

---

## Success Criteria

✅ **Visual Quality**:
- Metallic surfaces show clear, accurate reflections
- Rough surfaces show appropriately blurred reflections
- Lighting matches reference viewer within 5% luminance

✅ **Performance**:
- Frame rate impact < 20% on desktop
- Frame rate impact < 30% on mobile
- No texture thrashing or stuttering

✅ **Compatibility**:
- Works on all platforms (Desktop, iOS, Android, WebAssembly)
- No conflicts with existing features (morphing, skinning, transmission)
- Degrades gracefully when disabled

---

## Future Enhancements

### Phase 7+: Advanced Features

1. **Multiple Environments**
   - Switch between environments at runtime
   - Crossfade between environments
   - Per-object environment override

2. **Real-Time IBL Generation**
   - Generate IBL from scene rendering (probe-based)
   - Update cubemaps dynamically

3. **Light Probe System**
   - Multiple light probes per scene
   - Blend between nearest probes
   - Automatic probe placement

4. **Optimization**
   - Spherical Harmonics for diffuse (cheaper than cubemap)
   - Prefiltered importance sampling
   - Temporal anti-aliasing for specular

---

## References

1. **Khronos glTF-Sample-Viewer**: https://github.com/KhronosGroup/glTF-Sample-Viewer
2. **glTF-Sample-Assets**: https://github.com/KhronosGroup/glTF-Sample-Assets
3. **glTF 2.0 Spec**: https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html
4. **Epic's Real Shading in UE4**: https://blog.selfshadow.com/publications/s2013-shading-course/
5. **Bruop's IBL Tutorial**: https://bruop.github.io/ibl/
6. **Filament PBR Guide**: https://google.github.io/filament/Filament.html

---

## Appendix: Quick Start Commands

```bash
# 1. Download assets
cd examples/sharpGltfExample/Assets
mkdir -p environments/papermill
# Download from glTF-Sample-Assets

# 2. Build shaders
cd ../../shaders
./compile.sh  # Or your build script

# 3. Build and run
cd ../..
dotnet build sharpGltfExample.csproj
./bin/Debug/sharpGltfExample

# 4. Test model
# Load MetalRoughSpheres.gltf
# Enable IBL in UI
# Adjust intensity slider
```

---

## Contact & Support

For questions or issues during implementation:
- Check reference viewer source code
- Review glTF forum: https://github.khronos.org/glTF-Forums/
- File issues at: [Your repo's issue tracker]

**Good luck with the implementation!** 🚀
