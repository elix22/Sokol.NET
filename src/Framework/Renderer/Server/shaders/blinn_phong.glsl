// Blinn-Phong shader for RenderingServer — M1.
//
// Vertex buffer 0 (ObjVertex, 48 B, per-vertex):
//   loc 0  in_pos      vec3  (position)
//   loc 1  in_normal   vec3
//   loc 2  in_uv       vec2
//   loc 3  in_tangent  vec4  (xyz = tangent, w = bitangent sign)
//
// Vertex buffer 1 (InstanceData, 80 B, per-instance):
//   loc 4-7  in_model_{0..3}  vec4  (row-major mat4)
//   loc 8    in_custom        vec4  (tint.rgb, lod_fade)
//
// Uniform blocks:
//   binding 0 (VS): blinn_phong_vs_params { mat4 view_proj; vec4 camera_pos; }
//   binding 1 (FS): blinn_phong_fs_params { material + light data }
//
// Textures (FS):
//   binding 0: diffuse_map   + diffuse_smp
//   binding 1: specular_map  + specular_smp
//   binding 2: normal_map    + normal_smp
//   binding 3: opacity_map   + opacity_smp

@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4
@ctype vec3 System.Numerics.Vector3
@ctype vec2 System.Numerics.Vector2

// ─── Vertex Shader ────────────────────────────────────────────────────────────

@vs blinn_phong_vs

layout(binding=0) uniform blinn_phong_vs_params {
    mat4 view_proj;
    vec4 camera_pos;   // xyz = world-space camera position
    mat4 dir_shadow_vp;
};

// Per-vertex (buffer slot 0)
layout(location=0) in vec3 in_pos;
layout(location=1) in vec3 in_normal;
layout(location=2) in vec2 in_uv;
layout(location=3) in vec4 in_tangent;   // xyz = tangent direction, w = bitangent sign

// Per-instance (buffer slot 1)
layout(location=4) in vec4 in_model_0;
layout(location=5) in vec4 in_model_1;
layout(location=6) in vec4 in_model_2;
layout(location=7) in vec4 in_model_3;
layout(location=8) in vec4 in_custom;    // rgb = tint, a = lod_fade

out vec3 fs_world_pos;
out vec3 fs_world_normal;
out vec3 fs_world_tangent;
out vec3 fs_world_bitangent;
out vec2 fs_uv;
out vec4 fs_custom;
out vec4 fs_shadow_pos;

void main() {
    mat4 model = mat4(in_model_0, in_model_1, in_model_2, in_model_3);

    vec4 wpos    = model * vec4(in_pos, 1.0);
    gl_Position  = view_proj * wpos;

    // Normal matrix = transpose(inverse(mat3(model))).
    // For uniform-scale objects this equals mat3(model), but we compute it fully.
    mat3 nm = transpose(inverse(mat3(model)));

    fs_world_pos       = wpos.xyz;
    fs_world_normal    = normalize(nm * in_normal);
    vec3 wt            = normalize(nm * in_tangent.xyz);
    fs_world_tangent   = wt;
    fs_world_bitangent = normalize(cross(fs_world_normal, wt) * in_tangent.w);
    fs_uv              = in_uv;
    fs_custom          = in_custom;
    fs_shadow_pos      = dir_shadow_vp * wpos;
}
@end

// ─── Fragment Shader ──────────────────────────────────────────────────────────

@fs blinn_phong_fs

// Layout: material params + packed light array.
// GpuLight = 4 vec4s (64 B) → MAX_LIGHTS(32) * 4 = 128 vec4s.
layout(binding=1) uniform blinn_phong_fs_params {
    vec4 material_kd_alpha;   // xyz = Kd (diffuse colour), w = opacity
    vec4 material_ks_shin;    // xyz = Ks (specular colour), w = Ns (shininess)
    vec4 ambient_num;         // xyz = ambient colour, w = float(numLights)
    vec4 cam_pos_pad;         // xyz = camera world position (for spec), w = pad
    vec4 lights_data[128];    // 32 lights × 4 vec4s
    vec4 spot_shadow_vp[32];  // 8 spot matrices × 4 rows (slices 4..11)
    vec4 point_shadow_vp[96]; // 4 point lights × 6 faces × 4 rows
    vec4 ambient_sky[9];      // Environment sky ambient, per channel 3 vec4 (AmbientSH.Pack); zero unless source = Sky
};

// Textures + separate samplers (sokol-shdc's cross-compilation model).
layout(binding=0) uniform texture2D diffuse_map;
layout(binding=1) uniform texture2D specular_map;
layout(binding=2) uniform texture2D normal_map;
layout(binding=3) uniform texture2D opacity_map;
layout(binding=4) uniform texture2DArray shadow_atlas;
layout(binding=5) uniform texture2DArray cube_shadow_array;

layout(binding=0) uniform sampler diffuse_smp;
layout(binding=1) uniform sampler specular_smp;
layout(binding=2) uniform sampler normal_smp;
layout(binding=3) uniform sampler opacity_smp;
layout(binding=4) uniform samplerShadow shadow_atlas_smp;
layout(binding=5) uniform samplerShadow cube_shadow_array_smp;

in vec3 fs_world_pos;
in vec3 fs_world_normal;
in vec3 fs_world_tangent;
in vec3 fs_world_bitangent;
in vec2 fs_uv;
in vec4 fs_custom;
in vec4 fs_shadow_pos;

out vec4 frag_color;

// Unpack normal from tangent-space normal map (RGB → [-1,1]).
vec3 sample_normal(vec2 uv, vec3 N, vec3 T, vec3 B) {
    vec3 n_ts = texture(sampler2D(normal_map, normal_smp), uv).xyz * 2.0 - 1.0;
    // Transform from tangent space to world space.
    return normalize(T * n_ts.x + B * n_ts.y + N * n_ts.z);
}

float sample_dir_shadow(float atlas_slice, float bias)
{
    vec3 ndc = fs_shadow_pos.xyz / max(fs_shadow_pos.w, 0.00001);
#if !SOKOL_GLSL
    ndc.y = -ndc.y;
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < 0.0 || ndc.z > 1.0)
        return 1.0;
#else
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < -1.0 || ndc.z > 1.0)
        return 1.0;
#endif

    vec2 uv = ndc.xy * 0.5 + 0.5;
#if SOKOL_GLSL
    float receiver_depth = ndc.z * 0.5 + 0.5;
#else
    float receiver_depth = ndc.z;
#endif
    vec2 texel = 1.0 / vec2(textureSize(sampler2DArray(shadow_atlas, shadow_atlas_smp), 0).xy);

    // shadow_quality (cam_pos_pad.w): 0=1-tap sharp, 1=3x3 PCF medium, 2=5x5 PCF soft
    float shadow_q = cam_pos_pad.w;
    if (shadow_q >= 2.0) {
        float vis = 0.0;
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++) {
                vec2 suv = uv + vec2(float(x), float(y)) * texel * 1.5;
                vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                               vec4(suv, atlas_slice, receiver_depth - bias));
            }
        return vis / 25.0;
    } else if (shadow_q >= 1.0) {
        float vis = 0.0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++) {
                vec2 suv = uv + vec2(float(x), float(y)) * texel;
                vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                               vec4(suv, atlas_slice, receiver_depth - bias));
            }
        return vis / 9.0;
    } else {
        return texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                       vec4(uv, atlas_slice, receiver_depth - bias));
    }
}

float sample_spot_shadow(float atlas_slice, float bias)
{
    int local = int(atlas_slice) - 4;
    if (local < 0 || local >= 8) return 1.0;

    int b = local * 4;
    mat4 spot_vp = mat4(
        spot_shadow_vp[b + 0],
        spot_shadow_vp[b + 1],
        spot_shadow_vp[b + 2],
        spot_shadow_vp[b + 3]);

    vec4 clip = spot_vp * vec4(fs_world_pos, 1.0);
    vec3 ndc = clip.xyz / max(clip.w, 0.00001);
#if !SOKOL_GLSL
    ndc.y = -ndc.y;
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < 0.0 || ndc.z > 1.0)
        return 1.0;
#else
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < -1.0 || ndc.z > 1.0)
        return 1.0;
#endif

    vec2 uv = ndc.xy * 0.5 + 0.5;
#if SOKOL_GLSL
    float receiver_depth = ndc.z * 0.5 + 0.5;
#else
    float receiver_depth = ndc.z;
#endif
    vec2 texel = 1.0 / vec2(textureSize(sampler2DArray(shadow_atlas, shadow_atlas_smp), 0).xy);

    // shadow_quality (cam_pos_pad.w): 0=1-tap sharp, 1=3x3 PCF medium, 2=5x5 PCF soft
    // Spot atlas slices pack a tight cone into 2048px, so each texel covers far less
    // world space than a directional cascade — the directional kernel spacing produces
    // a sub-pixel (invisible) penumbra here. Widen it so the quality levels are visible.
    float shadow_q = cam_pos_pad.w;
    if (shadow_q >= 2.0) {
        float vis = 0.0;
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++) {
                vec2 suv = uv + vec2(float(x), float(y)) * texel * 4.0;
                vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                               vec4(suv, atlas_slice, receiver_depth - bias));
            }
        return vis / 25.0;
    } else if (shadow_q >= 1.0) {
        float vis = 0.0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++) {
                vec2 suv = uv + vec2(float(x), float(y)) * texel * 2.5;
                vis += texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                               vec4(suv, atlas_slice, receiver_depth - bias));
            }
        return vis / 9.0;
    } else {
        return texture(sampler2DArrayShadow(shadow_atlas, shadow_atlas_smp),
                       vec4(uv, atlas_slice, receiver_depth - bias));
    }
}

float sample_point_shadow(int point_slot, vec3 light_pos, float bias, vec3 receiver_pos)
{
    if (point_slot < 0 || point_slot >= 4) return 1.0;

    vec3 to_frag = receiver_pos - light_pos;
    vec3 at = abs(to_frag);
    int face = 0;
    if (at.x >= at.y && at.x >= at.z) {
        face = to_frag.x > 0.0 ? 0 : 1;
    } else if (at.y >= at.x && at.y >= at.z) {
        face = to_frag.y > 0.0 ? 2 : 3;
    } else {
        face = to_frag.z > 0.0 ? 4 : 5;
    }

    int local = point_slot * 24 + face * 4;
    mat4 face_vp = mat4(
        point_shadow_vp[local + 0],
        point_shadow_vp[local + 1],
        point_shadow_vp[local + 2],
        point_shadow_vp[local + 3]);

    vec4 clip = face_vp * vec4(receiver_pos, 1.0);
    vec3 ndc = clip.xyz / max(clip.w, 0.00001);
#if !SOKOL_GLSL
    ndc.y = -ndc.y;
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < 0.0 || ndc.z > 1.0)
        return 1.0;
#else
    if (ndc.x < -1.0 || ndc.x > 1.0 || ndc.y < -1.0 || ndc.y > 1.0 || ndc.z < -1.0 || ndc.z > 1.0)
        return 1.0;
#endif

    vec2 uv = ndc.xy * 0.5 + 0.5;
#if SOKOL_GLSL
    float receiver_depth = ndc.z * 0.5 + 0.5;
#else
    float receiver_depth = ndc.z;
#endif
    float slice = float(point_slot * 6 + face);
    vec2 texel = 1.0 / vec2(textureSize(sampler2DArray(cube_shadow_array, cube_shadow_array_smp), 0).xy);

    // shadow_quality (cam_pos_pad.w): 0=1-tap sharp, 1=3x3 PCF medium, 2=5x5 PCF soft
    float shadow_q = cam_pos_pad.w;
    if (shadow_q >= 2.0) {
        float vis = 0.0;
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++) {
                vec2 suv = uv + vec2(float(x), float(y)) * texel * 1.5;
                vis += texture(sampler2DArrayShadow(cube_shadow_array, cube_shadow_array_smp),
                               vec4(suv, slice, receiver_depth - bias));
            }
        return vis / 25.0;
    } else if (shadow_q >= 1.0) {
        float vis = 0.0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++) {
                vec2 suv = uv + vec2(float(x), float(y)) * texel;
                vis += texture(sampler2DArrayShadow(cube_shadow_array, cube_shadow_array_smp),
                               vec4(suv, slice, receiver_depth - bias));
            }
        return vis / 9.0;
    } else {
        return texture(sampler2DArrayShadow(cube_shadow_array, cube_shadow_array_smp),
                       vec4(uv, slice, receiver_depth - bias));
    }
}

// Sky ambient (irradiance / π) for unit normal n: per channel c + v·n + nᵀQn, see AmbientSH.cs.
float sky_ambient_channel(int i, vec3 n) {
    vec4 a = ambient_sky[i];
    vec4 b = ambient_sky[i + 1];
    vec4 c = ambient_sky[i + 2];
    return a.x + dot(a.yzw, n) + dot(b.xyz, n * n)
         + 2.0 * (b.w * n.x * n.y + c.x * n.x * n.z + c.y * n.y * n.z);
}

void main() {
    // --- material fetch ---
    vec4  diff_tex  = texture(sampler2D(diffuse_map,  diffuse_smp),  fs_uv);
    float spec_tex  = texture(sampler2D(specular_map, specular_smp), fs_uv).r;
    float opac_tex  = texture(sampler2D(opacity_map,  opacity_smp),  fs_uv).r;

    vec3  Kd        = material_kd_alpha.xyz * diff_tex.rgb * fs_custom.rgb;
    vec3  Ks        = material_ks_shin.xyz  * spec_tex;
    float Ns        = max(material_ks_shin.w, 1.0);
    float opacity   = material_kd_alpha.w   * opac_tex;

    // Alpha test (discard nearly transparent fragments).
    if (opacity < 0.05) discard;

    // --- normal ---
    vec3 N = sample_normal(fs_uv, fs_world_normal, fs_world_tangent, fs_world_bitangent);
    vec3 V = normalize(cam_pos_pad.xyz - fs_world_pos);

    // --- lighting accumulation ---
    vec3 sky     = max(vec3(sky_ambient_channel(0, N), sky_ambient_channel(3, N), sky_ambient_channel(6, N)), vec3(0.0));
    vec3 ambient = (ambient_num.xyz + sky) * Kd;
    vec3 accum   = ambient;
    int  count   = min(int(ambient_num.w), 32);

    for (int i = 0; i < count; i++) {
        vec4 d0 = lights_data[i * 4 + 0];  // xyz=pos,      w=type
        vec4 d1 = lights_data[i * 4 + 1];  // xyz=dir,      w=range
        vec4 d2 = lights_data[i * 4 + 2];  // xyz=color,    w=intensity
        vec4 d3 = lights_data[i * 4 + 3];  // x=innerCos, y=outerCos

        vec3  lc  = d2.xyz * d2.w;
        int   lt  = int(d0.w);

        vec3 L = vec3(0.0);
        float atten = 1.0;
        float shadow = 1.0;

        if (lt == 0) {
            // Directional — d1.xyz = travel direction (away from source)
            L = normalize(-d1.xyz);
            if (d3.z >= 0.0 && fs_custom.a > 0.5) {
                float slopeBias = max(0.002, 0.012 * (1.0 - max(dot(N, L), 0.0)));
                shadow = sample_dir_shadow(d3.z, slopeBias);
            }

        } else if (lt == 1) {
            // Point
            vec3  toL = d0.xyz - fs_world_pos;
            float dist = length(toL);
            float rng  = d1.w;
            if (dist >= rng || rng <= 0.0) continue;
            L     = normalize(toL);
            float t = dist / rng;
            atten = max(1.0 - t * t, 0.0);
            float nl = max(dot(N, L), 0.0);

            if (d3.z >= 0.0 && fs_custom.a > 0.5 && nl > 0.0) {
                float slopeBias = max(0.002, 0.01 * (1.0 - nl));
                float normalOffset = max(0.0003, 0.001 * (1.0 - nl));
                vec3 receiverPos = fs_world_pos + N * normalOffset;
                shadow = sample_point_shadow(int(d3.z), d0.xyz, slopeBias, receiverPos);
            }

        } else {
            // Spot
            vec3  toL = d0.xyz - fs_world_pos;
            float dist = length(toL);
            float rng  = d1.w;
            if (dist >= rng || rng <= 0.0) continue;
            L     = normalize(toL);
            float t    = dist / rng;
            atten      = max(1.0 - t * t, 0.0);
            vec3 sd    = normalize(d1.xyz);
            float cosA = dot(-L, sd);
            float sf   = clamp((cosA - d3.y) / max(d3.x - d3.y, 0.0001), 0.0, 1.0);
            atten *= sf;

            if (d3.z >= 0.0 && fs_custom.a > 0.5) {
                float slopeBias = max(0.0025, 0.014 * (1.0 - max(dot(N, L), 0.0)));
                shadow = sample_spot_shadow(d3.z, slopeBias);
            }
        }

        // Diffuse (Lambert)
        float NdotL = max(dot(N, L), 0.0);

        // Specular (Blinn-Phong)
        vec3  H     = normalize(L + V);
        float NdotH = max(dot(N, H), 0.0);
        float spec  = pow(NdotH, Ns);

        accum += lc * (atten * shadow) * (Kd * NdotL + Ks * spec);
    }

    frag_color = vec4(accum, opacity);
}
@end

@program blinn_phong blinn_phong_vs blinn_phong_fs
