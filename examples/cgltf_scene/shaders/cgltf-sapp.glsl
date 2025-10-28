/*
    Taken from the GLTF reference viewer:

    https://github.com/KhronosGroup/glTF-Sample-Viewer/tree/master/src/shaders
 */

// this is just here to test the `@module` prefix feature of the sokol-shdc
// C code generator, other then that it's not needed
@module cgltf

@ctype mat4 hmm_mat4
@ctype vec4 hmm_vec4
@ctype vec3 hmm_vec3

@vs vs
const int MAX_BONES = 100;
const int MAX_BONE_INFLUENCE = 4;

layout(binding=0) uniform vs_params {
    mat4 model;
    mat4 view_proj;
    vec3 eye_pos;
#ifdef SKINNING
    mat4 finalBonesMatrices[MAX_BONES];
#endif
};

layout(location=0) in vec4 position;
layout(location=1) in vec3 normal;
layout(location=2) in vec2 texcoord;
layout(location=3) in vec4 boneIds;
layout(location=4) in vec4 weights;


out vec3 v_pos;
out vec3 v_nrm;
out vec2 v_uv;
out vec3 v_eye_pos;

void main() {
    vec4 finalPosition = position;
    vec3 finalNormal = normal;
    
    // Apply skinning only if animation is available
#ifdef SKINNING
        bool hasValidBone = false;
        vec4 totalPosition = vec4(0.0);
        vec3 totalNormal = vec3(0.0);
        
        // Apply skinning if bones are present
        for(int i = 0; i < MAX_BONE_INFLUENCE; i++)
        {
            if(int(boneIds[i]) == -1) 
                continue;
            if(int(boneIds[i]) >= MAX_BONES) 
            {
                totalPosition = position;
                totalNormal = normal;
                hasValidBone = true;
                break;
            }
            vec4 localPosition = finalBonesMatrices[int(boneIds[i])] * position;
            totalPosition += localPosition * weights[i];
            vec3 localNormal = mat3(finalBonesMatrices[int(boneIds[i])]) * normal;
            totalNormal += localNormal * weights[i];
            hasValidBone = true;
        }
        
        // If valid bone influences found, use them
        if (hasValidBone) {
            finalPosition = totalPosition;
            finalNormal = totalNormal;
        }
#endif
    
    vec4 pos = model * finalPosition;
    v_pos = pos.xyz / pos.w;
    v_nrm = (model * vec4(finalNormal, 0.0)).xyz;
    v_uv = texcoord;
    v_eye_pos = eye_pos;
    gl_Position = view_proj * pos;
}
@end

@fs metallic_fs

in vec3 v_pos;
in vec3 v_nrm;
in vec2 v_uv;
in vec3 v_eye_pos;

out vec4 frag_color;

struct material_info_t {
    float perceptual_roughness;     // roughness value, as authored by the model creator (input to shader)
    vec3 reflectance0;              // full reflectance color (normal incidence angle)
    float alpha_roughness;          // roughness mapped to a more linear change in the roughness (proposed by [2])
    vec3 diffuse_color;             // color contribution from diffuse lighting
    vec3 reflectance90;             // reflectance color at grazing angle
    vec3 specular_color;            // color contribution from specular lighting
};

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
};

const int MAX_LIGHTS = 4;
const int LIGHT_TYPE_DIRECTIONAL = 0;
const int LIGHT_TYPE_POINT = 1;
const int LIGHT_TYPE_SPOT = 2;

layout(binding=2) uniform light_params {
    int num_lights;
    float _pad1, _pad2, _pad3;  // Padding to align to vec4
    vec4 light_positions[MAX_LIGHTS];   // w component: light type
    vec4 light_directions[MAX_LIGHTS];  // w component: spot inner cutoff (cosine)
    vec4 light_colors[MAX_LIGHTS];      // w component: intensity
    vec4 light_params_data[MAX_LIGHTS]; // x: range, y: spot outer cutoff, z/w: unused
};

layout(binding=0) uniform texture2D base_color_tex;
layout(binding=1) uniform texture2D metallic_roughness_tex;
layout(binding=2) uniform texture2D normal_tex;
layout(binding=3) uniform texture2D occlusion_tex;
layout(binding=4) uniform texture2D emissive_tex;

layout(binding=0) uniform sampler base_color_smp;
layout(binding=1) uniform sampler metallic_roughness_smp;
layout(binding=2) uniform sampler normal_smp;
layout(binding=3) uniform sampler occlusion_smp;
layout(binding=4) uniform sampler emissive_smp;

vec3 linear_to_srgb(vec3 linear) {
    return pow(linear, vec3(1.0/2.2));
}

vec4 srgb_to_linear(vec4 srgb) {
    return vec4(pow(srgb.rgb, vec3(2.2)), srgb.a);
}

vec3 get_normal() {
    if (has_normal_tex < 0.5) {
        return normalize(v_nrm);
    }
    vec3 pos_dx = dFdx(v_pos);
    vec3 pos_dy = dFdy(v_pos);
    vec3 tex_dx = dFdx(vec3(v_uv,0.0));
    vec3 tex_dy = dFdy(vec3(v_uv,0.0));
    vec3 t = (tex_dy.t * pos_dx - tex_dx.t * pos_dy) / (tex_dx.s * tex_dy.t - tex_dy.s * tex_dx.t);
    vec3 ng = normalize(v_nrm);
    t = normalize(t - ng * dot(ng, t));
    vec3 b = normalize(cross(ng, t));
    mat3 tbn = mat3(t, b, ng);
    vec2 n_xy = texture(sampler2D(normal_tex, normal_smp), v_uv).xw * 2.0 - 1.0;
    vec3 n = vec3(n_xy.x, n_xy.y, sqrt(1.0 - n_xy.x*n_xy.x - n_xy.y*n_xy.y));
    n = normalize(tbn * n);
    return n;
}

struct angular_info_t {
    float n_dot_l;                // cos angle between normal and light direction
    float n_dot_v;                // cos angle between normal and view direction
    float n_dot_h;                // cos angle between normal and half vector
    float l_dot_h;                // cos angle between light direction and half vector
    float v_dot_h;                // cos angle between view direction and half vector
    vec3 padding;
};

angular_info_t get_angular_info(vec3 point_to_light, vec3 normal, vec3 view) {
    // Standard one-letter names
    vec3 n = normalize(normal);           // Outward direction of surface point
    vec3 v = normalize(view);             // Direction from surface point to view
    vec3 l = normalize(point_to_light);     // Direction from surface point to light
    vec3 h = normalize(l + v);            // Direction of the vector between l and v

    float NdotL = clamp(dot(n, l), 0.0, 1.0);
    float NdotV = clamp(dot(n, v), 0.0, 1.0);
    float NdotH = clamp(dot(n, h), 0.0, 1.0);
    float LdotH = clamp(dot(l, h), 0.0, 1.0);
    float VdotH = clamp(dot(v, h), 0.0, 1.0);

    return angular_info_t(
        NdotL,
        NdotV,
        NdotH,
        LdotH,
        VdotH,
        vec3(0, 0, 0)
    );
}

const float M_PI = 3.141592653589793;

// The following equation models the Fresnel reflectance term of the spec equation (aka F())
// Implementation of fresnel from [4], Equation 15
vec3 specular_reflection(material_info_t material_info, angular_info_t angular_info) {
    return material_info.reflectance0 + (material_info.reflectance90 - material_info.reflectance0) * pow(clamp(1.0 - angular_info.v_dot_h, 0.0, 1.0), 5.0);
}

// Smith Joint GGX
// Note: Vis = G / (4 * NdotL * NdotV)
// see Eric Heitz. 2014. Understanding the Masking-Shadowing Function in Microfacet-Based BRDFs. Journal of Computer Graphics Techniques, 3
// see Real-Time Rendering. Page 331 to 336.
// see https://google.github.io/filament/Filament.md.html#materialsystem/specularbrdf/geometricshadowing(specularg)
float visibility_occlusion(material_info_t material_info, angular_info_t angular_info) {
    float n_dot_l = angular_info.n_dot_l;
    float n_dot_v = angular_info.n_dot_v;
    float alpha_roughness_sq = material_info.alpha_roughness * material_info.alpha_roughness;

    float GGXV = n_dot_l * sqrt(n_dot_v * n_dot_v * (1.0 - alpha_roughness_sq) + alpha_roughness_sq);
    float GGXL = n_dot_v * sqrt(n_dot_l * n_dot_l * (1.0 - alpha_roughness_sq) + alpha_roughness_sq);
    float GGX = GGXV + GGXL;
    if (GGX > 0.0) {
        return 0.5 / GGX;
    }
    return 0.0;
}

// The following equation(s) model the distribution of microfacet normals across the area being drawn (aka D())
// Implementation from "Average Irregularity Representation of a Roughened Surface for Ray Reflection" by T. S. Trowbridge, and K. P. Reitz
// Follows the distribution function recommended in the SIGGRAPH 2013 course notes from EPIC Games [1], Equation 3.
float microfacet_distribution(material_info_t material_info, angular_info_t angular_info) {
    float alpha_roughness_sq = material_info.alpha_roughness * material_info.alpha_roughness;
    float f = (angular_info.n_dot_h * alpha_roughness_sq - angular_info.n_dot_h) * angular_info.n_dot_h + 1.0;
    return alpha_roughness_sq / (M_PI * f * f);
}

// Lambert lighting
// see https://seblagarde.wordpress.com/2012/01/08/pi-or-not-to-pi-in-game-lighting-equation/
vec3 diffuse(material_info_t material_info) {
    return material_info.diffuse_color / M_PI;
}

vec3 get_point_shade(vec3 point_to_light, material_info_t material_info, vec3 normal, vec3 view) {
    angular_info_t angular_info = get_angular_info(point_to_light, normal, view);
    if ((angular_info.n_dot_l > 0.0) || (angular_info.n_dot_v > 0.0)) {
        // Calculate the shading terms for the microfacet specular shading model
        vec3 F = specular_reflection(material_info, angular_info);
        float Vis = visibility_occlusion(material_info, angular_info);
        float D = microfacet_distribution(material_info, angular_info);

        // Calculation of analytical lighting contribution
        vec3 diffuse_contrib = (1.0 - F) * diffuse(material_info);
        // Use standard PBR specular (removed 3x boost for more realistic lighting)
        vec3 spec_contrib = F * Vis * D;

        // Obtain final intensity as reflectance (BRDF) scaled by the energy of the light (cosine law)
        return angular_info.n_dot_l * (diffuse_contrib + spec_contrib);
    }
    return vec3(0.0, 0.0, 0.0);
}

float get_range_attenuation(float range, float distance) {
    if (range < 0.0) {
        return 1.0;
    }
    return max(min(1.0 - pow(distance / range, 4.0), 1.0), 0.0) / pow(distance, 2.0);
}

// Calculate PBR lighting for a single light source
vec3 apply_single_light(int light_idx, material_info_t material_info, vec3 normal, vec3 view) {
    int light_type = int(light_positions[light_idx].w);
    vec3 light_pos = light_positions[light_idx].xyz;
    vec3 light_dir = light_directions[light_idx].xyz;
    vec3 light_color = light_colors[light_idx].rgb;
    float light_intensity = light_colors[light_idx].w;
    float light_range = light_params_data[light_idx].x;
    
    vec3 point_to_light;
    float attenuation = 1.0;
    
    if (light_type == LIGHT_TYPE_DIRECTIONAL) {
        // Directional light
        point_to_light = normalize(-light_dir);
        attenuation = 1.0;
    }
    else if (light_type == LIGHT_TYPE_POINT) {
        // Point light
        point_to_light = light_pos - v_pos;
        float distance = length(point_to_light);
        attenuation = get_range_attenuation(light_range, distance);
        point_to_light = normalize(point_to_light);
    }
    else if (light_type == LIGHT_TYPE_SPOT) {
        // Spot light
        point_to_light = light_pos - v_pos;
        float distance = length(point_to_light);
        point_to_light = normalize(point_to_light);
        
        // Spot cone calculation
        vec3 spot_dir = normalize(-light_dir);
        float theta = dot(-point_to_light, spot_dir);
        float inner_cutoff = light_directions[light_idx].w;  // cosine of inner angle
        float outer_cutoff = light_params_data[light_idx].y; // cosine of outer angle
        float epsilon = inner_cutoff - outer_cutoff;
        float spot_intensity = clamp((theta - outer_cutoff) / epsilon, 0.0, 1.0);
        
        attenuation = get_range_attenuation(light_range, distance) * spot_intensity;
    }
    else {
        return vec3(0.0); // Unknown light type
    }
    
    // Calculate PBR shading
    vec3 shade = get_point_shade(point_to_light, material_info, normal, view);
    return attenuation * light_intensity * light_color * shade;
}

// Apply all active lights
vec3 apply_all_lights(material_info_t material_info, vec3 normal, vec3 view) {
    vec3 total_light = vec3(0.0);
    
    for (int i = 0; i < num_lights && i < MAX_LIGHTS; i++) {
        total_light += apply_single_light(i, material_info, normal, view);
    }
    
    return total_light;
}

// Uncharted 2 tone map
// see: http://filmicworlds.com/blog/filmic-tonemapping-operators/
vec3 toneMapUncharted2Impl(vec3 color) {
    const float A = 0.15;
    const float B = 0.50;
    const float C = 0.10;
    const float D = 0.20;
    const float E = 0.02;
    const float F = 0.30;
    return ((color*(A*color+C*B)+D*E)/(color*(A*color+B)+D*F))-E/F;
}

vec3 toneMapUncharted(vec3 color) {
    const float W = 11.2;
    color = toneMapUncharted2Impl(color * 2.0);
    vec3 whiteScale = 1.0 / toneMapUncharted2Impl(vec3(W));
    return linear_to_srgb(color * whiteScale);
}

vec3 tone_map(vec3 color) {
    // color *= exposure;
    return toneMapUncharted(color);
}

void main() {
    // Step 1: Get base color
    vec4 base_color = (has_base_color_tex > 0.5)
        ? srgb_to_linear(texture(sampler2D(base_color_tex, base_color_smp), v_uv))
        : base_color_factor;
    base_color *= base_color_factor;
    
    // Step 2: Get metallic/roughness
    // glTF 2.0 Specification for metallicRoughnessTexture:
    // - Blue channel (.b) = metallic
    // - Green channel (.g) = roughness
    float metallic = metallic_factor;
    float perceptual_roughness = roughness_factor;
    if (has_metallic_roughness_tex > 0.5) {
        vec4 mr_sample = texture(sampler2D(metallic_roughness_tex, metallic_roughness_smp), v_uv);
        perceptual_roughness *= mr_sample.g; // GREEN channel = roughness (glTF 2.0 spec)
        metallic *= mr_sample.b;              // BLUE channel = metallic (glTF 2.0 spec)
    }
    
    // Use the material's actual roughness values for realistic PBR
    // (removed the 0.3x reduction that was making everything too shiny)
    
    // Step 3: Get normal using proper tangent-space transformation
    vec3 normal = get_normal();
    
    // Step 4: Get ambient occlusion (stored in Red channel)
    float occlusion = 1.0;
    if (has_occlusion_tex > 0.5) {
        occlusion = texture(sampler2D(occlusion_tex, occlusion_smp), v_uv).r;
    }
    
    // Step 5: Get emissive (self-illumination)
    vec3 emissive = emissive_factor;
    if (has_emissive_tex > 0.5) {
        vec3 emissive_sample = srgb_to_linear(texture(sampler2D(emissive_tex, emissive_smp), v_uv)).rgb;
        emissive *= emissive_sample;
    }
    
    // Clamp roughness to avoid artifacts (lower minimum for shinier surfaces)
    perceptual_roughness = clamp(perceptual_roughness, 0.01, 1.0);
    metallic = clamp(metallic, 0.0, 1.0);
    float alpha_roughness = perceptual_roughness * perceptual_roughness;
    
    // Calculate reflectance with enhanced metallic response
    vec3 f0 = vec3(0.04);
    vec3 diffuse_color = base_color.rgb * (vec3(1.0) - f0) * (1.0 - metallic);
    vec3 specular_color = mix(f0, base_color.rgb, metallic);
    
    float reflectance = max(max(specular_color.r, specular_color.g), specular_color.b);
    vec3 specular_environment_r0 = specular_color.rgb;
    // Boost specular for metals (increased from 50.0 to 100.0)
    vec3 specular_environment_r90 = vec3(clamp(reflectance * 100.0, 0.0, 1.0));
    
    material_info_t material_info = material_info_t(
        perceptual_roughness,
        specular_environment_r0,
        alpha_roughness,
        diffuse_color,
        specular_environment_r90,
        specular_color
    );
    
    // Apply PBR lighting from all lights
    vec3 view = normalize(v_eye_pos - v_pos);
    vec3 color = apply_all_lights(material_info, normal, view);
    
    // Enhanced ambient with metallic reflection
    vec3 ambient = base_color.rgb * 0.15; // Base ambient
    // Add extra specular reflection for metallic surfaces
    float fresnel = pow(1.0 - max(dot(normal, view), 0.0), 5.0);
    vec3 metallic_ambient = specular_color * metallic * (0.3 + fresnel * 0.4);
    ambient += metallic_ambient;
    color += ambient;
    
    color *= occlusion; // Apply ambient occlusion
    color += emissive;  // Add emissive glow
    
    // Tone mapping
    color = tone_map(color);
    
    frag_color = vec4(color, base_color.a);
}
@end

@program metallic vs metallic_fs
