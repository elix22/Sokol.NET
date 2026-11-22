// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.CGltf;

namespace Sokol
{
public static unsafe partial class SMeshopt
{
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_decode_gltf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_decode_gltf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int smeshopt_decode_gltf(cgltf_data* data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_vertex_buffer_bound", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_vertex_buffer_bound", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint smeshopt_encode_vertex_buffer_bound(nuint vertex_count, nuint vertex_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_vertex_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_vertex_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint smeshopt_encode_vertex_buffer(byte* buffer, nuint buffer_size, void* vertices, nuint vertex_count, nuint vertex_size, int level, int version);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_decode_vertex_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_decode_vertex_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int smeshopt_decode_vertex_buffer(void* destination, nuint vertex_count, nuint vertex_size, in byte buffer, nuint buffer_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_index_buffer_bound", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_index_buffer_bound", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint smeshopt_encode_index_buffer_bound(nuint index_count, nuint vertex_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_index_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_index_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint smeshopt_encode_index_buffer(byte* buffer, nuint buffer_size, in uint indices, nuint index_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_decode_index_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_decode_index_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int smeshopt_decode_index_buffer(void* destination, nuint index_count, nuint index_size, in byte buffer, nuint buffer_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_index_sequence_bound", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_index_sequence_bound", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint smeshopt_encode_index_sequence_bound(nuint index_count, nuint vertex_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_index_sequence", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_index_sequence", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint smeshopt_encode_index_sequence(byte* buffer, nuint buffer_size, in uint indices, nuint index_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_decode_index_sequence", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_decode_index_sequence", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int smeshopt_decode_index_sequence(void* destination, nuint index_count, nuint index_size, in byte buffer, nuint buffer_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_filter_oct", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_filter_oct", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void smeshopt_encode_filter_oct(void* destination, nuint count, nuint stride, int bits, in float data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_filter_quat", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_filter_quat", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void smeshopt_encode_filter_quat(void* destination, nuint count, nuint stride, int bits, in float data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_filter_exp", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_filter_exp", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void smeshopt_encode_filter_exp(void* destination, nuint count, nuint stride, int bits, in float data, int mode);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_encode_filter_color", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_encode_filter_color", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void smeshopt_encode_filter_color(void* destination, nuint count, nuint stride, int bits, in float data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "smeshopt_decode_filter", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "smeshopt_decode_filter", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void smeshopt_decode_filter(void* buffer, nuint count, nuint stride, cgltf_meshopt_compression_filter filter);

}
}
