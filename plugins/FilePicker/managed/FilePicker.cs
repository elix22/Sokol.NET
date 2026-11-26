using System;
using System.IO;
using System.Threading;

/// <summary>How a pick ended.</summary>
public enum FilePickerStatus { Picked, Cancelled, Failed }

/// <summary>The outcome of <see cref="FilePicker.Open"/> or <see cref="FilePicker.Save"/>.</summary>
public sealed class FilePickerResult
{
    public FilePickerStatus Status { get; }
    /// <summary>The file's handle for <see cref="FilePicker.Read"/>, <see cref="FilePicker.Write"/> and the rest
    /// (Android: a content:// URI, Apple: a file URL, Windows / Linux: a path). Null unless picked.</summary>
    public string? Handle { get; }
    /// <summary>The file's name as the user sees it ("dungeon.map"). Null unless picked.</summary>
    public string? Name { get; }

    public FilePickerResult(FilePickerStatus status, string? handle = null, string? name = null)
    {
        Status = status;
        Handle = handle;
        Name = name;
    }
}

/// <summary>
/// The system's own file pickers: open a file the user picks, or save to a place the user picks — anywhere the
/// system's picker reaches (device storage, iCloud Drive, Google Drive, Dropbox, OneDrive, a USB stick, ... through
/// their document providers) — and keep access to it across launches for a recent-files list.
///
/// Backends: Android Storage Access Framework (ACTION_OPEN_DOCUMENT / ACTION_CREATE_DOCUMENT, persisted URI
/// permissions), iOS UIDocumentPickerViewController (security-scoped bookmarks), macOS NSOpenPanel / NSSavePanel,
/// Windows common file dialogs, Linux zenity or kdialog. The web has none (<see cref="Supported"/> is false).
///
/// <see cref="Open"/> and <see cref="Save"/> return at once; the answer reaches the callback from
/// <see cref="Update"/>, which the app calls once per frame on its game thread. <see cref="Read"/>,
/// <see cref="Write"/>, <see cref="Size"/> and <see cref="Modified"/> may block (a cloud provider downloads the
/// file first): call them from a worker thread. A file is a handle, not a path — never assume one is a path.
/// </summary>
public static class FilePicker
{
    static Action<FilePickerResult>? _pending;
#if !(__ANDROID__ || __IOS__ || __MACOS__)
    static volatile FilePickerResult? _desktopResult;   // Windows / Linux: set by the dialog's worker thread
#endif
    static readonly object _io = new();

    /// <summary>True when this platform has a picker (Linux: when zenity or kdialog is on the PATH).</summary>
#if __ANDROID__ || __IOS__ || __MACOS__ || __WINDOWS__
    public static bool Supported => true;
#elif __LINUX__
    public static bool Supported => LinuxDialog.Tool != null;
#else
    public static bool Supported => false;
#endif

    /// <summary>True while a picker is up.</summary>
    public static bool Busy => _pending != null;

    /// <summary>Show the picker for an existing file. <paramref name="extensions"/> ("map", "json"; none = any file)
    /// filter where the system can; Android and iOS show every file for a made-up extension, so check the
    /// picked name. False when a picker is already up or none can be shown (<paramref name="done"/> is not called).</summary>
    public static bool Open(string[] extensions, Action<FilePickerResult> done) => Start(0, extensions, null, null, done);

    /// <summary>Save <paramref name="data"/> as a new file the user names and places, starting from
    /// <paramref name="suggestedName"/>. The data is written before <paramref name="done"/> reports Picked; the
    /// handle then stands for the new file (later saves go through <see cref="Write"/>).</summary>
    public static bool Save(string suggestedName, byte[] data, string[] extensions, Action<FilePickerResult> done) =>
        Start(1, extensions, suggestedName, data, done);

    /// <summary>Delivers a finished pick to its callback. Call once per frame from the game thread.</summary>
    public static void Update()
    {
        if (_pending == null) return;
        FilePickerResult? r = null;
#if __ANDROID__ || __IOS__ || __MACOS__
        int s = SokolFilePicker.Poll();
        if (s == 1) r = new FilePickerResult(FilePickerStatus.Picked, SokolFilePicker.Str(SokolFilePicker.ResultHandle()), SokolFilePicker.Str(SokolFilePicker.ResultName()));
        else if (s == 2) r = new FilePickerResult(FilePickerStatus.Cancelled);
        else if (s == 3) r = new FilePickerResult(FilePickerStatus.Failed);
#else
        r = _desktopResult;
        _desktopResult = null;
#endif
        if (r == null) return;
        var done = _pending;
        _pending = null;
        done(r);
    }

    static bool Start(int mode, string[] extensions, string? name, byte[]? data, Action<FilePickerResult> done)
    {
        if (_pending != null || !Supported) return false;
        string ext = string.Join(";", extensions ?? Array.Empty<string>());
#if __ANDROID__ || __IOS__ || __MACOS__
        unsafe
        {
            fixed (byte* p = data)
                if (!SokolFilePicker.Start(mode, ext, name, p, data?.LongLength ?? 0)) return false;
        }
        _pending = done;
        return true;
#elif __WINDOWS__ || __LINUX__
        _pending = done;
        _desktopResult = null;
        var t = new Thread(() =>
        {
            FilePickerResult r;
            try
            {
#if __WINDOWS__
                string? path = WindowsDialog.Show(mode == 1, ext, name);
#else
                string? path = LinuxDialog.Show(mode == 1, ext, name);
#endif
                if (path == null) r = new FilePickerResult(FilePickerStatus.Cancelled);
                else
                {
                    if (mode == 1) File.WriteAllBytes(path, data ?? Array.Empty<byte>());
                    r = new FilePickerResult(FilePickerStatus.Picked, path, Path.GetFileName(path));
                }
            }
            catch { r = new FilePickerResult(FilePickerStatus.Failed); }
            _desktopResult = r;
        }) { IsBackground = true, Name = "FilePicker" };
#if __WINDOWS__
        if (OperatingSystem.IsWindows()) t.SetApartmentState(ApartmentState.STA);   // the common dialogs use COM
#endif
        t.Start();
        return true;
#else
        return false;
#endif
    }

    /// <summary>The file's whole content, or null when it cannot be read (gone, access lost). May block.</summary>
    public static byte[]? Read(string handle)
    {
        if (string.IsNullOrEmpty(handle)) return null;
#if __ANDROID__ || __IOS__ || __MACOS__
        lock (_io)
        {
            long n = SokolFilePicker.Load(handle);
            if (n < 0) return null;
            var bytes = new byte[n];
            unsafe { fixed (byte* p = bytes) SokolFilePicker.Take(p); }
            return bytes;
        }
#else
        try { return File.ReadAllBytes(handle); } catch { return null; }
#endif
    }

    /// <summary>Replaces the file's content. False when it cannot be written. May block.</summary>
    public static bool Write(string handle, byte[] data)
    {
        if (string.IsNullOrEmpty(handle) || data == null) return false;
#if __ANDROID__ || __IOS__ || __MACOS__
        lock (_io)
        {
            unsafe { fixed (byte* p = data) return SokolFilePicker.Write(handle, p, data.LongLength); }
        }
#else
        try { File.WriteAllBytes(handle, data); return true; } catch { return false; }
#endif
    }

    /// <summary>The file's size in bytes, or -1 when unknown or the file is gone.</summary>
    public static long Size(string handle)
    {
        if (string.IsNullOrEmpty(handle)) return -1;
#if __ANDROID__ || __IOS__ || __MACOS__
        lock (_io) return SokolFilePicker.Size(handle);
#else
        try { var f = new FileInfo(handle); return f.Exists ? f.Length : -1; } catch { return -1; }
#endif
    }

    /// <summary>When the file last changed, or null when unknown (some providers do not say) or the file is gone.
    /// Compare it with the value seen at open to tell whether someone else changed the file since.</summary>
    public static DateTimeOffset? Modified(string handle)
    {
        if (string.IsNullOrEmpty(handle)) return null;
#if __ANDROID__ || __IOS__ || __MACOS__
        long ms;
        lock (_io) ms = SokolFilePicker.Modified(handle);
        return ms < 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(ms);
#else
        try { var f = new FileInfo(handle); return f.Exists ? new DateTimeOffset(f.LastWriteTimeUtc) : null; } catch { return null; }
#endif
    }

    /// <summary>A token that keeps access to the file after a restart — store it in the recent-files list — or
    /// null when the system gives none.</summary>
    public static string? Bookmark(string handle)
    {
        if (string.IsNullOrEmpty(handle)) return null;
#if __ANDROID__ || __IOS__ || __MACOS__
        lock (_io) return SokolFilePicker.Str(SokolFilePicker.Bookmark(handle));
#else
        return handle;
#endif
    }

    /// <summary>The handle a <see cref="Bookmark"/> token stands for, or null when access was lost or the file is
    /// gone. Bookmark the handle again after a resolve to keep the token fresh (Apple renews moved files).</summary>
    public static string? Resolve(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
#if __ANDROID__ || __IOS__ || __MACOS__
        lock (_io) return SokolFilePicker.Str(SokolFilePicker.Resolve(token));
#else
        return File.Exists(token) ? token : null;
#endif
    }

#if __WINDOWS__
    /// <summary>Windows: GetOpenFileNameW / GetSaveFileNameW (comdlg32), run on the picker's own STA thread.</summary>
    static unsafe class WindowsDialog
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct OPENFILENAMEW
        {
            public int lStructSize;
            public IntPtr hwndOwner, hInstance, lpstrFilter, lpstrCustomFilter;
            public int nMaxCustFilter, nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public IntPtr lpstrInitialDir, lpstrTitle;
            public int Flags;
            public short nFileOffset, nFileExtension;
            public IntPtr lpstrDefExt, lCustData, lpfnHook, lpTemplateName, pvReserved;
            public int dwReserved, FlagsEx;
        }

        [System.Runtime.InteropServices.DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
        static extern int GetOpenFileName(OPENFILENAMEW* ofn);
        [System.Runtime.InteropServices.DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW")]
        static extern int GetSaveFileName(OPENFILENAMEW* ofn);

        const int OFN_OVERWRITEPROMPT = 0x2, OFN_NOCHANGEDIR = 0x8, OFN_PATHMUSTEXIST = 0x800,
                  OFN_FILEMUSTEXIST = 0x1000, OFN_EXPLORER = 0x80000;
        const int MaxPath = 32768;

        public static string? Show(bool save, string extensions, string? name)
        {
            // Filter: "Files (*.map;*.json)\0*.map;*.json\0All files\0*.*\0\0"
            string pattern = "";
            foreach (var e in extensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
                pattern += (pattern.Length > 0 ? ";" : "") + "*." + e;
            string filter = (pattern.Length > 0 ? pattern + "\0" + pattern + "\0" : "") + "*.*\0*.*\0\0";
            string? defExt = extensions.Split(';', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } a ? a[0] : null;

            char* file = (char*)System.Runtime.InteropServices.Marshal.AllocHGlobal(MaxPath * sizeof(char));
            try
            {
                new Span<char>(file, MaxPath).Clear();
                if (save && !string.IsNullOrEmpty(name)) name.AsSpan(0, Math.Min(name.Length, MaxPath - 1)).CopyTo(new Span<char>(file, MaxPath));
                fixed (char* f = filter)
                fixed (char* d = defExt)
                {
                    var ofn = new OPENFILENAMEW
                    {
                        lStructSize = sizeof(OPENFILENAMEW),
                        lpstrFilter = (IntPtr)f,
                        nFilterIndex = 1,
                        lpstrFile = (IntPtr)file,
                        nMaxFile = MaxPath,
                        lpstrDefExt = (IntPtr)d,
                        Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
                    };
                    int ok = save ? GetSaveFileName(&ofn) : GetOpenFileName(&ofn);
                    return ok != 0 ? new string(file) : null;
                }
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)file); }
        }
    }
#endif

#if __LINUX__
    /// <summary>Linux: zenity (GNOME) or kdialog (KDE), run as a process on the picker's thread.</summary>
    static class LinuxDialog
    {
        static bool _looked;
        static string? _tool;

        public static string? Tool
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    foreach (var t in new[] { "zenity", "kdialog" })
                        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
                            if (_tool == null && dir.Length > 0 && File.Exists(Path.Combine(dir, t))) _tool = t;
                }
                return _tool;
            }
        }

        public static string? Show(bool save, string extensions, string? name)
        {
            if (Tool == null) return null;
            string pattern = "";
            foreach (var e in extensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
                pattern += (pattern.Length > 0 ? " " : "") + "*." + e;
            var psi = new System.Diagnostics.ProcessStartInfo(Tool) { UseShellExecute = false, RedirectStandardOutput = true };
            if (Tool == "zenity")
            {
                psi.ArgumentList.Add("--file-selection");
                if (save)
                {
                    psi.ArgumentList.Add("--save");
                    psi.ArgumentList.Add("--confirm-overwrite");
                    if (!string.IsNullOrEmpty(name)) psi.ArgumentList.Add("--filename=" + name);
                }
                if (pattern.Length > 0) psi.ArgumentList.Add("--file-filter=" + pattern);
            }
            else
            {
                psi.ArgumentList.Add(save ? "--getsavefilename" : "--getopenfilename");
                psi.ArgumentList.Add(save && !string.IsNullOrEmpty(name) ? name : ".");
                if (pattern.Length > 0) psi.ArgumentList.Add(pattern);
            }
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return null;
            string output = p.StandardOutput.ReadToEnd().Trim('\n', '\r');
            p.WaitForExit();
            return p.ExitCode == 0 && output.Length > 0 ? output : null;
        }
    }
#endif
}
