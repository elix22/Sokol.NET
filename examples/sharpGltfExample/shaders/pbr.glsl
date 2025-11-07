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

// Vertex attributes (matching glTF primitive layout)
layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;
layout(location=2) in vec4 tangent;     // w component is handedness (+1 or -1)
layout(location=3) in vec2 texcoord_0;
layout(location=4) in vec2 texcoord_1;
layout(location=5) in vec4 color_0;

// Uniforms
@include vs_uniforms.glsl

// Outputs
out vec3 v_Position;
out vec3 v_Normal;
out vec4 v_Tangent;
out vec2 v_TexCoord0;
out vec2 v_TexCoord1;
out vec4 v_Color;
out mat3 v_TBN;  // Tangent-Bitangent-Normal matrix for normal mapping

void main() {
    vec4 pos = model * vec4(position, 1.0);
    v_Position = vec3(pos.xyz) / pos.w;
    
    // Texture coordinates
    v_TexCoord0 = texcoord_0;
    v_TexCoord1 = texcoord_1;
    
    // Vertex color
    v_Color = color_0;
    
    // Transform normal to world space
    mat3 normalMatrix = transpose(inverse(mat3(model)));
    vec3 normalW = normalize(normalMatrix * normal);
    v_Normal = normalW;
    
    // Transform tangent to world space and build TBN matrix
    vec3 tangentW = normalize(vec3(model * vec4(tangent.xyz, 0.0)));
    vec3 bitangentW = cross(normalW, tangentW) * tangent.w;
    bitangentW = normalize(bitangentW);
    
    v_Tangent = vec4(tangentW, tangent.w);
    v_TBN = mat3(tangentW, bitangentW, normalW);
    
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
in vec3 v_Normal;
in vec4 v_Tangent;
in vec2 v_TexCoord0;
in vec2 v_TexCoord1;
in vec4 v_Color;
in mat3 v_TBN;

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
    ivec2 u_TransmissionFramebufferSize;
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

layout(binding=8) uniform textureCube u_CharlieEnvTexture;
layout(binding=8) uniform sampler u_CharlieEnvSampler_Raw;

layout(binding=9) uniform texture2D u_CharlieLUTTexture;
layout(binding=9) uniform sampler u_CharlieLUTSampler_Raw;

// Transmission framebuffer (for refraction/transparency)
layout(binding=10) uniform texture2D u_TransmissionFramebufferTexture;
layout(binding=10) uniform sampler u_TransmissionFramebufferSampler_Raw;

// Create combined samplers for IBL functions
#define u_GGXEnvSampler samplerCube(u_GGXEnvTexture, u_GGXEnvSampler_Raw)
#define u_LambertianEnvSampler samplerCube(u_LambertianEnvTexture, u_LambertianEnvSampler_Raw)
#define u_GGXLUT sampler2D(u_GGXLUTTexture, u_GGXLUTSampler_Raw)
#define u_CharlieEnvSampler samplerCube(u_CharlieEnvTexture, u_CharlieEnvSampler_Raw)
#define u_CharlieLUT sampler2D(u_CharlieLUTTexture, u_CharlieLUTSampler_Raw)
#define u_TransmissionFramebufferSampler sampler2D(u_TransmissionFramebufferTexture, u_TransmissionFramebufferSampler_Raw)

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
    
    if (has_normal_tex > 0.5) {
        // Sample normal map (tangent space)
        vec3 tangentNormal = texture(sampler2D(u_NormalTexture, u_NormalSampler), v_TexCoord0).xyz * 2.0 - 1.0;
        tangentNormal.xy *= normal_map_scale;
        
        // Transform from tangent space to world space using TBN matrix
        n = normalize(v_TBN * tangentNormal);
    }
    
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
    
    // Alpha test (alphamode: 0=opaque, 1=mask, 2=blend)
    if (alphamode == 1 && baseColor.a < alpha_cutoff) {
        discard;
    }
    
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
    
    if (use_ibl > 0) {
        // Diffuse contribution
        vec3 irradiance = getDiffuseLight(n);
        vec3 diffuse = irradiance * diffuseColor;
        
        // Specular contribution
        vec3 specularLight = getIBLRadianceGGX(n, v, perceptualRoughness);
        vec3 iblFresnel = getIBLGGXFresnel(n, v, perceptualRoughness, specularColor, 1.0);
        vec3 specular = specularLight * iblFresnel;
        
        // Combine diffuse and specular
        color = diffuse + specular;
    }
    
    
    // ========================================================================
    // Punctual Lights (optional)
    // ========================================================================
    
    if (use_punctual_lights > 0) {
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
    }
    
    
    // ========================================================================
    // Ambient occlusion
    // ========================================================================
    
    float ao = getOcclusion();
    color = mix(color, color * ao, 1.0); // Apply AO
    
    
    // ========================================================================
    // Emissive
    // ========================================================================
    
    color += getEmissive();
    
    
    // ========================================================================
    // Tone mapping and output
    // ========================================================================
    
    if (use_tonemapping > 0) {
        color = toneMap(color);
    }
    
    // Gamma correction (if not in linear output mode)
    if (linear_output == 0) {
        color = pow(color, vec3(1.0/2.2));
    }
    
    frag_color = vec4(color, baseColor.a);
}
@end


// ============================================================================
// Program definition
// ============================================================================

@program pbr_program vs_pbr fs_pbr
