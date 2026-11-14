// Tone curves shared by the tonemap pass (an HDR target's picture → the display) and the PBR shader's own output on a
// GPU without float render targets (G8.3, examples/GameEditor/docs/HDR_TONEMAP_DESIGN.md). Functions only.

const mat3 ACESInputMat = mat3(
     0.59719,  0.07600,  0.02840,
     0.35458,  0.90834,  0.13383,
     0.04823,  0.01566,  0.83777);

const mat3 ACESOutputMat = mat3(
     1.60475, -0.10208, -0.00327,
    -0.53108,  1.10813, -0.07276,
    -0.07367, -0.00605,  1.07602);

vec3 RRTAndODTFit(vec3 c)
{
    vec3 a = c * (c + 0.0245786) - 0.000090537;
    vec3 b = c * (0.983729 * c + 0.4329510) + 0.238081;
    return a / b;
}

vec3 toneMapACES_Hill(vec3 c)
{
    c = ACESInputMat * c;
    c = RRTAndODTFit(c);
    c = ACESOutputMat * c;
    return clamp(c, 0.0, 1.0);
}

vec3 toneMapKhrPbrNeutral(vec3 c)
{
    const float startCompression = 0.8 - 0.04;
    const float desaturation     = 0.15;
    float x = min(c.r, min(c.g, c.b));
    float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
    c -= offset;
    float peak = max(c.r, max(c.g, c.b));
    if (peak < startCompression) return c;
    const float d = 1.0 - startCompression;
    float newPeak = 1.0 - d * d / (peak + d - startCompression);
    c *= newPeak / peak;
    float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
    return mix(c, newPeak * vec3(1.0), g);
}
