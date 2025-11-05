layout(location=0) in vec3 position;  // FIXED: Was vec4, should be vec3 to match Vertex struct!
layout(location=1) in vec3 normal;
layout(location=2) in vec4 color;
layout(location=3) in vec2 texcoord;
layout(location=4) in vec4 boneIds;  // Changed from uvec4 for WebGL compatibility
layout(location=5) in vec4 weights;


out vec3 v_pos;
out vec3 v_nrm;
out vec4 v_color;
out vec2 v_uv;
out vec3 v_eye_pos;
