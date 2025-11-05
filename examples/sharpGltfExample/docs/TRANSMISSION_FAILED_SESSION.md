# Transmission Debugging - Failed Session Summary
**Date**: November 4, 2025  
**Status**: ❌ UNRESOLVED - Glass still rendering opaque blue

## Problem Statement
GlassHurricaneCandleHolder model renders with **opaque blue glass** instead of transparent glass with visible logo underneath.

## Expected vs Actual
- **Expected**: Transparent glass showing background and metal base with green glTF logo
- **Actual**: Opaque solid blue glass, no transparency, logo not visible

## What Was Tried (All Failed)

### 1. Fixed Double Attenuation (✅ Dragon, ❌ Glass)
- **Change**: Modified volume attenuation to use `else if` to prevent double application
- **Result**: Dragon shows correct amber color, but glass still opaque blue
- **Files**: `cgltf-sapp.glsl` line 706-740

### 2. Fixed Floating Point Comparison
- **Change**: Changed `attenuationDistance == 0.0` to `< 0.001`
- **Result**: No effect on glass transparency
- **Reason**: Glass has `attenuationDistance = 0.00` (infinite)

### 3. Consulted glTF-Sample-Viewer Reference
- **Action**: Searched official Khronos reference implementation
- **Finding**: Reference DOES multiply transmission by baseColor
- **Result**: Confirmed our approach was spec-compliant, but still broken

### 4. Changed Transmission to Use base_color_factor Only
- **Change**: Passed `base_color_factor.rgb` instead of `base_color.rgb` to `calculate_refraction`
- **Rationale**: Thought base color texture (blue) was incorrectly tinting transmission
- **Result**: NO CHANGE - glass still opaque blue

### 5. Changed Background to Green for Testing
- **Change**: Background from blue (0.25, 0.5, 0.75) to green (0.0, 1.0, 0.0)
- **Result**: Top rim showed dark green (refraction working!), body still opaque blue
- **Key Finding**: Refraction IS working but seeing blue from somewhere

### 6. Suppressed Diffuse Color for Transmission
- **Change**: `vec3 diffuse_color = base_color.rgb * ... * (1.0 - transmission_factor)`
- **Rationale**: Prevent blue diffuse from accumulating before transmission mix
- **Result**: NO CHANGE - glass still opaque blue

### 7. Debug: Show Only Refracted Color
- **Change**: `color = refracted_color` (no mixing)
- **Result**: Top rim dark green, body pure blue
- **Conclusion**: Refraction shader sees blue, not green background

### 8. Changed Background to Black
- **Change**: Background from green to black (0, 0, 0)
- **Rationale**: Eliminate background color confusion
- **Result**: Top rim black, body still opaque blue

### 9. Force Base Color to Factor for Transmission
- **Change**: `if (transmission_factor > 0.0) base_color = base_color_factor;`
- **Rationale**: Completely ignore blue base color texture
- **Result**: UNKNOWN - user stopped session before testing

## Root Cause Analysis (Incomplete)

### What We Know
1. ✅ **Refraction shader IS working** - top rim shows background color correctly
2. ✅ **Material classification IS correct** - glass has `TransmissionFactor = 1.0`
3. ✅ **Two-pass rendering IS working** - opaque pass renders to screen texture
4. ❌ **Glass body sees BLUE instead of background** - this is the core issue

### Hypotheses (All Unverified)
1. **Base color texture is blue**: The texture file `GlassHurricaneCandleHolder_basecolor.png` has blue painted in the glass UV region
2. **Screen texture sampling fails**: The refraction UV coordinates for the glass body sample wrong location
3. **Depth buffer issue**: Glass fragments fail depth test and fall back to some default blue
4. **UV coordinate issue**: Glass mesh has incorrect UVs that sample blue part of texture
5. **Rendering order issue**: Glass is being rendered in opaque pass incorrectly

## Material Properties (From Logs)
```
Material 0 (Opaque Base):
- BaseColor: <1, 1, 1, 1>
- TransmissionFactor: 0.0
- Has base color texture: Yes (image_1)

Material 1 (Glass):
- BaseColor: <1, 1, 1, 1> (factor)
- BaseColor Texture: Yes (image_1) - SAME TEXTURE!
- TransmissionFactor: 1.0
- Volume: Thickness=0.10, AttenuationColor=(0.80, 0.95, 1.00), AttenuationDistance=0.00
- Both meshes share the SAME base color texture!
```

## Critical Finding
**Both the opaque metal base AND the transparent glass use the SAME base color texture** (`image_1 = GlassHurricaneCandleHolder_basecolor.png`). The texture is RGB (no alpha), which means:
- Different UV regions must represent glass vs metal
- The glass UV region in the texture is painted BLUE
- This blue is being applied even though transmission should make it transparent

## Files Modified
1. `examples/sharpGltfExample/shaders/cgltf-sapp.glsl`
   - Line 540-543: `applyVolumeAttenuation` - changed `== 0.0` to `< 0.001`
   - Line 658: Added `* (1.0 - transmission_factor)` to diffuse_color
   - Line 722: Changed to use `base_color_factor.rgb` instead of `base_color.rgb`
   - Line 596-599: Added conditional to ignore base color texture for transmission
   - Line 733: DEBUG line `color = refracted_color` (should be reverted)

2. `examples/sharpGltfExample/Source/SharpGltfModel.cs`
   - Lines 531-541: Changed transmission-only materials to use white attenuation

3. `examples/sharpGltfExample/Source/Init.cs`
   - Line 787: Changed background colors for testing (green, then black)

## Next Steps (For Future Debugging)
1. **Verify screen texture contents**: Add debug output to save screen texture after Pass 1
2. **Check UV coordinates**: Log glass mesh UV coordinates to verify they're valid
3. **Test with different model**: Try a known-working transmission model (e.g., official glTF samples)
4. **Check refraction UV calculation**: Verify `calculate_refraction` computes correct screen UVs
5. **Inspect base color texture**: Open the PNG file and see if glass region is actually blue
6. **Compare with reference**: Load same model in glTF-Sample-Viewer to see expected result

## Conclusion
After ~20 attempts and multiple approaches, the glass remains opaque blue. The debugging revealed that refraction IS working (top rim shows background), but the glass body consistently shows blue regardless of background color. The issue likely lies in either:
- The base color texture itself being blue in the glass UV region
- The screen texture sampling returning incorrect data
- Some aspect of the rendering pipeline we haven't identified

**Recommendation**: Start fresh with a simpler transmission test case before returning to this complex model.
