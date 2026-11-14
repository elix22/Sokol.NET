// lines.glsl — G8.4 D3 (examples/GameEditor/docs/TEXT3D_SVG_DRAW_DESIGN.md §5c): the lines scripts draw in the world
// (Draw.Line, Ray, Box, Sphere). A colour per vertex.
@ctype mat4 System.Numerics.Matrix4x4

@vs lines_vs
layout(binding=0) uniform lines_vs_params {
    mat4 view_proj;
};
in vec3 position;
in vec4 color0;
out vec4 v_color;
void main()
{
    gl_Position = view_proj * vec4(position, 1.0);
    v_color = color0;
}
@end

@fs lines_fs
in vec4 v_color;
out vec4 frag_color;
void main()
{
    frag_color = v_color;
}
@end

@program lines lines_vs lines_fs
