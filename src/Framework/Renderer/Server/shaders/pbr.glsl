/*
    Framework PBR shader — adapted from CGltfViewer/shaders/pbr.glsl.

    Key differences from CGltfViewer:
      - `model` matrix comes from per-instance vertex attributes (loc 4-7),
        not from a per-draw uniform.  Same pattern as blinn_phong.glsl.
      - `joints_0` / `weights_0` are at vertex attribute locations 9-10
        (avoiding conflict with the instance-buffer attributes at 4-8).
      - Light loop uses the packed GpuLight[32] block (lights_data[128]).
      - The TonemapPass post-process handles tone-mapping; this shader
        writes linear HDR color.  set linear_output=1 from C#.
      - CSM shadow sampling is behind #if CSM_CASCADES > 0.

    Vertex buffer layout:
      Buffer 0 (per-vertex, ObjVertex / PbrVertex, 48 B):
        loc 0  position    vec3
        loc 1  normal      vec3
        loc 2  texcoord_0  vec2
        loc 3  tangent     vec4  (xyz tangent, w handedness)
      Buffer 1 (per-instance, InstanceData, 80 B):
        loc 4  in_model_0  vec4  — row 0 of model matrix
        loc 5  in_model_1  vec4  — row 1
        loc 6  in_model_2  vec4  — row 2
        loc 7  in_model_3  vec4  — row 3
        loc 8  in_custom   vec4  — rgb=tint, a=lod_fade
      Skinned variants add to buffer 0 (PbrSkinnedVertex, 80 B):
        loc 9  joints_0    vec4
        loc 10 weights_0   vec4
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

// Per-vertex (buffer 0)
layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;
layout(location=2) in vec2 texcoord_0;
layout(location=3) in vec4 tangent;       // xyz tangent, w = handedness (+1/-1)
// loc 9 (not 11): the STATIC pipeline uses locations 0-8, so texcoord_1 must sit at 9 to keep the
// attribute list gap-free (sokol requires contiguous attrs). Skinned joints/weights move to 10/11.
layout(location=9) in vec2 texcoord_1;    // 2nd UV set (e.g. glTF AO maps); (0,0) when the mesh has none

// Per-instance (buffer 1)
layout(location=4) in vec4 in_model_0;
layout(location=5) in vec4 in_model_1;
layout(location=6) in vec4 in_model_2;
layout(location=7) in vec4 in_model_3;
layout(location=8) in vec4 in_custom;    // rgb = tint, a = lod_fade

// Joints/weights — added only for skinned variants (locs 10/11, after texcoord_1 at 9)
#ifdef SKINNING
layout(location=10) in vec4 joints_0;
layout(location=11) in vec4 weights_0;
#endif

// VS uniforms — this include also defines CSM_CASCADES (before the out declarations below).
@include pbr_vs_uniforms.glsl

// Animation helpers
@include animation.glsl

// Outputs (declared after pbr_vs_uniforms so CSM_CASCADES is already defined)
out vec3 v_Position;
out vec3 v_Normal;
out vec4 v_Tangent;
out vec2 v_TexCoord0;
out vec2 v_TexCoord1;   // Always passed as (0,0) in the static path
out vec4 v_Color;
out mat3 v_TBN;
out vec4 v_Custom;      // tint.rgb, lod_fade

#if CSM_CASCADES > 0
out vec4 v_ShadowPos[CSM_CASCADES];
#endif

void main()
{
    // Build model matrix from per-instance attributes.
    mat4 model = mat4(in_model_0, in_model_1, in_model_2, in_model_3);

    // ── Morph targets ──────────────────────────────────────────────────────
    vec3 morphedPosition = position;
    vec3 morphedNormal   = normal;
    vec3 morphedTangent  = tangent.xyz;
#ifdef MORPHING
    morphedPosition += getTargetPosition(gl_VertexIndex, 8);
    morphedNormal   += getTargetNormal  (gl_VertexIndex, 8, 8);
    morphedTangent  += getTargetTangent (gl_VertexIndex, 8, 16);
#endif

    // ── Skinning ──────────────────────────────────────────────────────────
    vec4 skinnedPosition = vec4(morphedPosition, 1.0);
    vec3 skinnedNormal   = morphedNormal;
    vec3 skinnedTangent  = morphedTangent;
#ifdef SKINNING
    mat4 skinMat   = getSkinningMatrix      (joints_0, weights_0);
    mat4 skinNorMat= getSkinningNormalMatrix(joints_0, weights_0);
    skinnedPosition = skinMat    * vec4(morphedPosition, 1.0);
    skinnedNormal   = mat3(skinNorMat) * morphedNormal;
    skinnedTangent  = mat3(skinNorMat) * morphedTangent;
#endif

    // ── World-space transform ──────────────────────────────────────────────
    vec4 worldPos = model * skinnedPosition;
    v_Position    = worldPos.xyz / worldPos.w;

    mat3 nm = transpose(inverse(mat3(model)));
    vec3 normalW  = normalize(nm * skinnedNormal);
    vec3 tangentW = normalize(vec3(model * vec4(skinnedTangent, 0.0)));
    // Gram-Schmidt re-orthogonalisation
    tangentW = normalize(tangentW - dot(tangentW, normalW) * normalW);
    vec3 bitangentW = cross(normalW, tangentW) * tangent.w;

    v_Normal  = normalW;
    v_Tangent = vec4(tangentW, tangent.w);
    v_TBN     = mat3(tangentW, bitangentW, normalW);

    v_TexCoord0 = texcoord_0;
    v_TexCoord1 = texcoord_1;  // 2nd UV set (e.g. AO); the vertex carries (0,0) if the mesh has none
    v_Color     = vec4(1.0);   // no vertex color in base layout
    v_Custom    = in_custom;

    // ── CSM shadow projection ──────────────────────────────────────────────
    // Normal-offset bias: push the shadow-space position a few texels along the surface normal
    // so the receiver doesn't self-shadow. This carries most of the anti-acne bias spatially, so
    // the depth bias (fs_lighting.glsl) can stay small — which avoids the peter-panning where a
    // close occluder's shadow drops off the receiver. Constant world offset; tune if acne or
    // peter-panning reappears (too small → acne, too large → light leak near contacts).
#if CSM_CASCADES > 0
    const float CSM_NORMAL_OFFSET = 0.04;
    vec4 shadowWorldPos = vec4(v_Position + normalW * CSM_NORMAL_OFFSET, 1.0);
#endif
#if defined(CSM4)
    v_ShadowPos[0] = csm_vp[0] * shadowWorldPos;
    v_ShadowPos[1] = csm_vp[1] * shadowWorldPos;
    v_ShadowPos[2] = csm_vp[2] * shadowWorldPos;
    v_ShadowPos[3] = csm_vp[3] * shadowWorldPos;
#elif defined(CSM2)
    v_ShadowPos[0] = csm_vp[0] * shadowWorldPos;
    v_ShadowPos[1] = csm_vp[1] * shadowWorldPos;
#elif defined(CSM1)
    v_ShadowPos[0] = csm_vp[0] * shadowWorldPos;
#endif

    gl_Position = view_proj * worldPos;
}
@end

// ============================================================================
// FRAGMENT SHADER
// ============================================================================

@fs fs_pbr
precision highp float;

// ── Transmission debug toggles ────────────────────────────────────────────────
#define ENABLE_TRANSMISSION_MIX       1
#define ENABLE_TRANSMISSION_ALPHA     1
#define ENABLE_BEER_LAW_ATTENUATION   1
#define ENABLE_BASE_COLOR_TINT        1

// ── Debug-view constants ──────────────────────────────────────────────────────
#define DEBUG_NONE                  0
#define DEBUG_UV_0                  1
#define DEBUG_UV_1                  2
#define DEBUG_NORMAL_TEXTURE        3
#define DEBUG_NORMAL_SHADING        4
#define DEBUG_NORMAL_GEOMETRY       5
#define DEBUG_TANGENT               6
#define DEBUG_BITANGENT             7
#define DEBUG_TANGENT_W             8
#define DEBUG_ALPHA                 9
#define DEBUG_OCCLUSION             10
#define DEBUG_EMISSIVE              11
#define DEBUG_METALLIC              12
#define DEBUG_ROUGHNESS             13
#define DEBUG_BASE_COLOR            14
#define DEBUG_CLEARCOAT_FACTOR      15
#define DEBUG_CLEARCOAT_ROUGHNESS   16
#define DEBUG_CLEARCOAT_NORMAL      17
#define DEBUG_SHEEN_COLOR           18
#define DEBUG_SHEEN_ROUGHNESS       19
#define DEBUG_SPECULAR_FACTOR       20
#define DEBUG_TRANSMISSION_FACTOR   21
#define DEBUG_VOLUME_THICKNESS      22
#define DEBUG_IOR                   23
#define DEBUG_F0                    24
#define DEBUG_ATTENUATION_DISTANCE  25
#define DEBUG_ATTENUATION_COLOR     26
#define DEBUG_TRANSMISSION_RESULT   27
#define DEBUG_REFRACTION_FRAMEBUFFER 28
#define DEBUG_REFRACTION_COORDS     29
#define DEBUG_FINAL_ALPHA           30
#define DEBUG_BEER_LAW_ATTENUATION  31
#define DEBUG_TRANSMISSION_BEFORE_TINT 32
#define DEBUG_BASE_COLOR_FOR_TINT   33
#define DEBUG_SURFACE_COLOR_BEFORE_MIX 34
#define DEBUG_CSM_CASCADE           35
#define DEBUG_SHADOW                36

// ── Constants ─────────────────────────────────────────────────────────────────
@include fs_constants.glsl

// ── Inputs from VS ────────────────────────────────────────────────────────────
in vec3 v_Position;
in vec3 v_Normal;
in vec4 v_Tangent;
in vec2 v_TexCoord0;
in vec2 v_TexCoord1;
in vec4 v_Color;
in mat3 v_TBN;
in vec4 v_Custom;

#if CSM_CASCADES > 0
in vec4 v_ShadowPos[CSM_CASCADES];
#endif

out vec4 frag_color;

// ── Uniforms & samplers ───────────────────────────────────────────────────────
@include pbr_fs_uniforms.glsl

// ── Utility helpers ───────────────────────────────────────────────────────────
float clampedDot(vec3 x, vec3 y) { return clamp(dot(x,y), 0.0, 1.0); }

float applyIorToRoughness(float roughness, float ior) {
    return roughness * clamp(ior * 2.0 - 2.0, 0.0, 1.0);
}

vec2 applyTextureTransform(vec2 uv, vec2 offset, float rotation, vec2 scale) {
    vec2 t = uv * scale;
    if (rotation != 0.0) {
        t -= 0.5;
        float c = cos(rotation), s = sin(rotation);
        t = mat2(c, s, -s, c) * t;
        t += 0.5;
    }
    return t + offset;
}

// ── Core includes ─────────────────────────────────────────────────────────────
@include brdf.glsl
@include ibl.glsl
// NOTE: No @include tonemapping.glsl — Framework routes PBR HDR output to a
// separate TonemapPass.  This shader always writes linear HDR color.
@include fs_lighting.glsl

// ── Aliases (pbr_material_params → names expected by helper functions below) ──
#define base_color_factor              base_color_factor
#define emissive_factor                emissive_factor
#define metallic_factor                metallic_factor
#define roughness_factor               roughness_factor
#define has_base_color_tex             has_base_color_tex
#define has_metallic_roughness_tex     has_metallic_roughness_tex
#define has_normal_tex                 has_normal_tex
#define has_occlusion_tex              has_occlusion_tex
#define has_emissive_tex               has_emissive_tex
#define alpha_cutoff                   alpha_cutoff
#define emissive_strength              emissive_strength
#define occlusion_strength             occlusion_strength
#define transmission_factor            transmission_factor
#define has_transmission_tex           has_transmission_tex
#define transmission_texcoord          transmission_texcoord
#define ior                            ior
#define attenuation_color              attenuation_color
#define attenuation_distance           attenuation_distance
#define thickness_factor               thickness_factor
#define has_thickness_tex              has_thickness_tex
#define thickness_texcoord             thickness_texcoord
#define thickness_tex_index            thickness_tex_index
#define clearcoat_factor               clearcoat_factor
#define clearcoat_roughness            clearcoat_roughness
#define base_color_tex_offset          base_color_tex_offset
#define base_color_tex_rotation        base_color_tex_rotation
#define base_color_tex_scale           base_color_tex_scale
#define metallic_roughness_tex_offset  metallic_roughness_tex_offset
#define metallic_roughness_tex_rotation metallic_roughness_tex_rotation
#define metallic_roughness_tex_scale   metallic_roughness_tex_scale
#define normal_tex_offset              normal_tex_offset
#define normal_tex_rotation            normal_tex_rotation
#define normal_tex_scale               normal_tex_scale
#define occlusion_tex_offset           occlusion_tex_offset
#define occlusion_tex_rotation         occlusion_tex_rotation
#define occlusion_tex_scale            occlusion_tex_scale
#define emissive_tex_offset            emissive_tex_offset
#define emissive_tex_rotation          emissive_tex_rotation
#define emissive_tex_scale             emissive_tex_scale
#define thickness_tex_offset           thickness_tex_offset
#define thickness_tex_rotation         thickness_tex_rotation
#define thickness_tex_scale            thickness_tex_scale
#define normal_map_scale               normal_map_scale
#define base_color_texcoord            base_color_texcoord
#define metallic_roughness_texcoord    metallic_roughness_texcoord
#define normal_texcoord                normal_texcoord
#define occlusion_texcoord             occlusion_texcoord
#define emissive_texcoord              emissive_texcoord
#define debug_view_enabled             debug_view_enabled
#define debug_view_mode                debug_view_mode
#define shadow_ambient_weight          shadow_ambient_weight

// Convenience aliases for the light block.
#ifdef LOW_TIER
#define num_lights  min(int(ambient_num.w + 0.5), LIGHT_VEC4S / 4)
#else
#define num_lights  int(ambient_num.w + 0.5)
#endif

// ── Normal mapping ────────────────────────────────────────────────────────────
vec3 getNormal() {
    vec3 n = normalize(v_Normal);
    if (has_normal_tex > 0.5) {
        vec2 baseUV = (normal_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        vec2 uv = applyTextureTransform(baseUV, normal_tex_offset, normal_tex_rotation, normal_tex_scale);
        vec3 ts = texture(sampler2D(u_NormalTexture, u_NormalSampler), uv).xyz * 2.0 - 1.0;
        float tileScale = max(normal_tex_scale.x, normal_tex_scale.y);
        ts.xy *= normal_map_scale / max(1.0, tileScale);
        n = normalize(v_TBN * normalize(ts));
    }
    return n;
}

// ── Material property helpers ─────────────────────────────────────────────────
vec4 getBaseColor() {
    vec4 c = base_color_factor;
    if (has_base_color_tex > 0.5) {
        vec2 uv = applyTextureTransform(
            (base_color_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1,
            base_color_tex_offset, base_color_tex_rotation, base_color_tex_scale);
        c *= texture(sampler2D(u_BaseColorTexture, u_BaseColorSampler), uv);
    }
    return c;
}

vec2 getMetallicRoughness() {
    float m = metallic_factor, r = roughness_factor;
    if (has_metallic_roughness_tex > 0.5) {
        vec2 uv = applyTextureTransform(
            (metallic_roughness_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1,
            metallic_roughness_tex_offset, metallic_roughness_tex_rotation, metallic_roughness_tex_scale);
        vec4 s = texture(sampler2D(u_MetallicRoughnessTexture, u_MetallicRoughnessSampler), uv);
        m *= s.b; r *= s.g;
    }
    return vec2(m, r);
}

float getOcclusion() {
    if (has_occlusion_tex > 0.5) {
        vec2 uv = applyTextureTransform(
            (occlusion_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1,
            occlusion_tex_offset, occlusion_tex_rotation, occlusion_tex_scale);
        return texture(sampler2D(u_OcclusionTexture, u_OcclusionSampler), uv).r;
    }
    return 1.0;
}

vec3 getEmissive() {
    vec3 e = emissive_factor;
    if (has_emissive_tex > 0.5) {
        vec2 uv = applyTextureTransform(
            (emissive_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1,
            emissive_tex_offset, emissive_tex_rotation, emissive_tex_scale);
        e *= texture(sampler2D(u_EmissiveTexture, u_EmissiveSampler), uv).rgb;
    }
    return e * emissive_strength;
}

#ifdef TRANSMISSION
float getThickness() {
    float t = thickness_factor;
    if (has_thickness_tex > 0.5) {
        vec2 uv = applyTextureTransform(
            (thickness_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1,
            thickness_tex_offset, thickness_tex_rotation, thickness_tex_scale);
        int idx = int(thickness_tex_index + 0.5);
        float v = 0.0;
        if      (idx == 0) v = texture(sampler2D(u_BaseColorTexture,          u_BaseColorSampler),          uv).g;
        else if (idx == 1) v = texture(sampler2D(u_MetallicRoughnessTexture,  u_MetallicRoughnessSampler),  uv).g;
        else if (idx == 2) v = texture(sampler2D(u_NormalTexture,             u_NormalSampler),             uv).g;
        else if (idx == 3) v = texture(sampler2D(u_OcclusionTexture,          u_OcclusionSampler),          uv).g;
        else if (idx == 4) v = texture(sampler2D(u_EmissiveTexture,           u_EmissiveSampler),           uv).g;
        t *= v;
    }
    return t;
}

// debug globals for transmission
vec2 g_refractionCoords       = vec2(0.0);
vec3 g_transmittedLightRaw    = vec3(0.0);
vec3 g_transmissionResult     = vec3(0.0);
vec3 g_beerLawAttenuation     = vec3(1.0);
vec3 g_transmissionBeforeTint = vec3(0.0);
vec3 g_baseColorForTint       = vec3(1.0);
vec3 g_surfaceColorBeforeMix  = vec3(0.0);

vec3 getTransmissionIBL(vec3 n, vec3 v_dir_in, vec3 baseColor) {
    vec3 pos            = v_Position;
    vec3 viewDir        = normalize(u_Camera - pos);
    vec3 refractionVec  = refract(-viewDir, normalize(n), 1.0 / ior);
    vec3 modelScale     = vec3(length(u_ModelMatrix[0].xyz),
                               length(u_ModelMatrix[1].xyz),
                               length(u_ModelMatrix[2].xyz));
    float thickness     = getThickness();
    vec3  transmRay     = normalize(refractionVec) * thickness * modelScale;
    vec3  exitPos       = pos + transmRay;
    vec4  ndcPos        = u_ProjectionMatrix * u_ViewMatrix * vec4(exitPos, 1.0);
    vec2  coords        = ndcPos.xy / ndcPos.w * 0.5 + 0.5;
#if !SOKOL_GLSL
    coords.y = 1.0 - coords.y;
#endif
    g_refractionCoords = coords;
    vec3 transmLight = texture(u_TransmissionFramebufferSampler, coords).rgb;
    g_transmittedLightRaw = transmLight;
#if ENABLE_BEER_LAW_ATTENUATION
    if (attenuation_distance > 0.0) {
        float d = length(transmRay);
        vec3  att = exp(-(-log(attenuation_color) / attenuation_distance) * d);
        transmLight *= att;
        g_beerLawAttenuation = att;
    }
#endif
    g_transmissionBeforeTint = transmLight;
    g_baseColorForTint = baseColor;
#if ENABLE_BASE_COLOR_TINT
    vec3 result = transmLight * baseColor;
#else
    vec3 result = transmLight;
#endif
    g_transmissionResult = result;
    return result;
}
#endif  // TRANSMISSION

// ─── Main ─────────────────────────────────────────────────────────────────────
void main()
{
    vec4  baseColor          = getBaseColor();
    vec2  mr                 = getMetallicRoughness();
    float metallic           = clamp(mr.x, 0.0, 1.0);
    float perceptualRoughness= clamp(mr.y, 0.0, 1.0);
    float alphaRoughness     = perceptualRoughness * perceptualRoughness;

    // Alpha-test / mask
    if (alphamode == 1 && baseColor.a < alpha_cutoff) discard;

    vec3  n   = getNormal();
    vec3  v   = normalize(u_Camera - v_Position);
    float NdotV = clampedDot(n, v);

    float transmission = 0.0;
    vec3  f0 = vec3(0.04);
#ifdef TRANSMISSION
    float f0_ior = pow((ior - 1.0) / (ior + 1.0), 2.0);
    f0 = vec3(f0_ior);
#endif

    vec3 diffuseColor  = baseColor.rgb * (1.0 - metallic);
    vec3 specularColor = mix(f0, baseColor.rgb, metallic);
    float reflectance  = max(max(specularColor.r, specularColor.g), specularColor.b);
    vec3 f90 = vec3(clamp(reflectance * 50.0, 0.0, 1.0));

    // ── IBL ─────────────────────────────────────────────────────────────────
    vec3 f_diffuse_ibl  = vec3(0.0);
    vec3 f_specular_ibl = vec3(0.0);

    if (use_ibl > 0) {
        vec3 irradiance    = getDiffuseLight(n);
        f_diffuse_ibl      = irradiance * diffuseColor;
        vec3 specLight     = getIBLRadianceGGX(n, v, perceptualRoughness);
        vec3 iblFresnel    = getIBLGGXFresnel(n, v, perceptualRoughness, specularColor, 1.0);
        f_specular_ibl     = specLight * iblFresnel;
    }

#ifdef TRANSMISSION
    transmission = transmission_factor;
    if (has_transmission_tex > 0.5) {
        vec2 uv = (transmission_texcoord < 0.5) ? v_TexCoord0 : v_TexCoord1;
        transmission *= texture(u_TransmissionSampler, uv).r;
    }
    vec3 f_specular_transmission = getTransmissionIBL(n, v, baseColor.rgb);
#if ENABLE_TRANSMISSION_MIX
    f_diffuse_ibl = mix(f_diffuse_ibl, f_specular_transmission, transmission);
#else
    f_diffuse_ibl = f_specular_transmission;
#endif
#endif  // TRANSMISSION

    vec3 color = vec3(0.0);
    float g_minShadow = 1.0;   // diagnostics: min shadow factor across all punctual lights

#ifdef TRANSMISSION
    g_surfaceColorBeforeMix = f_diffuse_ibl + f_specular_ibl;
#endif

    if (use_ibl > 0) {
        // The sky's light and reflection: the ambient light comes from the sky. (A dielectric had a flat
        // ambient_strength instead, the same on every face, whatever the sky.)
        color = f_diffuse_ibl + f_specular_ibl;
    } else {
#ifdef TRANSMISSION
        color = f_diffuse_ibl;
#else
        color = mix(diffuseColor, baseColor.rgb, metallic) * ambient_strength;
#endif
    }

    vec3 g_ambient = color;   // IBL/ambient term — captured for optional shadow modulation below.

    // ── Clearcoat setup ──────────────────────────────────────────────────────
    vec3  clearcoatNormal         = normalize(v_Normal);
    float clearcoatAlphaRoughness = clearcoat_roughness * clearcoat_roughness;
    vec3  clearcoatF0  = vec3(0.04);
    vec3  clearcoatF90 = vec3(1.0);
    vec3  clearcoat_punctual = vec3(0.0);

    // ── Punctual lights ──────────────────────────────────────────────────────
    if (use_punctual_lights > 0) {
        for (int i = 0; i < num_lights; i++) {
            vec3  lightColor    = lights_data[i*4+2].xyz;
            float lightIntensity= lights_data[i*4+2].w;

            vec4  lightInfo = getLightDirectionAndAttenuation(i, v_Position);
            vec3  l         = lightInfo.xyz;
            float att       = lightInfo.w;

            vec3  h     = normalize(l + v);
            float VdotH = clampedDot(v, h);

            // ── Shadow factor: directional via CSM, spot via the atlas cone slice ──
            float shadow = 1.0;
            int ltype_i = int(lights_data[i*4+0].w + 0.5);
#if CSM_CASCADES > 0
            if (ltype_i == LIGHT_TYPE_DIRECTIONAL) {
                // view_depth = -(u_ViewMatrix * vec4(v_Position,1)).z
                float vd = -(u_ViewMatrix * vec4(v_Position, 1.0)).z;
                shadow = sampleCsmShadow(v_Position, vd, clampedDot(n, l));
                // A surface turned away from the sun is in its own shadow. The map cannot say so
                // (the receiver lies on its caster: the lookup is noise there), and with
                // shadow_ambient_weight that noise showed in the ambient as jagged dark patches.
                g_minShadow = min(g_minShadow, smoothstep(0.0, 0.15, dot(n, l)));
            }
#endif
#if !defined(TRANSMISSION)
            if (ltype_i == LIGHT_TYPE_SPOT) {
                // lights_data[i*4+3].z = spot shadow atlas slice (4-11), or 0 = no shadow.
                shadow = sample_spot_shadow(lights_data[i*4+3].z, v_Position, n);
            }
            else if (ltype_i == LIGHT_TYPE_POINT) {
                // lights_data[i*4+3].z = point shadow slot (0-3); pos = lights_data[i*4+0].xyz.
                shadow = sample_point_shadow(int(lights_data[i*4+3].z + 0.5),
                                             lights_data[i*4+0].xyz, v_Position, n);
            }
#endif
            g_minShadow = min(g_minShadow, shadow);

            // ── Base material ─────────────────────────────────────────────
            float NdotL = clampedDot(n, l);
            float NdotH = clampedDot(n, h);
            if (NdotL > 0.0 || NdotV > 0.0) {
                vec3  F   = F_Schlick(specularColor, f90, VdotH);
                float Vis = V_GGX(NdotL, NdotV, alphaRoughness);
                float D   = D_GGX(NdotH, alphaRoughness);
                vec3 f_spec = F * Vis * D;
                vec3 f_diff = (vec3(1.0) - F) * (diffuseColor / M_PI);
                // × π: a light's intensity in the common engines' units — 1 lights a white face square on fully.
                color += NdotL * att * shadow * lightIntensity * M_PI * lightColor * (f_diff + f_spec);
            }

            // ── Clearcoat ─────────────────────────────────────────────────
            if (clearcoat_factor > 0.0) {
                float NdotL_cc = clampedDot(clearcoatNormal, l);
                float NdotH_cc = clampedDot(clearcoatNormal, h);
                float NdotV_cc = clampedDot(clearcoatNormal, v);
                if (NdotL_cc > 0.0 || NdotV_cc > 0.0) {
                    vec3  F_cc  = F_Schlick(clearcoatF0, clearcoatF90, VdotH);
                    float Vis_cc= V_GGX(NdotL_cc, NdotV_cc, clearcoatAlphaRoughness);
                    float D_cc  = D_GGX(NdotH_cc, clearcoatAlphaRoughness);
                    clearcoat_punctual += NdotL_cc * att * shadow * lightIntensity * M_PI * lightColor
                                        * (F_cc * Vis_cc * D_cc);
                }
            }
        }
    }

    // ── Optional: shadows darken the IBL ambient ──────────────────────────────
    // Physically, punctual shadows only occlude their own (1/d²-attenuated) contribution,
    // so on an IBL-lit floor a spot/point shadow is nearly invisible. When enabled, fold
    // the combined shadow term into the ambient so the shadow reads at any light intensity.
    // weight == 0 (default) → mix() returns 1.0 → exact no-op on every variant.
    color -= g_ambient * (1.0 - mix(1.0, g_minShadow, clamp(shadow_ambient_weight, 0.0, 1.0)));

    // ── Clearcoat layer ───────────────────────────────────────────────────────
    if (clearcoat_factor > 0.0) {
        vec3 ccFresnel = F_Schlick(clearcoatF0, clearcoatF90, clampedDot(clearcoatNormal, v));
        vec3 cc_brdf   = vec3(0.0);
        if (use_ibl > 0) cc_brdf = getIBLRadianceGGX(clearcoatNormal, v, clearcoat_roughness);
        cc_brdf += clearcoat_punctual;
        color = mix(color, cc_brdf, clearcoat_factor * ccFresnel);
    }

    // ── Occlusion ─────────────────────────────────────────────────────────────
    float ao = getOcclusion();
    color    = color * (1.0 + occlusion_strength * (ao - 1.0));

    // ── Emissive ──────────────────────────────────────────────────────────────
    color += getEmissive();

    // ── Final alpha ───────────────────────────────────────────────────────────
    float finalAlpha = baseColor.a;
#ifdef TRANSMISSION
    // Transmission surfaces are drawn OPAQUE (no blend) in RenderingServer's dedicated
    // transmission pass — the refracted background is already composited into RGB via the
    // screen-copy sample. So the output must be fully opaque (alpha = 1); the offscreen RT is
    // displayed alpha-blended by the editor, and a (1 - transmission) alpha would make a
    // transmissionFactor=1 surface vanish to the panel behind it (appearing black).
    // (This differs from CGltfViewer, which alpha-BLENDS transmission and needs 1 - transmission.)
    finalAlpha = 1.0;
#endif

    // ── Debug views ───────────────────────────────────────────────────────────
    if (debug_view_enabled > 0.5) {
        int mode = int(debug_view_mode + 0.5);
        if      (mode == DEBUG_UV_0)            color = vec3(v_TexCoord0, 0.0);
        else if (mode == DEBUG_UV_1)            color = vec3(v_TexCoord1, 0.0);
        else if (mode == DEBUG_NORMAL_SHADING)  color = (n + 1.0) * 0.5;
        else if (mode == DEBUG_NORMAL_GEOMETRY) color = (normalize(v_Normal) + 1.0) * 0.5;
        else if (mode == DEBUG_TANGENT)         color = (normalize(v_Tangent.xyz) + 1.0) * 0.5;
        else if (mode == DEBUG_TANGENT_W)       color = vec3((1.0 - v_Tangent.w) * 0.5);
        else if (mode == DEBUG_ALPHA)           color = vec3(baseColor.a);
        else if (mode == DEBUG_OCCLUSION)       color = vec3(ao);
        else if (mode == DEBUG_EMISSIVE)        color = getEmissive();
        else if (mode == DEBUG_METALLIC)        color = vec3(metallic);
        else if (mode == DEBUG_ROUGHNESS)       color = vec3(perceptualRoughness);
        else if (mode == DEBUG_BASE_COLOR)      color = baseColor.rgb;
        else if (mode == DEBUG_F0)              color = mix(vec3(0.04), baseColor.rgb, metallic);
        else if (mode == DEBUG_SHADOW)          color = vec3(g_minShadow); // white=lit, black=shadowed
#if CSM_CASCADES > 0
        else if (mode == DEBUG_CSM_CASCADE) {
            float vd = -(u_ViewMatrix * vec4(v_Position, 1.0)).z;
            int cascade = CSM_CASCADES - 1;
            for (int c = 0; c < CSM_CASCADES; c++) {
                if (vd < csm_split_depths[c]) { cascade = c; break; }
            }
            vec3 cols[4];
            cols[0] = vec3(1,0,0); cols[1] = vec3(0,1,0);
            cols[2] = vec3(0,0,1); cols[3] = vec3(1,1,0);
            color = cols[cascade];
        }
#endif
    }

    // ── In-shader tonemap + sRGB fallback ─────────────────────────────────────
    // When NOT writing to an HDR target (linear_output == 0), tonemap and sRGB-
    // encode here so the LDR (RGBA8) output displays correctly.  linear_output == 1
    // keeps the colour linear HDR for a downstream TonemapPass.  Debug views are
    // already display-ready, so they bypass this.
    if (linear_output == 0 && debug_view_enabled <= 0.5) {
        // No tone curve (the common engines' default): clamped, then linear → sRGB. ACES (Narkowicz, without its 0.6
        // exposure) lifted mid-grey 1.5× and washed every picture out (docs: ENVIRONMENT_DESIGN.md).
        color = clamp(color, 0.0, 1.0);
        color = pow(color, vec3(1.0 / 2.2)); // linear → sRGB
    }

    frag_color = vec4(color, finalAlpha);
}
@end

// ============================================================================
// Programs
// ============================================================================
@program pbr_program vs_pbr fs_pbr
