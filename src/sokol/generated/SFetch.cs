// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class SFetch
{
public enum sfetch_log_item_t
{
    SFETCH_LOGITEM_OK,
    SFETCH_LOGITEM_MALLOC_FAILED,
    SFETCH_LOGITEM_FILE_PATH_UTF8_DECODING_FAILED,
    SFETCH_LOGITEM_SEND_QUEUE_FULL,
    SFETCH_LOGITEM_REQUEST_CHANNEL_INDEX_TOO_BIG,
    SFETCH_LOGITEM_REQUEST_PATH_IS_NULL,
    SFETCH_LOGITEM_REQUEST_PATH_TOO_LONG,
    SFETCH_LOGITEM_REQUEST_CALLBACK_MISSING,
    SFETCH_LOGITEM_REQUEST_CHUNK_SIZE_GREATER_BUFFER_SIZE,
    SFETCH_LOGITEM_REQUEST_USERDATA_PTR_IS_SET_BUT_USERDATA_SIZE_IS_NULL,
    SFETCH_LOGITEM_REQUEST_USERDATA_PTR_IS_NULL_BUT_USERDATA_SIZE_IS_NOT,
    SFETCH_LOGITEM_REQUEST_USERDATA_SIZE_TOO_BIG,
    SFETCH_LOGITEM_CLAMPING_NUM_CHANNELS_TO_MAX_CHANNELS,
    SFETCH_LOGITEM_REQUEST_POOL_EXHAUSTED,
}
[StructLayout(LayoutKind.Sequential)]
public struct sfetch_logger_t
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sfetch_range_t
{
    public void* ptr;
    public nuint size;
}
[StructLayout(LayoutKind.Sequential)]
public struct sfetch_allocator_t
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sfetch_desc_t
{
    public uint max_requests;
    public uint num_channels;
    public uint num_lanes;
    public sfetch_allocator_t allocator;
    public sfetch_logger_t logger;
}
[StructLayout(LayoutKind.Sequential)]
public struct sfetch_handle_t
{
    public uint id;
}
public enum sfetch_error_t
{
    SFETCH_ERROR_NO_ERROR,
    SFETCH_ERROR_FILE_NOT_FOUND,
    SFETCH_ERROR_NO_BUFFER,
    SFETCH_ERROR_BUFFER_TOO_SMALL,
    SFETCH_ERROR_UNEXPECTED_EOF,
    SFETCH_ERROR_INVALID_HTTP_STATUS,
    SFETCH_ERROR_CANCELLED,
    SFETCH_ERROR_JS_OTHER,
}
[StructLayout(LayoutKind.Sequential)]
public struct sfetch_response_t
{
    public sfetch_handle_t handle;
#if WEB
    private byte _dispatched;
    public bool dispatched { get => _dispatched != 0; set => _dispatched = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool dispatched;
#endif
#if WEB
    private byte _fetched;
    public bool fetched { get => _fetched != 0; set => _fetched = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool fetched;
#endif
#if WEB
    private byte _paused;
    public bool paused { get => _paused != 0; set => _paused = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool paused;
#endif
#if WEB
    private byte _finished;
    public bool finished { get => _finished != 0; set => _finished = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool finished;
#endif
#if WEB
    private byte _failed;
    public bool failed { get => _failed != 0; set => _failed = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool failed;
#endif
#if WEB
    private byte _cancelled;
    public bool cancelled { get => _cancelled != 0; set => _cancelled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool cancelled;
#endif
    public sfetch_error_t error_code;
    public uint channel;
    public uint lane;
    public IntPtr path;
    public void* user_data;
    public uint data_offset;
    public sfetch_range_t data;
    public sfetch_range_t buffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sfetch_request_t
{
    public uint channel;
#if WEB
    private IntPtr _path;
    public string path { get => Marshal.PtrToStringAnsi(_path);  set { if (_path != IntPtr.Zero) { Marshal.FreeHGlobal(_path); _path = IntPtr.Zero; } if (value != null) { _path = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string path;
#endif
    public delegate* unmanaged<sfetch_response_t*, void> callback;
    public uint chunk_size;
    public sfetch_range_t buffer;
    public sfetch_range_t user_data;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_setup(in sfetch_desc_t desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_shutdown();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sfetch_valid();

#if WEB
public static sfetch_desc_t sfetch_desc()
{
    sfetch_desc_t result = default;
    sfetch_desc_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sfetch_desc_t sfetch_desc();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_max_userdata_bytes", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_max_userdata_bytes", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sfetch_max_userdata_bytes();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_max_path", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_max_path", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sfetch_max_path();

#if WEB
public static sfetch_handle_t sfetch_send(in sfetch_request_t request)
{
    sfetch_handle_t result = default;
    sfetch_send_internal(ref result, request);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_send", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_send", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sfetch_handle_t sfetch_send(in sfetch_request_t request);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_handle_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_handle_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sfetch_handle_valid(sfetch_handle_t h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_dowork", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_dowork", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_dowork();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_bind_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_bind_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_bind_buffer(sfetch_handle_t h, sfetch_range_t buffer);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_unbind_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_unbind_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sfetch_unbind_buffer(sfetch_handle_t h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_cancel", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_cancel", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_cancel(sfetch_handle_t h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_pause", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_pause", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_pause(sfetch_handle_t h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_continue", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_continue", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_continue(sfetch_handle_t h);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_desc_internal(ref sfetch_desc_t result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfetch_send_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfetch_send_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfetch_send_internal(ref sfetch_handle_t result, in sfetch_request_t request);

}
}
