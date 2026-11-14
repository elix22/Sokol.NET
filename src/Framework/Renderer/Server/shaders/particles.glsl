// particles.glsl — G8.7 (examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §3.0): the world's particles (Scene/WorldParticles),
// drawn by Sprites2D/ParticleBatch.cs: the instance layout of Render2D's sprite.glsl (a unit quad, per particle {origin, axis_x,
// axis_y, uv rect, colour}), its own pipelines for Alpha and Additive blending.
@ctype mat4 System.Numerics.Matrix4x4

@vs particles_vs

layout(binding=0) uniform particles_params {
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
flat out vec4 v_rect;   // the frame's uv rectangle, min xy and max zw: the fragment shader keeps v_uv inside it

void main()
{
    vec3 world = i_origin + corner.x * i_axis_x + corner.y * i_axis_y;
    gl_Position = view_proj * vec4(world, 1.0);
    v_uv    = mix(i_uvrect.xy, i_uvrect.zw, corner);
    v_color = i_color;
    v_rect  = vec4(min(i_uvrect.xy, i_uvrect.zw), max(i_uvrect.xy, i_uvrect.zw));
}

@end

@fs particles_fs

layout(binding=0) uniform texture2D particles_tex;
layout(binding=0) uniform sampler   particles_smp;

in vec2 v_uv;
in vec4 v_color;
flat in vec4 v_rect;
out vec4 frag_color;

void main()
{
    // Kept half a texel inside the frame (a Texture Sheet's cell): filtered linearly, a sample at the cell's edge would take
    // half of the next cell's texel - a seam of mixed colour at every quad's edge, seen through the overlapping particles
    // (G8.7's check, all four targets). Half a texel in, nearest filtering still takes the edge texel itself. The same
    // keeps a quad's edge pixel under MSAA (shaded at its centre, outside the quad) inside the frame.
    vec2 d = vec2(0.5) / vec2(textureSize(sampler2D(particles_tex, particles_smp), 0));
    vec2 uv = clamp(v_uv, v_rect.xy + d, v_rect.zw - d);
    frag_color = v_color * texture(sampler2D(particles_tex, particles_smp), uv);
}

@end

@program particles particles_vs particles_fs
