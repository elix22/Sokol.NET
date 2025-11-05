// ============================================================================
// Lighting Functions for PBR Rendering
// ============================================================================
// This file contains all lighting-related functions including:
// - Light attenuation calculation
// - Per-light PBR shading
// - Multi-light accumulation

// Calculate distance-based attenuation for point and spot lights
float get_range_attenuation(float range, float distance) {
    if (range < 0.0) {
        return 1.0;
    }
    return max(min(1.0 - pow(distance / range, 4.0), 1.0), 0.0) / pow(distance, 2.0);
}

// Calculate PBR shading contribution from a single light source
// Handles directional, point, and spot lights
vec3 apply_single_light(int light_idx, material_info_t material_info, vec3 normal, vec3 view, vec3 frag_pos) {
    int light_type = int(light_positions[light_idx].w);
    vec3 light_pos = light_positions[light_idx].xyz;
    vec3 light_dir = light_directions[light_idx].xyz;
    vec3 light_color = light_colors[light_idx].rgb;
    float light_intensity = light_colors[light_idx].w;
    float light_range = light_params_data[light_idx].x;
    
    vec3 point_to_light;
    float attenuation = 1.0;
    
    if (light_type == LIGHT_TYPE_DIRECTIONAL) {
        // Directional light
        point_to_light = normalize(-light_dir);
        attenuation = 1.0;
    }
    else if (light_type == LIGHT_TYPE_POINT) {
        // Point light
        point_to_light = light_pos - frag_pos;
        float distance = length(point_to_light);
        attenuation = get_range_attenuation(light_range, distance);
        point_to_light = normalize(point_to_light);
    }
    else if (light_type == LIGHT_TYPE_SPOT) {
        // Spot light
        point_to_light = light_pos - frag_pos;
        float distance = length(point_to_light);
        point_to_light = normalize(point_to_light);
        
        // Spot cone calculation
        vec3 spot_dir = normalize(-light_dir);
        float theta = dot(-point_to_light, spot_dir);
        float inner_cutoff = light_directions[light_idx].w;  // cosine of inner angle
        float outer_cutoff = light_params_data[light_idx].y; // cosine of outer angle
        float epsilon = inner_cutoff - outer_cutoff;
        float spot_intensity = clamp((theta - outer_cutoff) / epsilon, 0.0, 1.0);
        
        attenuation = get_range_attenuation(light_range, distance) * spot_intensity;
    }
    else {
        return vec3(0.0); // Unknown light type
    }
    
    // Calculate PBR shading with proper attenuation
    vec3 shade = get_point_shade(point_to_light, material_info, normal, view);
    return attenuation * light_intensity * light_color * shade;
}

// Apply all active lights and accumulate their contributions
vec3 apply_all_lights(material_info_t material_info, vec3 normal, vec3 view, vec3 frag_pos) {
    vec3 total_light = vec3(0.0);
    
    for (int i = 0; i < num_lights && i < MAX_LIGHTS; i++) {
        total_light += apply_single_light(i, material_info, normal, view, frag_pos);
    }
    
    return total_light;
}
