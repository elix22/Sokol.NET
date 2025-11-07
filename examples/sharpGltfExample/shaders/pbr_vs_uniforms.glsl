// PBR Vertex Shader Uniforms
// Dedicated uniforms for pbr.glsl shader (separate from cgltf-sapp.glsl)

#ifdef SKINNING
#ifndef MAX_BONES
#define MAX_BONES 100
#endif
#endif

layout(binding=0, std140) uniform vs_params {
    layout(offset=0) highp mat4 model;              // offset 0, size 64
    layout(offset=64) highp mat4 view_proj;         // offset 64, size 64
    layout(offset=128) highp vec3 eye_pos;          // offset 128, size 12 (but std140 pads to 16)
    layout(offset=144) vec4 u_morphWeights[2];      // offset 144 (8 morph weights as 2 vec4s)
    layout(offset=176) int use_morphing;            // offset 176 (0 or 1, for runtime morph checks)
    layout(offset=180) int has_morph_targets;       // offset 180 (0 or 1, for runtime morph checks)
#ifdef SKINNING
    layout(offset=192) highp mat4 finalBonesMatrices[MAX_BONES];  // offset 192 (adjusted for new ints)
#endif
};
