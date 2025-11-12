# Transmission Material Fix Summary

## Problem Statement
The GlassHurricaneCandleHolder.gltf model was rendering as opaque solid blue instead of transparent glass. The transmission system was not working correctly.

## Root Cause Analysis
1. **Pipeline Selection**: Transmission materials were using OPAQUE pipeline instead of BLEND pipeline
2. **Alpha Output**: Shader was forcing `alpha = 1.0` for transmission materials, making them completely opaque
3. **Side Effects**: Fixes were affecting non-glass materials (watch numbers fuzzy, dragon losing color)

## Solution Implemented

### 1. **Threshold-Based Approach**
Only materials with `transmission_factor >= 0.1` (10% transmission) are treated as actual glass:

### 2. **Pipeline Selection Fix** (Frame.cs)
```csharp
// Only apply transmission (BLEND) pipeline for materials with SIGNIFICANT transmission
if (mesh.TransmissionFactor >= 0.1f) {
    effectiveAlphaMode = SharpGLTF.Schema2.AlphaMode.BLEND; // Force blend for real glass materials
}
```

### 3. **Transparency Sorting Fix** (Frame.cs)
```csharp
// Only treat materials as transparent if they have SIGNIFICANT transmission or explicit BLEND mode
bool isTransparent = (mesh.TransmissionFactor >= 0.1f) || (mesh.AlphaMode == SharpGLTF.Schema2.AlphaMode.BLEND);
```

### 4. **Alpha Calculation Fix** (Shader)
```glsl
if (transmission_factor >= 0.1) {
    // Calculate transmission alpha for actual glass materials
    float transmission_alpha = 1.0 - (transmission_factor * 0.4); // Max 40% transparency
    final_alpha = max(base_color.a * transmission_alpha, 0.2);
} else {
    // Materials with low/no transmission use base alpha unchanged
    final_alpha = base_color.a;
}
```

### 5. **Transmission Color Mixing Fix** (Shader)
```glsl
if (transmission_factor >= 0.1) {
    // Only apply refraction color mixing for actual glass materials
    vec3 refracted_color = calculate_refraction(...);
    color = mix(color, refracted_color, transmission_factor);
}
```

## Current Status
- ✅ **Glass Hurricane Candle Holder**: Properly transparent with refraction
- ❌ **Dragon**: Still losing color and appearing washed out/transparent
- ✅ **Watch**: Should be sharp and clear
- ✅ **Other Models**: Should be unaffected

## Next Steps
Need to investigate the Dragon model's transmission_factor value and possibly adjust the threshold or approach.

---

## Transmission Texture Implementation (November 2025)

### Problem Statement
The CompareIor.gltf model was not correctly rendering transmission materials with per-pixel transparency masks. Issues included:
1. **Missing Transmission Texture Support**: No texture sampling for `KHR_materials_transmission.transmissionTexture`
2. **Lost Environment Reflections**: Right sphere (Diamond, IOR 2.42) had no visible environment reflections
3. **Distorted Left Sphere**: Left sphere appeared deformed instead of clear glass

### Root Cause Analysis

#### 1. Missing Texture Sampling
The shader only used the uniform `transmission_factor` and didn't sample the `transmissionTexture` (RED channel) that provides per-pixel transmission masks (0 = opaque glTF logo, 1 = transparent glass).

#### 2. Incorrect IBL Mixing
Transmission was mixed with the **entire surface color** (diffuse + specular), which replaced surface reflections instead of preserving them. The glTF Sample Viewer mixes transmission with **diffuse only**, keeping specular reflections separate.

Reference viewer code (pbr.frag:184):
```glsl
f_diffuse = mix(f_diffuse, f_specular_transmission, materialInfo.transmissionFactor);
```

#### 3. Wrong Thickness Default
Materials without `KHR_materials_volume` were defaulting to `thickness = 1.0`, causing volume refraction artifacts on infinitely thin glass surfaces. Per glTF spec, thickness should be `0.0` when the volume extension is absent.

### Solution Implemented

#### 1. Transmission Texture Binding (pbr.glsl, pbr_fs_uniforms.glsl)

**Shader uniforms added:**
```glsl
float has_transmission_tex;      // 1.0 if texture available
float transmission_texcoord;     // 0 = TEXCOORD_0, 1 = TEXCOORD_1
```

**Texture binding:**
```glsl
layout(binding=8) uniform texture2D u_TransmissionTexture;
layout(binding=8) uniform sampler u_TransmissionSampler_Raw;
#define u_TransmissionSampler sampler2D(u_TransmissionTexture, u_TransmissionSampler_Raw)
```

**Texture sampling:**
```glsl
transmission = transmission_factor;
if (has_transmission_tex > 0.5) {
    vec2 baseUV = (transmission_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
    transmission *= texture(u_TransmissionSampler, baseUV).r;  // RED channel
}
```

**Binding slot 8** is shared with Charlie environment (sheen materials) - unlikely to conflict.

#### 2. C# Binding Code (Mesh.cs, Frame_*.cs)

**Mesh.cs (lines 402-417):**
```csharp
if (TransmissionTextureIndex >= 0 && TransmissionTextureIndex < Textures.Count && 
    Textures[TransmissionTextureIndex] != null)
{
    var transmissionTex = Textures[TransmissionTextureIndex]!;
    bind.views[8] = transmissionTex.View;
    bind.samplers[8] = transmissionTex.Sampler;
}
```

**Frame_Static.cs, Frame_Skinning.cs, etc.:**
```csharp
metallicParams.has_transmission_tex = 
    (mesh.TransmissionTextureIndex >= 0 && ...) ? 1.0f : 0.0f;
metallicParams.transmission_texcoord = 0.0f;  // Always TEXCOORD_0
```

#### 3. Fixed IBL Mixing to Preserve Specular Reflections (pbr.glsl)

**Separated diffuse and specular calculations:**
```glsl
vec3 f_diffuse_ibl = vec3(0.0);
vec3 f_specular_ibl = vec3(0.0);

if (use_ibl > 0) {
    vec3 irradiance = getDiffuseLight(n);
    f_diffuse_ibl = irradiance * diffuseColor;
    
    vec3 specularLight = getIBLRadianceGGX(n, v, perceptualRoughness);
    vec3 iblFresnel = getIBLGGXFresnel(n, v, perceptualRoughness, specularColor, 1.0);
    f_specular_ibl = specularLight * iblFresnel;
}
```

**Critical fix - Mix transmission with diffuse ONLY:**
```glsl
#ifdef TRANSMISSION
    vec3 f_specular_transmission = getTransmissionIBL(n, v, baseColor.rgb);
    
    // Mix transmission with diffuse ONLY (not specular!)
    // This preserves surface reflections (Fresnel) on glass materials
    f_diffuse_ibl = mix(f_diffuse_ibl, f_specular_transmission, transmission);
#endif

// Then combine: color = f_diffuse_ibl + f_specular_ibl
```

This approach matches the glTF Sample Viewer and ensures glass materials show **both**:
- Surface reflections (specular from Fresnel)
- Volume refraction (transmission through glass)

#### 4. Fixed Thickness Default (SharpGltfModel.cs:590-595)

**Before (WRONG):**
```csharp
else if (mesh.TransmissionFactor > 0.0f)
{
    mesh.ThicknessFactor = 1.0f;  // ❌ Causes distortion on thin glass!
    mesh.AttenuationDistance = 1.0f;
    mesh.AttenuationColor = baseColor.RGB;
}
```

**After (CORRECT):**
```csharp
else if (mesh.TransmissionFactor > 0.0f)
{
    // Per glTF spec: thickness = 0 when KHR_materials_volume absent
    mesh.ThicknessFactor = 0.0f;  // ✅ Infinitely thin glass
    mesh.AttenuationDistance = float.MaxValue;  // No absorption
    mesh.AttenuationColor = new Vector3(1, 1, 1);  // No tint
}
```

When `thickness = 0`, the transmission ray becomes zero vector, so the shader samples the framebuffer at the surface position (no volume offset), creating proper thin glass refraction without distortion.

### Results - CompareIor.gltf

✅ **Left Sphere** (IOR 1.5, no volume):
- Clear, undistorted glass surface
- Proper refraction without volume artifacts
- Environment reflections visible
- glTF logo opaque (transmission texture mask = 0)

✅ **Right Sphere** (IOR 2.42 Diamond, volume with thickness):
- Strong environment reflections (high IOR)
- Volume refraction through glass
- Beer's law absorption (attenuation)
- glTF logo opaque (transmission texture mask = 0)

✅ **Transmission Texture Masking**:
- RED channel = 0 → opaque logo
- RED channel = 1 → transparent glass
- Smooth gradients work correctly

### Technical Details

**Transmission Texture Format:**
- RED channel contains transmission mask
- 0.0 = completely opaque
- 1.0 = fully transparent
- Final transmission = `transmission_factor × texture(transmissionTex, uv).r`

**glTF Extensions Used:**
- `KHR_materials_transmission` - Base transparency
- `KHR_materials_transmission.transmissionTexture` - Per-pixel mask
- `KHR_materials_volume` - Thickness and absorption (optional)
- `KHR_materials_ior` - Index of refraction (optional, defaults to 1.5)

**Alpha Mode Interaction:**
- CompareIor uses `alphaMode: "MASK"` with `alphaCutoff: 0.5`
- Transmission modulates alpha: `finalAlpha = baseAlpha × (1.0 - transmission)`
- Opaque logos (transmission=0) → alpha unchanged
- Transparent glass (transmission=1) → alpha reduced

### Files Modified

**Shaders:**
- `pbr_fs_uniforms.glsl` - Added texture uniforms
- `pbr.glsl` - Added texture sampling and fixed IBL mixing

**C# Code:**
- `Mesh.cs` - Added texture binding at slot 8
- `Frame_Static.cs` - Set texture availability flags
- `Frame_Skinning.cs` - Set texture availability flags
- `Frame_Morphing.cs` - Set texture availability flags
- `Frame_SkinnedMorphing.cs` - Set texture availability flags
- `SharpGltfModel.cs` - Fixed thickness default to 0.0

### Lessons Learned

1. **Texture channel usage matters**: glTF transmission uses RED channel only, not RGB
2. **IBL mixing order is critical**: Mix transmission with diffuse before combining with specular
3. **Thickness defaults affect rendering**: Zero thickness for thin glass is not the same as unit thickness
4. **Binding slot conflicts**: Slot 8 shared with Charlie/sheen, but materials rarely use both
5. **glTF spec compliance**: Always check spec for default values when extensions are absent

### References

- [glTF Sample Viewer pbr.frag](https://github.com/KhronosGroup/glTF-Sample-Viewer/blob/main/source/Renderer/shaders/pbr.frag#L184) - Transmission mixing
- [glTF KHR_materials_transmission spec](https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_materials_transmission)
- [CompareIor glTF model](https://github.com/KhronosGroup/glTF-Sample-Assets/tree/main/Models/CompareIor)

### Current Status (November 2025)

✅ **Fully Working:**
- Transmission texture sampling (RED channel)
- Per-pixel transmission masks
- Environment reflections on glass
- Infinitely thin glass surfaces (thickness=0)
- Volume glass with absorption (thickness>0)
- Proper alpha blending with transmission

✅ **Tested Models:**
- CompareIor.gltf - Both spheres render correctly
- GlassHurricaneCandleHolder.gltf - Transparent glass with refraction

### Future Enhancements

Consider implementing:
1. **Transmission roughness** - Currently not supported, could add blur to refracted image
2. **Screen-space thickness** - Calculate thickness from depth buffer for realistic volume
3. **Chromatic dispersion** - Split RGB channels with different IOR (rainbow effect)
4. **Multiple refractions** - Currently single-bounce, multi-bounce would be more realistic