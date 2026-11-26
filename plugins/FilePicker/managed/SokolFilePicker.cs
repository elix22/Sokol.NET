#if __ANDROID__ || __IOS__ || __MACOS__
using System;
using System.Runtime.InteropServices;

internal static unsafe class SokolFilePicker
{
#if __IOS__
    const string Lib = "@rpath/sokol_filepicker.framework/sokol_filepicker";
#else
    const string Lib = "sokol_filepicker";
#endif

    [DllImport(Lib, EntryPoint = "sokolfilepicker_start", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Start(int mode, [MarshalAs(UnmanagedType.LPUTF8Str)] string extensions,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? name, byte* data, long len);

    [DllImport(Lib, EntryPoint = "sokolfilepicker_poll", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Poll();

    [DllImport(Lib, EntryPoint = "sokolfilepicker_result_handle", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ResultHandle();

    [DllImport(Lib, EntryPoint = "sokolfilepicker_result_name", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ResultName();

    [DllImport(Lib, EntryPoint = "sokolfilepicker_load", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long Load([MarshalAs(UnmanagedType.LPUTF8Str)] string handle);

    [DllImport(Lib, EntryPoint = "sokolfilepicker_take", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Take(byte* dst);

    [DllImport(Lib, EntryPoint = "sokolfilepicker_write", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Write([MarshalAs(UnmanagedType.LPUTF8Str)] string handle, byte* data, long len);

    [DllImport(Lib, EntryPoint = "sokolfilepicker_size", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long Size([MarshalAs(UnmanagedType.LPUTF8Str)] string handle);

    [DllImport(Lib, EntryPoint = "sokolfilepicker_modified", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long Modified([MarshalAs(UnmanagedType.LPUTF8Str)] string handle);

    [DllImport(Lib, EntryPoint = "sokolfilepicker_bookmark", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr Bookmark([MarshalAs(UnmanagedType.LPUTF8Str)] string handle);

    [DllImport(Lib, EntryPoint = "sokolfilepicker_resolve", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr Resolve([MarshalAs(UnmanagedType.LPUTF8Str)] string token);

    internal static string? Str(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
}
#endif
