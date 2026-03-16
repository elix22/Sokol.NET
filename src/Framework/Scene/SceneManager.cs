using System.IO;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Scene
{
    public static class SceneManager
    {
        public static Scene? ActiveScene { get; private set; }
        public static PlayModeState PlayMode { get; private set; } = PlayModeState.Stopped;

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

        public static void SetPlayMode(PlayModeState state)
        {
            PlayMode = state;
            EventBus.RaisePlayModeChanged(state);
        }

        public static void Play()  => SetPlayMode(PlayModeState.Playing);
        public static void Pause() => SetPlayMode(PlayModeState.Paused);
        public static void Stop()  => SetPlayMode(PlayModeState.Stopped);
    }
}
