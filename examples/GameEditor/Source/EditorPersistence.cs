// EditorPersistence.cs
// Lightweight state persistence for GameEditor, inspired by Jot.
// Uses System.Text.Json with a source-generated serialization context
// so it is fully compatible with AOT (PublishAot=true / NativeAOT).
//
// Persisted file: ~/.sokolnet_config/gameeditor_state.json
//
// Usage:
//   EditorPersistence.Load();              // call once in Init()
//   EditorPersistence.RestoreSession();    // call after Load() in Init()
//   EditorPersistence.Save();             // call in Cleanup()
//   EditorPersistence.AddRecentProject(path); // call after a project is opened

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scene;
using GameEditor.CodeEditor;
using GameEditor.UI;

namespace GameEditor
{
    // ── Serialisable state bag ──────────────────────────────────────────────

    public class EditorState_Persisted
    {
        public string? LastProjectFolder { get; set; }
        public string? LastScenePath     { get; set; }
        public List<string> RecentProjects { get; set; } = new();

        // Editor UI state (overlays, gizmo)
        public int?    OverlayFlags    { get; set; }   // GizmoOverlayFlags bitmask; null = use default
        public int?    GizmoOperation  { get; set; }   // ImGuizmo.Operation; null = use default (Translate)
        public int?    GizmoMode       { get; set; }   // ImGuizmo.Mode;      null = use default (World)

        // Font sizes
        public float?  UiFontSize      { get; set; }   // Editor UI font size in px; null = 14
        public float?  CodeFontSize    { get; set; }   // Script editor font size in px; null = 14

        // GUI style theme ("Dark", "Light", "Classic")
        public string? GuiTheme        { get; set; }   // null = "Dark"
    }

    // ── AOT-safe JSON context ───────────────────────────────────────────────

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(EditorState_Persisted))]
    [JsonSerializable(typeof(List<string>))]
    internal partial class EditorPersistenceJsonContext : JsonSerializerContext { }

    // ── Persistence manager ─────────────────────────────────────────────────

    public static class EditorPersistence
    {
        private const int MaxRecentProjects = 10;

        private static readonly string _stateFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sokolnet_config",
            "gameeditor_state.json");

        private static EditorState_Persisted _state = new();

        public static string?       LastProjectFolder => _state.LastProjectFolder;
        public static string?       LastScenePath     => _state.LastScenePath;
        public static IReadOnlyList<string> RecentProjects => _state.RecentProjects;

        // ── Load ────────────────────────────────────────────────────────────

        public static void Load()
        {
            try
            {
                if (!File.Exists(_stateFile))
                    return;

                string json = File.ReadAllText(_stateFile);
                var loaded = JsonSerializer.Deserialize(
                    json,
                    EditorPersistenceJsonContext.Default.EditorState_Persisted);
                if (loaded != null)
                    _state = loaded;
            }
            catch (Exception ex)
            {
                Logger.Warning($"[EditorPersistence] Could not load state: {ex.Message}");
            }

            KeyBindings.LoadFromFile();
        }

        // ── Save ────────────────────────────────────────────────────────────

        public static void Save()
        {
            try
            {
                // Snapshot current state before writing
                if (ConfigManager.HasProject)
                    _state.LastProjectFolder = ConfigManager.ProjectFolder;

                var scene = SceneManager.ActiveScene;
                if (scene?.FilePath != null)
                    _state.LastScenePath = scene.FilePath;

                // Snapshot overlay + gizmo state before serialising
                _state.OverlayFlags   = (int)EditorState.Overlays;
                _state.GizmoOperation = (int)EditorState.CurrentGizmoOp;
                _state.GizmoMode      = (int)EditorState.CurrentGizmoMode;

                // Snapshot font sizes
                _state.UiFontSize   = EditorFonts.UiFontSize;
                _state.CodeFontSize = EditorFonts.CodeFontSize;

                // Snapshot GUI theme
                _state.GuiTheme = EditorTheme.Current;

                Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
                string json = JsonSerializer.Serialize(
                    _state,
                    EditorPersistenceJsonContext.Default.EditorState_Persisted);
                File.WriteAllText(_stateFile, json);

                KeyBindings.SaveToFile();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[EditorPersistence] Could not save state: {ex.Message}");
            }
        }

        // ── Restore session on startup ──────────────────────────────────────

        public static void RestoreSession()
        {
            // 0. Restore overlay + gizmo state (fast — no I/O, just field sets)
            if (_state.OverlayFlags.HasValue)
                EditorState.Overlays = (GizmoOverlayFlags)_state.OverlayFlags.Value;
            if (_state.GizmoOperation.HasValue)
                EditorState.CurrentGizmoOp = (Imgui.ImGuizmo.Operation)_state.GizmoOperation.Value;
            if (_state.GizmoMode.HasValue)
                EditorState.CurrentGizmoMode = (Imgui.ImGuizmo.Mode)_state.GizmoMode.Value;

            // Restore font sizes (actual atlas rebuild happens in Frame() / LoadFonts)
            if (_state.UiFontSize.HasValue && _state.UiFontSize.Value >= 8f)
                EditorFonts.UiFontSize = _state.UiFontSize.Value;
            if (_state.CodeFontSize.HasValue && _state.CodeFontSize.Value >= 8f)
                EditorFonts.CodeFontSize = _state.CodeFontSize.Value;

            // Restore GUI theme
            if (!string.IsNullOrEmpty(_state.GuiTheme))
                EditorTheme.Apply(_state.GuiTheme);

            // 1. Restore last project
            if (!string.IsNullOrEmpty(_state.LastProjectFolder) &&
                Directory.Exists(_state.LastProjectFolder))
            {
                try
                {
                    ConfigManager.Load(_state.LastProjectFolder);
                    Logger.Info($"[EditorPersistence] Restored project: {_state.LastProjectFolder}");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[EditorPersistence] Could not restore project: {ex.Message}");
                    return;
                }
            }

            // 2. Restore last scene (only if the project was also loaded)
            if (ConfigManager.HasProject &&
                !string.IsNullOrEmpty(_state.LastScenePath) &&
                File.Exists(_state.LastScenePath))
            {
                try
                {
                    SceneManager.LoadScene(_state.LastScenePath);
                    Logger.Info($"[EditorPersistence] Restored scene: {_state.LastScenePath}");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[EditorPersistence] Could not restore scene: {ex.Message}");
                }
            }

            // 3. Start file watcher + trigger initial background build so the assembly
            //    is ready before the user presses Play.
            if (ConfigManager.HasProject)
            {
                string proj = ConfigManager.ProjectFolder!;
                GameAssemblyRunner.StartWatcher(proj);
                GameAssemblyRunner.TriggerBuild(proj);
                Logger.Info($"[EditorPersistence] Background build started for {proj}");
            }
        }

        // ── Recent-projects list helpers ────────────────────────────────────

        public static void AddRecentProject(string folder)
        {
            _state.RecentProjects.Remove(folder);          // de-duplicate
            _state.RecentProjects.Insert(0, folder);       // most-recent first
            if (_state.RecentProjects.Count > MaxRecentProjects)
                _state.RecentProjects.RemoveAt(_state.RecentProjects.Count - 1);
        }

        public static void SetLastScene(string? path)
        {
            _state.LastScenePath = path;
        }
    }
}
