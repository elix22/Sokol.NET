
struct material_info_t {
    float perceptual_roughness;     // roughness value, as authored by the model creator (input to shader)
    vec3 reflectance0;              // full reflectance color (normal incidence angle)
    float alpha_roughness;          // roughness mapped to a more linear change in the roughness (proposed by [2])
    vec3 diffuse_color;             // color contribution from diffuse lighting
    vec3 reflectance90;             // reflectance color at grazing angle
    vec3 specular_color;            // color contribution from specular lighting
    float metallic;                 // metallic value (0 = dielectric, 1 = metal)
};

struct angular_info_t {
    float n_dot_l;                // cos angle between normal and light direction
    float n_dot_v;                // cos angle between normal and view direction
    float n_dot_h;                // cos angle between normal and half vector
    float l_dot_h;                // cos angle between light direction and half vector
    float v_dot_h;                // cos angle between view direction and half vector
    vec3 padding;
};
