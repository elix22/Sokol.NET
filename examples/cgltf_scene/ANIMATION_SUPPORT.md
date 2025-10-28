# Animation Support for CGLTF Scene Viewer

## Overview
Animation support has been added to the CGLTF scene viewer, allowing playback of skeletal animations from GLTF files.

## What Was Added

### New Files
1. **CGltfAnimation.cs** - Animation data structure for CGLTF
2. **CGltfBone.cs** - Bone with keyframe animation data
3. **CGltfAnimator.cs** - Animator that calculates final bone matrices
4. **CGltfNodeData.cs** - Node hierarchy structure for animations

### Modified Files
1. **CGltfParser.cs**
   - Added `ParseSkins()` - Parses skin/skeleton data from GLTF
   - Added `ParseAnimations()` - Parses animation channels and keyframes
   - Added `BuildAnimationNodeHierarchy()` - Builds node hierarchy for animation
   - Added `Animations` property to store parsed animations
   
2. **cgltf-sapp.glsl** - Shader updated to support skinning
   - Added bone IDs and weights as vertex attributes (locations 3 and 4)
   - Added `finalBonesMatrices[MAX_BONES]` uniform array
   - Added skinning calculations in vertex shader
   
3. **cgltf-scene-app.cs**
   - Added animator to state
   - Initialize animator when animations are loaded
   - Update animator in Frame() function
   - Pass bone matrices to shader uniforms

### Shader Changes
The vertex shader now accepts:
```glsl
layout(location=3) in vec4 boneIds;    // Bone indices affecting this vertex
layout(location=4) in vec4 weights;    // Bone weights (must sum to 1.0)
```

And includes bone transformation matrices:
```glsl
layout(binding=0) uniform vs_params {
    mat4 model;
    mat4 view_proj;
    vec3 eye_pos;
    mat4 finalBonesMatrices[MAX_BONES];  // 100 bones max
};
```

## How to Use

### 1. Recompile Shaders
The shader must be recompiled to generate the updated C# bindings:

```bash
cd examples/cgltf_scene
# Run the shader compilation script for your platform
# This will regenerate the shader .cs files with bone matrix support
```

### 2. Load Animated Model
The parser will automatically detect and load animations from GLTF files:

```csharp
_parser.LoadFromFileAsync(gltfFilePath, state.scene,
    onComplete: () =>
    {
        // Initialize animator if animations exist
        if (_parser.Animations.Count > 0)
        {
            var firstAnimation = _parser.Animations[0];
            state.animator = new CGltfAnimator(firstAnimation);
        }
    });
```

### 3. Update Animation Each Frame
```csharp
// In Frame() function:
if (state.animator != null)
{
    state.animator.UpdateAnimation((float)sapp_frame_duration());
}
```

### 4. Pass Bone Matrices to Shader
```csharp
// Get bone matrices from animator
var boneMatrices = state.animator?.GetFinalBoneMatrices() 
    ?? new Matrix4x4[AnimationConstants.MAX_BONES];

// Fill vs_params with bone matrices
cgltf_vs_params_t vs_params = new cgltf_vs_params_t();
vs_params.model = modelMatrix;
vs_params.view_proj = camera.ViewProj;
vs_params.eye_pos = camera.EyePos;

// Copy bone matrices
for (int i = 0; i < AnimationConstants.MAX_BONES; i++)
{
    vs_params.finalBonesMatrices[i] = boneMatrices[i];
}
```

## Animation Constants
```csharp
public static class AnimationConstants
{
    public const int MAX_BONES = 100;           // Maximum number of bones
    public const int MAX_BONE_INFLUENCE = 4;    // Max bones per vertex
}
```

## Limitations
- Currently supports up to 100 bones per skeleton
- Each vertex can be influenced by up to 4 bones
- Supports first animation only (when multiple animations exist)
- Only parses first skin (if multiple skins exist)

## Testing
Test with animated GLTF models such as:
- DancingGangster.glb (from Mixamo)
- Any rigged character with animations from Sketchfab

## Future Enhancements
- Support for multiple animations with switching
- Animation blending
- Multiple skin support
- IK (Inverse Kinematics)
- Animation events/callbacks
