// Lighting functions for the Framework PBR shader.
// Uses the same packed GpuLight[32] format as blinn_phong.glsl so LightBuffer.cs
// can fill it unchanged.
//
// GpuLight packed layout (4 vec4s, indices i*4+0 … i*4+3):
//   [i*4+0] PositionType   : xyz=worldPos  w=type (0=dir,1=point,2=spot)
//   [i*4+1] DirectionRange : xyz=dir       w=range
//   [i*4+2] ColorIntensity : xyz=color     w=intensity
//   [i*4+3] SpotShadow     : x=innerCos  y=outerCos  z=shadowIdx  w=pad

float get_range_attenuation(float range, float distance)
{
    if (range < 0.0) return 1.0;
    return max(min(1.0 - pow(distance / range, 4.0), 1.0), 0.0) / max(distance * distance, 1e-4);
}

// Returns vec4(lightDir.xyz, attenuation).
vec4 getLightDirectionAndAttenuation(int i, vec3 frag_pos)
{
    vec4  pt        = LIGHT_DATA(i, 0);   // the uniform block, or Forward+'s texture (pbr_fs_uniforms.glsl)
    vec4  dr        = LIGHT_DATA(i, 1);
    vec4  ss        = LIGHT_DATA(i, 3);
    vec3  pos       = pt.xyz;
    float ltype     = pt.w;
    vec3  dir       = dr.xyz;
    float range     = dr.w;
    float innerCos  = ss.x;
    float outerCos  = ss.y;

    vec3  l = vec3(0.0);
    float att = 1.0;

    int ltype_i = int(ltype + 0.5);
    if (ltype_i == LIGHT_TYPE_DIRECTIONAL) {
        l = normalize(-dir);
    } else if (ltype_i == LIGHT_TYPE_POINT) {
        vec3 toLight = pos - frag_pos;
        float dist = length(toLight);
        l   = toLight / max(dist, 1e-6);
        att = get_range_attenuation(range, dist);
    } else if (ltype_i == LIGHT_TYPE_SPOT) {
        vec3 toLight = pos - frag_pos;
        float dist = length(toLight);
        l   = toLight / max(dist, 1e-6);
        att = get_range_attenuation(range, dist);
        float theta   = dot(-l, normalize(dir));
        float epsilon = innerCos - outerCos;
        float cone    = clamp((theta - outerCos) / max(epsilon, 1e-4), 0.0, 1.0);
        att *= cone;
    }
    return vec4(l, att);
}

// ─── CSM shadow sampling ──────────────────────────────────────────────────────
#if CSM_CASCADES > 0

// Returns a 0…1 shadow term for the directional cascade that covers view_depth.
// ndotl = saturated dot(surfaceNormal, lightDir); drives the slope-scaled bias.
float sampleCsmShadow(vec3 worldPos, float view_depth, float ndotl)
{
    // Choose cascade.
    int cascade = CSM_CASCADES - 1;
    for (int c = 0; c < CSM_CASCADES; c++) {
        if (view_depth < csm_split_depths[c]) { cascade = c; break; }
    }

    // Project into shadow clip space using the VS-passed cascade VP.
    // The VS already computed v_ShadowPos[c] for all cascades.
    // We re-project here from the array output of the VS.
    vec4 shadowCoord;
#if defined(CSM4)
    if      (cascade == 0) shadowCoord = v_ShadowPos[0];
    else if (cascade == 1) shadowCoord = v_ShadowPos[1];
    else if (cascade == 2) shadowCoord = v_ShadowPos[2];
    else                   shadowCoord = v_ShadowPos[3];
#elif defined(CSM2)
    if      (cascade == 0) shadowCoord = v_ShadowPos[0];
    else                   shadowCoord = v_ShadowPos[1];
#else
    shadowCoord = v_ShadowPos[0];
#endif

    vec3 ndc = shadowCoord.xyz / max(shadowCoord.w, 1e-5);
#if !SOKOL_GLSL
    ndc.y = -ndc.y;
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 ||
        ndc.z <  0.0 || ndc.z >  1.0) return 1.0;
#else
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 ||
        ndc.z < -1.0 || ndc.z >  1.0) return 1.0;
#endif

    vec2 uv = ndc.xy * 0.5 + 0.5;
#if SOKOL_GLSL
    float receiver = ndc.z * 0.5 + 0.5;
#else
    float receiver = ndc.z;
#endif
    // Depth bias — now SMALL because the normal-offset bias (pbr.glsl VS, CSM_NORMAL_OFFSET)
    // carries most of the anti-acne work spatially. A large depth bias caused peter-panning
    // (a close occluder's shadow dropping off the receiver), so the slope term is kept light;
    // csm_bias is the near-perpendicular floor. If acne returns, raise CSM_NORMAL_OFFSET first.
    float slope    = clamp(1.0 - ndotl, 0.0, 1.0);
    float bias     = max(csm_bias, 0.0025 * slope);
    float slice    = float(cascade);

    vec2 texel = 1.0 / vec2(textureSize(sampler2DArray(shadow_atlas, shadow_atlas_smp), 0).xy);

    if (csm_pcf_taps >= 25) {
        float vis = 0.0;
        for (int y = -2; y <= 2; y++)
        for (int x = -2; x <= 2; x++) {
            vec2 s = uv + vec2(float(x), float(y)) * texel * 1.5;
            vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                           vec4(s, slice, receiver - bias));
        }
        return vis / 25.0;
    } else if (csm_pcf_taps >= 9) {
        float vis = 0.0;
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++) {
            vec2 s = uv + vec2(float(x), float(y)) * texel;
            vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                           vec4(s, slice, receiver - bias));
        }
        return vis / 9.0;
    } else {
        return texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                       vec4(uv, slice, receiver - bias));
    }
}
#endif  // CSM_CASCADES > 0

// ─── Spot-light shadow sampling ───────────────────────────────────────────────
// Projects worldPos by the spot cone's atlas-slice VP and 3×3-PCF samples the shared
// shadow atlas (slices 4-11). Returns 1.0 (lit) for slices outside the spot range, so
// spot lights without a shadow map contribute no occlusion.
#if !defined(TRANSMISSION)
// Normal-offset world units for punctual shadows (same idea as CSM_NORMAL_OFFSET): push the
// receiver off its surface so it doesn't self-shadow, which lets the depth bias stay small and
// avoids the acne rings a close point/spot light produces. Tune if acne/leak reappears.
#define PUNCTUAL_NORMAL_OFFSET 0.05
float sample_spot_shadow(float atlas_slice, vec3 worldPos, vec3 nrm)
{
    int local = int(atlas_slice) - 4;
    if (local < 0 || local >= SPOT_SHADOW_VEC4S / 4) return 1.0;

    int b = local * 4;
    mat4 spot_vp = mat4(
        spot_shadow_vp[b + 0], spot_shadow_vp[b + 1],
        spot_shadow_vp[b + 2], spot_shadow_vp[b + 3]);

    vec4 clip = spot_vp * vec4(worldPos + nrm * PUNCTUAL_NORMAL_OFFSET, 1.0);
    vec3 ndc  = clip.xyz / max(clip.w, 1e-5);
#if !SOKOL_GLSL
    ndc.y = -ndc.y;
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < 0.0 || ndc.z > 1.0) return 1.0;
    float receiver = ndc.z;
#else
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < -1.0 || ndc.z > 1.0) return 1.0;
    float receiver = ndc.z * 0.5 + 0.5;
#endif

    vec2  uv    = ndc.xy * 0.5 + 0.5;
    float bias  = 0.0006;
    vec2  texel = 1.0 / vec2(textureSize(sampler2DArray(shadow_atlas, shadow_atlas_smp), 0).xy);

    float vis = 0.0;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++) {
        vec2 suv = uv + vec2(float(x), float(y)) * texel * 2.5;
        vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                       vec4(suv, atlas_slice, receiver - bias));
    }
    return vis / 9.0;
}

// ─── Point-light shadow sampling ──────────────────────────────────────────────
// Picks the cube face from the light→fragment direction, projects by that face's VP,
// and 3×3-PCF samples cube_shadow_array (slice = slot*6 + face). 1.0 (lit) for invalid
// slots, so point lights without a shadow map contribute no occlusion.
// In SKINNING variants cube_shadow_array lives at binding 8 (joints occupies 11); see
// pbr_fs_uniforms.glsl. Sampling code below is identical regardless of binding.
float sample_point_shadow(int point_slot, vec3 light_pos, vec3 worldPos, vec3 nrm)
{
    if (point_slot < 0 || point_slot >= POINT_SHADOW_VEC4S / 24) return 1.0;

    // The depth is read at a normal-offset position (avoids the self-shadow acne rings of a close point
    // light), and the face is chosen from that same position: chosen from the true one, an offset point
    // near a face's edge fell outside that face and came out lit — a thin lit line along the cube map's
    // seams across a shadow (2026-11-14).
    vec3 p = worldPos + nrm * PUNCTUAL_NORMAL_OFFSET;
    vec3 to_frag = p - light_pos;
    vec3 a = abs(to_frag);
    int face;
    if (a.x >= a.y && a.x >= a.z)      face = to_frag.x > 0.0 ? 0 : 1;
    else if (a.y >= a.x && a.y >= a.z) face = to_frag.y > 0.0 ? 2 : 3;
    else                               face = to_frag.z > 0.0 ? 4 : 5;

    int li = point_slot * 24 + face * 4;
    mat4 face_vp = mat4(
        point_shadow_vp[li + 0], point_shadow_vp[li + 1],
        point_shadow_vp[li + 2], point_shadow_vp[li + 3]);

    vec4 clip = face_vp * vec4(p, 1.0);
    vec3 ndc  = clip.xyz / max(clip.w, 1e-5);
#if !SOKOL_GLSL
    ndc.y = -ndc.y;
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < 0.0 || ndc.z > 1.0) return 1.0;
    float receiver = ndc.z;
#else
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < -1.0 || ndc.z > 1.0) return 1.0;
    float receiver = ndc.z * 0.5 + 0.5;
#endif

    vec2  uv    = ndc.xy * 0.5 + 0.5;
    float bias  = 0.0006;
    float slice = float(point_slot * 6 + face);
    vec2  texel = 1.0 / vec2(textureSize(sampler2DArray(cube_shadow_array, cube_shadow_array_smp), 0).xy);

    float vis2 = 0.0;
    for (int y = -1; y <= 1; y++)
    for (int x = -1; x <= 1; x++) {
        vec2 suv = uv + vec2(float(x), float(y)) * texel;
        vis2 += texture(sampler2DArrayShadow(cube_shadow_array, cube_shadow_array_smp),
                        vec4(suv, slice, receiver - bias));
    }
    return vis2 / 9.0;
}
#endif  // !TRANSMISSION
