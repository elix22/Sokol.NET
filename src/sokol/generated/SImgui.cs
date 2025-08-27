// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

using static Sokol.SApp;

namespace Sokol
{
public static unsafe partial class SImgui
{
public enum simgui_log_item_t
{
    SIMGUI_LOGITEM_OK,
    SIMGUI_LOGITEM_MALLOC_FAILED,
}
[StructLayout(LayoutKind.Sequential)]
public struct simgui_allocator_t
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct simgui_logger_t
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct simgui_desc_t
{
    public int max_vertices;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
    [M(U.LPUTF8Str)] public string ini_filename;
    [M(U.I1)] public bool no_default_font;
    [M(U.I1)] public bool disable_paste_override;
    [M(U.I1)] public bool disable_set_mouse_cursor;
    [M(U.I1)] public bool disable_windows_resize_from_edges;
    [M(U.I1)] public bool write_alpha_channel;
    public simgui_allocator_t allocator;
    public simgui_logger_t logger;
}
[StructLayout(LayoutKind.Sequential)]
public struct simgui_frame_desc_t
{
    public int width;
    public int height;
    public double delta_time;
    public float dpi_scale;
}
[StructLayout(LayoutKind.Sequential)]
public struct simgui_font_tex_desc_t
{
    public sg_filter min_filter;
    public sg_filter mag_filter;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_setup(in simgui_desc_t desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_new_frame", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_new_frame", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_new_frame(in simgui_frame_desc_t desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_render", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_render", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_render();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_imtextureid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_imtextureid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong simgui_imtextureid(sg_view tex_view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_imtextureid_with_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_imtextureid_with_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong simgui_imtextureid_with_sampler(sg_view tex_view, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_texture_view_from_imtextureid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_texture_view_from_imtextureid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_view simgui_texture_view_from_imtextureid(ulong imtex_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_sampler_from_imtextureid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_sampler_from_imtextureid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_sampler simgui_sampler_from_imtextureid(ulong imtex_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_focus_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_focus_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_focus_event(bool focus);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_mouse_pos_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_mouse_pos_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_mouse_pos_event(float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_touch_pos_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_touch_pos_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_touch_pos_event(float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_mouse_button_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_mouse_button_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_mouse_button_event(int mouse_button, bool down);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_mouse_wheel_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_mouse_wheel_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_mouse_wheel_event(float wheel_x, float wheel_y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_key_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_key_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_key_event(int imgui_key, bool down);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_input_character", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_input_character", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_input_character(uint c);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_input_characters_utf8", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_input_characters_utf8", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_input_characters_utf8([M(U.LPUTF8Str)] string c);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_add_touch_button_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_add_touch_button_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_add_touch_button_event(int mouse_button, bool down);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_handle_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_handle_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool simgui_handle_event(in sapp_event ev);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_map_keycode", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_map_keycode", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int simgui_map_keycode(sapp_keycode keycode);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "simgui_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "simgui_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void simgui_shutdown();

}
}
