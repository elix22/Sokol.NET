// Animation support for glTF (skinning and morph targets)
// Adapted from Khronos glTF-Sample-Viewer
// Features controlled by SKINNING and MORPHING preprocessor defines

// NOTE: These uniforms must be defined in the main shader before including this file:
// - Morph weights in vs_params (binding=0): vec4 u_morphWeights[2]; (8 weights as 2 vec4s)
// - layout(binding=11) uniform texture2D u_jointsSampler_Tex; layout(binding=11) uniform sampler u_jointsSampler_Smp;
// - layout(binding=9) uniform texture2DArray u_MorphTargetsSampler_Tex; layout(binding=9) uniform sampler u_MorphTargetsSampler_Smp;
// Note: Slot 9 is shared with CharlieLUT - morphing and Charlie sheen rarely used together

#ifdef MORPHING
// Helper to get morph weight at index i (u_morphWeights is vec4[2])
float getMorphWeight(int i)
{
    // Unrolled for SPIRV-Cross compatibility (no dynamic vec4 indexing)
    if (i == 0) return u_morphWeights[0].x;
    if (i == 1) return u_morphWeights[0].y;
    if (i == 2) return u_morphWeights[0].z;
    if (i == 3) return u_morphWeights[0].w;
    if (i == 4) return u_morphWeights[1].x;
    if (i == 5) return u_morphWeights[1].y;
    if (i == 6) return u_morphWeights[1].z;
    if (i == 7) return u_morphWeights[1].w;
    return 0.0;
}
#endif

#ifdef SKINNING
// Helper function to get matrix from texture for skinning
mat4 getMatrixFromTexture(texture2D tex, sampler smp, int index)
{
    mat4 result = mat4(1);
    int texSize = textureSize(sampler2D(tex, smp), 0).x;
    int pixelIndex = index * 4;
    for (int i = 0; i < 4; ++i)
    {
        int x = (pixelIndex + i) % texSize;
        int y = (pixelIndex + i - x) / texSize; 
        result[i] = texelFetch(sampler2D(tex, smp), ivec2(x, y), 0);
    }
    return result;
}

// Get skinning matrix (blended from joint matrices)
mat4 getSkinningMatrix(vec4 joints_0, vec4 weights_0)
{
    mat4 skin;
    
    // Choose skinning method based on flag (0=texture-based, 1=uniform-based)
    if (use_uniform_skinning == 1)
    {
        // Uniform-based skinning: use finalBonesMatrices array
        skin = 
            weights_0.x * finalBonesMatrices[int(joints_0.x)] +
            weights_0.y * finalBonesMatrices[int(joints_0.y)] +
            weights_0.z * finalBonesMatrices[int(joints_0.z)] +
            weights_0.w * finalBonesMatrices[int(joints_0.w)];
    }
    else
    {
        // Texture-based skinning: use joint texture sampler
        skin = 
            weights_0.x * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.x) * 2) +
            weights_0.y * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.y) * 2) +
            weights_0.z * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.z) * 2) +
            weights_0.w * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.w) * 2);
    }
    
    if (skin == mat4(0)) { 
        return mat4(1); 
    }
    return skin;
}

// Get skinning normal matrix (for transforming normals)
mat4 getSkinningNormalMatrix(vec4 joints_0, vec4 weights_0)
{
    mat4 skin;
    
    // Choose skinning method based on flag (0=texture-based, 1=uniform-based)
    if (use_uniform_skinning == 1)
    {
        // Uniform-based skinning: use finalBonesMatrices array (same matrices for normals)
        skin = 
            weights_0.x * finalBonesMatrices[int(joints_0.x)] +
            weights_0.y * finalBonesMatrices[int(joints_0.y)] +
            weights_0.z * finalBonesMatrices[int(joints_0.z)] +
            weights_0.w * finalBonesMatrices[int(joints_0.w)];
    }
    else
    {
        // Texture-based skinning: use joint texture sampler (same even indices as positions)
        skin = 
            weights_0.x * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.x) * 2) +
            weights_0.y * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.y) * 2) +
            weights_0.z * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.z) * 2) +
            weights_0.w * getMatrixFromTexture(u_jointsSampler_Tex, u_jointsSampler_Smp, int(joints_0.w) * 2);
    }
    
    if (skin == mat4(0)) { 
        return mat4(1); 
    }
    return skin;
}
#endif

#ifdef MORPHING
// Helper to get morph target displacement from texture
vec4 getDisplacement(int vertexID, int targetIndex, int texSize)
{
    int x = vertexID % texSize;
    int y = (vertexID - x) / texSize; 
    return texelFetch(sampler2DArray(u_MorphTargetsSampler_Tex, u_MorphTargetsSampler_Smp), ivec3(x, y, targetIndex), 0);
}

// Get morph target position displacement
vec3 getTargetPosition(int vertexID, int morphTargetCount)
{
    vec3 pos = vec3(0);
    int texSize = textureSize(sampler2DArray(u_MorphTargetsSampler_Tex, u_MorphTargetsSampler_Smp), 0).x;
    
    for(int i = 0; i < morphTargetCount && i < 8; i++)
    {
        vec4 displacement = getDisplacement(vertexID, i, texSize);
        pos += getMorphWeight(i) * displacement.xyz;
    }
    
    return pos;
}

// Get morph target normal displacement
vec3 getTargetNormal(int vertexID, int morphTargetCount, int normalOffset)
{
    vec3 normal = vec3(0);
    int texSize = textureSize(sampler2DArray(u_MorphTargetsSampler_Tex, u_MorphTargetsSampler_Smp), 0).x;
    
    for(int i = 0; i < morphTargetCount && i < 8; i++)
    {
        vec3 displacement = getDisplacement(vertexID, normalOffset + i, texSize).xyz;
        normal += getMorphWeight(i) * displacement;
    }
    
    return normal;
}

// Get morph target tangent displacement
vec3 getTargetTangent(int vertexID, int morphTargetCount, int tangentOffset)
{
    vec3 tangent = vec3(0);
    int texSize = textureSize(sampler2DArray(u_MorphTargetsSampler_Tex, u_MorphTargetsSampler_Smp), 0).x;
    
    for(int i = 0; i < morphTargetCount && i < 8; i++)
    {
        vec3 displacement = getDisplacement(vertexID, tangentOffset + i, texSize).xyz;
        tangent += getMorphWeight(i) * displacement;
    }
    
    return tangent;
}
#endif
