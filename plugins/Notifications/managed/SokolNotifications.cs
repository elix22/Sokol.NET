#if __ANDROID__ || __IOS__
#nullable enable
using System;
using System.Runtime.InteropServices;

internal static class SokolNotifications
{
#if __IOS__
    const string Lib = "@rpath/sokol_notifications.framework/sokol_notifications";
#else
    const string Lib = "sokol_notifications";
#endif

    /* Mirrors sokolnotify_event in sokol_notifications.h. */
    [StructLayout(LayoutKind.Sequential)]
    internal struct Event
    {
        public int type;
        public int value;
        public long time;
    }

    [DllImport(Lib, EntryPoint = "sokolnotify_init", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Init();

    [DllImport(Lib, EntryPoint = "sokolnotify_configure", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Configure(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string channelName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? smallIconAsset);

    [DllImport(Lib, EntryPoint = "sokolnotify_status", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Status();

    [DllImport(Lib, EntryPoint = "sokolnotify_request_permission", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void RequestPermission();

    [DllImport(Lib, EntryPoint = "sokolnotify_schedule", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Schedule(int id, long fireUnix, long expireUnix,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string body);

    [DllImport(Lib, EntryPoint = "sokolnotify_cancel", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Cancel(int id);

    [DllImport(Lib, EntryPoint = "sokolnotify_cancel_all", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void CancelAll();

    [DllImport(Lib, EntryPoint = "sokolnotify_query_pending", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void QueryPending();

    [DllImport(Lib, EntryPoint = "sokolnotify_take_opened", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int TakeOpened();

    [DllImport(Lib, EntryPoint = "sokolnotify_open_settings", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void OpenSettings();

    [DllImport(Lib, EntryPoint = "sokolnotify_poll_event", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool PollEvent(out Event evt);
}
#endif
