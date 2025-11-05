
float clampedDot(vec3 x, vec3 y)
{
    return clamp(dot(x, y), 0.0, 1.0);
}


float max3(vec3 v)
{
    return max(max(v.x, v.y), v.z);
}


float sq(float t)
{
    return t * t;
}

vec2 sq(vec2 t)
{
    return t * t;
}

vec3 sq(vec3 t)
{
    return t * t;
}

vec4 sq(vec4 t)
{
    return t * t;
}


float applyIorToRoughness(float roughness, float ior)
{
    // Scale roughness with IOR so that an IOR of 1.0 results in no microfacet refraction and
    // an IOR of 1.5 results in the default amount of microfacet refraction.
    return roughness * clamp(ior * 2.0 - 2.0, 0.0, 1.0);
}

vec3 rgb_mix(vec3 base, vec3 layer, vec3 rgb_alpha)
{
    float rgb_alpha_max = max(rgb_alpha.r, max(rgb_alpha.g, rgb_alpha.b));
    return (1.0 - rgb_alpha_max) * base + rgb_alpha * layer;
}

vec3 linear_to_srgb(vec3 linear) {
    return pow(linear, vec3(1.0/2.2));
}

vec4 srgb_to_linear(vec4 srgb) {
    return vec4(pow(srgb.rgb, vec3(2.2)), srgb.a);
}

// Uncharted 2 tone map
// see: http://filmicworlds.com/blog/filmic-tonemapping-operators/
vec3 toneMapUncharted2Impl(vec3 color) {
    const float A = 0.15;
    const float B = 0.50;
    const float C = 0.10;
    const float D = 0.20;
    const float E = 0.02;
    const float F = 0.30;
    return ((color*(A*color+C*B)+D*E)/(color*(A*color+B)+D*F))-E/F;
}

vec3 toneMapUncharted(vec3 color) {
    const float W = 11.2;
    color = toneMapUncharted2Impl(color * 2.0);
    vec3 whiteScale = 1.0 / toneMapUncharted2Impl(vec3(W));
    return linear_to_srgb(color * whiteScale);
}

vec3 tone_map(vec3 color) {
    return toneMapUncharted(color);
}


// Calculate the 3D transmission ray through the volume
// Returns a ray vector in world space representing the path through the material
vec3 getVolumeTransmissionRay(vec3 n, vec3 v, float thickness, float ior, mat4 modelMatrix) {
    // Direction of refracted light using Snell's law
    vec3 refractionVector = refract(-v, normalize(n), 1.0 / ior);
    
    // Compute rotation-independent scaling of the model matrix
    vec3 modelScale;
    modelScale.x = length(vec3(modelMatrix[0].xyz));
    modelScale.y = length(vec3(modelMatrix[1].xyz));
    modelScale.z = length(vec3(modelMatrix[2].xyz));
    
    // The thickness is specified in local space
    // Scale by model transform and apply the refraction direction
    return refractionVector * thickness * modelScale;
}

// Apply Beer's law volume attenuation
// Compute attenuated light as it travels through a volume
vec3 applyVolumeAttenuation(vec3 radiance, float transmissionDistance, 
                            vec3 attenuationColor, float attenuationDistance) {
    if (attenuationDistance == 0.0) {
        // Attenuation distance is +∞, transmitted color is not attenuated
        return radiance;
    }
    
    // Compute light attenuation using Beer's law
    // transmittance = attenuationColor^(distance / attenuationDistance)
    vec3 transmittance = pow(attenuationColor, vec3(transmissionDistance / attenuationDistance));
    return transmittance * radiance;
}


// Apply texture transform (KHR_texture_transform)
vec2 apply_texture_transform(vec2 uv, vec2 offset, float rotation, vec2 scale) {
    // Scale
    vec2 transformed_uv = uv * scale;
    
    // Rotation around (0.5, 0.5) pivot
    if (rotation != 0.0) {
        transformed_uv -= vec2(0.5);
        float c = cos(rotation);
        float s = sin(rotation);
        mat2 rotation_matrix = mat2(c, s, -s, c);
        transformed_uv = rotation_matrix * transformed_uv;
        transformed_uv += vec2(0.5);
    }
    
    // Offset
    transformed_uv += offset;
    
    return transformed_uv;
}

vec3 get_normal(float has_normal_tex, vec3 v_pos, vec3 v_nrm, vec2 v_uv, 
                texture2D normal_tex, sampler normal_smp, 
                vec2 normal_tex_offset, float normal_tex_rotation, vec2 normal_tex_scale, 
                float normal_map_scale) {
    if (has_normal_tex < 0.5) {
        return normalize(v_nrm);
    }
    
    // Apply texture transform only if non-default values exist
    vec2 transformed_uv = v_uv;
    bool has_transform = (length(normal_tex_offset) > 0.001 || 
                         abs(normal_tex_rotation) > 0.001 || 
                         abs(length(normal_tex_scale) - 1.414213) > 0.001); // length of (1,1) = sqrt(2)
    if (has_transform) {
        transformed_uv = apply_texture_transform(v_uv, normal_tex_offset, normal_tex_rotation, normal_tex_scale);
    }
    
    // Calculate TBN matrix using screen-space derivatives
    vec3 pos_dx = dFdx(v_pos);
    vec3 pos_dy = dFdy(v_pos);
    vec2 tex_dx = dFdx(transformed_uv);
    vec2 tex_dy = dFdy(transformed_uv);
    
    // Calculate tangent vector
    float det = tex_dx.s * tex_dy.t - tex_dy.s * tex_dx.t;
    vec3 t = (tex_dy.t * pos_dx - tex_dx.t * pos_dy) / det;
    
    // Gram-Schmidt orthogonalization
    vec3 ng = normalize(v_nrm);
    t = normalize(t - ng * dot(ng, t));
    vec3 b = cross(ng, t);
    mat3 tbn = mat3(t, b, ng);
    
    // Sample normal map with transformed UVs
    vec3 normal_sample = texture(sampler2D(normal_tex, normal_smp), transformed_uv).rgb;
    vec3 n = normal_sample * 2.0 - 1.0;
    
    // Apply normal map scale (controls strength of perturbation)
    // Scale 0.2 = subtle bump, 1.0 = full strength
    n.xy *= normal_map_scale;
    
    n = normalize(tbn * n);
    return n;
}

angular_info_t get_angular_info(vec3 point_to_light, vec3 normal, vec3 view) {
    // Standard one-letter names
    vec3 n = normalize(normal);           // Outward direction of surface point
    vec3 v = normalize(view);             // Direction from surface point to view
    vec3 l = normalize(point_to_light);     // Direction from surface point to light
    vec3 h = normalize(l + v);            // Direction of the vector between l and v

    float NdotL = clamp(dot(n, l), 0.0, 1.0);
    float NdotV = clamp(dot(n, v), 0.0, 1.0);
    float NdotH = clamp(dot(n, h), 0.0, 1.0);
    float LdotH = clamp(dot(l, h), 0.0, 1.0);
    float VdotH = clamp(dot(v, h), 0.0, 1.0);

    return angular_info_t(
        NdotL,
        NdotV,
        NdotH,
        LdotH,
        VdotH,
        vec3(0, 0, 0)
    );
}

// The following equation models the Fresnel reflectance term of the spec equation (aka F())
// Implementation of fresnel from [4], Equation 15
vec3 specular_reflection(material_info_t material_info, angular_info_t angular_info) {
    return material_info.reflectance0 + (material_info.reflectance90 - material_info.reflectance0) * pow(clamp(1.0 - angular_info.v_dot_h, 0.0, 1.0), 5.0);
}

// Geometry function (Schlick-GGX)
float geometry_schlick_ggx(float n_dot_v, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    
    float nom = n_dot_v;
    float denom = n_dot_v * (1.0 - k) + k;
    
    return nom / denom;
}

// Smith's method combines the geometry obstruction and shadowing
float geometry_smith(material_info_t material_info, angular_info_t angular_info) {
    float n_dot_v = angular_info.n_dot_v;
    float n_dot_l = angular_info.n_dot_l;
    float roughness = material_info.perceptual_roughness;
    
    float ggx2 = geometry_schlick_ggx(n_dot_v, roughness);
    float ggx1 = geometry_schlick_ggx(n_dot_l, roughness);
    
    return ggx1 * ggx2;
}

// The following equation(s) model the distribution of microfacet normals across the area being drawn (aka D())
// Implementation from "Average Irregularity Representation of a Roughened Surface for Ray Reflection" by T. S. Trowbridge, and K. P. Reitz
// Follows the distribution function recommended in the SIGGRAPH 2013 course notes from EPIC Games [1], Equation 3.
float microfacet_distribution(material_info_t material_info, angular_info_t angular_info) {
    float a = material_info.perceptual_roughness * material_info.perceptual_roughness;
    float a2 = a * a;
    float NdotH = angular_info.n_dot_h;
    float NdotH2 = NdotH * NdotH;
    
    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = M_PI * denom * denom;
    
    return nom / denom;
}

// Lambert lighting
// see https://seblagarde.wordpress.com/2012/01/08/pi-or-not-to-pi-in-game-lighting-equation/
vec3 diffuse(material_info_t material_info) {
    return material_info.diffuse_color / M_PI;
}

// Smith Joint GGX - proper visibility term
float visibility_occlusion(material_info_t material_info, angular_info_t angular_info) {
    float n_dot_l = angular_info.n_dot_l;
    float n_dot_v = angular_info.n_dot_v;
    float alpha_roughness_sq = material_info.alpha_roughness * material_info.alpha_roughness;

    float GGXV = n_dot_l * sqrt(n_dot_v * n_dot_v * (1.0 - alpha_roughness_sq) + alpha_roughness_sq);
    float GGXL = n_dot_v * sqrt(n_dot_l * n_dot_l * (1.0 - alpha_roughness_sq) + alpha_roughness_sq);
    float GGX = GGXV + GGXL;
    if (GGX > 0.0) {
        return 0.5 / GGX;
    }
    return 0.0;
}

vec3 get_point_shade(vec3 point_to_light, material_info_t material_info, vec3 normal, vec3 view) {
    angular_info_t angular_info = get_angular_info(point_to_light, normal, view);
    if ((angular_info.n_dot_l > 0.0) || (angular_info.n_dot_v > 0.0)) {
        // Calculate the shading terms for the microfacet specular shading model
        vec3 F = specular_reflection(material_info, angular_info);
        float Vis = visibility_occlusion(material_info, angular_info);
        float D = microfacet_distribution(material_info, angular_info);

        // Calculation of analytical lighting contribution
        vec3 diffuse_contrib = (1.0 - F) * diffuse(material_info);
        
        // Boost specular for metals to make them more visible and shiny
        float metallic = material_info.metallic;
        float spec_boost = mix(1.0, 2.5, metallic);  // 2.5x boost for metals (increased)
        vec3 spec_contrib = F * Vis * D * spec_boost;

        // Obtain final intensity as reflectance (BRDF) scaled by the energy of the light (cosine law)
        return angular_info.n_dot_l * (diffuse_contrib + spec_contrib);
    }
    return vec3(0.0, 0.0, 0.0);
}

