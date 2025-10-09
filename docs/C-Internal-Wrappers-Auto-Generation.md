# Auto-Generated C Internal Wrappers for WebAssembly

## Overview

WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke. To work around this limitation, we use internal wrapper functions that take an output pointer parameter instead of returning structs by value.

Previously, these wrapper functions were manually written in `ext/sokol.c`. Now, they are **automatically generated** by the binding generator.

## How It Works

### 1. **Declaration in gen_csharp.py**

Functions that return structs by value are listed in the `web_wrapper_struct_return_functions` dictionary:

```python
web_wrapper_struct_return_functions = {
    'sglue_swapchain': 'sg_swapchain',
    'sglue_environment': 'sg_environment',
    'sdtx_font_kc853': 'sdtx_font_desc_t',
    'sdtx_font_kc854': 'sdtx_font_desc_t',
    # ... more functions
    'sshape_build_plane': 'sshape_buffer_t',
    'sshape_build_box': 'sshape_buffer_t',
    # ... etc
}
```

The key is the function name, and the value is the return type.

### 2. **Automatic C Header Generation**

When you run `./scripts/generate-bindings.sh`, the binding generator:

1. Processes all sokol headers
2. Identifies functions in `web_wrapper_struct_return_functions`
3. Generates `ext/sokol_csharp_internal_wrappers.h` containing all internal wrapper implementations

Example generated code:

```c
SOKOL_API_IMPL void sshape_build_plane_internal(sshape_buffer_t* result, const sshape_buffer_t* buf, const sshape_plane_t* params) {
    *result = sshape_build_plane(buf, params);
}
```

### 3. **Inclusion in sokol.c**

The generated header is included in `ext/sokol.c`:

```c
#include "sokol_csharp_internal_wrappers.h"
```

### 4. **C# Binding Generation**

For each function in `web_wrapper_struct_return_functions`, the C# binding generator creates:

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
// Regular P/Invoke declaration
[DllImport("sokol", EntryPoint = "sshape_build_plane", CallingConvention = CallingConvention.Cdecl)]
public static extern sshape_buffer_t SshapeBuildPlane(in sshape_buffer_t buf, in sshape_plane_t parameters);
#endif
```

#### The _internal function declaration:
```csharp
[DllImport("sokol", EntryPoint = "sshape_build_plane_internal", CallingConvention = CallingConvention.Cdecl)]
public static extern void SshapeBuildPlane_internal(ref sshape_buffer_t result, in sshape_buffer_t buf, in sshape_plane_t parameters);
```

## Adding New Functions

To add a new function that returns a struct by value:

1. **Add to `web_wrapper_struct_return_functions` in `bindgen/gen_csharp.py`:**

```python
web_wrapper_struct_return_functions = {
    # ... existing entries
    'my_new_function': 'my_return_type_t',
}
```

2. **Run the binding generator:**

```bash
./scripts/generate-bindings.sh
```

3. **Done!** The system automatically:
   - Generates the C internal wrapper in `sokol_csharp_internal_wrappers.h`
   - Generates the C# WebAssembly wrapper code
   - Generates the C# _internal function declaration

## Benefits

✅ **No Manual C Code**: Never manually write internal wrapper functions again
✅ **Consistency**: All wrappers follow the same pattern
✅ **Maintainability**: Easy to add new functions - just update the dictionary
✅ **Type Safety**: Uses actual function signatures from IR, not manual typing
✅ **Automatic Updates**: Re-running the generator updates everything

## Files Involved

- **`bindgen/gen_csharp.py`**: Contains `web_wrapper_struct_return_functions` and generation logic
- **`bindgen/gen.py`**: Orchestrates generation and writes the header file
- **`ext/sokol_csharp_internal_wrappers.h`**: Auto-generated C header (DO NOT EDIT)
- **`ext/sokol.c`**: Includes the generated header
- **`src/sokol/generated/*.cs`**: Generated C# bindings with WebAssembly wrappers

## Currently Supported Functions

### sokol_glue.h (2 functions)
- `sglue_environment` → `sg_environment`
- `sglue_swapchain` → `sg_swapchain`

### sokol_debugtext.h (6 functions)
- `sdtx_font_kc853` → `sdtx_font_desc_t`
- `sdtx_font_kc854` → `sdtx_font_desc_t`
- `sdtx_font_z1013` → `sdtx_font_desc_t`
- `sdtx_font_cpc` → `sdtx_font_desc_t`
- `sdtx_font_c64` → `sdtx_font_desc_t`
- `sdtx_font_oric` → `sdtx_font_desc_t`

### sokol_shape.h (19 functions)
- **Build functions**: `sshape_build_plane`, `sshape_build_box`, `sshape_build_sphere`, `sshape_build_cylinder`, `sshape_build_torus`
- **Size functions**: `sshape_plane_sizes`, `sshape_box_sizes`, `sshape_sphere_sizes`, `sshape_cylinder_sizes`, `sshape_torus_sizes`
- **Descriptor functions**: `sshape_element_range`, `sshape_vertex_buffer_desc`, `sshape_index_buffer_desc`, `sshape_vertex_buffer_layout_state`
- **Attribute functions**: `sshape_position_vertex_attr_state`, `sshape_normal_vertex_attr_state`, `sshape_texcoord_vertex_attr_state`, `sshape_color_vertex_attr_state`
- **Matrix functions**: `sshape_mat4`, `sshape_mat4_transpose`

**Total: 27 functions** automatically wrapped for WebAssembly compatibility!
