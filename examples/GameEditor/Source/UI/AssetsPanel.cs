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
        private const string IconHome        = "\uF015";
        private const string IconArrowUp     = "\uF062";
        private const string IconArrowLeft   = "\uF060";

        // ── State ────────────────────────────────────────────────────────────
        private static string? _selectedFolder;    // highlighted in left pane
        private static string? _selectedFile;
        // Explicitly track which folders are expanded so ImGui state changes
        // (e.g. icon prefix in label changing) never accidentally collapse a node.
        private static HashSet<string> _openFolders = new();

        private static string? _renamingPath;
        private static bool    _renamingIsFolder;
        private static byte[]  _renameBuffer = new byte[256];
        private static bool    _renameFocusPending;

        private static byte[]  _searchBuf = new byte[128];

        // Cached directory content for right pane
        private static string?       _cachedFolderPath;
        private static List<string>  _cachedSubDirs  = new();
        private static List<string>  _cachedFiles    = new();

        // Navigation history for Back button
        private static Stack<string> _navHistory = new();

        // Pending delete — set before opening confirmation modal
        private static string? _pendingDeletePath;
        private static bool    _pendingDeleteIsFolder;
        private static bool    _shouldOpenDeleteModal;   // deferred: open popup outside any popup context
        private static byte    _deleteModalOpen = 1;

        // Auto-refresh (only on button click or after file ops — no timer-based I/O)

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
            DrawDeleteConfirmModal();
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
            string cur = _selectedFolder ?? root;

            // Back
            bool canGoBack = _navHistory.Count > 0;
            if (!canGoBack) igBeginDisabled(true);
            if (igSmallButton($"{IconArrowLeft}##nav_back") && canGoBack)
            {
                string prev = _navHistory.Pop();
                SelectFolder(prev);
            }
            if (!canGoBack) igEndDisabled();
            igSameLine(0, 4);

            // Home
            bool atRoot = cur == root;
            if (atRoot) igBeginDisabled(true);
            if (igSmallButton($"{IconHome}##nav_home") && !atRoot)
                NavigateTo(root);
            if (atRoot) igEndDisabled();
            igSameLine(0, 4);

            // Up
            string? parentFolder = (cur != root) ? Path.GetDirectoryName(cur) : null;
            bool canGoUp = parentFolder != null && parentFolder != cur;
            if (!canGoUp) igBeginDisabled(true);
            if (igSmallButton($"{IconArrowUp}##nav_up") && canGoUp)
                NavigateTo(parentFolder!);
            if (!canGoUp) igEndDisabled();
            igSameLine(0, 8);

            // Refresh
            if (igSmallButton($"{IconRefresh} Refresh"))
                _cachedFolderPath = null;
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
            // Inline rename: replace tree node row with an input field
            if (_renamingPath == dirPath)
            {
                DrawRenameInput(dirPath);
                return;
            }

            string label = dirPath == root
                ? Path.GetFileName(dirPath.TrimEnd(Path.DirectorySeparatorChar))
                : Path.GetFileName(dirPath);

            bool isSelected = _selectedFolder == dirPath;
            List<string> subDirs = SortedDirs(dirPath);
            bool hasSubDirs = subDirs.Count > 0;

            // Auto-open root on first visit
            if (dirPath == root && !_openFolders.Contains(dirPath))
                _openFolders.Add(dirPath);

            var flags = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
            if (!hasSubDirs)
                flags |= ImGuiTreeNodeFlags.Leaf;
            if (isSelected)
                flags |= ImGuiTreeNodeFlags.Selected;

            // Drive open/close from our own state so ImGui never collapses a node
            // because the display label (icon prefix) changed.
            bool isOpen = _openFolders.Contains(dirPath);
            igSetNextItemOpen(isOpen, ImGuiCond.Always);

            string icon = (isOpen && hasSubDirs) ? IconFolderOpen : IconFolder;
            // Use igTreeNodeEx_StrStr: stable str_id (dirPath) separate from display label.
            bool expanded = igTreeNodeEx_StrStr(dirPath, flags, $"{icon}  {label}");

            // Let the user toggle open/close via the arrow only
            if (igIsItemToggledOpen())
            {
                if (isOpen) _openFolders.Remove(dirPath);
                else        _openFolders.Add(dirPath);
            }

            if (igIsItemClicked(ImGuiMouseButton.Left) && !igIsItemToggledOpen())
            {
                _selectedFolder = dirPath;
                _cachedFolderPath = null;
            }
            // Double-click on the label (not the arrow) starts rename
            if (igIsItemHovered(ImGuiHoveredFlags.None) &&
                igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left) &&
                !igIsItemToggledOpen())
                BeginRename(dirPath, isFolder: true);

            DrawFolderContextMenu(dirPath);

            if (expanded)
            {
                foreach (string sub in subDirs)
                    DrawFolderTree(sub, root);
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

                if (_renamingPath == sub)
                {
                    DrawRenameInput(sub);
                    continue;
                }

                bool sel = _selectedFolder == sub;
                if (igSelectable_Bool($"{IconFolder}  {name}##{sub}", sel,
                    ImGuiSelectableFlags.None, Vector2.Zero))
                    NavigateTo(sub);
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

                if (igIsItemHovered(ImGuiHoveredFlags.None) && igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left))
                {
                    if (name.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase))
                    {
                        SceneManager.LoadScene(file);
                        EditorPersistence.SetLastScene(file);
                    }
                    else
                        BeginRename(file, isFolder: false);
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
                if (igMenuItem_Bool("Rename", null, false, true))
                {
                    BeginRename(dirPath, isFolder: true);
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Delete Folder", null, false, true))
                {
                    _pendingDeletePath = dirPath;
                    _pendingDeleteIsFolder = true;
                    _shouldOpenDeleteModal = true;
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
                    BeginRename(filePath, isFolder: false);
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Delete", null, false, true))
                {
                    _pendingDeletePath = filePath;
                    _pendingDeleteIsFolder = false;
                    _shouldOpenDeleteModal = true;
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
        private static void DrawRenameInput(string path)
        {
            igSetNextItemWidth(-1);
            if (_renameFocusPending)
            {
                igSetKeyboardFocusHere(0);
                _renameFocusPending = false;
            }
            if (igInputText($"##rename_{path}", ref _renameBuffer[0], (uint)_renameBuffer.Length,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll, null, null))
            {
                string newName = System.Text.Encoding.UTF8.GetString(_renameBuffer).TrimEnd('\0');
                if (_renamingIsFolder)
                    TryRenameFolder(path, newName);
                else
                    TryRenameFile(path, newName);
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

        // Starts an inline rename for a file or folder.
        private static void BeginRename(string path, bool isFolder)
        {
            _renamingPath = path;
            _renamingIsFolder = isFolder;
            _renameFocusPending = true;
            string name = isFolder
                ? (Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) ?? path)
                : Path.GetFileName(path);
            var bytes = System.Text.Encoding.UTF8.GetBytes(name);
            int len = Math.Min(bytes.Length, _renameBuffer.Length - 1);
            Array.Clear(_renameBuffer, 0, _renameBuffer.Length);
            bytes.AsSpan(0, len).CopyTo(_renameBuffer);
        }

        // Navigates to a folder, recording the current location in history (for Back).
        private static void NavigateTo(string path)
        {
            if (_selectedFolder != null && _selectedFolder != path)
                _navHistory.Push(_selectedFolder);
            SelectFolder(path);
        }

        // Selects a folder and ensures every ancestor up to root is expanded in the left tree.
        private static void SelectFolder(string path)
        {
            _selectedFolder = path;
            _cachedFolderPath = null;
            // Walk up and open all ancestors
            string? cur = Path.GetDirectoryName(path);
            while (cur != null)
            {
                _openFolders.Add(cur);
                string? parent = Path.GetDirectoryName(cur);
                if (parent == cur) break; // filesystem root
                cur = parent;
            }
        }

        private static List<string> SortedDirs(string path)
        {
            try
            {
                var list = new List<string>(Directory.EnumerateDirectories(path));
                list.Sort(StringComparer.OrdinalIgnoreCase);
                return list;
            }
            catch { return new List<string>(); }
        }

        private static List<string> SortedFiles(string path)
        {
            try
            {
                var list = new List<string>(Directory.EnumerateFiles(path));
                list.Sort(StringComparer.OrdinalIgnoreCase);
                return list;
            }
            catch { return new List<string>(); }
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

        private static void TryRenameFolder(string dirPath, string newName)
        {
            try
            {
                string? parent = Path.GetDirectoryName(dirPath);
                if (parent == null || string.IsNullOrWhiteSpace(newName)) return;
                string newPath = Path.Combine(parent, newName);
                if (Directory.Exists(newPath)) return;
                Directory.Move(dirPath, newPath);
                _openFolders.Remove(dirPath);
                _openFolders.Add(newPath);
                if (_selectedFolder == dirPath) SelectFolder(newPath);
                _navHistory.Clear();
                _cachedFolderPath = null;
            }
            catch (Exception ex) { Logger.Warning($"[Assets] {ex.Message}"); }
        }

        private static void TryDeleteFolder(string dirPath)
        {
            try
            {
                Directory.Delete(dirPath, recursive: true);
                _cachedFolderPath = null;
                if (_selectedFolder == dirPath)
                    _selectedFolder = Path.GetDirectoryName(dirPath);
                _navHistory.Clear();
            }
            catch (Exception ex) { Logger.Warning($"[Assets] {ex.Message}"); }
        }

        private static void DrawDeleteConfirmModal()
        {
            // Must call igOpenPopup at the root window level (not inside any popup)
            // so the modal is not parented to and dismissed with the context menu.
            if (_shouldOpenDeleteModal)
            {
                _shouldOpenDeleteModal = false;
                _deleteModalOpen = 1;
                igOpenPopup_Str("Delete?##modal", ImGuiPopupFlags.None);
            }

            igSetNextWindowSize(new Vector2(360, 0), ImGuiCond.Always);
            if (igBeginPopupModal("Delete?##modal", ref _deleteModalOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                igTextWrapped($"Delete '{Path.GetFileName(_pendingDeletePath)}'?");
                if (_pendingDeleteIsFolder)
                    igTextWrapped("All folder contents will be permanently deleted.");
                igSpacing();
                igSeparator();
                igSpacing();

                if (igButton("Delete", new Vector2(120, 0)))
                {
                    if (_pendingDeleteIsFolder)
                        TryDeleteFolder(_pendingDeletePath);
                    else
                        TryDeleteFile(_pendingDeletePath);
                    _pendingDeletePath = null;
                    igCloseCurrentPopup();
                }
                igSameLine(0, 8);
                if (igButton("Cancel", new Vector2(120, 0)))
                {
                    _pendingDeletePath = null;
                    igCloseCurrentPopup();
                }
                igEndPopup();
            }
            // If user dismissed via the X button
            if (_deleteModalOpen == 0)
            {
                _pendingDeletePath = null;
                _deleteModalOpen = 1;
            }
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
