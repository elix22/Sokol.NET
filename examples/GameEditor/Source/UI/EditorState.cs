using System.Collections.Generic;
using System.Numerics;
using Imgui;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scene;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.UI
{
    /// <summary>Which scene overlays are currently visible (toggled via the Overlays dropdown).</summary>
    [System.Flags]
    public enum GizmoOverlayFlags
    {
        None            = 0,
        Grid            = 1 << 0,   // XZ infinite grid
        WorldAxes       = 1 << 1,   // X/Y/Z axis arrows drawn over the viewport
        OrientationCube = 1 << 2,   // ViewManipulate cube in the upper-right corner
        EntityGizmos    = 1 << 3,   // transform gizmo on the selected entity
        CameraGizmos    = 1 << 4,   // camera frustum wireframe + icon
        LightGizmos     = 1 << 5,   // light range indicators + icons
        All             = Grid | WorldAxes | OrientationCube | EntityGizmos | CameraGizmos | LightGizmos
    }

    public static class EditorState
    {
        private static int _primaryEntity = -1;

        /// <summary>The primary (last-clicked) selected entity, or -1 if nothing is selected.</summary>
        public static int SelectedEntity => _primaryEntity;

        /// <summary>All currently selected entity IDs (always contains <see cref="SelectedEntity"/> when non-negative).</summary>
        public static HashSet<int> SelectedEntities { get; } = new HashSet<int>();

        /// <summary>Number of currently selected entities.</summary>
        public static int SelectionCount => SelectedEntities.Count;

        /// <summary>Returns true when <paramref name="id"/> is part of the current selection.</summary>
        public static bool IsSelected(int id) => SelectedEntities.Contains(id);

        public static PlayModeState PlayMode => SceneManager.PlayMode;

        // Gizmo state ─────────────────────────────────────────────────────────
        public static ImGuizmo.Operation CurrentGizmoOp   { get; set; } = ImGuizmo.Operation.Translate;
        public static ImGuizmo.Mode      CurrentGizmoMode { get; set; } = ImGuizmo.Mode.World;

        // Overlay visibility ──────────────────────────────────────────────────
        public static GizmoOverlayFlags Overlays { get; set; } = GizmoOverlayFlags.All;

        public static readonly List<(LogLevel Level, string Message)> ConsoleEntries = new();
        private const int MaxConsoleEntries = 512;

        static EditorState()
        {
            Logger.OnLog += (level, msg) =>
            {
                if (ConsoleEntries.Count >= MaxConsoleEntries)
                    ConsoleEntries.RemoveAt(0);
                ConsoleEntries.Add((level, msg));
            };

            EventBus.EntitySelected += id => _primaryEntity = id;
            EventBus.SceneUnloaded += () =>
            {
                SelectedEntities.Clear();
                _primaryEntity = -1;
            };
            EventBus.EntityDestroyed += id =>
            {
                SelectedEntities.Remove(id);
                if (_primaryEntity == id)
                {
                    int next = FirstSelected();
                    if (next != _primaryEntity)
                        _primaryEntity = next; // no event needed — entity was already destroyed
                }
            };
        }

        /// <summary>Replace the entire selection with a single entity (or deselect when id == -1).</summary>
        public static void SelectEntity(int id)
        {
            SelectedEntities.Clear();
            if (id >= 0) SelectedEntities.Add(id);
            if (_primaryEntity != id)
                EventBus.RaiseEntitySelected(id);
        }

        /// <summary>
        /// Adds <paramref name="id"/> to the selection, or removes it if already selected.
        /// The primary entity shifts to <paramref name="id"/> when adding, or to the first
        /// remaining entity (or -1) when removing.
        /// </summary>
        public static void ToggleEntityInSelection(int id)
        {
            if (id < 0) return;
            if (SelectedEntities.Contains(id))
            {
                SelectedEntities.Remove(id);
                int newPrimary = FirstSelected();
                if (_primaryEntity != newPrimary)
                    EventBus.RaiseEntitySelected(newPrimary);
            }
            else
            {
                SelectedEntities.Add(id);
                if (_primaryEntity != id)
                    EventBus.RaiseEntitySelected(id);
            }
        }

        /// <summary>
        /// Adds <paramref name="id"/> to the selection without toggling (Shift+click style).
        /// If already selected it becomes the new primary without deselecting anything.
        /// </summary>
        public static void AddToSelection(int id)
        {
            if (id < 0) return;
            SelectedEntities.Add(id);
            if (_primaryEntity != id)
                EventBus.RaiseEntitySelected(id);
        }

        public static void Deselect()
        {
            SelectedEntities.Clear();
            if (_primaryEntity != -1)
                EventBus.RaiseEntitySelected(-1);
        }

        // Returns the first element in SelectedEntities, or -1 if empty.
        private static int FirstSelected()
        {
            foreach (int id in SelectedEntities) return id;
            return -1;
        }

        /// <summary>
        /// Duplicates all currently selected entities, selects the copies, and offsets each by
        /// (0.5, 0, 0).  Keyboard shortcut: Ctrl+D / Cmd+D.
        /// </summary>
        public static void DuplicateSelected()
        {
            var scene = SceneManager.ActiveScene;
            if (scene == null) return;

            var world  = ECSWorld.Instance;
            var srcIds = new List<int>(SelectedEntities);
            if (srcIds.Count == 0) return;

            var newIds = new List<int>(srcIds.Count);
            foreach (int srcId in srcIds)
            {
                world.TryGetComponent<NameTag>(srcId, out var srcName);
                int newId = scene.CreateEntity(srcName.Name);

                if (world.TryGetComponent<Transform>(srcId, out var srcTr))
                {
                    srcTr.Position += new Vector3(0.5f, 0f, 0f);
                    srcTr.Parent    = null;
                    world.AddComponent(newId, srcTr);
                }
                if (world.TryGetComponent<ActiveFlag>(srcId, out var af))
                    world.AddComponent(newId, af);
                if (world.TryGetComponent<MeshRenderer>(srcId, out var mr))
                    world.AddComponent(newId, mr);
                if (world.TryGetComponent<CameraComponent>(srcId, out var cam))
                {
                    cam.IsMain = false;   // never duplicate main-camera designation
                    world.AddComponent(newId, cam);
                }
                if (world.TryGetComponent<LightComponent>(srcId, out var lt))
                    world.AddComponent(newId, lt);
                if (world.TryGetComponent<RigidbodyComponent>(srcId, out var rb))
                    world.AddComponent(newId, rb);
                if (world.TryGetComponent<ScriptComponent>(srcId, out var sc))
                    world.AddComponent(newId, sc);

                newIds.Add(newId);
            }

            // Select all copies (last copy becomes primary)
            SelectedEntities.Clear();
            foreach (int newId in newIds) SelectedEntities.Add(newId);
            int primary = newIds.Count > 0 ? newIds[newIds.Count - 1] : -1;
            if (_primaryEntity != primary)
                EventBus.RaiseEntitySelected(primary);

            Logger.Info($"Duplicated {newIds.Count} entit{(newIds.Count == 1 ? "y" : "ies")}.");
        }
    }
}
