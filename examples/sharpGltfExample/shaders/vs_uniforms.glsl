layout(binding=0, std140) uniform vs_params {
    layout(offset=0) highp mat4 model;              // offset 0, size 64
    layout(offset=64) highp mat4 view_proj;         // offset 64, size 64
    layout(offset=128) highp vec3 eye_pos;          // offset 128, size 12 (but std140 pads to 16)
#ifdef SKINNING
    layout(offset=144) highp mat4 finalBonesMatrices[MAX_BONES];  // offset 144 (128+16)
#endif
};