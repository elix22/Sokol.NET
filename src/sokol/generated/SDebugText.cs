// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SDebugText
{
public enum sdtx_log_item_t
{
    SDTX_LOGITEM_OK,
    SDTX_LOGITEM_MALLOC_FAILED,
    SDTX_LOGITEM_ADD_COMMIT_LISTENER_FAILED,
    SDTX_LOGITEM_COMMAND_BUFFER_FULL,
    SDTX_LOGITEM_CONTEXT_POOL_EXHAUSTED,
    SDTX_LOGITEM_CANNOT_DESTROY_DEFAULT_CONTEXT,
}
[StructLayout(LayoutKind.Sequential)]
public struct sdtx_logger_t
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sdtx_context
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sdtx_range
{
    public void* ptr;
    public nuint size;
}
[StructLayout(LayoutKind.Sequential)]
public struct sdtx_font_desc_t
{
    public sdtx_range data;
    public byte first_char;
    public byte last_char;
}
[StructLayout(LayoutKind.Sequential)]
public struct sdtx_context_desc_t
{
    public int max_commands;
    public int char_buf_size;
    public float canvas_width;
    public float canvas_height;
    public int tab_width;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct sdtx_allocator_t
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sdtx_desc_t
{
    public int context_pool_size;
    public int printf_buf_size;
    #pragma warning disable 169
    public struct fontsCollection
    {
        public ref sdtx_font_desc_t this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sdtx_font_desc_t _item0;
        private sdtx_font_desc_t _item1;
        private sdtx_font_desc_t _item2;
        private sdtx_font_desc_t _item3;
        private sdtx_font_desc_t _item4;
        private sdtx_font_desc_t _item5;
        private sdtx_font_desc_t _item6;
        private sdtx_font_desc_t _item7;
    }
    #pragma warning restore 169
    public fontsCollection fonts;
    public sdtx_context_desc_t context;
    public sdtx_allocator_t allocator;
    public sdtx_logger_t logger;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_setup(in sdtx_desc_t desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_shutdown();

#if WEB
public static sdtx_font_desc_t sdtx_font_kc853()
{
    sdtx_font_desc_t result = default;
    sdtx_font_kc853_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_kc853", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_kc853", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sdtx_font_desc_t sdtx_font_kc853();
#endif

#if WEB
public static sdtx_font_desc_t sdtx_font_kc854()
{
    sdtx_font_desc_t result = default;
    sdtx_font_kc854_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_kc854", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_kc854", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sdtx_font_desc_t sdtx_font_kc854();
#endif

#if WEB
public static sdtx_font_desc_t sdtx_font_z1013()
{
    sdtx_font_desc_t result = default;
    sdtx_font_z1013_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_z1013", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_z1013", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sdtx_font_desc_t sdtx_font_z1013();
#endif

#if WEB
public static sdtx_font_desc_t sdtx_font_cpc()
{
    sdtx_font_desc_t result = default;
    sdtx_font_cpc_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_cpc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_cpc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sdtx_font_desc_t sdtx_font_cpc();
#endif

#if WEB
public static sdtx_font_desc_t sdtx_font_c64()
{
    sdtx_font_desc_t result = default;
    sdtx_font_c64_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_c64", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_c64", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sdtx_font_desc_t sdtx_font_c64();
#endif

#if WEB
public static sdtx_font_desc_t sdtx_font_oric()
{
    sdtx_font_desc_t result = default;
    sdtx_font_oric_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_oric", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_oric", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sdtx_font_desc_t sdtx_font_oric();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_make_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_make_context", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sdtx_make_context_internal(in sdtx_context_desc_t desc);
public static sdtx_context sdtx_make_context(in sdtx_context_desc_t desc)
{
    uint _id = sdtx_make_context_internal(desc);
    return new sdtx_context { id = _id };
}
#else
public static extern sdtx_context sdtx_make_context(in sdtx_context_desc_t desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_destroy_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_destroy_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_destroy_context(sdtx_context ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_set_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_set_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_set_context(sdtx_context ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_get_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_get_context", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sdtx_get_context_internal();
public static sdtx_context sdtx_get_context()
{
    uint _id = sdtx_get_context_internal();
    return new sdtx_context { id = _id };
}
#else
public static extern sdtx_context sdtx_get_context();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_default_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_default_context", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sdtx_default_context_internal();
public static sdtx_context sdtx_default_context()
{
    uint _id = sdtx_default_context_internal();
    return new sdtx_context { id = _id };
}
#else
public static extern sdtx_context sdtx_default_context();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_draw();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_context_draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_context_draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_context_draw(sdtx_context ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_draw_layer(int layer_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_context_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_context_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_context_draw_layer(sdtx_context ctx, int layer_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_layer(int layer_id);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_font(uint font_index);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_canvas", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_canvas", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_canvas(float w, float h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_origin", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_origin", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_origin(float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_home", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_home", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_home();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_pos", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_pos", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_pos(float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_pos_x", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_pos_x", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_pos_x(float x);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_pos_y", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_pos_y", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_pos_y(float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_move", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_move", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_move(float dx, float dy);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_move_x", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_move_x", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_move_x(float dx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_move_y", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_move_y", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_move_y(float dy);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_crlf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_crlf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_crlf();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_color3b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_color3b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_color3b(byte r, byte g, byte b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_color3f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_color3f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_color3f(float r, float g, float b);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_color4b", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_color4b", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_color4b(byte r, byte g, byte b, byte a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_color4f", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_color4f", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_color4f(float r, float g, float b, float a);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_color1i", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_color1i", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_color1i(uint rgba);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_putc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_putc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_putc(byte c);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_puts", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_puts", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_puts([M(U.LPUTF8Str)] string str);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_putr", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_putr", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_putr([M(U.LPUTF8Str)] string str, int len);

#if WEB
public static sdtx_range sdtx_get_cleared_fmt_buffer()
{
    sdtx_range result = default;
    sdtx_get_cleared_fmt_buffer_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_get_cleared_fmt_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_get_cleared_fmt_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sdtx_range sdtx_get_cleared_fmt_buffer();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_kc853_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_kc853_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_font_kc853_internal(ref sdtx_font_desc_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_kc854_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_kc854_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_font_kc854_internal(ref sdtx_font_desc_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_z1013_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_z1013_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_font_z1013_internal(ref sdtx_font_desc_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_cpc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_cpc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_font_cpc_internal(ref sdtx_font_desc_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_c64_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_c64_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_font_c64_internal(ref sdtx_font_desc_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_font_oric_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_font_oric_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_font_oric_internal(ref sdtx_font_desc_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sdtx_get_cleared_fmt_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sdtx_get_cleared_fmt_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sdtx_get_cleared_fmt_buffer_internal(ref sdtx_range result);

}
}
