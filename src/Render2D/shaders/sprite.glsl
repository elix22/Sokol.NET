// sprite.glsl — Sokol.Render2D world-space sprites (Renderer/SpriteBatch.cs).
// One instanced unit-quad pipeline: vbuf[0] = static [0,1] quad corners, vbuf[1] = a per-sprite
// instance stream {origin, axis_x, axis_y, uv rect, colour}. The quad is origin + corner.x * axis_x
// + corner.y * axis_y in WORLD units, so any world matrix (rotation, scale, a parent's) is already
// in the two axes. Drawn inside the pass that is open, with the camera's view-projection matrix.

@vs sprite_vs

layout(binding=0) uniform sprite_params {
    mat4 view_proj;
};

in vec2 corner;     // static unit quad in [0,1]  (vbuf 0)
in vec3 i_origin;   // instance: world position of the quad's bottom-left corner  (vbuf 1)
in vec3 i_axis_x;   // instance: world vector along the quad's width
in vec3 i_axis_y;   // instance: world vector along the quad's height
in vec4 i_uvrect;   // instance: texture sub-rect (xy = bottom-left corner's uv, zw = top-right corner's)
in vec4 i_color;    // instance: straight RGBA

out vec2 v_uv;
out vec4 v_color;

void main()
{
    vec3 world = i_origin + corner.x * i_axis_x + corner.y * i_axis_y;
    gl_Position = view_proj * vec4(world, 1.0);
    v_uv    = mix(i_uvrect.xy, i_uvrect.zw, corner);
    v_color = i_color;
}

@end

@fs sprite_fs

layout(binding=0) uniform texture2D sprite_tex;
layout(binding=0) uniform sampler   sprite_smp;

in vec2 v_uv;
in vec4 v_color;
out vec4 frag_color;

void main()
{
    frag_color = v_color * texture(sampler2D(sprite_tex, sprite_smp), v_uv);
}

@end

@program sprite sprite_vs sprite_fs
