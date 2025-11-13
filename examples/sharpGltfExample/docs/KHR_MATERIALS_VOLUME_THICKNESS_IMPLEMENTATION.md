# KHR_materials_volume Thickness Texture Implementation

## Overview

This document describes the implementation of thickness texture support for the `KHR_materials_volume` extension in the glTF PBR renderer. The thickness texture provides spatially-varying thickness values for Beer's law volume attenuation, enabling realistic colored glass and translucent materials with depth-dependent absorption.

## Implementation Date
November 12, 2025

## Related Extensions
- `KHR_materials_volume` - Volume absorption with Beer's law
- `KHR_materials_transmission` - Transmission/refraction (often used together)

## Problem Statement

The volume extension was initially implemented with only uniform thickness values (via `thicknessFactor`). However, many realistic glass materials require **varying thickness** across the surface to achieve effects like:
- Amber glass with thicker/thinner regions showing different color saturation
- Hurricane candle holders with thickness gradients
- Bottles and containers with non-uniform wall thickness

The glTF specification allows thickness to be stored in a texture (GREEN channel), but this was not implemented, causing models like `GlassHurricaneCandleHolder.gltf` to render incorrectly.

## Technical Challenges

### Challenge 1: Limited Texture Binding Slots

**Problem**: Sokol Graphics provides 12 texture binding slots (0-11) shared between vertex and fragment shaders. All slots were already allocated:
- Slots 0-4: Standard PBR textures (BaseColor, MetallicRoughness, Normal, Occlusion, Emissive)
- Slots 5-7: IBL textures (GGX env, Lambertian env, GGX LUT)
- Slot 8: Charlie env (sheen) OR Transmission texture
- Slot 9: Charlie LUT OR Morph targets
- Slot 10: Transmission framebuffer
- Slot 11: Joint matrices (skinning)

**Solution**: Implemented **dynamic runtime slot allocation** that finds the first available 2D texture slot and binds thickness there. The C# code passes the slot number to the shader via the `thickness_tex_index` uniform, allowing the shader to sample from any valid slot.

### Challenge 2: Slot Type Mismatches

**Problem**: Not all slots are equivalent:
- Slots 5-6 are **cubemap** slots (`textureCube`) - cannot be used for 2D thickness textures
- Slot 8 binding depends on shader variant (Charlie cubemap vs Transmission 2D texture)
- Slot 11 is vertex-shader only (joint matrices)

**Solution**: Implemented slot preference order that avoids cubemap slots:
```csharp
int[] preferredSlots = { 7, 8, 9, 10, 11, 0, 1, 2, 3, 4 };
```
This prioritizes 2D texture slots and skips slots 5-6 entirely.

### Challenge 3: Slot Overwrites

**Problem**: Even after assigning thickness to a slot, later code would overwrite it. For example:
1. Thickness assigned to slot 8
2. Transmission texture binding code runs and overwrites slot 8 with default white texture
3. Thickness texture lost, shader samples wrong texture

**Solution**: Added checks to prevent overwrites when thickness is already bound:
```csharp
if (ThicknessBindingSlot != 8 && ...) {
    // Only bind if thickness is not using slot 8
    bind.views[8] = defaultWhite.View;
    bind.samplers[8] = defaultWhite.Sampler;
}
```

### Challenge 4: Shader Binding Declarations

**Problem**: The shader has fixed texture binding declarations per slot. When thickness is bound to slot 8, the shader needs to know which texture binding to use (e.g., `u_TransmissionTexture` vs `u_GGXLUTTexture`).

**Solution**: Updated `getThickness()` shader function to handle multiple slots with appropriate texture bindings:
```glsl
int texIndex = int(thickness_tex_index + 0.5);
if (texIndex == 7) {
    thicknessValue = texture(sampler2D(u_GGXLUTTexture, u_GGXLUTSampler_Raw), baseUV).g;
}
else if (texIndex == 8) {
    #ifdef TRANSMISSION
    thicknessValue = texture(sampler2D(u_TransmissionTexture, u_TransmissionSampler_Raw), baseUV).g;
    #endif
}
// ... handles other valid slots
```

## Implementation Details

### C# Code (Mesh.cs)

**Slot Tracking**:
```csharp
bool[] slotUsed = new bool[12]; // Track all 12 slots
slotUsed[0] = true; // BaseColor
slotUsed[1] = true; // MetallicRoughness
// ... initialize all slots based on what's actually in use
slotUsed[5] = useIBL; // GGX env only used if IBL enabled
slotUsed[8] = (TransmissionTextureIndex >= 0); // Reserve if transmission exists
```

**Dynamic Allocation**:
```csharp
ThicknessBindingSlot = -1;
if (ThicknessTextureIndex >= 0 && Textures[ThicknessTextureIndex] != null)
{
    int[] preferredSlots = { 7, 8, 9, 10, 11, 0, 1, 2, 3, 4 };
    foreach (int slot in preferredSlots)
    {
        if (!slotUsed[slot])
        {
            ThicknessBindingSlot = slot;
            slotUsed[slot] = true;
            bind.views[slot] = thicknessTex.View;
            bind.samplers[slot] = thicknessTex.Sampler;
            break;
        }
    }
}
```

**Preventing Overwrites**:
```csharp
// Transmission texture binding - check thickness first
if (TransmissionTextureIndex >= 0 && Textures[TransmissionTextureIndex] != null)
{
    if (ThicknessBindingSlot != 8) // Don't overwrite thickness
    {
        bind.views[8] = transmissionTex.View;
        bind.samplers[8] = transmissionTex.Sampler;
    }
}
else if (ThicknessBindingSlot != 8 && ...) 
{
    // Default binding - also check thickness
    bind.views[8] = defaultWhite.View;
    bind.samplers[8] = defaultWhite.Sampler;
}
```

### Shader Code (pbr.glsl)

**Uniform**:
```glsl
layout(binding=1) uniform metallic_params {
    // ...
    float thickness_tex_index;  // Which slot contains thickness (0-11)
    // ...
};
```

**Sampling Function**:
```glsl
float getThickness() {
    float thickness = thickness_factor;
    
    if (has_thickness_tex > 0.5) {
        vec2 baseUV = (thickness_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        int texIndex = int(thickness_tex_index + 0.5);
        float thicknessValue = 0.0;
        
        // Sample from dynamically-assigned slot
        // glTF spec: thickness is GREEN channel
        if (texIndex == 0) {
            thicknessValue = texture(sampler2D(u_BaseColorTexture, u_BaseColorSampler), baseUV).g;
        }
        // ... handles slots 1-4 (standard PBR)
        else if (texIndex == 7) {
            thicknessValue = texture(sampler2D(u_GGXLUTTexture, u_GGXLUTSampler_Raw), baseUV).g;
        }
        else if (texIndex == 8) {
            #ifdef TRANSMISSION
            thicknessValue = texture(sampler2D(u_TransmissionTexture, u_TransmissionSampler_Raw), baseUV).g;
            #endif
        }
        else if (texIndex == 9) {
            #ifndef MORPHING
            thicknessValue = texture(sampler2D(u_CharlieLUTTexture, u_CharlieLUTSampler_Raw), baseUV).g;
            #endif
        }
        else if (texIndex == 10) {
            thicknessValue = texture(sampler2D(u_TransmissionFramebufferTexture, u_TransmissionFramebufferSampler_Raw), baseUV).g;
        }
        
        thickness *= thicknessValue;
    }
    
    return thickness;
}
```

### Frame Files

Updated all 4 frame rendering files to pass the dynamic slot number:
- `Frame_Static.cs`
- `Frame_Skinning.cs`
- `Frame_Morphing.cs`
- `Frame_SkinnedMorphing.cs`

```csharp
metallicParams.has_thickness_tex = hasThicknessTex ? 1.0f : 0.0f;
metallicParams.thickness_texcoord = mesh.ThicknessTexCoord;
metallicParams.thickness_tex_index = mesh.ThicknessBindingSlot >= 0 ? (float)mesh.ThicknessBindingSlot : 0.0f;
```

## Slot Allocation Strategy

### Preferred Slot Order
1. **Slot 7** - First choice when IBL disabled (GGX LUT texture, 2D)
2. **Slot 8** - First choice when IBL enabled (Transmission texture, 2D)
3. **Slot 9** - Charlie LUT or available (2D)
4. **Slot 10** - Transmission framebuffer or available (2D)
5. **Slot 11** - Vertex shader only, but can be used if needed
6. **Slots 0-4** - Last resort (overwrites standard PBR textures)

### Avoided Slots
- **Slots 5-6** - Never used (cubemap slots, incompatible with 2D thickness texture)

### Typical Scenarios

**With IBL enabled, transmission material**:
- Slots 5-7: IBL textures (cubemaps + LUT)
- Slot 8: **Thickness texture** (transmission texture not present in GlassHurricaneCandleHolder)
- Slot 9: Charlie LUT
- Slot 10: Transmission framebuffer

**With IBL disabled, transmission material**:
- Slots 5-6: Default cubemaps (not used by thickness)
- Slot 7: **Thickness texture** (first available 2D slot)
- Slot 8: Default white texture
- Slot 10: Transmission framebuffer

**With normal + thickness texture (e.g., PotOfCoalsAnimationPointer.gltf)**:
- Slot 2: Normal texture
- Slot 8: **Thickness texture** (if transmission absent)
- Both can coexist without conflicts

## Beer's Law Volume Attenuation

Once thickness texture is properly sampled, it's used in Beer's law calculation:

```glsl
float transmissionRayLength = length(transmissionRay);
vec3 attenuationCoefficient = -log(attenuation_color) / attenuation_distance;
vec3 attenuation = exp(-attenuationCoefficient * transmissionRayLength);
transmittedLight *= attenuation;
```

Where:
- `thickness` from texture modulates the ray length
- `attenuation_color` (e.g., cyan [0.8, 0.95, 1.0]) creates complementary color effect
- `attenuation_distance` controls absorption rate

## Testing

### Test Assets

**GlassHurricaneCandleHolder.gltf**:
- Material: Glass with transmission
- Thickness texture: Gradient (thicker at base, thinner at top)
- Attenuation color: [0.8, 0.95, 1.0] (cyan)
- Attenuation distance: 0.001 (very small, strong absorption)
- Expected result: Blue/cyan glass with depth-dependent color intensity

**PotOfCoalsAnimationPointer.gltf**:
- Material: HeatDome with both normal texture (index 8) and thickness texture (index 9)
- Verifies: Thickness can coexist with other textures without conflicts

### Debug Views

**DEBUG_VOLUME_THICKNESS** view shows:
- White/gray gradient = thickness texture being sampled correctly
- Solid color = thickness not being sampled (uniform value only)
- Black = no thickness data

### Validation

Tested configurations:
- ✅ IBL enabled, transmission material → thickness uses slot 8
- ✅ IBL disabled, transmission material → thickness uses slot 7
- ✅ Normal + thickness textures → both coexist without conflicts
- ✅ No crashes when toggling IBL on/off

## Known Limitations

1. **Very Small Attenuation Distances**: The GlassHurricaneCandleHolder uses `attenuationDistance = 0.001`, which causes extremely strong absorption. This may be a glTF file issue rather than implementation issue.

2. **Slot Exhaustion**: If all 12 slots are occupied, thickness texture cannot be bound. This is unlikely but theoretically possible with complex materials using all extensions simultaneously.

3. **Shader Variant Complexity**: The `getThickness()` function has conditional compilation (`#ifdef TRANSMISSION`, `#ifndef MORPHING`) which creates different code paths per shader variant.

## Future Improvements

1. **Dedicated Thickness Slot**: Consider reserving a specific slot for thickness texture to avoid dynamic allocation complexity.

2. **Attenuation Distance Handling**: Investigate if very small attenuation distances should be clamped or handled specially to match glTF Sample Viewer behavior.

3. **Texture Atlas Support**: Could pack thickness into unused channels of existing textures to reduce slot usage.

## References

- glTF Specification: `KHR_materials_volume` extension
- glTF Sample Viewer: [Reference implementation](https://github.com/KhronosGroup/glTF-Sample-Viewer)
- Test Assets: Khronos glTF Sample Models repository

## Conclusion

The dynamic slot allocation approach successfully enables thickness texture support without requiring additional fixed texture binding slots. The implementation handles various edge cases (IBL on/off, slot conflicts, texture type mismatches) while maintaining compatibility with existing PBR rendering features. This allows realistic rendering of colored glass and translucent materials with spatially-varying absorption properties.
