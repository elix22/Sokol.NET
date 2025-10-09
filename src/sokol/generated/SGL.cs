// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SGL
{
public enum sgl_log_item_t
{
    SGL_LOGITEM_OK,
    SGL_LOGITEM_MALLOC_FAILED,
    SGL_LOGITEM_MAKE_PIPELINE_FAILED,
    SGL_LOGITEM_PIPELINE_POOL_EXHAUSTED,
    SGL_LOGITEM_ADD_COMMIT_LISTENER_FAILED,
    SGL_LOGITEM_CONTEXT_POOL_EXHAUSTED,
    SGL_LOGITEM_CANNOT_DESTROY_DEFAULT_CONTEXT,
}
[StructLayout(LayoutKind.Sequential)]
public struct sgl_logger_t
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgl_pipeline
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgl_context
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgl_error_t
{
#if WEB
    private byte _any;
    public bool any { get => _any != 0; set => _any = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool any;
#endif
#if WEB
    private byte _vertices_full;
    public bool vertices_full { get => _vertices_full != 0; set => _vertices_full = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool vertices_full;
#endif
#if WEB
    private byte _uniforms_full;
    public bool uniforms_full { get => _uniforms_full != 0; set => _uniforms_full = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool uniforms_full;
#endif
#if WEB
    private byte _commands_full;
    public bool commands_full { get => _commands_full != 0; set => _commands_full = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool commands_full;
#endif
#if WEB
    private byte _stack_overflow;
    public bool stack_overflow { get => _stack_overflow != 0; set => _stack_overflow = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool stack_overflow;
#endif
#if WEB
    private byte _stack_underflow;
    public bool stack_underflow { get => _stack_underflow != 0; set => _stack_underflow = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool stack_underflow;
#endif
#if WEB
    private byte _no_context;
    public bool no_context { get => _no_context != 0; set => _no_context = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool no_context;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sgl_context_desc_t
{
    public int max_vertices;
    public int max_commands;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgl_allocator_t
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgl_desc_t
{
    public int max_vertices;
    public int max_commands;
    public int context_pool_size;
    public int pipeline_pool_size;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
    public sg_face_winding face_winding;
    public sgl_allocator_t allocator;
    public sgl_logger_t logger;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_setup(in sgl_desc_t desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_shutdown();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_rad", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_rad", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float sgl_as_radians(float deg);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_deg", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_deg", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float sgl_as_degrees(float rad);

#if WEB
public static sgl_error_t sgl_get_error()
{
    sgl_error_t result = default;
    sgl_get_error_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_error", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_error", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgl_error_t sgl_get_error();
#endif

#if WEB
public static sgl_error_t sgl_context_error(sgl_context ctx)
{
    sgl_error_t result = default;
    sgl_context_error_internal(ref result, ctx);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_context_error", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_context_error", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgl_error_t sgl_context_error(sgl_context ctx);
#endif

#if WEB
public static sgl_context sgl_make_context(in sgl_context_desc_t desc)
{
    sgl_context result = default;
    sgl_make_context_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_make_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_make_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgl_context sgl_make_context(in sgl_context_desc_t desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_destroy_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_destroy_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_destroy_context(sgl_context ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_set_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_set_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_set_context(sgl_context ctx);

#if WEB
public static sgl_context sgl_get_context()
{
    sgl_context result = default;
    sgl_get_context_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_get_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_get_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgl_context sgl_get_context();
#endif

#if WEB
public static sgl_context sgl_default_context()
{
    sgl_context result = default;
    sgl_default_context_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_default_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_default_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgl_context sgl_default_context();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_num_vertices", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_num_vertices", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sgl_num_vertices();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_num_commands", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_num_commands", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sgl_num_commands();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_draw();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_context_draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_context_draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_context_draw(sgl_context ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_draw_layer(int layer_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_context_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_context_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_context_draw_layer(sgl_context ctx, int layer_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sgl_make_pipeline_internal(in sg_pipeline_desc desc);
public static sgl_pipeline sgl_make_pipeline(in sg_pipeline_desc desc)
{
    uint _id = sgl_make_pipeline_internal(desc);
    return new sgl_pipeline { id = _id };
}
#else
public static extern sgl_pipeline sgl_make_pipeline(in sg_pipeline_desc desc);
#endif

#if WEB
public static sgl_pipeline sgl_context_make_pipeline(sgl_context ctx, in sg_pipeline_desc desc)
{
    sgl_pipeline result = default;
    sgl_context_make_pipeline_internal(ref result, ctx, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_context_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_context_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sgl_pipeline sgl_context_make_pipeline(sgl_context ctx, in sg_pipeline_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_destroy_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_destroy_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_destroy_pipeline(sgl_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_defaults", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_defaults", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_defaults();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_viewport", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_viewport", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_viewport(int x, int y, int w, int h, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_viewportf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_viewportf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_viewportf(float x, float y, float w, float h, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_scissor_rect", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_scissor_rect", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_scissor_rect(int x, int y, int w, int h, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_scissor_rectf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_scissor_rectf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_scissor_rectf(float x, float y, float w, float h, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_enable_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_enable_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_enable_texture();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_disable_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_disable_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_disable_texture();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_texture(sg_view tex_view, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_layer(int layer_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_load_default_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_load_default_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_load_default_pipeline();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_load_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_load_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_load_pipeline(sgl_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_push_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_push_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_push_pipeline();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_pop_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_pop_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_pop_pipeline();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_matrix_mode_modelview", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_matrix_mode_modelview", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_matrix_mode_modelview();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_matrix_mode_projection", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_matrix_mode_projection", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_matrix_mode_projection();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_matrix_mode_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_matrix_mode_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_matrix_mode_texture();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_load_identity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_load_identity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_load_identity();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_load_matrix", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_load_matrix", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_load_matrix(in float m);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_load_transpose_matrix", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_load_transpose_matrix", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_load_transpose_matrix(in float m);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_mult_matrix", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_mult_matrix", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_mult_matrix(in float m);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_mult_transpose_matrix", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_mult_transpose_matrix", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_mult_transpose_matrix(in float m);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_rotate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_rotate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_rotate(float angle_rad, float x, float y, float z);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_scale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_scale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_scale(float x, float y, float z);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_translate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_translate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_translate(float x, float y, float z);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_frustum", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_frustum", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_frustum(float l, float r, float b, float t, float n, float f);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_ortho", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_ortho", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_ortho(float l, float r, float b, float t, float n, float f);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_perspective", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_perspective", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_perspective(float fov_y, float aspect, float z_near, float z_far);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_lookat", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_lookat", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_lookat(float eye_x, float eye_y, float eye_z, float center_x, float center_y, float center_z, float up_x, float up_y, float up_z);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_push_matrix", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_push_matrix", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_push_matrix();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_pop_matrix", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_pop_matrix", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_pop_matrix();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_t2f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_t2f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_t2f(float u, float v);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_c3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_c3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_c3f(float r, float g, float b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_c4f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_c4f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_c4f(float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_c3b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_c3b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_c3b(byte r, byte g, byte b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_c4b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_c4b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_c4b(byte r, byte g, byte b, byte a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_c1i", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_c1i", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_c1i(uint rgba);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_point_size", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_point_size", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_point_size(float s);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_begin_points", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_begin_points", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_begin_points();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_begin_lines", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_begin_lines", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_begin_lines();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_begin_line_strip", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_begin_line_strip", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_begin_line_strip();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_begin_triangles", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_begin_triangles", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_begin_triangles();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_begin_triangle_strip", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_begin_triangle_strip", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_begin_triangle_strip();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_begin_quads", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_begin_quads", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_begin_quads();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f(float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f(float x, float y, float z);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_t2f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_t2f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_t2f(float x, float y, float u, float v);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_t2f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_t2f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_t2f(float x, float y, float z, float u, float v);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_c3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_c3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_c3f(float x, float y, float r, float g, float b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_c3b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_c3b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_c3b(float x, float y, byte r, byte g, byte b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_c4f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_c4f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_c4f(float x, float y, float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_c4b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_c4b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_c4b(float x, float y, byte r, byte g, byte b, byte a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_c1i", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_c1i", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_c1i(float x, float y, uint rgba);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_c3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_c3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_c3f(float x, float y, float z, float r, float g, float b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_c3b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_c3b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_c3b(float x, float y, float z, byte r, byte g, byte b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_c4f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_c4f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_c4f(float x, float y, float z, float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_c4b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_c4b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_c4b(float x, float y, float z, byte r, byte g, byte b, byte a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_c1i", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_c1i", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_c1i(float x, float y, float z, uint rgba);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_t2f_c3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_t2f_c3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_t2f_c3f(float x, float y, float u, float v, float r, float g, float b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_t2f_c3b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_t2f_c3b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_t2f_c3b(float x, float y, float u, float v, byte r, byte g, byte b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_t2f_c4f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_t2f_c4f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_t2f_c4f(float x, float y, float u, float v, float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_t2f_c4b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_t2f_c4b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_t2f_c4b(float x, float y, float u, float v, byte r, byte g, byte b, byte a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v2f_t2f_c1i", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v2f_t2f_c1i", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v2f_t2f_c1i(float x, float y, float u, float v, uint rgba);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_t2f_c3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_t2f_c3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_t2f_c3f(float x, float y, float z, float u, float v, float r, float g, float b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_t2f_c3b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_t2f_c3b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_t2f_c3b(float x, float y, float z, float u, float v, byte r, byte g, byte b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_t2f_c4f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_t2f_c4f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_t2f_c4f(float x, float y, float z, float u, float v, float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_t2f_c4b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_t2f_c4b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_t2f_c4b(float x, float y, float z, float u, float v, byte r, byte g, byte b, byte a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_v3f_t2f_c1i", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_v3f_t2f_c1i", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_v3f_t2f_c1i(float x, float y, float z, float u, float v, uint rgba);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_end", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_end", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_end();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_error_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_error_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_get_error_internal(ref sgl_error_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_context_error_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_context_error_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_context_error_internal(ref sgl_error_t result, sgl_context ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_make_context_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_make_context_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_make_context_internal(ref sgl_context result, in sgl_context_desc_t desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_get_context_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_get_context_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_get_context_internal(ref sgl_context result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_default_context_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_default_context_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_default_context_internal(ref sgl_context result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgl_context_make_pipeline_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgl_context_make_pipeline_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgl_context_make_pipeline_internal(ref sgl_pipeline result, sgl_context ctx, in sg_pipeline_desc desc);

}
}
