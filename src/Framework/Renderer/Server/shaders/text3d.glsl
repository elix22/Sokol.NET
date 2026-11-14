// text3d.glsl — G8.4 (examples/GameEditor/docs/TEXT3D_SVG_DRAW_DESIGN.md): a Text3D's quad in the world. Its corners are
// placed on the CPU (turned with its entity or to the camera); the texture holds the text as NanoVG drew it
// (premultiplied alpha, its colours and outline in it).
@ctype mat4 System.Numerics.Matrix4x4

@vs text3d_vs
layout(binding=0) uniform text3d_vs_params {
    mat4 view_proj;
};
in vec3 position;
in vec2 texcoord;
out vec2 v_uv;
void main()
{
    gl_Position = view_proj * vec4(position, 1.0);
    v_uv = texcoord;
}
@end

@fs text3d_fs
layout(binding=0) uniform texture2D text_tex;
layout(binding=0) uniform sampler   text_smp;
in vec2 v_uv;
out vec4 frag_color;
void main()
{
    frag_color = texture(sampler2D(text_tex, text_smp), v_uv);
}
@end

@program text3d text3d_vs text3d_fs
