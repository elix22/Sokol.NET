// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SShape
{
[StructLayout(LayoutKind.Sequential)]
public struct sshape_range
{
    public void* ptr;
    public nuint size;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_mat4_t
{
    #pragma warning disable 169
    public struct mCollection
    {
        public ref float this[int x, int y] { get { fixed (float* pTP = &_item0) return ref *(pTP + x + (y * 4)); } }
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
        private float _item4;
        private float _item5;
        private float _item6;
        private float _item7;
        private float _item8;
        private float _item9;
        private float _item10;
        private float _item11;
        private float _item12;
        private float _item13;
        private float _item14;
        private float _item15;
    }
    #pragma warning restore 169
    public mCollection m;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_vertex_t
{
    public float x;
    public float y;
    public float z;
    public uint normal;
    public ushort u;
    public ushort v;
    public uint color;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_element_range_t
{
    public uint base_element;
    public uint num_elements;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_sizes_item_t
{
    public uint num;
    public uint size;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_sizes_t
{
    public sshape_sizes_item_t vertices;
    public sshape_sizes_item_t indices;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_buffer_item_t
{
    public sshape_range buffer;
    public nuint data_size;
    public nuint shape_offset;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_buffer_t
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public sshape_buffer_item_t vertices;
    public sshape_buffer_item_t indices;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_plane_t
{
    public float width;
    public float depth;
    public ushort tiles;
    public uint color;
#if WEB
    private byte _random_colors;
    public bool random_colors { get => _random_colors != 0; set => _random_colors = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool random_colors;
#endif
#if WEB
    private byte _merge;
    public bool merge { get => _merge != 0; set => _merge = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool merge;
#endif
    public sshape_mat4_t transform;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_box_t
{
    public float width;
    public float height;
    public float depth;
    public ushort tiles;
    public uint color;
#if WEB
    private byte _random_colors;
    public bool random_colors { get => _random_colors != 0; set => _random_colors = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool random_colors;
#endif
#if WEB
    private byte _merge;
    public bool merge { get => _merge != 0; set => _merge = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool merge;
#endif
    public sshape_mat4_t transform;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_sphere_t
{
    public float radius;
    public ushort slices;
    public ushort stacks;
    public uint color;
#if WEB
    private byte _random_colors;
    public bool random_colors { get => _random_colors != 0; set => _random_colors = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool random_colors;
#endif
#if WEB
    private byte _merge;
    public bool merge { get => _merge != 0; set => _merge = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool merge;
#endif
    public sshape_mat4_t transform;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_cylinder_t
{
    public float radius;
    public float height;
    public ushort slices;
    public ushort stacks;
    public uint color;
#if WEB
    private byte _random_colors;
    public bool random_colors { get => _random_colors != 0; set => _random_colors = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool random_colors;
#endif
#if WEB
    private byte _merge;
    public bool merge { get => _merge != 0; set => _merge = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool merge;
#endif
    public sshape_mat4_t transform;
}
[StructLayout(LayoutKind.Sequential)]
public struct sshape_torus_t
{
    public float radius;
    public float ring_radius;
    public ushort sides;
    public ushort rings;
    public uint color;
#if WEB
    private byte _random_colors;
    public bool random_colors { get => _random_colors != 0; set => _random_colors = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool random_colors;
#endif
#if WEB
    private byte _merge;
    public bool merge { get => _merge != 0; set => _merge = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool merge;
#endif
    public sshape_mat4_t transform;
}
#if WEB
public static sshape_buffer_t sshape_build_plane(in sshape_buffer_t buf, in sshape_plane_t parameters)
{
    sshape_buffer_t result = default;
    sshape_build_plane_internal(ref result, buf, parameters);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_plane", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_plane", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_buffer_t sshape_build_plane(in sshape_buffer_t buf, in sshape_plane_t parameters);
#endif

#if WEB
public static sshape_buffer_t sshape_build_box(in sshape_buffer_t buf, in sshape_box_t parameters)
{
    sshape_buffer_t result = default;
    sshape_build_box_internal(ref result, buf, parameters);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_box", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_box", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_buffer_t sshape_build_box(in sshape_buffer_t buf, in sshape_box_t parameters);
#endif

#if WEB
public static sshape_buffer_t sshape_build_sphere(in sshape_buffer_t buf, in sshape_sphere_t parameters)
{
    sshape_buffer_t result = default;
    sshape_build_sphere_internal(ref result, buf, parameters);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_sphere", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_sphere", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_buffer_t sshape_build_sphere(in sshape_buffer_t buf, in sshape_sphere_t parameters);
#endif

#if WEB
public static sshape_buffer_t sshape_build_cylinder(in sshape_buffer_t buf, in sshape_cylinder_t parameters)
{
    sshape_buffer_t result = default;
    sshape_build_cylinder_internal(ref result, buf, parameters);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_cylinder", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_cylinder", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_buffer_t sshape_build_cylinder(in sshape_buffer_t buf, in sshape_cylinder_t parameters);
#endif

#if WEB
public static sshape_buffer_t sshape_build_torus(in sshape_buffer_t buf, in sshape_torus_t parameters)
{
    sshape_buffer_t result = default;
    sshape_build_torus_internal(ref result, buf, parameters);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_torus", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_torus", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_buffer_t sshape_build_torus(in sshape_buffer_t buf, in sshape_torus_t parameters);
#endif

#if WEB
public static sshape_sizes_t sshape_plane_sizes(uint tiles)
{
    sshape_sizes_t result = default;
    sshape_plane_sizes_internal(ref result, tiles);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_plane_sizes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_plane_sizes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_sizes_t sshape_plane_sizes(uint tiles);
#endif

#if WEB
public static sshape_sizes_t sshape_box_sizes(uint tiles)
{
    sshape_sizes_t result = default;
    sshape_box_sizes_internal(ref result, tiles);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_box_sizes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_box_sizes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_sizes_t sshape_box_sizes(uint tiles);
#endif

#if WEB
public static sshape_sizes_t sshape_sphere_sizes(uint slices, uint stacks)
{
    sshape_sizes_t result = default;
    sshape_sphere_sizes_internal(ref result, slices, stacks);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_sphere_sizes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_sphere_sizes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_sizes_t sshape_sphere_sizes(uint slices, uint stacks);
#endif

#if WEB
public static sshape_sizes_t sshape_cylinder_sizes(uint slices, uint stacks)
{
    sshape_sizes_t result = default;
    sshape_cylinder_sizes_internal(ref result, slices, stacks);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_cylinder_sizes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_cylinder_sizes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_sizes_t sshape_cylinder_sizes(uint slices, uint stacks);
#endif

#if WEB
public static sshape_sizes_t sshape_torus_sizes(uint sides, uint rings)
{
    sshape_sizes_t result = default;
    sshape_torus_sizes_internal(ref result, sides, rings);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_torus_sizes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_torus_sizes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_sizes_t sshape_torus_sizes(uint sides, uint rings);
#endif

#if WEB
public static sshape_element_range_t sshape_make_element_range(in sshape_buffer_t buf)
{
    sshape_element_range_t result = default;
    sshape_make_element_range_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_element_range", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_element_range", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_element_range_t sshape_make_element_range(in sshape_buffer_t buf);
#endif

#if WEB
public static sg_buffer_desc sshape_vertex_buffer_desc(in sshape_buffer_t buf)
{
    sg_buffer_desc result = default;
    sshape_vertex_buffer_desc_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_vertex_buffer_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_vertex_buffer_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer_desc sshape_vertex_buffer_desc(in sshape_buffer_t buf);
#endif

#if WEB
public static sg_buffer_desc sshape_index_buffer_desc(in sshape_buffer_t buf)
{
    sg_buffer_desc result = default;
    sshape_index_buffer_desc_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_index_buffer_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_index_buffer_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer_desc sshape_index_buffer_desc(in sshape_buffer_t buf);
#endif

#if WEB
public static sg_vertex_buffer_layout_state sshape_vertex_buffer_layout_state()
{
    sg_vertex_buffer_layout_state result = default;
    sshape_vertex_buffer_layout_state_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_vertex_buffer_layout_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_vertex_buffer_layout_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_vertex_buffer_layout_state sshape_vertex_buffer_layout_state();
#endif

#if WEB
public static sg_vertex_attr_state sshape_position_vertex_attr_state()
{
    sg_vertex_attr_state result = default;
    sshape_position_vertex_attr_state_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_position_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_position_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_vertex_attr_state sshape_position_vertex_attr_state();
#endif

#if WEB
public static sg_vertex_attr_state sshape_normal_vertex_attr_state()
{
    sg_vertex_attr_state result = default;
    sshape_normal_vertex_attr_state_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_normal_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_normal_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_vertex_attr_state sshape_normal_vertex_attr_state();
#endif

#if WEB
public static sg_vertex_attr_state sshape_texcoord_vertex_attr_state()
{
    sg_vertex_attr_state result = default;
    sshape_texcoord_vertex_attr_state_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_texcoord_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_texcoord_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_vertex_attr_state sshape_texcoord_vertex_attr_state();
#endif

#if WEB
public static sg_vertex_attr_state sshape_color_vertex_attr_state()
{
    sg_vertex_attr_state result = default;
    sshape_color_vertex_attr_state_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_color_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_color_vertex_attr_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_vertex_attr_state sshape_color_vertex_attr_state();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_color_4f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_color_4f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint sshape_color_4f(float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_color_3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_color_3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint sshape_color_3f(float r, float g, float b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_color_4b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_color_4b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint sshape_color_4b(byte r, byte g, byte b, byte a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_color_3b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_color_3b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint sshape_color_3b(byte r, byte g, byte b);

#if WEB
public static sshape_mat4_t sshape_make_mat4(in float m)
{
    sshape_mat4_t result = default;
    sshape_make_mat4_internal(ref result, m);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_mat4", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_mat4", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_mat4_t sshape_make_mat4(in float m);
#endif

#if WEB
public static sshape_mat4_t sshape_mat4_transpose(in float m)
{
    sshape_mat4_t result = default;
    sshape_mat4_transpose_internal(ref result, m);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_mat4_transpose", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_mat4_transpose", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sshape_mat4_t sshape_mat4_transpose(in float m);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_plane_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_plane_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_build_plane_internal(ref sshape_buffer_t result, in sshape_buffer_t buf, in sshape_plane_t parameters);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_box_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_box_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_build_box_internal(ref sshape_buffer_t result, in sshape_buffer_t buf, in sshape_box_t parameters);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_sphere_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_sphere_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_build_sphere_internal(ref sshape_buffer_t result, in sshape_buffer_t buf, in sshape_sphere_t parameters);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_cylinder_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_cylinder_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_build_cylinder_internal(ref sshape_buffer_t result, in sshape_buffer_t buf, in sshape_cylinder_t parameters);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_build_torus_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_build_torus_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_build_torus_internal(ref sshape_buffer_t result, in sshape_buffer_t buf, in sshape_torus_t parameters);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_plane_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_plane_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_plane_sizes_internal(ref sshape_sizes_t result, uint tiles);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_box_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_box_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_box_sizes_internal(ref sshape_sizes_t result, uint tiles);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_sphere_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_sphere_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_sphere_sizes_internal(ref sshape_sizes_t result, uint slices, uint stacks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_cylinder_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_cylinder_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_cylinder_sizes_internal(ref sshape_sizes_t result, uint slices, uint stacks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_torus_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_torus_sizes_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_torus_sizes_internal(ref sshape_sizes_t result, uint sides, uint rings);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_element_range_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_element_range_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_make_element_range_internal(ref sshape_element_range_t result, in sshape_buffer_t buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_vertex_buffer_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_vertex_buffer_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_vertex_buffer_desc_internal(ref sg_buffer_desc result, in sshape_buffer_t buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_index_buffer_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_index_buffer_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_index_buffer_desc_internal(ref sg_buffer_desc result, in sshape_buffer_t buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_vertex_buffer_layout_state_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_vertex_buffer_layout_state_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_vertex_buffer_layout_state_internal(ref sg_vertex_buffer_layout_state result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_position_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_position_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_position_vertex_attr_state_internal(ref sg_vertex_attr_state result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_normal_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_normal_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_normal_vertex_attr_state_internal(ref sg_vertex_attr_state result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_texcoord_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_texcoord_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_texcoord_vertex_attr_state_internal(ref sg_vertex_attr_state result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_color_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_color_vertex_attr_state_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_color_vertex_attr_state_internal(ref sg_vertex_attr_state result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_mat4_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_mat4_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_make_mat4_internal(ref sshape_mat4_t result, in float m);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sshape_mat4_transpose_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sshape_mat4_transpose_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sshape_mat4_transpose_internal(ref sshape_mat4_t result, in float m);

}
}
