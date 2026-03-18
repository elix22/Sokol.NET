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
                            AttachScriptToEntity(curId, className);
                    }
                    var stp = igAcceptDragDropPayload("SCRIPT_TYPE", ImGuiDragDropFlags.None);
                    if (stp != null && stp->DataSize > 0)
                    {
                        string droppedType = System.Text.Encoding.UTF8.GetString((byte*)stp->Data, stp->DataSize);
                        if (!string.IsNullOrEmpty(droppedType))
                            AttachScriptToEntity(curId, droppedType);
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
                ComponentDrawers.DrawScriptComponent(id, ref sc, "Script", "0");
                world.AddComponent(id, sc);
                DrawRemovePrimaryScriptButton(id, sc);
            }

            if (world.TryGetComponent<ScriptCollectionComponent>(id, out var scc))
            {
                scc.Scripts ??= new System.Collections.Generic.List<ScriptComponent>();
                bool listChanged = false;
                int removeIndex = -1;

                for (int i = 0; i < scc.Scripts.Count; i++)
                {
                    var item = scc.Scripts[i];
                    ComponentDrawers.DrawScriptComponent(id, ref item, $"Script {i + 2}", (i + 1).ToString());
                    scc.Scripts[i] = item;

                    igPushID_Str($"scr_extra_rm_{id}_{i}");
                    if (igSmallButton("Remove This Script"))
                        removeIndex = i;
                    igPopID();
                }

                if (removeIndex >= 0)
                {
                    scc.Scripts.RemoveAt(removeIndex);
                    listChanged = true;
                }

                if (igButton("+ Add Script", new Vector2(-1, 0)))
                {
                    scc.Scripts.Add(new ScriptComponent { TypeName = "" });
                    listChanged = true;
                }

                if (listChanged)
                {
                    if (scc.Scripts.Count == 0)
                        world.RemoveComponent<ScriptCollectionComponent>(id);
                    else
                        world.AddComponent(id, scc);
                    if (SceneManager.ActiveScene != null)
                        SceneManager.ActiveScene.IsDirty = true;
                    EventBus.RaiseComponentChanged(id, nameof(ScriptComponent));
                }
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
                if (igMenuItem_Bool("Script", null, false, true))
                {
                    int cid = id;
                    if (!world.HasComponent<ScriptComponent>(cid))
                    {
                        var snap = new ScriptComponent { TypeName = "" };
                        UndoStack.Record(new DelegateCommand("Add ScriptComponent",
                            () => world.AddComponent(cid, snap),
                            () => world.RemoveComponent<ScriptComponent>(cid)));
                    }
                    else
                    {
                        world.TryGetComponent<ScriptCollectionComponent>(cid, out var extraScripts);
                        extraScripts.Scripts ??= new System.Collections.Generic.List<ScriptComponent>();
                        var before = new System.Collections.Generic.List<ScriptComponent>(extraScripts.Scripts);
                        extraScripts.Scripts.Add(new ScriptComponent { TypeName = "" });
                        var after = new System.Collections.Generic.List<ScriptComponent>(extraScripts.Scripts);
                        UndoStack.Record(new DelegateCommand("Add Additional Script",
                            () => world.AddComponent(cid, new ScriptCollectionComponent { Scripts = new System.Collections.Generic.List<ScriptComponent>(after) }),
                            () =>
                            {
                                if (before.Count == 0)
                                    world.RemoveComponent<ScriptCollectionComponent>(cid);
                                else
                                    world.AddComponent(cid, new ScriptCollectionComponent { Scripts = new System.Collections.Generic.List<ScriptComponent>(before) });
                            }));
                    }
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

        private static void AttachScriptToEntity(int entityId, string typeName)
        {
            var w = ECSWorld.Instance;
            if (!w.TryGetComponent<ScriptComponent>(entityId, out var primary) || string.IsNullOrEmpty(primary.TypeName))
            {
                w.AddComponent(entityId, new ScriptComponent { TypeName = typeName });
            }
            else
            {
                w.TryGetComponent<ScriptCollectionComponent>(entityId, out var scc);
                scc.Scripts ??= new System.Collections.Generic.List<ScriptComponent>();
                scc.Scripts.Add(new ScriptComponent { TypeName = typeName });
                w.AddComponent(entityId, scc);
            }
            if (SceneManager.ActiveScene != null)
                SceneManager.ActiveScene.IsDirty = true;
            EventBus.RaiseComponentChanged(entityId, nameof(ScriptComponent));
        }

        private static void DrawRemovePrimaryScriptButton(int id, ScriptComponent snapshot)
        {
            igPushID_Str($"##rm_primary_script_{id}");
            igSameLine(igGetWindowWidth() - 28f, 0);
            if (igSmallButton("X"))
            {
                var world = ECSWorld.Instance;
                int cid = id;
                ScriptComponent beforePrimary = snapshot;
                world.TryGetComponent<ScriptCollectionComponent>(cid, out var beforeCollection);

                UndoStack.Record(new DelegateCommand("Remove Primary Script",
                    () =>
                    {
                        if (world.TryGetComponent<ScriptCollectionComponent>(cid, out var scc)
                            && scc.Scripts != null && scc.Scripts.Count > 0)
                        {
                            var promoted = scc.Scripts[0];
                            scc.Scripts.RemoveAt(0);
                            world.AddComponent(cid, promoted);
                            if (scc.Scripts.Count == 0)
                                world.RemoveComponent<ScriptCollectionComponent>(cid);
                            else
                                world.AddComponent(cid, scc);
                        }
                        else
                        {
                            world.RemoveComponent<ScriptComponent>(cid);
                        }
                        EventBus.RaiseComponentChanged(cid, nameof(ScriptComponent));
                    },
                    () =>
                    {
                        world.AddComponent(cid, beforePrimary);
                        if (beforeCollection.Scripts == null || beforeCollection.Scripts.Count == 0)
                            world.RemoveComponent<ScriptCollectionComponent>(cid);
                        else
                            world.AddComponent(cid, beforeCollection);
                        EventBus.RaiseComponentChanged(cid, nameof(ScriptComponent));
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
