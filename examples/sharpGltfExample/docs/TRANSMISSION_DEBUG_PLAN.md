# Transmission Refraction Debug Plan

## Problem Statement
When rotating the CommercialRefrigerator model or moving the camera, the interior (bottles) appears misaligned when viewed through the glass. The misalignment suggests incorrect screen-space coordinate calculation for refraction sampling.

## Debug Strategy
We'll systematically verify each component of the transmission pipeline to isolate the issue.

---

## Step 1: Verify Screen Texture Capture
**Goal**: Ensure Pass 1 (opaque pass) correctly captures the background

**Test Method**:
- Visualize the screen texture directly instead of using it for refraction
- This will show us exactly what's being captured in Pass 1

**Implementation**:
Modify `calculate_refraction()` to return the raw screen texture sample at the fragment's current screen position (no refraction offset).

**Expected Result**: 
- Should show the opaque objects (interior) correctly positioned
- If this fails, the screen texture capture itself is wrong

**Status**: PENDING

---

## Step 2: Verify Fragment Screen Position
**Goal**: Confirm fragments map to correct screen coordinates without refraction

**Test Method**:
- Sample screen texture using fragment's actual screen position (gl_FragCoord)
- Compare with Step 1 results

**Implementation**:
Use `gl_FragCoord.xy / vec2(screen_size)` as texture coordinates

**Expected Result**:
- Glass should appear invisible (showing exact background)
- Proves our base screen-space mapping is correct

**Status**: PENDING

---

## Step 3: Verify Refraction Direction Calculation
**Goal**: Check if `refract()` produces reasonable world-space directions

**Test Method**:
- Visualize refraction vector direction as color
- Red = X component, Green = Y component, Blue = Z component

**Implementation**:
```glsl
vec3 refractionVector = refract(-view, normalize(normal), 1.0 / ior);
return abs(refractionVector); // Visualize as RGB
```

**Expected Result**:
- Should show smooth color gradients across the glass surface
- Colors should change predictably as camera/model rotates
- If colors jump or are discontinuous, refraction calculation is wrong

**Status**: PENDING

---

## Step 4: Verify Transmission Ray Length
**Goal**: Check if thickness calculation is reasonable

**Test Method**:
- Visualize transmission ray length as grayscale intensity

**Implementation**:
```glsl
vec3 transmission_ray = getVolumeTransmissionRay(normal, view, thickness, ior, model_mat);
float len = length(transmission_ray);
return vec3(len / 10.0); // Scale for visibility
```

**Expected Result**:
- Should show uniform gray across the glass (uniform thickness)
- If it varies with rotation, the model matrix scale extraction is wrong

**Status**: PENDING

---

## Step 5: Verify Refracted Ray Exit Position
**Goal**: Check if exit point calculation is in correct world space

**Test Method**:
- Visualize the refracted ray exit point position as color

**Implementation**:
```glsl
vec3 refracted_ray_exit = position + transmission_ray;
return fract(refracted_ray_exit); // Wrap to [0,1] for visualization
```

**Expected Result**:
- Should show smooth position-based color patterns
- Should remain stable relative to model during rotation

**Status**: PENDING

---

## Step 6: Verify Screen-Space Projection
**Goal**: Check if exit points project to correct NDC coordinates

**Test Method**:
- Visualize NDC coordinates before UV conversion

**Implementation**:
```glsl
vec4 ndc_pos = proj_mat * view_mat * vec4(refracted_ray_exit, 1.0);
vec3 ndc = ndc_pos.xyz / ndc_pos.w;
return vec3(ndc.xy * 0.5 + 0.5, 0.0); // Show as RG, blue=0
```

**Expected Result**:
- Should show smooth gradients in red/green channels
- Values should stay in [0,1] range (edges might go outside)
- If values jump during rotation, matrix multiplication is wrong

**Status**: PENDING

---

## Step 7: Verify UV Coordinate Mapping
**Goal**: Check final texture coordinates used for sampling

**Test Method**:
- Visualize the final refraction_coords as color

**Implementation**:
```glsl
vec2 refraction_coords = ndc_pos.xy / ndc_pos.w;
refraction_coords = refraction_coords * 0.5 + 0.5;
return vec3(refraction_coords, 0.0);
```

**Expected Result**:
- Should show position that slightly offsets from Step 2
- Offset direction should match refraction physics
- If offset is too large or jumps, coordinate calculation is wrong

**Status**: PENDING

---

## Step 8: Compare Model Matrix Components
**Goal**: Verify model matrix doesn't have unexpected transforms

**Test Method**:
- Extract and visualize model matrix scale factors

**Implementation**:
```glsl
vec3 modelScale;
modelScale.x = length(vec3(model_mat[0].xyz));
modelScale.y = length(vec3(model_mat[1].xyz));
modelScale.z = length(vec3(model_mat[2].xyz));
return modelScale / 10.0; // Visualize as RGB
```

**Expected Result**:
- Should show uniform color (uniform scale)
- Should NOT change with model rotation
- If it changes, something is wrong with how model_mat is passed

**Status**: PENDING

---

## Step 9: Test Without Model Matrix
**Goal**: Isolate whether model matrix is causing the issue

**Test Method**:
- Temporarily use identity matrix instead of model_mat
- Use fixed thickness value

**Implementation**:
```glsl
vec3 transmission_ray = normalize(refractionVector) * thickness; // No scale
```

**Expected Result**:
- If this fixes the issue, model matrix is the problem
- If still broken, issue is elsewhere in the pipeline

**Status**: PENDING

---

## Step 10: Verify View/Projection Matrices
**Goal**: Check if view/proj matrices are consistent between passes

**Test Method**:
- Log matrix values in C# code
- Verify they're the same in both Pass 1 (capture) and Pass 2 (refraction)

**Implementation**:
Add debug logging in Frame.cs:
```csharp
Info($"Pass 1 View: {state.camera.View.M11}, {state.camera.View.M12}...");
Info($"Pass 2 View: {transmissionParams.view_matrix.M11}...");
```

**Expected Result**:
- Matrices should be identical in both passes
- If different, we're sampling from wrong viewpoint

**Status**: PENDING

---

## Implementation Notes

### ⚠️ CRITICAL: Shader Compiler Optimization Warning

**IMPORTANT**: When adding debug code that returns early, be aware of shader compiler optimizations:

- The sokol-shdc compiler removes unused uniform buffers from the generated code
- If debug code returns before using transmission uniforms (like `model_matrix`, `view_matrix`, `projection_matrix`), the compiler will optimize out the entire `cgltf_transmission_params_t` uniform buffer
- This causes C# code generation to fail because `cgltf_transmission_params_t` type won't be generated
- Result: Build errors like `error CS0246: The type 'cgltf_transmission_params_t' could not be found`

**Solution**: 
- Always ensure debug code still references the transmission uniform buffer, OR
- Comment out debug returns instead of leaving them in place, OR
- Recompile shaders after removing debug code to regenerate the C# bindings

**Build Commands**:
```bash
# Compile shaders only (regenerates C# bindings)
dotnet build sharpGltfExample.csproj -t:CompileShaders

# Full project build (includes shader compilation + C# compilation)
dotnet build sharpGltfExample.csproj
```

**Example of problematic debug code**:
```glsl
vec3 calculate_refraction(..., mat4 model_mat, mat4 view_mat, mat4 proj_mat) {
    // DEBUG: Return early
    return vec3(1.0, 0.0, 1.0); // Magenta
    
    // All code below is unreachable, so compiler removes view_mat, proj_mat uniforms
    vec4 ndc_pos = proj_mat * view_mat * vec4(position, 1.0);
    ...
}
```

### Debug Visualization Modes

The shader now includes built-in debug visualizations. Change `DEBUG_MODE` at the top of `calculate_refraction()` in `fs_functions.glsl`:

- **DEBUG_MODE = 0**: Normal rendering (no debug)
- **DEBUG_MODE = 1**: World-space position (wrapped to [0,1] RGB)
- **DEBUG_MODE = 2**: Surface normal (mapped to [0,1] RGB)
- **DEBUG_MODE = 3**: View direction (absolute values)
- **DEBUG_MODE = 4**: Transmission ray length (grayscale, scaled by /10)
- **DEBUG_MODE = 5**: Refracted ray exit position (world space, wrapped)
- **DEBUG_MODE = 6**: NDC coordinates before UV conversion (RG channels)
- **DEBUG_MODE = 7**: Final UV coordinates used for texture sampling

**Note**: These debug modes are safe - they don't cause early returns, so the uniform buffer remains intact.

### How to Add Debug Visualization

1. **Modify `calculate_refraction()` in fs_functions.glsl**:
   - Change the `#define DEBUG_MODE` value (currently set to 1)
   - Set to 0 for normal rendering, 1-7 for debug modes
   - **CRITICAL**: Ensure you set to 0 before final build

2. **Recompile shaders**:
   ```bash
   dotnet build sharpGltfExample.csproj -t:CompileShaders
   ```

3. **Test and observe**:
   - Run the app and load CommercialRefrigerator.gltf
   - Rotate model and camera
   - Check if visualization matches expectations

4. **Document results**:
   - Update this file with PASS/FAIL status
   - Note any unexpected behavior
   - Move to next step

5. **Clean up debug code**:
   - Remove all debug visualizations
   - Recompile shaders to regenerate correct C# bindings:
     ```bash
     dotnet build sharpGltfExample.csproj -t:CompileShaders
     ```
   - Verify full project builds successfully:
     ```bash
     dotnet build sharpGltfExample.csproj
     ```

### Debug Workflow
- Start with Step 1
- If step passes, move to next
- If step fails, that component is the problem
- Fix the issue and retest
- Continue until issue is resolved

---

## Results Log
(To be filled in as we test each step)

### Step 1: Screen Texture Capture
**Status**: ❌ FAILED
**Notes**: Interior is misaligned even when sampling screen texture at fragment's exact screen position (no refraction). This proves the screen texture capture in Pass 1 is incorrect. 

### Step 2: Fragment Screen Position  
**Status**: 
**Notes**: 

### Step 3: Refraction Direction
**Status**: 
**Notes**: 

### Step 4: Transmission Ray Length
**Status**: 
**Notes**: 

### Step 5: Refracted Ray Exit Position
**Status**: 
**Notes**: 

### Step 6: Screen-Space Projection
**Status**: 
**Notes**: 

### Step 7: UV Coordinate Mapping
**Status**: 
**Notes**: 

### Step 8: Model Matrix Components
**Status**: 
**Notes**: 

### Step 9: Test Without Model Matrix
**Status**: 
**Notes**: 

### Step 10: View/Projection Matrices
**Status**: 
**Notes**: 

---

## Conclusion

**Root Cause**: Matrix multiplication order mismatch between vertex shader and fragment shader

**Details**:
- Vertex shader uses: `gl_Position = view_proj * pos` where `view_proj = View * Proj` (from C#)
- Fragment shader was using: `proj_mat * view_mat * position` = `Proj * View * position`
- This caused screen-space coordinates to be calculated with reversed matrix order
- Result: When sampling the screen texture, fragments were looking up the wrong coordinates

**Solution**: 
Changed fragment shader projection from:
```glsl
vec4 ndc_pos = proj_mat * view_mat * vec4(refracted_ray_exit, 1.0);
```
To:
```glsl
vec4 ndc_pos = view_mat * proj_mat * vec4(refracted_ray_exit, 1.0);
```

This now matches the vertex shader's matrix order.

**Verification**: Test with CommercialRefrigerator.gltf and verify interior stays aligned during model rotation and camera movement. 
