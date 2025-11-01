# SharpGLTF Renderer Implementation Summary

## Overview
This implementation creates a complete SharpGLTF-based renderer with support for:
- Static mesh rendering with PBR materials
- Skinned mesh rendering with bone animations  
- **Glass materials with transmission and refraction** (KHR_materials_transmission)
- **Volume absorption (Beer's Law)** (KHR_materials_volume)
- **Index of refraction** (KHR_materials_ior)
- **HDR emissive materials** (KHR_materials_emissive_strength)
- **Dual 16-bit/32-bit index buffers** (supports meshes >65K vertices)
- **Bloom post-processing** with HDR tone mapping
- **Frustum culling** for performance optimization
- Texture loading and caching with hit rate tracking
- Advanced camera controls with orbital and free-look modes
- ImGui-based UI with multiple windows and themes

## Architecture

### Core Components

#### 1. **Vertex.cs**
- Defines vertex structure for both static and skinned meshes
- Fields: Position, Normal, TexCoord, BoneIDs, BoneWeights
- Supports up to 4 bone influences per vertex (MAX_BONE_INFLUENCE)

#### 2. **Camera.cs**
- Spherical coordinate camera system (latitude/longitude)
- Generates view and projection matrices
- Configurable field of view, near/far planes

#### 3. **Texture.cs**
- Loads textures from memory using StbImage
- Creates Sokol image, view, and sampler resources
- Supports RGBA8 format with linear filtering

#### 4. **Mesh.cs**
- Renderable mesh with vertex/index buffers
- **Dual index buffer support**: 16-bit for meshes <65536 vertices, 32-bit for larger meshes
- PBR material properties:
  - Base color texture and factor
  - Metallic/roughness texture and factors
  - Normal map
  - Occlusion map
  - Emissive texture and factor with strength (HDR support)
  - **Transmission factor** (0.0-1.0 for glass/transparent materials)
  - **Index of refraction (IOR)** (1.0-2.4, default 1.5)
  - **Volume absorption** (attenuation color, distance, thickness)
- Alpha mode support: OPAQUE, BLEND, MASK
- `Draw()` method handles rendering with proper pipeline selection
- Default white texture fallback for missing textures
- Bounding box for frustum culling

### Animation System

#### 5. **AnimationConstants.cs**
- MAX_BONES = 100 (matches shader uniform array size)
- MAX_BONE_INFLUENCE = 4 (bones per vertex)

#### 6. **BoneInfo.cs**
- Stores bone offset matrix (inverse bind pose)

#### 7. **SharpGltfBone.cs**
- Represents single bone with keyframe animation
- Keyframe types: Position (Vector3), Rotation (Quaternion), Scale (Vector3)
- Interpolation methods:
  - Linear interpolation (Lerp) for position and scale
  - Spherical linear interpolation (Slerp) for rotation
- `Update(animationTime)` calculates interpolated transform for current time

#### 8. **SharpGltfAnimation.cs**
- Container for all bones in an animation
- Maintains bone hierarchy via parent-child relationships
- Duration calculated from maximum bone keyframe time

#### 9. **SharpGltfAnimator.cs**
- Calculates final bone transformation matrices for GPU skinning
- `CalculateBoneTransform()`: Recursively traverses node hierarchy
- Applies formula: `FinalTransform = OffsetMatrix × GlobalTransform`
- Outputs array of 100 Matrix4x4 for shader uniform

### Model Loading

#### 10. **SharpGltfModel.cs**
- Main parser converting SharpGLTF ModelRoot to renderable format
- **Automatic index type detection**: 16-bit vs 32-bit based on vertex count
- Key methods:
  - `ProcessSkinning()`: Extracts inverse bind matrices from skin data
  - `ProcessMesh()`: Creates Mesh objects with vertex data and materials
  - `ProcessAnimations()`: Samples animation curves at 30fps
- **Extended material processing**:
  - PBR metallic-roughness parameters
  - **KHR_materials_transmission**: TransmissionFactor for refraction
  - **KHR_materials_volume**: AttenuationColor, AttenuationDistance, ThicknessFactor
  - **KHR_materials_ior**: Index of refraction (default 1.5)
  - **KHR_materials_emissive_strength**: HDR emissive intensity
  - Alpha modes: OPAQUE, BLEND, MASK with cutoff values
  - Loads textures from embedded or external sources
  - Uses default values when materials are undefined
- **TextureCache integration**: Prevents duplicate texture loading

### Rendering Pipeline

#### 11. **PipelineManager.cs**
- Centralized pipeline management with 12 pipeline types:
  - Standard, Skinned (opaque)
  - StandardBlend, SkinnedBlend (alpha blending)
  - StandardMask, SkinnedMask (alpha testing)
  - Standard32, Skinned32, StandardBlend32, SkinnedBlend32, StandardMask32, SkinnedMask32 (32-bit index variants)
- Pipeline caching for performance
- Specialized pipelines for offscreen rendering (bloom, transmission)
- Depth format and sample count configuration

#### 12. **Bloom Post-Processing**
- HDR bloom with multi-pass Gaussian blur
- Passes: Scene → Bright extraction → Horizontal blur → Vertical blur → Composite
- Configurable intensity (0.0-3.0) and threshold (0.1-5.0)
- Tone mapping: Uncharted 2 / Reinhard with gamma correction
- Manual sRGB conversion for correct color space handling

#### 13. **Transmission/Refraction System**
- **Automatic activation**: Detects meshes with `transmission_factor > 0`
- **Two-pass rendering**:
  - Pass 1 (Offscreen): Captures opaque background to texture
  - Pass 2 (Swapchain): Renders opaque + transparent with refraction
- Screen-space refraction using Snell's Law
- Beer's Law volume absorption for colored glass effects
- Fresnel effect for realistic glass edges
- Pipeline format matching for offscreen vs swapchain rendering

#### 14. **Main Application (Main.cs, Frame.cs, Init.cs)**
- Complete rendering loop with multi-pass support
- Priority system: Transmission > Bloom > Regular rendering
- Frustum culling with statistics tracking
- Opaque/transparent sorting (back-to-front for transparency)
- `Init()`: Creates pipelines, loads model, initializes post-processing
- `Frame()`: Updates animation, performs culling, renders with appropriate passes
- Uniform binding includes lighting, materials, glass properties, bone matrices

### Additional Components

#### 15. **TextureCache.cs**
- Singleton pattern for texture management
- Prevents duplicate texture loading via key-based caching
- Tracks cache hits/misses for performance monitoring
- Thread-safe dictionary implementation

#### 16. **GUI.cs**
- Comprehensive ImGui-based UI system
- Multiple windows: Model Info, Animation, Lighting, Bloom, Glass Materials, Culling, Statistics, Camera Info, Camera Controls, Help
- Theme support: Dark, Light, Classic
- Real-time statistics display (FPS, vertices, indices, texture cache)
- Material override system with presets (Clear Glass, Amber, Water, Emerald, Ruby, Diamond)
- Light control panel with up to 4 lights

#### 17. **Lighting.cs**
- Support for 3 light types: Directional, Point, Spot
- Configurable properties: Position, Direction, Color, Intensity, Range
- Spot light cone angles (inner/outer cutoff)
- Up to 4 active lights simultaneously
- Ambient light strength control

#### 18. **FileSystem.cs**
- Async file loading using sokol-fetch
- Automatic buffer resizing for large files
- Progress tracking and error handling

## Shader Integration

Uses pre-compiled shaders from `/Shaders/` directory:
- **cgltf-sapp.glsl** (`cgltf_sapp_shader_cs_cgltf.Shaders`): Main PBR rendering with transmission/refraction
- **cgltf-sapp-skinning.glsl** (`cgltf_sapp_shader_skinning_cs_skinning.Shaders`): Skinned mesh variant
- **bloom-shaders.glsl**: Bright pass, Gaussian blur (H/V), composite

Shaders support:
- **PBR metallic-roughness workflow** with full material system
- **Multiple texture maps**: base color, metallic-roughness, normal, occlusion, emissive
- **Vertex skinning** with up to 100 bones
- **Screen-space refraction** with IOR-based ray bending (Snell's Law)
- **Beer's Law volume absorption** for colored glass effects
- **Fresnel effect** for realistic glass appearance
- **HDR rendering** with tone mapping (Uncharted 2)
- **Manual sRGB conversion** for correct color space
- **Alpha modes**: OPAQUE, BLEND, MASK
- **Up to 4 dynamic lights** with distance attenuation

## Usage

### Building
```bash
# Desktop build
dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture desktop --path examples/sharpGltfExample

# Web build
dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture web --path examples/sharpGltfExample
```

### Model Requirements
- GLTF 2.0 or GLB format
- Supports both static and skinned meshes
- PBR metallic-roughness materials
- Embedded or external textures

### Controls
- **Camera**:
  - Left Mouse/1 Finger: Orbit camera around model
  - Middle Mouse/2 Finger: Rotate model
  - Mouse Wheel: Zoom in/out
  - WASD/Arrow Keys: Move camera position
  - Q/E: Move camera up/down
  - Shift: Faster camera movement
- **Animation**: Automatically plays and loops (if present)
- **UI**: Access all features through menu bar → Windows

## Implementation Notes

### Index Buffer Strategy
The renderer automatically selects the appropriate index type:
- **16-bit indices** (ushort): For meshes with <65,536 vertices
  - More memory efficient
  - Faster on mobile/integrated GPUs
- **32-bit indices** (uint): For meshes with ≥65,536 vertices
  - Required for large models like DragonAttenuation (76,809 vertices)
  - Uses specialized pipeline variants (Standard32, Skinned32, etc.)

Decision based on **vertex count**, not index count, since indices can reference any vertex.

### Transmission/Refraction Architecture
**Automatic per-material handling** (no global toggle):
1. System scans all meshes for `TransmissionFactor > 0` on load
2. If detected, activates two-pass rendering:
   - **Pass 1 (Offscreen)**: Render opaque objects to texture (background capture)
   - **Pass 2 (Swapchain)**: Render opaque + transparent with refraction
3. Transparent objects sample offscreen texture for screen-space refraction

**Critical implementation details**:
- Pass 1 uses transmission-specific pipelines (offscreen format)
- Pass 2 uses regular swapchain pipelines
- Pipeline format mismatch causes validation errors
- `renderToOffscreen` parameter in `RenderNode()` controls pipeline selection

### Animation Sampling Strategy
Rather than using SharpGLTF's animation samplers directly, this implementation:
1. Samples animation curves at 30fps during loading
2. Stores keyframes in SharpGltfBone objects
3. Performs runtime interpolation in `SharpGltfBone.Update()`

This approach provides:
- Consistent frame rate regardless of original animation sampling
- Simplified runtime animation logic
- Easy integration with Sokol rendering loop

### Material Defaults
When materials are undefined or incomplete, the implementation uses sensible defaults:
- BaseColorFactor: (1, 1, 1, 1) - white
- MetallicFactor: 0.0 - non-metallic
- RoughnessFactor: 1.0 - fully rough
- EmissiveFactor: (0, 0, 0) - no emission
- EmissiveStrength: 1.0
- IOR: 1.5 (glass)
- TransmissionFactor: 0.0 (opaque)
- AttenuationColor: (1, 1, 1) - white (no absorption)

### Texture Handling
- **TextureCache prevents duplicates**: Same texture reused across meshes
- Async loading via FileSystem with automatic buffer resizing
- White default texture used when textures are missing
- **Manual sRGB conversion in shader** for correct color space:
  - Base color and emissive: sRGB → Linear
  - Metallic-roughness, normal, occlusion: Linear (no conversion)
- Proper handling of alpha channels for transparency

### Performance Considerations
- **Vertex and index buffers created once** during loading
- **Frustum culling** reduces rendering workload (tracks visible/culled meshes)
- **Texture caching** eliminates redundant GPU uploads
- **Bone matrices calculated per frame** only for animated meshes
- **Static meshes use simpler pipeline** without skinning overhead
- **Pipeline caching** in PipelineManager avoids recreation
- **Opaque/transparent sorting**: Front-to-back for opaque (early-z), back-to-front for transparent
- Maximum 100 bones supported (shader uniform array limit)
- **Offscreen passes only when needed**: Bloom/transmission activate on demand

## Tested Models
- ✅ **DragonAttenuation.glb**: 76,809 vertices (32-bit indices), transmission + volume absorption
- ✅ **MosquitoInAmber.glb**: Transmission, volume absorption, amber glass effect
- ✅ **DamagedHelmet.glb**: Static PBR materials
- ✅ **Mixamo models**: Skinned animation (requires 100× scale, -90° X rotation)
- ✅ **EmissiveStrengthTest.glb**: HDR emissive materials
- ✅ **AttenuationTest.glb**: Volume absorption test cases

## Known Issues & Limitations
- **DragonAttenuation color**: Appears darker red/orange instead of bright yellow-orange
  - Cause: Node scaling not compensated in thickness calculations
  - This is a known issue documented in the model's README ("Common Problems")
  - Can be fixed by scaling `thicknessFactor` by node transform scale
- **Maximum 100 bones**: Shader uniform array size limit
- **No morph targets**: Not yet implemented
- **Single animation playback**: No blending between animations

## Reference Examples
Implementation patterns adapted from:
- **assimp_animation**: Animation system architecture
- **cgltf_scene**: Texture loading, mesh rendering, PBR materials
- **Khronos glTF Sample Models**: Test cases and validation

## Recent Major Features (November 2025)
- ✅ **Dual 16-bit/32-bit index support** (supports large meshes like DragonAttenuation)
- ✅ **Per-material transmission system** (removed global toggle, automatic detection)
- ✅ **Two-pass rendering** (offscreen background capture + swapchain compositing)
- ✅ **Beer's Law volume absorption** (colored glass effects)
- ✅ **Screen-space refraction** with IOR
- ✅ **Bloom post-processing** with HDR tone mapping
- ✅ **KHR_materials_emissive_strength** support
- ✅ **Frustum culling** with statistics
- ✅ **Texture caching** system
- ✅ **Comprehensive ImGui UI** with multiple windows

## Future Enhancements
- [ ] Node scale compensation for thickness calculations (lighter DragonAttenuation)
- [ ] Multiple animation support with blending
- [ ] Morph target animations
- [ ] Transmission texture support (currently factor-only)
- [ ] Debug visualization for bone hierarchy
- [ ] Level of detail (LOD) support
- [ ] Ray-traced refraction (vs screen-space approximation)
- [ ] Increase bone limit (require shader changes)
- [ ] Per-mesh material override UI
- [ ] Animation timeline scrubbing
