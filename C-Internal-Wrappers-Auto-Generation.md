# Auto-Generated C Internal Wrappers for WebAssembly

## Overview

WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke. To work around this limitation, we use internal wrapper functions that take an output pointer parameter instead of returning structs by value.

Previously, these wrapper functions were manually written in `ext/sokol.c`. Now, they are **fully automatically detected and generated** by the binding generator with **zero manual configuration required**.

## How It Works

### 1. **Automatic Detection**

The binding generator **automatically scans all sokol headers** during the pre-parse phase and detects functions that return structs by value. No manual dictionary maintenance required!

The detection algorithm in `gen_csharp.py`:
- Scans all function declarations in the IR
- Identifies functions returning struct types (not pointers, not primitives, not void)
- Excludes special cases:
  - Functions in `web_wrapper_functions` (id-based wrappers like `sg_make_shader`)
  - Ignored/deprecated functions
- Automatically populates the `web_wrapper_struct_return_functions` dictionary

```python
# This dictionary is now AUTO-POPULATED - no manual editing needed!
web_wrapper_struct_return_functions = {}

def detect_struct_return_functions(inp):
    """Automatically detect all functions returning structs by value"""
    for decl in inp['decls']:
        if not decl['is_dep'] and decl['kind'] == 'func':
            # ... detection logic ...
            if is_struct_type(return_type) and not is_pointer and return_type != 'void':
                web_wrapper_struct_return_functions[func_name] = return_type
                print(f"[AUTO-DETECTED] {func_name} returns struct {return_type}")
```

### 2. **Automatic C Header Generation**

When you run `./scripts/generate-bindings.sh`, the binding generator:

1. **Auto-detects** all struct-returning functions across all sokol modules
2. Generates `ext/sokol_csharp_internal_wrappers.h` with grouped wrapper implementations
3. Includes **conditional compilation** for optional modules (Basis Universal, sokol_gp)

Example generated code:

```c
// ========== SShape (sshape_) ==========

SOKOL_API_IMPL void sshape_build_plane_internal(sshape_buffer_t* result, const sshape_buffer_t* buf, const sshape_plane_t* params) {
    *result = sshape_build_plane(buf, params);
}

// ========== SBasisu (sbasisu_) ==========
#if defined(SOKOL_BASISU_INCLUDED)
SOKOL_API_IMPL void sbasisu_make_image_internal(sg_image* result, sg_range basisu_data) {
    *result = sbasisu_make_image(basisu_data);
}
#endif // SOKOL_BASISU_INCLUDED
```

### 3. **Inclusion in sokol.c**

The generated header is included in `ext/sokol.c`:

```c
// Include auto-generated internal wrapper functions
#include "sokol_csharp_internal_wrappers.h"
```

### 4. **C# Binding Generation**

For each auto-detected function, the C# binding generator creates platform-specific wrappers:

#### On WebAssembly (WEB defined):
```csharp
#if WEB
public static sshape_buffer_t SshapeBuildPlane(in sshape_buffer_t buf, in sshape_plane_t parameters)
{
    sshape_buffer_t result = default;
    SshapeBuildPlane_internal(ref result, buf, parameters);
    return result;
}
#else
// Regular P/Invoke declaration for desktop/mobile
[DllImport("sokol", EntryPoint = "sshape_build_plane", CallingConvention = CallingConvention.Cdecl)]
public static extern sshape_buffer_t SshapeBuildPlane(in sshape_buffer_t buf, in sshape_plane_t parameters);
#endif
```

#### The _internal function declaration:
```csharp
[DllImport("sokol", EntryPoint = "sshape_build_plane_internal", CallingConvention = CallingConvention.Cdecl)]
public static extern void SshapeBuildPlane_internal(ref sshape_buffer_t result, in sshape_buffer_t buf, in sshape_plane_t parameters);
```

## Adding New Functions - IT'S AUTOMATIC!

**You don't need to do anything!** When you add a new sokol function that returns a struct by value:

1. **Just run the binding generator:**
   ```bash
   ./scripts/generate-bindings.sh
   ```

2. **Done!** The system automatically:
   - Detects the new struct-returning function
   - Generates the C internal wrapper in `sokol_csharp_internal_wrappers.h`
   - Generates the C# WebAssembly wrapper code
   - Generates the C# _internal function declaration
   - Prints `[AUTO-DETECTED] function_name returns struct return_type`

## Benefits

✅ **Zero Manual Configuration**: The system automatically detects ALL struct-returning functions
✅ **No Manual C Code**: Never manually write internal wrapper functions
✅ **Comprehensive Coverage**: Detects 92+ functions across all sokol modules
✅ **Consistency**: All wrappers follow the same pattern
✅ **Type Safety**: Uses actual function signatures from IR, not manual typing
✅ **Optional Module Support**: Handles Basis Universal and sokol_gp with conditional compilation
✅ **Future-Proof**: New sokol functions are automatically detected on re-generation

## Files Involved

- **`bindgen/gen_csharp.py`**: Auto-detection logic and generation code
- **`bindgen/gen.py`**: Orchestrates generation and writes the header file
- **`ext/sokol_csharp_internal_wrappers.h`**: Auto-generated C header (**DO NOT EDIT MANUALLY**)
- **`ext/sokol.c`**: Includes the generated header
- **`src/sokol/generated/*.cs`**: Generated C# bindings with WebAssembly wrappers

## Auto-Detected Functions by Module

The system automatically detects **92 struct-returning functions** across all sokol modules:

### sokol_gfx.h (49 functions)
- Query functions: `sg_query_desc`, `sg_query_features`, `sg_query_limits`, `sg_query_pixelformat`
- Resource info: `sg_query_buffer_info`, `sg_query_image_info`, `sg_query_sampler_info`, `sg_query_shader_info`, `sg_query_pipeline_info`, `sg_query_view_info`
- Descriptor queries: `sg_query_buffer_desc`, `sg_query_image_desc`, `sg_query_sampler_desc`, `sg_query_shader_desc`, `sg_query_pipeline_desc`, `sg_query_view_desc`
- Default queries: `sg_query_buffer_defaults`, `sg_query_image_defaults`, `sg_query_sampler_defaults`, `sg_query_shader_defaults`, `sg_query_pipeline_defaults`, `sg_query_view_defaults`
- Usage/stats: `sg_query_buffer_usage`, `sg_query_image_usage`, `sg_query_frame_stats`
- View queries: `sg_query_view_image`, `sg_query_view_buffer`
- Backend queries: D3D11, Metal, WebGPU, OpenGL specific query functions

### sokol_app.h (1 function)
- `sapp_query_desc`

### sokol_glue.h (2 functions)
- `sglue_environment`, `sglue_swapchain`

### sokol_audio.h (1 function)
- `saudio_query_desc`

### sokol_fetch.h (2 functions)
- `sfetch_desc`, `sfetch_send`

### sokol_gl.h (6 functions)
- `sgl_error`, `sgl_context_error`, `sgl_make_context`, `sgl_get_context`, `sgl_default_context`, `sgl_context_make_pipeline`

### sokol_debugtext.h (7 functions)
- Font descriptors: `sdtx_font_kc853`, `sdtx_font_kc854`, `sdtx_font_z1013`, `sdtx_font_cpc`, `sdtx_font_c64`, `sdtx_font_oric`
- `sdtx_get_cleared_fmt_buffer`

### sokol_shape.h (19 functions)
- Build functions: `sshape_build_plane`, `sshape_build_box`, `sshape_build_sphere`, `sshape_build_cylinder`, `sshape_build_torus`
- Size functions: `sshape_plane_sizes`, `sshape_box_sizes`, `sshape_sphere_sizes`, `sshape_cylinder_sizes`, `sshape_torus_sizes`
- Buffer descriptors: `sshape_element_range`, `sshape_vertex_buffer_desc`, `sshape_index_buffer_desc`, `sshape_vertex_buffer_layout_state`
- Attribute states: `sshape_position_vertex_attr_state`, `sshape_normal_vertex_attr_state`, `sshape_texcoord_vertex_attr_state`, `sshape_color_vertex_attr_state`
- Matrix functions: `sshape_mat4`, `sshape_mat4_transpose`

### sokol_basisu.h (2 functions - optional)
- `sbasisu_make_image`, `sbasisu_transcode` *(wrapped with `#if defined(SOKOL_BASISU_INCLUDED)`)*

### sokol_imgui.h (2 functions)
- `simgui_texture_view_from_imtextureid`, `simgui_sampler_from_imtextureid`

### sokol_gp.h (1+ functions - optional)
- `sgp_query_desc` and others *(wrapped with `#if defined(SOKOL_GP_INCLUDED)`)*

**Total: 92+ functions** automatically detected and wrapped for WebAssembly compatibility!

## Detection Output

When running `./scripts/generate-bindings.sh`, you'll see output like:

```
[AUTO-DETECTED] sg_query_desc returns struct sg_desc
[AUTO-DETECTED] sg_query_features returns struct sg_features
[AUTO-DETECTED] sshape_build_plane returns struct sshape_buffer_t
...
Auto-detected 92 functions returning structs by value
Generated: ../ext/sokol_csharp_internal_wrappers.h
```
