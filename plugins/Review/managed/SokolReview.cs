#if __ANDROID__ || __IOS__
using System.Runtime.InteropServices;

internal static class SokolReview
{
#if __IOS__
    const string Lib = "@rpath/sokol_review.framework/sokol_review";
#else
    const string Lib = "sokol_review";
#endif

    [DllImport(Lib, EntryPoint = "sokolreview_request", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool Request();

    [DllImport(Lib, EntryPoint = "sokolreview_get_state", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetState();

    [DllImport(Lib, EntryPoint = "sokolreview_get_error", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetError();

    [DllImport(Lib, EntryPoint = "sokolreview_test_use_fake", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void TestUseFake([MarshalAs(UnmanagedType.I1)] bool on);
}
#endif
