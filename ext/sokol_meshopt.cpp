// sokol_meshopt.cpp -- the implementation of sokol_meshopt.h: EXT_meshopt_compression / KHR_meshopt_compression
// decoded with meshoptimizer (ext/meshoptimizer: vertexcodec.cpp, indexcodec.cpp, vertexfilter.cpp are compiled beside
// this file into the sokol library).
#define SOKOL_MESHOPT_IMPL
#if defined(_WIN32)
#define SOKOL_DLL
#endif
#include "sokol_meshopt.h"
#include "meshoptimizer/src/meshoptimizer.h"

static void smeshopt_apply_filter(void* buffer, size_t count, size_t stride, cgltf_meshopt_compression_filter filter)
{
	switch (filter)
	{
	case cgltf_meshopt_compression_filter_octahedral: meshopt_decodeFilterOct(buffer, count, stride); break;
	case cgltf_meshopt_compression_filter_quaternion: meshopt_decodeFilterQuat(buffer, count, stride); break;
	case cgltf_meshopt_compression_filter_exponential: meshopt_decodeFilterExp(buffer, count, stride); break;
	case cgltf_meshopt_compression_filter_color: meshopt_decodeFilterColor(buffer, count, stride); break;
	default: break;
	}
}

static bool smeshopt_filter_fits(cgltf_meshopt_compression_filter filter, size_t stride)
{
	// meshoptimizer asserts these; a file that breaks them is refused here instead
	switch (filter)
	{
	case cgltf_meshopt_compression_filter_none: return true;
	case cgltf_meshopt_compression_filter_octahedral: return stride == 4 || stride == 8;
	case cgltf_meshopt_compression_filter_quaternion: return stride == 8;
	case cgltf_meshopt_compression_filter_exponential: return stride > 0 && stride % 4 == 0;
	case cgltf_meshopt_compression_filter_color: return stride == 4 || stride == 8;
	default: return false;
	}
}

int smeshopt_decode_gltf(cgltf_data* data)
{
	int decoded = 0;

	for (cgltf_size i = 0; i < data->buffer_views_count; ++i)
	{
		cgltf_buffer_view* view = &data->buffer_views[i];

		if (!view->has_meshopt_compression || view->data)
			continue;

		const cgltf_meshopt_compression* mc = &view->meshopt_compression;

		if (!mc->buffer || !mc->buffer->data)
			return -1;

		if (mc->offset > mc->buffer->size || mc->size > mc->buffer->size - mc->offset)
			return -3;

		const unsigned char* source = (const unsigned char*)mc->buffer->data + mc->offset;
		size_t count = mc->count, stride = mc->stride;

		// the decoded bytes become the view's: they must cover the whole view, or an accessor would read past them
		if (stride == 0 || count > SIZE_MAX / stride || count * stride < view->size)
			return -3;

		// meshoptimizer's own preconditions (asserted there): checked so that a malformed file is an error, not a crash
		bool fits = false;
		switch (mc->mode)
		{
		case cgltf_meshopt_compression_mode_attributes: fits = stride % 4 == 0 && stride <= 256; break;
		case cgltf_meshopt_compression_mode_triangles: fits = (stride == 2 || stride == 4) && count % 3 == 0; break;
		case cgltf_meshopt_compression_mode_indices: fits = stride == 2 || stride == 4; break;
		default: return -4;
		}
		if (!fits || !smeshopt_filter_fits(mc->filter, stride))
			return mc->filter > cgltf_meshopt_compression_filter_color ? -4 : -3;

		void* result = data->memory.alloc_func(data->memory.user_data, count * stride > 0 ? count * stride : 1);
		if (!result)
			return -2;

		int rc = 0;
		switch (mc->mode)
		{
		case cgltf_meshopt_compression_mode_attributes: rc = meshopt_decodeVertexBuffer(result, count, stride, source, mc->size); break;
		case cgltf_meshopt_compression_mode_triangles: rc = meshopt_decodeIndexBuffer(result, count, stride, source, mc->size); break;
		case cgltf_meshopt_compression_mode_indices: rc = meshopt_decodeIndexSequence(result, count, stride, source, mc->size); break;
		default: break;
		}

		if (rc != 0)
		{
			data->memory.free_func(data->memory.user_data, result);
			return -3;
		}

		smeshopt_apply_filter(result, count, stride, mc->filter);

		view->data = result;
		decoded++;
	}

	return decoded;
}

size_t smeshopt_encode_vertex_buffer_bound(size_t vertex_count, size_t vertex_size)
{
	return meshopt_encodeVertexBufferBound(vertex_count, vertex_size);
}

size_t smeshopt_encode_vertex_buffer(unsigned char* buffer, size_t buffer_size, const void* vertices, size_t vertex_count, size_t vertex_size, int level, int version)
{
	if (vertex_size == 0 || vertex_size > 256 || vertex_size % 4 != 0 || level < 0 || level > 9 || version > 1)
		return 0;
	return meshopt_encodeVertexBufferLevel(buffer, buffer_size, vertices, vertex_count, vertex_size, level, version);
}

int smeshopt_decode_vertex_buffer(void* destination, size_t vertex_count, size_t vertex_size, const unsigned char* buffer, size_t buffer_size)
{
	if (vertex_size == 0 || vertex_size > 256 || vertex_size % 4 != 0)
		return -1;
	return meshopt_decodeVertexBuffer(destination, vertex_count, vertex_size, buffer, buffer_size);
}

size_t smeshopt_encode_index_buffer_bound(size_t index_count, size_t vertex_count)
{
	return meshopt_encodeIndexBufferBound(index_count, vertex_count);
}

size_t smeshopt_encode_index_buffer(unsigned char* buffer, size_t buffer_size, const uint32_t* indices, size_t index_count)
{
	if (index_count % 3 != 0)
		return 0;
	return meshopt_encodeIndexBuffer(buffer, buffer_size, indices, index_count);
}

int smeshopt_decode_index_buffer(void* destination, size_t index_count, size_t index_size, const unsigned char* buffer, size_t buffer_size)
{
	if (index_count % 3 != 0 || (index_size != 2 && index_size != 4))
		return -1;
	return meshopt_decodeIndexBuffer(destination, index_count, index_size, buffer, buffer_size);
}

size_t smeshopt_encode_index_sequence_bound(size_t index_count, size_t vertex_count)
{
	return meshopt_encodeIndexSequenceBound(index_count, vertex_count);
}

size_t smeshopt_encode_index_sequence(unsigned char* buffer, size_t buffer_size, const uint32_t* indices, size_t index_count)
{
	return meshopt_encodeIndexSequence(buffer, buffer_size, indices, index_count);
}

int smeshopt_decode_index_sequence(void* destination, size_t index_count, size_t index_size, const unsigned char* buffer, size_t buffer_size)
{
	if (index_size != 2 && index_size != 4)
		return -1;
	return meshopt_decodeIndexSequence(destination, index_count, index_size, buffer, buffer_size);
}

void smeshopt_encode_filter_oct(void* destination, size_t count, size_t stride, int bits, const float* data)
{
	if ((stride == 4 && bits >= 2 && bits <= 8) || (stride == 8 && bits >= 2 && bits <= 16))
		meshopt_encodeFilterOct(destination, count, stride, bits, data);
}

void smeshopt_encode_filter_quat(void* destination, size_t count, size_t stride, int bits, const float* data)
{
	if (stride == 8 && bits >= 4 && bits <= 16)
		meshopt_encodeFilterQuat(destination, count, stride, bits, data);
}

void smeshopt_encode_filter_exp(void* destination, size_t count, size_t stride, int bits, const float* data, int mode)
{
	if (stride > 0 && stride % 4 == 0 && stride <= 256 && bits >= 1 && bits <= 24 && mode >= 0 && mode <= 3)
		meshopt_encodeFilterExp(destination, count, stride, bits, data, (meshopt_EncodeExpMode)mode);
}

void smeshopt_encode_filter_color(void* destination, size_t count, size_t stride, int bits, const float* data)
{
	if ((stride == 4 && bits >= 2 && bits <= 8) || (stride == 8 && bits >= 2 && bits <= 16))
		meshopt_encodeFilterColor(destination, count, stride, bits, data);
}

void smeshopt_decode_filter(void* buffer, size_t count, size_t stride, cgltf_meshopt_compression_filter filter)
{
	if (smeshopt_filter_fits(filter, stride))
		smeshopt_apply_filter(buffer, count, stride, filter);
}
