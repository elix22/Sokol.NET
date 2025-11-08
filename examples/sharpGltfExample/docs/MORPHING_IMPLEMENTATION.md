# Morph Target Implementation

## Overview
Full morph target animation support for glTF models, following the glTF-Sample-Renderer reference implementation.

## Architecture

### Texture-Based Displacement Storage
- **Format**: RGBA32F texture2DArray
- **Filtering**: NEAREST (no interpolation)
- **Layout**: 3N layers where N = max morph targets
  - Layers 0 to N-1: Position displacements
  - Layers N to 2N-1: Normal displacements  
  - Layers 2N to 3N-1: Tangent displacements
- **Packing**: vec3 → vec4 (w component unused)

### Uniform Data
- **vs_params block** (binding 0):
  - `u_morphWeights[2]`: 2 × vec4 = 8 float weights
  - `use_morphing`: Runtime flag (0/1)
  - `has_morph_targets`: Mesh has morph data flag (0/1)

### Texture Bindings
- **Slot 9**: MorphTargets (texture2DArray)
  - Shared with CharlieLUT (sheen)
  - Conditional compilation: `#ifndef MORPHING` for Charlie textures
  - Rationale: Morphing (character animation) and sheen (fabric) rarely used together

### Shader Variants
Compiled automatically before build via `Directory.Build.props`:
1. **Base**: `pbr-shader.cs` → Static meshes only
2. **SKINNING**: `pbr-shader-skinning.cs` → Skinning without morphing
3. **MORPHING**: `pbr-shader-morphing.cs` → Morphing without skinning
4. **SKINNING+MORPHING**: `pbr-shader-skinning-morphing.cs` → Combined (dedicated namespace)

### Renderer Architecture
Four specialized renderer files to avoid namespace collisions:

- **Frame_Static.cs** → `pbr_shader_cs.Shaders`
  - Handles: static meshes (no skinning, no morphing)
  
- **Frame_Skinning.cs** → `pbr_shader_skinning_cs_skinning.Shaders`
  - Handles: skinned meshes without morphing
  
- **Frame_Morphing.cs** → `pbr_shader_morphing_cs_morphing.Shaders`
  - Handles: morphing meshes without skinning
  - Uses `morphing_*_params_t` uniform types
  
- **Frame_SkinnedMorphing.cs** → `pbr_shader_skinning_morphing_cs_skinning_morphing.Shaders`
  - Handles: **combined skinning + morphing**
  - Uses `skinning_morphing_*_params_t` uniform types
  - Applies morphing first, then skinning (per glTF spec)

**Routing Logic (Frame.cs):**
```csharp
if (useSkinning && useMorphing)
    RenderSkinnedMorphingMesh(...);  // Dedicated combined shader
else if (useSkinning)
    RenderSkinnedMesh(...);          // Skinning-only
else if (useMorphing)
    RenderMorphingMesh(...);         // Morphing-only
else
    RenderStaticMesh(...);           // Static
```

## Implementation Details

### Texture Creation (`Init.cs`)
```csharp
static ImageView CreateMorphTargetTexture(
    int maxVertices, 
    int maxTargets,
    MeshPrimitive[] primitives)
{
    // Calculate texture dimensions
    int width = (int)Math.Ceiling(Math.Sqrt(maxVertices));
    int totalLayers = maxTargets * 3; // pos, normal, tangent
    
    // RGBA32F format with vec3→vec4 padding
    var data = new float[width * width * totalLayers * 4];
    
    // Pack displacements by attribute type
    for (int targetIdx = 0; targetIdx < maxTargets; targetIdx++) {
        // Position layer: targetIdx
        // Normal layer:   targetIdx + maxTargets
        // Tangent layer:  targetIdx + 2*maxTargets
    }
    
    return sg.MakeImageView(...);
}
```

### Weight Extraction (`Frame.cs`)
```csharp
// Get weights from node or mesh
var weights = gltfNode.MorphWeights ?? gltfMesh?.MorphWeights;

// Pack into 2 vec4s (max 8 weights)
for (int i = 0; i < Math.Min(weights.Count, 8); i++) {
    int vec4Index = i / 4;
    int componentIndex = i % 4;
    vsParams.u_morphWeights[vec4Index][componentIndex] = weights[i];
}

// Set runtime flags
vsParams.use_morphing = mesh.HasMorphTargets ? 1 : 0;
vsParams.has_morph_targets = mesh.HasMorphTargets ? 1 : 0;
```

### Shader Displacement (`animation.glsl`)
```glsl
vec3 getTargetPosition(int targetIndex, int vertexIndex) {
    int layer = targetIndex;
    vec4 displacement = texelFetch(
        u_MorphTargetsSampler_Tex,
        ivec3(x, y, layer),
        0
    );
    return displacement.xyz;
}

// Apply weighted blend
for (int i = 0; i < MAX_MORPH_TARGETS; i++) {
    if (i >= u_morphWeights.length()) break;
    float weight = u_morphWeights[i / 4][i % 4];
    position += getTargetPosition(i, vertexIndex) * weight;
    normal += getTargetNormal(i, vertexIndex) * weight;
    tangent += getTargetTangent(i, vertexIndex) * weight;
}
```

## Optimization Notes

### Binding Slot Strategy
- **Slots 0-11**: Only available range (WebGL/Metal limit)
- **Slot 9 shared**: MorphTargets vs CharlieLUT
  - Charlie sheen disabled when `MORPHING` defined
  - Acceptable trade-off: fabric sheen rarely needed with character animation
- **Slot 10 preserved**: TransmissionSampler for glass materials
  - User insight: "glass materials with morphing" is valid use case
- **Slot 11 dedicated**: JointsSampler for skinning

### Uniform Block Consolidation
- **Original approach**: Separate `vs_rendering_flags` block (binding 3)
  - Wasteful: 9 fields but only 2 used in VS
- **Optimized**: Added only needed flags to existing `vs_params` block
  - Freed binding slot 3 entirely
  - Reduced total uniform blocks from 8 to 7
  - Better std140 layout utilization

### std140 Layout (vs_params)
```glsl
layout(binding=0, std140) uniform vs_params {
    mat4 model;                  // offset 0
    mat4 view_proj;              // offset 64
    vec3 eye_pos;                // offset 128
    vec4 u_morphWeights[2];      // offset 144 (8 floats)
    int use_morphing;            // offset 176
    int has_morph_targets;       // offset 180
    mat4 finalBonesMatrices[75]; // offset 192
};
```

## Current Limitations

1. **Max 8 morph targets** per mesh (uniform array size)
   - glTF spec allows unlimited targets
   - Can be extended by increasing `u_morphWeights` array size

2. **Charlie sheen incompatible** with morphing
   - Both need slot 9
   - Could add separate MORPHING+CHARLIE variant if needed

3. **Static weights only** in current implementation
   - Weight extraction happens once per frame
   - Animation channel support requires timeline updates

## Testing Checklist

- [ ] Load AnimatedMorphCube.gltf
- [ ] Verify texture creation succeeds
- [ ] Check morph weight extraction
- [ ] Test morphing + skinning combination
- [ ] Verify morph target count limits
- [ ] Test with models having >8 targets (graceful degradation)
- [ ] Confirm Charlie sheen disabled when morphing active

## Future Enhancements

1. **Animation channel support**
   - Parse glTF animation channels for morph weight keyframes
   - Interpolate weights over time

2. **Sparse morph targets**
   - Only store non-zero displacements
   - Reduce texture memory usage

3. **Compute-based morphing**
   - Move displacement to compute shader
   - Free vertex shader from texture lookups

4. **MORPHING+CHARLIE variant**
   - Use slot 10 for one of them
   - Requires rethinking transmission texture binding

## References
- [glTF-Sample-Renderer morphing](https://github.com/KhronosGroup/glTF-Sample-Renderer/blob/main/source/Renderer/shaders/animation.glsl)
- [glTF 2.0 Specification - Morph Targets](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html#morph-targets)
