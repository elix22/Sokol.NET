# glTF Sample Viewer - Transmission (Refraction) Implementation Deep Dive

## Overview

This document provides a comprehensive analysis of how the Khronos glTF Sample Viewer implements the **KHR_materials_transmission** extension, which enables realistic glass and transparent material rendering through refraction. The implementation follows the glTF 2.0 specification and demonstrates industry-standard techniques for handling light transmission through transparent surfaces.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Material Properties Setup](#material-properties-setup)
3. [Image-Based Lighting (IBL) Transmission](#image-based-lighting-ibl-transmission)
4. [Punctual Light Transmission](#punctual-light-transmission)
5. [Volume Attenuation (Beer's Law)](#volume-attenuation-beers-law)
6. [Coordinate Space Transformations](#coordinate-space-transformations)
7. [Dispersion Effect](#dispersion-effect)
8. [Key Implementation Details](#key-implementation-details)

---

## Architecture Overview

The transmission implementation is split across multiple shader modules:

- **`pbr.frag`**: Main fragment shader that orchestrates the rendering pipeline
- **`material_info.glsl`**: Structures and functions for gathering material properties
- **`ibl.glsl`**: Image-based lighting calculations, including `getIBLVolumeRefraction()`
- **`punctual.glsl`**: Punctual (direct) light calculations, including `getPunctualRadianceTransmission()`
- **`functions.glsl`**: Utility functions like `applyIorToRoughness()`
- **`brdf.glsl`**: BRDF functions (Fresnel, GGX distribution, etc.)

---

## Material Properties Setup

### MaterialInfo Structure

The `MaterialInfo` struct in `material_info.glsl` holds all transmission-related properties:

```glsl
struct MaterialInfo
{
    float ior;                    // Index of refraction (default: 1.5)
    float transmissionFactor;     // Transmission strength [0-1]
    float thickness;              // Material thickness in local space
    vec3 attenuationColor;        // Color tint for volume absorption
    float attenuationDistance;    // Distance for attenuation calculation
    float dispersion;             // Chromatic dispersion amount
    // ... other properties
};
```

### Property Loading

#### Transmission Factor

```glsl
MaterialInfo getTransmissionInfo(MaterialInfo info)
{
    info.transmissionFactor = u_TransmissionFactor;

#ifdef HAS_TRANSMISSION_MAP
    vec4 transmissionSample = texture(u_TransmissionSampler, getTransmissionUV());
    info.transmissionFactor *= transmissionSample.r;
#endif

#ifdef MATERIAL_DISPERSION
    info.dispersion = u_Dispersion;
#else
    info.dispersion = 0.0;
#endif
    return info;
}
```

**Key Points:**
- Starts with uniform `u_TransmissionFactor` (0-1 range)
- Multiplies by red channel of transmission texture if present
- Sets dispersion for chromatic aberration effect

#### Volume Properties

```glsl
MaterialInfo getVolumeInfo(MaterialInfo info)
{
    info.thickness = u_ThicknessFactor;
    info.attenuationColor = u_AttenuationColor;
    info.attenuationDistance = u_AttenuationDistance;

#ifdef HAS_THICKNESS_MAP
    vec4 thicknessSample = texture(u_ThicknessSampler, getThicknessUV());
    info.thickness *= thicknessSample.g;
#endif
    return info;
}
```

**Key Points:**
- Thickness defines how deep the refracted ray travels through the volume
- Attenuation color and distance control Beer's Law absorption
- Thickness map (green channel) provides spatial variation

#### Index of Refraction (IOR)

```glsl
MaterialInfo getIorInfo(MaterialInfo info)
{
    info.ior = u_Ior;
    // Update f0_dielectric based on IOR
    info.f0_dielectric = vec3(pow((info.ior - 1.0) / (info.ior + 1.0), 2.0));
    return info;
}
```

**Key Points:**
- Default IOR is 1.5 (typical for glass)
- Recalculates Fresnel F0 reflectance for dielectrics using IOR
- Formula: `F0 = ((ior - 1) / (ior + 1))²`

---

## Image-Based Lighting (IBL) Transmission

The main IBL transmission function handles environment refraction sampling.

### Function Signature

```glsl
vec3 getIBLVolumeRefraction(
    vec3 n,                          // Surface normal
    vec3 v,                          // View direction
    float perceptualRoughness,       // Surface roughness
    vec3 baseColor,                  // Material base color (tint)
    vec3 position,                   // World space position
    mat4 modelMatrix,                // Model transformation matrix
    mat4 viewMatrix,                 // View transformation matrix
    mat4 projMatrix,                 // Projection matrix
    float ior,                       // Index of refraction
    float thickness,                 // Material thickness
    vec3 attenuationColor,           // Absorption color
    float attenuationDistance,       // Absorption distance
    float dispersion                 // Chromatic dispersion factor
)
```

### Step-by-Step Breakdown

#### Step 1: Calculate Refraction Ray

```glsl
vec3 transmissionRay = getVolumeTransmissionRay(n, v, thickness, ior, modelMatrix);
```

Calls helper function to compute the refracted ray through the volume (detailed below).

#### Step 2: Calculate Exit Point

```glsl
vec3 refractedRayExit = position + transmissionRay;
```

The exit point is where the refracted ray leaves the material volume, calculated by adding the transmission ray vector to the entry point.

#### Step 3: Transform to Screen Space

```glsl
// Project refracted exit point to clip space
vec4 ndcPos = projMatrix * viewMatrix * vec4(refractedRayExit, 1.0);

// Convert to normalized device coordinates (NDC)
vec2 refractionCoords = ndcPos.xy / ndcPos.w;

// Map from [-1, 1] to [0, 1] (texture coordinates)
refractionCoords += 1.0;
refractionCoords /= 2.0;
```

**Why this transformation?**
- `viewMatrix * position` → View space
- `projMatrix * viewSpace` → Clip space
- `xy / w` → Perspective divide → NDC space [-1, 1]
- `(ndc + 1) / 2` → Texture coordinates [0, 1]

#### Step 4: Sample Framebuffer

```glsl
vec3 transmittedLight = getTransmissionSample(refractionCoords, perceptualRoughness, ior);
```

Samples the pre-rendered framebuffer at the refracted coordinates:

```glsl
vec3 getTransmissionSample(vec2 fragCoord, float roughness, float ior)
{
    float framebufferLod = log2(float(u_TransmissionFramebufferSize.x)) 
                          * applyIorToRoughness(roughness, ior);
    vec3 transmittedLight = textureLod(u_TransmissionFramebufferSampler, 
                                       fragCoord.xy, 
                                       framebufferLod).rgb;
    return transmittedLight;
}
```

**Key aspects:**
- **LOD calculation**: Higher roughness → blurrier refraction
- **IOR adjustment**: `applyIorToRoughness()` scales roughness by IOR
- **Mipmap sampling**: Uses `textureLod()` for controlled blur

#### Step 5: Apply Volume Attenuation (Beer's Law)

```glsl
vec3 attenuatedColor = applyVolumeAttenuation(
    transmittedLight, 
    transmissionRayLength, 
    attenuationColor, 
    attenuationDistance
);
```

Simulates light absorption through the volume (detailed in section below).

#### Step 6: Apply Base Color Tint

```glsl
return attenuatedColor * baseColor;
```

Multiplies by base color to tint the transmitted light.

### Dispersion Variant (Chromatic Aberration)

When `MATERIAL_DISPERSION` is defined, the implementation splits RGB channels:

```glsl
#ifdef MATERIAL_DISPERSION
    // Dispersion spreads IOR across R, G, B channels
    float halfSpread = (ior - 1.0) * 0.025 * dispersion;
    vec3 iors = vec3(ior - halfSpread, ior, ior + halfSpread);

    vec3 transmittedLight;
    float transmissionRayLength;
    
    // Process each color channel independently
    for (int i = 0; i < 3; i++)
    {
        vec3 transmissionRay = getVolumeTransmissionRay(n, v, thickness, iors[i], modelMatrix);
        transmissionRayLength = length(transmissionRay);
        vec3 refractedRayExit = position + transmissionRay;

        // Project to screen space
        vec4 ndcPos = projMatrix * viewMatrix * vec4(refractedRayExit, 1.0);
        vec2 refractionCoords = ndcPos.xy / ndcPos.w;
        refractionCoords += 1.0;
        refractionCoords /= 2.0;

        // Sample only the corresponding channel
        transmittedLight[i] = getTransmissionSample(refractionCoords, perceptualRoughness, iors[i])[i];
    }
#endif
```

**Physical basis:**
- Red light bends less (lower IOR)
- Blue light bends more (higher IOR)
- Creates rainbow-like color fringing at edges (like a prism)

---

## Volume Transmission Ray Calculation

This is the core physics calculation that determines the refracted ray path.

### Function: `getVolumeTransmissionRay()`

```glsl
vec3 getVolumeTransmissionRay(vec3 n, vec3 v, float thickness, float ior, mat4 modelMatrix)
{
    // 1. Apply Snell's Law (refraction at interface)
    vec3 refractionVector = refract(-v, normalize(n), 1.0 / ior);

    // 2. Extract rotation-independent scale from model matrix
    vec3 modelScale;
    modelScale.x = length(vec3(modelMatrix[0].xyz));
    modelScale.y = length(vec3(modelMatrix[1].xyz));
    modelScale.z = length(vec3(modelMatrix[2].xyz));

    // 3. Scale thickness by model scale (thickness is in local space)
    return normalize(refractionVector) * thickness * modelScale;
}
```

### Detailed Breakdown

#### 1. Snell's Law Refraction

```glsl
vec3 refractionVector = refract(-v, normalize(n), 1.0 / ior);
```

**GLSL `refract()` function:**
```
refract(I, N, eta) = I - N * (dot(N, I) * eta + sqrt(k))
where:
  k = 1.0 - eta² * (1.0 - (dot(N, I))²)
  If k < 0, returns vec3(0) (total internal reflection)
```

**Parameters:**
- `I = -v`: Incident ray (view direction negated)
- `N = normalize(n)`: Surface normal
- `eta = 1.0 / ior`: Relative IOR (air-to-glass)

**Physical interpretation:**
- For glass (IOR ≈ 1.5): Light bends toward the normal when entering
- The ratio `1.0 / ior` (≈ 0.67) makes light bend inward

#### 2. Model Scale Extraction

```glsl
vec3 modelScale;
modelScale.x = length(vec3(modelMatrix[0].xyz));
modelScale.y = length(vec3(modelMatrix[1].xyz));
modelScale.z = length(vec3(modelMatrix[2].xyz));
```

**Why extract scale?**
- Model matrix = Rotation × Scale × Translation
- Each column vector's magnitude = scale in that axis
- Thickness is authored in local (unscaled) space
- Must scale thickness to match world-space object size

**Example:**
- Object scaled 2x → rays must travel 2x farther through material

#### 3. Final Ray Calculation

```glsl
return normalize(refractionVector) * thickness * modelScale;
```

- `normalize(refractionVector)`: Unit direction of refraction
- `* thickness`: Author-defined material depth
- `* modelScale`: Account for object scaling

**Result:** A vector in world space representing the path through the material

---

## Punctual Light Transmission

Handles direct lighting (point, spot, directional lights) through transmissive surfaces.

### Main Integration (pbr.frag)

```glsl
#ifdef MATERIAL_TRANSMISSION
    // Calculate transmission ray to find light exit point
    vec3 transmissionRay = getVolumeTransmissionRay(n, v, materialInfo.thickness, 
                                                     materialInfo.ior, u_ModelMatrix);
    // Adjust light vector to exit point
    pointToLight -= transmissionRay;
    l = normalize(pointToLight);

    // Calculate transmitted radiance
    vec3 transmittedLight = lightIntensity * getPunctualRadianceTransmission(
        n, v, l, 
        materialInfo.alphaRoughness, 
        baseColor.rgb, 
        materialInfo.ior
    );

#ifdef MATERIAL_VOLUME
    // Apply absorption through volume
    transmittedLight = applyVolumeAttenuation(
        transmittedLight, 
        length(transmissionRay), 
        materialInfo.attenuationColor, 
        materialInfo.attenuationDistance
    );
#endif

    // Blend with diffuse lighting
    l_diffuse = mix(l_diffuse, transmittedLight, materialInfo.transmissionFactor);
#endif
```

### Function: `getPunctualRadianceTransmission()`

```glsl
vec3 getPunctualRadianceTransmission(
    vec3 normal, 
    vec3 view, 
    vec3 pointToLight, 
    float alphaRoughness,
    vec3 baseColor, 
    float ior
)
{
    // 1. Adjust roughness based on IOR
    float transmissionRougness = applyIorToRoughness(alphaRoughness, ior);

    // 2. Normalize vectors
    vec3 n = normalize(normal);
    vec3 v = normalize(view);
    vec3 l = normalize(pointToLight);
    
    // 3. Mirror the light direction across the surface
    vec3 l_mirror = normalize(l + 2.0 * n * dot(-l, n));
    
    // 4. Calculate halfway vector for BTDF
    vec3 h = normalize(l_mirror + v);

    // 5. Evaluate microfacet distribution
    float D = D_GGX(clamp(dot(n, h), 0.0, 1.0), transmissionRougness);
    
    // 6. Evaluate visibility/shadowing term
    float Vis = V_GGX(clamp(dot(n, l_mirror), 0.0, 1.0), 
                      clamp(dot(n, v), 0.0, 1.0), 
                      transmissionRougness);

    // 7. Return BTDF (Bidirectional Transmittance Distribution Function)
    return baseColor * D * Vis;
}
```

### Detailed Explanation

#### Light Vector Mirroring

```glsl
vec3 l_mirror = normalize(l + 2.0 * n * dot(-l, n));
```

**Mathematical basis:**
- Reflection formula: `R = I - 2 * N * dot(N, I)`
- Mirrors light across the surface for transmission calculation
- Used instead of actual refracted direction (approximation)

**Why mirror instead of refract?**
- Computationally cheaper than full refraction
- Works well for thin surfaces
- Accounts for microfacet orientation

#### GGX Microfacet Model

**D_GGX (Normal Distribution Function):**
```glsl
float D_GGX(float NdotH, float alphaRoughness)
{
    float alphaRoughnessSq = alphaRoughness * alphaRoughness;
    float f = (NdotH * NdotH) * (alphaRoughnessSq - 1.0) + 1.0;
    return alphaRoughnessSq / (M_PI * f * f);
}
```

**Physical meaning:**
- Describes how microfacets are oriented
- Higher roughness → wider distribution
- Peak at `NdotH = 1` (aligned with halfway vector)

**V_GGX (Visibility/Geometry Function):**
```glsl
float V_GGX(float NdotL, float NdotV, float alphaRoughness)
{
    float alphaRoughnessSq = alphaRoughness * alphaRoughness;
    float GGXV = NdotL * sqrt(NdotV * NdotV * (1.0 - alphaRoughnessSq) + alphaRoughnessSq);
    float GGXL = NdotV * sqrt(NdotL * NdotL * (1.0 - alphaRoughnessSq) + alphaRoughnessSq);
    float GGX = GGXV + GGXL;
    return (GGX > 0.0) ? (0.5 / GGX) : 0.0;
}
```

**Physical meaning:**
- Accounts for microfacet shadowing and masking
- Rougher surfaces → more self-occlusion
- Smith joint approximation (optimized)

#### IOR Roughness Adjustment

```glsl
float applyIorToRoughness(float roughness, float ior)
{
    // Scale roughness so IOR 1.0 = no refraction, IOR 1.5 = default
    return roughness * clamp(ior * 2.0 - 2.0, 0.0, 1.0);
}
```

**Rationale:**
- Higher IOR → stronger refraction → more visible roughness effects
- `ior * 2.0 - 2.0`:
  - IOR 1.0 → factor 0.0 (no roughness)
  - IOR 1.5 → factor 1.0 (full roughness)
  - IOR 2.0 → factor 2.0 (clamped to 1.0)

---

## Volume Attenuation (Beer's Law)

Simulates light absorption as it travels through the material volume.

### Function: `applyVolumeAttenuation()`

```glsl
vec3 applyVolumeAttenuation(
    vec3 radiance,              // Incoming light
    float transmissionDistance, // Distance traveled through material
    vec3 attenuationColor,      // Absorption color
    float attenuationDistance   // Reference distance for absorption
)
{
    if (attenuationDistance == 0.0)
    {
        // Infinite attenuation distance = no absorption
        return radiance;
    }
    else
    {
        // Beer-Lambert Law: I = I₀ * e^(-αx)
        vec3 transmittance = pow(attenuationColor, vec3(transmissionDistance / attenuationDistance));
        return transmittance * radiance;
    }
}
```

### Physics Explanation

**Beer-Lambert Law:**
```
I(x) = I₀ * e^(-α * x)
```

Where:
- `I(x)` = Intensity at distance x
- `I₀` = Initial intensity
- `α` = Absorption coefficient
- `x` = Distance traveled

**Shader implementation uses power function:**
```glsl
transmittance = pow(attenuationColor, transmissionDistance / attenuationDistance)
```

**Equivalence:**
- `attenuationColor` represents `e^(-α * attenuationDistance)`
- Raising to power `transmissionDistance / attenuationDistance` gives `e^(-α * transmissionDistance)`

**Color absorption:**
- `attenuationColor = (1.0, 0.5, 0.1)` (orange tint)
- Blue light absorbed most (0.1), red least (1.0)
- Creates colored glass effect

**Examples:**

1. **Clear glass:**
   - `attenuationColor = (1, 1, 1)` → No absorption
   - `transmittance = (1, 1, 1)` regardless of distance

2. **Green glass:**
   - `attenuationColor = (0.3, 0.9, 0.3)`
   - After 1x distance: `(0.3, 0.9, 0.3)`
   - After 2x distance: `(0.09, 0.81, 0.09)` (more absorption)

3. **Infinite distance:**
   - `attenuationDistance = 0.0` → No attenuation applied

---

## Coordinate Space Transformations

Understanding the coordinate transformations is crucial for correct refraction.

### Transformation Pipeline

```
Local Space (Model)
    ↓ [Model Matrix]
World Space
    ↓ [View Matrix]
View Space (Camera)
    ↓ [Projection Matrix]
Clip Space
    ↓ [Perspective Divide]
NDC Space (Normalized Device Coordinates)
    ↓ [Viewport Transform]
Screen Space / Texture Coordinates
```

### Critical Code Section

```glsl
// 1. World space position (already available)
vec3 refractedRayExit = position + transmissionRay;

// 2. Transform to view space
vec4 viewPos = viewMatrix * vec4(refractedRayExit, 1.0);

// 3. Transform to clip space
vec4 clipPos = projMatrix * viewPos;

// 4. Perspective divide → NDC space [-1, 1]
vec2 ndc = clipPos.xy / clipPos.w;

// 5. Map to texture coordinates [0, 1]
vec2 texCoords = ndc * 0.5 + 0.5;
```

### Why Each Step Matters

1. **World Space:** Physics calculations (refraction, thickness)
2. **View Space:** Camera-relative positioning
3. **Clip Space:** Perspective projection applied
4. **NDC Space:** Normalized for screen mapping
5. **Texture Space:** Match framebuffer sampling

### Handling Different Graphics APIs

```glsl
#if !SOKOL_GLSL
    // Metal/D3D: flip Y for texture coordinate system
    refractionCoords.y = 1.0 - refractionCoords.y;
#endif
```

**Reason:**
- OpenGL: Origin (0,0) at bottom-left
- Metal/D3D11: Origin (0,0) at top-left
- Must flip Y to sample correct framebuffer pixel

---

## Integration in Main Shader (pbr.frag)

### Setup Phase

```glsl
#ifdef MATERIAL_TRANSMISSION
    materialInfo = getTransmissionInfo(materialInfo);
#endif

#ifdef MATERIAL_VOLUME
    materialInfo = getVolumeInfo(materialInfo);
#endif
```

Loads transmission properties from textures and uniforms.

### IBL Application

```glsl
#if defined(MATERIAL_TRANSMISSION)
    f_specular_transmission = getIBLVolumeRefraction(
        n, v,
        materialInfo.perceptualRoughness,
        baseColor.rgb, v_Position, u_ModelMatrix, u_ViewMatrix, u_ProjectionMatrix,
        materialInfo.ior, materialInfo.thickness, 
        materialInfo.attenuationColor, materialInfo.attenuationDistance, 
        materialInfo.dispersion
    );
    
    // Blend transmission with diffuse lighting
    f_diffuse = mix(f_diffuse, f_specular_transmission, materialInfo.transmissionFactor);
#endif
```

**Blending:**
- `transmissionFactor = 0.0` → Fully opaque (diffuse)
- `transmissionFactor = 1.0` → Fully transparent (refraction)

### Punctual Light Loop

```glsl
for (int i = 0; i < LIGHT_COUNT; ++i)
{
    // ... standard lighting setup ...
    
#ifdef MATERIAL_TRANSMISSION
    vec3 transmissionRay = getVolumeTransmissionRay(n, v, materialInfo.thickness, 
                                                     materialInfo.ior, u_ModelMatrix);
    pointToLight -= transmissionRay;
    l = normalize(pointToLight);

    vec3 transmittedLight = lightIntensity * getPunctualRadianceTransmission(
        n, v, l, materialInfo.alphaRoughness, baseColor.rgb, materialInfo.ior
    );

#ifdef MATERIAL_VOLUME
    transmittedLight = applyVolumeAttenuation(transmittedLight, length(transmissionRay), 
                                               materialInfo.attenuationColor, 
                                               materialInfo.attenuationDistance);
#endif
    l_diffuse = mix(l_diffuse, transmittedLight, materialInfo.transmissionFactor);
#endif

    // ... continue with standard BRDF ...
}
```

---

## Key Implementation Details

### 1. Framebuffer Pre-pass

**Requirement:** Transmission requires a pre-rendered framebuffer of the scene behind the transparent object.

**Rendering order:**
1. Render all opaque objects to framebuffer
2. Render transmissive objects, sampling from framebuffer

### 2. Two-Pass Rendering Consideration

**Why needed:**
- Can't sample framebuffer while writing to it
- Must render background first
- Transparent objects must be sorted back-to-front

### 3. Roughness and LOD

```glsl
float framebufferLod = log2(float(u_TransmissionFramebufferSize.x)) 
                      * applyIorToRoughness(roughness, ior);
```

**Purpose:**
- Blurs refraction for rough surfaces
- Uses mipmap levels for efficient blur
- IOR affects blur intensity

### 4. Thickness as Artist Control

**Thickness factor is NOT physical depth:**
- Artist-controlled parameter
- Scales refraction ray length
- Affects attenuation amount
- Can be texture-mapped for variation

### 5. Fresnel Not Applied

**Notable omission:**
- Full transmission implementation would include Fresnel
- Would control reflection vs. transmission ratio
- Likely handled separately in viewer's Fresnel calculations

### 6. Approximations and Limitations

**This implementation assumes:**
- Single refraction event (no multiple bounces)
- Flat-ish surfaces (mirrored light vector approximation)
- Pre-rendered background (no real-time recursion)
- No caustics (light focusing through refraction)

### 7. Performance Optimizations

- Conditional compilation (`#ifdef`) for unused features
- Single texture sample per fragment (with LOD)
- Efficient vector math (minimal branching)
- Smith joint GGX (optimized G term)

---

## Comparison with Simplified Implementations

### What glTF Sample Viewer Does Right

1. **Proper coordinate transformations** (world → view → clip → NDC → texture)
2. **IOR-based physics** (Snell's law, Fresnel F0 calculation)
3. **Volume attenuation** (Beer's law for colored glass)
4. **Roughness-dependent blur** (LOD selection)
5. **Dispersion support** (chromatic aberration)
6. **Model scale handling** (thickness in local space)

### Common Mistakes in Other Implementations

1. **Forgetting model scale** → Refraction breaks when objects are scaled
2. **Wrong coordinate space** → Refraction points to wrong screen location
3. **No roughness blur** → Rough glass looks too sharp
4. **Ignoring perspective divide** → Distorted refraction
5. **Missing API differences** → Flipped coordinates on Metal/D3D

---

## Summary

The glTF Sample Viewer's transmission implementation demonstrates a production-quality approach to real-time refraction:

- **Physics-based:** Uses Snell's law, Beer's law, and microfacet BRDF theory
- **Efficient:** Single-pass refraction with mipmap-based blur
- **Flexible:** Supports IOR, thickness, attenuation, dispersion, roughness
- **Robust:** Handles different coordinate systems and model transformations
- **Standard-compliant:** Follows KHR_materials_transmission specification

The key insight is the **screen-space refraction** technique: instead of ray-tracing through geometry, it samples a pre-rendered framebuffer at the refracted screen position. This trades physical accuracy for real-time performance while maintaining convincing visual results.

---

## References

- [glTF 2.0 Specification - KHR_materials_transmission](https://github.com/KhronosGroup/glTF/tree/master/extensions/2.0/Khronos/KHR_materials_transmission)
- [glTF Sample Viewer Repository](https://github.com/KhronosGroup/glTF-Sample-Viewer)
- Real Shading in Unreal Engine 4 (Karis 2013)
- Physically Based Shading at Disney (Burley 2012)
- Beer-Lambert Law (Wikipedia)
- Snell's Law and Refraction (Physics)
