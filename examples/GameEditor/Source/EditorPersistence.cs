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

namespace GameEditor
{
    // ── Serialisable state bag ──────────────────────────────────────────────

    public class EditorState_Persisted
    {
        public string? LastProjectFolder { get; set; }
        public string? LastScenePath     { get; set; }
        public List<string> RecentProjects { get; set; } = new();
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

                Directory.CreateDirectory(Path.GetDirectoryName(_stateFile)!);
                string json = JsonSerializer.Serialize(
                    _state,
                    EditorPersistenceJsonContext.Default.EditorState_Persisted);
                File.WriteAllText(_stateFile, json);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[EditorPersistence] Could not save state: {ex.Message}");
            }
        }

        // ── Restore session on startup ──────────────────────────────────────

        public static void RestoreSession()
        {
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
