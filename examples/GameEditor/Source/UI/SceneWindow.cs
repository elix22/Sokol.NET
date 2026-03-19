using System;
using System.Numerics;
using System.Collections.Generic;
using Imgui;
using static Imgui.ImguiNative;
using static Sokol.SImgui;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scene;
using static Sokol.SApp;

namespace GameEditor.UI
{
    public static unsafe class SceneWindow
    {
        public static readonly OffscreenTarget Target = new OffscreenTarget();
        public static readonly ViewportCamera Camera = new ViewportCamera();

        // ── Camera-preview overlay ──────────────────────────────────────────
        /// <summary>Offscreen target used to render the selected camera's POV preview.</summary>
        public static readonly OffscreenTarget CameraPreviewTarget = new OffscreenTarget();
        public const int PreviewW = 320;
        public const int PreviewH = 180;

        private static int _viewW = 1280;
        private static int _viewH = 720;
        private static bool _viewportHovered;

        // Gizmo undo state ────────────────────────────────────────────────────
        private static int       _gizmoUndoId     = -1;
        private static Transform _gizmoUndoBefore;
        private static bool      _gizmoWasUsing   = false;

        // Per-frame primary transform snapshot (used to compute frame delta for multi-select)
        private static Transform _gizmoPrevTr;

        // Box selection (marquee) state ───────────────────────────────────────
        private static bool    _boxSelActive       = false;
        private static bool    _lmbPressedOnViewport = false;  // LMB was pressed inside the viewport
        private static Vector2 _boxSelStart;     // screen-space anchor corner
        private static Vector2 _viewRectMin;     // cached each frame from igGetItemRectMin
        private static Vector2 _viewRectSize;    // cached each frame from igGetItemRectSize

        // Captures all selected entities' transforms at drag-start (for compound undo)
        private static readonly Dictionary<int, Transform> _gizmoUndoAllBefore = new();

        public static bool IsHovered => _viewportHovered;
        public static int ViewWidth  => _viewW;
        public static int ViewHeight => _viewH;

        public static void Init()
        {
            Camera.Init(center: Vector3.Zero, distance: 5f, yaw: 0.4f, pitch: 0.3f);
            Target.Create(_viewW, _viewH);
            CameraPreviewTarget.Create(PreviewW, PreviewH);
        }

        /// <summary>True if the Scene window had keyboard focus last frame.</summary>
        public static bool IsWindowFocused { get; private set; }
        /// <summary>True if the Scene tab is the currently visible (active) tab in its dock.</summary>
        public static bool IsVisible { get; private set; }

        public static void FocusWindow()
        {
            igSetWindowFocus_Str("Scene");
        }

        public static void Draw()
        {
            ImGuiWindowClass sceneClass = default;
            sceneClass.DockingAlwaysTabBar = 1;
            igSetNextWindowClass(&sceneClass);

            igPushStyleVar_Vec2(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            byte open = 1;
            bool visible = igBegin("Scene", ref open, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            igPopStyleVar(1);
            IsWindowFocused = visible && igIsWindowFocused(ImGuiFocusedFlags.ChildWindows);
            IsVisible = visible;

            if (!visible)
            {
                igEnd();
                return;
            }

            // Determine available size and resize offscreen target if needed
            Vector2 avail = default;
            igGetContentRegionAvail(ref avail);
            int newW = (int)MathF.Max(4, avail.X);
            int newH = (int)MathF.Max(4, avail.Y);
            Target.Resize(newW, newH);
            _viewW = newW;
            _viewH = newH;

            Camera.Update(newW, newH);

            // Render the offscreen texture into this panel
            if (Target.IsValid)
            {
                ulong texId = simgui_imtextureid(Target.TexView);
                var texRef = new ImTextureRef { _TexData = null, _TexID = texId };
                igImage(texRef, avail, Vector2.Zero, Vector2.One);

                // ── Accept scene drag-drop onto the viewport ──────────────────
                if (igBeginDragDropTarget())
                {
                    var payload = igAcceptDragDropPayload("SCENE_PATH", ImGuiDragDropFlags.None);
                    if (payload != null && payload->Delivery != 0)
                    {
                        string scenePath = System.Text.Encoding.UTF8.GetString(
                            new ReadOnlySpan<byte>((byte*)payload->Data, payload->DataSize))
                            .TrimEnd('\0');
                        SceneManager.LoadScene(scenePath);
                        EditorPersistence.SetLastScene(scenePath);
                    }
                    igEndDragDropTarget();
                }

                // ── ImGuizmo overlay (drawn on top of the viewport image) ──────
                DrawGizmo();

                // Cache viewport rect for box selection and overlays
                Vector2 _cachedRectMin  = default;
                Vector2 _cachedRectSize = default;
                igGetItemRectMin(ref _cachedRectMin);
                igGetItemRectSize(ref _cachedRectSize);
                _viewRectMin  = _cachedRectMin;
                _viewRectSize = _cachedRectSize;

                // ── Camera preview overlay (bottom-right corner) ──────────────
                DrawCameraPreviewOverlay();

                bool overGizmo = ImGuizmo.IsOver();
                var io = igGetIO_Nil();
                bool isCtrl  = io->KeyCtrl  != 0;
                bool isShift = io->KeyShift != 0;

                // ── Record that LMB was pressed inside the viewport ───────────
                // (used to fire pick/box-select only for presses that originated here)
                if (igIsItemHovered(ImGuiHoveredFlags.None)
                    && igIsMouseClicked_Bool(ImGuiMouseButton.Left, false))
                {
                    Vector2 mp = default;
                    igGetMousePos(ref mp);
                    _boxSelStart           = mp;
                    _boxSelActive          = false;
                    _lmbPressedOnViewport  = !overGizmo;
                }

                // ── While LMB is held: detect drag and draw marquee ───────────
                if (_lmbPressedOnViewport && igIsMouseDown_Nil(ImGuiMouseButton.Left)
                    && !isCtrl && !isShift)
                {
                    // Use ImGui's built-in drag threshold (6 px) so it matches gizmo
                    if (!_boxSelActive && igIsMouseDragging(ImGuiMouseButton.Left, 6f))
                        _boxSelActive = true;

                    if (_boxSelActive)
                    {
                        Vector2 mp = default;
                        igGetMousePos(ref mp);
                        DrawBoxSelectionOverlay(_boxSelStart, mp);
                    }
                }

                // ── LMB released: commit box OR fire single click pick ────────
                if (_lmbPressedOnViewport && igIsMouseReleased_Nil(ImGuiMouseButton.Left))
                {
                    _lmbPressedOnViewport = false;

                    if (_boxSelActive)
                    {
                        // Box-select path
                        Vector2 mp = default;
                        igGetMousePos(ref mp);
                        CommitBoxSelection(_boxSelStart, mp, _cachedRectMin, _cachedRectSize, isShift);
                        _boxSelActive = false;
                    }
                    else
                    {
                        // Single-click pick path — fires on release to avoid conflicts
                        Vector2 mousePos = default;
                        igGetMousePos(ref mousePos);
                        float u = (mousePos.X - _cachedRectMin.X) / _cachedRectSize.X;
                        float v = (mousePos.Y - _cachedRectMin.Y) / _cachedRectSize.Y;
                        if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                            PickEntityAtUV(u, v, isCtrl, isShift);
                    }
                }

                // Cancel box select if mouse leaves viewport mid-drag (safety)
                if (_boxSelActive && !igIsMouseDown_Nil(ImGuiMouseButton.Left))
                    _boxSelActive = false;
            }

            _viewportHovered = igIsWindowHovered(ImGuiHoveredFlags.None);

            igEnd();
        }

        /// <summary>
        /// Draws a small camera-preview overlay at the bottom-right of the scene viewport
        /// when the selected entity has a <see cref="CameraComponent"/>.
        /// </summary>
        private static void DrawCameraPreviewOverlay()
        {
            int selId = EditorState.SelectedEntity;
            if (selId < 0) return;

            var world = ECSWorld.Instance;
            if (!world.HasComponent<CameraComponent>(selId)) return;
            if (!CameraPreviewTarget.IsValid) return;

            // Grab the rect of the scene-viewport image (set by the most-recent igImage call)
            Vector2 rectMin  = default;
            Vector2 rectSize = default;
            igGetItemRectMin(ref rectMin);
            igGetItemRectSize(ref rectSize);

            const float PadX    = 10f;
            const float PadY    = 10f;
            const float HeaderH = 18f;
            const float Border  = 1.5f;
            const float PrevW   = PreviewW;
            const float PrevH   = PreviewH;

            // Bottom-right anchor
            Vector2 boxMin = new Vector2(
                rectMin.X + rectSize.X - PrevW - PadX,
                rectMin.Y + rectSize.Y - PrevH - HeaderH - PadY);
            Vector2 boxMax = boxMin + new Vector2(PrevW, PrevH + HeaderH);
            Vector2 imgMin = boxMin + new Vector2(0, HeaderH);
            Vector2 imgMax = boxMax;

            var dl = igGetWindowDrawList();

            // Outer border
            ImDrawList_AddRectFilled(dl,
                boxMin - new Vector2(Border, Border),
                boxMax + new Vector2(Border, Border),
                0xCC111111, 4f, ImDrawFlags.None);

            // Header bar
            ImDrawList_AddRectFilled(dl, boxMin, new Vector2(boxMax.X, boxMin.Y + HeaderH), 0xDD2A2A2A, 0f, ImDrawFlags.None);

            // Header label
            world.TryGetComponent<NameTag>(selId, out var nt);
            string label = $"\u25b6  {nt.Name}";   // ▶ entity name
            ImDrawList_AddText_Vec2(dl, boxMin + new Vector2(6f, 2f), 0xFFAAAAAA, label, null);

            // Preview texture
            ulong prevTexId = simgui_imtextureid(CameraPreviewTarget.TexView);
            var   texRef    = new ImTextureRef { _TexData = null, _TexID = prevTexId };
            ImDrawList_AddImage(dl, texRef, imgMin, imgMax, Vector2.Zero, Vector2.One, 0xFFFFFFFF);

            // Outline
            ImDrawList_AddRect(dl,
                boxMin - new Vector2(Border, Border),
                boxMax + new Vector2(Border, Border),
                0xFF444444, 4f, ImDrawFlags.None, Border);
        }

        /// <summary>
        /// Draws the ImGuizmo transform gizmo over the selected entity.
        /// Must be called after <c>igImage</c> so the rect min/size are available.
        /// </summary>
        private static unsafe void DrawGizmo()
        {
            int selId = EditorState.SelectedEntity;

            // Set the gizmo's screen rect to match the image widget
            Vector2 rectMin  = default;
            Vector2 rectSize = default;
            igGetItemRectMin(ref rectMin);
            igGetItemRectSize(ref rectSize);

            // Redirect gizmo draws to this window's draw list so they appear
            // on top of the igImage rather than behind it.
            ImGuizmo.SetDrawlistWindow();
            ImGuizmo.SetRect(rectMin.X, rectMin.Y, rectSize.X, rectSize.Y);

            // ── Orientation cube (upper-right corner of the viewport) ────────────────────
            if ((EditorState.Overlays & GizmoOverlayFlags.OrientationCube) != 0)
            {
                const float CubeSize = 128f;
                var viewForCube = Camera.View;
                bool cubeChanged = ImGuizmo.ViewManipulate(
                    (float*)&viewForCube,
                    Camera.Distance,
                    rectMin.X + rectSize.X - CubeSize,   // top-right X
                    rectMin.Y,                             // top-right Y
                    CubeSize);
                if (cubeChanged)
                    Camera.SetViewFromMatrix(viewForCube);
            }
            // ─────────────────────────────────────────────────────────────────────────────────────

            // ── World-axis overlay ────────────────────────────────────────────────────────────────
            if ((EditorState.Overlays & GizmoOverlayFlags.WorldAxes) != 0)
                DrawWorldAxes(igGetWindowDrawList(), rectMin, rectSize);

            // ── Camera and light entity gizmos ────────────────────────────────────────────────────
            DrawEntityIconGizmos(igGetWindowDrawList(), rectMin, rectSize);
            // ─────────────────────────────────────────────────────────────────────────────────────

            bool isUsing = ImGuizmo.IsUsing();

            if (selId < 0)
            {
                _gizmoWasUsing = isUsing;
                return;
            }

            var world = ECSWorld.Instance;
            if (!world.TryGetComponent<Transform>(selId, out var tr))
            {
                _gizmoWasUsing = isUsing;
                return;
            }

            if ((EditorState.Overlays & GizmoOverlayFlags.EntityGizmos) == 0)
            {
                _gizmoWasUsing = isUsing;
                return;
            }

            bool multiSelect = EditorState.SelectionCount > 1;
            Vector3 pivotPos = tr.Position;
            if (multiSelect && TryGetSelectionCentroid(world, out var centroid))
                pivotPos = centroid;

            // In multi-select mode, place gizmo at group centroid while preserving
            // orientation/scale behavior from the primary selection.
            var gizmoTr = tr;
            gizmoTr.Position = pivotPos;

            // Save "before" snapshot when drag starts (all selected entities)
            if (isUsing && !_gizmoWasUsing)
            {
                _gizmoUndoBefore = tr;
                _gizmoUndoId     = selId;
                _gizmoUndoAllBefore.Clear();
                foreach (int sid in EditorState.SelectedEntities)
                    if (world.TryGetComponent<Transform>(sid, out var st))
                        _gizmoUndoAllBefore[sid] = st;
            }

            // ImGuizmo uses row-major matrices (same as System.Numerics) — no transpose needed.
            var viewM = Camera.View;
            var projM = Camera.Proj;
            var matM  = gizmoTr.LocalMatrix;

            // Capture primary transform BEFORE manipulation so we can compute the per-frame delta.
            _gizmoPrevTr = gizmoTr;

            bool changed = ImGuizmo.Manipulate(
                (float*)&viewM, (float*)&projM,
                EditorState.CurrentGizmoOp,
                multiSelect ? ImGuizmo.Mode.World : EditorState.CurrentGizmoMode,
                (float*)&matM);

            if (changed)
            {
                float* newPos   = stackalloc float[3];
                float* newRot   = stackalloc float[3];
                float* newScale = stackalloc float[3];
                ImGuizmo.DecomposeMatrix((float*)&matM, newPos, newRot, newScale);

                var newPosV   = new Vector3(newPos[0],   newPos[1],   newPos[2]);
                var newRotV   = new Vector3(newRot[0],   newRot[1],   newRot[2]);
                var newScaleV = new Vector3(newScale[0], newScale[1], newScale[2]);

                if (!multiSelect)
                {
                    // Single entity: apply decomposed transform directly.
                    ref var trRef = ref world.GetComponent<Transform>(selId);
                    trRef.Position    = newPosV;
                    trRef.EulerAngles = newRotV;
                    trRef.Scale       = newScaleV;
                }
                else
                {
                    // Multi entity: apply delta relative to previous gizmo pivot.
                    var op           = EditorState.CurrentGizmoOp;
                    Vector3 prevPivot = _gizmoPrevTr.Position;
                    Vector3 nextPivot = newPosV;
                    Vector3 dPos     = newPosV   - _gizmoPrevTr.Position;
                    Vector3 dRot     = newRotV   - _gizmoPrevTr.EulerAngles;

                    const float Deg2Rad = MathF.PI / 180f;
                    var qDelta = Quaternion.CreateFromYawPitchRoll(
                        dRot.Y * Deg2Rad,
                        dRot.X * Deg2Rad,
                        dRot.Z * Deg2Rad);

                    Vector3 scaleMul = Vector3.One;
                    if (MathF.Abs(_gizmoPrevTr.Scale.X) > 1e-6f) scaleMul.X = newScaleV.X / _gizmoPrevTr.Scale.X;
                    if (MathF.Abs(_gizmoPrevTr.Scale.Y) > 1e-6f) scaleMul.Y = newScaleV.Y / _gizmoPrevTr.Scale.Y;
                    if (MathF.Abs(_gizmoPrevTr.Scale.Z) > 1e-6f) scaleMul.Z = newScaleV.Z / _gizmoPrevTr.Scale.Z;

                    foreach (int otherId in EditorState.SelectedEntities)
                    {
                        if (!world.TryGetComponent<Transform>(otherId, out var otherTr)) continue;

                        if (op == ImGuizmo.Operation.Translate)
                        {
                            otherTr.Position += dPos;
                        }
                        else if (op == ImGuizmo.Operation.Rotate)
                        {
                            Vector3 rel = otherTr.Position - prevPivot;
                            rel = Vector3.Transform(rel, qDelta);
                            otherTr.Position = nextPivot + rel;
                            otherTr.EulerAngles += dRot;
                        }
                        else if (op == ImGuizmo.Operation.Scale)
                        {
                            Vector3 rel = otherTr.Position - prevPivot;
                            rel = new Vector3(rel.X * scaleMul.X, rel.Y * scaleMul.Y, rel.Z * scaleMul.Z);
                            otherTr.Position = nextPivot + rel;
                            otherTr.Scale = new Vector3(
                                otherTr.Scale.X * scaleMul.X,
                                otherTr.Scale.Y * scaleMul.Y,
                                otherTr.Scale.Z * scaleMul.Z);
                        }

                        world.AddComponent(otherId, otherTr);
                    }
                }
            }

            // Record compound undo when drag ends (covers all selected entities)
            if (_gizmoWasUsing && !isUsing && _gizmoUndoId == selId)
            {
                var beforeAll = new Dictionary<int, Transform>(_gizmoUndoAllBefore);
                var afterAll  = new Dictionary<int, Transform>();
                foreach (int sid in beforeAll.Keys)
                    if (world.TryGetComponent<Transform>(sid, out var st))
                        afterAll[sid] = st;

                UndoStack.RecordAlreadyExecuted(new DelegateCommand(
                    "Transform Gizmo",
                    () => { foreach (var kv in afterAll)  ECSWorld.Instance.AddComponent(kv.Key, kv.Value); },
                    () => { foreach (var kv in beforeAll) ECSWorld.Instance.AddComponent(kv.Key, kv.Value); }
                ));
                _gizmoUndoId = -1;
            }

            _gizmoWasUsing = isUsing;
        }

        private static bool TryGetSelectionCentroid(ECSWorld world, out Vector3 centroid)
        {
            centroid = Vector3.Zero;
            int count = 0;

            foreach (int sid in EditorState.SelectedEntities)
            {
                if (!world.TryGetComponent<Transform>(sid, out var st))
                    continue;
                centroid += st.Position;
                count++;
            }

            if (count == 0)
                return false;

            centroid /= count;
            return true;
        }

        /// <summary>Cast a ray through (u,v) in viewport UV-space and select the nearest entity.</summary>
        private static void PickEntityAtUV(float u, float v, bool ctrl, bool shift)
        {
            // Unproject two points on the near/far planes via inverse ViewProj
            float ndcX =  u * 2f - 1f;
            float ndcY = -(v * 2f - 1f); // ImGui Y is top-down, NDC Y is bottom-up

            if (!Matrix4x4.Invert(Camera.ViewProj, out var invVP)) return;

            Vector4 nearH = Vector4.Transform(new Vector4(ndcX, ndcY, 0f, 1f), invVP);
            nearH /= nearH.W;
            Vector4 farH  = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), invVP);
            farH  /= farH.W;

            var rayOrigin = new Vector3(nearH.X, nearH.Y, nearH.Z);
            var rayDir    = Vector3.Normalize(new Vector3(farH.X - nearH.X, farH.Y - nearH.Y, farH.Z - nearH.Z));

            var world = ECSWorld.Instance;
            int bestId = -1;
            float bestT = float.MaxValue;

            foreach (int id in world.Entities)
            {
                if (!world.TryGetComponent<ActiveFlag>(id, out var af) || !af.Active) continue;
                if (!world.TryGetComponent<Transform>(id, out var tr)) continue;

                Vector3 halfExtents = tr.Scale * 0.5f;
                if (halfExtents.LengthSquared() < 1e-6f) continue;

                if (RayAABB(rayOrigin, rayDir,
                    tr.Position - halfExtents, tr.Position + halfExtents, out float t) && t < bestT)
                {
                    bestT = t;
                    bestId = id;
                }
            }

            if (bestId >= 0)
            {
                if (ctrl)
                    EditorState.ToggleEntityInSelection(bestId);
                else if (shift)
                    EditorState.AddToSelection(bestId);        // Shift: additive, no toggle
                else
                    EditorState.SelectEntity(bestId);
            }
            else if (!ctrl && !shift)
            {
                EditorState.Deselect();
            }
        }

        /// <summary>Draw the translucent marquee rectangle on the foreground draw list.</summary>
        private static unsafe void DrawBoxSelectionOverlay(Vector2 start, Vector2 end)
        {
            Vector2 bMin = new Vector2(MathF.Min(start.X, end.X), MathF.Min(start.Y, end.Y));
            Vector2 bMax = new Vector2(MathF.Max(start.X, end.X), MathF.Max(start.Y, end.Y));
            var dl = igGetForegroundDrawList_ViewportPtr(null);
            ImDrawList_AddRectFilled(dl, bMin, bMax, 0x3300AAFF, 0f, ImDrawFlags.None);
            ImDrawList_AddRect(dl, bMin, bMax, 0xFF44AAFF, 0f, ImDrawFlags.None, 1.5f);
        }

        /// <summary>
        /// Select all entities whose projected screen position falls inside the marquee rect.
        /// When <paramref name="additive"/> is true, the existing selection is kept.
        /// </summary>
        private static void CommitBoxSelection(Vector2 start, Vector2 end,
            Vector2 rectMin, Vector2 rectSize, bool additive)
        {
            float xMin = MathF.Min(start.X, end.X);
            float yMin = MathF.Min(start.Y, end.Y);
            float xMax = MathF.Max(start.X, end.X);
            float yMax = MathF.Max(start.Y, end.Y);

            if (xMax - xMin < 2f && yMax - yMin < 2f) return;   // too small — ignore

            var world = ECSWorld.Instance;
            var vp    = Camera.ViewProj;
            var hits  = new System.Collections.Generic.List<int>();

            foreach (int id in world.Entities)
            {
                if (!world.TryGetComponent<ActiveFlag>(id, out var af) || !af.Active) continue;
                if (!world.TryGetComponent<Transform>(id, out var tr)) continue;

                var sp = WorldToScreen(tr.Position, vp, rectMin, rectSize, out bool inFront);
                if (!inFront) continue;
                if (sp.X >= xMin && sp.X <= xMax && sp.Y >= yMin && sp.Y <= yMax)
                    hits.Add(id);
            }

            if (hits.Count == 0)
            {
                if (!additive) EditorState.Deselect();
                return;
            }

            if (!additive) EditorState.Deselect();
            foreach (int id in hits)
                EditorState.AddToSelection(id);
        }

        /// <summary>Slab-test ray vs axis-aligned box. Returns true and t >= 0 if hit.</summary>
        private static bool RayAABB(Vector3 o, Vector3 d, Vector3 bMin, Vector3 bMax, out float tHit)
        {
            float tMin = 0f, tMax = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float orig = i == 0 ? o.X : i == 1 ? o.Y : o.Z;
                float dir  = i == 0 ? d.X : i == 1 ? d.Y : d.Z;
                float mn   = i == 0 ? bMin.X : i == 1 ? bMin.Y : bMin.Z;
                float mx   = i == 0 ? bMax.X : i == 1 ? bMax.Y : bMax.Z;

                if (MathF.Abs(dir) < 1e-8f)
                {
                    if (orig < mn || orig > mx) { tHit = 0; return false; }
                }
                else
                {
                    float t1 = (mn - orig) / dir;
                    float t2 = (mx - orig) / dir;
                    if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                    tMin = MathF.Max(tMin, t1);
                    tMax = MathF.Min(tMax, t2);
                    if (tMin > tMax) { tHit = 0; return false; }
                }
            }
            tHit = tMin;
            return tMin >= 0f;
        }

        // ── World axis overlay ────────────────────────────────────────────────────────────────────

        /// <summary>Projects a 3-D world position into viewport screen pixels.</summary>
        /// <param name="inFront">False when the point is behind the camera (w ≤ 0).</param>
        private static Vector2 WorldToScreen(Vector3 world, Matrix4x4 vp,
            Vector2 rectMin, Vector2 rectSize, out bool inFront)
        {
            // Multiply by the combined ViewProj (row-vector convention: v * M)
            var clip = Vector4.Transform(new Vector4(world, 1f), vp);
            inFront = clip.W > 0f;
            if (!inFront)
                return Vector2.Zero;
            float invW = 1f / clip.W;
            float ndcX =  clip.X * invW;
            float ndcY =  clip.Y * invW;                        // +1 = top in NDC
            return new Vector2(
                rectMin.X + (ndcX * 0.5f + 0.5f) * rectSize.X,
                rectMin.Y + (1f - (ndcY * 0.5f + 0.5f)) * rectSize.Y  // flip Y for screen
            );
        }

        /// <summary>
        /// Draws the world X/Y/Z axis arrows onto an ImDrawList already obtained from the
        /// scene window, using projected screen-space coordinates.
        /// </summary>
        private static void DrawWorldAxes(ImDrawList* dl, Vector2 rectMin, Vector2 rectSize)
        {
            if (dl == null) return;

            var vp = Camera.ViewProj;

            // Axis endpoints in world space.  Length = 1 world-unit.
            var origin = Vector3.Zero;
            (Vector3 dir, uint col, string label)[] axes =
            {
                (Vector3.UnitX,  0xFF4444FF, "X"),   // red   (ABGR)
                (Vector3.UnitY,  0xFF44FF44, "Y"),   // green
                (Vector3.UnitZ,  0xFFFF4444, "Z"),   // blue
            };

            const float LineThick  = 2.5f;
            const float ArrowSize  = 7f;   // half-width of arrowhead base
            const float LabelOff   = 10f;  // pixels past tip for the label

            var o2d = WorldToScreen(origin, vp, rectMin, rectSize, out bool oFront);
            if (!oFront) return;   // camera is inside origin — nothing sensible to draw

            foreach (var (dir, col, label) in axes)
            {
                var tip2d = WorldToScreen(dir, vp, rectMin, rectSize, out bool tipFront);
                if (!tipFront) continue;

                // Shaft
                ImDrawList_AddLine(dl, o2d, tip2d, col, LineThick);

                // Arrowhead: filled triangle at the tip
                Vector2 shaft = Vector2.Normalize(tip2d - o2d);
                Vector2 perp  = new Vector2(-shaft.Y, shaft.X) * ArrowSize;
                ImDrawList_AddTriangleFilled(dl,
                    tip2d + shaft * ArrowSize * 1.5f,     // apex
                    tip2d - perp,                          // base-left
                    tip2d + perp,                          // base-right
                    col);

                // Label
                var labelPos = tip2d + shaft * (ArrowSize * 1.5f + LabelOff) - new Vector2(4f, 7f);
                ImDrawList_AddText_Vec2(dl, labelPos + Vector2.One, 0xFF000000, label, null); // shadow
                ImDrawList_AddText_Vec2(dl, labelPos,               col,         label, null);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────

        // ── Camera and light entity icon gizmos ──────────────────────────────────────────────────

        /// <summary>
        /// Iterates all active entities and draws camera frustums + icons and light icons onto dl.
        /// </summary>
        private static void DrawEntityIconGizmos(ImDrawList* dl, Vector2 rectMin, Vector2 rectSize)
        {
            if (dl == null) return;
            var vp    = Camera.ViewProj;
            var world = ECSWorld.Instance;

            foreach (int id in world.Entities)
            {
                if (!world.TryGetComponent<ActiveFlag>(id, out var af) || !af.Active) continue;
                if (!world.TryGetComponent<Transform>(id, out var tr)) continue;

                bool isSelected = EditorState.SelectedEntity == id;

                if ((EditorState.Overlays & GizmoOverlayFlags.CameraGizmos) != 0 &&
                    world.TryGetComponent<CameraComponent>(id, out var cam))
                {
                    DrawCameraEntityGizmo(dl, tr, cam, isSelected, vp, rectMin, rectSize);
                }

                if ((EditorState.Overlays & GizmoOverlayFlags.LightGizmos) != 0 &&
                    world.TryGetComponent<LightComponent>(id, out var light))
                {
                    DrawLightEntityGizmo(dl, tr, light, isSelected, vp, rectMin, rectSize);
                }
            }
        }

        private static void DrawCameraEntityGizmo(ImDrawList* dl, Transform tr, CameraComponent cam,
            bool selected, Matrix4x4 vp, Vector2 rectMin, Vector2 rectSize)
        {
            var rot   = Matrix4x4.CreateFromYawPitchRoll(
                tr.EulerAngles.Y * MathF.PI / 180f,
                tr.EulerAngles.X * MathF.PI / 180f,
                tr.EulerAngles.Z * MathF.PI / 180f);
            var fwd   = new Vector3(rot.M31, rot.M32, rot.M33);  // local +Z in world
            var right = new Vector3(rot.M11, rot.M12, rot.M13);
            var up    = new Vector3(rot.M21, rot.M22, rot.M23);

            // Yellow for cameras: ABGR 0xAABBGGRR
            uint col    = selected ? 0xFF00FFFF : 0xAA00CCDD;
            uint colDim = selected ? 0xFF0088BB : 0x5500778A;

            // ── 2D icon at the camera's world position ────────────────────────────────
            var pos2d = WorldToScreen(tr.Position, vp, rectMin, rectSize, out bool posInFront);
            if (posInFront)
            {
                DrawCameraIcon2D(dl, pos2d, col);
                string label = cam.IsOrthographic ? "Cam [Ortho]" : "Cam";
                ImDrawList_AddText_Vec2(dl, pos2d + new Vector2(13, -6), 0xFF000000, label, null);
                ImDrawList_AddText_Vec2(dl, pos2d + new Vector2(12, -7), col, label, null);
            }

            // ── Direction arrow (1.5 units forward from camera) ───────────────────────
            var start2d = WorldToScreen(tr.Position,             vp, rectMin, rectSize, out bool s1);
            var end2d   = WorldToScreen(tr.Position + fwd * 1.5f, vp, rectMin, rectSize, out bool s2);
            if (s1 && s2)
                ImDrawList_AddLine(dl, start2d, end2d, col, 1.5f);

            // ── Frustum wireframe ─────────────────────────────────────────────────────
            float displayFar = MathF.Min(cam.FarZ, MathF.Max(cam.NearZ * 40f, 3f));
            if (cam.IsOrthographic)
            {
                float orthoH = cam.OrthoSize > 0f ? cam.OrthoSize : 5f;
                float aspect = _viewW > 0 && _viewH > 0 ? (float)_viewW / _viewH : 16f / 9f;
                DrawOrthoFrustum(dl, tr.Position, fwd, right, up,
                    orthoH, aspect, cam.NearZ, displayFar, colDim, vp, rectMin, rectSize);
            }
            else
            {
                DrawCameraFrustum(dl, tr.Position, fwd, right, up,
                    cam.Fov, cam.NearZ, displayFar, colDim, vp, rectMin, rectSize);
            }
        }

        private static void DrawCameraIcon2D(ImDrawList* dl, Vector2 c, uint col)
        {
            const float W = 10f, H = 7f;
            // Camera body rectangle
            ImDrawList_AddRect(dl,
                c + new Vector2(-W, -H * 0.5f),
                c + new Vector2( W,  H * 0.5f),
                col, 2f, ImDrawFlags.None, 1.5f);
            // Lens circle
            ImDrawList_AddCircle(dl, c, 3f, col, 8, 1.5f);
            // Viewfinder bump on top of body
            ImDrawList_AddLine(dl, c + new Vector2(-4f, -H * 0.5f),       c + new Vector2(-4f, -H * 0.5f - 3.5f), col, 1.5f);
            ImDrawList_AddLine(dl, c + new Vector2(-4f, -H * 0.5f - 3.5f), c + new Vector2( 4f, -H * 0.5f - 3.5f), col, 1.5f);
            ImDrawList_AddLine(dl, c + new Vector2( 4f, -H * 0.5f - 3.5f), c + new Vector2( 4f, -H * 0.5f),        col, 1.5f);
        }

        private static void DrawCameraFrustum(ImDrawList* dl,
            Vector3 pos, Vector3 fwd, Vector3 right, Vector3 up,
            float fovDeg, float nearZ, float farZ, uint col,
            Matrix4x4 vp, Vector2 rectMin, Vector2 rectSize)
        {
            float tanHalf = MathF.Tan(fovDeg * 0.5f * MathF.PI / 180f);
            float aspect  = _viewW > 0 && _viewH > 0 ? (float)_viewW / _viewH : 16f / 9f;

            float nh = nearZ * tanHalf, nw = nh * aspect;
            float fh = farZ  * tanHalf, fw = fh * aspect;

            var nc = pos + fwd * nearZ;
            var fc = pos + fwd * farZ;

            // 8 frustum corners (0-3 = near, 4-7 = far; winding: TR, TL, BL, BR)
            Span<Vector3> w3 = stackalloc Vector3[8]
            {
                nc + right * nw + up * nh,  nc - right * nw + up * nh,
                nc - right * nw - up * nh,  nc + right * nw - up * nh,
                fc + right * fw + up * fh,  fc - right * fw + up * fh,
                fc - right * fw - up * fh,  fc + right * fw - up * fh,
            };

            Span<Vector2> s2  = stackalloc Vector2[8];
            Span<bool>    vis = stackalloc bool[8];
            for (int i = 0; i < 8; i++)
                s2[i] = WorldToScreen(w3[i], vp, rectMin, rectSize, out vis[i]);

            // Near quad
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4;     if (vis[i]   && vis[j])   ImDrawList_AddLine(dl, s2[i],   s2[j],   col, 1f); }
            // Far quad
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4 + 4; if (vis[i+4] && vis[j])   ImDrawList_AddLine(dl, s2[i+4], s2[j],   col, 1f); }
            // 4 edge lines
            for (int i = 0; i < 4; i++) {                            if (vis[i]   && vis[i+4]) ImDrawList_AddLine(dl, s2[i],   s2[i+4], col, 1f); }
        }

        private static void DrawOrthoFrustum(ImDrawList* dl,
            Vector3 pos, Vector3 fwd, Vector3 right, Vector3 up,
            float orthoH, float aspect, float nearZ, float farZ, uint col,
            Matrix4x4 vp, Vector2 rectMin, Vector2 rectSize)
        {
            float w = orthoH * aspect;
            var nc = pos + fwd * nearZ;
            var fc = pos + fwd * farZ;

            // 8 ortho box corners (same winding as perspective)
            Span<Vector3> w3 = stackalloc Vector3[8]
            {
                nc + right * w + up * orthoH,  nc - right * w + up * orthoH,
                nc - right * w - up * orthoH,  nc + right * w - up * orthoH,
                fc + right * w + up * orthoH,  fc - right * w + up * orthoH,
                fc - right * w - up * orthoH,  fc + right * w - up * orthoH,
            };

            Span<Vector2> s2  = stackalloc Vector2[8];
            Span<bool>    vis = stackalloc bool[8];
            for (int i = 0; i < 8; i++)
                s2[i] = WorldToScreen(w3[i], vp, rectMin, rectSize, out vis[i]);

            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4;     if (vis[i]   && vis[j])   ImDrawList_AddLine(dl, s2[i],   s2[j],   col, 1f); }
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4 + 4; if (vis[i+4] && vis[j])   ImDrawList_AddLine(dl, s2[i+4], s2[j],   col, 1f); }
            for (int i = 0; i < 4; i++) {                            if (vis[i]   && vis[i+4]) ImDrawList_AddLine(dl, s2[i],   s2[i+4], col, 1f); }
        }

        private static void DrawLightEntityGizmo(ImDrawList* dl, Transform tr, LightComponent light,
            bool selected, Matrix4x4 vp, Vector2 rectMin, Vector2 rectSize)
        {
            var rot = Matrix4x4.CreateFromYawPitchRoll(
                tr.EulerAngles.Y * MathF.PI / 180f,
                tr.EulerAngles.X * MathF.PI / 180f,
                tr.EulerAngles.Z * MathF.PI / 180f);
            var fwd = new Vector3(rot.M31, rot.M32, rot.M33);

            // Orange/warm for lights: ABGR
            uint col    = selected ? 0xFF0096FF : 0xAA0078DD;
            uint colDim = selected ? 0xFF0060CC : 0x550050AA;

            var pos2d = WorldToScreen(tr.Position, vp, rectMin, rectSize, out bool inFront);
            if (!inFront) return;

            switch (light.Type)
            {
                case LightType.Point:
                    DrawPointLightIcon(dl, pos2d, col, colDim, tr.Position, light.Range, vp, rectMin, rectSize);
                    break;
                case LightType.Directional:
                    DrawDirectionalLightIcon(dl, pos2d, col, fwd, tr.Position, vp, rectMin, rectSize);
                    break;
                case LightType.Spot:
                    DrawSpotLightIcon(dl, pos2d, col, colDim, fwd, tr.Position, light, vp, rectMin, rectSize);
                    break;
            }
        }

        private static void DrawPointLightIcon(ImDrawList* dl, Vector2 pos2d,
            uint col, uint colDim, Vector3 worldPos, float range,
            Matrix4x4 vp, Vector2 rectMin, Vector2 rectSize)
        {
            // Bulb: filled dot + outer circle
            ImDrawList_AddCircleFilled(dl, pos2d, 3f, col, 8);
            ImDrawList_AddCircle(dl, pos2d, 6f, col, 12, 1.5f);
            // 8 small rays
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI / 4f;
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                ImDrawList_AddLine(dl, pos2d + dir * 8f, pos2d + dir * 13f, col, 1.5f);
            }
            // Range circle in the XZ plane
            if (range > 0f)
            {
                const int Segs = 32;
                Vector2? prev = null;
                for (int i = 0; i <= Segs; i++)
                {
                    float a = i * MathF.Tau / Segs;
                    var wp = worldPos + new Vector3(MathF.Cos(a) * range, 0f, MathF.Sin(a) * range);
                    var sp = WorldToScreen(wp, vp, rectMin, rectSize, out bool vis);
                    if (vis) { if (prev.HasValue) ImDrawList_AddLine(dl, prev.Value, sp, colDim, 1f); prev = sp; }
                    else prev = null;
                }
            }
        }

        private static void DrawDirectionalLightIcon(ImDrawList* dl, Vector2 pos2d,
            uint col, Vector3 fwd, Vector3 worldPos,
            Matrix4x4 vp, Vector2 rectMin, Vector2 rectSize)
        {
            // Sun circle
            ImDrawList_AddCircle(dl, pos2d, 8f, col, 12, 1.5f);
            // 3 parallel direction arrows starting just past the icon
            var tip1 = WorldToScreen(worldPos + fwd * 1.5f, vp, rectMin, rectSize, out bool t1);
            if (!t1) return;
            Vector2 arrowDir = Vector2.Normalize(tip1 - pos2d);
            Vector2 perp     = new Vector2(-arrowDir.Y, arrowDir.X);
            for (int i = -1; i <= 1; i++)
            {
                var off   = perp * (i * 8f);
                var start = pos2d + off + arrowDir * 12f;
                var end   = pos2d + off + arrowDir * 24f;
                ImDrawList_AddLine(dl, start, end, col, 1.5f);
                ImDrawList_AddTriangleFilled(dl,
                    end + arrowDir * 5f,
                    end - perp * 3f,
                    end + perp * 3f,
                    col);
            }
        }

        private static void DrawSpotLightIcon(ImDrawList* dl, Vector2 pos2d,
            uint col, uint colDim, Vector3 fwd, Vector3 worldPos, LightComponent light,
            Matrix4x4 vp, Vector2 rectMin, Vector2 rectSize)
        {
            // Small apex circle
            ImDrawList_AddCircle(dl, pos2d, 4f, col, 8, 1.5f);

            // Construct a local orthonormal frame for the cone circle
            var up    = MathF.Abs(Vector3.Dot(fwd, Vector3.UnitY)) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
            var right = Vector3.Normalize(Vector3.Cross(fwd, up));
            up        = Vector3.Normalize(Vector3.Cross(right, fwd));

            float coneDist   = light.Range > 0f ? MathF.Min(light.Range, 3f) : 2f;
            float halfAngle  = MathF.PI / 6f;  // 30°
            float coneRadius = coneDist * MathF.Tan(halfAngle);
            var   coneCenter = worldPos + fwd * coneDist;

            // Cone base ring
            const int ConeSegs = 16;
            Vector2? prev = null;
            for (int i = 0; i <= ConeSegs; i++)
            {
                float a  = i * MathF.Tau / ConeSegs;
                var wp   = coneCenter + (right * MathF.Cos(a) + up * MathF.Sin(a)) * coneRadius;
                var sp   = WorldToScreen(wp, vp, rectMin, rectSize, out bool vis);
                if (vis) { if (prev.HasValue) ImDrawList_AddLine(dl, prev.Value, sp, colDim, 1f); prev = sp; }
                else prev = null;
            }
            // 4 edge lines from apex to ring
            for (int i = 0; i < 4; i++)
            {
                float a  = i * MathF.Tau / 4f;
                var wp   = coneCenter + (right * MathF.Cos(a) + up * MathF.Sin(a)) * coneRadius;
                var sp   = WorldToScreen(wp, vp, rectMin, rectSize, out bool vis);
                if (vis) ImDrawList_AddLine(dl, pos2d, sp, col, 1f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Handle raw sokol events for viewport camera, returns true if consumed.</summary>
        public static bool HandleEvent(sapp_event* e)
        {
            if (!_viewportHovered && !Camera.IsCapturing)
                return false;

            switch (e->type)
            {
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
                    // Don't start orbit/pan when clicking a gizmo handle
                    if (!ImGuizmo.IsOver())
                        Camera.HandleMouseDown(e->mouse_button, e->mouse_x, e->mouse_y);
                    break;
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
                    Camera.HandleMouseUp(e->mouse_button);
                    break;
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
                    Camera.HandleMouseMove(e->mouse_x, e->mouse_y);
                    break;
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
                    if (_viewportHovered)
                        Camera.HandleScroll(e->scroll_y);
                    break;
            }
            return Camera.IsCapturing;
        }

        public static void Cleanup()
        {
            Target.Destroy();
        }
    }
}
