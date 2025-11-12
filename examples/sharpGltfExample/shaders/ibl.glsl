// Image-Based Lighting (IBL) functions for PBR rendering
// Adapted from Khronos glTF-Sample-Viewer
// https://github.com/KhronosGroup/glTF-Sample-Viewer

// NOTE: Before including this file, the main shader must define:
// - uniform float u_EnvIntensity
// - uniform int u_MipCount
// - uniform mat4 u_EnvRotation
// - uniform ivec2 u_TransmissionFramebufferSize (for transmission)
// - Combined samplers: u_GGXEnvSampler, u_LambertianEnvSampler, u_GGXLUT
// - Combined samplers: u_CharlieEnvSampler, u_CharlieLUT (for sheen)
// - Combined sampler: u_TransmissionFramebufferSampler (for transmission)
// - Function: float applyIorToRoughness(float roughness, float ior)

// Helper functions for volume transmission
vec3 applyVolumeAttenuation(vec3 radiance, float transmissionDistance, vec3 attenuationColor, float attenuationDistance)
{
    if (attenuationDistance == 0.0)
    {
        // Attenuation distance is +∞ (which we indicate by zero), i.e. the transmitted color is not attenuated at all.
        return radiance;
    }
    else
    {
        // Compute light attenuation using Beer's law.
        vec3 transmittance = pow(attenuationColor, vec3(transmissionDistance / attenuationDistance));
        return transmittance * radiance;
    }
}

vec3 getVolumeTransmissionRay(vec3 n, vec3 v, float thickness, float ior, mat4 modelMatrix)
{
    // Direction of refracted light.
    vec3 refractionVector = refract(-v, normalize(n), 1.0 / ior);

    // Compute rotation-independant scaling of the model matrix.
    vec3 modelScale;
    modelScale.x = length(vec3(modelMatrix[0].xyz));
    modelScale.y = length(vec3(modelMatrix[1].xyz));
    modelScale.z = length(vec3(modelMatrix[2].xyz));

    // The thickness is specified in local space.
    return normalize(refractionVector) * thickness * modelScale;
}

vec3 getDiffuseLight(vec3 n)
{
    vec3 rotatedNormal = mat3(u_EnvRotation) * n;
    vec4 textureSample = texture(u_LambertianEnvSampler, rotatedNormal);
    textureSample.rgb *= u_EnvIntensity;
    return textureSample.rgb;
}


vec4 getSpecularSample(vec3 reflection, float lod)
{
    vec3 rotatedReflection = mat3(u_EnvRotation) * reflection;
    vec4 textureSample = textureLod(u_GGXEnvSampler, rotatedReflection, lod);
    textureSample.rgb *= u_EnvIntensity;
    return textureSample;
}

// Charlie sheen functions disabled when MORPHING is enabled (they share texture bindings 8-9)
#ifndef MORPHING
vec4 getSheenSample(vec3 reflection, float lod)
{
    vec3 rotatedReflection = mat3(u_EnvRotation) * reflection;
    vec4 textureSample = textureLod(u_CharlieEnvSampler, rotatedReflection, lod);
    textureSample.rgb *= u_EnvIntensity;
    return textureSample;
}
#endif

vec3 getIBLGGXFresnel(vec3 n, vec3 v, float roughness, vec3 F0, float specularWeight)
{
    // see https://bruop.github.io/ibl/#single_scattering_results at Single Scattering Results
    // Roughness dependent fresnel, from Fdez-Aguera
    float NdotV = clampedDot(n, v);
    vec2 brdfSamplePoint = clamp(vec2(NdotV, roughness), vec2(0.0, 0.0), vec2(1.0, 1.0));
    vec2 f_ab = texture(u_GGXLUT, brdfSamplePoint).rg;
    vec3 Fr = max(vec3(1.0 - roughness), F0) - F0;
    vec3 k_S = F0 + Fr * pow(1.0 - NdotV, 5.0);
    vec3 FssEss = specularWeight * (k_S * f_ab.x + f_ab.y);

    // Multiple scattering, from Fdez-Aguera
    float Ems = (1.0 - (f_ab.x + f_ab.y));
    vec3 F_avg = specularWeight * (F0 + (1.0 - F0) / 21.0);
    vec3 FmsEms = Ems * FssEss * F_avg / (1.0 - F_avg * Ems);

    return FssEss + FmsEms;
}

vec3 getIBLRadianceGGX(vec3 n, vec3 v, float roughness)
{
    float NdotV = clampedDot(n, v);
    float lod = roughness * float(u_MipCount - 1);
    vec3 reflection = normalize(reflect(-v, n));
    vec4 specularSample = getSpecularSample(reflection, lod);

    vec3 specularLight = specularSample.rgb;

    return specularLight;
}

#ifdef TRANSMISSION
vec3 getTransmissionSample(vec2 fragCoord, float roughness, float ior)
{
    float framebufferLod = log2(float(u_TransmissionFramebufferSize.x)) * applyIorToRoughness(roughness, ior);
    vec3 transmittedLight = textureLod(u_TransmissionFramebufferSampler, fragCoord.xy, framebufferLod).rgb;

    return transmittedLight;
}


vec3 getIBLVolumeRefraction(vec3 n, vec3 v, float perceptualRoughness, vec3 baseColor, vec3 position, mat4 modelMatrix,
    mat4 viewMatrix, mat4 projMatrix, float ior, float thickness, vec3 attenuationColor, float attenuationDistance, float dispersion)
{
    // Dispersion will spread out the ior values for each r,g,b channel
    float halfSpread = (ior - 1.0) * 0.025 * dispersion;
    vec3 iors = vec3(ior - halfSpread, ior, ior + halfSpread);

    vec3 transmittedLight;
    float transmissionRayLength;
    for (int i = 0; i < 3; i++)
    {
        vec3 transmissionRay = getVolumeTransmissionRay(n, v, thickness, iors[i], modelMatrix);
        // TODO: taking length of blue ray, ideally we would take the length of the green ray. For now overwriting seems ok
        transmissionRayLength = length(transmissionRay);
        vec3 refractedRayExit = position + transmissionRay;

        // Project refracted vector on the framebuffer, while mapping to normalized device coordinates.
        vec4 ndcPos = projMatrix * viewMatrix * vec4(refractedRayExit, 1.0);
        vec2 refractionCoords = ndcPos.xy / ndcPos.w;
        refractionCoords += 1.0;
        refractionCoords /= 2.0;

        // Sample framebuffer to get pixel the refracted ray hits for this color channel.
        transmittedLight[i] = getTransmissionSample(refractionCoords, perceptualRoughness, iors[i])[i];
    }

    vec3 attenuatedColor = applyVolumeAttenuation(transmittedLight, transmissionRayLength, attenuationColor, attenuationDistance);

    return attenuatedColor * baseColor;
}
#endif // TRANSMISSION

vec3 getIBLRadianceAnisotropy(vec3 n, vec3 v, float roughness, float anisotropy, vec3 anisotropyDirection)
{
    float NdotV = clampedDot(n, v);

    float tangentRoughness = mix(roughness, 1.0, anisotropy * anisotropy);
    vec3  anisotropicTangent  = cross(anisotropyDirection, v);
    vec3  anisotropicNormal   = cross(anisotropicTangent, anisotropyDirection);
    float bendFactor          = 1.0 - anisotropy * (1.0 - roughness);
    float bendFactorPow4      = bendFactor * bendFactor * bendFactor * bendFactor;
    vec3  bentNormal          = normalize(mix(anisotropicNormal, n, bendFactorPow4));

    float lod = roughness * float(u_MipCount - 1);
    vec3 reflection = normalize(reflect(-v, bentNormal));

    vec4 specularSample = getSpecularSample(reflection, lod);

    vec3 specularLight = specularSample.rgb;

    return specularLight;
}

#ifndef MORPHING
vec3 getIBLRadianceCharlie(vec3 n, vec3 v, float sheenRoughness, vec3 sheenColor)
{
    float NdotV = clampedDot(n, v);
    float lod = sheenRoughness * float(u_MipCount - 1);
    vec3 reflection = normalize(reflect(-v, n));

    vec2 brdfSamplePoint = clamp(vec2(NdotV, sheenRoughness), vec2(0.0, 0.0), vec2(1.0, 1.0));
    float brdf = texture(u_CharlieLUT, brdfSamplePoint).b;
    vec4 sheenSample = getSheenSample(reflection, lod);

    vec3 sheenLight = sheenSample.rgb;
    return sheenLight * sheenColor * brdf;
}
#endif

