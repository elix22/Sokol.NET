# Sokol.NET Shader Programming Guide

Complete guide to writing cross-platform shaders for Sokol.NET using sokol-shdc and annotated GLSL.

## Table of Contents

- [Overview](#overview)
- [Getting Started](#getting-started)
- [Shader Language Basics](#shader-language-basics)
- [Shader Tags Reference](#shader-tags-reference)
- [Uniform Buffers](#uniform-buffers)
- [Textures and Samplers](#textures-and-samplers)
- [Vertex Attributes](#vertex-attributes)
- [Advanced Features](#advanced-features)
- [Platform-Specific Code](#platform-specific-code)
- [Code Reusability](#code-reusability)
- [Best Practices](#best-practices)
- [Common Patterns](#common-patterns)
- [Limitations and Considerations](#limitations-and-considerations)
- [Troubleshooting](#troubleshooting)

---

## Overview

Sokol.NET uses **sokol-shdc** (sokol shader compiler) to convert annotated GLSL shaders into C# files containing platform-specific shader code. This approach provides:

- **Write Once, Run Everywhere**: Write shaders in GLSL 450, compile to HLSL/Metal/GLSL
- **Type-Safe C# Bindings**: Generated structs for uniforms with proper layout
- **Compile-Time Validation**: Catch shader errors during build, not at runtime
- **Zero Runtime Overhead**: All shader variants embedded in your assembly
- **Multiple Backend Support**: Direct3D 11, Metal, OpenGL, OpenGL ES, WebGL

### Compilation Flow

```
Your Shader (.glsl)
     ↓
sokol-shdc compiler
     ↓
Generated C# File (.cs)
     ├─ HLSL bytecode (Windows)
     ├─ Metal source (macOS/iOS)
     ├─ GLSL source (Linux/Android/Web)
     └─ Type-safe C# structs
     ↓
Compiled into your .NET assembly
```

---

## Getting Started

### Basic Shader Structure

Every shader file must contain at least:
1. A vertex shader block (`@vs ... @end`)
2. A fragment shader block (`@fs ... @end`)
3. A program declaration (`@program name vs fs`)

**Minimal Example** (`simple.glsl`):

```glsl
@vs vs
in vec4 position;

void main() {
    gl_Position = position;
}
@end

@fs fs
out vec4 frag_color;

void main() {
    frag_color = vec4(1.0, 0.0, 0.0, 1.0);
}
@end

@program simple vs fs
```

### Compilation

Shaders are compiled automatically during build using MSBuild targets defined in `Directory.Build.props`. The compilation process:

1. **Detects your platform** (Windows, macOS, Linux, Android, iOS, Web)
2. **Selects appropriate shader language**:
   - Windows → `hlsl5` (Direct3D 11)
   - macOS → `metal_macos` (Metal)
   - iOS → `metal_ios` (Metal)
   - Linux → `glsl430` (OpenGL 4.3)
   - Android → `glsl300es` (OpenGL ES 3.0)
   - Web → `glsl300es` (WebGL 2.0)
3. **Compiles shaders** to platform-specific output folder
4. **Generates C# files** with embedded shader code

**MSBuild Target** (from `Directory.Build.props`):
```xml
<ItemGroup>
  <ShaderFiles Include="shaders/**/*.glsl" />
</ItemGroup>

<Target Name="CompileShaders" BeforeTargets="BeforeBuild"
        Inputs="@(ShaderFiles)"
        Outputs="@(ShaderFiles -> 'shaders/compiled/$(HostShaderFolder)/%(Filename)-shader.cs')">
  <MakeDir Directories="shaders/compiled/$(HostShaderFolder)" />
  <Message Text="Compiling shader: %(ShaderFiles.Identity)" Importance="high" />
  <Exec Command="&quot;$(SokolShdcPath)&quot; --input &quot;%(ShaderFiles.Identity)&quot; --output shaders/compiled/$(HostShaderFolder)/%(Filename)-shader.cs --slang $(ShaderSlang) -f sokol_csharp" />
</Target>
```

**Project Configuration** (from `.csproj`):
```xml
<!-- Desktop projects: cube.csproj -->
<PropertyGroup>
  <ShaderSlang Condition="'$(IsAndroid)'=='true'">glsl300es</ShaderSlang>
  <ShaderSlang Condition="'$(IsOSX)'=='true'">metal_macos</ShaderSlang>
  <ShaderSlang Condition="'$(IsWindows)'=='true'">hlsl5</ShaderSlang>
  <ShaderSlang Condition="'$(IsLinux)'=='true'">glsl430</ShaderSlang>
</PropertyGroup>

<!-- Web projects: cubeweb.csproj -->
<PropertyGroup>
  <HostShaderFolder>web</HostShaderFolder>
  <ShaderSlang>glsl300es</ShaderSlang>
</PropertyGroup>
```

**Output Structure**:
```
your_project/
├── shaders/
│   ├── simple.glsl              # Your source shader
│   └── compiled/
│       ├── windows/
│       │   └── simple-shader.cs # HLSL bytecode
│       ├── osx/
│       │   └── simple-shader.cs # Metal source
│       ├── linux/
│       │   └── simple-shader.cs # GLSL source
│       ├── android/
│       │   └── simple-shader.cs # GLSL ES source
│       ├── ios/
│       │   └── simple-shader.cs # Metal source
│       └── web/
│           └── simple-shader.cs # WebGL source
```

The build system automatically includes only the platform-specific compiled shader for your target platform, excluding others.

**Manual Shader Compilation**:

You can trigger shader compilation manually without building the entire project:

```bash
# Compile shaders for host platform (Windows/macOS/Linux)
dotnet build cube.csproj -t:CompileShaders

# Compile shaders for Web
dotnet build cubeweb.csproj -t:CompileShaders

# Compile shaders for Android (requires Android defines)
dotnet build cube.csproj -t:CompileShaders -p:DefineConstants=__ANDROID__

# Compile shaders for iOS (requires iOS defines)
dotnet build cube.csproj -t:CompileShaders -p:DefineConstants=__IOS__
```

**Customizing Shader Compilation**:

You can customize the shader compilation in your `Directory.Build.props`. For example, the `GltfViewer` project compiles multiple shader variants with different defines:

```xml
<ItemGroup>
  <ShaderFiles Include="shaders/bloom.glsl" />
  <ShaderFiles Include="shaders/cubemap.glsl" />
  <ShaderFiles Include="shaders/pbr.glsl" />
  <SkinningMorphingShaderFiles Include="shaders/pbr.glsl" />
</ItemGroup>

<Target Name="CompileShaders" BeforeTargets="BeforeBuild"
        Inputs="@(ShaderFiles)"
        Outputs="shaders/compiled/$(HostShaderFolder)/.shader_timestamp">
  <MakeDir Directories="shaders/compiled/$(HostShaderFolder)" />
  
  <!-- Standard shaders -->
  <Exec Command="&quot;$(SokolShdcPath)&quot; --input &quot;%(ShaderFiles.Identity)&quot; --reflection --output shaders/compiled/$(HostShaderFolder)/%(ShaderFiles.Filename)-shader.cs --slang $(ShaderSlang) -f sokol_csharp" />
  
  <!-- Shader variants with different defines -->
  <Exec Command="&quot;$(SokolShdcPath)&quot; --input &quot;%(SkinningMorphingShaderFiles.Identity)&quot; --defines &quot;SKINNING&quot; --module &quot;skinning&quot; --reflection --output shaders/compiled/$(HostShaderFolder)/%(SkinningMorphingShaderFiles.Filename)-shader-skinning.cs --slang $(ShaderSlang) -f sokol_csharp" />
  
  <Exec Command="&quot;$(SokolShdcPath)&quot; --input &quot;%(SkinningMorphingShaderFiles.Identity)&quot; --defines &quot;MORPHING&quot; --module &quot;morphing&quot; --reflection --output shaders/compiled/$(HostShaderFolder)/%(SkinningMorphingShaderFiles.Filename)-shader-morphing.cs --slang $(ShaderSlang) -f sokol_csharp" />
  
  <Exec Command="&quot;$(SokolShdcPath)&quot; --input &quot;%(SkinningMorphingShaderFiles.Identity)&quot; --defines &quot;SKINNING:MORPHING&quot; --module &quot;skinning_morphing&quot; --reflection --output shaders/compiled/$(HostShaderFolder)/%(SkinningMorphingShaderFiles.Filename)-shader-skinning-morphing.cs --slang $(ShaderSlang) -f sokol_csharp" />
  
  <!-- Create timestamp to track completion -->
  <Touch Files="shaders/compiled/$(HostShaderFolder)/.shader_timestamp" AlwaysCreate="true" />
</Target>
```

**Advanced sokol-shdc Options**:

The shader compiler supports several useful options:

```bash
# Enable reflection (generates metadata about uniforms, textures)
--reflection

# Define preprocessor symbols (for conditional compilation)
--defines "SKINNING:MORPHING"

# Custom module name (generates different C# namespace)
--module "skinning"

# Specify output format
--format sokol_csharp

# Multiple shader languages in one command
--slang "glsl430:hlsl5:metal_macos"
```

**Direct sokol-shdc Usage** (bypassing MSBuild):
```bash
# Get the sokol-shdc path (platform-specific)
# macOS ARM: tools/bin/osx/arm64/sokol-shdc
# macOS Intel: tools/bin/osx/x64/sokol-shdc
# Linux: tools/bin/linux/x64/sokol-shdc
# Windows: tools/bin/windows/x64/sokol-shdc.exe

# Example: Compile for Windows with reflection
sokol-shdc --input shaders/simple.glsl \
  --output shaders/compiled/windows/simple-shader.cs \
  --slang hlsl5 \
  --format sokol_csharp \
  --reflection
```

---

## Shader Language Basics

### GLSL Version

Write shaders in **GLSL 450** (Vulkan-style GLSL) with:
- Explicit bindings: `layout(binding=N)`
- Separate texture and sampler objects
- Modern shader features

When using sokol-shdc, you write GLSL 450 and the compiler cross-compiles to backend-specific shader languages automatically. However, understanding the target requirements helps when debugging or writing backend-specific code:

**Backend-Specific GLSL Versions:**

| Backend | Required Version | Storage Buffers | Compute Shaders |
|---------|-----------------|-----------------|-----------------|
| Desktop OpenGL | `#version 410` | `#version 430` | `#version 430` |
| OpenGL ES 3.0 / WebGL 2.0 | `#version 300 es` | `#version 310 es` | `#version 310 es` |
| Direct3D 11 | HLSL 4.0 / 5.0 | ✓ (via shader model) | ✓ |
| Metal | MSL metal-1.1 | ✓ | ✓ |
| WebGPU | WGSL | ✓ | ✓ |

**Note:** These version requirements are handled automatically by sokol-shdc. You only need to be aware of them when:
- Writing backend-specific shader code manually
- Debugging cross-compilation issues
- Understanding platform limitations

### Shader Stages

```glsl
@vs vertex_shader_name
// Vertex shader code
@end

@fs fragment_shader_name
// Fragment shader code
@end

@cs compute_shader_name
// Compute shader code (OpenGL 4.3+, GLES 3.1+)
@end

@program program_name vertex_shader_name fragment_shader_name
```

---

## Shader Tags Reference

### Core Tags

#### `@vs` - Vertex Shader Block
```glsl
@vs my_vertex_shader
layout(binding=0) uniform vs_params {
    mat4 mvp;
};

in vec4 position;
in vec4 color;
out vec4 v_color;

void main() {
    gl_Position = mvp * position;
    v_color = color;
}
@end
```

#### `@fs` - Fragment Shader Block
```glsl
@fs my_fragment_shader
in vec4 v_color;
out vec4 frag_color;

void main() {
    frag_color = v_color;
}
@end
```

#### `@program` - Shader Program Declaration
```glsl
@program my_program my_vertex_shader my_fragment_shader
```

Creates a shader program that can be used with `sg_make_shader()`.

### Meta Tags

#### `@ctype` - C# Type Mapping
```glsl
@ctype mat4 System.Numerics.Matrix4x4
@ctype vec4 System.Numerics.Vector4
@ctype vec3 System.Numerics.Vector3
@ctype vec2 System.Numerics.Vector2
```

Maps GLSL types to C# types in generated structs. Common mappings:
- `mat4` → `Matrix4x4` (System.Numerics or custom)
- `vec4` → `Vector4`
- `vec3` → `Vector3`
- `vec2` → `Vector2`

#### `@block` / `@end` - Reusable Code Blocks
```glsl
@block lighting_functions
vec3 calculate_lighting(vec3 normal, vec3 light_dir) {
    float ndotl = max(dot(normal, light_dir), 0.0);
    return vec3(ndotl);
}
@end

@vs vs
@include_block lighting_functions
// Can now use calculate_lighting()
@end
```

#### `@include_block` - Include Code Block
```glsl
@vs my_vertex_shader
@include_block shared_functions
// Code from shared_functions block is inserted here
@end
```

#### `@module` - Module Name
```glsl
@module my_shaders
// Generates: namespace my_shaders { ... }
```

### Advanced Tags

#### `@glsl_options` - GLSL-Specific Options
```glsl
@vs vs_fsq
@glsl_options flip_vert_y

in vec2 pos;
void main() {
    gl_Position = vec4(pos*2.0-1.0, 0.5, 1.0);
}
@end
```

Options:
- `flip_vert_y`: Flips vertex Y coordinate for fullscreen quads

#### `@header` - Custom Header Code
```glsl
@header
// Custom C# code injected at top of generated file
@end
```

---

## Uniform Buffers

### Basic Uniform Buffer

```glsl
layout(binding=0) uniform vs_params {
    mat4 mvp;
    vec3 light_dir;
};
```

**Generated C# Struct**:
```csharp
[StructLayout(LayoutKind.Sequential, Pack=1)]
public struct VsParams
{
    public Matrix4x4 Mvp;
    public Vector3 LightDir;
}
```

### Multiple Uniform Buffers

```glsl
// Vertex shader uniforms
layout(binding=0) uniform vs_params {
    mat4 model;
    mat4 view_proj;
};

// Fragment shader uniforms
layout(binding=0) uniform fs_params {
    vec4 color;
    float roughness;
};
```

**Usage in C#**:
```csharp
var vsParams = new VsParams {
    Model = modelMatrix,
    ViewProj = viewProjMatrix
};
sg_apply_uniforms(UB_vs_params, SG_RANGE(ref vsParams));

var fsParams = new FsParams {
    Color = new Vector4(1, 0, 0, 1),
    Roughness = 0.5f
};
sg_apply_uniforms(UB_fs_params, SG_RANGE(ref fsParams));
```

### Uniform Buffer Layout (std140)

For proper alignment, use `std140` layout:

```glsl
layout(binding=0, std140) uniform vs_params {
    mat4 model;              // offset 0, size 64
    mat4 view_proj;          // offset 64, size 64
    vec3 eye_pos;            // offset 128, size 12 (padded to 16)
    float time;              // offset 144
    vec4 params[10];         // offset 160, array of vec4
};
```

**Alignment Rules**:
- `vec3` padded to 16 bytes (like `vec4`)
- Arrays start at 16-byte boundaries
- Matrices are 4x `vec4` (64 bytes)

### std140 vs Native Uniform Layout

Sokol-gfx supports two uniform buffer layout modes:

**std140 Layout (Recommended for Cross-Platform)**:
- **Use when**: Writing cross-platform code that needs to work on all backends
- **Advantages**: Guaranteed consistent layout across D3D11, Metal, OpenGL, WebGPU
- **Restrictions**: Limited to specific types: `float`, `vec2/3/4`, `int`, `ivec2/3/4`, `mat4`
- **Arrays**: Only allowed for `vec4`, `ivec4`, and `mat4`
- **Declaration**: `layout(binding=0, std140) uniform my_params { ... };`

**Native Layout**:
- **Use when**: Targeting a single backend (D3D11 or Metal only)
- **Advantages**: Can use any uniform types, potentially better performance
- **Disadvantages**: Layout differs between backends, not portable
- **Declaration**: `layout(binding=0) uniform my_params { ... };` (omit std140)

**Alignment Requirements for std140**:

| Type | Alignment | Size | Notes |
|------|-----------|------|-------|
| `float` | 4 bytes | 4 bytes | |
| `vec2` | 8 bytes | 8 bytes | |
| `vec3` | **16 bytes** | 12 bytes | Padded to 16! |
| `vec4` | 16 bytes | 16 bytes | |
| `int` | 4 bytes | 4 bytes | |
| `ivec2` | 8 bytes | 8 bytes | |
| `ivec3` | **16 bytes** | 12 bytes | Padded to 16! |
| `ivec4` | 16 bytes | 16 bytes | |
| `mat4` | 16 bytes | 64 bytes | 4× vec4 |
| `vec4[]` | 16 bytes | 16 × count | Array elements |

**Note:** When using sokol-shdc, uniform buffer layouts are handled automatically and the generated C# structs will have the correct layout. These details are primarily useful when manually defining uniform blocks or debugging alignment issues.

---

## Textures and Samplers

### Separate Texture and Sampler (Sokol Style)

```glsl
@fs fs
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;

in vec2 uv;
out vec4 frag_color;

void main() {
    // Combine texture and sampler with sampler2D() constructor
    frag_color = texture(sampler2D(tex, smp), uv);
}
@end
```

**Usage in C#**:
```csharp
sg_bindings bind = new sg_bindings
{
    views = {
        [0] = sg_make_view(new sg_view_desc
        {
            texture = { image = myTexture }
        })
    },
    samplers = { [0] = mySampler }
};
sg_apply_bindings(in bind);
```

### Multiple Textures

```glsl
@fs fs
layout(binding=0) uniform texture2D tex_albedo;
layout(binding=1) uniform texture2D tex_normal;
layout(binding=2) uniform texture2D tex_roughness;
layout(binding=0) uniform sampler smp;

in vec2 uv;
out vec4 frag_color;

void main() {
    vec3 albedo = texture(sampler2D(tex_albedo, smp), uv).rgb;
    vec3 normal = texture(sampler2D(tex_normal, smp), uv).rgb;
    float roughness = texture(sampler2D(tex_roughness, smp), uv).r;
    
    // Material calculations...
    frag_color = vec4(albedo, 1.0);
}
@end
```

### Texture Arrays

```glsl
@fs fs
layout(binding=0) uniform texture2DArray tex_array;
layout(binding=0) uniform sampler smp;

in vec2 uv;
in float layer;
out vec4 frag_color;

void main() {
    frag_color = texture(sampler2DArray(tex_array, smp), vec3(uv, layer));
}
@end
```

### Cubemap Textures

```glsl
@fs fs
layout(binding=0) uniform textureCube env_map;
layout(binding=0) uniform sampler smp;

in vec3 world_normal;
out vec4 frag_color;

void main() {
    vec3 env_color = texture(samplerCube(env_map, smp), world_normal).rgb;
    frag_color = vec4(env_color, 1.0);
}
@end
```

### Unfilterable Float Textures

Some mobile devices (especially iOS) cannot perform linear filtering on 32-bit float textures. For these cases, use the `@image_sample_type` and `@sampler_type` annotations:

**Supported unfilterable formats:**
- `SG_PIXELFORMAT_R32F`
- `SG_PIXELFORMAT_RG32F`
- `SG_PIXELFORMAT_RGBA32F`

**Example (sampling skinning matrices from RGBA32F texture in vertex shader):**

```glsl
@vs vs
// Annotate texture as unfilterable_float
@image_sample_type joint_tex unfilterable_float
layout(binding=0) uniform texture2D joint_tex;

// Annotate sampler as nonfiltering
@sampler_type smp nonfiltering
layout(binding=0) uniform sampler smp;

in vec4 position;
out vec4 v_position;

void main() {
    // Read skinning matrix from float texture
    vec4 mat_row0 = texture(sampler2D(joint_tex, smp), vec2(0.0, 0.0));
    // ... use matrix data
    gl_Position = position;
}
@end

@fs fs
out vec4 frag_color;
void main() {
    frag_color = vec4(1.0);
}
@end

@program skinned vs fs
```

**Compatibility Rules:**

| Image Sample Type | Compatible Sampler Types |
|------------------|-------------------------|
| `SG_IMAGESAMPLETYPE_FLOAT` | `SG_SAMPLERTYPE_FILTERING` or `SG_SAMPLERTYPE_NONFILTERING` |
| `SG_IMAGESAMPLETYPE_UNFILTERABLE_FLOAT` | `SG_SAMPLERTYPE_NONFILTERING` only |
| `SG_IMAGESAMPLETYPE_SINT` | `SG_SAMPLERTYPE_NONFILTERING` only |
| `SG_IMAGESAMPLETYPE_UINT` | `SG_SAMPLERTYPE_NONFILTERING` only |
| `SG_IMAGESAMPLETYPE_DEPTH` | `SG_SAMPLERTYPE_COMPARISON` only |

**Note:** These restrictions are enforced by the WebGPU backend and validation layer to ensure cross-platform compatibility.

---

## Vertex Attributes

### Input Attributes

```glsl
@vs vs
in vec4 position;       // Vertex position
in vec3 normal;         // Vertex normal
in vec2 texcoord0;      // UV coordinates
in vec4 color0;         // Vertex color
in vec4 tangent;        // Tangent (w = handedness)

out vec3 v_normal;
out vec2 v_uv;

void main() {
    gl_Position = position;
    v_normal = normal;
    v_uv = texcoord0;
}
@end
```

**Generated Constants**:
```csharp
public const int ATTR_vs_position = 0;
public const int ATTR_vs_normal = 1;
public const int ATTR_vs_texcoord0 = 2;
public const int ATTR_vs_color0 = 3;
public const int ATTR_vs_tangent = 4;
```

### Vertex Format Mapping

Sokol-gfx enforces strict vertex format rules to ensure correct data interpretation across all backends:

**CPU-Side Format → GPU-Side Type Mapping:**

| Vertex Format (SG_VERTEXFORMAT_*) | GLSL Type | Notes |
|-----------------------------------|-----------|-------|
| `FLOAT`, `FLOAT2`, `FLOAT3`, `FLOAT4` | `float`, `vec2`, `vec3`, `vec4` | Direct float values |
| `BYTE4N`, `UBYTE4N` | `vec4` | Normalized to [-1..+1] or [0..+1] |
| `SHORT2N`, `SHORT4N` | `vec2`, `vec4` | Normalized to [-1..+1] |
| `USHORT2N`, `USHORT4N` | `vec2`, `vec4` | Normalized to [0..+1] |
| `INT`, `INT2`, `INT3`, `INT4` | `int`, `ivec2`, `ivec3`, `ivec4` | Signed integer types |
| `UINT`, `UINT2`, `UINT3`, `UINT4` | `uint`, `uvec2`, `uvec3`, `uvec4` | Unsigned integer types |
| `BYTE4` | `ivec4` | Non-normalized signed bytes |
| `UBYTE4` | `uvec4` | Non-normalized unsigned bytes |
| `SHORT2`, `SHORT4` | `ivec2`, `ivec4` | Non-normalized signed shorts |
| `USHORT2`, `USHORT4` | `uvec2`, `uvec4` | Non-normalized unsigned shorts |

**Important Rules:**
- **Float formats** (including normalized formats) must use `float`/`vec*` in shader
- **Signed integer formats** must use `int`/`ivec*` in shader
- **Unsigned integer formats** must use `uint`/`uvec*` in shader
- **All vertex formats are 4-byte aligned** (minimum size is 4 bytes)
- **No gaps allowed** in vertex attribute bindings (e.g., can't skip slot 1 if using slots 0 and 2)

When using sokol-shdc with `--reflection`, these mappings are automatically validated. Mismatches will trigger validation errors.

### Output Variables

```glsl
@vs vs
out vec3 v_position;    // Varying: VS → FS
out vec3 v_normal;
out vec2 v_uv;
out vec4 v_color;
@end

@fs fs
in vec3 v_position;     // Must match VS outputs
in vec3 v_normal;
in vec2 v_uv;
in vec4 v_color;

out vec4 frag_color;
@end
```

---

## Advanced Features

### Storage Buffers

Storage buffers allow passing large amounts of random-access structured data to shaders. They're more convenient than data textures for array-like data.

**Platform Support:**
- ✓ Desktop OpenGL (4.3+), Direct3D 11, Metal, WebGPU
- ✓ Linux with GLES3.1+, Android with GLES3.1+
- ✗ macOS OpenGL (only goes to 4.1)
- ✗ iOS with GLES3 (OpenGL ES 3.0)
- ✗ **WebGL 2.0** (based on GLES3.0, lacks compute support)

**Important:** WebGL 2.0 does NOT support storage buffers because it's based on OpenGL ES 3.0. Storage buffers require OpenGL ES 3.1+ features. For storage buffers on web, use the WebGPU backend instead of WebGL 2.0.

**Readonly Storage Buffer (Vertex/Fragment Shaders):**

```glsl
@vs vs
// Declare struct for array elements
struct sb_vertex {
    vec3 pos;
    vec4 color;
};

// Readonly storage buffer with flexible array member
layout(binding=0) readonly buffer vertices {
    sb_vertex vtx[];
};

out vec4 v_color;

void main() {
    // Access storage buffer by vertex index
    vec3 pos = vtx[gl_VertexIndex].pos;
    v_color = vtx[gl_VertexIndex].color;
    gl_Position = vec4(pos, 1.0);
}
@end

@fs fs
in vec4 v_color;
out vec4 frag_color;

void main() {
    frag_color = v_color;
}
@end

@program vertexpull vs fs
```

**Read/Write Storage Buffer (Compute Shaders):**

```glsl
@cs compute
// Struct for particles
struct sb_particle {
    vec3 pos;
    vec3 vel;
};

// Read/write storage buffer
layout(binding=0) buffer particles_ssbo {
    sb_particle particles[];
};

// Compute shader thread group size (REQUIRED for all compute shaders)
layout(local_size_x=64, local_size_y=1, local_size_z=1) in;

void main() {
    uint idx = gl_GlobalInvocationID.x;
    
    // Read current particle state
    vec3 pos = particles[idx].pos;
    vec3 vel = particles[idx].vel;
    
    // Update physics
    vel.y -= 0.01;  // gravity
    pos += vel * 0.016;  // integrate
    
    // Write back updated state
    particles[idx].pos = pos;
    particles[idx].vel = vel;
}
@end

@program update_particles compute
```

**Compute Shader Thread Group Sizes:**

The `layout(local_size_x=X, local_size_y=Y, local_size_z=Z) in;` declaration is **required** for all compute shaders. It specifies how many threads execute in parallel per work group.

**Platform Support for Compute Shaders:**
- ✓ Desktop OpenGL (4.3+), Direct3D 11, Metal, WebGPU
- ✓ Linux with GLES3.1+, Android with GLES3.1+
- ✗ macOS OpenGL (only goes to 4.1)
- ✗ iOS with GLES3 (OpenGL ES 3.0)
- ✗ **WebGL 2.0** (based on GLES3.0, lacks compute support)

**Note:** For compute shaders on web, use the WebGPU backend instead of WebGL 2.0.

**Guidelines:**
- Total threads per group (X × Y × Z) should be a multiple of 32 or 64 for best performance
- Common configurations:
  - 1D work: `(64, 1, 1)` or `(256, 1, 1)`
  - 2D work: `(8, 8, 1)` or `(16, 16, 1)`
  - 3D work: `(4, 4, 4)` or `(8, 8, 2)`
- **Metal**: Thread group size is extracted from GLSL and passed to Metal shader creation
- Maximum total threads per group is typically 1024, but varies by hardware

**Usage in C#:**

```csharp
// Create storage buffer
var storageBuffer = sg_make_buffer(new sg_buffer_desc {
    usage = new sg_buffer_usage { 
        storage_buffer = true, 
        dynamic_update = true 
    },
    size = (nuint)(particleCount * Marshal.SizeOf<Particle>()),
    data = SG_RANGE(particleData)
});

// Bind storage buffer
var bindings = new sg_bindings();
bindings.vs.storage_buffers[0] = storageBuffer;
sg_apply_bindings(in bindings);
```

**Storage Buffer Authoring Rules:**
- Declare a struct describing a single array element
- Use `readonly buffer` for read-only access (vertex/fragment shaders)
- Use `buffer` (without readonly) for read/write access (compute shaders)
- Only put a single flexible array member in the buffer block
- Use `layout(binding=N)` to specify bind slot

**See Also:**
- Vertex pulling example: `examples/vertexpull-sapp.c`
- Instancing with storage buffers: `examples/instancing-pull-sapp.c`
- Compute shader particles: `examples/computeboids-sapp.c`

### Storage Images

Storage images allow compute shaders to write directly to textures. Useful for image processing and procedural generation.

**Supported Access Modes:**
- `writeonly` - Write-only access (most common)
- `readwrite` - Read and write access

**Supported Pixel Formats:**
- `RGBA8`, `RGBA8SN/UI/SI`
- `RGBA16UI/SI/F`
- `R32UI/SI/F`
- `RG32UI/SI/F`
- `RGBA32UI/SI/F`

**Example (Image Blur Compute Shader):**

```glsl
@cs blur
// Input texture (regular sampled texture)
layout(binding=0) uniform texture2D input_tex;
layout(binding=0) uniform sampler smp;

// Output storage image (writeonly)
layout(binding=0, rgba8) writeonly uniform image2D output_img;

layout(local_size_x=8, local_size_y=8, local_size_z=1) in;

void main() {
    ivec2 coord = ivec2(gl_GlobalInvocationID.xy);
    
    // Sample surrounding pixels
    vec4 color = vec4(0.0);
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            color += texture(sampler2D(input_tex, smp), 
                           vec2(coord + ivec2(x, y)) / vec2(256.0));
        }
    }
    color /= 9.0;  // Average
    
    // Write to output image
    imageStore(output_img, coord, color);
}
@end

@program blur blur
```

**Platform Support:**
- ✓ Direct3D 11, Metal, WebGPU, OpenGL 4.3+
- ✓ Linux/Android with GLES3.1+
- ✗ macOS OpenGL (only goes to 4.1)
- ✗ iOS with GLES3 (OpenGL ES 3.0)
- ✗ **WebGL 2.0** (based on GLES3.0, lacks compute support)

**Note:** Storage images require compute shader support (GLES3.1+), which WebGL 2.0 does not have. For storage images on web, use the WebGPU backend.

### Multiple Render Targets (MRT)

```glsl
@fs fs_offscreen
in float bright;

layout(location=0) out vec4 frag_color_0;  // First render target
layout(location=1) out vec4 frag_color_1;  // Second render target
layout(location=2) out vec4 frag_color_2;  // Third render target

void main() {
    frag_color_0 = vec4(bright, 0.0, 0.0, 1.0);
    frag_color_1 = vec4(0.0, bright, 0.0, 1.0);
    frag_color_2 = vec4(0.0, 0.0, bright, 1.0);
}
@end
```

### Instancing

```glsl
@vs vs
layout(binding=0) uniform vs_params {
    mat4 mvp;
};

in vec3 pos;            // Per-vertex attribute
in vec4 color0;         // Per-vertex attribute
in vec3 inst_pos;       // Per-instance attribute

out vec4 color;

void main() {
    vec4 position = vec4(pos + inst_pos, 1.0);  // Offset by instance
    gl_Position = mvp * position;
    color = color0;
}
@end
```

### Shadow Mapping

```glsl
@block util
vec4 encode_depth(float v) {
    vec4 enc = vec4(1.0, 255.0, 65025.0, 16581375.0) * v;
    enc = fract(enc);
    enc -= enc.yzww * vec4(1.0/255.0, 1.0/255.0, 1.0/255.0, 0.0);
    return enc;
}

float decode_depth(vec4 rgba) {
    return dot(rgba, vec4(1.0, 1.0/255.0, 1.0/65025.0, 1.0/16581375.0));
}

float sample_shadow(texture2D tex, sampler smp, vec2 uv, float compare) {
    float depth = decode_depth(texture(sampler2D(tex, smp), uv));
    return step(compare, depth);
}
@end

// Shadow pass - render depth
@vs vs_shadow
layout(binding=0) uniform vs_shadow_params {
    mat4 mvp;
};

in vec4 pos;
out vec2 proj_zw;

void main() {
    gl_Position = mvp * pos;
    proj_zw = gl_Position.zw;
}
@end

@fs fs_shadow
@include_block util

in vec2 proj_zw;
out vec4 frag_color;

void main() {
    float depth = proj_zw.x / proj_zw.y;
    frag_color = encode_depth(depth);
}
@end

@program shadow vs_shadow fs_shadow

// Display pass - use shadow map
@vs vs_display
layout(binding=0) uniform vs_display_params {
    mat4 mvp;
    mat4 light_mvp;
};

in vec4 pos;
out vec4 light_proj_pos;

void main() {
    gl_Position = mvp * pos;
    light_proj_pos = light_mvp * pos;
}
@end

@fs fs_display
@include_block util

layout(binding=0) uniform texture2D shadow_map;
layout(binding=0) uniform sampler smp;

in vec4 light_proj_pos;
out vec4 frag_color;

void main() {
    vec3 light_pos = light_proj_pos.xyz / light_proj_pos.w;
    vec2 sm_uv = light_pos.xy * 0.5 + 0.5;
    float shadow = sample_shadow(shadow_map, smp, sm_uv, light_pos.z);
    
    vec3 color = vec3(0.5) * shadow;
    frag_color = vec4(color, 1.0);
}
@end

@program display vs_display fs_display
```

### YUV to RGB Conversion (Video Playback)

```glsl
@fs fs
layout(binding=0) uniform texture2D tex_y;
layout(binding=1) uniform texture2D tex_cb;
layout(binding=2) uniform texture2D tex_cr;
layout(binding=0) uniform sampler smp;

in vec2 uv;
out vec4 frag_color;

// Rec. 601 color space conversion matrix
mat4 rec601 = mat4(
    1.16438,  0.00000,  1.59603, -0.87079,
    1.16438, -0.39176, -0.81297,  0.52959,
    1.16438,  2.01723,  0.00000, -1.08139,
    0, 0, 0, 1
);

void main() {
    float y = texture(sampler2D(tex_y, smp), uv).r;
    float cb = texture(sampler2D(tex_cb, smp), uv).r;
    float cr = texture(sampler2D(tex_cr, smp), uv).r;
    frag_color = vec4(y, cb, cr, 1.0) * rec601;
}
@end
```

---

## Platform-Specific Code

### Conditional Compilation

Use preprocessor defines to handle platform differences:

```glsl
@vs vs
in vec4 pos;
out vec4 light_proj_pos;

void main() {
    gl_Position = pos;
    light_proj_pos = some_matrix * pos;
    
    // Flip Y for non-GLSL backends
    #if !SOKOL_GLSL
        light_proj_pos.y = -light_proj_pos.y;
    #endif
}
@end
```

**Available Defines**:
- `SOKOL_GLSL` - OpenGL/WebGL backends
- `SOKOL_HLSL` - Direct3D backend
- `SOKOL_MSL` - Metal backend
- `SOKOL_WGSL` - WebGPU backend

### Backend Detection

```glsl
#if SOKOL_GLSL
    // OpenGL-specific code
    vec4 color = texture(tex, uv);
#elif SOKOL_HLSL
    // Direct3D-specific code
    vec4 color = tex.Sample(smp, uv);
#elif SOKOL_MSL
    // Metal-specific code
    vec4 color = tex.sample(smp, uv);
#endif
```

### Coordinate System Differences

```glsl
@vs vs
void main() {
    gl_Position = mvp * position;
    
    // Handle coordinate system differences
    #if SOKOL_METAL || SOKOL_D3D11
        // These APIs have Y-down clip space
        gl_Position.y = -gl_Position.y;
    #endif
}
@end
```

### Backend-Specific Shader Syntax

Different backends have slightly different syntax requirements for vertex attributes, uniform buffers, and resource bindings:

**Vertex Attributes:**

| Backend | Syntax | Example |
|---------|--------|---------|
| **GLSL** (GL/GLES) | `in` keyword, optional `layout(location=N)` | `in vec3 position;` or `layout(location=0) in vec3 position;` |
| **HLSL** (D3D11) | Semantic names required (e.g., `TEXCOORD0`) | Position attribute → `TEXCOORD0`, semantic="TEXCOORD", index=0 |
| **MSL** (Metal) | `[[attribute(N)]]` in shader | Vertex attributes bound by `[[attribute(N)]]` |
| **WGSL** (WebGPU) | `@location(N)` in shader | Vertex attributes bound by `@location(N)` |

**Note:** When using sokol-shdc, these backend-specific differences are handled automatically during cross-compilation. You only need to be aware of them when writing backend-specific shader code manually.

**Uniform Block Bindings:**

| Backend | Bind Slot Declaration | Slot Range |
|---------|----------------------|------------|
| **D3D11/HLSL** | `register(b0..b7)` | Per shader stage, b0-b7 for uniform blocks |
| **Metal/MSL** | `[[buffer(0..7)]]` | Per shader stage, slots 0-7 for uniform blocks |
| **WebGPU/WGSL** | `@group(0) @binding(0..15)` | Common bindslot space across stages |
| **GL/GLSL** | Uniform blocks bound by name | Name-based binding |

**Texture/Sampler Bindings:**

| Backend | Texture Binding | Sampler Binding | Slot Range |
|---------|----------------|-----------------|------------|
| **D3D11/HLSL** | `register(t0..t31)` | `register(s0..s11)` | Shared with readonly storage buffers (t) |
| **Metal/MSL** | `[[texture(0..31)]]` | `[[sampler(0..11)]]` | Shared with storage images |
| **WebGPU/WGSL** | `@group(1) @binding(N)` | `@group(1) @binding(N)` | N = 0..127 (shared space) |
| **GL/GLSL** | `layout(binding=N)` | `layout(binding=N)` | Name-based or explicit binding |

**Storage Buffer Bindings:**

| Backend | Readonly Storage Buffer | Read/Write Storage Buffer | Slot Range |
|---------|------------------------|---------------------------|------------|
| **D3D11/HLSL** | `register(t0..t31)` (shared with textures) | `register(u0..u31)` (UAV) | Separate spaces for SRV/UAV |
| **Metal/MSL** | `[[buffer(8..23)]]` | `[[buffer(8..23)]]` | Slots 8-23 for storage buffers |
| **WebGPU/WGSL** | `@group(1) @binding(0..127)` | `@group(1) @binding(0..127)` | Shared with textures/samplers |
| **GL/GLSL** | `layout(std430, binding=N)` | `layout(std430, binding=N)` | N = 0..max_storage_buffer_bindings |

---

## Code Reusability

### Shared Code Blocks

```glsl
@block math_functions
float square(float x) {
    return x * x;
}

vec3 srgb_to_linear(vec3 srgb) {
    return pow(srgb, vec3(2.2));
}

vec3 linear_to_srgb(vec3 linear) {
    return pow(linear, vec3(1.0/2.2));
}
@end

@block lighting_functions
@include_block math_functions  // Can include other blocks

vec3 calculate_diffuse(vec3 normal, vec3 light_dir, vec3 light_color) {
    float ndotl = max(dot(normal, light_dir), 0.0);
    return light_color * ndotl;
}

vec3 calculate_specular(vec3 normal, vec3 light_dir, vec3 view_dir, 
                       vec3 light_color, float shininess) {
    vec3 reflect_dir = reflect(-light_dir, normal);
    float spec = pow(max(dot(view_dir, reflect_dir), 0.0), shininess);
    return light_color * spec;
}
@end

@vs vs
// Use shared functions
@end

@fs fs
@include_block lighting_functions

in vec3 v_normal;
in vec3 v_view_dir;
out vec4 frag_color;

void main() {
    vec3 light_dir = normalize(vec3(1.0, 1.0, -1.0));
    vec3 light_color = vec3(1.0);
    
    vec3 diffuse = calculate_diffuse(v_normal, light_dir, light_color);
    vec3 specular = calculate_specular(v_normal, light_dir, v_view_dir, 
                                       light_color, 32.0);
    
    vec3 color = srgb_to_linear(diffuse + specular);
    frag_color = vec4(linear_to_srgb(color), 1.0);
}
@end

@program lit vs fs
```

### External Include Files

**file: pbr_functions.glsl**
```glsl
@block pbr_functions
vec3 fresnel_schlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
}

float distribution_ggx(vec3 N, vec3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    
    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = 3.14159 * denom * denom;
    
    return nom / denom;
}
@end
```

**file: main.glsl**
```glsl
@include pbr_functions.glsl

@fs fs_pbr
@include_block pbr_functions

in vec3 v_normal;
in vec3 v_view;
out vec4 frag_color;

void main() {
    vec3 F0 = vec3(0.04);  // Default dielectric
    float roughness = 0.5;
    
    vec3 H = normalize(v_view + light_dir);
    vec3 F = fresnel_schlick(max(dot(H, v_view), 0.0), F0);
    float D = distribution_ggx(v_normal, H, roughness);
    
    // Rest of PBR calculation...
}
@end
```

---

## Best Practices

### 1. Always Use Explicit Bindings

**Good**:
```glsl
layout(binding=0) uniform vs_params { mat4 mvp; };
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;
```

**Bad**:
```glsl
uniform vs_params { mat4 mvp; };  // No binding specified
uniform texture2D tex;
uniform sampler smp;
```

### 2. Use Type Mappings

Always provide `@ctype` for proper C# struct generation:

```glsl
@ctype mat4 System.Numerics.Matrix4x4
@ctype vec3 System.Numerics.Vector3
@ctype vec4 System.Numerics.Vector4
```

### 3. Separate Concerns with Blocks

```glsl
@block vertex_functions
// Vertex-related functions
@end

@block fragment_functions
// Fragment-related functions
@end

@block shared_functions
// Functions used by both
@end
```

### 4. Comment Your Shaders

```glsl
//------------------------------------------------------------------------------
//  PBR shader with Image-Based Lighting
//  
//  Features:
//  - Metallic-Roughness workflow
//  - IBL support
//  - Normal mapping
//  - Vertex skinning
//------------------------------------------------------------------------------

@vs vs_pbr
// Vertex attributes
layout(location=0) in vec3 position;  // Vertex position in model space
layout(location=1) in vec3 normal;    // Vertex normal
layout(location=2) in vec4 tangent;   // Tangent (w = handedness)

// ... rest of shader
@end
```

### 5. Handle Platform Differences

```glsl
@vs vs
void main() {
    gl_Position = mvp * position;
    
    // Handle Y-axis differences
    #if !SOKOL_GLSL
        gl_Position.y = -gl_Position.y;
    #endif
    
    // Handle Z-range differences (0..1 vs -1..1)
    #if SOKOL_D3D11 || SOKOL_METAL
        gl_Position.z = (gl_Position.z + gl_Position.w) * 0.5;
    #endif
}
@end
```

### 6. Use Meaningful Names

**Good**:
```glsl
@vs vs_shadow_pass
@fs fs_shadow_pass
@program shadow vs_shadow_pass fs_shadow_pass

@vs vs_display_pass
@fs fs_display_pass
@program display vs_display_pass fs_display_pass
```

**Bad**:
```glsl
@vs vs1
@fs fs1
@program prog1 vs1 fs1

@vs vs2
@fs fs2
@program prog2 vs2 fs2
```

### 7. Validate Early, Optimize Later

```glsl
@fs fs
in vec3 v_normal;
out vec4 frag_color;

void main() {
    // Validate inputs during development
    #ifdef DEBUG
        if (length(v_normal) < 0.9 || length(v_normal) > 1.1) {
            frag_color = vec4(1.0, 0.0, 1.0, 1.0);  // Magenta = error
            return;
        }
    #endif
    
    // Normal shader code
    vec3 normal = normalize(v_normal);
    frag_color = vec4(normal * 0.5 + 0.5, 1.0);
}
@end
```

---

## Common Patterns

### Pattern 1: Simple Textured Mesh

```glsl
@ctype mat4 System.Numerics.Matrix4x4

@vs vs
layout(binding=0) uniform vs_params {
    mat4 mvp;
};

in vec4 position;
in vec2 texcoord0;
out vec2 uv;

void main() {
    gl_Position = mvp * position;
    uv = texcoord0;
}
@end

@fs fs
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;

in vec2 uv;
out vec4 frag_color;

void main() {
    frag_color = texture(sampler2D(tex, smp), uv);
}
@end

@program textured vs fs
```

### Pattern 2: Lit Mesh with Normal Mapping

```glsl
@ctype mat4 System.Numerics.Matrix4x4
@ctype vec3 System.Numerics.Vector3

@vs vs
layout(binding=0) uniform vs_params {
    mat4 model;
    mat4 view_proj;
    vec3 light_pos;
};

in vec4 position;
in vec3 normal;
in vec4 tangent;
in vec2 texcoord0;

out vec3 v_world_pos;
out vec3 v_light_pos;
out vec2 v_uv;
out mat3 v_tbn;

void main() {
    vec4 world_pos = model * position;
    v_world_pos = world_pos.xyz;
    gl_Position = view_proj * world_pos;
    v_uv = texcoord0;
    
    // Build TBN matrix for normal mapping
    vec3 N = normalize((model * vec4(normal, 0.0)).xyz);
    vec3 T = normalize((model * vec4(tangent.xyz, 0.0)).xyz);
    vec3 B = cross(N, T) * tangent.w;
    v_tbn = mat3(T, B, N);
    
    v_light_pos = light_pos;
}
@end

@fs fs
layout(binding=0) uniform texture2D tex_albedo;
layout(binding=1) uniform texture2D tex_normal;
layout(binding=0) uniform sampler smp;

in vec3 v_world_pos;
in vec3 v_light_pos;
in vec2 v_uv;
in mat3 v_tbn;

out vec4 frag_color;

void main() {
    // Sample textures
    vec3 albedo = texture(sampler2D(tex_albedo, smp), v_uv).rgb;
    vec3 normal_map = texture(sampler2D(tex_normal, smp), v_uv).rgb;
    
    // Transform normal from tangent space to world space
    vec3 normal = normalize(v_tbn * (normal_map * 2.0 - 1.0));
    
    // Simple diffuse lighting
    vec3 light_dir = normalize(v_light_pos - v_world_pos);
    float ndotl = max(dot(normal, light_dir), 0.0);
    
    vec3 color = albedo * ndotl;
    frag_color = vec4(color, 1.0);
}
@end

@program lit_normal_mapped vs fs
```

### Pattern 3: Fullscreen Quad Post-Processing

```glsl
@vs vs_fsq
@glsl_options flip_vert_y

in vec2 pos;  // [-1..1] NDC coordinates
out vec2 uv;

void main() {
    gl_Position = vec4(pos, 0.5, 1.0);
    uv = pos * 0.5 + 0.5;  // Convert to [0..1] UV
}
@end

@fs fs_post_process
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;

layout(binding=0) uniform fs_params {
    float blur_amount;
    float saturation;
};

in vec2 uv;
out vec4 frag_color;

void main() {
    vec4 color = texture(sampler2D(tex, smp), uv);
    
    // Simple blur (sample neighbors)
    vec2 texel_size = vec2(1.0/1024.0, 1.0/768.0);
    color += texture(sampler2D(tex, smp), uv + vec2(-1, -1) * texel_size);
    color += texture(sampler2D(tex, smp), uv + vec2( 1, -1) * texel_size);
    color += texture(sampler2D(tex, smp), uv + vec2(-1,  1) * texel_size);
    color += texture(sampler2D(tex, smp), uv + vec2( 1,  1) * texel_size);
    color *= 0.2 * blur_amount;
    
    // Saturation adjustment
    float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
    color.rgb = mix(vec3(gray), color.rgb, saturation);
    
    frag_color = color;
}
@end

@program post_process vs_fsq fs_post_process
```

### Pattern 4: Particle System

```glsl
@ctype mat4 System.Numerics.Matrix4x4
@ctype vec3 System.Numerics.Vector3

@vs vs_particles
layout(binding=0) uniform vs_params {
    mat4 view_proj;
};

// Per-vertex (billboard corners)
in vec2 pos;           // [-1..1] corner positions
in vec2 texcoord0;

// Per-instance (particle data)
in vec3 inst_pos;      // Particle world position
in vec4 inst_color;    // Particle color
in float inst_size;    // Particle size

out vec2 uv;
out vec4 color;

void main() {
    // Billboard: always face camera
    vec3 world_pos = inst_pos;
    world_pos.xy += pos * inst_size;
    
    gl_Position = view_proj * vec4(world_pos, 1.0);
    uv = texcoord0;
    color = inst_color;
}
@end

@fs fs_particles
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;

in vec2 uv;
in vec4 color;
out vec4 frag_color;

void main() {
    vec4 tex_color = texture(sampler2D(tex, smp), uv);
    frag_color = tex_color * color;
}
@end

@program particles vs_particles fs_particles
```

---

## Limitations and Considerations

### 1. GLSL Version Compatibility

**Supported**: GLSL 450 (Vulkan-style)
```glsl
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;
vec4 color = texture(sampler2D(tex, smp), uv);
```

**Not Supported**: GLSL 330 or older
```glsl
// Don't use this style:
uniform sampler2D tex;
vec4 color = texture2D(tex, uv);
```

### 2. Separate Textures and Samplers

Always use separate texture and sampler objects:

```glsl
// Correct
layout(binding=0) uniform texture2D tex;
layout(binding=0) uniform sampler smp;
vec4 color = texture(sampler2D(tex, smp), uv);

// Incorrect
uniform sampler2D tex;  // Combined sampler (not supported)
```

### 3. Uniform Buffer Alignment

Follow std140 layout rules:

```glsl
layout(binding=0, std140) uniform params {
    vec3 position;   // 12 bytes, but padded to 16
    float radius;    // Uses the padding space
    vec4 color;      // 16 bytes, aligned
};
```

**Alignment Rules**:
- Scalars: 4 bytes
- `vec2`: 8 bytes
- `vec3`, `vec4`: 16 bytes
- Arrays: Each element aligned to 16 bytes
- Structs: Aligned to largest member

### 4. Maximum Bindings and Limits

Sokol.NET enforces portable limits across all backends to ensure cross-platform compatibility:

**Resource Binding Limits** (from `sokol_gfx.h`):
- **Texture Bindings per Stage**: 16 (`SG_MAX_PORTABLE_TEXTURE_BINDINGS_PER_STAGE`)
- **Sampler Bindings**: 12 (`SG_MAX_SAMPLER_BINDINGS`)
- **Uniform Block Bindings**: 8 (`SG_MAX_UNIFORMBLOCK_BINDSLOTS`)
- **Vertex Attributes**: 16 (`SG_MAX_VERTEX_ATTRIBUTES`)
- **Vertex Buffer Bind Slots**: 8 (`SG_MAX_VERTEXBUFFER_BINDSLOTS`)

**Rendering Limits**:
- **Color Attachments (MRT)**: 4 portable, 8 maximum (`SG_MAX_PORTABLE_COLOR_ATTACHMENTS`, `SG_MAX_COLOR_ATTACHMENTS`)
- **Uniform Block Members**: 16 (`SG_MAX_UNIFORMBLOCK_MEMBERS`)
- **Mipmap Levels**: 16 (`SG_MAX_MIPMAPS`)

**Compute Shader Limits** (if `sg_features.compute = true`):
- **Storage Buffer Bindings per Stage**: 8 (`SG_MAX_PORTABLE_STORAGEBUFFER_BINDINGS_PER_STAGE`)
- **Storage Image Bindings per Stage**: 4 (`SG_MAX_PORTABLE_STORAGEIMAGE_BINDINGS_PER_STAGE`)

**Best Practices**:
- Stay within portable limits for guaranteed cross-platform support
- Use up to **16 textures** per shader stage (vertex or fragment)
- Use up to **12 samplers** total
- Use up to **8 uniform blocks** per stage
- For MRT, use **4 color attachments** for portability (8 maximum on some platforms)

**Example - Maximum Texture Usage**:
```glsl
@fs fs
// Maximum 16 textures per stage (portable limit)
layout(binding=0) uniform texture2D tex0;
layout(binding=1) uniform texture2D tex1;
layout(binding=2) uniform texture2D tex2;
// ... up to binding=15
layout(binding=15) uniform texture2D tex15;

// Maximum 12 samplers total
layout(binding=0) uniform sampler smp0;
layout(binding=1) uniform sampler smp1;
// ... up to binding=11
@end
```

**Backend-Specific Notes**:
- Desktop OpenGL/Vulkan: May support more, but limited to portable values
- WebGL 2.0: Typically supports 16 texture units per stage
- Direct3D 11: Has higher internal limits but constrained to portable values
- Metal: Has higher internal limits but constrained to portable values

These limits are enforced by Sokol to ensure your shaders work consistently across all platforms.

### 5. Shader Complexity

- **Desktop**: Complex shaders supported
- **Mobile**: Keep shader complexity low
  - Avoid dynamic branching
  - Minimize texture samples
  - Reduce register pressure
- **WebGL**: Additional restrictions
  - No uniform buffer objects in WebGL 1.0
  - Limited precision (`mediump` may be required)

### 6. Compute Shaders

Compute shader support varies:

```glsl
@cs compute_shader
layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(binding=0, rgba8) uniform image2D img_output;

void main() {
    ivec2 coords = ivec2(gl_GlobalInvocationID.xy);
    vec4 color = vec4(1.0, 0.0, 0.0, 1.0);
    imageStore(img_output, coords, color);
}
@end
```

**Supported**:
- OpenGL 4.3+
- OpenGL ES 3.1+
- Metal
- Direct3D 11 (limited)
- WebGPU

**Not Supported**:
- OpenGL < 4.3
- OpenGL ES 3.0
- WebGL 2.0

### 7. Precision Qualifiers

Mobile GPUs may require precision qualifiers:

```glsl
@fs fs
precision highp float;  // Global precision

in highp vec3 v_position;
in mediump vec2 v_uv;
in lowp vec4 v_color;

out lowp vec4 frag_color;

void main() {
    // Use appropriate precision for mobile
    mediump vec4 tex_color = texture(sampler2D(tex, smp), v_uv);
    frag_color = tex_color * v_color;
}
@end
```

### 8. Integer Textures

Not all backends support integer textures:

```glsl
// May not work on all platforms
layout(binding=0) uniform itexture2D int_tex;
ivec4 value = texelFetch(int_tex, coords, 0);
```

**Best Practice**: Use float textures and convert in shader if needed

---

## Troubleshooting

### Error: "Binding X already used"

**Problem**: Multiple resources sharing same binding number in same stage

**Solution**: Use unique bindings per resource type:
```glsl
// Wrong
layout(binding=0) uniform texture2D tex1;
layout(binding=0) uniform texture2D tex2;  // Conflict!

// Correct
layout(binding=0) uniform texture2D tex1;
layout(binding=1) uniform texture2D tex2;
layout(binding=0) uniform sampler smp;  // OK, different type
```

### Error: "Uniform block size exceeds maximum"

**Problem**: Uniform buffer too large

**Solution**: Split into multiple buffers or use storage buffers:
```glsl
// Split large uniform block
layout(binding=0) uniform vs_params_1 {
    mat4 matrices[50];
};

layout(binding=1) uniform vs_params_2 {
    vec4 colors[100];
};
```

### Error: "gl_Position not written in vertex shader"

**Problem**: Vertex shader must write to `gl_Position`

**Solution**: Always assign `gl_Position`:
```glsl
@vs vs
void main() {
    gl_Position = mvp * position;  // Required!
}
@end
```

### Warning: "Unused variable"

**Problem**: Declared but unused variables

**Solution**: Remove unused variables or comment them:
```glsl
// Wrong
in vec4 color;  // Declared but never used

// Correct
// in vec4 color;  // Disabled for now
```

### Error: "Type mismatch between VS output and FS input"

**Problem**: Vertex shader outputs don't match fragment shader inputs

**Solution**: Ensure exact type and name match:
```glsl
@vs vs
out vec3 v_color;  // vec3
@end

@fs fs
in vec4 v_color;   // Wrong! Should be vec3
@end
```

### Platform-Specific Issues

#### Metal: Y-Axis Flip

```glsl
@vs vs
void main() {
    gl_Position = mvp * position;
    #if SOKOL_METAL
        gl_Position.y = -gl_Position.y;
    #endif
}
@end
```

#### WebGL: Precision Issues

```glsl
@fs fs
precision highp float;  // Explicit precision for WebGL

void main() {
    // shader code
}
@end
```

#### Direct3D: Clip Space Range

```glsl
@vs vs
void main() {
    gl_Position = mvp * position;
    #if SOKOL_D3D11
        // Convert from -1..1 to 0..1 depth range
        gl_Position.z = (gl_Position.z + gl_Position.w) * 0.5;
    #endif
}
@end
```

---

## Additional Resources

### Example Shaders in Sokol.NET

Browse the `examples/` folder for real-world shader examples:

- **`cube/shaders/`** - Basic textured mesh
- **`shadows/shaders/`** - Shadow mapping
- **`mrt/shaders/`** - Multiple render targets
- **`instancing/shaders/`** - Hardware instancing
- **`offscreen/shaders/`** - Render-to-texture
- **`GltfViewer/shaders/`** - Advanced PBR with IBL
- **`plmpeg/shaders/`** - YUV video decoding
- **`sdf/shaders/`** - Signed distance fields

### sokol-shdc Documentation

Full sokol-shdc documentation: `tools/sokol-tools/docs/sokol-shdc.md`

### Sokol Headers

Original C library documentation: https://github.com/floooh/sokol

---

## Summary

**Key Takeaways**:

1. ✅ **Write Once**: GLSL 450 source code
2. ✅ **Run Everywhere**: Automatic HLSL/Metal/GLSL generation
3. ✅ **Type-Safe**: C# structs generated for uniforms
4. ✅ **Compile-Time Validation**: Catch errors during build
5. ✅ **Zero Runtime Cost**: All shaders embedded in assembly

**Essential Tags**:
- `@vs` / `@fs` / `@cs` - Shader stage blocks
- `@program` - Shader program declaration
- `@ctype` - C# type mapping
- `@block` / `@include_block` - Code reuse
- `@glsl_options` - Platform-specific options

**Best Practices**:
- Always use explicit bindings
- Separate textures and samplers
- Follow std140 alignment rules
- Handle platform differences with `#if`
- Use meaningful names for shaders and programs

**Common Patterns**: See [Common Patterns](#common-patterns) section for ready-to-use examples

---

Happy shader coding! 🎨✨
