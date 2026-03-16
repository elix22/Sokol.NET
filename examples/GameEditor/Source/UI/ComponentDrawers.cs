using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer;

namespace GameEditor.UI
{
    /// <summary>
    /// Per-component Inspector drawers.
    /// Drag-type widgets apply changes live each frame; undo is recorded on
    /// igIsItemDeactivatedAfterEdit() so one drag = one undo step.
    /// Instant toggles/checkboxes use UndoStack.Record() directly.
    /// </summary>
    public static unsafe class ComponentDrawers
    {
        // ── Undo snapshot state ─────────────────────────────────────────────
        private static int       _transformUndoId     = -1;
        private static Transform _transformUndoBefore;

        private static int             _cameraUndoId     = -1;
        private static CameraComponent _cameraUndoBefore;

        private static int           _lightUndoId     = -1;
        private static LightComponent _lightUndoBefore;

        private static int                _rbUndoId     = -1;
        private static RigidbodyComponent _rbUndoBefore;

        private static int    _meshUndoId = -1;
        private static string _meshUndoBeforePath = string.Empty;

        private static readonly string[] _primitiveNames =
        {
            "Box", "Sphere", "Plane", "Cylinder", "Cone", "Ring", "Pyramid"
        };

        // ── Transform ───────────────────────────────────────────────────────
        public static void DrawTransform(int id, ref Transform t)
        {
            if (!igCollapsingHeader_TreeNodeFlags("Transform", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            // Reset snapshot when switching entity
            if (_transformUndoId != id) { _transformUndoId = id; _transformUndoBefore = t; }

            bool changed = false;
            changed |= igDragFloat3("Position", ref t.Position,    0.01f, float.MinValue, float.MaxValue, "%.3f",  ImGuiSliderFlags.None);
            bool posDeact = igIsItemDeactivatedAfterEdit();

            changed |= igDragFloat3("Rotation", ref t.EulerAngles, 0.5f,  float.MinValue, float.MaxValue, "%.2f°", ImGuiSliderFlags.None);
            bool rotDeact = igIsItemDeactivatedAfterEdit();

            changed |= igDragFloat3("Scale",    ref t.Scale,       0.01f, 0.001f,         float.MaxValue, "%.3f",  ImGuiSliderFlags.None);
            bool scaleDeact = igIsItemDeactivatedAfterEdit();

            if (changed)
            {
                ECSWorld.Instance.AddComponent(id, t);
                EventBus.RaiseComponentChanged(id, nameof(Transform));
            }

            if (posDeact || rotDeact || scaleDeact)
            {
                var before = _transformUndoBefore; var after = t; int cid = id;
                UndoStack.RecordAlreadyExecuted(new DelegateCommand("Transform",
                    () => { ECSWorld.Instance.AddComponent(cid, after);  EventBus.RaiseComponentChanged(cid, nameof(Transform)); },
                    () => { ECSWorld.Instance.AddComponent(cid, before); EventBus.RaiseComponentChanged(cid, nameof(Transform)); }));
                _transformUndoBefore = t;
            }
        }

        // ── MeshRenderer ────────────────────────────────────────────────────
        public static void DrawMeshRenderer(int id, ref MeshRenderer m)
        {
            if (!igCollapsingHeader_TreeNodeFlags("Mesh Renderer", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            if (_meshUndoId != id)
            {
                _meshUndoId = id;
                _meshUndoBeforePath = m.MeshPath ?? string.Empty;
            }

            igText($"Mesh: {(string.IsNullOrEmpty(m.MeshPath) ? "(none)" : m.MeshPath)}");

            bool meshChanged = false;
            bool meshCommitted = false;

            if (PrimitiveMeshSpec.TryParse(m.MeshPath, out var spec))
            {
                int kindIdx = (int)spec.Kind;
                if (igCombo_Str_arr("Primitive", ref kindIdx, _primitiveNames, _primitiveNames.Length, -1))
                {
                    spec = PrimitiveMeshSpec.Default((PrimitiveKind)kindIdx);
                    meshChanged = true;
                    meshCommitted = true;
                }

                switch (spec.Kind)
                {
                    case PrimitiveKind.Box:
                        meshChanged |= igDragFloat("Width", ref spec.Width, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragFloat("Height", ref spec.Height, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragFloat("Depth", ref spec.Depth, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragInt("Tiles", ref spec.Tiles, 0.2f, 1, 64, "%d", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        break;

                    case PrimitiveKind.Sphere:
                        meshChanged |= igDragFloat("Radius", ref spec.Radius, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragInt("Slices", ref spec.Slices, 0.3f, 3, 128, "%d", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragInt("Stacks", ref spec.Stacks, 0.3f, 1, 128, "%d", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        break;

                    case PrimitiveKind.Plane:
                        meshChanged |= igDragFloat("Width", ref spec.Width, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragFloat("Depth", ref spec.Depth, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragInt("Tiles", ref spec.Tiles, 0.2f, 1, 256, "%d", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        break;

                    case PrimitiveKind.Cylinder:
                    case PrimitiveKind.Cone:
                    case PrimitiveKind.Pyramid:
                        meshChanged |= igDragFloat("Radius", ref spec.Radius, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragFloat("Height", ref spec.Height, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        if (spec.Kind == PrimitiveKind.Cylinder || spec.Kind == PrimitiveKind.Cone)
                        {
                            meshChanged |= igDragInt("Slices", ref spec.Slices, 0.3f, 3, 128, "%d", ImGuiSliderFlags.None);
                            meshCommitted |= igIsItemDeactivatedAfterEdit();
                        }
                        if (spec.Kind == PrimitiveKind.Cylinder)
                        {
                            meshChanged |= igDragInt("Stacks", ref spec.Stacks, 0.2f, 1, 32, "%d", ImGuiSliderFlags.None);
                            meshCommitted |= igIsItemDeactivatedAfterEdit();
                        }
                        if (spec.Kind == PrimitiveKind.Pyramid)
                        {
                            meshChanged |= igDragInt("Sides", ref spec.Sides, 0.2f, 3, 12, "%d", ImGuiSliderFlags.None);
                            meshCommitted |= igIsItemDeactivatedAfterEdit();
                        }
                        break;

                    case PrimitiveKind.Ring:
                        meshChanged |= igDragFloat("Radius", ref spec.Radius, 0.01f, 0.01f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragFloat("Ring Radius", ref spec.RingRadius, 0.01f, 0.005f, 1000f, "%.3f", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragInt("Sides", ref spec.Sides, 0.3f, 3, 128, "%d", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        meshChanged |= igDragInt("Rings", ref spec.Rings, 0.3f, 3, 128, "%d", ImGuiSliderFlags.None);
                        meshCommitted |= igIsItemDeactivatedAfterEdit();
                        break;
                }

                if (meshChanged)
                {
                    m.MeshPath = PrimitiveMeshSpec.ToMeshPath(spec);
                    ECSWorld.Instance.AddComponent(id, m);
                    EventBus.RaiseComponentChanged(id, nameof(MeshRenderer));
                }

                if (meshChanged && meshCommitted)
                {
                    string beforePath = _meshUndoBeforePath;
                    string afterPath = m.MeshPath ?? string.Empty;
                    int cid = id;
                    UndoStack.RecordAlreadyExecuted(new DelegateCommand("MeshRenderer.Primitive",
                        () =>
                        {
                            var c = ECSWorld.Instance.GetComponent<MeshRenderer>(cid);
                            c.MeshPath = afterPath;
                            ECSWorld.Instance.AddComponent(cid, c);
                            EventBus.RaiseComponentChanged(cid, nameof(MeshRenderer));
                        },
                        () =>
                        {
                            var c = ECSWorld.Instance.GetComponent<MeshRenderer>(cid);
                            c.MeshPath = beforePath;
                            ECSWorld.Instance.AddComponent(cid, c);
                            EventBus.RaiseComponentChanged(cid, nameof(MeshRenderer));
                        }));
                    _meshUndoBeforePath = afterPath;
                }
            }

            byte vis = m.Visible ? (byte)1 : (byte)0;
            if (igCheckbox("Visible", ref vis))
            {
                bool newVis = vis != 0; bool oldVis = m.Visible; int cid = id;
                UndoStack.Record(new DelegateCommand("MeshRenderer.Visible",
                    () => { var c = ECSWorld.Instance.GetComponent<MeshRenderer>(cid); c.Visible = newVis; ECSWorld.Instance.AddComponent(cid, c); },
                    () => { var c = ECSWorld.Instance.GetComponent<MeshRenderer>(cid); c.Visible = oldVis; ECSWorld.Instance.AddComponent(cid, c); }));
                m.Visible = newVis;
            }
        }

        // ── Camera ──────────────────────────────────────────────────────────
        public static void DrawCamera(int id, ref CameraComponent c)
        {
            if (!igCollapsingHeader_TreeNodeFlags("Camera", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            if (_cameraUndoId != id) { _cameraUndoId = id; _cameraUndoBefore = c; }

            bool changed = false;

            // Projection type
            int projIdx = c.IsOrthographic ? 1 : 0;
            if (igCombo_Str_arr("Projection", ref projIdx, _projTypeNames, _projTypeNames.Length, -1))
            {
                bool newOrtho = projIdx == 1; bool oldOrtho = c.IsOrthographic; int cid0 = id;
                c.IsOrthographic = newOrtho;
                if (c.IsOrthographic && c.OrthoSize <= 0f) c.OrthoSize = 5f;
                changed = true;
                UndoStack.Record(new DelegateCommand("Camera.Projection",
                    () => { var ch = ECSWorld.Instance.GetComponent<CameraComponent>(cid0); ch.IsOrthographic = newOrtho; ECSWorld.Instance.AddComponent(cid0, ch); },
                    () => { var ch = ECSWorld.Instance.GetComponent<CameraComponent>(cid0); ch.IsOrthographic = oldOrtho; ECSWorld.Instance.AddComponent(cid0, ch); }));
            }

            if (c.IsOrthographic)
            {
                if (c.OrthoSize <= 0f) c.OrthoSize = 5f;
                changed |= igDragFloat("Ortho Size", ref c.OrthoSize, 0.05f, 0.01f, 10000f, "%.2f", ImGuiSliderFlags.None);
                bool orthoDeact = igIsItemDeactivatedAfterEdit();
                if (orthoDeact) { var before = _cameraUndoBefore; var after = c; int cid2 = id; UndoStack.RecordAlreadyExecuted(new DelegateCommand("Camera", () => { ECSWorld.Instance.AddComponent(cid2, after); EventBus.RaiseComponentChanged(cid2, nameof(CameraComponent)); }, () => { ECSWorld.Instance.AddComponent(cid2, before); EventBus.RaiseComponentChanged(cid2, nameof(CameraComponent)); })); _cameraUndoBefore = c; }
            }
            else
            {
                changed |= igDragFloat("FOV",  ref c.Fov,   0.5f,   10f,    179f,      "%.1f°", ImGuiSliderFlags.None);
            }
            bool fovDeact  = igIsItemDeactivatedAfterEdit();
            changed |= igDragFloat("Near", ref c.NearZ, 0.001f, 0.001f, c.FarZ,   "%.4f",  ImGuiSliderFlags.None);
            bool nearDeact = igIsItemDeactivatedAfterEdit();
            changed |= igDragFloat("Far",  ref c.FarZ,  1f,     c.NearZ, 100000f, "%.1f",   ImGuiSliderFlags.None);
            bool farDeact  = igIsItemDeactivatedAfterEdit();

            byte isMain = c.IsMain ? (byte)1 : (byte)0;
            if (igCheckbox("Main Camera", ref isMain))
            {
                c.IsMain = isMain != 0; changed = true;
                bool newMain = c.IsMain; bool oldMain = !newMain; int cid = id;
                UndoStack.Record(new DelegateCommand("Camera.IsMain",
                    () => { var ch = ECSWorld.Instance.GetComponent<CameraComponent>(cid); ch.IsMain = newMain; ECSWorld.Instance.AddComponent(cid, ch); },
                    () => { var ch = ECSWorld.Instance.GetComponent<CameraComponent>(cid); ch.IsMain = oldMain; ECSWorld.Instance.AddComponent(cid, ch); }));
            }

            if (changed)
            {
                ECSWorld.Instance.AddComponent(id, c);
                EventBus.RaiseComponentChanged(id, nameof(CameraComponent));
            }

            if (fovDeact || nearDeact || farDeact)
            {
                var before = _cameraUndoBefore; var after = c; int cid = id;
                UndoStack.RecordAlreadyExecuted(new DelegateCommand("Camera",
                    () => { ECSWorld.Instance.AddComponent(cid, after);  EventBus.RaiseComponentChanged(cid, nameof(CameraComponent)); },
                    () => { ECSWorld.Instance.AddComponent(cid, before); EventBus.RaiseComponentChanged(cid, nameof(CameraComponent)); }));
                _cameraUndoBefore = c;
            }
        }

        // ── Light ────────────────────────────────────────────────────────────
        private static readonly string[] _projTypeNames  = { "Perspective", "Orthographic" };
        private static readonly string[] _lightTypeNames = { "Directional", "Point", "Spot" };

        public static void DrawLight(int id, ref LightComponent l)
        {
            if (!igCollapsingHeader_TreeNodeFlags("Light", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            if (_lightUndoId != id) { _lightUndoId = id; _lightUndoBefore = l; }

            // Light type combo
            int typeIdx = (int)l.Type;
            if (igCombo_Str_arr("Type", ref typeIdx, _lightTypeNames, _lightTypeNames.Length, -1))
            {
                LightType newType = (LightType)typeIdx; LightType oldType = l.Type; int cid = id;
                l.Type = newType;
                UndoStack.Record(new DelegateCommand("Light.Type",
                    () => { var ch = ECSWorld.Instance.GetComponent<LightComponent>(cid); ch.Type = newType; ECSWorld.Instance.AddComponent(cid, ch); },
                    () => { var ch = ECSWorld.Instance.GetComponent<LightComponent>(cid); ch.Type = oldType; ECSWorld.Instance.AddComponent(cid, ch); }));
            }

            bool changed = false;
            changed |= igDragFloat("Intensity", ref l.Intensity, 0.01f, 0f, 100f,   "%.2f", ImGuiSliderFlags.None);
            bool intDeact   = igIsItemDeactivatedAfterEdit();
            changed |= igDragFloat("Range",     ref l.Range,     0.1f,  0f, 10000f, "%.1f", ImGuiSliderFlags.None);
            bool rangeDeact = igIsItemDeactivatedAfterEdit();
            changed |= igColorEdit3("Color", ref l.Color, ImGuiColorEditFlags.None);
            bool colDeact   = igIsItemDeactivatedAfterEdit();

            bool innerDeact = false, outerDeact = false;
            if (l.Type == LightType.Spot)
            {
                if (l.InnerAngle <= 0f) { l.InnerAngle = 25f; changed = true; }
                if (l.OuterAngle <= l.InnerAngle) { l.OuterAngle = l.InnerAngle + 5f; changed = true; }

                changed |= igDragFloat("Inner Angle", ref l.InnerAngle, 0.5f, 1f, 89f, "%.1f", ImGuiSliderFlags.None);
                innerDeact = igIsItemDeactivatedAfterEdit();
                if (l.InnerAngle >= l.OuterAngle) { l.InnerAngle = l.OuterAngle - 0.5f; changed = true; }

                changed |= igDragFloat("Outer Angle", ref l.OuterAngle, 0.5f, 1f, 90f, "%.1f", ImGuiSliderFlags.None);
                outerDeact = igIsItemDeactivatedAfterEdit();
                if (l.OuterAngle <= l.InnerAngle) { l.OuterAngle = l.InnerAngle + 0.5f; changed = true; }
            }

            if (changed)
            {
                ECSWorld.Instance.AddComponent(id, l);
                EventBus.RaiseComponentChanged(id, nameof(LightComponent));
            }

            if (intDeact || rangeDeact || colDeact || innerDeact || outerDeact)
            {
                var before = _lightUndoBefore; var after = l; int cid = id;
                UndoStack.RecordAlreadyExecuted(new DelegateCommand("Light",
                    () => { ECSWorld.Instance.AddComponent(cid, after);  EventBus.RaiseComponentChanged(cid, nameof(LightComponent)); },
                    () => { ECSWorld.Instance.AddComponent(cid, before); EventBus.RaiseComponentChanged(cid, nameof(LightComponent)); }));
                _lightUndoBefore = l;
            }
        }

        // ── Rigidbody ────────────────────────────────────────────────────────
        public static void DrawRigidbody(int id, ref RigidbodyComponent rb)
        {
            if (!igCollapsingHeader_TreeNodeFlags("Rigidbody", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            if (_rbUndoId != id) { _rbUndoId = id; _rbUndoBefore = rb; }

            bool changed = false;
            changed |= igDragFloat("Mass", ref rb.Mass, 0.1f, 0f, float.MaxValue, "%.2f", ImGuiSliderFlags.None);
            bool massDeact = igIsItemDeactivatedAfterEdit();

            byte gravity = rb.UseGravity ? (byte)1 : (byte)0;
            if (igCheckbox("Use Gravity", ref gravity))
            {
                bool nv = gravity != 0; bool ov = rb.UseGravity; int cid = id;
                rb.UseGravity = nv; changed = true;
                UndoStack.Record(new DelegateCommand("Rigidbody.UseGravity",
                    () => { var c = ECSWorld.Instance.GetComponent<RigidbodyComponent>(cid); c.UseGravity = nv; ECSWorld.Instance.AddComponent(cid, c); },
                    () => { var c = ECSWorld.Instance.GetComponent<RigidbodyComponent>(cid); c.UseGravity = ov; ECSWorld.Instance.AddComponent(cid, c); }));
            }

            byte isStatic = rb.IsStatic ? (byte)1 : (byte)0;
            if (igCheckbox("Is Static", ref isStatic))
            {
                bool nv = isStatic != 0; bool ov = rb.IsStatic; int cid = id;
                rb.IsStatic = nv; changed = true;
                UndoStack.Record(new DelegateCommand("Rigidbody.IsStatic",
                    () => { var c = ECSWorld.Instance.GetComponent<RigidbodyComponent>(cid); c.IsStatic = nv; ECSWorld.Instance.AddComponent(cid, c); },
                    () => { var c = ECSWorld.Instance.GetComponent<RigidbodyComponent>(cid); c.IsStatic = ov; ECSWorld.Instance.AddComponent(cid, c); }));
            }

            if (changed)
            {
                ECSWorld.Instance.AddComponent(id, rb);
                EventBus.RaiseComponentChanged(id, nameof(RigidbodyComponent));
            }

            if (massDeact)
            {
                var before = _rbUndoBefore; var after = rb; int cid = id;
                UndoStack.RecordAlreadyExecuted(new DelegateCommand("Rigidbody.Mass",
                    () => { ECSWorld.Instance.AddComponent(cid, after);  EventBus.RaiseComponentChanged(cid, nameof(RigidbodyComponent)); },
                    () => { ECSWorld.Instance.AddComponent(cid, before); EventBus.RaiseComponentChanged(cid, nameof(RigidbodyComponent)); }));
                _rbUndoBefore = rb;
            }
        }

        // ── ScriptComponent ──────────────────────────────────────────────────
        public static void DrawScriptComponent(int id, ref ScriptComponent sc)
        {
            if (!igCollapsingHeader_TreeNodeFlags("Script", ImGuiTreeNodeFlags.DefaultOpen))
                return;

            igText($"Type: {(string.IsNullOrEmpty(sc.TypeName) ? "(none)" : sc.TypeName)}");
            igTextDisabled("Scripts are edited externally in your IDE.");
        }
    }
}
