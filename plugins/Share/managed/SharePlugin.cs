// High-level cross-platform share API.
// Call SharePlugin.ShareScore(score, gameName) when the player taps Share.
//
// Android / iOS / macOS: native share sheet with image + text.
// Windows             : MAPI — opens default mail client with image pre-attached.
// Linux               : xdg-email with --attach for image + text.
// Web                 : not supported (call sites guarded by #if !WEB).
//
// All file I/O uses Sokol.SFilesystem exclusively — no System.IO file access.
#if !WEB
using static Sokol.SFilesystem;
using System.Runtime.InteropServices;

public static unsafe class SharePlugin
{
    // Entry point — call at game-over when the player taps the Share button.
    public static void ShareScore(int score, string gameName = "Sokol.NET Game")
    {
        string imagePath = GenerateScoreCard(score, gameName);
        string text      = $"I scored {score:N0} pts in {gameName}! Can you beat me?";
        ShareImageAndText(imagePath, text);
    }

    // Overload that composites the score card overlay onto a caller-supplied RGB screenshot.
    // rgbPixels must be w*h*3 bytes (3 bytes per pixel, no alpha).
    public static void ShareScore(byte[] rgbPixels, int w, int h,
                                  int score, string gameName = "Sokol.NET Game")
    {
        string imagePath = GenerateScoreCard(rgbPixels, w, h, score, gameName);
        string text      = $"I scored {score:N0} pts in {gameName}! Can you beat me?";
        ShareImageAndText(imagePath, text);
    }

    // ── Image generation ──────────────────────────────────────────────────────

    static string GenerateScoreCard(int score, string gameName)
    {
        IntPtr dirPtr = sfs_get_temp_dir();
        string dir = Marshal.PtrToStringUTF8(dirPtr) ?? "";
        sfs_free_path(dirPtr);

        string path     = dir + "score_card.png";
        byte[] pngBytes = ScoreCardRenderer.Render(score, gameName);

        var file = sfs_open_file(path, sfs_open_mode_t.SFS_OPEN_CREATE_WRITE);
        if (file != IntPtr.Zero)
        {
            fixed (byte* p = pngBytes) sfs_write_file(file, p, pngBytes.Length);
            sfs_close_file(file);
        }
        return path;
    }

    static string GenerateScoreCard(byte[] rgbPixels, int w, int h, int score, string gameName)
    {
        IntPtr dirPtr = sfs_get_temp_dir();
        string dir = Marshal.PtrToStringUTF8(dirPtr) ?? "";
        sfs_free_path(dirPtr);

        string path     = dir + "score_card.png";
        byte[] pngBytes = ScoreCardRenderer.Render(rgbPixels, w, h, score, gameName,w,h);

        var file = sfs_open_file(path, sfs_open_mode_t.SFS_OPEN_CREATE_WRITE);
        if (file != IntPtr.Zero)
        {
            fixed (byte* p = pngBytes) sfs_write_file(file, p, pngBytes.Length);
            sfs_close_file(file);
        }
        return path;
    }

    // Share a caller-composed moment: an already-written PNG (or null for text-only) + text.
    // For apps that capture their own screenshot (e.g. Sokol.NET ScreenCapture) and compose
    // their own localized message, instead of the ShareScore card above.
    public static void ShareMoment(string? imagePath, string text)
    {
        if (imagePath == null)
        {
#if __ANDROID__ || __IOS__ || __MACOS__
            SokolShare.ShareImageText(null, text);
#endif
            // Windows/Linux fall back to mail clients that need an attachment; text-only is a no-op there.
            return;
        }
        ShareImageAndText(imagePath, text);
    }

    // Put text on the system clipboard, on the two platforms sokol_app.h does NOT
    // implement sapp_set_clipboard_string() for. Returns false everywhere else, so a
    // caller can fall back to sokol's own clipboard rather than silently doing nothing.
    public static bool SetClipboard(string text)
    {
#if __ANDROID__ || __IOS__
        SokolShare.SetClipboard(text);
        return true;
#else
        _ = text;
        return false;   // macOS/Windows/Linux: sapp_set_clipboard_string() already works
#endif
    }

    // Share one file the app has already written (an export, a document: PNG, JPEG, ZIP, JSON, ...) through the
    // system share sheet, keeping its name. Android and iOS ONLY (false elsewhere): show a Share button only
    // where CanShareFile is true.
    // mime:  e.g. "image/png", "application/zip" - Android offers the apps that take that type; iOS reads the type
    //        from the file's extension. null = "application/octet-stream".
    // title: the Android chooser's title (null = the system's; Android 10+ does not show it for a send).
    // Android: the file must be under the app's files folder (sfs_get_pref_path), which the plugin's FileProvider
    // exposes. Returns false when the file is missing or cannot be shared.
    public static bool ShareFile(string path, string? mime = null, string? title = null)
    {
#if __ANDROID__ || __IOS__
        return SokolShare.ShareFile(path, mime, title) != 0;
#else
        _ = path; _ = mime; _ = title;
        return false;
#endif
    }

    /// <summary>True on the platforms ShareFile works on (Android, iOS).</summary>
    public static bool CanShareFile =>
#if __ANDROID__ || __IOS__
        true;
#else
        false;
#endif

    // ── Platform dispatch ─────────────────────────────────────────────────────

    static void ShareImageAndText(string imagePath, string text)
    {
#if __ANDROID__ || __IOS__ || __MACOS__
        SokolShare.ShareImageText(imagePath, text);
#elif __WINDOWS__
        ShareWindows(imagePath);
#elif __LINUX__
        ShareLinux(imagePath, text);
#endif
    }

#if __WINDOWS__
    // MAPI: opens the default Windows mail client with the image pre-attached.
    // MAPISendDocuments is in mapi32.dll, present on all modern Windows.
    [DllImport("mapi32.dll", CharSet = CharSet.Ansi,
               BestFitMapping = false, ThrowOnUnmappableChar = true)]
    static extern int MAPISendDocuments(
        nint   ulUIParam,
        [MarshalAs(UnmanagedType.LPStr)] string lpszDelimChar,
        [MarshalAs(UnmanagedType.LPStr)] string lpszFilePaths,
        [MarshalAs(UnmanagedType.LPStr)] string lpszFileNames,
        uint   ulReserved);

    static void ShareWindows(string imagePath)
    {
        try
        {
            string fileName = Path.GetFileName(imagePath);
            int result = MAPISendDocuments(0, ";", imagePath, fileName, 0);
            if (result != 0)
                throw new Exception($"MAPI error {result}");
        }
        catch
        {
            // Fallback: reveal in Explorer so the user can attach manually.
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(
                        "explorer.exe", $"/select,\"{imagePath}\"")
                    { UseShellExecute = true });
            }
            catch { /* best-effort */ }
        }
    }
#endif

#if __LINUX__
    static void ShareLinux(string imagePath, string text)
    {
        try
        {
            // xdg-email opens the default mail client with an attachment and body pre-filled.
            var psi = new System.Diagnostics.ProcessStartInfo("xdg-email")
            {
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--attach");
            psi.ArgumentList.Add(imagePath);
            psi.ArgumentList.Add("--body");
            psi.ArgumentList.Add(text);
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            // Fallback: open the image in the system viewer.
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(imagePath)
                    { UseShellExecute = true });
            }
            catch { /* best-effort */ }
        }
    }
#endif
}
#endif  // !WEB
