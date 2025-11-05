/*
    Taken from the GLTF reference viewer:

    https://github.com/KhronosGroup/glTF-Sample-Viewer/tree/master/src/shaders
 */

// this is just here to test the `@module` prefix feature of the sokol-shdc
// C# code generator, other then that it's not needed
@module cgltf


@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4
@ctype vec3 System.Numerics.Vector3

@vs vs
// Force high precision on ARM32 GPUs (default is mediump which loses bone matrix precision)
precision highp float;
precision highp int;

const int MAX_BONES = 100;
const int MAX_BONE_INFLUENCE = 4;

@include vs_uniforms.glsl
@include vs_attributes.glsl


void main() {
    highp vec3 finalPosition = position;
    highp vec3 finalNormal = normal;
    
    // Apply skinning only if animation is available
#ifdef SKINNING
        bool hasValidBone = false;
        highp vec4 totalPosition = vec4(0.0);
        highp vec3 totalNormal = vec3(0.0);
        
        // Apply skinning if bones are present
        // Unroll loop explicitly to avoid dynamic array indexing issues on ARM32 drivers
        // ARM32 GLES3 drivers may have bugs with uniform array dynamic indexing
        
        // Bone influence 0
        if(weights.x > 0.0) {
            int boneId = int(boneIds.x + 0.5);
            if(boneId < MAX_BONES) {
                highp vec4 localPosition = finalBonesMatrices[boneId] * vec4(position, 1.0);
                totalPosition += localPosition * weights.x;
                highp vec3 localNormal = mat3(finalBonesMatrices[boneId]) * normal;
                totalNormal += localNormal * weights.x;
                hasValidBone = true;
            }
        }
        
        // Bone influence 1
        if(weights.y > 0.0) {
            int boneId = int(boneIds.y + 0.5);
            if(boneId < MAX_BONES) {
                highp vec4 localPosition = finalBonesMatrices[boneId] * vec4(position, 1.0);
                totalPosition += localPosition * weights.y;
                highp vec3 localNormal = mat3(finalBonesMatrices[boneId]) * normal;
                totalNormal += localNormal * weights.y;
                hasValidBone = true;
            }
        }
        
        // Bone influence 2
        if(weights.z > 0.0) {
            int boneId = int(boneIds.z + 0.5);
            if(boneId < MAX_BONES) {
                highp vec4 localPosition = finalBonesMatrices[boneId] * vec4(position, 1.0);
                totalPosition += localPosition * weights.z;
                highp vec3 localNormal = mat3(finalBonesMatrices[boneId]) * normal;
                totalNormal += localNormal * weights.z;
                hasValidBone = true;
            }
        }
        
        // Bone influence 3
        if(weights.w > 0.0) {
            int boneId = int(boneIds.w + 0.5);
            if(boneId < MAX_BONES) {
                highp vec4 localPosition = finalBonesMatrices[boneId] * vec4(position, 1.0);
                totalPosition += localPosition * weights.w;
                highp vec3 localNormal = mat3(finalBonesMatrices[boneId]) * normal;
                totalNormal += localNormal * weights.w;
                hasValidBone = true;
            }
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

@include fs_constants.glsl
@include brdf.glsl
@include fs_uniforms.glsl
@include fs_attributes.glsl
@include fs_structures.glsl
@include fs_functions.glsl
@include fs_lighting.glsl



void main() {
    // Step 1: Get base color
    // Manual sRGB to linear conversion (textures are RGBA8 format)
    vec4 base_color = (has_base_color_tex > 0.5)
        ? srgb_to_linear(texture(sampler2D(base_color_tex, base_color_smp), v_uv))
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
    
    // Step 2: Get metallic/roughness from texture
    float metallic = metallic_factor;
    float perceptual_roughness = roughness_factor;
    
    if (has_metallic_roughness_tex > 0.5) {
        // GLTF: Blue channel = metallic, Green channel = roughness
        vec4 mr_sample = texture(sampler2D(metallic_roughness_tex, metallic_roughness_smp), v_uv);
        metallic = mr_sample.b * metallic_factor;
        perceptual_roughness = mr_sample.g * roughness_factor;
    }
    
    // Step 3: Get normal using proper tangent-space transformation
    // For double-sided materials, flip normal to face the camera if needed
    vec3 normal = get_normal(has_normal_tex, v_pos, v_nrm, v_uv,
                            normal_tex, normal_smp,
                            normal_tex_offset, normal_tex_rotation, normal_tex_scale,
                            normal_map_scale);
    if (!gl_FrontFacing) {
        normal = -normal;  // Flip normal for backfaces (double-sided rendering)
    }
    
    // Step 4: Get ambient occlusion (stored in Red channel)
    float occlusion = 1.0;
    if (has_occlusion_tex > 0.5) {
        occlusion = texture(sampler2D(occlusion_tex, occlusion_smp), v_uv).r;
    }
    
    // Step 5: Get emissive (self-illumination)
    // Manual sRGB to linear conversion (textures are RGBA8 format)
    vec3 emissive = emissive_factor;
    if (has_emissive_tex > 0.5) {
        vec3 emissive_sample = srgb_to_linear(texture(sampler2D(emissive_tex, emissive_smp), v_uv)).rgb;
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
    
    // Calculate F0 from IOR (KHR_materials_ior)
    // Formula: F0 = ((ior - 1) / (ior + 1))^2
    // This is CRITICAL for proper IOR rendering
    float ior_to_f0 = pow((ior - 1.0) / (ior + 1.0), 2.0);
    vec3 f0 = vec3(ior_to_f0);
    
    // For dielectrics (non-metals), use IOR-based F0
    // For metals, use base color as F0 (standard metallic workflow)
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
    
    // Get combined lighting (diffuse + specular)
    vec3 color = apply_all_lights(material_info, normal, view, v_pos);
    
    // Ambient lighting with IOR-aware adjustments
    // High-IOR materials (glass, diamond) need strong ambient to simulate environment reflections
    float n_dot_v = max(dot(normal, view), 0.0);
    
    // Fresnel for ambient lighting (view-dependent reflectance)
    vec3 ambient_fresnel = specular_environment_r0 + (specular_environment_r90 - specular_environment_r0) * pow(1.0 - n_dot_v, 5.0);
    
    // Base ambient strength
    float effective_ambient = ambient_strength;
    
    // Boost ambient for high-IOR materials (they need visible reflections)
    // IOR 1.0 (air) -> F0=0.0 -> no boost
    // IOR 1.5 (glass) -> F0=0.04 -> moderate boost  
    // IOR 2.42 (diamond) -> F0=0.17 -> strong boost
    float ior_boost = mix(1.0, 30.0, ior_to_f0);  // Scale from 1x to 30x based on F0
    effective_ambient = ambient_strength * ior_boost;
    
    // Apply ambient: diffuse for non-metals, Fresnel-based specular for all
    vec3 ambient_diffuse = (1.0 - metallic) * diffuse_color * ambient_strength;
    vec3 ambient_specular = ambient_fresnel * effective_ambient;
    vec3 ambient = ambient_diffuse + ambient_specular;
    color += ambient;
    
    // Apply ambient occlusion
    color *= occlusion;
    
    // NOTE: Emissive will be added AFTER transmission, so it always shows through glass
    
    // Apply transmission (glass/refraction) BEFORE volume absorption
    // Following glTF-Sample-Viewer approach: replace diffuse component with transmission
    if (transmission_factor > 0.0) {
        // Calculate refracted background color using proper 3D ray tracing
        vec3 refracted_color = calculate_refraction(
            v_pos,                              // Fragment position in world space
            normal,                             // Surface normal
            view,                               // View direction
            ior,                                // Index of refraction
            thickness_factor,                   // Material thickness
            material_info.perceptual_roughness, // Roughness for blur
            base_color.rgb,                     // Base color tint
            attenuation_color,                  // Volume absorption color
            attenuation_distance,               // Volume absorption distance
            screen_tex,                         // Screen texture for refraction
            screen_smp,                         // Screen texture sampler
            model_matrix,                       // Model matrix
            view_matrix,                        // View matrix
            projection_matrix                   // Projection matrix
        );
        
        // glTF-Sample-Viewer approach: Mix current color with refracted background
        // This preserves specular highlights while showing refraction
        // The transmission replaces the diffuse component, specular remains via Fresnel
        color = mix(color, refracted_color, transmission_factor);
    }
    // Apply volume absorption ONLY for non-transmissive materials
    // (e.g., solid colored objects that aren't glass)
    else if (thickness_factor > 0.0 && attenuation_distance < 1e10) {
        // Beer's Law: Intensity = I0 * exp(-absorption_coefficient * distance)
        // attenuation_color is the target color at attenuation_distance
        vec3 absorption = -log(max(attenuation_color, vec3(0.001))) / max(attenuation_distance, 0.001);
        vec3 volume_color = exp(-absorption * thickness_factor);
        
        // Apply volume color tint to the rendered surface
        color *= volume_color;
    }
    
    // Apply clearcoat layer (KHR_materials_clearcoat) - glTF spec compliant
    if (clearcoat_factor > 0.0) {
        // Use geometric normal for clearcoat (smooth glossy layer)
        vec3 clearcoat_normal = normalize(v_nrm);
        float clearcoat_NdotV = max(abs(dot(clearcoat_normal, view)), 0.0);
        
        // Clearcoat parameters
        float clearcoat_alpha = clearcoat_roughness * clearcoat_roughness;
        
        // Clearcoat Fresnel (f0 = 0.04 for IOR 1.5)
        float clearcoat_f0 = 0.04;
        float clearcoat_fresnel = clearcoat_f0 + (1.0 - clearcoat_f0) * pow(1.0 - clearcoat_NdotV, 5.0);
        
        // Calculate clearcoat specular from lights
        vec3 clearcoat_specular = vec3(0.0);
        for (int i = 0; i < num_lights && i < MAX_LIGHTS; i++) {
            int light_type = int(light_positions[i].w);
            vec3 light_pos = light_positions[i].xyz;
            vec3 light_dir_data = light_directions[i].xyz;
            vec3 light_color = light_colors[i].rgb * light_colors[i].w;
            float light_range = light_params_data[i].x;
            
            vec3 L;
            float attenuation = 1.0;
            
            if (light_type == LIGHT_TYPE_DIRECTIONAL) {
                L = normalize(-light_dir_data);
            } else if (light_type == LIGHT_TYPE_POINT) {
                vec3 to_light = light_pos - v_pos;
                float distance = length(to_light);
                L = normalize(to_light);
                attenuation = get_range_attenuation(light_range, distance);
            } else if (light_type == LIGHT_TYPE_SPOT) {
                vec3 to_light = light_pos - v_pos;
                float distance = length(to_light);
                L = normalize(to_light);
                float spot_theta = dot(-L, normalize(-light_dir_data));
                float inner_cutoff = light_directions[i].w;
                float outer_cutoff = light_params_data[i].y;
                float spot_intensity = clamp((spot_theta - outer_cutoff) / (inner_cutoff - outer_cutoff), 0.0, 1.0);
                attenuation = get_range_attenuation(light_range, distance) * spot_intensity;
            } else {
                continue;
            }
            
            float clearcoat_NdotL = max(dot(clearcoat_normal, L), 0.0);
            if (clearcoat_NdotL > 0.0) {
                vec3 H = normalize(view + L);
                float clearcoat_NdotH = max(dot(clearcoat_normal, H), 0.0);
                
                // GGX distribution
                float a2 = clearcoat_alpha * clearcoat_alpha;
                float denom = clearcoat_NdotH * clearcoat_NdotH * (a2 - 1.0) + 1.0;
                float D = a2 / (M_PI * denom * denom);
                
                // Geometry term
                float k = clearcoat_alpha / 2.0;
                float G_V = clearcoat_NdotV / (clearcoat_NdotV * (1.0 - k) + k);
                float G_L = clearcoat_NdotL / (clearcoat_NdotL * (1.0 - k) + k);
                float G = G_V * G_L;
                
                // Specular BRDF
                float spec = (D * G) / max(4.0 * clearcoat_NdotV * clearcoat_NdotL, 0.001);
                
                clearcoat_specular += light_color * spec * clearcoat_NdotL * attenuation;
            }
        }
        
        // glTF spec layering: Clearcoat is additive on top of base layer
        // The base layer is attenuated by the clearcoat's Fresnel term (energy conservation)
        // Then the clearcoat specular is added on top
        float clearcoat_contribution = clearcoat_factor * clearcoat_fresnel;
        color = color * (1.0 - clearcoat_contribution) + clearcoat_specular * clearcoat_factor;
    }
    
    // Add emissive AFTER all transmission/refraction/clearcoat calculations
    // This ensures emissive logos show through transparent/glass materials
    color += emissive;
    
    // Apply tone mapping (Uncharted 2) and gamma correction
    frag_color = vec4(tone_map(color), base_color.a);
}
@end

@program metallic vs metallic_fs

// Include bloom post-processing shaders
@include bloom.glsl
