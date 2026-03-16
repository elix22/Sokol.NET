using System;

namespace GameEditor.Framework.Core
{
    public enum PlayModeState { Stopped, Playing, Paused }

    public static class EventBus
    {
        public static event Action<int>? EntitySelected;
        public static event Action<int>? EntityCreated;
        public static event Action<int>? EntityDestroyed;
        public static event Action<int, string>? ComponentChanged;
        public static event Action<PlayModeState>? PlayModeChanged;
        public static event Action? SceneLoaded;
        public static event Action? SceneUnloaded;

        public static void RaiseEntitySelected(int id)             => EntitySelected?.Invoke(id);
        public static void RaiseEntityCreated(int id)              => EntityCreated?.Invoke(id);
        public static void RaiseEntityDestroyed(int id)            => EntityDestroyed?.Invoke(id);
        public static void RaiseComponentChanged(int id, string c) => ComponentChanged?.Invoke(id, c);
        public static void RaisePlayModeChanged(PlayModeState s)   => PlayModeChanged?.Invoke(s);
        public static void RaiseSceneLoaded()                      => SceneLoaded?.Invoke();
        public static void RaiseSceneUnloaded()                    => SceneUnloaded?.Invoke();
    }
}
