// PBR Fragment-Shader uniforms for the Framework.
//
// Light block uses the same packed GpuLight × 32 format as blinn_phong.glsl so that
// the same LightBuffer.cs can fill it without any adaptation.
//   GpuLight layout (4 × vec4 = 64 B):
//     lights_data[i*4+0] = PositionType   (xyz=pos,   w=type)
//     lights_data[i*4+1] = DirectionRange (xyz=dir,   w=range)
//     lights_data[i*4+2] = ColorIntensity (xyz=color, w=intensity)
//     lights_data[i*4+3] = SpotShadow     (x=innerCos,y=outerCos,z=shadowIdx,w=pad)
//
// Material params (binding=1) — layout identical to CGltfViewer's metallic_params.
// Light params    (binding=2) — packed 32-light block.
// IBL params      (binding=3) — environment map parameters.
// Camera params   (binding=4) — camera world position.
// Rendering flags (binding=5) — feature toggles.
// CSM params      (binding=6) — cascade split depths (VS VP matrices are in pbr_vs_uniforms).

// ─── Material (binding=1) ─────────────────────────────────────────────────────
layout(binding=1) uniform pbr_material_params {
    vec4  base_color_factor;
    vec3  emissive_factor;
    float metallic_factor;
    float roughness_factor;
    // Texture-availability flags (1.0 = present, 0.0 = absent)
    float has_base_color_tex;
    float has_metallic_roughness_tex;
    float has_normal_tex;
    float has_occlusion_tex;
    float has_emissive_tex;
    // Alpha
    float alpha_cutoff;
    // Emissive strength (KHR_materials_emissive_strength)
    float emissive_strength;
    // Occlusion strength
    float occlusion_strength;
    // Transmission (KHR_materials_transmission)
    float transmission_factor;
    float has_transmission_tex;
    float transmission_texcoord;
    float ior;
    // Volume (KHR_materials_volume)
    vec3  attenuation_color;
    float attenuation_distance;
    float thickness_factor;
    float has_thickness_tex;
    float thickness_texcoord;
    float thickness_tex_index;
    // Clearcoat (KHR_materials_clearcoat)
    float clearcoat_factor;
    float clearcoat_roughness;
    // Texture transforms (KHR_texture_transform)
    vec2  base_color_tex_offset;
    float base_color_tex_rotation;
    vec2  base_color_tex_scale;
    vec2  metallic_roughness_tex_offset;
    float metallic_roughness_tex_rotation;
    vec2  metallic_roughness_tex_scale;
    vec2  normal_tex_offset;
    float normal_tex_rotation;
    vec2  normal_tex_scale;
    vec2  occlusion_tex_offset;
    float occlusion_tex_rotation;
    vec2  occlusion_tex_scale;
    vec2  emissive_tex_offset;
    float emissive_tex_rotation;
    vec2  emissive_tex_scale;
    vec2  thickness_tex_offset;
    float thickness_tex_rotation;
    vec2  thickness_tex_scale;
    // Normal-map scale
    float normal_map_scale;
    // Texture-coordinate set indices
    float base_color_texcoord;
    float metallic_roughness_texcoord;
    float normal_texcoord;
    float occlusion_texcoord;
    float emissive_texcoord;
    // Debug
    float debug_view_enabled;
    float debug_view_mode;
    // Game-style punch: 0 = off (physical), 1 = shadows fully darken the IBL ambient.
    float shadow_ambient_weight;
};

// ─── Lights (binding=2) — packed GpuLight[32] ────────────────────────────────
layout(binding=2) uniform pbr_light_params {
    vec4  lights_data[128];     // 32 lights × 4 vec4s
    vec4  spot_shadow_vp[32];   // 8 spot matrices × 4 rows (shadow atlas slices 4-11)
    vec4  point_shadow_vp[96];  // 4 point lights × 6 faces × 4 rows
    vec4  ambient_num;          // xyz = ambient colour, w = float(numLights)
};

// ─── IBL (binding=3) ─────────────────────────────────────────────────────────
layout(binding=3) uniform pbr_ibl_params {
    float u_EnvIntensity;
    float u_EnvBlurNormalized;
    int   u_MipCount;
    float _ibl_pad;
    mat4  u_EnvRotation;
    ivec2 u_TransmissionFramebufferSize;
    mat4  u_ViewMatrix;
    mat4  u_ProjectionMatrix;
    mat4  u_ModelMatrix;   // per-instance model; C# sets to current draw's model
    vec4  u_AmbientParams; // Environment ambient: diffuse IBL × x + yzw (Sky: x = energy; Color: yzw = colour × energy)
};

// ─── Camera (binding=4) ──────────────────────────────────────────────────────
layout(binding=4) uniform pbr_camera_params {
    vec3  u_Camera;
    float _cam_pad;
};

// ─── Rendering flags (binding=5) ─────────────────────────────────────────────
layout(binding=5) uniform pbr_rendering_flags {
    int   use_ibl;
    int   use_punctual_lights;
    int   alphamode;        // 0=opaque, 1=mask, 2=blend
    int   linear_output;    // 1 = HDR output (no tonemap); tonemap done in post-process pass
    float ambient_strength; // fallback ambient when IBL is off
    float _rf_pad0;
    float _rf_pad1;
    float _rf_pad2;
};

// ─── CSM (binding=6, conditional) ────────────────────────────────────────────
#if CSM_CASCADES > 0
layout(binding=6) uniform pbr_csm_params {
    vec4  csm_split_depths;     // x,y,z,w = cascade 0..3 split depths (view-space far edge)
    float csm_bias;
    float csm_blend_band;       // depth range over which to blend between cascades
    int   csm_pcf_taps;         // 1, 9, or 25
    float _csm_pad;
};
#endif

// ─── Texture samplers ─────────────────────────────────────────────────────────
layout(binding=0) uniform texture2D       u_BaseColorTexture;
layout(binding=0) uniform sampler         u_BaseColorSampler;
layout(binding=1) uniform texture2D       u_MetallicRoughnessTexture;
layout(binding=1) uniform sampler         u_MetallicRoughnessSampler;
layout(binding=2) uniform texture2D       u_NormalTexture;
layout(binding=2) uniform sampler         u_NormalSampler;
layout(binding=3) uniform texture2D       u_OcclusionTexture;
layout(binding=3) uniform sampler         u_OcclusionSampler;
layout(binding=4) uniform texture2D       u_EmissiveTexture;
layout(binding=4) uniform sampler         u_EmissiveSampler;

// IBL
layout(binding=5) uniform textureCube     u_GGXEnvTexture;
layout(binding=5) uniform sampler         u_GGXEnvSampler_Raw;
layout(binding=6) uniform textureCube     u_LambertianEnvTexture;
layout(binding=6) uniform sampler         u_LambertianEnvSampler_Raw;
layout(binding=7) uniform texture2D       u_GGXLUTTexture;
layout(binding=7) uniform sampler         u_GGXLUTSampler_Raw;

// Charlie sheen reuses the GGX env + LUT bindings rather than dedicated bindings 8/9. The sheen
// IBL (getSheenSample, ibl.glsl) is dead-stripped from every compiled variant — it is never
// sampled — so this alias is a no-op that keeps bindings 8/9 free. That lets the point-shadow
// cube array take binding 8 in SKINNING variants (where joints occupy 11). Still #ifndef MORPHING
// to mirror getSheenSample's own guard.
#ifndef MORPHING
#define u_CharlieEnvSampler samplerCube(u_GGXEnvTexture, u_GGXEnvSampler_Raw)
#define u_CharlieLUT        sampler2D  (u_GGXLUTTexture, u_GGXLUTSampler_Raw)
#endif

#ifdef TRANSMISSION
layout(binding=10) uniform texture2D       u_TransmissionFramebufferTexture;
layout(binding=10) uniform sampler         u_TransmissionFramebufferSampler_Raw;
layout(binding=8)  uniform texture2D       u_TransmissionTexture;
layout(binding=8)  uniform sampler         u_TransmissionSampler_Raw;

#define u_TransmissionFramebufferSampler sampler2D(u_TransmissionFramebufferTexture, u_TransmissionFramebufferSampler_Raw)
#define u_TransmissionSampler            sampler2D(u_TransmissionTexture,            u_TransmissionSampler_Raw)
#endif

// Joint-matrix and morph-target textures are VS-only (animation.glsl); no FS
// declarations needed here.

// Shadow atlas (binding=10, 2D array + shadow sampler) — slices 0-3 are directional CSM
// cascades, slices 4-11 are spot-light shadow maps. Declared for every NON-transmission
// variant so both the directional CSM path AND the punctual spot-shadow path can sample it
// (binding 10 is otherwise the transmission framebuffer; those variants do no shadowing).
#if !defined(TRANSMISSION)
layout(binding=10) uniform texture2DArray  shadow_atlas;
layout(binding=10) uniform samplerShadow   shadow_atlas_smp;
#endif

// Point-light cube shadows: 6 faces per light packed as 2D-array slices (slot*6 + face).
// Non-skinning variants reuse binding 11 (the joints texture there is VS-only and stripped).
// SKINNING variants keep joints at binding 11, so the cube array moves to the free slot 8
// (sheen is excluded in skinning above, so binding 8 is unused and ≤11, within the sampler cap).
#if !defined(TRANSMISSION)
#ifdef SKINNING
layout(binding=8)  uniform texture2DArray  cube_shadow_array;
layout(binding=8)  uniform samplerShadow   cube_shadow_array_smp;
#else
layout(binding=11) uniform texture2DArray  cube_shadow_array;
layout(binding=11) uniform samplerShadow   cube_shadow_array_smp;
#endif
#endif

// Combined-sampler macros required by ibl.glsl
#define u_GGXEnvSampler      samplerCube(u_GGXEnvTexture,       u_GGXEnvSampler_Raw)
#define u_LambertianEnvSampler samplerCube(u_LambertianEnvTexture, u_LambertianEnvSampler_Raw)
#define u_GGXLUT             sampler2D  (u_GGXLUTTexture,       u_GGXLUTSampler_Raw)
