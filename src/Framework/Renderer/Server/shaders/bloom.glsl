// bloom.glsl - the post effects' bloom (G8.9a, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md §3), the reference editor's:
// a prefilter at half size (threshold with a soft knee, clamp), a chain of halvings, then back up, each level mixed with
// the one under it by Scatter. Every pass is a full-screen triangle into a colour-only target; uv from gl_FragCoord and
// the target's size (the same origin as the texture read, on every backend - as tonemap_pass.glsl).

@ctype vec4 System.Numerics.Vector4

@vs bloom_vs
void main()
{
    vec2 uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}
@end

// u_Target.xy: 1 / the target's size; u_Source.xy: 1 / the source's size.
// u_Params: prefilter - threshold (linear), knee, clamp, encoded (1: the source is display-encoded, LDR);
//           down - hq (1: 13 taps), -, -, -;   up - scatter, -, -, -.

@fs bloom_prefilter_fs
layout(binding=0) uniform bloom_params { vec4 u_Target; vec4 u_Source; vec4 u_Params; };
layout(binding=0) uniform texture2D src_tex;
layout(binding=0) uniform sampler src_smp;
out vec4 frag_color;

void main()
{
    vec2 uv = gl_FragCoord.xy * u_Target.xy;
    vec2 d = u_Source.xy;
    // 4 bilinear taps: the 4 x 4 source texels under this half-size texel, averaged.
    vec3 c = texture(sampler2D(src_tex, src_smp), uv + vec2(-d.x, -d.y)).rgb
           + texture(sampler2D(src_tex, src_smp), uv + vec2( d.x, -d.y)).rgb
           + texture(sampler2D(src_tex, src_smp), uv + vec2(-d.x,  d.y)).rgb
           + texture(sampler2D(src_tex, src_smp), uv + vec2( d.x,  d.y)).rgb;
    c *= 0.25;
    c = min(c, vec3(u_Params.z));
    float threshold = u_Params.x, knee = u_Params.y;
    float bright = max(c.r, max(c.g, c.b));
    float soft = clamp(bright - threshold + knee, 0.0, 2.0 * knee);
    soft = soft * soft / (4.0 * knee + 1e-4);
    c *= max(bright - threshold, soft) / max(bright, 1e-4);
    frag_color = vec4(c, 1.0);
}
@end

@fs bloom_down_fs
layout(binding=0) uniform bloom_params { vec4 u_Target; vec4 u_Source; vec4 u_Params; };
layout(binding=0) uniform texture2D src_tex;
layout(binding=0) uniform sampler src_smp;
out vec4 frag_color;

vec3 tap(vec2 uv) { return texture(sampler2D(src_tex, src_smp), uv).rgb; }

void main()
{
    vec2 uv = gl_FragCoord.xy * u_Target.xy;
    vec2 d = u_Source.xy;
    vec3 c;
    if (u_Params.x > 0.5)
    {
        // High Quality Filtering: 13 taps (a 4 x 4 box in the middle, four 2 x 2 boxes round it).
        vec3 a = tap(uv + d * vec2(-2.0, -2.0)), b = tap(uv + d * vec2(0.0, -2.0)), e = tap(uv + d * vec2(2.0, -2.0));
        vec3 f = tap(uv + d * vec2(-1.0, -1.0)), g = tap(uv + d * vec2(1.0, -1.0));
        vec3 h = tap(uv + d * vec2(-2.0,  0.0)), i = tap(uv), j = tap(uv + d * vec2(2.0, 0.0));
        vec3 k = tap(uv + d * vec2(-1.0,  1.0)), l = tap(uv + d * vec2(1.0, 1.0));
        vec3 m = tap(uv + d * vec2(-2.0,  2.0)), n = tap(uv + d * vec2(0.0, 2.0)), o = tap(uv + d * vec2(2.0, 2.0));
        c = (f + g + k + l) * 0.125
          + (a + b + h + i) * 0.03125 + (b + e + i + j) * 0.03125
          + (h + i + m + n) * 0.03125 + (i + j + n + o) * 0.03125;
    }
    else
        c = (tap(uv + d * vec2(-1.0, -1.0)) + tap(uv + d * vec2(1.0, -1.0)) + tap(uv + d * vec2(-1.0, 1.0)) + tap(uv + d * vec2(1.0, 1.0))) * 0.25;
    frag_color = vec4(c, 1.0);
}
@end

@fs bloom_up_fs
layout(binding=0) uniform bloom_params { vec4 u_Target; vec4 u_Source; vec4 u_Params; };
layout(binding=0) uniform texture2D src_tex;    // the level under (smaller), already upsampled to there
layout(binding=0) uniform sampler src_smp;
layout(binding=1) uniform texture2D high_tex;   // this level's own (the down chain's)
layout(binding=1) uniform sampler high_smp;
out vec4 frag_color;

vec3 tap(vec2 uv) { return texture(sampler2D(src_tex, src_smp), uv).rgb; }

void main()
{
    vec2 uv = gl_FragCoord.xy * u_Target.xy;
    vec2 d = u_Source.xy;
    // A 3 x 3 tent over the smaller level.
    vec3 low = tap(uv) * 4.0
             + (tap(uv + vec2(-d.x, 0.0)) + tap(uv + vec2(d.x, 0.0)) + tap(uv + vec2(0.0, -d.y)) + tap(uv + vec2(0.0, d.y))) * 2.0
             + tap(uv - d) + tap(uv + d) + tap(uv + vec2(-d.x, d.y)) + tap(uv + vec2(d.x, -d.y));
    low *= 1.0 / 16.0;
    vec3 high = texture(sampler2D(high_tex, high_smp), uv).rgb;
    frag_color = vec4(mix(high, low, u_Params.x), 1.0);
}
@end

@program bloom_prefilter bloom_vs bloom_prefilter_fs
@program bloom_down bloom_vs bloom_down_fs
@program bloom_up bloom_vs bloom_up_fs
