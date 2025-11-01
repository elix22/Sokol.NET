# Sokol/Metal Uniform Buffer System Analysis

**Date:** November 1, 2025  
**Context:** Investigation into why KHR_materials_iridescence implementation failed on Metal backend

## Executive Summary

After extensive debugging of the iridescence feature implementation, we discovered a **fundamental architectural limitation** in how Sokol handles uniform buffers on the Metal backend. This limitation causes uniform data caching issues that make it extremely difficult (or impossible) to reliably pass per-mesh uniform data when rendering multiple objects with different uniform values in the same render pass.

**Conclusion:** The iridescence implementation was correctly written on both CPU (C#) and GPU (shader) sides, but failed due to Metal's aggressive uniform buffer offset caching in Sokol's single-buffer architecture.

---

## How Sokol/Metal Uniform Buffers Work

### 1. Single Shared Buffer Architecture

From `sokol_gfx.h` lines 15379-15403:

```c
_SOKOL_PRIVATE void _sg_mtl_bind_uniform_buffers(void) {
    // In the Metal backend, uniform buffer bindings happen once in sg_begin_pass() and
    // remain valid for the entire pass. Only binding offsets will be updated
    // in sg_apply_uniforms()
    if (_sg.cur_pass.is_compute) {
        // ... compute path omitted ...
    } else {
        SOKOL_ASSERT(nil != _sg.mtl.render_cmd_encoder);
        for (size_t slot = 0; slot < SG_MAX_UNIFORMBLOCK_BINDSLOTS; slot++) {
            [_sg.mtl.render_cmd_encoder
                setVertexBuffer:_sg.mtl.uniform_buffers[_sg.mtl.cur_frame_rotate_index]
                offset:0
                atIndex:slot];
            [_sg.mtl.render_cmd_encoder
                setFragmentBuffer:_sg.mtl.uniform_buffers[_sg.mtl.cur_frame_rotate_index]
                offset:0
                atIndex:slot];
        }
    }
}
```

**Key Design Decisions:**

1. **Single Uniform Buffer for ALL Slots**
   - Sokol allocates ONE shared uniform buffer per frame
   - This same buffer is bound to ALL uniform block slots (0-7)
   - Binding happens ONCE at pass start with `offset:0`
   - Bindings remain valid for the entire pass

2. **Offset-Based Updates**
   - When `sg_apply_uniforms(slot, data)` is called, data is copied to the shared buffer
   - Only the buffer **offset** is updated via `setVertexBufferOffset`/`setFragmentBufferOffset`
   - The offset advances for each subsequent uniform update

### 2. The `sg_apply_uniforms()` Implementation

From `sokol_gfx.h` lines 15968-16000:

```c
_SOKOL_PRIVATE void _sg_mtl_apply_uniforms(int ub_slot, const sg_range* data) {
    SOKOL_ASSERT((ub_slot >= 0) && (ub_slot < SG_MAX_UNIFORMBLOCK_BINDSLOTS));
    SOKOL_ASSERT(((size_t)_sg.mtl.cur_ub_offset + data->size) <= (size_t)_sg.mtl.ub_size);
    SOKOL_ASSERT((_sg.mtl.cur_ub_offset & (_SG_MTL_UB_ALIGN-1)) == 0);
    const _sg_pipeline_t* pip = _sg_pipeline_ref_ptr(&_sg.cur_pip);
    SOKOL_ASSERT(pip);
    const _sg_shader_t* shd = _sg_shader_ref_ptr(&pip->cmn.shader);
    SOKOL_ASSERT(data->size == shd->cmn.uniform_blocks[ub_slot].size);

    const sg_shader_stage stage = shd->cmn.uniform_blocks[ub_slot].stage;
    const NSUInteger mtl_slot = shd->mtl.ub_buffer_n[ub_slot];

    // copy to global uniform buffer, record offset into cmd encoder, and advance offset
    uint8_t* dst = &_sg.mtl.cur_ub_base_ptr[_sg.mtl.cur_ub_offset];
    memcpy(dst, data->ptr, data->size);
    if (stage == SG_SHADERSTAGE_VERTEX) {
        SOKOL_ASSERT(nil != _sg.mtl.render_cmd_encoder);
        [_sg.mtl.render_cmd_encoder setVertexBufferOffset:(NSUInteger)_sg.mtl.cur_ub_offset atIndex:mtl_slot];
        _sg_stats_add(metal.uniforms.num_set_vertex_buffer_offset, 1);
    } else if (stage == SG_SHADERSTAGE_FRAGMENT) {
        SOKOL_ASSERT(nil != _sg.mtl.render_cmd_encoder);
        [_sg.mtl.render_cmd_encoder setFragmentBufferOffset:(NSUInteger)_sg.mtl.cur_ub_offset atIndex:mtl_slot];
        _sg_stats_add(metal.uniforms.num_set_fragment_buffer_offset, 1);
    }
    // ... compute stage handling ...
    _sg.mtl.cur_ub_offset = _sg_roundup(_sg.mtl.cur_ub_offset + (int)data->size, _SG_MTL_UB_ALIGN);
}
```

**Process Flow:**
1. Copy uniform data to shared buffer at current offset
2. Update Metal command encoder's buffer offset for the specific slot
3. Advance offset for next uniform update

---

## The Fundamental Problem

### Root Cause: Metal's Offset Caching

**The Issue:** Metal's driver aggressively caches buffer offsets across draw calls, and `setVertexBufferOffset`/`setFragmentBufferOffset` calls do NOT guarantee immediate propagation to the GPU.

**When This Breaks:**

```
Frame Rendering Sequence:
1. sg_apply_pipeline(pipeline_16bit)      // Bind pipeline
2. sg_apply_uniforms(slot=1, mesh0_data)  // Set uniforms for Mesh 0 at offset 0
3. sg_draw(mesh0)                         // Draw Mesh 0
   
4. sg_apply_uniforms(slot=1, mesh1_data)  // Set uniforms for Mesh 1 at offset 256
5. sg_draw(mesh1)                         // Draw Mesh 1 - MAY USE OFFSET 0 DUE TO CACHING!
   
6. sg_apply_uniforms(slot=1, mesh2_data)  // Set uniforms for Mesh 2 at offset 512
7. sg_draw(mesh2)                         // Draw Mesh 2 - MAY USE OFFSET 0 OR 256!
```

**Why Metal Caches:**
- Metal optimizes by batching state changes
- Same pipeline + same buffer binding = potential for offset reuse
- Driver assumes offsets are stable unless explicitly invalidated
- No API guarantee that offset updates are immediately visible

### Observed Behavior in Iridescence Implementation

**What We Confirmed:**

1. ✅ **CPU-side values were CORRECT**
   ```
   [C# STRUCT] MeshIdx=0, irid_factor=0.000, irid_ior=1.300, irid_thick_min=100.0
   [C# STRUCT] MeshIdx=1, irid_factor=1.000, irid_ior=1.670, irid_thick_min=395.0
   [C# STRUCT] MeshIdx=2, irid_factor=1.000, irid_ior=1.800, irid_thick_min=485.0
   ```

2. ✅ **Shader code was CORRECT**
   - Physics-based thin-film interference calculation
   - Proper wavelength-dependent phase shifts
   - Correct Fresnel term application

3. ❌ **GPU received WRONG/STALE values**
   - Shader debug visualization showed only `iridescence_ior` was being read correctly
   - `iridescence_factor` and `iridescence_thickness_min/max` were reading as ZERO
   - Visual result: Rainbow colors appeared on WRONG meshes (Mesh 0 instead of Mesh 1/2)

4. ✅ **Diagnostic test confirmed caching issue**
   - Setting `iridescenceFactor = -999.0f` for Mesh 0 had NO EFFECT
   - Proved shader was NOT reading CPU-set values
   - Indicated GPU-level uniform buffer caching

---

## Why Standard Workarounds Failed

### Attempted Fixes (All Failed)

1. **❌ `sg_commit()` between passes**
   - Does not force Metal to flush buffer offset updates
   - Only commits command buffer, doesn't invalidate offset cache

2. **❌ Pipeline rebind before uniforms**
   - Still uses same underlying buffer binding
   - Offset cache persists across pipeline changes (when using same buffer)

3. **❌ Applying uniforms twice**
   ```csharp
   sg_apply_uniforms(UB_cgltf_metallic_params, SG_RANGE(ref metallicParams));
   sg_apply_uniforms(UB_cgltf_metallic_params, SG_RANGE(ref metallicParams));
   ```
   - Redundant - both calls write to same offset in shared buffer
   - Metal doesn't see this as a "different" update

4. **❌ Complete struct initialization**
   - Ensures no uninitialized data, but doesn't solve caching
   - Data IS being written correctly to CPU-side buffer

5. **❌ Disabling transmission two-pass**
   - Issue persisted in single-pass rendering
   - Proved it wasn't a pass-boundary problem

6. **⚠️ Forcing different pipeline variants (32-bit vs 16-bit indices)**
   - Attempted to force Metal to use different uniform buffer bindings
   - Partially successful (removed iridescence from wrong mesh)
   - BUT still didn't show iridescence on correct meshes
   - Different pipeline doesn't guarantee different buffer binding in Sokol

---

## Sokol Documentation on Metal Uniforms

From `sokol_gfx.h` lines 1205-1214:

> For the D3D11 and Metal backends, sokol-gfx only cares about the size of uniform
> blocks, but not about the internal layout. The data will just be copied into
> a uniform/constant buffer in a single operation and it's up you to arrange the
> CPU-side layout so that it matches the GPU side layout.

**Important Note:** This documentation describes struct layout requirements, but does NOT mention the single-buffer-with-offsets architecture or potential caching issues.

---

## Why This Architecture Was Chosen

### Sokol's Design Goals

1. **Performance Optimization**
   - Single large buffer allocation per frame
   - Minimize buffer binding changes
   - Reduce API calls by updating offsets only

2. **Cross-Platform Compatibility**
   - Works well on D3D11, WebGPU (which have similar offset-based uniform systems)
   - Simplified backend code

3. **Frame-Based Memory Management**
   - Triple-buffered uniform buffers
   - Linear allocation within frame
   - No per-object allocations

### Where It Works Well

✅ **Static or slowly-changing uniforms:**
- Camera matrices (once per frame)
- Light parameters (once per pass)
- Global material properties

✅ **Single-object rendering:**
- One object per pass
- Uniforms set once before draw

✅ **Predictable update patterns:**
- Uniforms that don't change between draws
- Same uniform values for multiple objects

### Where It Breaks

❌ **Per-object varying uniforms in same pass:**
- Multiple meshes with different material properties
- Each mesh needs unique uniform values
- Metal caching causes wrong values to be used

❌ **Fine-grained uniform updates:**
- Frequent uniform changes between draw calls
- Assumption that offset updates are immediately visible
- Reality: Metal batches and caches for performance

---

## Potential Solutions (Not Implemented)

### 1. **Separate Buffers Per Slot** ⚠️ Breaking Change
- Allocate separate uniform buffers for each slot (0-7)
- Bind different physical buffers to different slots
- **Pros:** Guaranteed isolation, no offset confusion
- **Cons:** Major Sokol architectural change, higher memory usage

### 2. **Use Metal Push Constants** ⚠️ Metal-Specific
```objc
[encoder setVertexBytes:&uniforms length:sizeof(uniforms) atIndex:slot];
[encoder setFragmentBytes:&uniforms length:sizeof(uniforms) atIndex:slot];
```
- **Pros:** Direct data copy, no caching issues, per-draw updates
- **Cons:** Limited size (4KB), Metal-only, requires Sokol modification

### 3. **Force Pipeline Changes** ⚠️ Workaround
- Create duplicate pipelines for materials with varying uniforms
- Different pipeline = forces Metal to re-evaluate bindings
- **Pros:** Can be done at application level
- **Cons:** Pipeline proliferation, maintenance burden, not guaranteed

### 4. **Texture-Based Parameters** ⚠️ Architecture Change
- Store per-object uniforms in textures
- Index into texture using vertex ID or instance ID
- **Pros:** Flexible, supports many objects
- **Cons:** Shader complexity, sampling overhead, not suitable for all data types

### 5. **Storage Buffers** ⚠️ Compute-Only
- Use storage buffers instead of uniform buffers
- **Pros:** Large capacity, structured data
- **Cons:** Only available in compute shaders, not supported on macOS+GL

---

## Recommendations for Future glTF Extensions

### ✅ Extensions That Work Well with Sokol/Metal

These extensions use **per-material constants** (set once, used for all draws of that material):

- `KHR_materials_emissive_strength` ✅ (Already implemented)
- `KHR_materials_transmission` ✅ (Already implemented)
- `KHR_materials_volume` ✅ (Already implemented)
- `KHR_materials_clearcoat`
- `KHR_materials_sheen`
- `KHR_materials_specular`

### ⚠️ Extensions with Potential Issues

These extensions may need **per-mesh or per-instance varying parameters**:

- `KHR_materials_iridescence` ⚠️ (FAILED - needs per-mesh factor/thickness)
- `KHR_materials_variants` ⚠️ (Switching materials per-object)
- Per-object animation parameters ⚠️

### Workaround Strategy

If you MUST implement extensions that need per-object parameters:

1. **Use Textures for Variation**
   - Store factor maps, parameter maps
   - Sample in shader based on UV coordinates
   - Example: `iridescence_factor_texture` instead of uniform factor

2. **Limit Variation to Materials, Not Instances**
   - Create separate material for each combination of parameters
   - Group objects by material
   - Trade memory for reliability

3. **Test Extensively on Metal**
   - Verify uniform updates work correctly
   - Test with multiple objects in same pass
   - Use debug visualization to confirm GPU receives correct values

---

## Lessons Learned

### 1. **Backend Differences Matter**
Even though Sokol abstracts backends, Metal's behavior differs significantly from D3D11/WebGPU:
- Metal aggressively caches uniform buffer offsets
- Offset updates via `setVertexBufferOffset` are NOT guaranteed immediate
- Same pipeline + same buffer = high cache retention

### 2. **Uniform Updates Are Not Guaranteed Synchronous**
The sequence:
```
sg_apply_uniforms(slot, data_A)
sg_draw(object_A)
sg_apply_uniforms(slot, data_B)
sg_draw(object_B)
```
Does NOT guarantee `object_B` sees `data_B` on Metal!

### 3. **Diagnostic Tests Are Critical**
Our breakthrough came from setting a sentinel value (`iridescenceFactor = -999.0f`) and observing NO change in output. This definitively proved the shader wasn't reading CPU-set values.

### 4. **When to Stop Debugging**
After confirming:
- ✅ CPU sets correct values (via logs)
- ✅ Shader code is correct (via physics validation)
- ✅ Diagnostic proves GPU doesn't read CPU values (via sentinel test)
- ❌ Multiple workarounds fail

**Conclusion:** The problem is architectural, not a bug in application code.

---

## References

- **Sokol GFX Source:** `/ext/sokol/sokol_gfx.h`
- **Key Functions:**
  - `_sg_mtl_bind_uniform_buffers()` (line 15379)
  - `_sg_mtl_apply_uniforms()` (line 15968)
- **Metal Documentation:** [Metal Best Practices Guide](https://developer.apple.com/metal/Metal-Best-Practices-Guide.pdf)
- **glTF Extensions:**
  - [KHR_materials_iridescence](https://github.com/KhronosGroup/glTF/tree/main/extensions/2.0/Khronos/KHR_materials_iridescence)

---

## Conclusion

The failure of the iridescence implementation was NOT due to incorrect code, but due to a fundamental limitation in Sokol's Metal backend uniform buffer architecture. The single-buffer-with-offsets design, while performant in many scenarios, does not guarantee reliable per-object uniform updates when rendering multiple objects with varying uniforms in the same pass.

**Decision:** Revert iridescence implementation and document this limitation for future reference.

**Impact:** Future glTF extensions requiring per-object varying uniform parameters should:
1. Use texture-based parameter storage
2. Create separate materials/pipelines per parameter combination
3. Test extensively on Metal before considering feature complete
