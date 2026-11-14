// fxaa.glsl - the post effects' FXAA (G8.9c, examples/GameEditor/docs/POST_EFFECTS_DESIGN.md §3), the reference editor's:
// the four diagonal neighbours give the edge's direction, four taps along it a blur, kept unless it leaves the
// neighbourhood's luma range. On the display's values (after the tone curve). A full-screen triangle; uv from
// gl_FragCoord and the target's size (as screen_copy.glsl) - the source may be smaller (a render scale): it is stretched.

@ctype vec4 System.Numerics.Vector4

@vs fxaa_vs
void main()
{
    vec2 uv = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}
@end

@fs fxaa_fs
layout(binding=0) uniform fxaa_params {
    vec4 u_Target;   // xy: 1 / the target's size
    vec4 u_Source;   // xy: 1 / the source's size
};
layout(binding=0) uniform texture2D src_tex;
layout(binding=0) uniform sampler src_smp;
out vec4 frag_color;

const float SPAN_MAX = 8.0;
const float REDUCE_MUL = 1.0 / 8.0;
const float REDUCE_MIN = 1.0 / 128.0;

vec3 tap(vec2 uv) { return clamp(texture(sampler2D(src_tex, src_smp), uv).rgb, 0.0, 1.0); }
float luma(vec3 c) { return dot(c, vec3(0.2126729, 0.7151522, 0.0721750)); }

void main()
{
    vec2 uv = gl_FragCoord.xy * u_Target.xy;
    vec2 d = u_Source.xy;
    vec3 rgbM = tap(uv);
    vec3 rgbNW = tap(uv + vec2(-d.x, -d.y)), rgbNE = tap(uv + vec2(d.x, -d.y));
    vec3 rgbSW = tap(uv + vec2(-d.x, d.y)), rgbSE = tap(uv + vec2(d.x, d.y));
    float lM = luma(rgbM), lNW = luma(rgbNW), lNE = luma(rgbNE), lSW = luma(rgbSW), lSE = luma(rgbSE);
    vec2 dir = vec2(-((lNW + lNE) - (lSW + lSE)), (lNW + lSW) - (lNE + lSE));
    float reduce = max((lNW + lNE + lSW + lSE) * (0.25 * REDUCE_MUL), REDUCE_MIN);
    float rcpMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
    dir = clamp(dir * rcpMin, vec2(-SPAN_MAX), vec2(SPAN_MAX)) * d;
    vec3 a0 = tap(uv + dir * (0.0 / 3.0 - 0.5)), a1 = tap(uv + dir * (1.0 / 3.0 - 0.5));
    vec3 a2 = tap(uv + dir * (2.0 / 3.0 - 0.5)), a3 = tap(uv + dir * (3.0 / 3.0 - 0.5));
    vec3 rgbA = 0.5 * (a1 + a2);
    vec3 rgbB = rgbA * 0.5 + 0.25 * (a0 + a3);
    float lB = luma(rgbB);
    float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
    float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));
    frag_color = vec4((lB < lMin || lB > lMax) ? rgbA : rgbB, 1.0);
}
@end

@program fxaa fxaa_vs fxaa_fs
