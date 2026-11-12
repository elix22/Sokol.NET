@ctype mat4 System.Numerics.Matrix4x4

@vs shadow_caster_vs
#ifdef LOW_TIER
#define MAX_BONES 1     // LOW_TIER: every skinned mesh takes the joint-texture path (use_uniform_skinning = 0)
#endif
#ifndef MAX_BONES
#define MAX_BONES 128   // must match RenderingConstants.MAX_BONES
#endif

layout(binding=0) uniform shadow_caster_vs_params
{
    // Non-skinned: mvp = model * lightViewProj.
    // Skinned:     mvp = lightViewProj only (model is applied separately below).
    mat4 mvp;
#ifdef SKINNING
    mat4 model;
    // Skinning path selector: 1 = uniform array (≤128 bones), 0 = joint-matrix texture (>128 bones).
    // Mirrors pbr_vs_uniforms.glsl so the caster pose matches the color pass for big rigs.
    int  use_uniform_skinning;
    int  _pad0;
    int  _pad1;
    int  _pad2;
    mat4 finalBonesMatrices[MAX_BONES];
#endif
};

layout(location=0) in vec3 in_pos;
#ifdef SKINNING
// Locations 1/2 (NOT 9/10 as in pbr.glsl): the shadow VS has no instance attrs, so sokol's
// "attrs must be continuous" rule requires 0,1,2 with no gaps. The pipeline maps these to the
// 80 B SkinnedVertex offsets 48 (joints) and 64 (weights).
layout(location=1) in vec4 joints_0;
layout(location=2) in vec4 weights_0;

// Joint-matrix texture for the >128-bone path (use_uniform_skinning==0). It is read with texelFetch
// only (never filtered), so it MUST be unfilterable_float + a nonfiltering sampler — RGBA32F is not
// a filterable format on GLES3 drivers without GL_OES_texture_float_linear (e.g. Mali-G52), and a
// plain `float` sample-type trips sokol's VALIDATE_ABND_TEXVIEW_EXPECTED_FILTERABLE_IMAGE panic.
// Shares the per-character texture built by SkinnedCharacterRegistry (same 8-texels/bone layout as
// animation.glsl: joint i's matrix at texel index i*2*4).
@image_sample_type u_jointsSampler_Tex unfilterable_float
@sampler_type u_jointsSampler_Smp nonfiltering
layout(binding=0) uniform texture2D u_jointsSampler_Tex;
layout(binding=0) uniform sampler   u_jointsSampler_Smp;

mat4 getMatrixFromTexture(int index)
{
    mat4 result = mat4(1);
    int texSize = textureSize(sampler2D(u_jointsSampler_Tex, u_jointsSampler_Smp), 0).x;
    int pixelIndex = index * 4;
    for (int i = 0; i < 4; ++i)
    {
        int x = (pixelIndex + i) % texSize;
        int y = (pixelIndex + i - x) / texSize;
        result[i] = texelFetch(sampler2D(u_jointsSampler_Tex, u_jointsSampler_Smp), ivec2(x, y), 0);
    }
    return result;
}
#endif

void main()
{
#ifdef SKINNING
    mat4 skin;
    if (use_uniform_skinning == 1)
    {
        skin = weights_0.x * finalBonesMatrices[int(joints_0.x)]
             + weights_0.y * finalBonesMatrices[int(joints_0.y)]
             + weights_0.z * finalBonesMatrices[int(joints_0.z)]
             + weights_0.w * finalBonesMatrices[int(joints_0.w)];
    }
    else
    {
        // index*2 stride matches animation.glsl (8 texels/bone: transform slot at i*2).
        skin = weights_0.x * getMatrixFromTexture(int(joints_0.x) * 2)
             + weights_0.y * getMatrixFromTexture(int(joints_0.y) * 2)
             + weights_0.z * getMatrixFromTexture(int(joints_0.z) * 2)
             + weights_0.w * getMatrixFromTexture(int(joints_0.w) * 2);
    }
    if (skin == mat4(0)) skin = mat4(1);
    gl_Position = mvp * model * skin * vec4(in_pos, 1.0);
#else
    gl_Position = mvp * vec4(in_pos, 1.0);
#endif
}
@end

@fs shadow_caster_fs
void main()
{
}
@end

@program shadow_caster shadow_caster_vs shadow_caster_fs
