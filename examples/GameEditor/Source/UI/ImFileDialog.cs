// Pure C# file/folder picker dialog rendered with ImGui, backed by .NET IO APIs.
// Inspired by aiekick/ImGuiFileDialog — same static API style.
//
// Usage (open):
//   ImFileDialog.OpenDialog("key", "Title", "Scene Files{.scene.json},All Files{.*}", startPath);
//   ImFileDialog.OpenDialog("key", "Title", null, startPath, "", ImFileDialog.Mode.SelectFolder);
//
// Usage (display inside your ImGui frame):
//   if (ImFileDialog.Display("key"))
//   {
//       if (ImFileDialog.IsOk()) path = ImFileDialog.GetFilePathName();
//       ImFileDialog.Close();
//   }

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using static Imgui.ImguiNative;
using Imgui;

namespace GameEditor.UI
{
    public static unsafe class ImFileDialog
    {
        // ── Public API ─────────────────────────────────────────────────────

        public enum Mode { OpenFile, SaveFile, SelectFolder }

        /// <summary>
        /// Open the dialog.
        /// <paramref name="filters"/> format: "Display{.ext1,.ext2},Display2{.*}" or null for all files.
        /// Simple forms like ".scene.json" or ".json,.scene.json" are also accepted.
        /// </summary>
        public static void OpenDialog(string key, string title, string? filters,
            string startPath, string defaultName = "", Mode mode = Mode.OpenFile)
        {
            if (_isOpen && _key == key) return;

            _key       = key;
            _title     = title;
            _mode      = mode;
            _wasOk     = false;
            _resultPath = "";
            _filterIdx  = 0;
            _selectedEntry = -1;

            ParseFilters(filters);

            // Resolve starting directory
            string dir;
            if (!string.IsNullOrEmpty(startPath))
            {
                if (Directory.Exists(startPath))
                    dir = startPath;
                else if (File.Exists(startPath))
                    dir = Path.GetDirectoryName(startPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                else
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            else
            {
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            NavigateTo(dir, addToHistory: false);

            // Populate filename buffer
            string fname = defaultName;
            if (string.IsNullOrEmpty(fname) && !string.IsNullOrEmpty(startPath) && File.Exists(startPath))
                fname = Path.GetFileName(startPath);
            FillBuffer(ref _nameBuf, fname);

            _searchBuf = new byte[256];

            if (!_quickAccessLoaded) LoadQuickAccess();

            _isOpen = true;
        }

        /// <summary>
        /// Render the dialog for <paramref name="key"/>. Returns true when the dialog
        /// has finished (user pressed OK or Cancel / closed the window).
        /// Call <see cref="IsOk"/> and <see cref="GetFilePathName"/> before <see cref="Close"/>.
        /// </summary>
        public static bool Display(string key)
        {
            if (!_isOpen || _key != key) return false;

            var vp     = igGetMainViewport();
            var center = new Vector2(vp->Pos.X + vp->Size.X * 0.5f,
                                     vp->Pos.Y + vp->Size.Y * 0.5f);

            igSetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
            igSetNextWindowSize(new Vector2(820, 520), ImGuiCond.Appearing);
            igSetNextWindowSizeConstraints(new Vector2(520, 360), new Vector2(9999f, 9999f), null, null);

            byte dlgOpen = 1;
            bool visible  = igBegin(_title + "##ifd_" + _key, ref dlgOpen,
                ImGuiWindowFlags.NoCollapse    |
                ImGuiWindowFlags.NoDocking     |
                ImGuiWindowFlags.NoSavedSettings);

            if (dlgOpen == 0) { _isOpen = false; igEnd(); return true; }

            if (visible) DrawContents();

            igEnd();

            return !_isOpen; // finished when closed internally
        }

        public static bool   IsOk()            => _wasOk;
        public static string GetFilePathName() => _resultPath;
        public static string GetCurrentPath()  => _currentDir;
        public static void   Close()           { _isOpen = false; _wasOk = false; }

        // ── State ──────────────────────────────────────────────────────────

        private static bool   _isOpen;
        private static string _key    = "";
        private static string _title  = "";
        private static Mode   _mode;
        private static bool   _wasOk;
        private static string _resultPath = "";

        // Filters
        private static string[] _filterNames      = Array.Empty<string>();
        private static string[] _filterExtensions = Array.Empty<string>();
        private static int      _filterIdx;

        // Navigation
        private static string         _currentDir = "";
        private static readonly List<string> _history   = new();
        private static int            _historyPos = -1;

        // Directory listing
        private static readonly List<(string Name, bool IsDir)> _entries = new();
        private static int  _selectedEntry = -1;
        private static bool _needsRefresh  = true;

        // Input buffers
        private static byte[] _pathBuf   = new byte[1024];
        private static byte[] _nameBuf   = new byte[512];
        private static byte[] _searchBuf = new byte[256];

        // Quick-access bookmarks
        private static readonly List<string> _quickAccess    = new();
        private static bool                  _quickAccessLoaded;

        // ── Rendering ─────────────────────────────────────────────────────

        private static void DrawContents()
        {
            DrawNavBar();
            igSeparator();

            float lineH    = igGetTextLineHeightWithSpacing();
            float bottomH  = lineH * 1.5f + 12f;  // filename row
            float sepH     = 4f;

            Vector2 avail = default;
            igGetContentRegionAvail(ref avail);
            float midH = avail.Y - bottomH - sepH - 2f;
            if (midH < 60f) midH = 60f;

            // ── Left: bookmarks ───────────────────────────────────────────
            igBeginChild_Str("##ifd_left", new Vector2(155, midH), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
            DrawQuickAccess();
            igEndChild();

            igSameLine(0, 3);

            // ── Right: file list ──────────────────────────────────────────
            igBeginChild_Str("##ifd_right", new Vector2(0, midH), ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
            DrawFileList();
            igEndChild();

            igSeparator();
            DrawBottomBar();
        }

        private static void DrawNavBar()
        {
            // Back
            igBeginDisabled(_historyPos <= 0);
            if (igButton("<##ifd_back", new Vector2(22, 0))) GoBack();
            igEndDisabled();
            igSameLine(0, 2);

            // Forward
            igBeginDisabled(_historyPos >= _history.Count - 1);
            if (igButton(">##ifd_fwd", new Vector2(22, 0))) GoForward();
            igEndDisabled();
            igSameLine(0, 2);

            // Up
            igBeginDisabled(IsRootDir(_currentDir));
            if (igButton("^##ifd_up", new Vector2(22, 0))) GoUp();
            igEndDisabled();
            igSameLine(0, 6);

            // Path input — pressing Enter navigates to typed path
            igSetNextItemWidth(-120f);
            if (igInputText("##ifd_path", ref _pathBuf[0], (uint)_pathBuf.Length,
                    ImGuiInputTextFlags.EnterReturnsTrue, null, null))
            {
                string typed = GetStr(_pathBuf);
                if (Directory.Exists(typed))
                    NavigateTo(typed);
                else
                    FillBuffer(ref _pathBuf, _currentDir); // revert
            }

            igSameLine(0, 6);
            igText("Search:");
            igSameLine(0, 3);
            igSetNextItemWidth(-1f);
            bool searchChanged = igInputText("##ifd_search", ref _searchBuf[0], (uint)_searchBuf.Length,
                ImGuiInputTextFlags.None, null, null);
            if (searchChanged) _selectedEntry = -1;
        }

        private static void DrawQuickAccess()
        {
            igTextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "Quick Access");
            igSeparator();

            foreach (var loc in _quickAccess)
            {
                string displayName = Path.GetFileName(loc);
                if (string.IsNullOrEmpty(displayName)) displayName = loc; // e.g. "/" on Linux

                bool isSelected = string.Equals(_currentDir, loc, StringComparison.OrdinalIgnoreCase);
                if (igSelectable_Bool(displayName + "##qa_" + loc, isSelected,
                        ImGuiSelectableFlags.None, Vector2.Zero))
                    NavigateTo(loc);
            }
        }

        private static void DrawFileList()
        {
            if (_needsRefresh) RefreshEntries();

            string searchLower = GetStr(_searchBuf).ToLowerInvariant();

            for (int i = 0; i < _entries.Count; i++)
            {
                var (entryName, isDir) = _entries[i];

                if (!string.IsNullOrEmpty(searchLower) &&
                    !entryName.ToLowerInvariant().Contains(searchLower))
                    continue;

                bool isSelected = i == _selectedEntry;

                // Colour: yellow-ish for directories, light grey for files
                var col = isDir
                    ? new Vector4(1f, 0.87f, 0.4f, 1f)
                    : new Vector4(0.9f, 0.9f, 0.9f, 1f);

                string prefix = isDir ? "[+] " : "    ";

                igPushStyleColor_Vec4(ImGuiCol.Text, col);
                bool clicked = igSelectable_Bool(prefix + entryName + "##ifd_e" + i, isSelected,
                    ImGuiSelectableFlags.AllowDoubleClick, Vector2.Zero);
                igPopStyleColor(1);

                if (clicked)
                {
                    bool dbl = igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left);
                    _selectedEntry = i;

                    if (isDir)
                    {
                        if (dbl)
                            NavigateTo(Path.Combine(_currentDir, entryName));
                        else if (_mode == Mode.SelectFolder)
                            FillBuffer(ref _nameBuf, entryName);
                    }
                    else
                    {
                        FillBuffer(ref _nameBuf, entryName);
                        if (dbl) ConfirmSelection();
                    }
                }
            }
        }

        private static void DrawBottomBar()
        {
            igSpacing();

            // Label
            string nameLabel = _mode == Mode.SelectFolder ? "Folder:" : "File:";
            igText(nameLabel);
            igSameLine(0, 4);

            // Compute widths
            float filterW = _filterNames.Length > 1 ? 170f : 0f;
            float btnsW   = 80f + 80f + 8f; // OK + Cancel
            if (filterW > 0) btnsW += filterW + 4f;
            btnsW += 8f; // spacing buffer

            Vector2 avail = default;
            igGetContentRegionAvail(ref avail);
            float labelW = 48f;
            float nameW  = MathF.Max(avail.X - labelW - btnsW - 4f, 100f);

            igSetNextItemWidth(nameW);
            if (igInputText("##ifd_name", ref _nameBuf[0], (uint)_nameBuf.Length,
                    ImGuiInputTextFlags.EnterReturnsTrue, null, null))
                ConfirmSelection();

            // Filter combo
            if (_filterNames.Length > 1)
            {
                igSameLine(0, 4);
                igSetNextItemWidth(filterW);
                string preview = _filterIdx < _filterNames.Length ? _filterNames[_filterIdx] : "";
                if (igBeginCombo("##ifd_filter", preview, ImGuiComboFlags.None))
                {
                    for (int f = 0; f < _filterNames.Length; f++)
                    {
                        bool sel = f == _filterIdx;
                        if (igSelectable_Bool(_filterNames[f] + "##flt" + f, sel,
                                ImGuiSelectableFlags.None, Vector2.Zero))
                        {
                            if (_filterIdx != f) { _filterIdx = f; _needsRefresh = true; }
                        }
                    }
                    igEndCombo();
                }
            }

            igSameLine(0, 8);
            string okLabel = _mode switch
            {
                Mode.SaveFile     => "Save",
                Mode.SelectFolder => "Select",
                _                 => "Open"
            };

            if (igButton(okLabel + "##ifd_ok", new Vector2(80, 0)))
                ConfirmSelection();

            igSameLine(0, 4);
            if (igButton("Cancel##ifd_cancel", new Vector2(80, 0)))
            {
                _isOpen = false;
                _wasOk  = false;
            }
        }

        // ── Logic ──────────────────────────────────────────────────────────

        private static void ConfirmSelection()
        {
            if (_mode == Mode.SelectFolder)
            {
                // Selected folder is the current dir (or sub-folder typed in name box)
                string typed = GetStr(_nameBuf);
                if (!string.IsNullOrEmpty(typed))
                {
                    string candidate = Path.IsPathRooted(typed)
                        ? typed
                        : Path.Combine(_currentDir, typed);
                    if (Directory.Exists(candidate))
                    {
                        _resultPath = candidate;
                        _wasOk = true;
                        _isOpen = false;
                        return;
                    }
                }
                _resultPath = _currentDir;
                _wasOk = true;
                _isOpen = false;
                return;
            }

            string name = GetStr(_nameBuf);
            if (string.IsNullOrEmpty(name)) return;

            // If user typed an absolute path that's a directory, navigate there
            if (Path.IsPathRooted(name) && Directory.Exists(name))
            {
                NavigateTo(name);
                return;
            }

            string fullPath = Path.IsPathRooted(name)
                ? name
                : Path.Combine(_currentDir, name);

            // For open mode the file must exist
            if (_mode == Mode.OpenFile && !File.Exists(fullPath)) return;

            // For save mode, auto-append the active filter extension if missing
            if (_mode == Mode.SaveFile &&
                _filterExtensions.Length > 0 &&
                _filterIdx < _filterExtensions.Length)
            {
                string ext = _filterExtensions[_filterIdx];
                if (ext != ".*" && !fullPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    // Handle compound extensions like ".scene.json"
                    string extNoDot = ext.TrimStart('.');
                    if (!fullPath.EndsWith(extNoDot, StringComparison.OrdinalIgnoreCase))
                        fullPath += ext;
                }
            }

            _resultPath = fullPath;
            _wasOk  = true;
            _isOpen = false;
        }

        private static void RefreshEntries()
        {
            _entries.Clear();
            _selectedEntry = -1;
            _needsRefresh  = false;

            if (!Directory.Exists(_currentDir)) return;

            var dirList  = new List<string>();
            var fileList = new List<string>();

            try
            {
                foreach (var d in Directory.GetDirectories(_currentDir))
                {
                    string n = Path.GetFileName(d);
                    if (!string.IsNullOrEmpty(n) && !n.StartsWith('.'))
                        dirList.Add(n);
                }

                if (_mode != Mode.SelectFolder)
                {
                    string ext = _filterExtensions.Length > 0 && _filterIdx < _filterExtensions.Length
                        ? _filterExtensions[_filterIdx]
                        : ".*";

                    foreach (var f in Directory.GetFiles(_currentDir))
                    {
                        string n = Path.GetFileName(f);
                        if (n.StartsWith('.')) continue;
                        if (ext == ".*" || n.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                            fileList.Add(n);
                    }
                }
            }
            catch { /* permission denied */ }

            dirList.Sort(StringComparer.OrdinalIgnoreCase);
            fileList.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var d in dirList)  _entries.Add((d, true));
            foreach (var f in fileList) _entries.Add((f, false));
        }

        private static void NavigateTo(string dir, bool addToHistory = true)
        {
            if (!Directory.Exists(dir)) return;

            _currentDir   = dir;
            _needsRefresh = true;
            FillBuffer(ref _pathBuf, dir);

            if (addToHistory)
            {
                // Prune forward history on new navigation
                if (_historyPos < _history.Count - 1)
                    _history.RemoveRange(_historyPos + 1, _history.Count - _historyPos - 1);
                _history.Add(dir);
                _historyPos = _history.Count - 1;
            }
            else
            {
                _history.Clear();
                _history.Add(dir);
                _historyPos = 0;
            }
        }

        private static void GoBack()
        {
            if (_historyPos <= 0) return;
            _historyPos--;
            _currentDir = _history[_historyPos];
            FillBuffer(ref _pathBuf, _currentDir);
            _needsRefresh = true;
        }

        private static void GoForward()
        {
            if (_historyPos >= _history.Count - 1) return;
            _historyPos++;
            _currentDir = _history[_historyPos];
            FillBuffer(ref _pathBuf, _currentDir);
            _needsRefresh = true;
        }

        private static void GoUp()
        {
            var parent = Directory.GetParent(_currentDir);
            if (parent != null) NavigateTo(parent.FullName);
        }

        private static bool IsRootDir(string path)
            => Directory.GetParent(path) == null;

        // ── Filters ────────────────────────────────────────────────────────

        private static void ParseFilters(string? filters)
        {
            _filterNames      = Array.Empty<string>();
            _filterExtensions = Array.Empty<string>();
            _filterIdx        = 0;

            if (string.IsNullOrEmpty(filters)) return;

            // Format: "Scene Files{.scene.json},All Files{.*}"
            // Simple: ".scene.json" or ".json,.scene.json"
            if (!filters.Contains('{'))
            {
                var exts  = filters.Split(',');
                var names = new string[exts.Length];
                for (int i = 0; i < exts.Length; i++)
                    names[i] = "*" + exts[i].Trim();
                _filterExtensions = exts;
                _filterNames      = names;
                return;
            }

            var nameList = new List<string>();
            var extList  = new List<string>();

            // Split on ',' but respect braces
            int depth = 0;
            int start = 0;
            for (int i = 0; i <= filters.Length; i++)
            {
                char c = i < filters.Length ? filters[i] : ',';
                if      (c == '{') depth++;
                else if (c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    ProcessFilterItem(filters[start..i], nameList, extList);
                    start = i + 1;
                }
            }

            _filterNames      = nameList.ToArray();
            _filterExtensions = extList.ToArray();
        }

        private static void ProcessFilterItem(string item, List<string> names, List<string> exts)
        {
            item = item.Trim();
            if (string.IsNullOrEmpty(item)) return;

            int brace = item.IndexOf('{');
            if (brace < 0) { names.Add(item); exts.Add(".*"); return; }

            string displayName = item[..brace].Trim();
            string extStr      = item[(brace + 1)..].TrimEnd('}').Trim();
            string firstExt    = extStr.Split(',')[0].Trim();

            names.Add(displayName);
            exts.Add(firstExt);
        }

        // ── Quick Access ───────────────────────────────────────────────────

        private static void LoadQuickAccess()
        {
            _quickAccess.Clear();

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            TryAddQuickAccess(home, "Home");

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (desktop != home) TryAddQuickAccess(desktop);

            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (docs != home && docs != desktop) TryAddQuickAccess(docs);

            if (OperatingSystem.IsMacOS())
            {
                TryAddQuickAccess(Path.Combine(home, "Downloads"));
                TryAddQuickAccess(Path.Combine(home, "Development"));
                TryAddQuickAccess(Path.Combine(home, "Projects"));
            }
            else if (OperatingSystem.IsWindows())
            {
                foreach (var drive in DriveInfo.GetDrives())
                    if (drive.IsReady) _quickAccess.Add(drive.RootDirectory.FullName);
            }
            else
            {
                TryAddQuickAccess("/");
                TryAddQuickAccess("/home");
                TryAddQuickAccess("/mnt");
            }

            _quickAccessLoaded = true;
        }

        private static void TryAddQuickAccess(string path, string? _ = null)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                _quickAccess.Add(path);
        }

        // ── Buffer helpers ─────────────────────────────────────────────────

        private static void FillBuffer(ref byte[] buf, string text)
        {
            buf = new byte[buf.Length];
            if (string.IsNullOrEmpty(text)) return;
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            int len   = Math.Min(bytes.Length, buf.Length - 1);
            System.Array.Copy(bytes, buf, len);
        }

        private static string GetStr(byte[] buf)
        {
            int len = Array.IndexOf(buf, (byte)0);
            return len > 0 ? System.Text.Encoding.UTF8.GetString(buf, 0, len) : string.Empty;
        }
    }
}
