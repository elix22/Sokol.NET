//------------------------------------------------------------------------------
// Bloom Post-Processing Shaders
//------------------------------------------------------------------------------
// This file contains all bloom effect shaders including:
// - Fullscreen quad vertex shader
// - Bright pass extraction (HDR threshold)
// - Gaussian blur (horizontal and vertical passes)
// - Final bloom composite

// Fullscreen quad vertex shader for post-processing  
@vs vs_fullscreen
@msl_options flip_vert_y
@hlsl_options flip_vert_y

layout(location=0) in vec2 position;
out vec2 uv;

void main() {
    gl_Position = vec4(position, 0.0, 1.0);
    // Convert from NDC [-1,1] to UV [0,1]
    uv = (position + 1.0) * 0.5;
}
@end

// Bright pass extraction - extracts pixels above brightness threshold
@fs fs_bright_pass
layout(binding=0) uniform texture2D scene_texture;
layout(binding=0) uniform sampler scene_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec4 color = texture(sampler2D(scene_texture, scene_sampler), uv);
    
    // For HDR/emissive materials, check max color component instead of just luminance
    // This captures bright emissive colors better (e.g., bright blue emissive)
    float brightness = max(max(color.r, color.g), color.b);
    
    // Smooth threshold - gradually fade in bloom contribution
    // knee controls how soft the transition is (0.1 = very soft)
    float knee = 0.1;
    float soft = smoothstep(brightness_threshold - knee, brightness_threshold + knee, brightness);
    
    // Only keep pixels that pass the threshold, preserve their full brightness
    frag_color = vec4(color.rgb * soft, 1.0);
}
@end

// Gaussian blur horizontal pass
@fs fs_blur_horizontal
layout(binding=0) uniform texture2D input_texture;
layout(binding=0) uniform sampler input_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec3 color = vec3(0.0);
    
    // 9-tap Gaussian blur weights
    float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    
    // Sample center
    color += texture(sampler2D(input_texture, input_sampler), uv).rgb * weights[0];
    
    // Sample both directions
    for (int i = 1; i < 5; i++) {
        float offset = float(i) * texel_size.x;
        color += texture(sampler2D(input_texture, input_sampler), uv + vec2(offset, 0.0)).rgb * weights[i];
        color += texture(sampler2D(input_texture, input_sampler), uv - vec2(offset, 0.0)).rgb * weights[i];
    }
    
    frag_color = vec4(color, 1.0);
}
@end

// Gaussian blur vertical pass
@fs fs_blur_vertical
layout(binding=0) uniform texture2D input_texture;
layout(binding=0) uniform sampler input_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec3 color = vec3(0.0);
    
    // 9-tap Gaussian blur weights
    float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
    
    // Sample center
    color += texture(sampler2D(input_texture, input_sampler), uv).rgb * weights[0];
    
    // Sample both directions
    for (int i = 1; i < 5; i++) {
        float offset = float(i) * texel_size.y;
        color += texture(sampler2D(input_texture, input_sampler), uv + vec2(0.0, offset)).rgb * weights[i];
        color += texture(sampler2D(input_texture, input_sampler), uv - vec2(0.0, offset)).rgb * weights[i];
    }
    
    frag_color = vec4(color, 1.0);
}
@end

// Final composite - combines original scene with bloom
@fs fs_bloom_composite
layout(binding=0) uniform texture2D scene_texture;
layout(binding=1) uniform texture2D bloom_texture;
layout(binding=0) uniform sampler scene_sampler;
layout(binding=1) uniform sampler bloom_sampler;

layout(binding=0) uniform bloom_params {
    float brightness_threshold;
    float bloom_intensity;
    vec2 texel_size;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec3 scene_color = texture(sampler2D(scene_texture, scene_sampler), uv).rgb;
    vec3 bloom_color = texture(sampler2D(bloom_texture, bloom_sampler), uv).rgb;
    
    // Additive blending with intensity control
    vec3 final_color = scene_color + bloom_color * bloom_intensity;
    
    frag_color = vec4(final_color, 1.0);
}
@end

// Bloom shader programs
@program bright_pass vs_fullscreen fs_bright_pass
@program blur_horizontal vs_fullscreen fs_blur_horizontal  
@program blur_vertical vs_fullscreen fs_blur_vertical
@program bloom_composite vs_fullscreen fs_bloom_composite
