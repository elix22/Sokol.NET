using System;
using Frent;

namespace GameEditor.Framework.Core
{
    public enum PlayModeState { Stopped, Playing, Paused }

    public static class EventBus
    {
        public static event Action<Entity>? EntitySelected;
        public static event Action<Entity>? EntityCreated;
        public static event Action<Entity>? EntityDestroyed;
        public static event Action<Entity, string>? ComponentChanged;
        public static event Action<PlayModeState>? PlayModeChanged;
        public static event Action? SceneLoaded;
        public static event Action? SceneUnloaded;
        /// <summary>A scene is about to be written to its file: what is saved with a scene (an editor's open tile maps) is written now.</summary>
        public static event Action? SceneSaving;
        /// <summary>Play is about to start: before the scene is kept for Stop (an editor's preview gives its values back here).</summary>
        public static event Action? PlayStarting;

        public static void RaiseEntitySelected(Entity e)             => EntitySelected?.Invoke(e);
        public static void RaiseEntityCreated(Entity e)              => EntityCreated?.Invoke(e);
        public static void RaiseEntityDestroyed(Entity e)            => EntityDestroyed?.Invoke(e);
        public static void RaiseComponentChanged(Entity e, string c) => ComponentChanged?.Invoke(e, c);
        public static void RaisePlayModeChanged(PlayModeState s)     => PlayModeChanged?.Invoke(s);
        public static void RaiseSceneLoaded()                        => SceneLoaded?.Invoke();
        public static void RaiseSceneUnloaded()                      => SceneUnloaded?.Invoke();
        public static void RaiseSceneSaving()                        => SceneSaving?.Invoke();
        public static void RaisePlayStarting()                       => PlayStarting?.Invoke();
    }
}
