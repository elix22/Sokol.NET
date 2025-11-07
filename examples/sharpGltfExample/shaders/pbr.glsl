/*
    Complete PBR shader with Image-Based Lighting (IBL)
    Based on the Khronos glTF-Sample-Viewer reference implementation
    
    Implements:
    - Metallic-Roughness workflow
    - Image-Based Lighting (IBL)
    - Punctual lights (directional, point, spot)
    - Normal mapping
    - Occlusion
    - Emissive
    - Alpha modes (opaque, mask, blend)
*/

@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4
@ctype vec3 System.Numerics.Vector3
@ctype vec2 System.Numerics.Vector2

// ============================================================================
// VERTEX SHADER
// ============================================================================

@vs vs_pbr
precision highp float;

// Vertex attributes
layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;
layout(location=2) in vec4 color;
layout(location=3) in vec2 texcoord;

// Uniforms
@include vs_uniforms.glsl

out vec3 v_Position;
out vec2 v_TexCoord0;
out vec3 v_Normal;

void main() {
    vec4 pos = model * vec4(position, 1.0);
    v_Position = pos.xyz / pos.w;
    v_TexCoord0 = texcoord;
    
    // Transform normal to world space
    mat3 normalMatrix = transpose(inverse(mat3(model)));
    v_Normal = normalize(normalMatrix * normal);
    
    gl_Position = view_proj * pos;
}
@end

// ============================================================================
// FRAGMENT SHADER
// ============================================================================

@fs fs_pbr
precision highp float;

// Constants
@include fs_constants.glsl

// Inputs from vertex shader
in vec3 v_Position;
in vec2 v_TexCoord0;
in vec3 v_Normal;

// Fragment output
out vec4 frag_color;

// Material uniforms
@include fs_uniforms.glsl

// Camera position
layout(binding=4) uniform camera_params {
    vec3 u_Camera;
};

// IBL uniforms
layout(binding=5) uniform ibl_params {
    float u_EnvIntensity;
    float u_EnvBlurNormalized;
    int u_MipCount;
    mat4 u_EnvRotation;
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

layout(binding=8) uniform textureCube u_CharlieEnvTexture;
layout(binding=8) uniform sampler u_CharlieEnvSampler_Raw;

layout(binding=9) uniform texture2D u_CharlieLUTTexture;
layout(binding=9) uniform sampler u_CharlieLUTSampler_Raw;

// Create combined samplers for IBL functions
#define u_GGXEnvSampler samplerCube(u_GGXEnvTexture, u_GGXEnvSampler_Raw)
#define u_LambertianEnvSampler samplerCube(u_LambertianEnvTexture, u_LambertianEnvSampler_Raw)
#define u_GGXLUT sampler2D(u_GGXLUTTexture, u_GGXLUTSampler_Raw)
#define u_CharlieEnvSampler samplerCube(u_CharlieEnvTexture, u_CharlieEnvSampler_Raw)
#define u_CharlieLUT sampler2D(u_CharlieLUTTexture, u_CharlieLUTSampler_Raw)

// Utility functions (must be defined before includes that use them)
float clampedDot(vec3 x, vec3 y)
{
    return clamp(dot(x, y), 0.0, 1.0);
}

float applyIorToRoughness(float roughness, float ior)
{
    // Scale roughness with IOR so that an IOR of 1.0 results in no microfacet refraction
    return roughness * clamp(ior * 2.0 - 2.0, 0.0, 1.0);
}

// Include core utilities and functions (after uniforms/samplers/utilities are defined)
@include brdf.glsl
@include ibl.glsl
@include tonemapping.glsl


// ============================================================================
// Normal mapping
// ============================================================================

vec3 getNormal() {
    vec3 n = normalize(v_Normal);
    
    #ifdef HAS_NORMAL_MAP
    if (has_normal_tex > 0.5) {
        // Sample normal map
        vec3 tangentNormal = texture(sampler2D(u_NormalTexture, u_NormalSampler), v_TexCoord0).xyz * 2.0 - 1.0;
        tangentNormal.xy *= normal_map_scale;
        
        // Derive tangent space from position and UV derivatives
        vec3 pos_dx = dFdx(v_Position);
        vec3 pos_dy = dFdy(v_Position);
        vec2 tex_dx = dFdx(v_TexCoord0);
        vec2 tex_dy = dFdy(v_TexCoord0);
        
        vec3 t = (tex_dy.y * pos_dx - tex_dx.y * pos_dy) / (tex_dx.x * tex_dy.y - tex_dy.x * tex_dx.y);
        t = normalize(t - n * dot(n, t));
        vec3 b = normalize(cross(n, t));
        mat3 tbn = mat3(t, b, n);
        
        n = normalize(tbn * tangentNormal);
    }
    #endif
    
    return n;
}


// ============================================================================
// Material property fetching
// ============================================================================

vec4 getBaseColor() {
    vec4 baseColor = base_color_factor;
    
    if (has_base_color_tex > 0.5) {
        baseColor *= texture(sampler2D(u_BaseColorTexture, u_BaseColorSampler), v_TexCoord0);
    }
    
    return baseColor;
}

vec2 getMetallicRoughness() {
    float metallic = metallic_factor;
    float roughness = roughness_factor;
    
    if (has_metallic_roughness_tex > 0.5) {
        vec4 mrSample = texture(sampler2D(u_MetallicRoughnessTexture, u_MetallicRoughnessSampler), v_TexCoord0);
        // glTF spec: metallic is B channel, roughness is G channel
        metallic *= mrSample.b;
        roughness *= mrSample.g;
    }
    
    return vec2(metallic, roughness);
}

float getOcclusion() {
    float occlusion = 1.0;
    
    if (has_occlusion_tex > 0.5) {
        occlusion = texture(sampler2D(u_OcclusionTexture, u_OcclusionSampler), v_TexCoord0).r;
    }
    
    return occlusion;
}

vec3 getEmissive() {
    vec3 emissive = emissive_factor;
    
    if (has_emissive_tex > 0.5) {
        emissive *= texture(sampler2D(u_EmissiveTexture, u_EmissiveSampler), v_TexCoord0).rgb;
    }
    
    emissive *= emissive_strength;
    
    return emissive;
}


// ============================================================================
// Main PBR shading
// ============================================================================

void main() {
    // Get material properties
    vec4 baseColor = getBaseColor();
    vec2 metallicRoughness = getMetallicRoughness();
    float metallic = clamp(metallicRoughness.x, 0.0, 1.0);
    float perceptualRoughness = clamp(metallicRoughness.y, 0.0, 1.0);
    float alphaRoughness = perceptualRoughness * perceptualRoughness;
    
    // Alpha test
    #ifdef ALPHAMODE_MASK
    if (baseColor.a < alpha_cutoff) {
        discard;
    }
    #endif
    
    // Get normal
    vec3 n = getNormal();
    
    // View vector
    vec3 v = normalize(u_Camera - v_Position);
    float NdotV = clampedDot(n, v);
    
    // Calculate F0 (reflectance at normal incidence)
    vec3 f0 = vec3(0.04); // Dielectric F0
    vec3 diffuseColor = baseColor.rgb * (1.0 - metallic);
    vec3 specularColor = mix(f0, baseColor.rgb, metallic);
    
    // Reflectivity
    float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);
    vec3 f90 = vec3(clamp(reflectance * 50.0, 0.0, 1.0));
    
    
    // ========================================================================
    // Image-Based Lighting (IBL)
    // ========================================================================
    
    vec3 color = vec3(0.0);
    
    #ifdef USE_IBL
    // Diffuse contribution
    vec3 irradiance = getDiffuseLight(n);
    vec3 diffuse = irradiance * diffuseColor;
    
    // Specular contribution
    vec3 specularLight = getIBLRadianceGGX(n, v, perceptualRoughness);
    vec3 iblFresnel = getIBLGGXFresnel(n, v, perceptualRoughness, specularColor, 1.0);
    vec3 specular = specularLight * iblFresnel;
    
    // Combine diffuse and specular
    color = diffuse + specular;
    #endif
    
    
    // ========================================================================
    // Punctual Lights (optional)
    // ========================================================================
    
    #ifdef USE_PUNCTUAL_LIGHTS
    for (int i = 0; i < num_lights; i++) {
        vec3 lightPos = light_positions[i].xyz;
        vec3 lightDir = light_directions[i].xyz;
        vec3 lightColor = light_colors[i].rgb;
        float lightIntensity = light_colors[i].w;
        int lightType = int(light_positions[i].w);
        
        vec3 l; // Light direction
        float attenuation = 1.0;
        
        // Directional light
        if (lightType == 0) {
            l = normalize(-lightDir);
        }
        // Point light
        else if (lightType == 1) {
            vec3 pointToLight = lightPos - v_Position;
            l = normalize(pointToLight);
            float distance = length(pointToLight);
            float range = light_params_data[i].x;
            attenuation = max(0.0, 1.0 - (distance / range));
            attenuation *= attenuation;
        }
        // Spot light
        else if (lightType == 2) {
            vec3 pointToLight = lightPos - v_Position;
            l = normalize(pointToLight);
            float distance = length(pointToLight);
            float range = light_params_data[i].x;
            attenuation = max(0.0, 1.0 - (distance / range));
            attenuation *= attenuation;
            
            // Spot cone
            float angle = dot(l, normalize(-lightDir));
            float innerCutoff = light_directions[i].w;
            float outerCutoff = light_params_data[i].y;
            float epsilon = innerCutoff - outerCutoff;
            float spotIntensity = clamp((angle - outerCutoff) / epsilon, 0.0, 1.0);
            attenuation *= spotIntensity;
        }
        
        vec3 h = normalize(l + v);
        float NdotL = clampedDot(n, l);
        float NdotH = clampedDot(n, h);
        float VdotH = clampedDot(v, h);
        
        if (NdotL > 0.0 || NdotV > 0.0) {
            // Calculate BRDF
            vec3 F = F_Schlick(specularColor, f90, VdotH);
            float Vis = V_GGX(NdotL, NdotV, alphaRoughness);
            float D = D_GGX(NdotH, alphaRoughness);
            
            vec3 diffuseContrib = (1.0 - F) * diffuseColor / M_PI;
            vec3 specContrib = F * Vis * D;
            
            color += NdotL * lightColor * lightIntensity * attenuation * (diffuseContrib + specContrib);
        }
    }
    #endif
    
    
    // ========================================================================
    // Ambient occlusion
    // ========================================================================
    
    #ifdef HAS_OCCLUSION_MAP
    float ao = getOcclusion();
    color = mix(color, color * ao, 1.0); // Apply AO
    #endif
    
    
    // ========================================================================
    // Emissive
    // ========================================================================
    
    color += getEmissive();
    
    
    // ========================================================================
    // Tone mapping and output
    // ========================================================================
    
    #ifdef USE_TONEMAPPING
    color = toneMap(color);
    #endif
    
    // Gamma correction (if not in LINEAR_OUTPUT mode)
    #ifndef LINEAR_OUTPUT
    color = pow(color, vec3(1.0/2.2));
    #endif
    
    frag_color = vec4(color, baseColor.a);
}
@end


// ============================================================================
// Program definition
// ============================================================================

@program pbr_program vs_pbr fs_pbr
