#if __ANDROID__ || __IOS__ || __MACOS__ || WEB
using System.Runtime.InteropServices;

internal static class SokolLinks
{
#if __IOS__
    const string Lib = "@rpath/sokol_links.framework/sokol_links";
#else
    const string Lib = "sokol_links";
#endif

    [DllImport(Lib, EntryPoint = "sokollinks_open", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Open([MarshalAs(UnmanagedType.LPUTF8Str)] string url);
}
#endif
