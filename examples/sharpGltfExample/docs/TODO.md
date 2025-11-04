# sharpGltfExample TODO List

## 🚧 In Progress Features (November 2025)

### KHR_animation_pointer Support ✅
**Status:** Steps 1-7 Complete - Testing in progress

**Target Model:** `PotOfCoalsAnimationPointer.gltf`
- Animates texture transform rotation for heat refraction effect
- Two counter-rotating textures (normal + thickness) create interference pattern

**Overview:**
The KHR_animation_pointer extension allows animating arbitrary glTF properties beyond just node transforms. In this case, it animates material texture transforms (`/materials/2/normalTexture/extensions/KHR_texture_transform/rotation`).

---

#### Implementation Sequence

##### Step 1: Data Structure Design 📋
**File:** `SharpGltfAnimation.cs` (new class or extend existing)

**Tasks:**
1. Create `MaterialPropertyAnimation` class to hold:
   - Material index (int)
   - Property path (string, e.g., "normalTexture/rotation")
   - Sampler type (rotation, offset, scale)
   - Keyframe data or sampler reference
   
2. Add to `SharpGltfAnimation`:
   ```csharp
   public List<MaterialPropertyAnimation> MaterialAnimations = new();
   ```

3. Create property target enum:
   ```csharp
   enum MaterialAnimationTarget {
       NormalTextureRotation,
       NormalTextureOffset,
       NormalTextureScale,
       // Add others as needed
   }
   ```

**Verification:** Compile successfully with new data structures

---

##### Step 2: Parse Animation Pointer Channels 📥
**File:** `SharpGltfModel.cs` → `ProcessAnimations()` method (lines ~675-710)

**Tasks:**
1. In the `foreach (var channel in gltfAnimation.Channels)` loop:
   - After the null check, add parsing for non-node channels
   
2. Detect animation pointer targets:
   ```csharp
   if (targetNode == null)
   {
       // Parse the target path (e.g., "/materials/2/normalTexture/extensions/KHR_texture_transform/rotation")
       var targetPath = channel.TargetNodePath;
       
       if (targetPath.Contains("/materials/") && targetPath.Contains("/KHR_texture_transform/"))
       {
           ParseMaterialPropertyAnimation(channel, animation);
       }
       else
       {
           Info($"Skipping unsupported animation pointer: {targetPath}", "SharpGLTF");
       }
       continue;
   }
   ```

3. Implement `ParseMaterialPropertyAnimation()`:
   - Extract material index from path (regex or string parsing)
   - Determine property type (rotation/offset/scale)
   - Store sampler reference
   - Add to `animation.MaterialAnimations`

**Verification:** Log material property animations correctly parsed

---

##### Step 3: Extract Sampler Data 🎯
**File:** `SharpGltfModel.cs` or `SharpGltfAnimation.cs`

**Tasks:**
1. For each material property channel, extract keyframes:
   ```csharp
   var sampler = channel.GetCubicSampler(); // or GetLinearSampler() based on interpolation
   foreach (var (time, value) in sampler)
   {
       // Store keyframes for runtime evaluation
   }
   ```

2. Handle different data types:
   - Rotation: single float (radians)
   - Offset: Vector2
   - Scale: Vector2

3. Store in `MaterialPropertyAnimation` for efficient lookup

**Verification:** Keyframe data extracted and logged correctly

---

##### Step 4: Runtime Animation Update 🔄
**File:** `Frame.cs` → `UpdateAnimations()` method (or similar)

**Tasks:**
1. After updating bone transforms, add material property updates:
   ```csharp
   if (model.Animation != null)
   {
       // Existing bone animation code...
       
       // NEW: Material property animations
       foreach (var matAnim in model.Animation.MaterialAnimations)
       {
           float value = matAnim.SampleAtTime(currentTime);
           ApplyMaterialPropertyValue(matAnim.MaterialIndex, matAnim.Target, value);
       }
   }
   ```

2. Implement `SampleAtTime()`:
   - Linear or cubic interpolation between keyframes
   - Handle looping/clamping

3. Implement `ApplyMaterialPropertyValue()`:
   - Update the corresponding `Mesh` property
   - Mark as "dirty" if needed for shader uniform updates

**Verification:** Material properties update correctly at runtime

---

##### Step 5: Update Mesh Material Properties 🎨
**File:** `Mesh.cs` and `SharpGltfModel.cs`

**Tasks:**
1. Ensure `Mesh` class has runtime-mutable properties:
   - `NormalTexRotation` (already exists, verify it's mutable)
   - `NormalTexOffset` (already exists)
   - `NormalTexScale` (already exists)

2. Add similar properties for thickness texture if needed:
   ```csharp
   public float ThicknessTexRotation { get; set; }
   public Vector2 ThicknessTexOffset { get; set; }
   public Vector2 ThicknessTexScale { get; set; }
   ```

3. Map material index → mesh (may need lookup table):
   ```csharp
   Dictionary<int, Mesh> _materialToMeshMap = new();
   ```

**Verification:** Property updates propagate to mesh instances

---

##### Step 6: Pass Animated Values to Shader 🖌️
**File:** `Frame.cs` → shader uniform binding (where `vs_params` is set)

**Tasks:**
1. Update shader uniform binding to use runtime values:
   ```csharp
   // For each mesh being rendered:
   vsParams.normal_tex_rotation = mesh.NormalTexRotation; // Animated value
   vsParams.normal_tex_offset = mesh.NormalTexOffset;
   vsParams.normal_tex_scale = mesh.NormalTexScale;
   ```

2. Verify shader already supports these uniforms (check `cgltf-sapp.glsl`)

3. If thickness texture animation is needed, add those uniforms too

**Verification:** Shader receives updated values each frame

---

##### Step 7: Shader Texture Transform Application ⚙️
**File:** `assets/cgltf-sapp.glsl` (vertex shader)

**Tasks:**
1. Verify texture coordinate transformation is already implemented:
   ```glsl
   // Should already exist from KHR_texture_transform support
   vec2 transformed_uv = apply_texture_transform(uv, rotation, offset, scale);
   ```

2. If not present, implement texture transform matrix:
   ```glsl
   mat3 get_texture_transform_matrix(float rotation, vec2 offset, vec2 scale)
   {
       float c = cos(rotation);
       float s = sin(rotation);
       return mat3(
           scale.x * c, scale.x * s, 0.0,
           scale.y * -s, scale.y * c, 0.0,
           offset.x, offset.y, 1.0
       );
   }
   ```

3. Apply to normal map sampling in fragment shader

**Verification:** Texture coordinates rotate/transform correctly

---

##### Step 8: Testing & Validation ✅
**Test Cases:**

1. **Load PotOfCoalsAnimationPointer.gltf:**
   - ✅ No crash on load
   - ✅ Animation channels detected and logged
   - ✅ Material property animations parsed

2. **Runtime Animation:**
   - ✅ Normal texture rotates counter-clockwise
   - ✅ Thickness texture rotates clockwise (if animated)
   - ✅ Heat refraction effect visible
   - ✅ Smooth animation loop

3. **Fallback Behavior:**
   - ✅ Models without animation pointer still work
   - ✅ Standard bone animations unaffected

4. **UI Verification:**
   - ✅ Add debug display for material property values
   - ✅ Consider adding override controls (pause/speed)

**Verification:** Full animation works as intended

---

##### Step 9: Documentation & Polish 📝
**Tasks:**

1. Update `IMPLEMENTATION_SUMMARY.md`:
   - Document KHR_animation_pointer support
   - Explain texture transform animation architecture

2. Add code comments explaining:
   - Property path parsing logic
   - Why texture transforms need per-frame updates

3. Consider future extensions:
   - Other animatable properties (emissive, IOR, etc.)
   - Support for multiple simultaneous animations

**Verification:** Documentation complete and clear

---

#### Technical Notes

**SharpGLTF Support:**
- SharpGLTF may not have native `KHR_animation_pointer` support
- Will need to access raw JSON extensions:
  ```csharp
  var extensions = channel.Extensions;
  if (extensions.TryGetValue("KHR_animation_pointer", out var pointerExt))
  {
      // Parse manually
  }
  ```

**Performance Considerations:**
- Material property animations are typically less frequent than bone animations
- Cache material-to-mesh mappings at load time
- Consider batching uniform updates if multiple materials animated

**Shader Coordinate System:**
- glTF texture coordinates: (0,0) = bottom-left
- Most rendering systems: (0,0) = top-left
- Verify rotation direction matches glTF spec

---

## ✅ Completed Features (November 2025)

### Render Loop Performance Optimization ✅
**Status:** Fully implemented and working

**Problem Solved:**
- Frame.cs lines 440-445 contained expensive LINQ operations (FirstOrDefault, Any) executed every frame for every node
- O(n) searches for animation data caused performance bottleneck in complex animated models

**Solution Implemented:**
- Pre-computed animation cache in SharpGltfModel.cs CacheAnimationInfo() method
- Added HasAnimation and CachedGltfNode fields to SharpGltfNode class
- Eliminated LINQ operations with O(1) HashSet/Dictionary lookups
- No functional changes to animation behavior, pure performance optimization

**Performance Impact:**
- Transforms expensive searches into instant cached lookups
- Critical improvement for models with many animated nodes
- ~2 LINQ operations eliminated per node per frame

---

### Bloom Effect ✅
**Status:** Fully implemented and working

**Implemented Features:**
- ✅ Offscreen framebuffers for render targets (5 passes)
- ✅ Bright pass extraction shader with configurable threshold
- ✅ Two-pass Gaussian blur (horizontal + vertical, separable)
- ✅ HDR tone mapping (Uncharted 2 operator)
- ✅ Final compositing pass combining bloom with main scene
- ✅ UI controls:
  - ✅ Bloom intensity slider (0.0 - 2.0)
  - ✅ Brightness threshold slider (0.0 - 10.0)
  - ✅ Enable/disable toggle
- ✅ Optimized performance with shader efficiency

**Benefits Achieved:**
- Makes emissive materials (especially high-intensity ones like 8x, 16x) visually appealing
- Creates dramatic "glow" effect around bright objects
- Enhances the visual progression of the EmissiveStrengthTest model

---

### Glass Materials: Transmission, Volume & IOR ✅
**Status:** Fully implemented and working

**Target Models:** 
- `DragonAttenuation.glb` (verified working)
- `MosquitoInAmber.glb` (compatible)

**Implemented Extensions:**
- ✅ KHR_materials_ior (Index of Refraction)
- ✅ KHR_materials_transmission (Light refraction through transparent materials)
- ✅ KHR_materials_volume (Beer's Law absorption for colored translucent materials)
- ✅ KHR_materials_emissive_strength (HDR emissive)

---

#### Phase 1: IOR (Index of Refraction) ✅
**Status:** Fully implemented

**Completed Implementation:**
- ✅ SharpGLTF has `MaterialIOR` extension support
- ✅ Added `IOR` property to `Mesh.cs` (default: 1.5)
- ✅ Extract IOR in `SharpGltfModel.cs` using `material.GetExtension<MaterialIOR>()`
- ✅ Pass IOR to shader as uniform
- ✅ IOR values properly logged and working

**Working Properties:**
- `ior` (float): Refractive index (default 1.5)
  - Air: 1.0, Water: 1.33, Glass: 1.5, Amber: 1.55, Diamond: 2.4

---

#### Phase 2: Screen-Space Transmission ✅
**Status:** Fully implemented with per-material auto-detection

**Completed Implementation:**
- ✅ Created offscreen framebuffer for scene capture
- ✅ Added screen-space texture to render pipeline
- ✅ Implemented two-pass rendering:
  - ✅ Pass 1: Render opaque objects → capture to screen texture
  - ✅ Pass 2: Render opaque + transparent objects with refraction
- ✅ Back-to-front depth sorting for transparent objects
- ✅ Added transmission properties to `Mesh.cs`:
  - ✅ `TransmissionFactor` (0.0-1.0): Blend opaque/transparent
  - ✅ Per-material auto-detection (no global toggle)
- ✅ Extract transmission in `SharpGltfModel.cs` using `material.GetExtension<MaterialTransmission>()`
- ✅ Modified `cgltf-sapp.glsl` shader:
  - ✅ Added `screen_texture` uniform sampler2D
  - ✅ Added `transmission_factor` uniform
  - ✅ Implemented refraction with Snell's Law
  - ✅ Screen-space UV distortion based on refracted ray
  - ✅ Proper blending with transmission_factor
- ✅ UI controls for material property overrides

**Architecture:**
- Per-material transmission (auto-detects `transmission_factor > 0`)
- Two-pass rendering with proper pipeline format matching
- Offscreen pass uses transmission-specific pipelines
- Swapchain pass uses standard pipelines

---

#### Phase 3: Volume Absorption (Beer's Law) ✅
**Status:** Fully implemented

**Completed Implementation:**
- ✅ Added volume properties to `Mesh.cs`:
  - ✅ `AttenuationColor` (RGB): Color absorbed by volume
  - ✅ `AttenuationDistance` (float): Distance for full absorption
  - ✅ `ThicknessFactor` (float): Object thickness
- ✅ Extract volume in `SharpGltfModel.cs` using `material.GetExtension<MaterialVolume>()`
- ✅ Using artist-defined thickness factor (glTF spec recommendation)
- ✅ Modified `cgltf-sapp.glsl` shader:
  - ✅ Added volume uniforms (attenuation_color, attenuation_distance, thickness_factor)
  - ✅ Implemented Beer's Law absorption: `color *= exp(-absorption * thickness_factor)`
  - ✅ Integrated with transmission (works independently)
- ✅ UI controls for material property overrides

**Beer's Law Implementation:**
```glsl
vec3 absorption = -log(max(attenuation_color, vec3(0.001))) / max(attenuation_distance, 0.001);
color *= exp(-absorption * thickness_factor);
```

---

#### Phase 4: Integration & Optimization ✅
**Status:** Complete and production-ready

**Completed Tasks:**
- ✅ Tested with DragonAttenuation.glb (working correctly)
- ✅ Visual appearance verified (colored glass with refraction)
- ✅ Performance optimized (minimal texture lookups)
- ✅ Edge cases handled:
  - ✅ Back-to-front transparent object sorting
  - ✅ Screen edge clamping
  - ✅ Zero thickness handling
  - ✅ Per-material detection
- ✅ Proper error handling for missing extensions
- ✅ Shader uniform buffer properly structured
- ✅ Extension detection logged
- ✅ UI controls for material overrides

**Known Characteristics:**
- DragonAttenuation appears darker red/orange (expected per model README due to node scaling)
- Volume absorption works independently from transmission
- Screen-space refraction limitations with overlapping transparent objects (acceptable)

---

### Achieved Visual Results ✅

**Glass/Amber Materials:**
- ✅ Realistic colored translucent appearance
- ✅ Light refraction bending through objects (lensing effect)
- ✅ Volumetric absorption creating depth and color (Beer's Law)
- ✅ Proper depth perception through transparent surfaces
- ✅ Per-material automatic handling (no global toggle)

**Technical Achievement:**
- ✅ Implements 4 advanced PBR extensions from Khronos glTF spec
- ✅ Screen-space refraction with Snell's Law
- ✅ Physically-based light transport through volumes (Beer's Law)
- ✅ Compatible with official glTF reference models
- ✅ Production-ready rendering pipeline

**Documentation:**
- See `TRANSMISSION_REFACTOR.md` for implementation details
- See `IMPLEMENTATION_SUMMARY.md` for complete system overview

**References:**
- [KHR_materials_transmission spec](https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_transmission/README.md)
- [KHR_materials_volume spec](https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_volume/README.md)
- [KHR_materials_ior spec](https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_ior/README.md)
- [Khronos Press Announcement](https://www.khronos.org/news/press/new-gltf-extensions-raise-the-bar-on-3d-asset-visual-realism)

---

## Future Enhancements
- Add more post-processing effects (tone mapping, color grading, etc.)
- Implement HDR rendering pipeline
- Add shadow mapping
- Implement KHR_materials_iridescence for soap bubbles/oil slicks (1-2 weeks)
- Add screen-space reflections (SSR) to complement refraction
