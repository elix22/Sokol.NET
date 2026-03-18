using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scene;

namespace GameEditor.UI
{
    public static unsafe class InspectorPanel
    {
        public static void Draw()
        {
            byte open = 1;
            if (!igBegin("Inspector", ref open, ImGuiWindowFlags.None))
            { igEnd(); return; }

            // Window-level drop target — must be called immediately after igBegin()
            // while the window is the "last item" so ImGui registers the hover correctly.
            if (igBeginDragDropTarget())
            {
                int curId = EditorState.SelectedEntity;
                if (curId >= 0)
                {
                    var sfp = igAcceptDragDropPayload("SCRIPT_FILE", ImGuiDragDropFlags.None);
                    if (sfp != null && sfp->DataSize > 0)
                    {
                        string droppedPath = System.Text.Encoding.UTF8.GetString((byte*)sfp->Data, sfp->DataSize);
                        string className   = System.IO.Path.GetFileNameWithoutExtension(droppedPath);
                        if (!string.IsNullOrEmpty(className))
                        {
                            var w = ECSWorld.Instance;
                            w.TryGetComponent<ScriptComponent>(curId, out var existing);
                            w.AddComponent(curId, new ScriptComponent { TypeName = className, Properties = existing.Properties });
                            SceneManager.ActiveScene!.IsDirty = true;
                        }
                    }
                    var stp = igAcceptDragDropPayload("SCRIPT_TYPE", ImGuiDragDropFlags.None);
                    if (stp != null && stp->DataSize > 0)
                    {
                        string droppedType = System.Text.Encoding.UTF8.GetString((byte*)stp->Data, stp->DataSize);
                        if (!string.IsNullOrEmpty(droppedType))
                        {
                            var w = ECSWorld.Instance;
                            w.TryGetComponent<ScriptComponent>(curId, out var existing);
                            w.AddComponent(curId, new ScriptComponent { TypeName = droppedType, Properties = existing.Properties });
                            SceneManager.ActiveScene!.IsDirty = true;
                        }
                    }
                }
                igEndDragDropTarget();
            }

            int id = EditorState.SelectedEntity;
            if (id < 0)
            { igText("No entity selected"); igEnd(); return; }

            var world = ECSWorld.Instance;

            // ── Multi-selection banner ────────────────────────────────────
            int selCount = EditorState.SelectionCount;
            if (selCount > 1)
            {
                igPushStyleColor_Vec4(ImGuiCol.ChildBg, new Vector4(0.20f, 0.30f, 0.45f, 0.40f));
                igBeginChild_Str("##multiSelBanner", new Vector2(-1, 48), ImGuiChildFlags.None, ImGuiWindowFlags.None);
                igSpacing();
                igTextColored(new Vector4(0.55f, 0.85f, 1.00f, 1f), $"{selCount} entities selected  (Ctrl+Click to toggle)");
                igSpacing();
                if (igButton($"Delete All ({selCount})##deleteAll", new Vector2(-1, 0)))
                {
                    var scene = SceneManager.ActiveScene;
                    if (scene != null)
                    {
                        var toDelete = new System.Collections.Generic.List<int>(EditorState.SelectedEntities);
                        foreach (int did in toDelete)
                            scene.DestroyEntity(did);
                    }
                    igEndChild();
                    igPopStyleColor(1);
                    igEnd();
                    return;
                }
                igEndChild();
                igPopStyleColor(1);
                igSeparator();
                igSpacing();
            }

            // ── Entity header ────────────────────────────────────────────
            igSeparatorText("Entity");

            // Active flag checkbox
            world.TryGetComponent<ActiveFlag>(id, out var af);
            byte active = af.Active ? (byte)1 : (byte)0;
            if (igCheckbox("Active##ent", ref active))
            {
                bool nv = active != 0; bool ov = af.Active; int cid = id;
                UndoStack.Record(new DelegateCommand($"Entity {cid} Active",
                    () => { world.AddComponent(cid, new ActiveFlag { Active = nv }); },
                    () => { world.AddComponent(cid, new ActiveFlag { Active = ov }); }));
            }

            igSameLine(0, 12);
            world.TryGetComponent<NameTag>(id, out var nameTag);
            igText($"ID: {id}  Name: {nameTag.Name}");

            igSpacing();

            // ── Components ───────────────────────────────────────────────

            // Transform (non-removable)
            if (world.TryGetComponent<Transform>(id, out var transform))
                ComponentDrawers.DrawTransform(id, ref transform);

            // MeshRenderer
            if (world.TryGetComponent<MeshRenderer>(id, out var mesh))
            {
                ComponentDrawers.DrawMeshRenderer(id, ref mesh);
                DrawRemoveButton("MeshRenderer", id, mesh);
            }

            // CameraComponent
            if (world.TryGetComponent<CameraComponent>(id, out var cam))
            {
                ComponentDrawers.DrawCamera(id, ref cam);
                DrawRemoveButton("CameraComponent", id, cam);
            }

            // LightComponent
            if (world.TryGetComponent<LightComponent>(id, out var light))
            {
                ComponentDrawers.DrawLight(id, ref light);
                DrawRemoveButton("LightComponent", id, light);
            }

            // RigidbodyComponent
            if (world.TryGetComponent<RigidbodyComponent>(id, out var rb))
            {
                ComponentDrawers.DrawRigidbody(id, ref rb);
                DrawRemoveButton("RigidbodyComponent", id, rb);
            }

            // ScriptComponent
            if (world.TryGetComponent<ScriptComponent>(id, out var sc))
            {
                ComponentDrawers.DrawScriptComponent(id, ref sc);
                DrawRemoveButton("ScriptComponent", id, sc);
            }

            igSpacing();
            igSeparator();

            // ── Add Component button ──────────────────────────────────────
            igSetCursorPosX((igGetWindowWidth() - 160f) * 0.5f);
            if (igButton("Add Component", new Vector2(160, 0)))
                igOpenPopup_Str("##add_component", ImGuiPopupFlags.None);

            if (igBeginPopup("##add_component", ImGuiWindowFlags.None))
            {
                if (igMenuItem_Bool("Mesh Renderer", null, false, !world.HasComponent<MeshRenderer>(id)))
                {
                    var snap = new MeshRenderer { Visible = true }; int cid = id;
                    UndoStack.Record(new DelegateCommand("Add MeshRenderer",
                        () => world.AddComponent(cid, snap),
                        () => world.RemoveComponent<MeshRenderer>(cid)));
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Camera", null, false, !world.HasComponent<CameraComponent>(id)))
                {
                    // Auto-assign Main Camera if no other camera is currently marked as main
                    bool anyMain = false;
                    foreach (int eid in world.Entities)
                        if (world.TryGetComponent<CameraComponent>(eid, out var existing) && existing.IsMain)
                        { anyMain = true; break; }
                    var snap = new CameraComponent { Fov = 60f, NearZ = 0.1f, FarZ = 1000f, IsMain = !anyMain }; int cid = id;
                    UndoStack.Record(new DelegateCommand("Add CameraComponent",
                        () => world.AddComponent(cid, snap),
                        () => world.RemoveComponent<CameraComponent>(cid)));
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Light", null, false, !world.HasComponent<LightComponent>(id)))
                {
                    var snap = new LightComponent { Type = LightType.Directional, Color = Vector3.One, Intensity = 1f }; int cid = id;
                    UndoStack.Record(new DelegateCommand("Add LightComponent",
                        () => world.AddComponent(cid, snap),
                        () => world.RemoveComponent<LightComponent>(cid)));
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Rigidbody", null, false, !world.HasComponent<RigidbodyComponent>(id)))
                {
                    var snap = new RigidbodyComponent { Mass = 1f, UseGravity = true }; int cid = id;
                    UndoStack.Record(new DelegateCommand("Add RigidbodyComponent",
                        () => world.AddComponent(cid, snap),
                        () => world.RemoveComponent<RigidbodyComponent>(cid)));
                    igCloseCurrentPopup();
                }
                if (igMenuItem_Bool("Script", null, false, !world.HasComponent<ScriptComponent>(id)))
                {
                    var snap = new ScriptComponent { TypeName = "" }; int cid = id;
                    UndoStack.Record(new DelegateCommand("Add ScriptComponent",
                        () => world.AddComponent(cid, snap),
                        () => world.RemoveComponent<ScriptComponent>(cid)));
                    igCloseCurrentPopup();
                }
                igEndPopup();
            }

            igEnd();
        }

        private static void DrawRemoveButton<T>(string componentName, int id, T snapshot) where T : struct
        {
            igPushID_Str($"##rm_{componentName}_{id}");
            igSameLine(igGetWindowWidth() - 28f, 0);
            if (igSmallButton("X"))
            {
                int cid = id;
                T before = snapshot;
                UndoStack.Record(new DelegateCommand($"Remove {componentName}",
                    () =>
                    {
                        ECSWorld.Instance.RemoveComponent<T>(cid);
                        EventBus.RaiseComponentChanged(cid, componentName);
                    },
                    () =>
                    {
                        ECSWorld.Instance.AddComponent(cid, before);
                        EventBus.RaiseComponentChanged(cid, componentName);
                    }));
            }
            igPopID();
        }

        private static float igGetWindowWidth() 
        {
            return Imgui.ImguiNative.igGetWindowWidth();
        }
    }
}
