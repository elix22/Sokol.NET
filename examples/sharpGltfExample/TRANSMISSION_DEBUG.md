# Transmission Refraction Debug Log

## Problem Statement
Glass transmission on ChronographWatch.glb shows "watch inside watch" effect - the interior rotates when rotating the watch, instead of staying stationary.

## Root Cause Analysis

### What SHOULD Happen
When looking through glass with refraction:
1. Light rays bend at the glass surface (Snell's law)
2. The refracted ray samples a different part of the background
3. As the glass rotates, the refraction angle changes relative to the viewer
4. **The interior SHOULD rotate with the glass** because the glass surface orientation changes

### What User Reports
"Interior rotates when rotating the watch" - this might actually be CORRECT behavior!

### Key Insight
**The refraction should change as the surface normal rotates!** When you rotate a glass object:
- The surface normal rotates in world space
- The angle between view direction and normal changes
- Therefore, the refraction direction SHOULD change
- The sampled background position SHOULD move

## Visual Comparison - User Screenshots

### My Implementation (INCORRECT):
Looking at screenshots from angles:
- Front view: Watch face visible through glass
- Side views: As watch rotates, the interior (watch face, hands, dial) ROTATES with the view
- **Problem**: Interior appears to spin/rotate when rotating the watch model
- **Effect**: "Watch inside a watch" - the interior doesn't stay fixed to the watch geometry

### Reference Viewer (CORRECT):
Looking at glTF-Sample-Viewer screenshots:
- Front view: Watch face visible through glass
- Side views: As watch rotates, the interior (watch face, hands, dial) STAYS STATIONARY
- **Correct behavior**: Interior stays locked to the watch geometry
- **Effect**: The glass visor shows refraction distortion, but the interior doesn't spin

### Key Difference
**Reference viewer**: Interior stays fixed to watch body, only refraction changes
**My implementation**: Interior rotates/spins independently, creating "watch inside watch" effect

This CONFIRMS the behavior is WRONG - it's not correct physics. The interior should stay fixed to the watch geometry!

## Attempt History

### Attempt 1: Original World-Space Implementation
**Date**: Initial implementation  
**Approach**: 
```glsl
vec3 refractionVector = refract(-v, normalize(n), 1.0 / ior);
vec3 transmissionRay = normalize(refractionVector) * thickness;
vec3 refractedRayExit = v_Position + transmissionRay;
vec4 ndcPos = u_ProjectionMatrix * u_ViewMatrix * vec4(refractedRayExit, 1.0);
```
**Result**: "Glass is opaque"  
**Why it failed**: Missing transmission shader logic entirely

### Attempt 2: Added Transmission Logic
**Date**: After opacity bug  
**Approach**: Added complete Snell's law + Beer's law  
**Result**: "Interior visible but upside down"  
**Why it failed**: Metal backend Y-coordinate flip issue

### Attempt 3: Added Metal Y-Flip
**Date**: After upside-down bug  
**Approach**: Added `#if !SOKOL_GLSL screenUV.y = 1.0 - screenUV.y;`  
**Result**: "Interior rotates with watch"  
**Why it failed**: This might be CORRECT behavior! Need clarification.

### Attempt 4: View-Space Refraction
**Date**: After analyzing coordinate spaces  
**Approach**: 
```glsl
vec3 viewSpaceNormal = normalize((u_ViewMatrix * vec4(n, 0.0)).xyz);
vec3 viewSpacePos = (u_ViewMatrix * vec4(v_Position, 1.0)).xyz;
vec3 viewSpaceViewDir = normalize(-viewSpacePos);
vec3 refractionVector = refract(-viewSpaceViewDir, viewSpaceNormal, 1.0 / ior);
vec3 transmissionRay = normalize(refractionVector) * thickness;
vec3 refractedRayExit = viewSpacePos + transmissionRay;
vec4 ndcPos = u_ProjectionMatrix * vec4(refractedRayExit, 1.0);
```
**Result**: Interior STILL rotates (identical to world-space!)  
**Why it failed**: Both view-space and world-space produce same wrong result - problem must be elsewhere

### Attempt 5: Matched Reference Implementation EXACTLY
**Date**: After reading reference code carefully  
**Discovery**: Reference uses world-space throughout, with model matrix scale for thickness  
**Approach**: Cleaned up to match reference format exactly:
```glsl
// World-space refraction (n and v already in world space)
vec3 refractionVector = refract(-v, normalize(n), 1.0 / ior);
// NOTE: Reference multiplies thickness by model scale, but we don't have model matrix
vec3 transmissionRay = normalize(refractionVector) * thickness;
vec3 refractedRayExit = v_Position + transmissionRay;
// Project world-space to screen (matching reference: proj * view * worldPos)
vec4 ndcPos = u_ProjectionMatrix * u_ViewMatrix * vec4(refractedRayExit, 1.0);
vec2 screenUV = ndcPos.xy / ndcPos.w;
screenUV = screenUV * 0.5 + 0.5;
```
**Key changes**: 
- Made matrix multiplication explicit to match reference exactly
- Added clear comments about world space
- Noted missing model matrix scale (potential issue?)
**Result**: Interior STILL rotates (identical to previous attempts) ❌

### Attempt 6: DEBUG - Direct Framebuffer Sampling (NO REFRACTION)
**Date**: Current debugging attempt  
**Purpose**: Isolate whether the problem is the refraction calculation OR the framebuffer content  
**Approach**: Sample framebuffer at CURRENT fragment's screen position (no refraction offset):
```glsl
// Calculate current fragment's screen UV (NO REFRACTION)
vec2 screenUV = gl_FragCoord.xy / vec2(textureSize(u_TransmissionFramebufferSampler, 0));
vec3 transmittedLight = texture(u_TransmissionFramebufferSampler, screenUV).rgb;
```
**Test Hypothesis**:
- **If interior does NOT rotate**: Framebuffer is correct → refraction UV calculation is wrong
- **If interior STILL rotates**: Framebuffer update is broken → need to fix framebuffer rendering

**TESTING NOW - User needs to rotate watch and report results**
```
**Result**: Still "Interior rotates"  
**Analysis**: View-space and world-space give same result because:
- `viewSpaceNormal = ViewMatrix * worldNormal` still rotates with object
- `viewSpacePos = ViewMatrix * worldPos` still rotates with object
- The relative angle between view and normal is the same in both spaces!

## Technical Analysis

### THE REAL PROBLEM DISCOVERED

Based on the visual comparison, the issue is NOT that refraction changes with rotation (that's correct).

**The actual bug**: We're sampling the WRONG part of the framebuffer. The screen UV calculation is somehow tied to the view rotation instead of being tied to the object's surface.

### What Should Happen (Reference Viewer)
1. Opaque pass renders watch interior to framebuffer
2. Glass pass: For each glass pixel, compute refracted ray
3. Sample framebuffer at the refracted position
4. **Key**: The sampled position should be relative to where the ray exits the glass volume in SCREEN SPACE at render time

### What's Happening (My Implementation)  
1. Opaque pass renders watch interior ✓
2. Glass pass: Compute refracted ray ✓
3. Sample framebuffer... but the UV is WRONG ✗
4. **Problem**: The screen UV is somehow rotating with the view, sampling different parts of the interior

### Root Cause Hypothesis

The issue is likely in how we're computing `screenUV`:

```glsl
vec4 ndcPos = u_ProjectionMatrix * u_ViewMatrix * vec4(refractedRayExit, 1.0);
vec2 screenUV = (ndcPos.xy / ndcPos.w) * 0.5 + 0.5;
```

This calculation should give us the screen-space position of where the refracted ray exits... but something is wrong.

**Possibilities**:
1. `refractedRayExit` position is in wrong space
2. View/Projection matrix is wrong
3. NDC to UV conversion is wrong
4. The framebuffer we're sampling is wrong/stale

### What the Reference Viewer Does
Looking at glTF-Sample-Renderer:
```glsl
vec3 transmissionRay = getVolumeTransmissionRay(n, v, thickness, ior, modelMatrix);
vec3 refractedRayExit = position + transmissionRay;  // world space
vec4 ndcPos = projMatrix * viewMatrix * vec4(refractedRayExit, 1.0);
```

**They use world space and the interior DOES rotate with the object!**

## Next Steps - NEW ANALYSIS

### BEFORE making any more changes:

1. **Check Current Screen UV Calculation**: 
   - Print/log the `screenUV` values - are they reasonable (0-1 range)?
   - Are they changing wildly as we rotate?

2. **Verify Framebuffer Contents**:
   - Is the transmission framebuffer actually showing the interior?
   - Debug: render the framebuffer texture directly to screen to see what's in it

3. **Check refractedRayExit Position**:
   - Is this position reasonable in world space?
   - When we rotate the watch, does this position rotate correctly?

4. **Compare with Reference Implementation Step-by-Step**:
   - Reference uses world-space calculation (same as our Attempt 1)
   - But reference works, ours doesn't
   - **Critical difference must be in the details**

### Theory: Current Screen Position Bug

**Hypothesis**: The current pixel's screen position (`gl_FragCoord`) needs to be used as a base, then offset by refraction.

Reference might be doing:
```glsl
vec2 currentScreenUV = gl_FragCoord.xy / screenSize;
vec2 offset = /* calculate refraction offset */;
vec2 sampleUV = currentScreenUV + offset;
```

Instead of projecting exit point directly to screen!

---

## Attempt 6: DEBUG - Direct Framebuffer Sampling (NO REFRACTION)

**Date**: November 9, 2025  
**Goal**: Isolate whether the bug is in framebuffer content or refraction calculation

### Changes Made
Removed ALL refraction logic and directly sample framebuffer at current fragment position:
```glsl
vec2 screenUV = gl_FragCoord.xy / vec2(textureSize(u_TransmissionFramebufferSampler, 0));
#if SOKOL_GLSL
screenUV.y = 1.0 - screenUV.y; // OpenGL Y-flip (bottom-left origin)
#endif
// Metal has top-left origin matching texture, no flip needed
vec3 transmittedLight = texture(u_TransmissionFramebufferSampler, screenUV).rgb;
```

**Important**: Had to fix gl_FragCoord Y-flip logic:
- **OpenGL** (`SOKOL_GLSL`): `gl_FragCoord` has bottom-left origin → **need Y-flip**
- **Metal**: `gl_FragCoord` has top-left origin → **no flip needed**
- Initial attempt had this inverted (`#if !SOKOL_GLSL`), causing upside-down display

### Result
**✅ SUCCESS** - Interior stays stationary when rotating watch!

**Key Finding**: No "watch inside watch" effect - the interior does NOT rotate with the camera.

### Conclusion
- ✅ **Framebuffer content is CORRECT** - opaque pass rendering works perfectly
- ❌ **Refraction UV calculation is WRONG** - the bug is in Attempts 1-5's projected exit point logic
- The rotating interior bug is caused by incorrect refraction calculation, NOT framebuffer issues

### Next Steps
Now that we've isolated the problem to refraction calculation:
1. The projected exit point approach in Attempts 1-5 is fundamentally wrong
2. Need to investigate offset-based approach instead of direct projection
3. Study how reference implementation calculates the refraction offset

---

## Reference Implementation Analysis

**Source**: `glTF-Sample-Renderer/source/Renderer/shaders/`

### Key Functions

**`getVolumeTransmissionRay()` (punctual.glsl:133-145)**:
```glsl
vec3 getVolumeTransmissionRay(vec3 n, vec3 v, float thickness, float ior, mat4 modelMatrix)
{
    // Direction of refracted light.
    vec3 refractionVector = refract(-v, normalize(n), 1.0 / ior);

    // Compute rotation-independant scaling of the model matrix.
    vec3 modelScale;
    modelScale.x = length(vec3(modelMatrix[0].xyz));
    modelScale.y = length(vec3(modelMatrix[1].xyz));
    modelScale.z = length(vec3(modelMatrix[2].xyz));

    // The thickness is specified in local space.
    return normalize(refractionVector) * thickness * modelScale;
}
```

**`getIBLVolumeRefraction()` (ibl.glsl:70-109)**:
```glsl
vec3 transmissionRay = getVolumeTransmissionRay(n, v, thickness, ior, modelMatrix);
vec3 refractedRayExit = position + transmissionRay;

// Project refracted vector on the framebuffer, while mapping to normalized device coordinates.
vec4 ndcPos = projMatrix * viewMatrix * vec4(refractedRayExit, 1.0);
vec2 refractionCoords = ndcPos.xy / ndcPos.w;
refractionCoords += 1.0;
refractionCoords /= 2.0;

// Sample framebuffer to get pixel the refracted ray hits.
vec3 transmittedLight = getTransmissionSample(refractionCoords, perceptualRoughness, ior);
```

### Critical Differences from Our Attempts

1. **Model Scale Extraction** (Lines 138-141):
   - Extract scale from model matrix by taking length of each column
   - This is **rotation-independent** - scale is preserved regardless of rotation
   - We were missing this in our attempts!

2. **Thickness Application** (Line 144):
   - Multiply by `thickness * modelScale`
   - Our attempts used thickness but didn't account for model scale

3. **World-Space Position** (Line 85):
   - Uses `position` (world-space fragment position) as starting point
   - Adds `transmissionRay` (scaled refraction vector) to get exit point
   - **Exactly like our Attempt 1, but with proper scale handling**

### Why Our Attempts Failed

**Attempt 1-5 Bug**: Missing model scale extraction!
- We calculated `transmissionRay` but didn't account for model matrix scale
- When watch rotates, the rotation is applied but scale wasn't properly extracted
- This caused the interior to appear to rotate because thickness wasn't properly scaled

### The Fix

Our Attempt 1 was **almost correct**, but we need to add model scale extraction:

```glsl
// Extract rotation-independent scale from model matrix
vec3 modelScale;
modelScale.x = length(vec3(u_ModelMatrix[0].xyz));
modelScale.y = length(vec3(u_ModelMatrix[1].xyz));
modelScale.z = length(vec3(u_ModelMatrix[2].xyz));

// Apply scale to thickness
vec3 transmissionRay = normalize(refractedRay) * materialInfo.thickness * modelScale;
```

This ensures the refraction calculation stays in world space correctly!

## Attempt 7: Model Scale Extraction (FAILED)

**Status**: Tested - Interior still rotates

**Changes**:
- Added `mat4 u_ModelMatrix` to fragment shader's `ibl_params` uniform block
- Extract rotation-independent scale from model matrix:
  ```glsl
  vec3 modelScale;
  modelScale.x = length(vec3(u_ModelMatrix[0].xyz));
  modelScale.y = length(vec3(u_ModelMatrix[1].xyz));
  modelScale.z = length(vec3(u_ModelMatrix[2].xyz));
  ```
- Apply scale to thickness: `transmissionRay = normalize(refractedRay) * thickness_factor * modelScale`
- Updated all Frame*.cs files to pass model matrix to shader

**Result**: FAILED - Interior still rotates when rotating watch

**Analysis**:
- Implementation matches reference glTF-Sample-Renderer EXACTLY
- Both use: `normalize(refract(-v, n, 1.0/ior)) * thickness * modelScale`
- Both project to NDC then map to [0,1]
- Both use world-space position (`v_Position`)
- Model scale extraction is identical
- Yet bug persists...

**Theory**: Since Attempt 6 debug test (direct framebuffer sampling without refraction) worked perfectly, but Attempt 7 (full refraction with proper model scale) still fails, the issue MUST be in the refraction calculation itself, not in coordinate systems or model matrices.

## Next Steps for Investigation

### Questions for User:

1. **In the reference glTF-Sample-Renderer**: When you rotate the watch, does the glass surface show ANY distortion changes, or does it look like it's just a transparent window with no refraction at all?

2. **Thickness value**: What is the actual `thickness` value in the ChronographWatch.glb file? Can you check in a glTF inspector?

3. **IOR value**: What is the `ior` (index of refraction) value? Default should be 1.5 for glass.

### Theory: View-Space vs World-Space

The reference might be doing refraction in **view-space** instead of world-space. This would make the refraction camera-relative, which could explain why rotation doesn't affect the interior appearance.

Let me try Attempt 8: View-space refraction calculation.

###

1. **Thickness too large** → excessive distortion
2. **IOR incorrect** → wrong refraction angle  
3. **Framebuffer not updated** → sampling stale data
4. **Screen UV calculation wrong** → sampling wrong pixels
5. **Normal direction flipped** → refraction in wrong direction

## Conclusion

The "rotating interior" might be **CORRECT PHYSICAL BEHAVIOR**. When you rotate glass, the refraction changes because the surface orientation changes. This is how real glass works!

**Need user to clarify**: What does the reference viewer show? Does it also rotate?
