#if __ANDROID__
using System.Runtime.InteropServices;

internal static class SokolAppUpdate
{
    const string Lib = "sokol_appupdate";

    /* Mirrors sokolappupdate_state in sokol_appupdate.h. */
    [StructLayout(LayoutKind.Sequential)]
    internal struct State
    {
        public int serial;
        public int availability;
        public int flexible_allowed;
        public int install_status;
        public int available_version_code;
        public int staleness_days;
        public int flow;
        public int error_code;
        public long bytes_downloaded;
        public long total_bytes;
    }

    [DllImport(Lib, EntryPoint = "sokolappupdate_init", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Init();

    [DllImport(Lib, EntryPoint = "sokolappupdate_check", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Check();

    [DllImport(Lib, EntryPoint = "sokolappupdate_start_flexible", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool StartFlexible();

    [DllImport(Lib, EntryPoint = "sokolappupdate_complete", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Complete();

    [DllImport(Lib, EntryPoint = "sokolappupdate_get_state", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GetState(out State state);

    [DllImport(Lib, EntryPoint = "sokolappupdate_test_use_fake", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void TestUseFake(int versionCode);

    [DllImport(Lib, EntryPoint = "sokolappupdate_test_fake", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void TestFake([MarshalAs(UnmanagedType.LPUTF8Str)] string action);

    [DllImport(Lib, EntryPoint = "sokolappupdate_test_fake_flags", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int TestFakeFlags();
}
#endif
