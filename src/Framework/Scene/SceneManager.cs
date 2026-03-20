using System.IO;
using System.Numerics;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Scripting;

namespace GameEditor.Framework.Scene
{
    public static class SceneManager
    {
        public static Scene? ActiveScene { get; private set; }
        public static PlayModeState PlayMode { get; private set; } = PlayModeState.Stopped;

        // JSON snapshot of the scene captured when Play() is called; restored on Stop().
        private static string? _playSnapshot;

        public static void NewScene(string name = "Untitled")
        {
            ActiveScene?.Clear();
            EventBus.RaiseSceneUnloaded();
            ActiveScene = new Scene(name);
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
        }

        public static void SaveScene(string path)
        {
            if (ActiveScene == null) return;
            string json = SceneSerializer.Serialize(ActiveScene);
            File.WriteAllText(path, json);
            ActiveScene.FilePath = path;
            ActiveScene.IsDirty = false;
            Logger.Info($"Scene saved to {path}");
        }

        public static void LoadScene(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Warning($"Scene file not found: {path}");
                return;
            }
            EventBus.RaiseSceneUnloaded();
            ActiveScene ??= new Scene("Untitled");
            string json = File.ReadAllText(path);
            SceneSerializer.Deserialize(json, ActiveScene);
            ActiveScene.FilePath = path;
            ActiveScene.IsDirty = false;
            EventBus.RaiseSceneLoaded();
            UndoStack.Clear();
            Logger.Info($"Scene loaded from {path}");
        }

        public static void LoadSceneFromAssetsAsync(string assetPath)
        {
            GameFileSystem.Instance.LoadFile(assetPath, (path, buffer, status) =>
            {
                if (status == FileLoadStatus.Success)
                {
                    string json = System.Text.Encoding.UTF8.GetString(buffer);
                    EventBus.RaiseSceneUnloaded();
                    ActiveScene ??= new Scene("Untitled");
                    SceneSerializer.Deserialize(json, ActiveScene);
                    ActiveScene.FilePath = null; // Loaded from assets, not a file path
                    ActiveScene.IsDirty = false;
                    EventBus.RaiseSceneLoaded();
                    UndoStack.Clear();
                    Logger.Info($"Scene loaded from assets: {assetPath}");
                }
                else
                {
                    Logger.Warning($"Failed to load scene from assets: {assetPath}");
                }
            });

        }

        public static void SetPlayMode(PlayModeState state)
        {
            PlayMode = state;
            EventBus.RaisePlayModeChanged(state);
        }

        /// <summary>
        /// Saves a scene snapshot, populates the ScriptSystem from current entities,
        /// starts all behaviours and transitions to Playing state.
        /// </summary>
        public static void Play()
        {
            if (PlayMode == PlayModeState.Playing) return;

            if (ActiveScene == null)
            {
                Logger.Warning("[SceneManager] No active scene to play.");
                return;
            }

            if (PlayMode == PlayModeState.Stopped)
            {
                // Snapshot the scene so we can restore it on Stop()
                if (ActiveScene != null)
                {
                    _playSnapshot = SceneSerializer.Serialize(ActiveScene);
                    Logger.Info("[SceneManager] Play snapshot saved.");
                }

                // Populate script behaviours from current scene entities
                ScriptSystem.PopulateFromScene(ECS.ECSWorld.Instance);

                SetPlayMode(PlayModeState.Playing);

                // Start all registered behaviours
                ScriptSystem.StartAll();
                Logger.Info($"[SceneManager] Play started. {ScriptSystem.Count} script(s) running.");
            }
            else if (PlayMode == PlayModeState.Paused)
            {
                // Resume without restarting scripts
                SetPlayMode(PlayModeState.Playing);
            }
        }

        /// <summary>Freezes script execution without destroying behaviours.</summary>
        public static void Pause()
        {
            if (PlayMode != PlayModeState.Playing) return;
            SetPlayMode(PlayModeState.Paused);
        }

        /// <summary>
        /// Stops all scripts, transitions to Stopped state and restores the pre-play
        /// scene snapshot so the editor sees the original unmodified scene.
        /// </summary>
        public static void Stop()
        {
            if (PlayMode == PlayModeState.Stopped) return;

            // Destroy all running scripts
            ScriptSystem.StopAll();

            SetPlayMode(PlayModeState.Stopped);

            // Restore the pre-play snapshot
            if (_playSnapshot != null && ActiveScene != null)
            {
                EventBus.RaiseSceneUnloaded();
                SceneSerializer.Deserialize(_playSnapshot, ActiveScene);
                ActiveScene.IsDirty = false;
                _playSnapshot = null;
                EventBus.RaiseSceneLoaded();
                Logger.Info("[SceneManager] Scene restored from play snapshot.");
            }

            UndoStack.Clear();
        }

        public static bool GetMainCameraMatrices(int width, int height,out Matrix4x4 viewProj, out Vector3 eyePos)
        {
            viewProj = Matrix4x4.Identity;
            eyePos = Vector3.Zero;
            if (width <= 0 || height <= 0) return false;

            var world = ECSWorld.Instance;
            foreach (int id in world.Entities)
            {
                if (!world.TryGetComponent<CameraComponent>(id, out var cam) || !cam.IsMain) continue;
                if (cam.NearZ <= 0f || cam.FarZ <= cam.NearZ) continue; // skip degenerate projection
                if (!world.TryGetComponent<Transform>(id, out var tr)) continue;

                Matrix4x4 rotMat = Matrix4x4.CreateFromYawPitchRoll(
                    tr.EulerAngles.Y * MathF.PI / 180f,
                    tr.EulerAngles.X * MathF.PI / 180f,
                    tr.EulerAngles.Z * MathF.PI / 180f);

                // Use the rotation matrix's +Z column as forward (matches gizmo convention)
                Vector3 forward = new Vector3(rotMat.M31, rotMat.M32, rotMat.M33);
                Vector3 up = new Vector3(rotMat.M21, rotMat.M22, rotMat.M23);

                eyePos = tr.Position;
                Matrix4x4 view = Matrix4x4.CreateLookAt(eyePos, eyePos + forward, up);
                Matrix4x4 proj;
                if (cam.IsOrthographic)
                {
                    float orthoH = MathF.Max(0.01f, cam.OrthoSize > 0f ? cam.OrthoSize : 5f);
                    float orthoW = orthoH * ((float)width / height);
                    proj = Matrix4x4.CreateOrthographicOffCenter(-orthoW, orthoW, -orthoH, orthoH, cam.NearZ, cam.FarZ);
                }
                else
                {
                    float fov = MathF.Max(1f, cam.Fov) * MathF.PI / 180f;
                    proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, (float)width / height, cam.NearZ, cam.FarZ);
                }
                viewProj = view * proj;
                return true;
            }
            return false;
        }
    }
}
