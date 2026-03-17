// AssetsPanel.cs
// Unity-style Assets browser panel for the GameEditor.
// Shows the file/folder tree of the currently loaded project root.
// Double-clicking a .scene.json file loads that scene.

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
        // ── Selection / rename state ─────────────────────────────────────────

        private static string? _selectedPath;
        private static string? _renamingPath;
        private static byte[]  _renameBuffer = new byte[256];

        // ── Refresh throttling ───────────────────────────────────────────────

        private const float RefreshInterval = 2.0f; // seconds
        private static float _timeSinceRefresh = RefreshInterval; // force first refresh
        private static List<string>? _cachedTopDirs;
        private static List<string>? _cachedTopFiles;
        private static readonly Dictionary<string, (List<string> dirs, List<string> files)> _dirCache = new();

        // ── Panel entry ──────────────────────────────────────────────────────

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

            // Refresh button + auto-refresh timer  
            _timeSinceRefresh += Time.DeltaTime;
            if (igSmallButton("⟳ Refresh") || _timeSinceRefresh >= RefreshInterval)
            {
                _dirCache.Clear();
                _cachedTopDirs  = null;
                _cachedTopFiles = null;
                _timeSinceRefresh = 0f;
            }

            igSeparator();

            // Populate top-level cache
            if (_cachedTopDirs == null)
                RefreshDir(root, out _cachedTopDirs, out _cachedTopFiles);

            // Breadcrumb / path bar (project name as root label)
            string rootLabel = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar));
            igPushStyleColor_Vec4(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 0.3f, 1f));
            igText(rootLabel);
            igPopStyleColor(1);

            igSeparator();

            // Scrollable tree area
            igBeginChild_Str("##assets_tree", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
            DrawDirectory(root, _cachedTopDirs, _cachedTopFiles);
            igEndChild();

            igEnd();
        }

        // ── Directory node ───────────────────────────────────────────────────

        private static void DrawDirectory(string dirPath, List<string> dirs, List<string> files)
        {
            // Sub-directories first
            foreach (string sub in dirs)
            {
                if (!_dirCache.TryGetValue(sub, out var subContent))
                {
                    RefreshDir(sub, out var sd, out var sf);
                    subContent = (sd, sf);
                    _dirCache[sub] = subContent;
                }

                string label = Path.GetFileName(sub);
                bool hasChildren = subContent.dirs.Count > 0 || subContent.files.Count > 0;
                var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (!hasChildren)
                    flags |= ImGuiTreeNodeFlags.Leaf;
                if (_selectedPath == sub)
                    flags |= ImGuiTreeNodeFlags.Selected;

                bool open = igTreeNodeEx_Str($"📁 {label}##{sub}", flags);

                if (igIsItemClicked(ImGuiMouseButton.Left))
                    _selectedPath = sub;

                DrawContextMenuDir(sub);

                if (open)
                {
                    DrawDirectory(sub, subContent.dirs, subContent.files);
                    igTreePop();
                }
            }

            // Files
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                bool isScene = name.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase);

                // Renaming in-place
                if (_renamingPath == file)
                {
                    DrawRenameInput(file);
                    continue;
                }

                string icon = isScene ? "🎬" : "📄";
                var flags = ImGuiTreeNodeFlags.Leaf     |
                            ImGuiTreeNodeFlags.NoTreePushOnOpen |
                            ImGuiTreeNodeFlags.SpanAvailWidth;
                if (_selectedPath == file)
                    flags |= ImGuiTreeNodeFlags.Selected;

                igTreeNodeEx_Str($"{icon} {name}##{file}", flags);

                if (igIsItemClicked(ImGuiMouseButton.Left))
                    _selectedPath = file;

                if (igIsItemHovered(ImGuiHoveredFlags.None) &&
                    igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left) &&
                    isScene)
                {
                    SceneManager.LoadScene(file);
                    EditorPersistence.SetLastScene(file);
                }

                DrawContextMenuFile(file);
            }
        }

        // ── Context menu – directory ─────────────────────────────────────────

        private static void DrawContextMenuDir(string dirPath)
        {
            string popupId = $"##ctx_dir_{dirPath}";
            if (igBeginPopupContextItem(popupId, ImGuiPopupFlags.MouseButtonRight))
            {
                if (igMenuItem_Bool("New Folder", null, false, true))
                {
                    TryCreateFolder(dirPath);
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Reveal in Finder", null, false, true))
                {
                    RevealInExplorer(dirPath);
                    igCloseCurrentPopup();
                }
                igEndPopup();
            }
        }

        // ── Context menu – file ──────────────────────────────────────────────

        private static void DrawContextMenuFile(string filePath)
        {
            string popupId = $"##ctx_file_{filePath}";
            if (igBeginPopupContextItem(popupId, ImGuiPopupFlags.MouseButtonRight))
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
                    string curName = Path.GetFileName(filePath);
                    var bytes = System.Text.Encoding.UTF8.GetBytes(curName);
                    int copyLen = Math.Min(bytes.Length, _renameBuffer.Length - 1);
                    bytes.AsSpan(0, copyLen).CopyTo(_renameBuffer);
                    _renameBuffer[copyLen] = 0;
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Delete", null, false, true))
                {
                    TryDeleteFile(filePath);
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Reveal in Finder", null, false, true))
                {
                    RevealInExplorer(Path.GetDirectoryName(filePath) ?? filePath);
                    igCloseCurrentPopup();
                }
                igEndPopup();
            }
        }

        // ── Rename input ─────────────────────────────────────────────────────

        private static void DrawRenameInput(string filePath)
        {
            igSetNextItemWidth(-1);
            if (igInputText($"##rename_{filePath}", ref _renameBuffer[0], (uint)_renameBuffer.Length,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll,
                null, null))
            {
                string newName = System.Text.Encoding.UTF8.GetString(_renameBuffer).TrimEnd('\0');
                TryRenameFile(filePath, newName);
                _renamingPath = null;
            }
            if (igIsItemDeactivated())
                _renamingPath = null; // cancelled
        }

        // ── File system helpers ──────────────────────────────────────────────

        private static void RefreshDir(string dirPath, out List<string> dirs, out List<string> files)
        {
            dirs  = new List<string>();
            files = new List<string>();
            try
            {
                foreach (string d in Directory.EnumerateDirectories(dirPath))
                    dirs.Add(d);
                dirs.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (string f in Directory.EnumerateFiles(dirPath))
                    files.Add(f);
                files.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch { /* ignore permission errors */ }
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
                _dirCache.Remove(parentDir);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Assets] Could not create folder: {ex.Message}");
            }
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
                string? parent = Path.GetDirectoryName(filePath);
                if (parent != null) _dirCache.Remove(parent);
                if (_selectedPath == filePath) _selectedPath = null;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Assets] Could not delete file: {ex.Message}");
            }
        }

        private static void TryRenameFile(string filePath, string newName)
        {
            try
            {
                string? dir  = Path.GetDirectoryName(filePath);
                if (dir == null) return;
                string dest = Path.Combine(dir, newName);
                File.Move(filePath, dest);
                _dirCache.Remove(dir);
                if (_selectedPath == filePath) _selectedPath = dest;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Assets] Could not rename: {ex.Message}");
            }
        }

        private static void RevealInExplorer(string path)
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
            catch { /* best-effort */ }
        }
    }
}
