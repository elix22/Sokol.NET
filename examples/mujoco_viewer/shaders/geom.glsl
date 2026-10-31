@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4

// One draw per MuJoCo geom (or capsule part): unit shape scaled by the model matrix,
// lit by one directional light plus ambient.
@vs vs
layout(binding=0) uniform vs_params {
    mat4 mvp;
    mat4 normal_mat;
};

layout(location=0) in vec4 position;
layout(location=1) in vec3 normal;

out vec3 v_normal;

void main() {
    gl_Position = mvp * position;
    v_normal = (normal_mat * vec4(normal, 0.0)).xyz;
}
@end

@fs fs
layout(binding=1) uniform fs_params {
    vec4 color;
    vec4 light_dir;
};

in vec3 v_normal;
out vec4 frag_color;

void main() {
    vec3 n = normalize(v_normal);
    float diffuse = max(dot(n, -light_dir.xyz), 0.0);
    float fill = 0.25 * max(dot(n, light_dir.xyz), 0.0);
    frag_color = vec4(color.rgb * (0.3 + 0.65 * diffuse + fill), color.a);
}
@end

@program geom vs fs
