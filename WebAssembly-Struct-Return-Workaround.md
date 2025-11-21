# WebAssembly Struct Return Workaround

## Problem

WebAssembly/Emscripten cannot properly marshal structs returned by value through P/Invoke from native code to the .NET runtime. Functions like:

```c
sg_environment sglue_environment(void);
sdtx_font_desc_t sdtx_font_kc853(void);
```

Will fail or return corrupted data when called from C# on WebAssembly builds.

## Solution

We use a two-part approach:

### 1. C Side: Helper Functions

All `_internal` helper functions are implemented in `ext/sokol.c` (which is not part of the upstream sokol repository). These functions take a pointer parameter instead of returning by value:

```c
void sglue_environment_internal(sg_environment* env) {
    *env = sglue_environment();
}

void sdtx_font_kc853_internal(sdtx_font_desc_t* desc) {
    *desc = sdtx_font_kc853();
}
```

### 2. C# Side: Conditional Compilation

The C# binding generator (`bindgen/gen_csharp.py`) automatically generates platform-specific wrappers:

```csharp
#if WEB
public static sg_environment sglue_environment()
{
    sg_environment result = default;
    sglue_environment_internal(ref result);
    return result;
}
#else
public static extern sg_environment sglue_environment();
#endif

// DllImport for the _internal function
[DllImport("sokol", EntryPoint = "sglue_environment_internal", ...)]
public static extern void sglue_environment_internal(ref sg_environment result);
```

## Configuration

Functions requiring this workaround are listed in `bindgen/gen_csharp.py`:

```python
web_wrapper_struct_return_functions = {
    'sglue_swapchain': 'sg_swapchain',
    'sglue_environment': 'sg_environment',
    'sdtx_font_kc853': 'sdtx_font_desc_t',
    'sdtx_font_kc854': 'sdtx_font_desc_t',
    'sdtx_font_z1013': 'sdtx_font_desc_t',
    'sdtx_font_cpc': 'sdtx_font_desc_t',
    'sdtx_font_c64': 'sdtx_font_desc_t',
    'sdtx_font_oric': 'sdtx_font_desc_t'
}
```

## Adding New Functions

To add a new function that returns a struct by value:

1. **Add C Implementation** in `ext/sokol.c`:
   ```c
   void my_function_internal(my_struct_t* result) {
       *result = my_function();
   }
   ```

2. **Register in Python** - Add to `web_wrapper_struct_return_functions` in `bindgen/gen_csharp.py`:
   ```python
   'my_function': 'my_struct_t',
   ```

3. **Regenerate Bindings**:
   ```bash
   ./scripts/generate-bindings.sh
   ```

The C# binding generator will automatically:
- Create the `#if WEB` wrapper function
- Generate the DllImport for `my_function_internal()`
- Handle the conditional compilation

## Architecture Benefits

1. **Upstream Clean**: All sokol header files remain unmodified, making upstream merges trivial
2. **Centralized**: All C# binding-specific code is in `ext/sokol.c`
3. **Automated**: The Python generator handles all the C# boilerplate
4. **Transparent**: Application code calls the same API on all platforms

## Files Involved

- `ext/sokol.c` - All `_internal` helper functions
- `bindgen/gen_csharp.py` - Binding generator with WebAssembly handling
- `src/sokol/generated/*.cs` - Auto-generated C# bindings

## Testing

The workaround is automatically tested by:
1. Building native libraries for all platforms
2. Building the C# library
3. Running WebAssembly examples that use these functions
