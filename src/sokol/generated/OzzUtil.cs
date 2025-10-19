// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class OzzUtil
{
[StructLayout(LayoutKind.Sequential)]
public struct ozz_vertex_t
{
    #pragma warning disable 169
    public struct positionCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 3)[index];
        private float _item0;
        private float _item1;
        private float _item2;
    }
    #pragma warning restore 169
    public positionCollection position;
    public uint normal;
    public uint joint_indices;
    public uint joint_weights;
}
[StructLayout(LayoutKind.Sequential)]
public struct ozz_desc_t
{
    public int max_palette_joints;
    public int max_instances;
}
#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_setup(in ozz_desc_t desc);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_shutdown();

#if WEB
public static sg_image ozz_joint_texture()
{
    sg_image result = default;
    ozz_joint_texture_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image ozz_joint_texture();
#endif

#if WEB
public static sg_view ozz_joint_texture_view()
{
    sg_view result = default;
    ozz_joint_texture_view_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_texture_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_texture_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_view ozz_joint_texture_view();
#endif

#if WEB
public static sg_sampler ozz_joint_sampler()
{
    sg_sampler result = default;
    ozz_joint_sampler_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_sampler ozz_joint_sampler();
#endif

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_create_instance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_create_instance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern IntPtr ozz_create_instance(int index);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_destroy_instance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_destroy_instance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_destroy_instance(IntPtr ozz);

#if WEB
public static sg_buffer ozz_vertex_buffer(IntPtr ozz)
{
    sg_buffer result = default;
    ozz_vertex_buffer_internal(ref result, ozz);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_vertex_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_vertex_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer ozz_vertex_buffer(IntPtr ozz);
#endif

#if WEB
public static sg_buffer ozz_index_buffer(IntPtr ozz)
{
    sg_buffer result = default;
    ozz_index_buffer_internal(ref result, ozz);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_index_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_index_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer ozz_index_buffer(IntPtr ozz);
#endif

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_all_loaded", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_all_loaded", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool ozz_all_loaded(IntPtr ozz);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_load_failed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_load_failed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool ozz_load_failed(IntPtr ozz);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_load_skeleton", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_load_skeleton", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_load_skeleton(IntPtr ozz, void* data, nuint num_bytes);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_load_animation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_load_animation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_load_animation(IntPtr ozz, void* data, nuint num_bytes);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_load_mesh", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_load_mesh", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_load_mesh(IntPtr ozz, void* data, nuint num_bytes);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_set_load_failed", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_set_load_failed", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_set_load_failed(IntPtr ozz);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_update_instance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_update_instance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_update_instance(IntPtr ozz, double seconds);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_update_joint_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_update_joint_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_update_joint_texture();

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_texture_pixel_width", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_texture_pixel_width", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float ozz_joint_texture_pixel_width();

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_texture_u", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_texture_u", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float ozz_joint_texture_u(IntPtr ozz);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_texture_v", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_texture_v", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float ozz_joint_texture_v(IntPtr ozz);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_num_triangle_indices", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_num_triangle_indices", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int ozz_num_triangle_indices(IntPtr ozz);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_texture_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_texture_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_joint_texture_internal(ref sg_image result);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_texture_view_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_texture_view_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_joint_texture_view_internal(ref sg_view result);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_joint_sampler_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_joint_sampler_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_joint_sampler_internal(ref sg_sampler result);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_vertex_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_vertex_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_vertex_buffer_internal(ref sg_buffer result, IntPtr ozz);

#if __IOS__
[DllImport("@rpath/ozzutil.framework/ozzutil", EntryPoint = "ozz_index_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("ozzutil", EntryPoint = "ozz_index_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ozz_index_buffer_internal(ref sg_buffer result, IntPtr ozz);

}
}
