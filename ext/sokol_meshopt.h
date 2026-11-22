#ifndef SOKOL_MESHOPT_INCLUDED
#define SOKOL_MESHOPT_INCLUDED
/*
    sokol_meshopt.h -- EXT_meshopt_compression / KHR_meshopt_compression for cgltf

    Project URL: https://github.com/elix22/Sokol.NET

    cgltf parses the extension (cgltf_buffer_view.has_meshopt_compression, .meshopt_compression) but leaves the
    decoding to the application: a compressed buffer view's bytes are only usable once they are decoded into
    cgltf_buffer_view.data, which cgltf's accessor functions (cgltf_accessor_read_*, cgltf_accessor_unpack_*,
    cgltf_buffer_view_data) then read instead of the buffer. This module does that with meshoptimizer's own
    decoders (ext/meshoptimizer, MIT; their NEON / SSE / WASM SIMD paths where the compiler has them), and gives
    the encoders for a tool that writes such files.

    HOW TO USE
    ==========
    Parse the file, give every buffer its bytes (cgltf_load_buffers or your own loading), then:

        if (smeshopt_decode_gltf(data) < 0) { ... a compressed view could not be decoded ... }

    before reading any accessor. The decoded bytes are allocated with data->memory.alloc_func and freed by
    cgltf_free with the rest of the file. A file without the extension costs a loop over its buffer views.

    The implementation is C++ (meshoptimizer's), in sokol_meshopt.cpp; the functions are plain C.

    LICENSE
    =======
    zlib/libpng license, as sokol. meshoptimizer: MIT, Copyright (c) 2016-2026 Arseny Kapoulkine.
*/
#include <stddef.h>
#include <stdint.h>
#include "cgltf.h"

#if defined(SOKOL_API_DECL) && !defined(SOKOL_MESHOPT_API_DECL)
#define SOKOL_MESHOPT_API_DECL SOKOL_API_DECL
#endif
#ifndef SOKOL_MESHOPT_API_DECL
#if defined(_WIN32) && defined(SOKOL_DLL) && defined(SOKOL_MESHOPT_IMPL)
#define SOKOL_MESHOPT_API_DECL __declspec(dllexport)
#elif defined(_WIN32) && defined(SOKOL_DLL)
#define SOKOL_MESHOPT_API_DECL __declspec(dllimport)
#else
#define SOKOL_MESHOPT_API_DECL extern
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Decodes every compressed buffer view of a parsed file whose buffers are loaded (and not decoded yet) into
   buffer_view->data. Returns the number of views decoded, or a negative value: -1 a view's compressed buffer has no
   bytes (not loaded), -2 out of memory, -3 the data is malformed, -4 an unknown mode or filter. */
SOKOL_MESHOPT_API_DECL int smeshopt_decode_gltf(cgltf_data* data);

/* meshoptimizer's codecs (meshopt_* in meshoptimizer.h) for a tool that writes compressed files. */

/* meshopt_encodeVertexBufferBound / meshopt_encodeVertexBufferLevel (level 0..3; version -1 for the default, 1);
   the encoded size, 0 when buffer_size is too small. vertex_size: a multiple of 4, at most 256. */
SOKOL_MESHOPT_API_DECL size_t smeshopt_encode_vertex_buffer_bound(size_t vertex_count, size_t vertex_size);
SOKOL_MESHOPT_API_DECL size_t smeshopt_encode_vertex_buffer(unsigned char* buffer, size_t buffer_size, const void* vertices, size_t vertex_count, size_t vertex_size, int level, int version);
/* meshopt_decodeVertexBuffer: 0 on success, negative for malformed data. */
SOKOL_MESHOPT_API_DECL int smeshopt_decode_vertex_buffer(void* destination, size_t vertex_count, size_t vertex_size, const unsigned char* buffer, size_t buffer_size);

/* meshopt_encodeIndexBuffer (triangles; may rotate a triangle's vertices, never its winding) and
   meshopt_encodeIndexSequence (any index list, order kept), their bounds and decoders (index_size 2 or 4). */
SOKOL_MESHOPT_API_DECL size_t smeshopt_encode_index_buffer_bound(size_t index_count, size_t vertex_count);
SOKOL_MESHOPT_API_DECL size_t smeshopt_encode_index_buffer(unsigned char* buffer, size_t buffer_size, const uint32_t* indices, size_t index_count);
SOKOL_MESHOPT_API_DECL int smeshopt_decode_index_buffer(void* destination, size_t index_count, size_t index_size, const unsigned char* buffer, size_t buffer_size);
SOKOL_MESHOPT_API_DECL size_t smeshopt_encode_index_sequence_bound(size_t index_count, size_t vertex_count);
SOKOL_MESHOPT_API_DECL size_t smeshopt_encode_index_sequence(unsigned char* buffer, size_t buffer_size, const uint32_t* indices, size_t index_count);
SOKOL_MESHOPT_API_DECL int smeshopt_decode_index_sequence(void* destination, size_t index_count, size_t index_size, const unsigned char* buffer, size_t buffer_size);

/* The filters, encoded from 4 floats per element (oct: unit vectors, quat: unit quaternions, color: RGBA 0..1;
   exp: stride / 4 floats, mode 0 separate, 1 shared vector, 2 shared component, 3 clamped) and decoded in place. */
SOKOL_MESHOPT_API_DECL void smeshopt_encode_filter_oct(void* destination, size_t count, size_t stride, int bits, const float* data);
SOKOL_MESHOPT_API_DECL void smeshopt_encode_filter_quat(void* destination, size_t count, size_t stride, int bits, const float* data);
SOKOL_MESHOPT_API_DECL void smeshopt_encode_filter_exp(void* destination, size_t count, size_t stride, int bits, const float* data, int mode);
SOKOL_MESHOPT_API_DECL void smeshopt_encode_filter_color(void* destination, size_t count, size_t stride, int bits, const float* data);
SOKOL_MESHOPT_API_DECL void smeshopt_decode_filter(void* buffer, size_t count, size_t stride, cgltf_meshopt_compression_filter filter);

#ifdef __cplusplus
} /* extern "C" */
#endif

#endif /* SOKOL_MESHOPT_INCLUDED */
