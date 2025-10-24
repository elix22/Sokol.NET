@ctype mat4 mat44_t

@vs vs

const int MAX_BONES = 100;
const int MAX_BONE_INFLUENCE = 4;

layout(binding=0) uniform vs_params {
    uniform mat4 projection;
    uniform mat4 view;
    uniform mat4 model;
    mat4 finalBonesMatrices[MAX_BONES];
};

in vec3 position;
in vec4 color0;
in vec2 texcoord0;
in vec4 boneIds; 
in vec4 weights;

layout(location=0) out vec4 color;
layout(location=1) out vec2 uv;


void main() {

    vec4 totalPosition = vec4(0.0f);
    bool hasValidBone = false;
    
    for(int i = 0 ; i < MAX_BONE_INFLUENCE ; i++)
    {
        if(int(boneIds[i]) == -1) 
            continue;
        if(int(boneIds[i]) >=MAX_BONES) 
        {
            totalPosition = vec4(position,1.0);
            hasValidBone = true;
            break;
        }
        vec4 localPosition = finalBonesMatrices[int(boneIds[i])] * vec4(position,1.0);
        totalPosition += localPosition * weights[i];
        hasValidBone = true;
   }
   
    // Handle static vertices (no bone influences)
    if (!hasValidBone) {
        totalPosition = vec4(position, 1.0);
    }
	
    gl_Position =  projection * view * model * totalPosition;
    color = color0;
    uv = texcoord0;
}
@end

@fs fs
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;
layout(location=0) in vec4 color;
layout(location=1) in vec2 uv;
out vec4 frag_color;

void main() {
    frag_color = texture(sampler2D(tex, smp), uv) * color;
}
@end

@program assimp vs fs