// PBR Vertex-Shader uniforms for the Framework.
// Differences from CGltfViewer:
//   - `model` matrix is NOT here; it is built from per-instance vertex attributes at
//     locations 4-7 in the vertex shader (same as blinn_phong.glsl).
//   - `view_proj` + `eye_pos` are combined with animation params in a single UBO so
//     the binding-0 slot remains consistent with the rest of the Framework shaders.

#ifdef LOW_TIER
#define MAX_BONES 1     // LOW_TIER: every skinned mesh takes the joint-texture path (use_uniform_skinning = 0)
#endif
#ifndef MAX_BONES
#define MAX_BONES 128   // Must match RenderingConstants.MAX_BONES
#endif

// CSM_CASCADES: a preprocessor macro used to size the csm_vp array and the
// v_ShadowPos output array.  Defined here (with #ifndef guard) so the VS stage
// doesn't need a separate @include of fs_constants.glsl (which must only be
// included once since sokol-shdc tracks @includes globally).
#ifndef CSM_CASCADES
  #if defined(CSM4)
    #define CSM_CASCADES 4
  #elif defined(CSM2)
    #define CSM_CASCADES 2
  #elif defined(CSM1)
    #define CSM_CASCADES 1
  #else
    #define CSM_CASCADES 0
  #endif
#endif

layout(binding=0) uniform pbr_vs_params {
    mat4 view_proj;
    vec3 eye_pos;
    float _pad0;

    // Morph-target weights (8 weights packed as 2 × vec4; used when MORPHING is defined).
    vec4 u_morphWeights[2];

    // Skinning path selector: 1 = uniform-array path (≤128 bones),
    //                         0 = joint-matrix-texture path (>128 bones).
    int  use_uniform_skinning;
    int  _pad1;
    int  _pad2;
    int  _pad3;

#ifdef SKINNING
    // Bone matrices for Path A (use_uniform_skinning == 1).
    mat4 finalBonesMatrices[MAX_BONES];
#endif

    // CSM directional-shadow view-projection matrices (one per cascade).
#if CSM_CASCADES >= 4
    mat4 csm_vp[4];
#elif CSM_CASCADES == 2
    mat4 csm_vp[2];
#elif CSM_CASCADES == 1
    mat4 csm_vp[1];
#endif
};

// Joint-matrix texture (binding=11) — declared unconditionally so that sokol-shdc
// always sees the binding; the actual fetch is gated by #ifdef SKINNING in animation.glsl.
// It holds raw mat4 data read with texelFetch only (never filtered), so it MUST be declared
// unfilterable_float with a nonfiltering sampler. RGBA32F is not a filterable format on GLES3
// drivers lacking GL_OES_texture_float_linear (e.g. Mali-G52 / Galaxy Tab A8); a plain `float`
// sample-type trips sokol's VALIDATE_ABND_TEXVIEW_EXPECTED_FILTERABLE_IMAGE panic in
// sg_apply_bindings on those devices (works on Metal / desktop-WebGL2 / float-linear GLES3).
@image_sample_type u_jointsSampler_Tex unfilterable_float
@sampler_type u_jointsSampler_Smp nonfiltering
layout(binding=11) uniform texture2D u_jointsSampler_Tex;
layout(binding=11) uniform sampler   u_jointsSampler_Smp;

// Morph-target texture array (binding=9) — same unconditional-declaration pattern.
// Like the joint texture above, it holds raw RGBA32F displacement data read with texelFetch only
// (never filtered), so it MUST be unfilterable_float + nonfiltering — otherwise binding it trips
// sokol's VALIDATE_ABND_TEXVIEW_EXPECTED_FILTERABLE_IMAGE panic on GLES3 drivers without
// GL_OES_texture_float_linear (e.g. Mali-G52 / Galaxy Tab A8), exactly as the joints texture did.
@image_sample_type u_MorphTargetsSampler_Tex unfilterable_float
@sampler_type u_MorphTargetsSampler_Smp nonfiltering
layout(binding=9) uniform texture2DArray u_MorphTargetsSampler_Tex;
#ifdef CLUSTERED
// Forward+ takes sampler slot 9 for its cluster texture (pbr_fs_uniforms.glsl): the morph texture is read through the
// joint texture's sampler, the same non-filtering one (RenderingServer binds _jointSampler to both).
#define u_MorphTargetsSampler_Smp u_jointsSampler_Smp
#else
layout(binding=9) uniform sampler        u_MorphTargetsSampler_Smp;
#endif
