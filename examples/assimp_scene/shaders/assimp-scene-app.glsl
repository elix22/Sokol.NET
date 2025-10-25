@ctype mat4 mat44_t
@ctype vec3 vec3_t
@ctype vec4 vec4_t

@vs vs

const int MAX_BONES = 100;
const int MAX_BONE_INFLUENCE = 4;

layout(binding=0) uniform vs_params {
    mat4 projection;
    mat4 view;
    mat4 model;
    mat4 finalBonesMatrices[MAX_BONES];
};

in vec3 position;
in vec3 normal;
in vec4 color0;
in vec2 texcoord0;
in vec4 boneIds; 
in vec4 weights;

out vec3 world_pos;
out vec3 world_normal;
out vec4 color;
out vec2 uv;

void main() {
    vec4 totalPosition = vec4(0.0f);
    vec4 totalNormal = vec4(0.0f);
    bool hasValidBone = false;
    
    for(int i = 0 ; i < MAX_BONE_INFLUENCE ; i++)
    {
        if(int(boneIds[i]) == -1) 
            continue;
        if(int(boneIds[i]) >= MAX_BONES) 
        {
            totalPosition = vec4(position, 1.0);
            totalNormal = vec4(normal, 0.0);
            hasValidBone = true;
            break;
        }
        vec4 localPosition = finalBonesMatrices[int(boneIds[i])] * vec4(position, 1.0);
        vec4 localNormal = finalBonesMatrices[int(boneIds[i])] * vec4(normal, 0.0);
        totalPosition += localPosition * weights[i];
        totalNormal += localNormal * weights[i];
        hasValidBone = true;
    }
   
    // Handle static vertices (no bone influences)
    if (!hasValidBone) {
        totalPosition = vec4(position, 1.0);
        totalNormal = vec4(normal, 0.0);
    }
	
    vec4 worldPos = model * totalPosition;
    gl_Position = projection * view * worldPos;
    
    world_pos = worldPos.xyz;
    world_normal = normalize((model * totalNormal).xyz);
    color = color0;
    uv = texcoord0;
}
@end

@fs fs

const int MAX_LIGHTS = 4;
const int LIGHT_TYPE_DIRECTIONAL = 0;
const int LIGHT_TYPE_POINT = 1;
const int LIGHT_TYPE_SPOT = 2;

layout(binding=1) uniform fs_params {
    vec3 camera_pos;
    int num_lights;
    vec4 light_positions[MAX_LIGHTS];      // w component: light type
    vec4 light_directions[MAX_LIGHTS];     // w component: spot cutoff angle (cosine)
    vec4 light_colors[MAX_LIGHTS];         // w component: intensity
    vec4 light_params[MAX_LIGHTS];         // x: range, y: spot outer cutoff, z: unused, w: unused
    vec3 ambient_color;
    float ambient_intensity;
};

layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;

in vec3 world_pos;
in vec3 world_normal;
in vec4 color;
in vec2 uv;

out vec4 frag_color;

vec4 gamma(vec4 c) {
    float p = 1.0 / 2.2;
    return vec4(pow(c.xyz, vec3(p)), c.w);
}

vec3 calculate_directional_light(int idx, vec3 normal, vec3 view_dir, vec3 base_color) {
    vec3 light_dir = normalize(-light_directions[idx].xyz);
    vec3 n = normalize(normal);
    
    // Diffuse
    float diff = max(dot(n, light_dir), 0.0);
    vec3 diffuse = diff * base_color * light_colors[idx].rgb * light_colors[idx].w;
    
    // Specular (Blinn-Phong)
    vec3 half_dir = normalize(light_dir + view_dir);
    float spec = pow(max(dot(n, half_dir), 0.0), 32.0);
    vec3 specular = spec * light_colors[idx].rgb * light_colors[idx].w;
    
    return diffuse + specular;
}

vec3 calculate_point_light(int idx, vec3 normal, vec3 view_dir, vec3 base_color) {
    vec3 light_pos = light_positions[idx].xyz;
    vec3 light_dir = normalize(light_pos - world_pos);
    vec3 n = normalize(normal);
    
    // Attenuation
    float distance = length(light_pos - world_pos);
    float range = light_params[idx].x;
    float attenuation = 1.0 - smoothstep(0.0, range, distance);
    
    // Diffuse
    float diff = max(dot(n, light_dir), 0.0);
    vec3 diffuse = diff * base_color * light_colors[idx].rgb * light_colors[idx].w;
    
    // Specular (Blinn-Phong)
    vec3 half_dir = normalize(light_dir + view_dir);
    float spec = pow(max(dot(n, half_dir), 0.0), 32.0);
    vec3 specular = spec * light_colors[idx].rgb * light_colors[idx].w;
    
    return (diffuse + specular) * attenuation;
}

vec3 calculate_spot_light(int idx, vec3 normal, vec3 view_dir, vec3 base_color) {
    vec3 light_pos = light_positions[idx].xyz;
    vec3 light_dir = normalize(light_pos - world_pos);
    vec3 spot_dir = normalize(-light_directions[idx].xyz);
    vec3 n = normalize(normal);
    
    // Spot cone calculation
    float theta = dot(light_dir, spot_dir);
    float inner_cutoff = light_directions[idx].w; // cosine of inner angle
    float outer_cutoff = light_params[idx].y;      // cosine of outer angle
    float epsilon = inner_cutoff - outer_cutoff;
    float intensity = clamp((theta - outer_cutoff) / epsilon, 0.0, 1.0);
    
    if (intensity > 0.0) {
        // Attenuation
        float distance = length(light_pos - world_pos);
        float range = light_params[idx].x;
        float attenuation = 1.0 - smoothstep(0.0, range, distance);
        
        // Diffuse
        float diff = max(dot(n, light_dir), 0.0);
        vec3 diffuse = diff * base_color * light_colors[idx].rgb * light_colors[idx].w;
        
        // Specular (Blinn-Phong)
        vec3 half_dir = normalize(light_dir + view_dir);
        float spec = pow(max(dot(n, half_dir), 0.0), 32.0);
        vec3 specular = spec * light_colors[idx].rgb * light_colors[idx].w;
        
        return (diffuse + specular) * attenuation * intensity;
    }
    
    return vec3(0.0);
}

void main() {
    vec4 tex_color = texture(sampler2D(tex, smp), uv);
    
    // Base color
    vec3 base_color;
    if (tex_color.r > 0.99 && tex_color.g > 0.99 && tex_color.b > 0.99) {
        base_color = color.rgb;
    } else {
        base_color = tex_color.rgb * color.rgb;
    }
    
    // Ambient lighting
    vec3 ambient = ambient_color * ambient_intensity * base_color;
    
    // Calculate lighting
    vec3 view_dir = normalize(camera_pos - world_pos);
    vec3 lighting = vec3(0.0);
    
    for (int i = 0; i < num_lights && i < MAX_LIGHTS; i++) {
        int light_type = int(light_positions[i].w);
        
        if (light_type == LIGHT_TYPE_DIRECTIONAL) {
            lighting += calculate_directional_light(i, world_normal, view_dir, base_color);
        }
        else if (light_type == LIGHT_TYPE_POINT) {
            lighting += calculate_point_light(i, world_normal, view_dir, base_color);
        }
        else if (light_type == LIGHT_TYPE_SPOT) {
            lighting += calculate_spot_light(i, world_normal, view_dir, base_color);
        }
    }
    
    vec3 final_color = ambient + lighting;
    frag_color = gamma(vec4(final_color, 1.0));
}
@end

@program assimp vs fs