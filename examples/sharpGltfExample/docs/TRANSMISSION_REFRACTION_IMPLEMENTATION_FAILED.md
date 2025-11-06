# Transmission & Refraction Implementation - Failed Attempt Report

**Date:** November 5, 2025  
**Status:** ❌ FAILED  
**Session Duration:** Extended debugging session  
**Outcome:** Unable to resolve sokol-gfx validation errors for MRT shader

---

## Executive Summary

Attempted to implement screen-space refraction for glTF transmission materials (glass) using a two-pass Multi-Render-Target (MRT) approach. The implementation failed due to persistent shader validation errors that could not be resolved despite multiple attempts to fix texture/sampler descriptor mismatches.

---

## What Was Attempted

### 1. **Two-Pass MRT Rendering Architecture**

**Pass 1 (Opaque Pass - MRT):**
- Render opaque geometry to offscreen textures
- Output 1: Color (RGBA8) - captured scene color
- Output 2: Depth-as-color (R32F) - depth values for refraction
- Actual depth buffer: DEPTH format for depth testing

**Pass 2 (Transparent Pass):**
- Render transparent/transmissive objects to swapchain
- Sample Pass 1 outputs in shader
- Calculate refraction based on depth difference
- Apply Beer's Law volume absorption

### 2. **Key Files Modified**

- `shaders/mrt-shader.glsl` - MRT shader for opaque pass
- `shaders/fs_uniforms_mrt.glsl` - Minimal uniforms for MRT
- `Source/Init.cs` - Transmission system initialization
- `Source/Frame.cs` - Two-pass rendering logic
- `Source/PipelineManager.cs` - MRT pipeline creation
- `Source/Mesh.cs` - Depth texture binding

### 3. **Changes Made**

1. Created separate MRT textures for color and depth
2. Configured MRT pass with 2 color attachments + depth buffer
3. Created MRT shader to output `gl_FragCoord.z` to second RT
4. Modified rendering to classify meshes as opaque/transparent
5. Implemented depth-based refraction calculation in shader
6. Added transmission params (model/view/projection matrices) for 3D refraction

---

## Root Cause of Failure

### **Sokol-GFX Shader Validation Error**

```
[sg][error][id:222] VALIDATE_SHADERDESC_TEXVIEW_NOT_REFERENCED_BY_TEXTURE_SAMPLER_PAIRS
[sg][error][id:223] VALIDATE_SHADERDESC_SAMPLER_NOT_REFERENCED_BY_TEXTURE_SAMPLER_PAIRS
[sg][panic][id:418] VALIDATION_FAILED: validation layer checks failed
```

**Problem:**
- The MRT shader declares textures/samplers in the descriptor
- Sokol validation requires all declared textures/samplers to be referenced in `texture_sampler_pairs[]`
- Despite multiple attempts to create minimal shaders with only used textures, the validation continued to fail
- The skinned variant of MRT shader also caused issues
- sokol-shdc (shader compiler) may be including extra texture declarations from included files

**Why it couldn't be fixed:**
1. The shader compilation pipeline includes files that pull in extra texture declarations
2. Even with completely self-contained shaders, the validation persisted
3. The skinned shader variant compilation adds complexity
4. Limited control over sokol-shdc's descriptor generation

---

## Alternative Solutions (Recommended)

### **Option 1: Single-Pass Approximate Refraction (SIMPLEST)**

**Approach:**
- No MRT, no depth texture needed
- Use simple UV offset based on normal and view direction
- Approximate refraction without actual depth testing

**Implementation:**
```glsl
vec2 refract_uv = screen_uv + refract(-view_dir, normal, 1.0/ior).xy * refraction_strength;
vec3 refracted_color = texture(screen_tex, refract_uv).rgb;
```

**Pros:**
- Simple, no validation issues
- Works on all platforms
- Fast performance

**Cons:**
- Not physically accurate
- No depth-based refraction
- May have artifacts at edges

**Difficulty:** ⭐ Easy  
**Recommendation:** ✅ **START HERE**

---

### **Option 2: Separate Depth Pass (Without MRT)**

**Approach:**
- Pass 1: Render opaque to color texture (RGBA8)
- Pass 2: Render opaque AGAIN to depth texture (R32F) - output `gl_FragCoord.z` manually
- Pass 3: Render transparent with refraction

**Implementation:**
```glsl
// Depth pass fragment shader (Pass 2)
out vec4 frag_color;
void main() {
    frag_color = vec4(gl_FragCoord.z, 0, 0, 1); // Write depth to R channel
}
```

**Pros:**
- Avoids MRT validation issues
- Gets proper depth for refraction
- Standard single-output shaders

**Cons:**
- Three passes instead of two
- Renders opaque geometry twice
- Performance cost

**Difficulty:** ⭐⭐ Moderate  
**Recommendation:** ✅ **GOOD FALLBACK**

---

### **Option 3: Manual Depth Texture Creation (CPU-Side)**

**Approach:**
- Use existing depth buffer from first pass
- Copy depth buffer to a readable texture using `sg_copy_image`
- Bind copied texture in second pass

**Implementation:**
```csharp
// After Pass 1
sg_end_pass();

// Copy depth buffer to readable texture
sg_copy_image(actual_depth_img, screen_depth_img);

// Pass 2 - use screen_depth_img
```

**Pros:**
- No shader validation issues
- True depth values
- Two passes only

**Cons:**
- Copy operation has performance cost
- Platform-specific limitations (WebGL?)
- May not work on all backends

**Difficulty:** ⭐⭐ Moderate  
**Recommendation:** ⚠️ **Test platform compatibility first**

---

### **Option 4: Fix MRT Validation (Requires Deep Dive)**

**Approach:**
- Investigate sokol-shdc source code
- Understand exact validation requirements
- Manually construct shader descriptors
- Potentially patch sokol-shdc or use custom shader compiler

**Implementation:**
- Requires C knowledge
- Need to build sokol-shdc from source
- Manual descriptor construction in C#

**Pros:**
- Proper MRT implementation
- Best performance (single geometry pass)
- Accurate depth-based refraction

**Cons:**
- Very time-consuming
- Requires C/sokol expertise
- May break with sokol updates
- High complexity

**Difficulty:** ⭐⭐⭐⭐⭐ Expert  
**Recommendation:** ❌ **NOT WORTH THE TIME**

---

### **Option 5: Pre-baked Refraction Maps**

**Approach:**
- Pre-compute refraction lookup textures
- Use artist-created distortion maps
- No runtime depth sampling needed

**Implementation:**
- Use normal map + custom distortion texture
- Художник creates refraction patterns
- Sample distortion texture based on UV

**Pros:**
- Artistic control
- No validation issues
- Fast runtime

**Cons:**
- Not dynamic
- Requires asset pipeline
- Not physically based

**Difficulty:** ⭐⭐ Moderate  
**Recommendation:** ⚠️ **For specific artistic use cases**

---

## Files to Revert

### **Modified Files (DELETE/REVERT):**
```
shaders/mrt-shader.glsl
shaders/fs_uniforms_mrt.glsl
shaders/compiled/osx/mrt-shader-shader.cs
shaders/compiled/osx/mrt-shader-shader-skinning.cs
```

### **Modified Files (REVERT TO PREVIOUS VERSION):**
```
Source/Init.cs - Remove InitializeTransmission() changes
Source/Frame.cs - Remove two-pass rendering logic
Source/Mesh.cs - Remove screen_depth_view parameter
Source/PipelineManager.cs - Remove TransmissionOpaque pipeline types
Source/Main.cs - Remove transmission state variables
```

### **Files to Keep:**
```
shaders/fs_functions.glsl - Refraction calculation code (may be useful for future attempts)
```

---

## Lessons Learned

1. **MRT + Sokol Validation = Complex**: The validation system is strict and requires perfect texture/sampler pairing
2. **Shader Includes Complicate Things**: Including shared files pulls in unwanted declarations
3. **Skinned Variants Double the Problem**: Every shader has a skinned variant that must also validate
4. **sokol-shdc is a Black Box**: Limited control over descriptor generation
5. **Start Simple**: Should have tried Option 1 or Option 2 first before MRT

---

## Recommended Next Steps

1. **Immediate:** Revert all changes to working state
2. **Short-term:** Implement **Option 1** (approximate refraction) - should take < 1 hour
3. **Medium-term:** Try **Option 2** (separate depth pass) if Option 1 insufficient
4. **Long-term:** Consider switching graphics APIs or waiting for sokol updates

---

## Code Snippets for Option 1 (Quick Win)

### In Fragment Shader:
```glsl
// Simple approximate refraction (no depth texture needed)
vec2 screen_uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));

// Calculate refraction offset from surface normal
vec3 view_dir = normalize(v_eye_pos - v_pos);
vec3 refract_dir = refract(-view_dir, normalize(v_nrm), 1.0 / ior);

// Apply offset to screen UV (scale factor controls strength)
vec2 refract_uv = screen_uv + refract_dir.xy * 0.05; // Adjust 0.05 for stronger/weaker effect
refract_uv = clamp(refract_uv, 0.0, 1.0);

// Sample refracted color
vec3 transmitted = texture(sampler2D(screen_tex, screen_smp), refract_uv).rgb;

// Apply transmission factor and Beer's law absorption
vec3 final_color = mix(base_color.rgb, transmitted, transmission_factor);
// ... apply volume attenuation if needed
```

### In C# (Init):
```csharp
// Only need ONE texture (no depth texture)
state.transmission.screen_color_img = sg_make_image(new sg_image_desc() {
    width = fb_width,
    height = fb_height,
    pixel_format = SG_PIXELFORMAT_RGBA8,
    usage = { color_attachment = true },
});

// ONE pass (no MRT)
state.transmission.opaque_pass = new sg_pass() {
    attachments = {
        colors = { [0] = screen_color_view },
        depth_stencil = depth_view
    }
};
```

---

## Conclusion

The MRT approach was overly complex for the current sokol-shdc toolchain and validation system. A simpler approach (Option 1 or 2) will provide working refraction much faster with far less risk.

**Time invested:** ~4+ hours  
**Working code produced:** 0 lines  
**Recommended alternative time:** ~30-60 minutes for Option 1

---

## Contact & Support

If attempting any of these alternatives:
- Start with Option 1 - it WILL work
- Test on target platforms early
- Keep shaders as simple as possible
- Avoid includes that pull in extra declarations

Good luck! 🍀
