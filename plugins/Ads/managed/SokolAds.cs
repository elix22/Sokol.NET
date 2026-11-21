#if __ANDROID__ || __IOS__
using System.Runtime.InteropServices;

internal static class SokolAds
{
#if __IOS__
    const string Lib = "@rpath/sokol_ads.framework/sokol_ads";
#else
    const string Lib = "sokol_ads";
#endif

    /* Mirrors sokolads_event in sokol_ads.h. */
    [StructLayout(LayoutKind.Sequential)]
    internal struct Event
    {
        public int type;
        public int code;
    }

    [DllImport(Lib, EntryPoint = "sokolads_init",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Init(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? appId,
        [MarshalAs(UnmanagedType.I1)] bool npaOnly,
        [MarshalAs(UnmanagedType.I1)] bool childDirected);

    [DllImport(Lib, EntryPoint = "sokolads_consent_gather",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ConsentGather();

    [DllImport(Lib, EntryPoint = "sokolads_load_interstitial",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void LoadInterstitial(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string adUnitId);

    [DllImport(Lib, EntryPoint = "sokolads_interstitial_ready",
               CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool InterstitialReady();

    [DllImport(Lib, EntryPoint = "sokolads_show_interstitial",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ShowInterstitial();

    [DllImport(Lib, EntryPoint = "sokolads_load_rewarded",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void LoadRewarded(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string adUnitId);

    [DllImport(Lib, EntryPoint = "sokolads_rewarded_ready",
               CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool RewardedReady();

    [DllImport(Lib, EntryPoint = "sokolads_show_rewarded",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ShowRewarded();

    [DllImport(Lib, EntryPoint = "sokolads_privacy_options_required",
               CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool PrivacyOptionsRequired();

    [DllImport(Lib, EntryPoint = "sokolads_show_privacy_options",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ShowPrivacyOptions();

    [DllImport(Lib, EntryPoint = "sokolads_poll_event",
               CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool PollEvent(out Event evt);
}
#endif
