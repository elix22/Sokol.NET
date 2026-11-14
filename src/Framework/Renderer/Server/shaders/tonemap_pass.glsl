// tonemap_pass.glsl
// Fullscreen HDR-to-display pass: reads an RGBA16F offscreen colour attachment,
// applies exposure + tone map operator, and writes sRGB output. With the post effects (G8.9) it is their uber pass:
// the bloom added first; on a display-encoded picture (no float target) only the effects.
// Vertex stage uses a single full-screen triangle (no vertex buffer needed).
// The UBO binding=0 must not conflict with any other resource in the same pass;
// this shader is always used in its own isolated pass, so binding=0 is safe.

@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4

// ── Vertex: clip-space fullscreen triangle from gl_VertexIndex ─────────────
@vs tonemap_vs

void main()
{
    // Generates a triangle that covers the entire NDC clip volume.
    // Vertex 0: (-1,-1)  Vertex 1: (3,-1)  Vertex 2: (-1, 3)
    vec2 uv  = vec2((gl_VertexIndex << 1) & 2, gl_VertexIndex & 2);
    gl_Position = vec4(uv * 2.0 - 1.0, 0.0, 1.0);
}

@end

// ── Fragment: tone map ──────────────────────────────────────────────────────
@fs tonemap_fs

layout(binding=0) uniform tonemap_params {
    float u_Exposure;
    int   u_Type;    // 0=linear, 1=ACES_Narkowicz, 2=ACES_Hill, 3=KHR_PBR_Neutral
    float u_Encoded; // 1: the picture is already display-encoded (no float target, G8.9): only the post effects
    float _pad1;
    vec4  u_Bloom;   // rgb: the bloom's tint x intensity (0: no bloom; G8.9a)
    vec4  u_VignetteColor;   // rgb: its colour; w: 1 / the roundness (the picture's aspect when Rounded, else 1) (G8.9b)
    vec4  u_Vignette;        // xy: its centre (0..1, y down); z: intensity x 3 (0: none); w: smoothness x 5
    vec4  u_Grade;           // x: contrast (1 = none); y: saturation (1 = none); z: hue shift (turns); w: 1 = grading on
    vec4  u_ColorFilter;     // rgb: the colour filter (linear)
};

layout(binding=0) uniform texture2D hdr_tex;
layout(binding=0) uniform sampler   hdr_smp;
layout(binding=1) uniform texture2D bloom_tex;   // the bloom's glow (half size), or 1 x 1 black
layout(binding=1) uniform sampler   bloom_smp;

out vec4 frag_color;

// ── Tone map operators ──────────────────────────────────────────────────────

const float GAMMA     = 2.2;
const float INV_GAMMA = 1.0 / GAMMA;

vec3 linearToSRGB(vec3 c)
{
    return pow(max(c, vec3(0.0)), vec3(INV_GAMMA));
}

vec3 toneMapACES_Narkowicz(vec3 c)
{
    const float A = 2.51, B = 0.03, C = 2.43, D = 0.59, E = 0.14;
    return clamp((c * (A * c + B)) / (c * (C * c + D) + E), 0.0, 1.0);
}

@include tone_curves.glsl

// ── Vignette and Color Adjustments (G8.9b), the reference editor's formulas ────────────────────────────────

// The ARRI LogC curve the reference editor grades contrast in, about its middle grey.
const float LOGC_A = 5.555556, LOGC_B = 0.047996, LOGC_C = 0.244161, LOGC_D = 0.386036;
const float ACESCC_MIDGRAY = 0.4135884;
vec3 linearToLogC(vec3 x) { return LOGC_C * log(LOGC_A * max(x, vec3(0.0)) + LOGC_B) / log(10.0) + LOGC_D; }
vec3 logCToLinear(vec3 x) { return (pow(vec3(10.0), (x - LOGC_D) / LOGC_C) - LOGC_B) / LOGC_A; }

vec3 rgbToHsv(vec3 c)
{
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-4;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsvToRgb(vec3 c)
{
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

vec3 applyVignette(vec3 color, vec2 uv)
{
    vec2 dist = abs(uv - u_Vignette.xy) * u_Vignette.z;
    dist.x *= u_VignetteColor.w;
    float f = pow(clamp(1.0 - dot(dist, dist), 0.0, 1.0), u_Vignette.w);
    return color * mix(u_VignetteColor.rgb, vec3(1.0), f);
}

vec3 applyGrading(vec3 color)
{
    vec3 lc = linearToLogC(color);
    color = logCToLinear((lc - ACESCC_MIDGRAY) * u_Grade.x + ACESCC_MIDGRAY);
    color = max(color * u_ColorFilter.rgb, vec3(0.0));
    vec3 hsv = rgbToHsv(color);
    hsv.x = fract(hsv.x + u_Grade.z);
    color = hsvToRgb(hsv);
    float luma = dot(color, vec3(0.2126729, 0.7151522, 0.0721750));
    return max(vec3(luma) + u_Grade.y * (color - vec3(luma)), vec3(0.0));
}

void main()
{
    // Reconstruct UV from fragment position.
    vec2 uv = gl_FragCoord.xy / vec2(textureSize(sampler2D(hdr_tex, hdr_smp), 0));
    vec3 color = texture(sampler2D(hdr_tex, hdr_smp), uv).rgb;

    // The post effects' uber pass (G8.9, the reference editor's order): the bloom added before the exposure and the curve.
    color += texture(sampler2D(bloom_tex, bloom_smp), uv).rgb * u_Bloom.rgb;
    // The picture's own uv for the vignette: y down on every backend (gl_FragCoord's origin is the bottom on GL).
    vec2 vuv = uv;
#if SOKOL_GLSL
    vuv.y = 1.0 - vuv.y;
#endif
    if (u_Encoded > 0.5)
    {
        // A display-encoded picture (no float target, G8.3's curve already in it): back to linear, the effects, encoded again.
        if (u_Vignette.z > 0.0 || u_Grade.w > 0.5)
        {
            color = pow(max(color, vec3(0.0)), vec3(GAMMA));
            if (u_Vignette.z > 0.0) color = applyVignette(color, vuv);
            if (u_Grade.w > 0.5) color = applyGrading(color);
            color = linearToSRGB(color);
        }
        frag_color = vec4(color, 1.0);
        return;
    }
    if (u_Vignette.z > 0.0) color = applyVignette(color, vuv);

    color *= u_Exposure;
    if (u_Grade.w > 0.5) color = applyGrading(color);

    if (u_Type == 1)      color = toneMapACES_Narkowicz(color);
    else if (u_Type == 2) color = toneMapACES_Hill(color);
    else if (u_Type == 3) color = toneMapKhrPbrNeutral(color);
    // u_Type == 0: linear (exposure only, no curve)

    frag_color = vec4(linearToSRGB(color), 1.0);
}

@end

@program tonemap_pass tonemap_vs tonemap_fs
