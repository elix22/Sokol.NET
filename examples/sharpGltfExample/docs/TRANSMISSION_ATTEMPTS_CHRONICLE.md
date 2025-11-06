# Transmission/Refraction Fix Attempts - Complete Chronicle

**Date**: November 5, 2025  
**Status**: ❌ **FAILED - DEAD END**  
**Model**: CommercialRefrigerator.gltf  
**Problem**: Interior bottles misalign during rotation when viewed through glass with KHR_materials_transmission

---

## The Core Problem

When rotating the CommercialRefrigerator model, the bottles visible through the glass panels appear **misaligned** - they don't stay in the correct position relative to the glass. This happens on **both Metal (macOS) and OpenGL backends**, indicating the issue is not simply a coordinate system Y-axis flip problem.

The user's goal: **"I want refraction"** - meaning the glass should show distortion/bending of light through it, while keeping the bottles properly aligned.

---

## Initial State

**Original Implementation**: World-space projection approach
```glsl
// Calculate 3D refraction ray in world space
vec3 transmission_ray = getVolumeTransmissionRay(normal, view, thickness, ior, model_mat);
vec3 refracted_ray_exit = position + transmission_ray;

// Project exit point to screen space
vec4 ndc_pos = view_mat * proj_mat * vec4(refracted_ray_exit, 1.0);
vec2 refraction_coords = ndc_pos.xy / ndc_pos.w;
refraction_coords = refraction_coords * 0.5 + 0.5;
```

**Problem**: Bottles appeared misaligned during rotation.

---

## Attempt #1: Matrix Multiplication Order Reversal

**Rationale**: Reference implementation (glTF-Sample-Renderer) uses `proj_mat * view_mat` order, not `view_mat * proj_mat`.

**Change**:
```glsl
// OLD: vec4 ndc_pos = view_mat * proj_mat * vec4(refracted_ray_exit, 1.0);
// NEW:
vec4 ndc_pos = proj_mat * view_mat * vec4(refracted_ray_exit, 1.0);
```

**Y-Flip Logic**: Added flip for OpenGL
```glsl
#if SOKOL_GLSL  // OpenGL
    refraction_coords.y = 1.0 - refraction_coords.y;
#endif
```

**Result**: ❌ Bottles still misaligned during rotation on both backends.

---

## Attempt #2: Use gl_FragCoord Instead of Projection

**Rationale**: `gl_FragCoord` provides the fragment's actual screen position without needing projection. Since Pass 1 rendered the background at the fragment's screen location, just sample there.

**Change**:
```glsl
// Use gl_FragCoord directly - no projection needed
vec2 screen_size = vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));
vec2 refraction_coords = gl_FragCoord.xy / screen_size;

#if SOKOL_GLSL  // OpenGL
    refraction_coords.y = 1.0 - refraction_coords.y;
#endif
```

**Result**: ✅ Perfect alignment, ❌ NO refraction effect - just a flat window showing the background unchanged.

**User Feedback**: "I want refraction"

---

## Attempt #3: Screen-Space Refraction with View-Space Normal

**Rationale**: Apply a small screen-space offset based on the surface normal direction, avoiding 3D projection entirely.

**Change**:
```glsl
// Transform normal to view space for screen-space offset
vec3 view_normal = normalize((view_mat * vec4(normal, 0.0)).xyz);

// Apply small screen-space offset based on normal direction
vec2 screen_size = vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));
vec2 base_coords = gl_FragCoord.xy / screen_size;
vec2 refraction_offset = view_normal.xy * 0.05; // Scale factor
vec2 refraction_coords = base_coords + refraction_offset;

#if SOKOL_GLSL  // OpenGL
    refraction_coords.y = 1.0 - refraction_coords.y;
#endif
```

**Result**: ❌ Bottles misaligned during rotation.

**Revert**: Changed back to gl_FragCoord-only approach.

---

## Attempt #4: Hybrid Approach - Project Both Entry and Exit Points

**Rationale**: Perhaps the issue is that we need to project BOTH the entry point (fragment position) AND the exit point, then calculate the difference.

**Change**:
```glsl
// Project BOTH entry and exit points
vec4 ndc_entry = proj_mat * view_mat * vec4(position, 1.0);
vec4 ndc_exit = proj_mat * view_mat * vec4(refracted_ray_exit, 1.0);

// Calculate screen-space offset
vec2 entry_uv = (ndc_entry.xy / ndc_entry.w) * 0.5 + 0.5;
vec2 exit_uv = (ndc_exit.xy / ndc_exit.w) * 0.5 + 0.5;
vec2 offset = exit_uv - entry_uv;

// Apply offset to gl_FragCoord base
vec2 screen_size = vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));
vec2 base_coords = gl_FragCoord.xy / screen_size;
vec2 refraction_coords = base_coords + offset;

#if SOKOL_GLSL  // OpenGL
    refraction_coords.y = 1.0 - refraction_coords.y;
#endif
```

**Result**: ❌ Bottles still misaligned during rotation on both backends.

---

## Attempt #5: Use gl_FragCoord as Entry, Only Project Exit

**Rationale**: Since `gl_FragCoord` IS the entry point (where the fragment is on screen), don't project the position. Only project the exit point to calculate the offset.

**Change**:
```glsl
// Use gl_FragCoord directly as entry point (don't project position)
vec2 screen_size = vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));
vec2 entry_uv = gl_FragCoord.xy / screen_size;

// Project ONLY the exit point
vec4 ndc_exit = proj_mat * view_mat * vec4(refracted_ray_exit, 1.0);
vec2 exit_uv = (ndc_exit.xy / ndc_exit.w) * 0.5 + 0.5;

// Calculate offset and apply
vec2 offset = exit_uv - entry_uv;
vec2 refraction_coords = entry_uv + offset;

#if SOKOL_GLSL  // OpenGL
    refraction_coords.y = 1.0 - refraction_coords.y;
#endif
```

**Result**: ❌ Bottles still misaligned during rotation on both backends.

**User Feedback**: "with this modification the flip is not needed, but the bottles are still misaligned when rotating, both in Metal and OpenGL"

---

## Attempt #6: Remove OpenGL Y-Flip Entirely

**Rationale**: User reported flip not needed, try without any Y-flip.

**Change**:
```glsl
// Removed the entire #if SOKOL_GLSL block
vec2 refraction_coords = entry_uv + offset;
// No Y-flip applied
```

**Result**: ❌ User reports "now flipping on y is needed for Metal, bottles are misaligned when rotating on both Metal and OpenGL"

---

## Debug Attempts

### Debug Visualization System (10 Modes)

Implemented comprehensive debug modes to diagnose the pipeline:

1. **DEBUG_MODE = 1**: Show screen texture at fragment position (no refraction)
   - **Result**: ✅ Shows bottles correctly aligned, confirms Pass 1 capture works

2. **DEBUG_MODE = 2**: Visualize surface normals
   - **Result**: Normal visualization works correctly

3. **DEBUG_MODE = 3**: Visualize view direction
   - **Result**: View direction correct

4. **DEBUG_MODE = 4**: Visualize refraction direction
   - **Result**: Refraction vector calculation appears correct

5. **DEBUG_MODE = 5**: Visualize transmission ray length
   - **Result**: Thickness calculation appears reasonable

6. **DEBUG_MODE = 6**: Visualize NDC coordinates
   - **Result**: 🔴 **CRITICAL FINDING**: NDC showed uniform yellow-green color that didn't change during rotation - indicating projected coordinates were "stuck"

7. **DEBUG_MODE = 7**: Visualize UV coordinates
   - **Result**: UVs covered full [0,1] range instead of being localized

8. **DEBUG_MODE = 8**: Visualize model scale extraction
   - **Result**: Model scale extraction appeared correct

9. **DEBUG_MODE = 9**: Visualize entry vs exit UV difference
   - **Result**: UV coordinates spanning entire [0,1] range

10. **DEBUG_MODE = 10**: Visualize NDC offset magnitude
    - **Result**: Bright cyan color indicating very large Y-offset

### Key Debug Findings

- ✅ Screen texture capture (Pass 1) works perfectly
- ✅ The refraction calculation math appears correct
- ❌ Projecting world-space positions to screen space creates offsets that don't stay aligned during rotation
- ❌ The offset magnitude is too large (DEBUG_MODE 10)
- ❌ The issue occurs identically on Metal and OpenGL, ruling out Y-flip as root cause

---

## Fundamental Issues Identified

### 1. Temporal Mismatch Problem

When the model rotates:
- The glass's world position changes in 3D space
- Projecting `refracted_ray_exit` uses the NEW position
- But `screen_tex` was captured with objects at their CURRENT positions
- This creates a frame-to-frame mismatch

### 2. Coordinate System Independence

The problem manifests **identically** on both Metal and OpenGL backends, which have opposite Y-axis conventions:
- **Metal/D3D**: Y-down (origin top-left)
- **OpenGL**: Y-up (origin bottom-left)

This proves the issue is NOT a simple coordinate system flip problem.

### 3. Projection Accuracy

Projecting `position` in the fragment shader doesn't match exactly where the vertex shader projected that vertex to `gl_FragCoord`. There are subtle differences in:
- Floating-point precision
- Interpolation across the triangle
- Matrix multiplication order in vertex vs fragment stages

---

## Why Each Approach Failed

### Approach 1 (Matrix Order): Failed
- Correct matrix order didn't solve the fundamental temporal mismatch
- Still projecting exit points that don't align with the captured background

### Approach 2 (gl_FragCoord Only): Failed User Requirements
- ✅ Perfect alignment (no projection)
- ❌ No refraction effect at all
- User: "I want refraction"

### Approach 3 (Screen-Space Normal Offset): Failed
- View-space normal offset doesn't account for actual 3D geometry
- Offset direction doesn't match physical refraction direction

### Approach 4 (Project Both Points): Failed
- Even projecting the entry point (position) doesn't match gl_FragCoord
- Fragment shader projection ≠ vertex shader projection result
- Offset calculation based on mismatched coordinates

### Approach 5 (gl_FragCoord Entry + Project Exit): Failed
- ✅ Entry point correct (gl_FragCoord)
- ❌ Exit point projection still creates misalignment during rotation
- The exit point world position doesn't correspond correctly to where Pass 1 rendered

### Approach 6 (No Y-Flip): Failed
- Removing Y-flip broke Metal backend
- Still had alignment issues on both backends

---

## Root Cause Analysis

### The Core Contradiction

There is a fundamental tension between two requirements:

1. **Alignment**: Requires sampling screen texture at the fragment's current screen position (gl_FragCoord)
2. **Refraction**: Requires applying an offset based on light bending through the glass (requires 3D calculation)

### Why 3D Projection Fails

The reference implementation (glTF-Sample-Renderer) works in WebGL with a simpler rendering model. In Sokol with two-pass rendering:

```
Pass 1: Render opaque objects → screen_tex at frame N
Pass 2: Render transparent objects
  - Glass fragment at world position P_glass (frame N)
  - Calculate exit point: P_exit = P_glass + refraction_ray
  - Project P_exit to screen: (x, y) = project(P_exit)
  - Sample screen_tex at (x, y)
  
Problem: When model rotates between frames:
  - P_glass moves to new position P_glass'
  - P_exit' = P_glass' + refraction_ray
  - Project(P_exit') gives DIFFERENT screen coordinates
  - But screen_tex still shows old frame's background
  - Result: MISALIGNMENT
```

### Why gl_FragCoord-Only Fails User Requirements

Using just `gl_FragCoord` gives perfect alignment but no refraction effect. The glass becomes a perfect flat window with no optical distortion.

---

## Possible Alternative Approaches (Not Attempted)

### 1. Screen-Space Refraction with Depth-Based Offset
- Use depth buffer to calculate how far behind the glass the background is
- Apply offset based on depth difference and surface normal
- May provide refraction effect while maintaining alignment

### 2. Post-Process Distortion Pass
- Render scene normally
- Apply refraction as a post-process effect using a distortion map
- Decouples refraction from the projection problem

### 3. Ray Marching Through Volume
- Instead of projecting exit points, ray march through the glass volume
- Sample at each step to accumulate color
- More expensive but physically accurate

### 4. Pre-Transform Refraction Coordinates
- Calculate refraction offset in world space BEFORE projection
- Transform coordinates differently to account for temporal alignment
- Complex mathematical derivation needed

### 5. Single-Pass with Depth Peeling
- Use depth peeling to separate front/back faces
- Calculate refraction within single render pass
- Avoids temporal mismatch between passes

---

## Lessons Learned

### 1. Reference Implementation Differences
The glTF-Sample-Renderer reference works in WebGL with different constraints:
- Single-pass rendering with different depth handling
- Different coordinate system conventions
- Simpler projection model

Directly translating their approach doesn't work in Sokol's architecture.

### 2. gl_FragCoord vs Projection
`gl_FragCoord` is the vertex shader's projection result. Attempting to replicate this in the fragment shader by projecting world positions creates subtle mismatches.

### 3. Cross-Backend Consistency
When an issue manifests identically on Metal and OpenGL (opposite Y-axis conventions), it's NOT a coordinate system problem - it's a fundamental algorithmic issue.

### 4. Two-Pass Rendering Constraints
Screen-space effects in multi-pass rendering require careful handling of temporal coherence. The background texture captured in Pass 1 must correspond exactly to where Pass 2 samples it.

---

## Current Status

**DEAD END REACHED**

After 6 major attempts and extensive debugging:
- ✅ Can achieve perfect alignment (no refraction)
- ✅ Can achieve refraction effect (misaligned)
- ❌ Cannot achieve BOTH alignment AND refraction simultaneously

The current two-pass world-space projection approach appears fundamentally incompatible with maintaining alignment during rotation.

---

## Recommendation

**STOP** the current approach and consider:

1. **Consult Sokol Community**: Ask if anyone has implemented screen-space refraction successfully
2. **Study Alternative Methods**: Research depth-based screen-space refraction techniques
3. **Simplify Requirements**: Perhaps accept simpler "frosty glass" effect instead of physical refraction
4. **Different Architecture**: Consider if a completely different rendering architecture is needed for this effect

The problem requires either:
- A different mathematical approach to refraction calculation
- A different rendering pipeline architecture
- Acceptance of limitations in the current system

---

## Files Modified During Attempts

1. **shaders/fs_functions.glsl**: `calculate_refraction()` function modified 6+ times
2. **docs/TRANSMISSION_DEBUG_PLAN.md**: Debug strategy documentation
3. **docs/TRANSMISSION_REFRACTION_FIX.md**: Original fix documentation (now outdated)
4. **docs/TRANSMISSION_COORDINATE_ISSUE.md**: Coordinate system analysis

---

## Technical Debt

The codebase now has:
- Multiple debug modes (1-10) that should be cleaned up
- Outdated documentation claiming the fix works
- Confusing history of coordinate system flip logic
- Inconsistent approach between different attempts

---

## Conclusion

This transmission/refraction implementation has reached an impasse. The fundamental architecture of projecting world-space exit points to screen-space coordinates in a two-pass rendering system creates alignment issues that cannot be resolved through coordinate system adjustments, matrix order changes, or hybrid approaches.

A successful solution will require either:
- A completely different approach to calculating refraction offsets
- A different rendering architecture
- Simplification of the visual effect requirements

**Current Status**: ❌ **BLOCKED - REQUIRES NEW APPROACH**
