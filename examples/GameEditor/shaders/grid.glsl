@ctype mat4 System.Numerics.Matrix4x4
@ctype vec3 System.Numerics.Vector3

@vs grid_vs
layout(binding=0) uniform grid_vs_params {
    mat4 viewproj;
    vec3 eye_pos;
    float _pad;
};

in vec2 in_pos;           // XZ plane quad vertex (-1..1)
out vec3 world_pos;

void main() {
    // Scale to large area around origin
    vec3 pos = vec3(in_pos.x * 500.0, 0.0, in_pos.y * 500.0);
    world_pos = pos;
    gl_Position = viewproj * vec4(pos, 1.0);
}
@end

@fs grid_fs
layout(binding=1) uniform grid_fs_params {
    mat4 viewproj;
    vec3 eye_pos;
    float _pad;
    float near_plane;
    float far_plane;
    float _pad2;
    float _pad3;
};

in vec3 world_pos;
out vec4 frag_color;

float grid_alpha(vec2 uv, float scale) {
    vec2 g = abs(fract(uv * scale) - 0.5);
    vec2 dg = fwidth(uv * scale);
    g = smoothstep(dg * 0.5, dg * 1.5, g);
    return 1.0 - min(g.x, g.y);
}

void main() {
    vec2 xz = world_pos.xz;

    float dist = length(world_pos - eye_pos);
    float fade = 1.0 - smoothstep(80.0, 120.0, dist);

    float g1 = grid_alpha(xz, 1.0) * 0.6;
    float g2 = grid_alpha(xz, 0.1) * 0.25;
    float g  = max(g1, g2) * fade;

    if (g < 0.01) discard;

    vec4 color = vec4(0.5, 0.5, 0.5, g);

    // Z-axis (x ≈ 0) → blue
    float xw = fwidth(xz.x);
    if (abs(xz.x) < xw * 2.0)
        color = vec4(0.3, 0.3, 1.0, g);

    // X-axis (z ≈ 0) → red
    float zw = fwidth(xz.y);
    if (abs(xz.y) < zw * 2.0)
        color = vec4(1.0, 0.3, 0.3, g);

    frag_color = color;
}
@end

@program grid grid_vs grid_fs
