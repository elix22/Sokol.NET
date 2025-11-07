@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4
@ctype vec3 System.Numerics.Vector3

@vs vs_cubmap

layout(binding=0) uniform vs_cubemap_params {
 mat4 u_ViewProjectionMatrix;
 mat4 u_EnvRotation;
};

in vec3 a_position;
out vec3 v_TexCoords;


void main()
{
    v_TexCoords = mat3(u_EnvRotation) * a_position;
    mat4 mat = u_ViewProjectionMatrix;
    mat[3] = vec4(0.0, 0.0, 0.0, 0.1);
    vec4 pos = mat * vec4(a_position, 1.0);
    gl_Position = pos.xyww;
}

@end

@fs fs_cubemap

precision highp float;
@include tonemapping.glsl

layout(binding=1) uniform fs_cubemap_params {
 float u_EnvIntensity;
 float u_EnvBlurNormalized;
 int u_MipCount;
 int u_LinearOutput;
};

out vec4 FragColor;
in vec3 v_TexCoords;

layout(binding=0) uniform textureCube u_GGXEnvTexture;
layout(binding=0) uniform sampler u_GGXEnvSampler;


void main()
{
    vec4 color  = textureLod(samplerCube(u_GGXEnvTexture, u_GGXEnvSampler), v_TexCoords, u_EnvBlurNormalized * float(u_MipCount - 1));
    color.rgb *= u_EnvIntensity;
    color.a = 1.0;

    if(u_LinearOutput == 1)
    {
        FragColor = color.rgba;
    }
    else
    {
        FragColor = vec4(toneMap(color.rgb), color.a);
    }

}

@end

@program cubemap_program vs_cubmap fs_cubemap