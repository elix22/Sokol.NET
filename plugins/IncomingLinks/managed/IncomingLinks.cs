#nullable enable
using System;
using System.Runtime.InteropServices;

/// <summary>
/// The link that opened the app: an Android App Link (<c>https://…</c> verified for the app's domain) or a
/// custom URL scheme, declared by the app in <c>platform/android/manifest/ActivityIntentFilters.xml</c>. A link
/// that starts the app (cold start) and one that arrives while it runs are both delivered.
///
/// Poll <see cref="TakePending"/> from the game loop (once per frame is cheap). Each link is returned once,
/// exactly as the system delivered it, fragment included. The plugin does not interpret it: the app checks the
/// shape it expects and ignores anything else — a link is input from outside the app.
/// </summary>
public static class IncomingLinks
{
    /// <summary>True when this platform has a backend (Android today).</summary>
#if __ANDROID__
    public static bool Supported => true;
#else
    public static bool Supported => false;
#endif

    /// <summary>The next link that opened the app, or null when there is none.</summary>
    public static string? TakePending()
    {
#if __ANDROID__
        IntPtr p = SokolIncomingLinks.Take();
        return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
#else
        return null;
#endif
    }
}
