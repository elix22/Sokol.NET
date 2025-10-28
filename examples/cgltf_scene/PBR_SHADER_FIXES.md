# PBR Shader Fixes for Standard glTF Files

## Problems Identified

Your original shader was designed for **custom glTF files with modified textures**, not standard glTF 2.0 files like `DamagedHelmet.glb`. This caused three critical issues:

### 1. **Wrong Texture Channel Mapping** ❌
**Original (WRONG):**
- Roughness from **Alpha channel** (`.a`)
- Metallic from **Red channel** (`.r`)

**Fixed (glTF 2.0 Spec):**
- Roughness from **Green channel** (`.g`)
- Metallic from **Blue channel** (`.b`)

### 2. **Missing Image-Based Lighting (IBL)** ❌
Your shader only had:
- 1 point light (directional lighting)
- 10% flat ambient (unrealistic)

This is NOT sufficient for PBR! Mac Preview and Godot use **environment maps** to light the scene realistically.

**Fixed:** Added approximated IBL with:
- Hemisphere lighting (sky + ground bounce)
- Fresnel-based reflection
- Roughness-dependent specular highlights
- Much brighter lighting values (3x-4x multipliers)

### 3. **Incorrect Base Color Fallback** ❌
**Original:** Used `base_color_factor` as fallback
**Fixed:** Use white (`vec4(1.0)`) as fallback, then multiply by factor

## Changes Made

### File: `cgltf-sapp.glsl`

#### Change 1: Fixed Metallic/Roughness Channel Reading
```glsl
// BEFORE (Wrong):
perceptual_roughness *= mr_sample.a; // Alpha channel
metallic *= mr_sample.r;              // Red channel

// AFTER (Correct for glTF 2.0):
perceptual_roughness *= mr_sample.g; // GREEN channel = roughness
metallic *= mr_sample.b;              // BLUE channel = metallic
```

#### Change 2: Added Proper IBL Lighting
```glsl
// BEFORE (Insufficient):
vec3 ambient = base_color.rgb * 0.1; // Just 10% flat ambient
color += ambient;

// AFTER (Proper PBR IBL):
// 1. Hemisphere lighting (sky + ground)
vec3 skyColor = vec3(0.6, 0.8, 1.0) * 3.0;      // Bright blue
vec3 groundColor = vec3(0.4, 0.35, 0.3) * 1.5;   // Warm bounce
vec3 iblAmbient = mix(groundColor, skyColor, skyAmount);

// 2. Fresnel-based diffuse/specular split
vec3 F = fresnel_calculation(...);
vec3 diffuseIBL = (1.0 - F) * diffuse_color * iblAmbient;
vec3 specularIBL = F * reflectionColor * roughnessFactor;

// 3. Combine (IBL is now the MAIN light source)
color += diffuseIBL + specularIBL;
```

#### Change 3: Fixed Base Color Handling
```glsl
// BEFORE:
vec4 base_color = has_base_color_tex > 0.5
    ? srgb_to_linear(texture(...))
    : base_color_factor;  // Wrong fallback

// AFTER:
vec4 base_color = has_base_color_tex > 0.5
    ? srgb_to_linear(texture(...))
    : vec4(1.0);  // White fallback
base_color *= base_color_factor;  // Then apply factor
```

## Why This Matters

### glTF 2.0 Specification
The glTF format has a **strict specification** for PBR materials:
- **occlusionTexture**: Red channel only
- **metallicRoughnessTexture**: Blue (metallic) + Green (roughness)
- **normalTexture**: RGB in tangent space
- **baseColorTexture**: RGBA in sRGB

Your original shader followed a **custom convention** that worked with modified textures but breaks with standard glTF files.

### PBR Requires Environment Lighting
Physically-Based Rendering (PBR) models how light interacts with materials in the real world. In reality:
- Objects are lit by **all directions** (sky, ground, surrounding objects)
- Metallic surfaces **reflect their environment**
- Rough surfaces **scatter light**, smooth surfaces **mirror** it

A single point light + flat ambient cannot reproduce this. That's why Mac Preview and Godot look so much better—they use **environment maps** (HDR images of surroundings) to light the scene.

## Results

After these changes, your shader should now:
- ✅ Correctly read standard glTF 2.0 texture channels
- ✅ Provide realistic lighting with IBL approximation
- ✅ Show proper metallic/rough material response
- ✅ Look much closer to Mac Preview and Godot

## Further Improvements (Optional)

For even better results, consider:
1. **Real IBL**: Load actual environment maps (HDR cubemaps)
2. **Pre-filtered specular**: Generate mipmap chain for glossy reflections
3. **BRDF LUT**: Use lookup texture for split-sum approximation
4. **Multiple lights**: Support more than one point light

These are more complex but would match professional PBR renderers.

## Testing

Rebuild and run:
```bash
# Compile shaders
dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture desktop --path examples/cgltf_scene

# Build and run
dotnet build examples/cgltf_scene/cgltf_scene.csproj
dotnet run --project examples/cgltf_scene/cgltf_scene.csproj
```

The DamagedHelmet should now show:
- Visible surface details (not pure black)
- Metallic highlights on metal parts
- Rough/worn areas with diffuse scattering
- Proper color and texture visibility
