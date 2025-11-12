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
    - Debug views for material properties
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
layout(location=6) in vec4 joints_0;    // Bone indices for skinning
layout(location=7) in vec4 weights_0;   // Bone weights for skinning

// Uniforms (PBR-specific, separate from cgltf-sapp.glsl)
@include pbr_vs_uniforms.glsl

// Animation texture samplers (use high bindings to avoid FS conflicts)
// Note: u_morphWeights is defined in vs_params (vs_uniforms.glsl)
// Note: Morph targets share slot 9 with CharlieLUT - they're unlikely to be used together
// (morphing is for character animation, Charlie sheen is for fabric materials)
layout(binding=11) uniform texture2D u_jointsSampler_Tex;
layout(binding=11) uniform sampler u_jointsSampler_Smp;

layout(binding=9) uniform texture2DArray u_MorphTargetsSampler_Tex;
layout(binding=9) uniform sampler u_MorphTargetsSampler_Smp;

// Animation support (uses uniforms defined above)
@include animation.glsl

// Outputs
out vec3 v_Position;
out vec3 v_Normal;
out vec4 v_Tangent;
out vec2 v_TexCoord0;
out vec2 v_TexCoord1;
out vec4 v_Color;
out mat3 v_TBN;  // Tangent-Bitangent-Normal matrix for normal mapping

void main() {
    // Apply morph targets to position
    vec3 morphedPosition = position;
#ifdef MORPHING
    morphedPosition += getTargetPosition(gl_VertexIndex, 8);
#endif
    
    // Apply morph targets to normal
    vec3 morphedNormal = normal;
#ifdef MORPHING
    morphedNormal += getTargetNormal(gl_VertexIndex, 8, 8);  // Assuming normal offset = 8
#endif
    
    // Apply morph targets to tangent
    vec3 morphedTangent = tangent.xyz;
#ifdef MORPHING
    morphedTangent += getTargetTangent(gl_VertexIndex, 8, 16);  // Assuming tangent offset = 16
#endif
    
    // Apply skinning to position
    vec4 skinnedPosition = vec4(morphedPosition, 1.0);
#ifdef SKINNING
    mat4 skinMatrix = getSkinningMatrix(joints_0, weights_0);
    skinnedPosition = skinMatrix * vec4(morphedPosition, 1.0);
#endif
    
    // Transform to world space
    vec4 pos = model * skinnedPosition;
    v_Position = vec3(pos.xyz) / pos.w;
    
    // Texture coordinates (no morphing support for UVs yet - add if needed)
    v_TexCoord0 = texcoord_0;
    v_TexCoord1 = texcoord_1;
    
    // Vertex color
    v_Color = color_0;
    
    // Apply skinning to normal and tangent
    vec3 skinnedNormal = morphedNormal;
    vec3 skinnedTangent = morphedTangent;
    
#ifdef SKINNING
    mat4 skinNormalMatrix = getSkinningNormalMatrix(joints_0, weights_0);
    skinnedNormal = mat3(skinNormalMatrix) * morphedNormal;
    skinnedTangent = mat3(skinNormalMatrix) * morphedTangent;
#endif
    
    // Transform normal to world space
    mat3 normalMatrix = transpose(inverse(mat3(model)));
    vec3 normalW = normalize(normalMatrix * skinnedNormal);
    v_Normal = normalW;
    
    // Transform tangent to world space and build TBN matrix
    vec3 tangentW = normalize(vec3(model * vec4(skinnedTangent, 0.0)));
    
    // Gram-Schmidt re-orthogonalization to ensure tangent is perpendicular to normal
    tangentW = normalize(tangentW - dot(tangentW, normalW) * normalW);
    
    // Calculate bitangent using cross product with handedness
    // glTF spec: bitangent = cross(normal, tangent) * tangent.w
    vec3 bitangentW = cross(normalW, tangentW) * tangent.w;
    
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

// ============================================================================
// TRANSMISSION DEBUG TOGGLES - Enable/disable parts of transmission code
// ============================================================================
// Set to 0 to disable specific features, 1 to enable
// NOTE: The transmission framebuffer currently contains only a gray clear color,
// not the actual checkerboard background. See Init.cs:707 for details on fixing this.
#define ENABLE_TRANSMISSION_MIX 1        // Mix transmission with surface color
#define ENABLE_TRANSMISSION_ALPHA 1      // Modulate alpha by transmission factor
#define ENABLE_BEER_LAW_ATTENUATION 1    // Apply Beer's law volume attenuation
#define ENABLE_BASE_COLOR_TINT 1         // Tint transmission by base color

// Debug view mode constants
#define DEBUG_NONE 0
#define DEBUG_UV_0 1
#define DEBUG_UV_1 2
#define DEBUG_NORMAL_TEXTURE 3
#define DEBUG_NORMAL_SHADING 4
#define DEBUG_NORMAL_GEOMETRY 5
#define DEBUG_TANGENT 6
#define DEBUG_BITANGENT 7
#define DEBUG_TANGENT_W 8
#define DEBUG_ALPHA 9
#define DEBUG_OCCLUSION 10
#define DEBUG_EMISSIVE 11
#define DEBUG_METALLIC 12
#define DEBUG_ROUGHNESS 13
#define DEBUG_BASE_COLOR 14
#define DEBUG_CLEARCOAT_FACTOR 15
#define DEBUG_CLEARCOAT_ROUGHNESS 16
#define DEBUG_CLEARCOAT_NORMAL 17
#define DEBUG_SHEEN_COLOR 18
#define DEBUG_SHEEN_ROUGHNESS 19
#define DEBUG_SPECULAR_FACTOR 20
#define DEBUG_TRANSMISSION_FACTOR 21
#define DEBUG_VOLUME_THICKNESS 22
#define DEBUG_IOR 23
#define DEBUG_F0 24
#define DEBUG_ATTENUATION_DISTANCE 25
#define DEBUG_ATTENUATION_COLOR 26
#define DEBUG_TRANSMISSION_RESULT 27
#define DEBUG_REFRACTION_FRAMEBUFFER 28
#define DEBUG_REFRACTION_COORDS 29
#define DEBUG_FINAL_ALPHA 30
#define DEBUG_BEER_LAW_ATTENUATION 31
#define DEBUG_TRANSMISSION_BEFORE_TINT 32
#define DEBUG_BASE_COLOR_FOR_TINT 33
#define DEBUG_SURFACE_COLOR_BEFORE_MIX 34

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

// Material uniforms (PBR-specific, separate from cgltf-sapp.glsl)
@include pbr_fs_uniforms.glsl

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
#endif

#ifdef TRANSMISSION
// Transmission framebuffer (for refraction/transparency)
layout(binding=10) uniform texture2D u_TransmissionFramebufferTexture;
layout(binding=10) uniform sampler u_TransmissionFramebufferSampler_Raw;
#endif

// Create combined samplers for IBL functions
#define u_GGXEnvSampler samplerCube(u_GGXEnvTexture, u_GGXEnvSampler_Raw)
#define u_LambertianEnvSampler samplerCube(u_LambertianEnvTexture, u_LambertianEnvSampler_Raw)
#define u_GGXLUT sampler2D(u_GGXLUTTexture, u_GGXLUTSampler_Raw)
#ifndef MORPHING
#define u_CharlieEnvSampler samplerCube(u_CharlieEnvTexture, u_CharlieEnvSampler_Raw)
#define u_CharlieLUT sampler2D(u_CharlieLUTTexture, u_CharlieLUTSampler_Raw)
#endif
#ifdef TRANSMISSION
#define u_TransmissionFramebufferSampler sampler2D(u_TransmissionFramebufferTexture, u_TransmissionFramebufferSampler_Raw)
#endif

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

// Apply texture transform (KHR_texture_transform)
// Transforms UV coordinates by scale, rotation, and offset
vec2 applyTextureTransform(vec2 uv, vec2 offset, float rotation, vec2 scale)
{
    // Scale
    vec2 transformed = uv * scale;
    
    // Rotation around (0.5, 0.5) pivot
    if (rotation != 0.0)
    {
        transformed -= vec2(0.5);
        float c = cos(rotation);
        float s = sin(rotation);
        mat2 rotMat = mat2(c, s, -s, c);
        transformed = rotMat * transformed;
        transformed += vec2(0.5);
    }
    
    // Offset
    transformed += offset;
    
    return transformed;
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
        // Select UV channel based on normal_texcoord (0 = TEXCOORD_0, 1 = TEXCOORD_1)
        vec2 baseUV = (normal_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        // Apply texture transform
        vec2 uv = applyTextureTransform(baseUV, normal_tex_offset, normal_tex_rotation, normal_tex_scale);
        
        // Sample normal map (tangent space)
        vec3 tangentNormal = texture(sampler2D(u_NormalTexture, u_NormalSampler), uv).xyz * 2.0 - 1.0;
        
        // Apply normal map scale (only to XY, not Z)
        tangentNormal.xy *= normal_map_scale;
        
        // Normalize the tangent-space normal after scaling
        tangentNormal = normalize(tangentNormal);
        
        // Transform from tangent space to world space using TBN matrix
        // The TBN matrix is already properly constructed in the vertex shader
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
        // Select UV channel based on base_color_texcoord (0 = TEXCOORD_0, 1 = TEXCOORD_1)
        vec2 baseUV = (base_color_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        vec2 uv = applyTextureTransform(baseUV, base_color_tex_offset, base_color_tex_rotation, base_color_tex_scale);
        baseColor *= texture(sampler2D(u_BaseColorTexture, u_BaseColorSampler), uv);
    }
    
    return baseColor;
}

vec2 getMetallicRoughness() {
    float metallic = metallic_factor;
    float roughness = roughness_factor;
    
    if (has_metallic_roughness_tex > 0.5) {
        // Select UV channel based on metallic_roughness_texcoord (0 = TEXCOORD_0, 1 = TEXCOORD_1)
        vec2 baseUV = (metallic_roughness_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        vec2 uv = applyTextureTransform(baseUV, metallic_roughness_tex_offset, metallic_roughness_tex_rotation, metallic_roughness_tex_scale);
        vec4 mrSample = texture(sampler2D(u_MetallicRoughnessTexture, u_MetallicRoughnessSampler), uv);
        // glTF spec: metallic is B channel, roughness is G channel
        metallic *= mrSample.b;
        roughness *= mrSample.g;
    }
    
    return vec2(metallic, roughness);
}

float getOcclusion() {
    float occlusion = 1.0;
    
    if (has_occlusion_tex > 0.5) {
        // Select UV channel based on occlusion_texcoord (0 = TEXCOORD_0, 1 = TEXCOORD_1)
        vec2 baseUV = (occlusion_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        vec2 uv = applyTextureTransform(baseUV, occlusion_tex_offset, occlusion_tex_rotation, occlusion_tex_scale);
        occlusion = texture(sampler2D(u_OcclusionTexture, u_OcclusionSampler), uv).r;
    }
    
    return occlusion;
}

vec3 getEmissive() {
    vec3 emissive = emissive_factor;
    
    if (has_emissive_tex > 0.5) {
        // Select UV channel based on emissive_texcoord (0 = TEXCOORD_0, 1 = TEXCOORD_1)
        vec2 baseUV = (emissive_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        vec2 uv = applyTextureTransform(baseUV, emissive_tex_offset, emissive_tex_rotation, emissive_tex_scale);
        emissive *= texture(sampler2D(u_EmissiveTexture, u_EmissiveSampler), uv).rgb;
    }
    
    emissive *= emissive_strength;
    
    return emissive;
}

#ifdef TRANSMISSION
// Global variables to store transmission intermediate values for debugging
vec2 g_refractionCoords = vec2(0.0);
vec3 g_transmittedLightRaw = vec3(0.0);
vec3 g_transmissionResult = vec3(0.0);
vec3 g_beerLawAttenuation = vec3(1.0);
vec3 g_transmissionBeforeTint = vec3(0.0);
vec3 g_baseColorForTint = vec3(1.0);
vec3 g_surfaceColorBeforeMix = vec3(0.0);

// Get transmission/refraction contribution for IBL
vec3 getTransmissionIBL(vec3 n, vec3 v, vec3 baseColor) {
    // 1. Extract normalized rotation from model matrix (upper 3x3)
    mat3 modelRot = mat3(normalize(u_ModelMatrix[0].xyz),
                        normalize(u_ModelMatrix[1].xyz),
                        normalize(u_ModelMatrix[2].xyz));
    
    // 2. Create model-compensated view rotation: ViewRot * inverse(ModelRot)
    mat3 modelCompensatedViewRot = mat3(u_ViewMatrix) * transpose(modelRot);
    
    // 3. Transform position to regular view space
    vec4 viewPos = u_ViewMatrix * vec4(v_Position, 1.0);
    vec3 positionView = viewPos.xyz / viewPos.w;
    
    // 4. Transform normal using model-compensated view rotation
    vec3 normalView = normalize(modelCompensatedViewRot * n);
    vec3 viewDirView = normalize(-positionView);
    
    // 5. Calculate refracted ray direction using Snell's law
    vec3 refractedRayView = refract(-viewDirView, normalView, 1.0 / ior);
    
    // 6. Extract model scale
    vec3 modelScale;
    modelScale.x = length(vec3(u_ModelMatrix[0].xyz));
    modelScale.y = length(vec3(u_ModelMatrix[1].xyz));
    modelScale.z = length(vec3(u_ModelMatrix[2].xyz));
    
    // 7. Calculate transmission ray with scaled thickness
    vec3 transmissionRayView = normalize(refractedRayView) * thickness_factor * modelScale;
    
    // 8. Calculate exit point in view space
    vec3 refractedRayExitView = positionView + transmissionRayView;
    
    // 9. Project exit point to screen space
    vec4 ndcPos = u_ProjectionMatrix * vec4(refractedRayExitView, 1.0);
    vec2 refractionCoords = ndcPos.xy / ndcPos.w;
    refractionCoords = refractionCoords * 0.5 + 0.5;
    
    #if !SOKOL_GLSL
    refractionCoords.y = 1.0 - refractionCoords.y;
    #endif
    
    // Store for debug views
    g_refractionCoords = refractionCoords;
    
    // 10. Sample the transmission framebuffer
    vec3 transmittedLight = texture(u_TransmissionFramebufferSampler, refractionCoords).rgb;
    
    // Store raw sampled value for debug
    g_transmittedLightRaw = transmittedLight;
    
    // 11. Apply Beer's law volume attenuation
    #if ENABLE_BEER_LAW_ATTENUATION
    if (attenuation_distance > 0.0) {
        float transmissionRayLength = length(transmissionRayView);
        vec3 attenuationCoefficient = -log(attenuation_color) / attenuation_distance;
        vec3 attenuation = exp(-attenuationCoefficient * transmissionRayLength);
        transmittedLight *= attenuation;
        g_beerLawAttenuation = attenuation;  // Store for debug
    }
    #endif
    
    // Store after Beer's law but before tinting
    g_transmissionBeforeTint = transmittedLight;
    g_baseColorForTint = baseColor;
    
    // 12. Modulate by base color
    #if ENABLE_BASE_COLOR_TINT
    vec3 result = transmittedLight * baseColor;
    #else
    vec3 result = transmittedLight;
    #endif
    
    // Store final transmission result (before mixing with surface)
    g_transmissionResult = result;
    
    return result;
}
#endif


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
    
#ifdef TRANSMISSION
    // For transmission materials, F0 should be calculated from IOR
    // F0 = ((ior - 1) / (ior + 1))^2
    // This gives proper Fresnel reflection strength for glass/transparent materials
    float f0_ior = pow((ior - 1.0) / (ior + 1.0), 2.0);
    f0 = vec3(f0_ior);
#endif
    
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
        
        // ====================================================================
        // WORKAROUND: Selective diffuse IBL for dielectrics vs metals
        // ====================================================================
        // ROOT CAUSE:
        //   Environment cubemap mipmaps use box-filter downsampling instead of
        //   GGX pre-filtering (see EnvironmentMapLoader.cs). This causes
        //   getDiffuseLight() to return overly sharp environment samples that
        //   create reflection-like artifacts on rough dielectric surfaces.
        //
        // SOLUTION:
        //   - Dielectrics (metallic < 0.3): Use material albedo (diffuseColor)
        //     instead of environment-sampled diffuse. They still get specular IBL.
        //   - Metals (metallic ≥ 0.5): Use full environment lighting (diffuse + specular)
        //
        // TRADE-OFF:
        //   Dielectrics lose environment-based diffuse lighting but retain
        //   specular reflections. This is more physically plausible than
        //   disabling IBL entirely, though not fully correct.
        //
        // PROPER FIX (TODO):
        //   Implement GGX convolution in mipmap generation so getDiffuseLight()
        //   returns properly pre-filtered irradiance without sharp artifacts.
        //   See EnvironmentMapLoader.cs CreateMipmappedCubemapFromFaces().
        // ====================================================================
        
        if (metallic < 0.3) {
           color = mix(diffuseColor, baseColor.rgb, metallic)* ambient_strength;
        }
        else {
            // Metals: full IBL (environment diffuse + specular)
            color = diffuse + specular;
        }
    }
    else {
        // When IBL is disabled, add basic ambient contribution
        // For dielectrics: use diffuse color
        // For metals: use base color (since metals absorb diffuse and reflect specular)
        color = mix(diffuseColor, baseColor.rgb, metallic) * ambient_strength;
    }
    
#ifdef TRANSMISSION
    // ========================================================================
    // Transmission (Glass/Refraction) - applies whether IBL is on or off
    // ========================================================================
    // Store surface color before transmission mixing (for debug)
    g_surfaceColorBeforeMix = color;
    
    // Calculate transmission refraction
    vec3 f_specular_transmission = getTransmissionIBL(n, v, baseColor.rgb);
    
    // Mix current color with transmission (transmission_factor controls blend)
    // When transmission_factor = 0.0: fully opaque (use color)
    // When transmission_factor = 1.0: fully transparent (use refraction)
    #if ENABLE_TRANSMISSION_MIX
    color = mix(color, f_specular_transmission, transmission_factor);
    #else
    // DEBUG: Show pure transmission without mixing with surface
    color = f_specular_transmission;
    #endif
#endif
    
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
                // Calculate the Fresnel term
                vec3 F = F_Schlick(specularColor, f90, VdotH);
                
                // Calculate the visibility term (G / (4 * NdotL * NdotV))
                float Vis = V_GGX(NdotL, NdotV, alphaRoughness);
                
                // Calculate the microfacet distribution term
                float D = D_GGX(NdotH, alphaRoughness);
                
                // Specular BRDF = F * Vis * D
                vec3 f_specular = F * Vis * D;
                
                // Diffuse BRDF = (1 - F) * diffuseColor / π
                // The (1 - F) term accounts for energy conservation
                vec3 f_diffuse = (vec3(1.0) - F) * (diffuseColor / M_PI);
                
                // Combined lighting contribution
                vec3 lightContribution = NdotL * attenuation * lightIntensity * lightColor * (f_diffuse + f_specular);
                
                color += lightContribution;
            }
        }
    }
    
    
    // ========================================================================
    // Ambient occlusion
    // ========================================================================
    
    float ao = getOcclusion();
    // Apply occlusion with strength parameter: color * (1 + strength * (ao - 1))
    // When strength = 0, result = color (no effect)
    // When strength = 1, result = color * ao (full occlusion effect)
    color = color * (1.0 + occlusion_strength * (ao - 1.0));
    
    
    // ========================================================================
    // Emissive
    // ========================================================================
    
    color += getEmissive();
    
    // ========================================================================
    // Calculate final alpha (before debug views so it can be displayed)
    // ========================================================================
    float finalAlpha = baseColor.a;
#ifdef TRANSMISSION
    #if ENABLE_TRANSMISSION_ALPHA
    finalAlpha *= (1.0 - transmission_factor);
    #endif
#endif
    
    // ========================================================================
    // Debug Views (bypass tone mapping/gamma for raw material visualization)
    // ========================================================================
    
    if (debug_view_enabled > 0.5) {
        // Convert float to int for robust comparison (avoids floating-point precision issues)
        int mode = int(debug_view_mode + 0.5);
        
        // UV coordinates
        if (mode == DEBUG_UV_0) {
            color = vec3(v_TexCoord0, 0.0);
        }
        else if (mode == DEBUG_UV_1) {
            color = vec3(v_TexCoord1, 0.0);
        }
        // Normals
        else if (mode == DEBUG_NORMAL_TEXTURE) {
            vec3 normalTex = texture(sampler2D(u_NormalTexture, u_NormalSampler), v_TexCoord0).rgb * 2.0 - 1.0;
            color = (normalTex + 1.0) / 2.0;
        }
        else if (mode == DEBUG_NORMAL_SHADING) {
            color = (n + 1.0) / 2.0;
        }
        else if (mode == DEBUG_NORMAL_GEOMETRY) {
            vec3 ng = normalize(v_Normal);
            color = (ng + 1.0) / 2.0;
        }
        else if (mode == DEBUG_TANGENT) {
            vec3 t = normalize(v_Tangent.xyz);
            color = (t + 1.0) / 2.0;
        }
        else if (mode == DEBUG_BITANGENT) {
            // Recalculate bitangent from normal and tangent like reference viewer does
            vec3 ng = normalize(v_Normal);
            vec3 t = normalize(v_Tangent.xyz);
            vec3 b = -cross(ng, t) * v_Tangent.w;
            color = (b + 1.0) / 2.0;
        }
        else if (mode == DEBUG_TANGENT_W) {
            // Tangent W is either +1 or -1, map to 0-1 range
            // Match glTF Sample Viewer: +1 (right-handed) = black, -1 (left-handed) = white
            float w = v_Tangent.w;
            color = vec3((1.0 - w) * 0.5);
        }
        // Material properties
        else if (mode == DEBUG_ALPHA) {
            color = vec3(baseColor.a);
        }
        else if (mode == DEBUG_OCCLUSION) {
            color = vec3(ao);
        }
        else if (mode == DEBUG_EMISSIVE) {
            // Show raw emissive texture/factor without strength multiplier
            vec3 emissive = emissive_factor;
            if (has_emissive_tex > 0.5) {
                emissive *= texture(sampler2D(u_EmissiveTexture, u_EmissiveSampler), v_TexCoord0).rgb;
            }
            color = emissive;
        }
        else if (mode == DEBUG_METALLIC) {
            color = vec3(metallic);
        }
        else if (mode == DEBUG_ROUGHNESS) {
            color = vec3(perceptualRoughness);
        }
        else if (mode == DEBUG_BASE_COLOR) {
            color = baseColor.rgb;
        }
        // Clearcoat
        else if (mode == DEBUG_CLEARCOAT_FACTOR) {
            color = vec3(clearcoat_factor);
        }
        else if (mode == DEBUG_CLEARCOAT_ROUGHNESS) {
            color = vec3(clearcoat_roughness);
        }
#ifdef TRANSMISSION
        // Transmission
        else if (mode == DEBUG_TRANSMISSION_FACTOR) {
            color = vec3(transmission_factor);
        }
        else if (mode == DEBUG_VOLUME_THICKNESS) {
            // Show raw thickness value normalized for visibility
            // Typical range: 0.1 to 2.0 (with 1.0 being common)
            // Clamp to reasonable range and normalize to 0-1
            color = vec3(clamp(thickness_factor / 2.0, 0.0, 1.0));
        }
        else if (mode == DEBUG_IOR) {
            // Normalize IOR to 0-1 range (1.0-2.5 -> 0.0-1.0)
            color = vec3((ior - 1.0) / 1.5);
        }
        else if (mode == DEBUG_ATTENUATION_DISTANCE) {
            // Show attenuation distance (typically 0.1 to 10.0)
            // Normalize to 0-1 range for better visibility
            color = vec3(clamp(attenuation_distance / 5.0, 0.0, 1.0));
        }
        else if (mode == DEBUG_ATTENUATION_COLOR) {
            // Show the attenuation color directly (RGB absorption tint)
            color = attenuation_color;
        }
        else if (mode == DEBUG_TRANSMISSION_RESULT) {
            // Show the transmission result BEFORE mixing with surface color
            // This is what getTransmissionIBL() returns
            color = g_transmissionResult;
        }
        else if (mode == DEBUG_REFRACTION_FRAMEBUFFER) {
            // Show raw framebuffer sample (what the camera sees through the glass)
            color = g_transmittedLightRaw;
        }
        else if (mode == DEBUG_REFRACTION_COORDS) {
            // Show refraction texture coordinates as RG color
            // Red = X coordinate, Green = Y coordinate
            color = vec3(g_refractionCoords, 0.0);
        }
        else if (mode == DEBUG_FINAL_ALPHA) {
            // Show final alpha value (grayscale)
            color = vec3(finalAlpha);
        }
        else if (mode == DEBUG_BEER_LAW_ATTENUATION) {
            // Show Beer's law attenuation factor (how much light survives volume absorption)
            // White = no attenuation, darker = more absorption
            color = g_beerLawAttenuation;
        }
        else if (mode == DEBUG_TRANSMISSION_BEFORE_TINT) {
            // Show transmission after Beer's law but before base color tinting
            color = g_transmissionBeforeTint;
        }
        else if (mode == DEBUG_BASE_COLOR_FOR_TINT) {
            // Show the base color used for tinting transmission
            color = g_baseColorForTint;
        }
        else if (mode == DEBUG_SURFACE_COLOR_BEFORE_MIX) {
            // Show the surface color before mixing with transmission
            // This is what would be rendered for an opaque material
            color = g_surfaceColorBeforeMix;
        }
#endif // TRANSMISSION
        else if (mode == DEBUG_F0) {
            // Show the F0 reflectance value
            vec3 f0 = mix(vec3(0.04), baseColor.rgb, metallic);
            color = f0;
        }
    }
    else 
    {
        // Normal rendering path
        // Match reference: only apply toneMap when enabled
        // toneMap already includes linearTosRGB (gamma correction) at the end
        if (use_tonemapping > 0) {
            color = toneMap(color);
        }
    }
    
    // finalAlpha already calculated before debug views
    
    frag_color = vec4(color, finalAlpha);
}
@end


// ============================================================================
// Program definition
// ============================================================================

@program pbr_program vs_pbr fs_pbr
