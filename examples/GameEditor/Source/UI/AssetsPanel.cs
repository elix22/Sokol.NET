// AssetsPanel.cs
// Unity-style two-pane Assets browser.
//   Left pane  : folder tree
//   Right pane : contents of the selected folder (icons + names)
//
// Icons use Font Awesome 4 codepoints merged onto the main font.
// FA4 relevant codepoints:
//   folder         \uF07B
//   folder-open    \uF07C
//   file           \uF15B
//   file-code      \uF1C9
//   file-image     \uF1C5
//   file-archive   \uF1C6
//   film (scene)   \uF008
//   refresh        \uF021
//   search         \uF002

using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scene;
using GameEditor;

namespace GameEditor.UI
{
    public static unsafe class AssetsPanel
    {
        // ── FA4 icon codepoints ──────────────────────────────────────────────
        private const string IconFolder      = "\uF07B";
        private const string IconFolderOpen  = "\uF07C";
        private const string IconFile        = "\uF15B";
        private const string IconFileCode    = "\uF1C9";
        private const string IconFileImage   = "\uF1C5";
        private const string IconFileArchive = "\uF1C6";
        private const string IconScene       = "\uF008";   // film strip
        private const string IconRefresh     = "\uF021";
        private const string IconSearch      = "\uF002";

        // ── State ────────────────────────────────────────────────────────────
        private static string? _selectedFolder;    // highlighted in left pane
        private static string? _selectedFile;

        private static string? _renamingPath;
        private static byte[]  _renameBuffer = new byte[256];
        private static bool    _renameFocusPending;

        private static byte[]  _searchBuf = new byte[128];

        // Cached directory content for right pane
        private static string?       _cachedFolderPath;
        private static List<string>  _cachedSubDirs  = new();
        private static List<string>  _cachedFiles    = new();

        // Auto-refresh
        private const float RefreshInterval = 2.0f;
        private static float _timeSinceRefresh = RefreshInterval;

        // ── Entry point ──────────────────────────────────────────────────────
        public static void Draw()
        {
            byte open = 1;
            if (!igBegin("Assets", ref open, ImGuiWindowFlags.None))
            {
                igEnd();
                return;
            }

            string? root = ConfigManager.HasProject ? ConfigManager.ProjectFolder : null;
            if (root == null || !Directory.Exists(root))
            {
                igTextDisabled("No project loaded.");
                igEnd();
                return;
            }

            // ── Toolbar ──────────────────────────────────────────────────────
            DrawToolbar(root);
            igSeparator();

            // ── Two panes ────────────────────────────────────────────────────
            Vector2 avail = default;
            igGetContentRegionAvail(ref avail);
            float leftW = MathF.Max(avail.X * 0.28f, 140f);

            // Left: folder tree
            igBeginChild_Str("##assets_tree", new Vector2(leftW, 0),
                ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
            DrawFolderTree(root, root);
            igEndChild();

            igSameLine(0, 4);

            // Right: folder contents
            igBeginChild_Str("##assets_content", new Vector2(0, 0),
                ImGuiChildFlags.None, ImGuiWindowFlags.None);
            string displayFolder = _selectedFolder ?? root;
            DrawFolderContents(displayFolder);
            igEndChild();

            igEnd();
        }

        // ── Toolbar ──────────────────────────────────────────────────────────
        private static void DrawToolbar(string root)
        {
            // Refresh button
            _timeSinceRefresh += Time.DeltaTime;
            if (igSmallButton($"{IconRefresh} Refresh") || _timeSinceRefresh >= RefreshInterval)
            {
                _cachedFolderPath = null;
                _timeSinceRefresh = 0f;
            }
            igSameLine(0, 8);

            // Search filter
            igPushItemWidth(180f);
            igInputText($"{IconSearch}##assets_search", ref _searchBuf[0],
                (uint)_searchBuf.Length, ImGuiInputTextFlags.None, null, null);
            igPopItemWidth();
        }

        // ── Left pane: recursive folder tree ────────────────────────────────
        private static void DrawFolderTree(string dirPath, string root)
        {
            string label = dirPath == root
                ? Path.GetFileName(dirPath.TrimEnd(Path.DirectorySeparatorChar))
                : Path.GetFileName(dirPath);

            bool isSelected = _selectedFolder == dirPath;
            bool hasSubDirs = HasSubDirectories(dirPath);

            var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
            if (!hasSubDirs)
                flags |= ImGuiTreeNodeFlags.Leaf;
            if (isSelected)
                flags |= ImGuiTreeNodeFlags.Selected;
            if (dirPath == root)
                flags |= ImGuiTreeNodeFlags.DefaultOpen;

            string icon = (isSelected && hasSubDirs) ? IconFolderOpen : IconFolder;
            bool expanded = igTreeNodeEx_Str($"{icon} {label}##{dirPath}", flags);

            if (igIsItemClicked(ImGuiMouseButton.Left))
            {
                _selectedFolder = dirPath;
                _cachedFolderPath = null; // force refresh of right pane
            }

            DrawFolderContextMenu(dirPath);

            if (expanded)
            {
                try
                {
                    foreach (string sub in SortedDirs(dirPath))
                        DrawFolderTree(sub, root);
                }
                catch { }
                igTreePop();
            }
        }

        // ── Right pane: contents of selected folder ──────────────────────────
        private static void DrawFolderContents(string folder)
        {
            // Rebuild cache when folder changes or refresh triggered
            if (_cachedFolderPath != folder)
            {
                _cachedFolderPath = folder;
                _cachedSubDirs.Clear();
                _cachedFiles.Clear();
                try
                {
                    foreach (string d in SortedDirs(folder)) _cachedSubDirs.Add(d);
                    foreach (string f in SortedFiles(folder)) _cachedFiles.Add(f);
                }
                catch { }
            }

            string searchText = System.Text.Encoding.UTF8.GetString(_searchBuf).TrimEnd('\0');
            bool filtering = !string.IsNullOrEmpty(searchText);

            // Path breadcrumb
            igPushStyleColor_Vec4(ImGuiCol.Text, new Vector4(0.55f, 0.75f, 1f, 1f));
            igText(folder);
            igPopStyleColor(1);
            igSeparator();

            // Sub-directories
            foreach (string sub in _cachedSubDirs)
            {
                string name = Path.GetFileName(sub);
                if (filtering && !name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool sel = _selectedFolder == sub;
                if (igSelectable_Bool($"{IconFolder}  {name}##{sub}", sel,
                    ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    _selectedFolder = sub;
                    _cachedFolderPath = null;
                }
                if (igIsItemHovered(ImGuiHoveredFlags.None) && igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left))
                {
                    _selectedFolder = sub;
                    _cachedFolderPath = null;
                }
                DrawFolderContextMenu(sub);
            }

            // Files
            foreach (string file in _cachedFiles)
            {
                string name = Path.GetFileName(file);
                if (filtering && !name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Inline rename
                if (_renamingPath == file)
                {
                    DrawRenameInput(file);
                    continue;
                }

                bool sel = _selectedFile == file;
                if (igSelectable_Bool($"{GetFileIcon(name)}  {name}##{file}", sel,
                    ImGuiSelectableFlags.None, Vector2.Zero))
                    _selectedFile = file;

                if (igIsItemHovered(ImGuiHoveredFlags.None) &&
                    igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left) &&
                    name.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase))
                {
                    SceneManager.LoadScene(file);
                    EditorPersistence.SetLastScene(file);
                }

                DrawFileContextMenu(file);
            }
        }

        // ── Context menu – folder ────────────────────────────────────────────
        private static void DrawFolderContextMenu(string dirPath)
        {
            if (igBeginPopupContextItem($"##ctx_{dirPath}", ImGuiPopupFlags.MouseButtonRight))
            {
                if (igMenuItem_Bool("New Folder", null, false, true))
                {
                    TryCreateFolder(dirPath);
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Reveal in Finder", null, false, true))
                {
                    RevealPath(dirPath);
                    igCloseCurrentPopup();
                }
                igEndPopup();
            }
        }

        // ── Context menu – file ──────────────────────────────────────────────
        private static void DrawFileContextMenu(string filePath)
        {
            if (igBeginPopupContextItem($"##ctx_{filePath}", ImGuiPopupFlags.MouseButtonRight))
            {
                bool isScene = filePath.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase);
                if (isScene && igMenuItem_Bool("Load Scene", null, false, true))
                {
                    SceneManager.LoadScene(filePath);
                    EditorPersistence.SetLastScene(filePath);
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Rename", null, false, true))
                {
                    _renamingPath = filePath;
                    _renameFocusPending = true;
                    string cur = Path.GetFileName(filePath);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(cur);
                    int len = Math.Min(bytes.Length, _renameBuffer.Length - 1);
                    bytes.AsSpan(0, len).CopyTo(_renameBuffer);
                    _renameBuffer[len] = 0;
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Delete", null, false, true))
                {
                    TryDeleteFile(filePath);
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Reveal in Finder", null, false, true))
                {
                    RevealPath(Path.GetDirectoryName(filePath) ?? filePath);
                    igCloseCurrentPopup();
                }
                igEndPopup();
            }
        }

        // ── Inline rename ────────────────────────────────────────────────────
        private static void DrawRenameInput(string filePath)
        {
            igSetNextItemWidth(-1);
            if (_renameFocusPending)
            {
                igSetKeyboardFocusHere(0);
                _renameFocusPending = false;
            }
            if (igInputText($"##rename_{filePath}", ref _renameBuffer[0], (uint)_renameBuffer.Length,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll, null, null))
            {
                string newName = System.Text.Encoding.UTF8.GetString(_renameBuffer).TrimEnd('\0');
                TryRenameFile(filePath, newName);
                _renamingPath = null;
            }
            if (igIsItemDeactivated())
                _renamingPath = null;
        }

        // ── Icon selection ───────────────────────────────────────────────────
        private static string GetFileIcon(string name)
        {
            if (name.EndsWith(".scene.json",  StringComparison.OrdinalIgnoreCase)) return IconScene;
            if (name.EndsWith(".json",        StringComparison.OrdinalIgnoreCase)) return IconFileCode;
            if (name.EndsWith(".cs",          StringComparison.OrdinalIgnoreCase)) return IconFileCode;
            if (name.EndsWith(".glsl",        StringComparison.OrdinalIgnoreCase)) return IconFileCode;
            if (name.EndsWith(".hlsl",        StringComparison.OrdinalIgnoreCase)) return IconFileCode;
            if (name.EndsWith(".metal",       StringComparison.OrdinalIgnoreCase)) return IconFileCode;
            if (name.EndsWith(".png",         StringComparison.OrdinalIgnoreCase)) return IconFileImage;
            if (name.EndsWith(".jpg",         StringComparison.OrdinalIgnoreCase)) return IconFileImage;
            if (name.EndsWith(".jpeg",        StringComparison.OrdinalIgnoreCase)) return IconFileImage;
            if (name.EndsWith(".tga",         StringComparison.OrdinalIgnoreCase)) return IconFileImage;
            if (name.EndsWith(".bmp",         StringComparison.OrdinalIgnoreCase)) return IconFileImage;
            if (name.EndsWith(".zip",         StringComparison.OrdinalIgnoreCase)) return IconFileArchive;
            if (name.EndsWith(".tar",         StringComparison.OrdinalIgnoreCase)) return IconFileArchive;
            return IconFile;
        }

        // ── File system helpers ──────────────────────────────────────────────
        private static bool HasSubDirectories(string path)
        {
            try { return Directory.EnumerateDirectories(path).GetEnumerator().MoveNext(); }
            catch { return false; }
        }

        private static IEnumerable<string> SortedDirs(string path)
        {
            var list = new List<string>(Directory.EnumerateDirectories(path));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static IEnumerable<string> SortedFiles(string path)
        {
            var list = new List<string>(Directory.EnumerateFiles(path));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static void TryCreateFolder(string parentDir)
        {
            string newPath = Path.Combine(parentDir, "New Folder");
            int idx = 1;
            while (Directory.Exists(newPath))
                newPath = Path.Combine(parentDir, $"New Folder {idx++}");
            try
            {
                Directory.CreateDirectory(newPath);
                _cachedFolderPath = null;
            }
            catch (Exception ex) { Logger.Warning($"[Assets] {ex.Message}"); }
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
                _cachedFolderPath = null;
                if (_selectedFile == filePath) _selectedFile = null;
            }
            catch (Exception ex) { Logger.Warning($"[Assets] {ex.Message}"); }
        }

        private static void TryRenameFile(string filePath, string newName)
        {
            try
            {
                string? dir = Path.GetDirectoryName(filePath);
                if (dir == null) return;
                File.Move(filePath, Path.Combine(dir, newName));
                _cachedFolderPath = null;
                if (_selectedFile == filePath)
                    _selectedFile = Path.Combine(dir, newName);
            }
            catch (Exception ex) { Logger.Warning($"[Assets] {ex.Message}"); }
        }

        private static void RevealPath(string path)
        {
            try
            {
                if (OperatingSystem.IsMacOS())
                    System.Diagnostics.Process.Start("open", $"\"{path}\"");
                else if (OperatingSystem.IsWindows())
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
                else
                    System.Diagnostics.Process.Start("xdg-open", $"\"{path}\"");
            }
            catch { }
        }
    }
}
