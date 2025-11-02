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

layout(location=0) in vec3 position;  // FIXED: Was vec4, should be vec3 to match Vertex struct!
layout(location=1) in vec3 normal;
layout(location=2) in vec4 color;
layout(location=3) in vec2 texcoord;
layout(location=4) in vec4 boneIds;  // Changed from uvec4 for WebGL compatibility
layout(location=5) in vec4 weights;


out vec3 v_pos;
out vec3 v_nrm;
out vec4 v_color;
out vec2 v_uv;
out vec3 v_eye_pos;

void main() {
    vec3 finalPosition = position;
    vec3 finalNormal = normal;
    
    // Apply skinning only if animation is available
#ifdef SKINNING
        bool hasValidBone = false;
        vec4 totalPosition = vec4(0.0);
        vec3 totalNormal = vec3(0.0);
        
        // Apply skinning if bones are present
        for(int i = 0; i < MAX_BONE_INFLUENCE; i++)
        {
            // Skip if weight is zero or bone ID is invalid
            if(weights[i] <= 0.0) 
                continue;
            int boneId = int(boneIds[i]);  // Convert float to int
            if(boneId >= MAX_BONES) 
            {
                totalPosition = vec4(position, 1.0);
                totalNormal = normal;
                hasValidBone = true;
                break;
            }
            vec4 localPosition = finalBonesMatrices[boneId] * vec4(position, 1.0);
            totalPosition += localPosition * weights[i];
            vec3 localNormal = mat3(finalBonesMatrices[boneId]) * normal;
            totalNormal += localNormal * weights[i];
            hasValidBone = true;
        }
        
        // If valid bone influences found, use them (extract xyz from vec4)
        if (hasValidBone) {
            finalPosition = totalPosition.xyz;
            finalNormal = totalNormal;
        }
#endif
    
    vec4 pos = model * vec4(finalPosition, 1.0);
    v_pos = pos.xyz / pos.w;
    
    // Calculate normal matrix as inverse transpose of model matrix (upper-left 3x3)
    // For transforms without non-uniform scaling, this simplifies to mat3(model)
    // But for correctness, we use transpose(inverse(mat3(model)))
    mat3 normal_matrix = transpose(inverse(mat3(model)));
    v_nrm = normalize(normal_matrix * finalNormal);
    
    v_color = color;
    v_uv = texcoord;
    v_eye_pos = eye_pos;
    gl_Position = view_proj * pos;
}
@end

@fs metallic_fs

in vec3 v_pos;
in vec3 v_nrm;
in vec4 v_color;
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
    float metallic;                 // metallic value (0 = dielectric, 1 = metal)
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
    // Clearcoat parameters - KHR_materials_clearcoat (additional specular layer)
    float clearcoat_factor;     // 0.0 = no clearcoat, 1.0 = full clearcoat layer
    float clearcoat_roughness;  // Roughness of clearcoat layer (0.0 = mirror-like)
    // Texture transform parameters - KHR_texture_transform
    // Base color texture transform (offset.xy, rotation, scale.xy)
    vec2 base_color_tex_offset;
    float base_color_tex_rotation;
    vec2 base_color_tex_scale;
    // Normal texture transform
    vec2 normal_tex_offset;
    float normal_tex_rotation;
    vec2 normal_tex_scale;
    // Metallic/Roughness texture transform
    vec2 metallic_roughness_tex_offset;
    float metallic_roughness_tex_rotation;
    vec2 metallic_roughness_tex_scale;
    // Emissive texture transform
    vec2 emissive_tex_offset;
    float emissive_tex_rotation;
    vec2 emissive_tex_scale;
    // Occlusion texture transform
    vec2 occlusion_tex_offset;
    float occlusion_tex_rotation;
    vec2 occlusion_tex_scale;
};

const int MAX_LIGHTS = 4;
const int LIGHT_TYPE_DIRECTIONAL = 0;
const int LIGHT_TYPE_POINT = 1;
const int LIGHT_TYPE_SPOT = 2;

layout(binding=2) uniform light_params {
    int num_lights;
    float ambient_strength;  // Controllable ambient light strength
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
layout(binding=5) uniform texture2D screen_tex;  // Screen texture for refraction (transmission pass)

layout(binding=0) uniform sampler base_color_smp;
layout(binding=1) uniform sampler metallic_roughness_smp;
layout(binding=2) uniform sampler normal_smp;
layout(binding=3) uniform sampler occlusion_smp;
layout(binding=4) uniform sampler emissive_smp;
layout(binding=5) uniform sampler screen_smp;  // Sampler for screen texture

vec3 linear_to_srgb(vec3 linear) {
    return pow(linear, vec3(1.0/2.2));
}

vec4 srgb_to_linear(vec4 srgb) {
    return vec4(pow(srgb.rgb, vec3(2.2)), srgb.a);
}

// Apply texture transform (KHR_texture_transform)
// Transforms UV coordinates using offset, rotation, and scale
vec2 apply_texture_transform(vec2 uv, vec2 offset, float rotation, vec2 scale) {
    // Apply scale and offset first
    vec2 transformed = uv * scale + offset;
    
    // Apply rotation around (0,0)
    // Note: rotation is in radians, counter-clockwise
    if (abs(rotation) > 0.0001) {
        float cos_rot = cos(rotation);
        float sin_rot = sin(rotation);
        mat2 rotation_matrix = mat2(cos_rot, sin_rot, -sin_rot, cos_rot);
        transformed = rotation_matrix * (transformed - vec2(0.5)) + vec2(0.5);
    }
    
    return transformed;
}

vec3 get_normal() {
    if (has_normal_tex < 0.5) {
        return normalize(v_nrm);
    }
    
    // Apply texture transform to normal map UVs
    vec2 normal_uv = apply_texture_transform(v_uv, normal_tex_offset, normal_tex_rotation, normal_tex_scale);
    
    // Calculate TBN matrix using screen-space derivatives
    vec3 pos_dx = dFdx(v_pos);
    vec3 pos_dy = dFdy(v_pos);
    vec2 tex_dx = dFdx(normal_uv);
    vec2 tex_dy = dFdy(normal_uv);
    
    // Calculate tangent vector
    float det = tex_dx.s * tex_dy.t - tex_dy.s * tex_dx.t;
    vec3 t = (tex_dy.t * pos_dx - tex_dx.t * pos_dy) / det;
    
    // Gram-Schmidt orthogonalization
    vec3 ng = normalize(v_nrm);
    t = normalize(t - ng * dot(ng, t));
    vec3 b = cross(ng, t);  // No need to normalize again since ng and t are orthonormal
    mat3 tbn = mat3(t, b, ng);
    
    // GLTF normal maps: RGB channels, stored as RGBA8
    // Normal maps are already in LINEAR space (they're direction vectors, not colors)
    // DO NOT apply sRGB->linear conversion!
    vec3 normal_sample = texture(sampler2D(normal_tex, normal_smp), normal_uv).rgb;
    // Convert from [0,1] to [-1,1] tangent space
    vec3 n = normal_sample * 2.0 - 1.0;
    // Transform from tangent space to world space
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

// Geometry function (Schlick-GGX)
// Matches LearnOpenGL PBR implementation
float geometry_schlick_ggx(float n_dot_v, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    
    float nom = n_dot_v;
    float denom = n_dot_v * (1.0 - k) + k;
    
    return nom / denom;
}

// Smith's method combines the geometry obstruction and shadowing
float geometry_smith(material_info_t material_info, angular_info_t angular_info) {
    float n_dot_v = angular_info.n_dot_v;
    float n_dot_l = angular_info.n_dot_l;
    float roughness = material_info.perceptual_roughness;
    
    float ggx2 = geometry_schlick_ggx(n_dot_v, roughness);
    float ggx1 = geometry_schlick_ggx(n_dot_l, roughness);
    
    return ggx1 * ggx2;
}

// The following equation(s) model the distribution of microfacet normals across the area being drawn (aka D())
// Implementation from "Average Irregularity Representation of a Roughened Surface for Ray Reflection" by T. S. Trowbridge, and K. P. Reitz
// Follows the distribution function recommended in the SIGGRAPH 2013 course notes from EPIC Games [1], Equation 3.
// Matches LearnOpenGL PBR implementation (GGX/Trowbridge-Reitz)
float microfacet_distribution(material_info_t material_info, angular_info_t angular_info) {
    float a = material_info.perceptual_roughness * material_info.perceptual_roughness;
    float a2 = a * a;
    float NdotH = angular_info.n_dot_h;
    float NdotH2 = NdotH * NdotH;
    
    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = M_PI * denom * denom;
    
    return nom / denom;
}

// Lambert lighting
// see https://seblagarde.wordpress.com/2012/01/08/pi-or-not-to-pi-in-game-lighting-equation/
vec3 diffuse(material_info_t material_info) {
    return material_info.diffuse_color / M_PI;
}

// Smith Joint GGX - proper visibility term
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

vec3 get_point_shade(vec3 point_to_light, material_info_t material_info, vec3 normal, vec3 view) {
    angular_info_t angular_info = get_angular_info(point_to_light, normal, view);
    if ((angular_info.n_dot_l > 0.0) || (angular_info.n_dot_v > 0.0)) {
        // Calculate the shading terms for the microfacet specular shading model
        vec3 F = specular_reflection(material_info, angular_info);
        float Vis = visibility_occlusion(material_info, angular_info);
        float D = microfacet_distribution(material_info, angular_info);

        // Calculation of analytical lighting contribution
        vec3 diffuse_contrib = (1.0 - F) * diffuse(material_info);
        
        // Boost specular for metals to make them more visible and shiny
        float metallic = material_info.metallic;
        float spec_boost = mix(1.0, 2.5, metallic);  // 2.5x boost for metals (increased)
        vec3 spec_contrib = F * Vis * D * spec_boost;

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
    
    // Calculate PBR shading with proper attenuation
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
    return toneMapUncharted(color);
}

// Calculate refracted color for transmission/glass materials
// Uses screen-space refraction with IOR-based ray bending
vec3 calculate_refraction(vec3 normal, vec3 view, float ior, float transmission_factor) {
    // Calculate refraction direction using Snell's law
    // Assuming we're going from air (IOR=1.0) into the material
    float eta = 1.0 / ior;  // Ratio of indices (air to material)
    
    // Refract the view ray through the surface
    vec3 refracted = refract(-view, normal, eta);
    
    // If total internal reflection occurs, use reflection instead
    if (length(refracted) < 0.01) {
        refracted = reflect(-view, normal);
    }
    
    // Convert fragment position to screen space UV coordinates
    vec2 screen_uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(screen_tex, screen_smp), 0));
    
    // Distort UV based on refracted direction
    // The strength of distortion depends on the angle of refraction and transmission factor
    float distortion_strength = 0.1 * transmission_factor;  // Scale factor for visible effect
    vec2 distorted_uv = screen_uv + refracted.xy * distortion_strength;
    
    // Clamp UV to valid range to avoid sampling outside texture
    distorted_uv = clamp(distorted_uv, vec2(0.0), vec2(1.0));
    
    // Sample the background (screen texture) at distorted coordinates
    vec3 refracted_color = texture(sampler2D(screen_tex, screen_smp), distorted_uv).rgb;
    
    return refracted_color;
}

void main() {
    // Step 1: Get base color
    // Apply texture transform to base color UVs
    vec2 base_color_uv = apply_texture_transform(v_uv, base_color_tex_offset, base_color_tex_rotation, base_color_tex_scale);
    
    // Manual sRGB to linear conversion (textures are RGBA8 format)
    vec4 base_color = (has_base_color_tex > 0.5)
        ? srgb_to_linear(texture(sampler2D(base_color_tex, base_color_smp), base_color_uv))
        : base_color_factor;
    base_color *= base_color_factor;
    
    // DEBUG: Force white base color to test if textures are the problem
    // base_color = vec4(1.0, 1.0, 1.0, 1.0);
    
    // Multiply with vertex color (for GLTF vertex colors or material colors)
    base_color *= v_color;
    
    // Alpha testing for MASK mode (alpha_cutoff > 0.0 indicates MASK mode)
    if (alpha_cutoff > 0.0 && base_color.a < alpha_cutoff) {
        discard;
    }
    
    // Step 2: Get metallic/roughness from texture (with texture transform)
    float metallic = metallic_factor;
    float perceptual_roughness = roughness_factor;
    
    if (has_metallic_roughness_tex > 0.5) {
        // Apply texture transform to UV coordinates
        vec2 mr_uv = apply_texture_transform(v_uv, metallic_roughness_tex_offset, metallic_roughness_tex_rotation, metallic_roughness_tex_scale);
        // GLTF: Blue channel = metallic, Green channel = roughness
        vec4 mr_sample = texture(sampler2D(metallic_roughness_tex, metallic_roughness_smp), mr_uv);
        metallic = mr_sample.b * metallic_factor;
        perceptual_roughness = mr_sample.g * roughness_factor;
    }
    
    // Step 3: Get normal using proper tangent-space transformation (transform applied in get_normal())
    vec3 normal = get_normal();
    
    // Apply clearcoat normal smoothing: blend toward geometric normal
    // Strong clearcoat significantly reduces normal map influence for smooth glossy appearance
    if (clearcoat_factor > 0.01) {
        vec3 geometric_normal = normalize(v_nrm);
        // Blend toward geometric normal based on clearcoat strength
        // With full clearcoat (factor=1), use almost entirely geometric normal for mirror-like glossiness
        float blend_factor = clearcoat_factor * 0.97; // Very strong blending for maximum glossiness
        normal = normalize(mix(normal, geometric_normal, blend_factor));
    }
    
    // Step 4: Get ambient occlusion (stored in Red channel) with texture transform
    float occlusion = 1.0;
    if (has_occlusion_tex > 0.5) {
        vec2 occlusion_uv = apply_texture_transform(v_uv, occlusion_tex_offset, occlusion_tex_rotation, occlusion_tex_scale);
        occlusion = texture(sampler2D(occlusion_tex, occlusion_smp), occlusion_uv).r;
    }
    
    // Step 5: Get emissive (self-illumination) with texture transform
    // Manual sRGB to linear conversion (textures are RGBA8 format)
    vec3 emissive = emissive_factor;
    if (has_emissive_tex > 0.5) {
        vec2 emissive_uv = apply_texture_transform(v_uv, emissive_tex_offset, emissive_tex_rotation, emissive_tex_scale);
        vec3 emissive_sample = srgb_to_linear(texture(sampler2D(emissive_tex, emissive_smp), emissive_uv)).rgb;
        emissive *= emissive_sample;
    }
    // Apply emissive strength (KHR_materials_emissive_strength extension)
    emissive *= emissive_strength;
    // Boost emissive to make glowing details more visible
    emissive *= 3.0;
    
    // Clamp roughness to avoid artifacts
    perceptual_roughness = clamp(perceptual_roughness, 0.04, 1.0);
    metallic = clamp(metallic, 0.0, 1.0);
    float alpha_roughness = perceptual_roughness * perceptual_roughness;
    
    // Calculate reflectance - standard PBR approach (LearnOpenGL method)
    vec3 f0 = vec3(0.04);
    vec3 diffuse_color = base_color.rgb * (vec3(1.0) - f0) * (1.0 - metallic);
    vec3 specular_color = mix(f0, base_color.rgb, metallic);
    
    float reflectance = max(max(specular_color.r, specular_color.g), specular_color.b);
    vec3 specular_environment_r0 = specular_color.rgb;
    vec3 specular_environment_r90 = vec3(clamp(reflectance * 50.0, 0.0, 1.0));
    
    material_info_t material_info = material_info_t(
        perceptual_roughness,
        specular_environment_r0,
        alpha_roughness,
        diffuse_color,
        specular_environment_r90,
        specular_color,
        metallic                    // Pass metallic value to material_info
    );
    
    // Apply PBR lighting from all lights
    vec3 view = normalize(v_eye_pos - v_pos);
    vec3 color = apply_all_lights(material_info, normal, view);
    
    // Ambient lighting - controllable via uniform
    // Metals need to stay dark to look shiny and reflective
    vec3 ambient_diffuse = (1.0 - metallic) * diffuse_color * ambient_strength;
    vec3 ambient_specular = metallic * specular_color * ambient_strength * 0.5;  // Even less for metals
    vec3 ambient = ambient_diffuse + ambient_specular;
    color += ambient;
    
    // Apply ambient occlusion
    color *= occlusion;
    
    // Add emissive
    color += emissive;
    
    // Apply volume absorption (Beer's Law) - ALWAYS applies when thickness > 0
    // This creates the colored tint effect (e.g., golden amber, orange dragon)
    // This is separate from transmission/refraction
    if (thickness_factor > 0.0 && attenuation_distance < 1e10) {
        // Beer's Law: Intensity = I0 * exp(-absorption_coefficient * distance)
        // attenuation_color is the target color at attenuation_distance
        vec3 absorption = -log(max(attenuation_color, vec3(0.001))) / max(attenuation_distance, 0.001);
        vec3 volume_color = exp(-absorption * thickness_factor);
        
        // Apply volume color tint to the rendered surface
        color *= volume_color;
    }
    
    // Apply transmission (glass/refraction) if enabled AND transmission_factor > 0
    // This adds refraction effect on top of the volume-tinted color
    if (transmission_factor > 0.0) {
        // Calculate refracted background color
        vec3 refracted_color = calculate_refraction(normal, view, ior, transmission_factor);
        
        // Apply the same volume absorption to the refracted background
        if (thickness_factor > 0.0 && attenuation_distance < 1e10) {
            vec3 absorption = -log(max(attenuation_color, vec3(0.001))) / max(attenuation_distance, 0.001);
            vec3 volume_color = exp(-absorption * thickness_factor);
            refracted_color *= volume_color;
        }
        
        // Mix between surface color and refracted background based on transmission factor
        // transmission_factor = 0.0 -> fully opaque (use surface color)
        // transmission_factor = 1.0 -> fully transparent (use refracted background)
        color = mix(color, refracted_color, transmission_factor);
        
        // For glass materials, also apply some Fresnel effect
        // Objects are more transparent when viewed straight on, more reflective at grazing angles
        float n_dot_v = max(dot(normal, view), 0.0);
        float fresnel = pow(1.0 - n_dot_v, 5.0);  // Schlick's approximation
        
        // Reduce transmission at grazing angles (makes edges more reflective)
        float effective_transmission = transmission_factor * (1.0 - fresnel * 0.5);
        color = mix(color, refracted_color, effective_transmission);
    }
    
    // Apply clearcoat layer (KHR_materials_clearcoat)
    // This adds an additional specular reflection layer on top of the base material
    // Common use case: car paint, varnished wood, coated surfaces
    if (clearcoat_factor > 0.01) {
        // Clearcoat uses geometric normal (not perturbed by base normal map)
        // This creates the smooth glossy layer effect on top of a rough base
        vec3 clearcoat_normal = normalize(v_nrm);
        
        // Clearcoat is a dielectric layer (non-metallic), so we use F0 = 0.04
        vec3 clearcoat_f0 = vec3(0.04);
        vec3 clearcoat_f90 = vec3(1.0);
        
        // Create material info for clearcoat layer
        // For smooth clearcoat (roughness near 0), we want glossy mirror-like highlights
        // Only add minimum roughness for very rough clearcoats to avoid artifacts
        float clearcoat_perceptual = max(clearcoat_roughness, 0.01);
        float clearcoat_alpha_roughness = clearcoat_perceptual * clearcoat_perceptual;
        material_info_t clearcoat_material = material_info_t(
            clearcoat_perceptual,     // perceptualRoughness (use clamped value)
            clearcoat_f0,             // specularEnvironmentR0
            clearcoat_alpha_roughness,// alphaRoughness
            vec3(0.0),                // diffuseColor (clearcoat is pure specular)
            clearcoat_f90,            // specularEnvironmentR90
            clearcoat_f0,             // f0 (for metals, but clearcoat is dielectric)
            0.0                       // metallic (clearcoat is always dielectric)
        );
        
        // Calculate clearcoat Fresnel for energy conservation (Schlick's approximation)
        float clearcoat_NdotV = max(dot(clearcoat_normal, view), 0.0);
        float clearcoat_fresnel = 0.04 + (1.0 - 0.04) * pow(1.0 - clearcoat_NdotV, 5.0);
        
        // Calculate clearcoat specular reflection
        vec3 clearcoat_specular = vec3(0.0);
        for (int i = 0; i < num_lights && i < MAX_LIGHTS; i++) {
            vec3 light_pos = light_positions[i].xyz;
            int light_type = int(light_positions[i].w);
            vec3 light_dir_unnorm = light_pos - v_pos;
            vec3 light_dir = normalize(light_dir_unnorm);
            
            vec3 light_color = light_colors[i].rgb;
            float light_intensity = light_colors[i].w;
            
            // Calculate light contribution based on type
            vec3 pointToLight = light_dir;
            float attenuation = 1.0;
            
            if (light_type == LIGHT_TYPE_POINT) {
                float distance = length(light_dir_unnorm);
                attenuation = 1.0 / (distance * distance);
            }
            
            // Calculate clearcoat specular using microfacet BRDF with geometric normal
            angular_info_t angular_info = get_angular_info(pointToLight, clearcoat_normal, view);
            
            vec3 clearcoat_brdf = specular_reflection(clearcoat_material, angular_info);
            clearcoat_specular += clearcoat_brdf * angular_info.n_dot_l * light_color * light_intensity * attenuation;
        }
        
        // Apply clearcoat with energy conservation
        // Boost clearcoat specular for bright glossy highlights
        // Smooth clearcoat (low roughness) gets strong boost for wet-paint, mirror-like appearance
        float clearcoat_spec_boost = mix(4.0, 10.0, clearcoat_factor * (1.0 - clearcoat_roughness));
        clearcoat_specular *= clearcoat_spec_boost;
        
        // Standard energy conservation with Fresnel
        float clearcoat_attenuation = 1.0 - clearcoat_factor * clearcoat_fresnel;
        color = color * clearcoat_attenuation + clearcoat_specular * clearcoat_factor;
    }
    
    // Apply tone mapping (Uncharted 2) and gamma correction
    frag_color = vec4(tone_map(color), base_color.a);
}
@end

@program metallic vs metallic_fs

//------------------------------------------------------------------------------
// Bloom Post-Processing Shaders
//------------------------------------------------------------------------------

// Fullscreen quad vertex shader for post-processing  
@vs vs_fullscreen
@msl_options flip_vert_y
@hlsl_options flip_vert_y

layout(location=0) in vec2 position;
out vec2 uv;

void main() {
    gl_Position = vec4(position, 0.0, 1.0);
    // Convert from NDC [-1,1] to UV [0,1]
    uv = (position + 1.0) * 0.5;
}
@end

// Bright pass extraction - extracts pixels above brightness threshold
@fs fs_bright_pass
layout(binding=0) uniform texture2D scene_texture;
layout(binding=0) uniform sampler scene_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec4 color = texture(sampler2D(scene_texture, scene_sampler), uv);
    
    // For HDR/emissive materials, check max color component instead of just luminance
    // This captures bright emissive colors better (e.g., bright blue emissive)
    float brightness = max(max(color.r, color.g), color.b);
    
    // Smooth threshold - gradually fade in bloom contribution
    // knee controls how soft the transition is (0.1 = very soft)
    float knee = 0.1;
    float soft = smoothstep(brightness_threshold - knee, brightness_threshold + knee, brightness);
    
    // Only keep pixels that pass the threshold, preserve their full brightness
    frag_color = vec4(color.rgb * soft, 1.0);
}
@end

// Gaussian blur horizontal pass
@fs fs_blur_horizontal
layout(binding=0) uniform texture2D input_texture;
layout(binding=0) uniform sampler input_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec3 color = vec3(0.0);
    
    // 9-tap Gaussian blur weights
    float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    
    // Sample center
    color += texture(sampler2D(input_texture, input_sampler), uv).rgb * weights[0];
    
    // Sample both directions
    for (int i = 1; i < 5; i++) {
        float offset = float(i) * texel_size.x;
        color += texture(sampler2D(input_texture, input_sampler), uv + vec2(offset, 0.0)).rgb * weights[i];
        color += texture(sampler2D(input_texture, input_sampler), uv - vec2(offset, 0.0)).rgb * weights[i];
    }
    
    frag_color = vec4(color, 1.0);
}
@end

// Gaussian blur vertical pass
@fs fs_blur_vertical
layout(binding=0) uniform texture2D input_texture;
layout(binding=0) uniform sampler input_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec3 color = vec3(0.0);
    
    // 9-tap Gaussian blur weights
    float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    
    // Sample center
    color += texture(sampler2D(input_texture, input_sampler), uv).rgb * weights[0];
    
    // Sample both directions
    for (int i = 1; i < 5; i++) {
        float offset = float(i) * texel_size.y;
        color += texture(sampler2D(input_texture, input_sampler), uv + vec2(0.0, offset)).rgb * weights[i];
        color += texture(sampler2D(input_texture, input_sampler), uv - vec2(0.0, offset)).rgb * weights[i];
    }
    
    frag_color = vec4(color, 1.0);
}
@end

// Final composite - combines original scene with bloom
@fs fs_bloom_composite
layout(binding=0) uniform texture2D scene_texture;
layout(binding=1) uniform texture2D bloom_texture;
layout(binding=0) uniform sampler scene_sampler;
layout(binding=1) uniform sampler bloom_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec3 scene_color = texture(sampler2D(scene_texture, scene_sampler), uv).rgb;
    vec3 bloom_color = texture(sampler2D(bloom_texture, bloom_sampler), uv).rgb;
    
    // Additive blending with intensity control
    vec3 final_color = scene_color + bloom_color * bloom_intensity;
    
    frag_color = vec4(final_color, 1.0);
}
@end

// Bloom shader programs
@program bright_pass vs_fullscreen fs_bright_pass
@program blur_horizontal vs_fullscreen fs_blur_horizontal  
@program blur_vertical vs_fullscreen fs_blur_vertical
@program bloom_composite vs_fullscreen fs_bloom_composite
