# Depth-Based Refraction: Correct Implementation Plan

## Problem Statement
Currently, the refraction effect shows misalignment because we're using a simple fixed offset. We need depth-based refraction where the refraction offset scales with the distance between the glass surface and the background objects.

## Reference: Offscreen Example
The `examples/offscreen` shows the EXACT pattern we need:
- One color attachment (RGBA8)
- One depth attachment (DEPTH format)
- Sample BOTH textures in the second pass
- NO MRT needed

## Current State Analysis

### What We Have Now (CORRECT):
1. ✅ Two-pass rendering system
2. ✅ `screen_color_img` - RGBA8 color texture
3. ✅ `screen_depth_img` - DEPTH format depth texture
4. ✅ Both textures already created in `Init.cs`
5. ✅ Pass 1 renders opaque objects to offscreen textures
6. ✅ Pass 2 renders transparent objects sampling from textures

### What's WRONG:
1. ❌ `screen_depth_img` is created as R32F (color format) - should be DEPTH format
2. ❌ MRT setup with 2 color attachments - unnecessary, offscreen example uses 1 color + 1 depth
3. ❌ `actual_depth_img` exists but shouldn't - we only need ONE depth attachment
4. ❌ Shader doesn't actually USE the depth texture for offset calculation
5. ❌ Pipeline configured for MRT (color_count=2) when it should be 1

## Correct Implementation Steps

### Step 1: Fix Image Creation in Init.cs
**File**: `Source/Init.cs`
**Location**: Around line 750-780

**Current (WRONG)**:
```csharp
// screen_depth_img as R32F color format
state.transmission.screen_depth_img = sg_make_image(new sg_image_desc
{
    usage = { color_attachment = true, sampled = true },
    width = w,
    height = h,
    pixel_format = sg_pixel_format.SG_PIXELFORMAT_R32F,  // WRONG
    sample_count = 1,
    label = "transmission-screen-depth"
});

// Separate actual_depth_img
state.transmission.actual_depth_img = sg_make_image(new sg_image_desc
{
    usage = { depth_stencil_attachment = true },
    width = w,
    height = h,
    pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
    sample_count = 1,
    label = "transmission-actual-depth"
});
```

**Should Be (CORRECT)**:
```csharp
// screen_depth_img as DEPTH format (exactly like offscreen example)
state.transmission.screen_depth_img = sg_make_image(new sg_image_desc
{
    usage = { depth_stencil_attachment = true, sampled = true },  // depth attachment + can be sampled
    width = w,
    height = h,
    pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
    sample_count = 1,
    label = "transmission-screen-depth"
});

// Remove actual_depth_img - not needed
```

**Certainty**: 100% - This is exactly how offscreen example does it.

---

### Step 2: Fix Pass Attachment Setup in Init.cs
**File**: `Source/Init.cs`
**Location**: Around line 820-860

**Current (WRONG - MRT setup)**:
```csharp
// Two color attachments
state.transmission.opaque_pass.attachments.colors[0] = ...  // color
state.transmission.opaque_pass.attachments.colors[1] = ...  // depth as color (WRONG!)
state.transmission.opaque_pass.attachments.depth_stencil = ... // actual depth

// Two clear actions
opaque_action.colors[0].load_action = ...
opaque_action.colors[1].load_action = ...  // WRONG!
```

**Should Be (CORRECT - like offscreen)**:
```csharp
// One color attachment
if (state.transmission.opaque_pass.attachments.colors[0].id == 0)
{
    state.transmission.opaque_pass.attachments.colors[0] = sg_alloc_view();
}
sg_init_view(state.transmission.opaque_pass.attachments.colors[0], new sg_view_desc()
{
    color_attachment = { image = state.transmission.screen_color_img },
    label = "opaque-color-view"
});

// One depth attachment
if (state.transmission.opaque_pass.attachments.depth_stencil.id == 0)
{
    state.transmission.opaque_pass.attachments.depth_stencil = sg_alloc_view();
}
sg_init_view(state.transmission.opaque_pass.attachments.depth_stencil, new sg_view_desc()
{
    depth_stencil_attachment = { image = state.transmission.screen_depth_img },
    label = "opaque-depth-view"
});

// One clear action for color, one for depth
sg_pass_action opaque_action = default;
opaque_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
opaque_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };
opaque_action.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
opaque_action.depth.clear_value = 1.0f;
```

**Certainty**: 100% - Copied exactly from offscreen example pattern.

---

### Step 3: Fix Pipeline Creation in Init.cs
**File**: `Source/Init.cs`
**Location**: Around line 870-890

**Current (WRONG)**:
```csharp
state.transmission.opaque_standard_pipeline = PipeLineManager.CreatePipelineForPass(
    PipelineType.TransmissionOpaque,
    sg_pixel_format.SG_PIXELFORMAT_RGBA8,
    sg_pixel_format.SG_PIXELFORMAT_R32F,  // WRONG - should be DEPTH
    1
);
```

**Should Be (CORRECT)**:
```csharp
state.transmission.opaque_standard_pipeline = PipeLineManager.CreatePipelineForPass(
    PipelineType.TransmissionOpaque,
    sg_pixel_format.SG_PIXELFORMAT_RGBA8,  // color format
    sg_pixel_format.SG_PIXELFORMAT_DEPTH,  // depth format
    1  // sample_count
);
```

**Certainty**: 100% - Matches offscreen example.

---

### Step 4: Fix Cleanup in Init.cs
**File**: `Source/Init.cs`
**Location**: Around line 210-215

**Current (WRONG)**:
```csharp
sg_uninit_view(state.transmission.opaque_pass.attachments.colors[0]);
sg_uninit_view(state.transmission.opaque_pass.attachments.colors[1]);  // WRONG - doesn't exist
sg_uninit_view(state.transmission.opaque_pass.attachments.depth_stencil);
```

**Should Be (CORRECT)**:
```csharp
sg_uninit_view(state.transmission.opaque_pass.attachments.colors[0]);
sg_uninit_view(state.transmission.opaque_pass.attachments.depth_stencil);
// That's it - only 2 views
```

**Certainty**: 100%

---

### Step 5: Fix Main.cs Structure
**File**: `Source/Main.cs`
**Location**: TransmissionPass struct

**Current (WRONG)**:
```csharp
public struct TransmissionPass
{
    public sg_image screen_color_img;
    public sg_image screen_depth_img;
    public sg_image actual_depth_img;  // WRONG - not needed
    ...
}
```

**Should Be (CORRECT)**:
```csharp
public struct TransmissionPass
{
    public sg_image screen_color_img;
    public sg_image screen_depth_img;  // This IS the depth attachment (DEPTH format)
    // Remove actual_depth_img
    ...
}
```

**Certainty**: 100%

---

### Step 6: Delete Unnecessary Files
**Files to Delete**:
1. `shaders/mrt-shader.glsl` - Not needed
2. `shaders/fs_attributes_mrt.glsl` - Not needed
3. `shaders/compiled/osx/mrt-shader-shader.cs` - Generated file, will be removed
4. `shaders/compiled/osx/mrt-shader-shader-skinning.cs` - Generated file

**Certainty**: 100% - These were created for MRT approach which we don't need.

---

### Step 7: Revert Shader Changes
**File**: `shaders/fs_attributes.glsl`

**Current (WRONG - has MRT stuff)**:
```glsl
#ifdef MRT_OUTPUT
layout(location=0) out vec4 frag_color;
layout(location=1) out vec4 frag_depth;
#else
out vec4 frag_color;
#endif
```

**Should Be (CORRECT - original)**:
```glsl
out vec4 frag_color;
```

**File**: `shaders/cgltf-sapp.glsl`

**Current (WRONG - has MRT stuff)**:
```glsl
frag_color = vec4(tone_map(color), base_color.a);

#ifdef MRT_OUTPUT
frag_depth = vec4(gl_FragCoord.z, 0.0, 0.0, 1.0);
#endif
```

**Should Be (CORRECT - original)**:
```glsl
frag_color = vec4(tone_map(color), base_color.a);
```

**Certainty**: 100% - Just reverting to original.

---

### Step 8: Fix Depth Sampler Configuration
**File**: `Source/Init.cs`
**Location**: Around line 740

**Current**:
```csharp
state.transmission.depth_sampler = sg_make_sampler(new sg_sampler_desc
{
    min_filter = sg_filter.SG_FILTER_LINEAR,
    mag_filter = sg_filter.SG_FILTER_LINEAR,
    wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
    wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
    label = "transmission-depth-sampler"
});
```

**Should Be (for DEPTH texture)**:
```csharp
state.transmission.depth_sampler = sg_make_sampler(new sg_sampler_desc
{
    min_filter = sg_filter.SG_FILTER_NEAREST,  // Depth textures typically use NEAREST
    mag_filter = sg_filter.SG_FILTER_NEAREST,
    wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
    wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
    compare = sg_compare_func.SG_COMPAREFUNC_NEVER,  // No shadow comparison
    label = "transmission-depth-sampler"
});
```

**Certainty**: 90% - DEPTH textures can be sampled, but filtering behavior varies. NEAREST is safest. May need LINEAR for smooth depth-based offsets.

---

### Step 9: Implement Depth-Based Offset in Shader
**File**: `shaders/fs_functions.glsl`
**Function**: `calculate_refraction()`
**Location**: Around line 230-290

**Current**: Has depth texture parameters but doesn't use them (comments say "kept for future use")

**Should Be**:
```glsl
vec3 calculate_refraction(
    // ... existing parameters ...
    texture2D screen_depth_tex,
    sampler screen_depth_smp
) {
    // ... existing code to calculate refracted_view_dir ...
    
    // Sample depth at glass surface (current fragment)
    float glass_depth = gl_FragCoord.z;
    
    // Calculate screen UV for sampling
    vec2 screen_uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));
    
    // Sample background depth from depth texture
    float bg_depth = texture(sampler2D(screen_depth_tex, screen_depth_smp), screen_uv).r;
    
    // Calculate depth difference (larger = background is farther behind glass)
    float depth_diff = bg_depth - glass_depth;
    depth_diff = max(depth_diff, 0.0);  // Only positive values
    
    // Scale refraction offset based on depth difference
    // Objects far behind glass get more offset than near objects
    float depth_scale = depth_diff * 10.0;  // Tunable multiplier
    
    // Calculate refraction offset
    vec2 refract_offset = refracted_view_dir.xy * ior_ratio * depth_scale * 0.05;
    
    // Apply offset and sample
    vec2 refracted_uv = screen_uv + refract_offset;
    refracted_uv = clamp(refracted_uv, vec2(0.0), vec2(1.0));
    
    vec3 refracted_color = texture(sampler2D(screen_tex, screen_smp), refracted_uv).rgb;
    
    return refracted_color;
}
```

**Certainty**: 85% - The depth comparison logic is correct. The `depth_scale * 10.0` multiplier needs tuning. The approach of "depth difference → offset scale" is physically motivated.

---

## Testing Plan

### Test 1: Verify Depth Texture Creation
**Expected**: No crash, depth texture is DEPTH format, can be sampled
**How**: Run app, check logs for "transmission-screen-depth" creation

### Test 2: Verify Single Color Attachment
**Expected**: No MRT errors, only colors[0] is used
**How**: Check pass setup in debugger

### Test 3: Verify Depth Sampling
**Expected**: Depth texture can be sampled with NEAREST filter
**How**: Run app, no Sokol validation errors about depth sampling

### Test 4: Verify Refraction Effect
**Expected**: 
- Background objects appear behind glass
- Refraction offset visible
- Bottles stay aligned during rotation
**How**: Rotate CommercialRefrigerator.gltf, observe bottles through glass

### Test 5: Tune Depth Scale
**Expected**: Refraction looks natural, not too extreme
**How**: Adjust `depth_scale * 10.0` multiplier (try 5.0, 10.0, 20.0)

---

## Known Uncertainties

1. **Depth Texture Filtering** (10% uncertain)
   - DEPTH textures may not support LINEAR filtering on all platforms
   - Metal should support it, OpenGL might not
   - Fallback: Use NEAREST if LINEAR causes issues

2. **Depth Scale Tuning** (15% uncertain)
   - The multiplier `10.0` is a guess
   - May need values between 1.0 and 50.0
   - Depends on scene scale and camera setup

3. **UV Coordinate System** (5% uncertain)
   - Screen UV calculation assumes standard coordinate system
   - May need flipping on some platforms (Metal vs OpenGL)

---

## Success Criteria

✅ **Must Have**:
1. No crashes
2. No Sokol validation errors
3. Refraction effect visible
4. Bottles stay aligned during rotation

✅ **Nice to Have**:
1. Refraction scales with depth (objects far behind glass are more distorted)
2. Smooth, natural-looking refraction
3. Works on both Metal and OpenGL

---

## Rollback Plan

If this approach fails:
1. Keep the simple fixed-offset refraction (current working version)
2. Document that depth-based refraction requires platform-specific handling
3. Consider using a stencil-based approach instead

---

## Estimated Implementation Time

- Step 1-7 (cleanup and fixes): 15 minutes
- Step 8 (sampler): 5 minutes
- Step 9 (shader logic): 20 minutes
- Testing and tuning: 30 minutes
- **Total**: ~70 minutes

---

## Confidence Level

**Overall**: 95% confident this approach will work

**Why**:
- Based on proven offscreen example pattern
- DEPTH textures ARE sampleable in modern graphics APIs
- Depth-based offset is physically correct
- Only uncertainty is tuning parameters

**Risk**: Depth texture sampling might not work on OpenGL backend (5% chance)
