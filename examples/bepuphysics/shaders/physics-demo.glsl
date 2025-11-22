@ctype mat4 mat44_t
@ctype vec3 vec3_t


// Vertex shader with smooth shading (for spheres)
@vs vs_smooth
layout(binding=0) uniform vs_params {
    mat4 vp;
};

layout(location=0) in vec3 position;
layout(location=1) in vec3 normal;

// Per-instance attributes
layout(location=2) in vec4 inst_model_0;
layout(location=3) in vec4 inst_model_1;
layout(location=4) in vec4 inst_model_2;
layout(location=5) in vec4 inst_model_3;
layout(location=6) in vec3 inst_color;

out vec3 world_normal;
out vec3 world_pos;
out vec3 color;

void main() {
    // Reconstruct model matrix from instance data
    mat4 model = mat4(inst_model_0, inst_model_1, inst_model_2, inst_model_3);
    
    vec4 world_position = model * vec4(position, 1.0);
    gl_Position = vp * world_position;
    // For correct normal transformation with non-uniform scale, use transpose of inverse
    // This is mathematically correct for all types of transformations
    mat3 normal_matrix = mat3(transpose(inverse(model)));
    world_normal = normalize(normal_matrix * normal);
    world_pos = world_position.xyz;
    color = inst_color;
}
@end


// Fragment shader for smooth shading
@fs fs_smooth
in vec3 world_normal;
in vec3 world_pos;
in vec3 color;
out vec4 frag_color;

layout(binding=1) uniform fs_params {
    vec3 light_dir;
    vec3 view_pos;
};

void main() {
    vec3 N = normalize(world_normal);
    vec3 L = normalize(light_dir);
    vec3 V = normalize(view_pos - world_pos);
    vec3 H = normalize(L + V);
    
    // Ambient
    vec3 ambient = 0.2 * color;
    
    // Diffuse
    float diff = max(dot(N, L), 0.0);
    vec3 diffuse = diff * color;
    
    // Specular
    float spec = pow(max(dot(N, H), 0.0), 32.0);
    vec3 specular = vec3(0.3) * spec;
    
    vec3 result = ambient + diffuse + specular;
    frag_color = vec4(result, 1.0);
}
@end

@program physics_demo_smooth vs_smooth fs_smooth
