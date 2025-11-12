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
    layout(offset=176) int use_uniform_skinning;    // offset 176 (0=texture-based, 1=uniform-based)
#ifdef SKINNING
    layout(offset=192) highp mat4 finalBonesMatrices[MAX_BONES];  // offset 192 (adjusted for removed int)
#endif
};



// Animation texture samplers (use high bindings to avoid FS conflicts)
// Note: u_morphWeights is defined in vs_params (vs_uniforms.glsl)
// Note: Morph targets share slot 9 with CharlieLUT - they're unlikely to be used together
// (morphing is for character animation, Charlie sheen is for fabric materials)
layout(binding=11) uniform texture2D u_jointsSampler_Tex;
layout(binding=11) uniform sampler u_jointsSampler_Smp;

layout(binding=9) uniform texture2DArray u_MorphTargetsSampler_Tex;
layout(binding=9) uniform sampler u_MorphTargetsSampler_Smp;