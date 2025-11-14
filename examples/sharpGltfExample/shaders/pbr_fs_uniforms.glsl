// PBR Fragment Shader Uniforms
// Dedicated uniforms for pbr.glsl shader (separate from cgltf-sapp.glsl)

// IMPORTANT: This value must match RenderingConstants.MAX_LIGHTS in C# code!
// If you change this, you must also update RenderingConstants.cs and recompile the application.
#define MAX_LIGHTS 4

// Material parameters (binding=1)
layout(binding=1) uniform metallic_params {
    vec4 base_color_factor;
    vec3 emissive_factor;
    float metallic_factor;
    float roughness_factor;
    // Texture availability flags (1.0 = texture available, 0.0 = not available)
    float has_base_color_tex;
    float has_metallic_roughness_tex;
    float has_normal_tex;
    float has_occlusion_tex;
    float has_emissive_tex;
    // Alpha parameters
    float alpha_cutoff;
    // Emissive strength (KHR_materials_emissive_strength extension)
    float emissive_strength;
    // Occlusion strength (controls how much AO affects the final color, 1.0 = full effect)
    float occlusion_strength;
    // Transmission (glass/refraction) parameters - KHR_materials_transmission
    float transmission_factor;  // 0.0 = opaque, 1.0 = fully transparent with refraction
    float has_transmission_tex; // 1.0 = texture available, 0.0 = use uniform only
    float transmission_texcoord;// 0 = TEXCOORD_0, 1 = TEXCOORD_1
    float ior;                  // Index of Refraction (1.0 = air, 1.5 = glass, 1.55 = amber)
    // Volume absorption parameters - KHR_materials_volume (Beer's Law)
    vec3 attenuation_color;     // RGB color filter (e.g., orange for amber)
    float attenuation_distance; // Distance at which light reaches attenuation_color intensity
    float thickness_factor;     // Thickness of the volume in world units
    float has_thickness_tex;    // 1.0 = texture available, 0.0 = use uniform only
    float thickness_texcoord;   // 0 = TEXCOORD_0, 1 = TEXCOORD_1
    float thickness_tex_index;  // Which texture slot contains thickness: 0=BaseColor, 1=MetallicRoughness, 2=Normal, 3=Occlusion, 4=Emissive
    // Clearcoat parameters - KHR_materials_clearcoat
    float clearcoat_factor;     // 0.0 = no clearcoat, 1.0 = full clearcoat
    float clearcoat_roughness;  // Roughness of the clearcoat layer
    // Texture transforms - KHR_texture_transform (offset, rotation, scale)
    vec2 base_color_tex_offset;
    float base_color_tex_rotation;
    vec2 base_color_tex_scale;
    vec2 metallic_roughness_tex_offset;
    float metallic_roughness_tex_rotation;
    vec2 metallic_roughness_tex_scale;
    vec2 normal_tex_offset;
    float normal_tex_rotation;
    vec2 normal_tex_scale;
    vec2 occlusion_tex_offset;
    float occlusion_tex_rotation;
    vec2 occlusion_tex_scale;
    vec2 emissive_tex_offset;
    float emissive_tex_rotation;
    vec2 emissive_tex_scale;
    // Normal map scale (strength of normal perturbation)
    float normal_map_scale;     // 1.0 = full strength, 0.2 = subtle
    // Texture coordinate set indices (which UV channel each texture uses)
    float base_color_texcoord;          // 0 = TEXCOORD_0, 1 = TEXCOORD_1
    float metallic_roughness_texcoord;  // 0 = TEXCOORD_0, 1 = TEXCOORD_1
    float normal_texcoord;              // 0 = TEXCOORD_0, 1 = TEXCOORD_1
    float occlusion_texcoord;           // 0 = TEXCOORD_0, 1 = TEXCOORD_1
    float emissive_texcoord;            // 0 = TEXCOORD_0, 1 = TEXCOORD_1
    // Debug view controls
    float debug_view_enabled;     // 0 = disabled, 1 = enabled
    float debug_view_mode;        // Which debug view to display (see DEBUG_* constants)
};

// Light parameters (binding=2)
layout(binding=2) uniform light_params {
    int num_lights;
    float ambient_strength;  // Controllable ambient light strength
    vec4 light_positions[MAX_LIGHTS];   // w component: light type
    vec4 light_directions[MAX_LIGHTS];  // w component: spot inner cutoff (cosine)
    vec4 light_colors[MAX_LIGHTS];      // w component: intensity
    vec4 light_params_data[MAX_LIGHTS]; // x: range, y: spot outer cutoff, z/w: unused
};

// IBL (Image-Based Lighting) parameters (binding=3)
layout(binding=3) uniform ibl_params {
    float u_EnvIntensity;           // Environment light intensity multiplier
    float u_EnvBlurNormalized;      // Blur for skybox rendering (0 = sharp, 1 = max blur)
    int u_MipCount;                 // Number of mip levels in specular cubemap
    mat4 u_EnvRotation;             // 3x3 rotation matrix for environment (stored as mat4)
    ivec2 u_TransmissionFramebufferSize; // For transmission sampling
    mat4 u_ViewMatrix;              // View matrix for transmission refraction
    mat4 u_ProjectionMatrix;        // Projection matrix for transmission refraction
    mat4 u_ModelMatrix;             // Model matrix for transmission refraction
};

// Camera position
layout(binding=4) uniform camera_params {
    vec3 u_Camera;
};

// Rendering feature flags
layout(binding=7) uniform rendering_flags {
    int use_ibl;              // 0 or 1
    int use_punctual_lights;  // 0 or 1
    int use_tonemapping;      // 0 or 1
    int linear_output;        // 0 or 1
    int alphamode;            // 0=opaque, 1=mask, 2=blend
};

// Texture samplers
layout(binding=0) uniform texture2D u_BaseColorTexture;
layout(binding=0) uniform sampler u_BaseColorSampler;

layout(binding=1) uniform texture2D u_MetallicRoughnessTexture;
layout(binding=1) uniform sampler u_MetallicRoughnessSampler;

layout(binding=2) uniform texture2D u_NormalTexture;
layout(binding=2) uniform sampler u_NormalSampler;

layout(binding=3) uniform texture2D u_OcclusionTexture;
layout(binding=3) uniform sampler u_OcclusionSampler;

layout(binding=4) uniform texture2D u_EmissiveTexture;
layout(binding=4) uniform sampler u_EmissiveSampler;

// IBL textures (separate texture and sampler)
layout(binding=5) uniform textureCube u_GGXEnvTexture;
layout(binding=5) uniform sampler u_GGXEnvSampler_Raw;

layout(binding=6) uniform textureCube u_LambertianEnvTexture;
layout(binding=6) uniform sampler u_LambertianEnvSampler_Raw;

layout(binding=7) uniform texture2D u_GGXLUTTexture;
layout(binding=7) uniform sampler u_GGXLUTSampler_Raw;

#ifndef MORPHING
layout(binding=8) uniform textureCube u_CharlieEnvTexture;
layout(binding=8) uniform sampler u_CharlieEnvSampler_Raw;

layout(binding=9) uniform texture2D u_CharlieLUTTexture;
layout(binding=9) uniform sampler u_CharlieLUTSampler_Raw;

#define u_CharlieEnvSampler samplerCube(u_CharlieEnvTexture, u_CharlieEnvSampler_Raw)
#define u_CharlieLUT sampler2D(u_CharlieLUTTexture, u_CharlieLUTSampler_Raw)
#endif

#ifdef TRANSMISSION
// Transmission framebuffer (for refraction/transparency)
layout(binding=10) uniform texture2D u_TransmissionFramebufferTexture;
layout(binding=10) uniform sampler u_TransmissionFramebufferSampler_Raw;

// Transmission texture (RED channel mask for per-pixel transmission)
// Uses binding 8 (shared with Charlie environment for sheen - rarely used together)
layout(binding=8) uniform texture2D u_TransmissionTexture;
layout(binding=8) uniform sampler u_TransmissionSampler_Raw;

#define u_TransmissionFramebufferSampler sampler2D(u_TransmissionFramebufferTexture, u_TransmissionFramebufferSampler_Raw)
#define u_TransmissionSampler sampler2D(u_TransmissionTexture, u_TransmissionSampler_Raw)
#endif

// Create combined samplers for IBL functions
#define u_GGXEnvSampler samplerCube(u_GGXEnvTexture, u_GGXEnvSampler_Raw)
#define u_LambertianEnvSampler samplerCube(u_LambertianEnvTexture, u_LambertianEnvSampler_Raw)
#define u_GGXLUT sampler2D(u_GGXLUTTexture, u_GGXLUTSampler_Raw)

