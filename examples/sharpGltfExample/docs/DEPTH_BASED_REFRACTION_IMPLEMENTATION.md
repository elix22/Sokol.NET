# Depth-Based Screen-Space Refraction Implementation Plan

**Date**: November 5, 2025  
**Approach**: Screen-space refraction using depth buffer  
**Goal**: Achieve refraction effect while maintaining alignment during rotation

---

## Overview

Instead of projecting 3D world-space exit points (which causes misalignment), we'll use a **screen-space approach** that:

1. Uses `gl_FragCoord` as the base position (always aligned)
2. Calculates refraction offset in **view space** (camera-relative coordinates)
3. Uses **depth difference** to scale the offset appropriately
4. All calculations relative to screen position = no temporal mismatch

---

## Architecture

### Current Two-Pass System
```
Pass 1 (Opaque): Render background → screen_color_img
Pass 2 (Transparent): Render glass, sample screen_color_img with offset
```

### New Enhanced System
```
Pass 1 (Opaque): Render background → screen_color_img + screen_depth_img
Pass 2 (Transparent): Render glass, use depth to calculate refraction offset
```

---

## Implementation Steps

### Step 1: Update Shader - Add Depth Texture Parameter ✓

**File**: `shaders/cgltf-sapp.glsl`

**Changes**:
- Add `texture2D screen_depth_tex` parameter to fragment shader
- Add `sampler screen_depth_smp` parameter
- Pass depth texture to `calculate_refraction()`

**Why**: Shader needs access to depth buffer to calculate depth-based offsets

---

### Step 2: Update Shader - Implement Depth-Based Refraction ✓

**File**: `shaders/fs_functions.glsl`

**Changes**:
Replace the projection-based refraction calculation with screen-space approach:

```glsl
vec3 calculate_refraction(
    // ... existing parameters ...
    texture2D screen_tex, sampler screen_smp,
    texture2D screen_depth_tex, sampler screen_depth_smp,  // NEW
    mat4 model_mat, mat4 view_mat, mat4 proj_mat) {
    
    // 1. Get fragment's screen UV (always aligned)
    vec2 screen_size = vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));
    vec2 screen_uv = gl_FragCoord.xy / screen_size;
    
    // 2. Sample background depth
    float bg_depth = texture(sampler2D(screen_depth_tex, screen_depth_smp), screen_uv).r;
    
    // 3. Calculate refraction direction in VIEW SPACE
    vec3 view_normal = normalize((view_mat * vec4(normal, 0.0)).xyz);
    vec3 view_dir = normalize((view_mat * vec4(view, 0.0)).xyz);
    vec3 refract_dir_view = refract(-view_dir, view_normal, 1.0 / ior);
    
    // 4. Calculate depth difference (how far behind glass is background)
    float glass_depth = gl_FragCoord.z;
    float depth_diff = bg_depth - glass_depth;
    
    // 5. Screen-space offset scaled by depth and refraction strength
    vec2 refraction_offset = refract_dir_view.xy * depth_diff * refraction_scale;
    
    // 6. Apply offset to screen UV
    vec2 refracted_uv = screen_uv + refraction_offset;
    refracted_uv = clamp(refracted_uv, vec2(0.0), vec2(1.0));
    
    // 7. Sample with offset
    vec3 transmitted_light = texture(sampler2D(screen_tex, screen_smp), refracted_uv).rgb;
    
    // 8. Apply Beer's law and base color
    // ... existing attenuation code ...
}
```

**Why**: This approach stays in screen space, avoiding 3D projection issues

---

### Step 3: Update C# - Create Depth Attachment for Pass 1 ✓

**File**: `Source/Frame.cs` or `Source/Transmission.cs`

**Changes**:
Modify offscreen pass to include depth attachment:

```csharp
// Create depth image for transmission pass
var depth_img_desc = new sg_image_desc {
    render_target = true,
    width = sapp_width(),
    height = sapp_height(),
    pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
    sample_count = 1
};
state.transmission.opaque_depth_img = sg_make_image(ref depth_img_desc);

// Create pass with depth attachment
var pass_desc = new sg_pass_desc {
    color_attachments = new sg_pass_attachment_desc[] {
        new sg_pass_attachment_desc { image = state.transmission.opaque_color_img }
    },
    depth_stencil_attachment = new sg_pass_attachment_desc { 
        image = state.transmission.opaque_depth_img 
    }
};
state.transmission.opaque_pass = sg_make_pass(ref pass_desc);
```

**Why**: Need to capture depth buffer during Pass 1

---

### Step 4: Update C# - Bind Depth Texture in Pass 2 ✓

**File**: `Source/Frame.cs` - in the transparent rendering section

**Changes**:
When rendering transparent objects, bind depth texture:

```csharp
// Bind both color and depth textures for refraction
var bindings = new sg_bindings {
    vertex_buffers = { ... },
    index_buffer = ...,
    fs = {
        images = {
            [0] = state.transmission.opaque_color_img,  // screen_tex
            [1] = state.transmission.opaque_depth_img   // screen_depth_tex (NEW)
        },
        samplers = {
            [0] = state.transmission.screen_sampler,    // screen_smp
            [1] = state.transmission.depth_sampler      // screen_depth_smp (NEW)
        }
    }
};
```

**Why**: Shader needs both color and depth to calculate refraction

---

### Step 5: Update C# - Create Depth Sampler ✓

**File**: `Source/Transmission.cs` or `Source/Init.cs`

**Changes**:
Create sampler for depth texture:

```csharp
var depth_smp_desc = new sg_sampler_desc {
    min_filter = sg_filter.SG_FILTER_LINEAR,
    mag_filter = sg_filter.SG_FILTER_LINEAR,
    wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
    wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE
};
state.transmission.depth_sampler = sg_make_sampler(ref depth_smp_desc);
```

**Why**: Need proper sampling for depth buffer

---

### Step 6: Tune Refraction Parameters ✓

**File**: `shaders/fs_functions.glsl`

**Changes**:
Add tunable parameters for refraction strength:

```glsl
// Refraction strength based on IOR
float refraction_strength = (ior - 1.0) * 0.1; // Scale factor to tune

// Apply to offset calculation
vec2 refraction_offset = refract_dir_view.xy * depth_diff * refraction_strength;
```

**Why**: Need to balance refraction visibility with alignment

---

## Expected Results

### What Should Work
- ✅ Perfect alignment during rotation (using gl_FragCoord base)
- ✅ Visible refraction effect (depth-scaled offset)
- ✅ Physically motivated (based on depth difference)
- ✅ Works on all backends (screen-space approach)

### What to Test
1. Rotate model - bottles should stay aligned
2. Move camera - refraction should adjust naturally
3. Different IOR values - stronger IOR = more offset
4. Grazing angles - refraction should be stronger

---

## Debug Modes

Add debug visualizations to verify each stage:

```glsl
#define DEBUG_MODE 0

// 0 = Normal rendering
// 1 = Show depth buffer (grayscale)
// 2 = Show depth difference (glass to background)
// 3 = Show refraction offset magnitude
// 4 = Show view-space normal
// 5 = Show refraction direction
```

---

## Advantages Over Previous Approach

| Aspect | Old (3D Projection) | New (Depth-Based) |
|--------|---------------------|-------------------|
| Alignment | ❌ Misaligns during rotation | ✅ Always aligned |
| Coordinate Space | World → Screen projection | Screen-space only |
| Temporal Coherence | ❌ Frame mismatch | ✅ Single frame |
| Complexity | High (matrix multiplications) | Low (view-space vectors) |
| Backend Compatibility | ⚠️ Y-flip issues | ✅ Consistent |

---

## Potential Issues & Solutions

### Issue 1: Depth Values May Be Non-Linear
**Problem**: Depth buffer stores non-linear depth (1/z distribution)  
**Solution**: May need to linearize depth before calculating difference

```glsl
float linearize_depth(float depth, float near, float far) {
    float z_ndc = depth * 2.0 - 1.0;
    return (2.0 * near * far) / (far + near - z_ndc * (far - near));
}
```

### Issue 2: Offset May Be Too Small/Large
**Problem**: Refraction not visible or too extreme  
**Solution**: Add tunable scale parameter, adjust per-material

### Issue 3: Edge Artifacts
**Problem**: Refraction sampling outside screen bounds  
**Solution**: Clamp UVs to [0,1] range (already included)

---

## Rollback Plan

If this approach doesn't work:
1. Keep depth buffer visualization for debugging
2. Document what didn't work and why
3. Consider hybrid approach (depth + small fixed offset)
4. Or fall back to simple distortion (no physical basis)

---

## Success Criteria

✅ **Must Have**:
- Bottles stay aligned during rotation
- Visible refraction/distortion effect
- Works on Metal and OpenGL

✅ **Nice to Have**:
- Physically plausible refraction
- Scales properly with IOR
- Adjustable refraction strength

---

## Implementation Order

1. ✅ Update shader signature (add depth texture params)
2. ✅ Implement new refraction calculation in `fs_functions.glsl`
3. ✅ Create depth attachment in C# transmission setup
4. ✅ Bind depth texture during Pass 2 rendering
5. ✅ Create depth sampler
6. ✅ Test and tune refraction strength
7. ✅ Add debug modes for troubleshooting
8. ✅ Document results

---

## Files to Modify

- `shaders/cgltf-sapp.glsl` - Add depth texture parameter
- `shaders/fs_functions.glsl` - Implement depth-based refraction
- `Source/Transmission.cs` (or init code) - Create depth resources
- `Source/Frame.cs` - Bind depth texture, configure pass
- `Source/State.cs` - Add depth image/sampler fields (if needed)

---

## Next Steps

After implementation:
1. Test with CommercialRefrigerator.gltf
2. Verify alignment during rotation
3. Tune refraction strength parameter
4. Test with other transmission models
5. Document final results
