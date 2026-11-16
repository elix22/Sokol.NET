// screen_copy.glsl
// Exact-passthrough fullscreen blit: samples one color texture and writes it
// unchanged. Used by the transmission path to capture the opaque scene into a
// sampleable "screen color" texture (you cannot sample the attachment you are
// drawing into). Deliberately NOT the tonemap pass — that one always re-encodes
// sRGB, which would double-encode an already display-ready RGBA8 scene target.
// Vertex stage is a single full-screen triangle (no vertex buffer needed).

// ── Vertex: clip-space fullscreen triangle from gl_VertexIndex ─────────────
@vs screen_copy_vs

void main()
{
    vec2 uv  = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}

@end

// ── Fragment: straight copy ─────────────────────────────────────────────────
@fs screen_copy_fs

layout(binding=0) uniform texture2D src_tex;
layout(binding=0) uniform sampler   src_smp;
layout(binding=0) uniform screen_copy_params {
    vec2 dst_size;   // the target's pixels: the source is stretched over it (a scene drawn at a render scale under 1)
};

out vec4 frag_color;

void main()
{
    // Reconstruct UV from window-space fragment position (backend-orientation safe,
    // mirrors tonemap_pass.glsl) so the copy preserves the source's texel layout - over the
    // TARGET's pixels: divided by the source's size, a smaller source (render scale 0.75) was
    // drawn 1:1 into the window's corner and its edge smeared over the rest.
    vec2 uv = gl_FragCoord.xy / dst_size;
    frag_color = texture(sampler2D(src_tex, src_smp), uv);
}

@end

@program screen_copy screen_copy_vs screen_copy_fs
