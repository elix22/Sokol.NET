using System;

/// <summary>
/// Open a link with the system's own handler: a web URL in the default browser, a <c>mailto:</c> URL in
/// the mail app. Backends: Android intents (ACTION_VIEW / ACTION_SENDTO), iOS <c>UIApplication openURL</c>,
/// macOS <c>NSWorkspace</c>, web <c>window.open</c>, Windows <c>ShellExecute</c>, Linux <c>xdg-open</c>.
///
/// <see cref="Open"/> answers whether the system accepted the request; on false (no browser or mail app,
/// a blocked popup) the app should offer the link as text to copy. Only <c>http</c>, <c>https</c> and
/// <c>mailto</c> URLs are opened — anything else is refused, so a link built from data can never run a
/// script (<c>javascript:</c>) or open a local file. Call it from the tap that asked for it: a browser only
/// lets a page open a window shortly after a user gesture.
/// </summary>
public static class Links
{
    /// <summary>True when this platform has a backend at all (Linux: when <c>xdg-open</c> is on the PATH).</summary>
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__ || WEB
    public static bool Supported => true;
#elif __LINUX__
    public static bool Supported => XdgOpen.Installed;
#else
    public static bool Supported => false;
#endif

    /// <summary>Open <paramref name="url"/> (absolute <c>http</c>, <c>https</c> or <c>mailto</c>). Returns true when
    /// the system accepted it; iOS answers asynchronously, so there true means "handed to the system".</summary>
    public static bool Open(string url)
    {
        if (!IsAllowed(url)) return false;
#if __ANDROID__ || __IOS__ || __MACOS__ || WEB
        return SokolLinks.Open(url);
#elif __WINDOWS__
        try
        {
            // ShellExecute hands the URL to its registered handler; null = an already-running handler took it.
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch { return false; }   // no handler registered for the scheme
#elif __LINUX__
        return XdgOpen.Open(url);
#else
        return false;
#endif
    }

    static bool IsAllowed(string url) =>
        !string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var u)
        && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeMailto);

#if __LINUX__
    /// <summary>Linux: the freedesktop <c>xdg-open</c> tool, run as a process. It exits once the handler is
    /// launched; its result is not waited for (the game loop must not block).</summary>
    static class XdgOpen
    {
        static bool? _installed;

        public static bool Installed
        {
            get
            {
                if (_installed == null)
                {
                    _installed = false;
                    foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
                        if (dir.Length > 0 && System.IO.File.Exists(System.IO.Path.Combine(dir, "xdg-open"))) { _installed = true; break; }
                }
                return _installed.Value;
            }
        }

        public static bool Open(string url)
        {
            if (!Installed) return false;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("xdg-open") { UseShellExecute = false };
                psi.ArgumentList.Add(url);
                using var p = System.Diagnostics.Process.Start(psi);
                return p != null;
            }
            catch { return false; }
        }
    }
#endif
}
