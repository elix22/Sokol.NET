// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SGP
{
public enum sgp_error
{
    SGP_NO_ERROR = 0,
    SGP_ERROR_SOKOL_INVALID,
    SGP_ERROR_VERTICES_FULL,
    SGP_ERROR_UNIFORMS_FULL,
    SGP_ERROR_COMMANDS_FULL,
    SGP_ERROR_VERTICES_OVERFLOW,
    SGP_ERROR_TRANSFORM_STACK_OVERFLOW,
    SGP_ERROR_TRANSFORM_STACK_UNDERFLOW,
    SGP_ERROR_STATE_STACK_OVERFLOW,
    SGP_ERROR_STATE_STACK_UNDERFLOW,
    SGP_ERROR_ALLOC_FAILED,
    SGP_ERROR_MAKE_VERTEX_BUFFER_FAILED,
    SGP_ERROR_MAKE_WHITE_IMAGE_FAILED,
    SGP_ERROR_MAKE_NEAREST_SAMPLER_FAILED,
    SGP_ERROR_MAKE_COMMON_SHADER_FAILED,
    SGP_ERROR_MAKE_COMMON_PIPELINE_FAILED,
}
public enum sgp_blend_mode
{
    SGP_BLENDMODE_NONE = 0,
    SGP_BLENDMODE_BLEND,
    SGP_BLENDMODE_BLEND_PREMULTIPLIED,
    SGP_BLENDMODE_ADD,
    SGP_BLENDMODE_ADD_PREMULTIPLIED,
    SGP_BLENDMODE_MOD,
    SGP_BLENDMODE_MUL,
    _SGP_BLENDMODE_NUM,
}
public enum sgp_vs_attr_location
{
    SGP_VS_ATTR_COORD = 0,
    SGP_VS_ATTR_COLOR = 1,
}
public enum sgp_uniform_slot
{
    SGP_UNIFORM_SLOT_VERTEX = 0,
    SGP_UNIFORM_SLOT_FRAGMENT = 1,
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_isize
{
    public int w;
    public int h;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_irect
{
    public int x;
    public int y;
    public int w;
    public int h;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_rect
{
    public float x;
    public float y;
    public float w;
    public float h;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_textured_rect
{
    public sgp_rect dst;
    public sgp_rect src;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_vec2
{
    public float x;
    public float y;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_line
{
    public sgp_vec2 a;
    public sgp_vec2 b;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_triangle
{
    public sgp_vec2 a;
    public sgp_vec2 b;
    public sgp_vec2 c;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_mat2x3
{
    #pragma warning disable 169
    public struct vCollection
    {
        public ref float this[int x, int y] { get { fixed (float* pTP = &_item0) return ref *(pTP + x + (y * 2)); } }
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
        private float _item4;
        private float _item5;
    }
    #pragma warning restore 169
    public vCollection v;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_color
{
    public float r;
    public float g;
    public float b;
    public float a;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_color_ub4
{
    public byte r;
    public byte g;
    public byte b;
    public byte a;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_vertex
{
    public sgp_vec2 position;
    public sgp_vec2 texcoord;
    public sgp_color_ub4 color;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_uniform_data
{
    #pragma warning disable 169
    public struct floatsCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
        private float _item4;
        private float _item5;
        private float _item6;
        private float _item7;
    }
    #pragma warning restore 169
    public floatsCollection floats;
    #pragma warning disable 169
    public struct bytesCollection
    {
        public ref byte this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 32)[index];
        private byte _item0;
        private byte _item1;
        private byte _item2;
        private byte _item3;
        private byte _item4;
        private byte _item5;
        private byte _item6;
        private byte _item7;
        private byte _item8;
        private byte _item9;
        private byte _item10;
        private byte _item11;
        private byte _item12;
        private byte _item13;
        private byte _item14;
        private byte _item15;
        private byte _item16;
        private byte _item17;
        private byte _item18;
        private byte _item19;
        private byte _item20;
        private byte _item21;
        private byte _item22;
        private byte _item23;
        private byte _item24;
        private byte _item25;
        private byte _item26;
        private byte _item27;
        private byte _item28;
        private byte _item29;
        private byte _item30;
        private byte _item31;
    }
    #pragma warning restore 169
    public bytesCollection bytes;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_uniform
{
    public ushort vs_size;
    public ushort fs_size;
    public sgp_uniform_data data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_textures_uniform
{
    public uint count;
    #pragma warning disable 169
    public struct imagesCollection
    {
        public ref sg_image this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 4)[index];
        private sg_image _item0;
        private sg_image _item1;
        private sg_image _item2;
        private sg_image _item3;
    }
    #pragma warning restore 169
    public imagesCollection images;
    #pragma warning disable 169
    public struct samplersCollection
    {
        public ref sg_sampler this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 4)[index];
        private sg_sampler _item0;
        private sg_sampler _item1;
        private sg_sampler _item2;
        private sg_sampler _item3;
    }
    #pragma warning restore 169
    public samplersCollection samplers;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_state
{
    public sgp_isize frame_size;
    public sgp_irect viewport;
    public sgp_irect scissor;
    public sgp_mat2x3 proj;
    public sgp_mat2x3 transform;
    public sgp_mat2x3 mvp;
    public float thickness;
    public sgp_color_ub4 color;
    public sgp_textures_uniform textures;
    public sgp_uniform uniform;
    public sgp_blend_mode blend_mode;
    public sg_pipeline pipeline;
    public uint _base_vertex;
    public uint _base_uniform;
    public uint _base_command;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_desc
{
    public uint max_vertices;
    public uint max_commands;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgp_pipeline_desc
{
    public sg_shader shader;
    public sg_primitive_type primitive_type;
    public sgp_blend_mode blend_mode;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
#if WEB
    private byte _has_vs_color;
    public bool has_vs_color { get => _has_vs_color != 0; set => _has_vs_color = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool has_vs_color;
#endif
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_setup(in sgp_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_shutdown();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_is_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_is_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sgp_is_valid();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_get_last_error", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_get_last_error", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgp_error sgp_get_last_error();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_get_error_message", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_get_error_message", CallingConvention = CallingConvention.Cdecl)]
#endif
private static extern IntPtr sgp_get_error_message_native(sgp_error error);

public static string sgp_get_error_message(sgp_error error)
{
    IntPtr ptr = sgp_get_error_message_native(error);
    if (ptr == IntPtr.Zero)
        return "";

    // Manual UTF-8 to string conversion to avoid marshalling corruption
    try
    {
        return Marshal.PtrToStringUTF8(ptr) ?? "";
    }
    catch
    {
        // Fallback in case of any marshalling issues
        return "";
    }
}

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sgp_make_pipeline_internal(in sgp_pipeline_desc desc);
public static sg_pipeline sgp_make_pipeline(in sgp_pipeline_desc desc)
{
    uint _id = sgp_make_pipeline_internal(desc);
    return new sg_pipeline { id = _id };
}
#else
public static extern sg_pipeline sgp_make_pipeline(in sgp_pipeline_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_begin", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_begin", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_begin(int width, int height);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_flush", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_flush", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_flush();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_end", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_end", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_end();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_project", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_project", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_project(float left, float right, float top, float bottom);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_project", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_project", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_project();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_push_transform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_push_transform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_push_transform();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_pop_transform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_pop_transform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_pop_transform();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_transform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_transform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_transform();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_translate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_translate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_translate(float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_rotate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_rotate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_rotate(float theta);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_rotate_at", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_rotate_at", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_rotate_at(float theta, float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_scale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_scale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_scale(float sx, float sy);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_scale_at", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_scale_at", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_scale_at(float sx, float sy, float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_set_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_set_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_set_pipeline(sg_pipeline pipeline);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_pipeline();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_set_uniform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_set_uniform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_set_uniform(void* vs_data, uint vs_size, void* fs_data, uint fs_size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_uniform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_uniform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_uniform();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_set_blend_mode", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_set_blend_mode", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_set_blend_mode(sgp_blend_mode blend_mode);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_blend_mode", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_blend_mode", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_blend_mode();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_set_color", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_set_color", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_set_color(float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_color", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_color", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_color();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_set_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_set_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_set_image(int channel, sg_image image);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_unset_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_unset_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_unset_image(int channel);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_image(int channel);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_set_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_set_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_set_sampler(int channel, sg_sampler sampler);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_sampler(int channel);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_viewport", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_viewport", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_viewport(int x, int y, int w, int h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_viewport", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_viewport", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_viewport();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_scissor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_scissor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_scissor(int x, int y, int w, int h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_scissor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_scissor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_scissor();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_reset_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_reset_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_reset_state();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_clear", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_clear", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_clear();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw(sg_primitive_type primitive_type, in sgp_vertex vertices, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_points", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_points", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_points(in sgp_vec2 points, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_point", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_point", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_point(float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_lines", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_lines", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_lines(in sgp_line lines, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_line", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_line", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_line(float ax, float ay, float bx, float by);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_lines_strip", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_lines_strip", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_lines_strip(in sgp_vec2 points, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_filled_triangles", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_filled_triangles", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_filled_triangles(in sgp_triangle triangles, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_filled_triangle", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_filled_triangle", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_filled_triangle(float ax, float ay, float bx, float by, float cx, float cy);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_filled_triangles_strip", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_filled_triangles_strip", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_filled_triangles_strip(in sgp_vec2 points, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_filled_rects", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_filled_rects", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_filled_rects(in sgp_rect rects, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_filled_rect", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_filled_rect", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_filled_rect(float x, float y, float w, float h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_textured_rects", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_textured_rects", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_textured_rects(int channel, in sgp_textured_rect rects, uint count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_draw_textured_rect", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_draw_textured_rect", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgp_draw_textured_rect(int channel, sgp_rect dest_rect, sgp_rect src_rect);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_query_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_query_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ref sgp_state sgp_query_state();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgp_query_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgp_query_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgp_desc sgp_query_desc();

}
}
