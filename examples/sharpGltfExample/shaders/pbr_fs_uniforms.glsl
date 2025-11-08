// PBR Fragment Shader Uniforms
// Dedicated uniforms for pbr.glsl shader (separate from cgltf-sapp.glsl)

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
    // Transmission (glass/refraction) parameters - KHR_materials_transmission
    float transmission_factor;  // 0.0 = opaque, 1.0 = fully transparent with refraction
    float ior;                  // Index of Refraction (1.0 = air, 1.5 = glass, 1.55 = amber)
    // Volume absorption parameters - KHR_materials_volume (Beer's Law)
    vec3 attenuation_color;     // RGB color filter (e.g., orange for amber)
    float attenuation_distance; // Distance at which light reaches attenuation_color intensity
    float thickness_factor;     // Thickness of the volume in world units
    // Clearcoat parameters - KHR_materials_clearcoat
    float clearcoat_factor;     // 0.0 = no clearcoat, 1.0 = full clearcoat
    float clearcoat_roughness;  // Roughness of the clearcoat layer
    // Normal texture transform - KHR_texture_transform
    vec2 normal_tex_offset;
    float normal_tex_rotation;
    vec2 normal_tex_scale;
    // Normal map scale (strength of normal perturbation)
    float normal_map_scale;     // 1.0 = full strength, 0.2 = subtle
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
};
