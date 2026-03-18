using System;
using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Scene;

namespace GameEditor.UI
{
    public static unsafe class HierarchyPanel
    {
        private static int _renamingId = -1;
        private static byte[] _renameBuffer = new byte[128];
        private static bool _renameFocusPending = false;
        private static bool _showEntityIds = false;

        public static void Draw()
        {
            byte open = 1;
            if (!igBegin("Hierarchy", ref open, ImGuiWindowFlags.None))
            {
                igEnd();
                return;
            }

            var scene = SceneManager.ActiveScene;
            if (scene == null)
            {
                igText("No active scene");
                igEnd();
                return;
            }

            // Header row: [+ Entity] button  +  [ID] toggle on the right
            Vector2 _hierarchyAvail = default;
            igGetContentRegionAvail(ref _hierarchyAvail);
            float btnW = _hierarchyAvail.X - 52f;
            if (igButton("+ Entity", new Vector2(btnW, 0)))
                igOpenPopup_Str("##create_entity_popup", ImGuiPopupFlags.None);

            if (igBeginPopup("##create_entity_popup", ImGuiWindowFlags.None))
            {
                if (igMenuItem_Bool("Empty Entity", null, false, true))
                {
                    int newId = scene.CreateEntity("Entity");
                    EditorState.SelectEntity(newId);
                    igCloseCurrentPopup();
                }

                if (igMenuItem_Bool("Camera", null, false, true))
                {
                    int newId = scene.CreateEntity("Camera");
                    var existingCameras = ECSWorld.Instance.GetAllComponents<CameraComponent>();
                    bool hasMainCamera = false;
                    foreach (var cam in existingCameras.Values)
                        if (cam.IsMain) { hasMainCamera = true; break; }
                    ECSWorld.Instance.AddComponent(newId, new CameraComponent
                    {
                        Fov = 60f,
                        NearZ = 0.1f,
                        FarZ = 1000f,
                        IsMain = !hasMainCamera,
                        IsOrthographic = false,
                        OrthoSize = 5f
                    });
                    EditorState.SelectEntity(newId);
                    scene.IsDirty = true;
                    igCloseCurrentPopup();
                }

                if (igMenuItem_Bool("Light", null, false, true))
                {
                    int newId = scene.CreateEntity("Light");
                    ECSWorld.Instance.AddComponent(newId, new LightComponent
                    {
                        Type = LightType.Directional,
                        Color = Vector3.One,
                        Intensity = 1f,
                        Range = 10f,
                        InnerAngle = 25f,
                        OuterAngle = 35f
                    });
                    EditorState.SelectEntity(newId);
                    scene.IsDirty = true;
                    igCloseCurrentPopup();
                }

                if (igBeginMenu("3D Primitives", true))
                {
                    if (igMenuItem_Bool("Box", null, false, true))
                    {
                        CreatePrimitiveEntity(scene, "Box", PrimitiveKind.Box);
                        igCloseCurrentPopup();
                    }
                    if (igMenuItem_Bool("Sphere", null, false, true))
                    {
                        CreatePrimitiveEntity(scene, "Sphere", PrimitiveKind.Sphere);
                        igCloseCurrentPopup();
                    }
                    if (igMenuItem_Bool("Plane", null, false, true))
                    {
                        CreatePrimitiveEntity(scene, "Plane", PrimitiveKind.Plane);
                        igCloseCurrentPopup();
                    }
                    if (igMenuItem_Bool("Cylinder", null, false, true))
                    {
                        CreatePrimitiveEntity(scene, "Cylinder", PrimitiveKind.Cylinder);
                        igCloseCurrentPopup();
                    }
                    if (igMenuItem_Bool("Cone", null, false, true))
                    {
                        CreatePrimitiveEntity(scene, "Cone", PrimitiveKind.Cone);
                        igCloseCurrentPopup();
                    }
                    if (igMenuItem_Bool("Ring", null, false, true))
                    {
                        CreatePrimitiveEntity(scene, "Ring", PrimitiveKind.Ring);
                        igCloseCurrentPopup();
                    }
                    if (igMenuItem_Bool("Pyramid", null, false, true))
                    {
                        CreatePrimitiveEntity(scene, "Pyramid", PrimitiveKind.Pyramid);
                        igCloseCurrentPopup();
                    }
                    igEndMenu();
                }

                igEndPopup();
            }
            igSameLine(0, 4);
            byte showIds = _showEntityIds ? (byte)1 : (byte)0;
            if (igCheckbox("ID##showId", ref showIds))
                _showEntityIds = showIds != 0;
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip("Show entity IDs");

            igSeparator();

            var world = ECSWorld.Instance;
            // Snapshot to avoid "collection modified" if an entity is deleted during Draw
            var entities = new System.Collections.Generic.List<int>(world.Entities);
            foreach (int id in entities)
            {
                world.TryGetComponent<NameTag>(id, out var nameTag);
                world.TryGetComponent<Transform>(id, out var transform);

                // Skip children drawn by parent
                if (transform.Parent.HasValue) continue;

                DrawEntityNode(id, world, scene);
            }

            // Deselect on click in blank area (not on any item)
            if (igIsWindowHovered(ImGuiHoveredFlags.None) && igIsMouseClicked_Bool(ImGuiMouseButton.Left, false)
                && igGetIO_Nil()->KeyCtrl == 0)
            {
                EditorState.Deselect();
            }

            igEnd();
        }

        private static void DrawEntityNode(int id, ECSWorld world, Scene scene)
        {
            world.TryGetComponent<NameTag>(id, out var nameTag);
            string baseName = string.IsNullOrEmpty(nameTag.Name) ? "Entity" : nameTag.Name;
            string name = _showEntityIds ? $"{baseName}  [{id}]" : baseName;

            bool isSelected = EditorState.IsSelected(id);
            bool isRenaming = _renamingId == id;

            // Determine whether this entity has any children
            bool hasChildren = false;
            foreach (int cid in world.Entities)
            {
                if (world.TryGetComponent<Transform>(cid, out var ct) && ct.Parent == id)
                { hasChildren = true; break; }
            }

            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
            if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf;
            if (isSelected)   flags |= ImGuiTreeNodeFlags.Selected;

            if (isRenaming)
            {
                // Draw the tree node label first (invisible, for layout), then overlay input
                igPushID_Int(id);

                if (_renameFocusPending)
                {
                    igSetKeyboardFocusHere(0);
                    _renameFocusPending = false;
                }

                // InputText inline
                bool submitted = igInputText("##rename", ref _renameBuffer[0], (uint)_renameBuffer.Length,
                    ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll, null, null);

                if (submitted)
                {
                    CommitRename(id, world);
                }
                else if (igIsItemDeactivated())
                {
                    // Input lost focus: commit on click-away, cancel on Escape
                    if (igIsKeyPressed_Bool(ImGuiKey.Escape, false))
                        _renamingId = -1;
                    else
                        CommitRename(id, world);
                }

                igPopID();
            }
            else
            {
                bool open = igTreeNodeEx_StrStr($"##entity{id}", flags, name);

                // Drag source: drag this entity
                if (igBeginDragDropSource(ImGuiDragDropFlags.None))
                {
                    igSetDragDropPayload("ENTITY_ID", (void*)(&id), 4, ImGuiCond.None);
                    igTextUnformatted(name, null);
                    igEndDragDropSource();
                }

                // Drop target: make dragged entity a child of this one, or assign a script type
                if (igBeginDragDropTarget())
                {
                    var payload = igAcceptDragDropPayload("ENTITY_ID", ImGuiDragDropFlags.None);
                    if (payload != null && payload->DataSize == 4)
                    {
                        int draggedId = *(int*)payload->Data;
                        if (draggedId != id && world.TryGetComponent<Transform>(draggedId, out var dt))
                        {
                            dt.Parent = id;
                            world.AddComponent(draggedId, dt);
                            scene.IsDirty = true;
                        }
                    }

                    // Accept a script type dropped from the inspector
                    var sp = igAcceptDragDropPayload("SCRIPT_TYPE", ImGuiDragDropFlags.None);
                    if (sp != null && sp->DataSize > 0)
                    {
                        string droppedType = System.Text.Encoding.UTF8.GetString((byte*)sp->Data, sp->DataSize);
                        if (!string.IsNullOrEmpty(droppedType))
                        {
                            AttachScriptToEntity(world, id, droppedType);
                            EditorState.SelectEntity(id);
                            scene.IsDirty = true;
                        }
                    }

                    // Accept a .cs file dragged from the Assets panel
                    var sfp = igAcceptDragDropPayload("SCRIPT_FILE", ImGuiDragDropFlags.None);
                    if (sfp != null && sfp->DataSize > 0)
                    {
                        string droppedPath = System.Text.Encoding.UTF8.GetString((byte*)sfp->Data, sfp->DataSize);
                        string className   = System.IO.Path.GetFileNameWithoutExtension(droppedPath);
                        if (!string.IsNullOrEmpty(className))
                        {
                            AttachScriptToEntity(world, id, className);
                            EditorState.SelectEntity(id);
                            scene.IsDirty = true;
                        }
                    }

                    igEndDragDropTarget();
                }

                if (igIsItemClicked(ImGuiMouseButton.Left))
                {
                    if (igGetIO_Nil()->KeyCtrl != 0)
                        EditorState.ToggleEntityInSelection(id);
                    else if (igGetIO_Nil()->KeyShift != 0)
                        EditorState.AddToSelection(id);
                    else
                        EditorState.SelectEntity(id);
                }

                // Double-click to rename
                if (igIsItemHovered(ImGuiHoveredFlags.None) && igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left))
                    BeginRename(id, name);

                // Right-click context menu
                if (igBeginPopupContextItem($"##ctx{id}", ImGuiPopupFlags.MouseButtonRight))
                {
                    if (igMenuItem_Bool("Rename", null, false, true))
                        BeginRename(id, name);

                    if (igMenuItem_Bool("Duplicate", "Ctrl+D", false, true))
                    {
                        // Ensure this entity is the selection so DuplicateSelected copies it
                        if (!EditorState.IsSelected(id))
                            EditorState.SelectEntity(id);
                        EditorState.DuplicateSelected();
                        igEndPopup();
                        if (open) igTreePop();
                        return;
                    }

                    world.TryGetComponent<Transform>(id, out var ctxTransform);
                    if (ctxTransform.Parent.HasValue)
                    {
                        if (igMenuItem_Bool("Clear Parent", null, false, true))
                        {
                            ctxTransform.Parent = null;
                            world.AddComponent(id, ctxTransform);
                            scene.IsDirty = true;
                        }
                    }

                    igSeparator();

                    // Bulk-delete when multiple entities are selected
                    int selCount = EditorState.SelectionCount;
                    if (selCount > 1 && EditorState.IsSelected(id))
                    {
                        if (igMenuItem_Bool($"Delete {selCount} Entities", null, false, true))
                        {
                            var toDelete = new System.Collections.Generic.List<int>(EditorState.SelectedEntities);
                            foreach (int did in toDelete)
                                scene.DestroyEntity(did);
                            igEndPopup();
                            if (open) igTreePop();
                            return;
                        }
                    }
                    else if (igMenuItem_Bool("Delete", null, false, true))
                    {
                        scene.DestroyEntity(id);
                        igEndPopup();
                        if (open) igTreePop();
                        return;
                    }
                    igEndPopup();
                }

                if (open)
                {
                    // Recursively draw children (snapshot to avoid mutation during iteration)
                    var children = new System.Collections.Generic.List<int>();
                    foreach (int cid in world.Entities)
                        if (world.TryGetComponent<Transform>(cid, out var ct) && ct.Parent == id)
                            children.Add(cid);
                    foreach (int cid in children)
                        DrawEntityNode(cid, world, scene);
                    igTreePop();
                }
            }
        }

        private static void AttachScriptToEntity(ECSWorld world, int entityId, string typeName)
        {
            if (!world.TryGetComponent<ScriptComponent>(entityId, out var primary) || string.IsNullOrEmpty(primary.TypeName))
            {
                world.AddComponent(entityId, new ScriptComponent { TypeName = typeName });
                return;
            }

            world.TryGetComponent<ScriptCollectionComponent>(entityId, out var scc);
            scc.Scripts ??= new System.Collections.Generic.List<ScriptComponent>();
            scc.Scripts.Add(new ScriptComponent { TypeName = typeName });
            world.AddComponent(entityId, scc);
        }

        private static void BeginRename(int id, string currentName)
        {
            _renamingId = id;
            _renameBuffer = new byte[128];
            var bytes = System.Text.Encoding.UTF8.GetBytes(currentName);
            int copyLen = Math.Min(bytes.Length, _renameBuffer.Length - 1);
            System.Array.Copy(bytes, _renameBuffer, copyLen);
            _renameFocusPending = true;
        }

        private static void CommitRename(int id, ECSWorld world)
        {
            int len = System.Array.IndexOf(_renameBuffer, (byte)0);
            string newName = len > 0
                ? System.Text.Encoding.UTF8.GetString(_renameBuffer, 0, len)
                : $"Entity {id}";

            world.TryGetComponent<NameTag>(id, out var tag);
            tag.Name = newName;
            world.AddComponent(id, tag);

            _renamingId = -1;
        }

        private static void CreatePrimitiveEntity(Scene scene, string name, PrimitiveKind kind)
        {
            int id = scene.CreateEntity(name);
            string meshPath = PrimitiveMeshSpec.ToMeshPath(PrimitiveMeshSpec.Default(kind));
            ECSWorld.Instance.AddComponent(id, new MeshRenderer { MeshPath = meshPath, Visible = true });
            scene.IsDirty = true;
            EditorState.SelectEntity(id);
        }
    }
}
