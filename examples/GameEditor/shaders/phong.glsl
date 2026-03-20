// Phong lit shader — up to 256 lights (directional / point / spot).
// Vertex format: sokol-shape
//   attr 0  position  vec4  (SG_VERTEXFORMAT_FLOAT3, w unused)
//   attr 1  normal    vec4  (SG_VERTEXFORMAT_BYTE4N)
//   attr 2  texcoord  vec2  (unused)
//   attr 3  color0    vec4  (SG_VERTEXFORMAT_UBYTE4N)
//
// Light packing — each light occupies 4 consecutive vec4s in lights_data[]:
//   [i*4+0]: xyz = position (point/spot) OR travel-direction (directional)
//            w   = type  (0=directional, 1=point, 2=spot)
//   [i*4+1]: xyz = spot direction (spot only)
//            w   = range
//   [i*4+2]: xyz = color,  w = intensity
//   [i*4+3]: x = inner_cos, y = outer_cos, zw = unused

@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4

@vs phong_vs
layout(binding=0) uniform phong_vs_params {
    mat4 mvp;
    mat4 model;
};

layout(location=0) in vec4 in_pos;
layout(location=1) in vec3 in_normal;
layout(location=2) in vec2 in_uv;
layout(location=3) in vec4 in_color;

out vec3 world_normal;
out vec3 world_pos;
out vec4 base_color;

void main() {
    gl_Position  = mvp * in_pos;
    world_normal = mat3(model[0].xyz, model[1].xyz, model[2].xyz) * in_normal;
    world_pos    = (model * in_pos).xyz;
    base_color   = in_color;
}
@end

@fs phong_fs
layout(binding=1) uniform phong_fs_params {
    vec4 ambient_and_count;  // xyz = ambient colour,  w = float(light_count)
    vec4 lights_data[64];  // 16 lights × 4 vec4s each
};

in vec3 world_normal;
in vec3 world_pos;
in vec4 base_color;
out vec4 frag_color;

void main() {
    vec3 n     = normalize(world_normal);
    vec3 accum = ambient_and_count.xyz;
    int  count = min(int(ambient_and_count.w), 256);

    for (int i = 0; i < count; i++) {
        vec4 d0 = lights_data[i * 4 + 0];
        vec4 d1 = lights_data[i * 4 + 1];
        vec4 d2 = lights_data[i * 4 + 2];
        vec4 d3 = lights_data[i * 4 + 3];
        vec3 lc = d2.xyz * d2.w;
        int  lt = int(d0.w);

        if (lt == 0) {
            // Directional — d0.xyz = travel direction
            vec3  L    = normalize(-d0.xyz);
            accum += lc * max(dot(n, L), 0.0);

        } else if (lt == 1) {
            // Point — d0.xyz = position,  d1.w = range
            vec3  toL  = d0.xyz - world_pos;
            float dist = length(toL);
            float rng  = d1.w;
            if (dist < rng && rng > 0.0) {
                vec3  L   = normalize(toL);
                float t   = dist / rng;
                float att = max(1.0 - t * t, 0.0);
                accum += lc * max(dot(n, L), 0.0) * att;
            }

        } else {
            // Spot — d0.xyz = position, d1.xyz = direction, d1.w = range, d3.xy = inner/outer cos
            vec3  toL  = d0.xyz - world_pos;
            float dist = length(toL);
            float rng  = d1.w;
            if (dist < rng && rng > 0.0) {
                vec3  L    = normalize(toL);
                float t    = dist / rng;
                float att  = max(1.0 - t * t, 0.0);
                vec3  sd   = normalize(d1.xyz);
                float cosA = dot(-L, sd);
                float sf   = clamp((cosA - d3.y) / max(d3.x - d3.y, 0.0001), 0.0, 1.0);
                accum += lc * max(dot(n, L), 0.0) * att * sf;
            }
        }
    }

    frag_color = vec4(base_color.rgb * accum, base_color.a);
}
@end

@program phong phong_vs phong_fs
