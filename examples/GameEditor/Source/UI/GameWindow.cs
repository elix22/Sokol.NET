using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using static Sokol.SImgui;
using GameEditor.Framework.Renderer;
using GameEditor.Framework.Core;
using GameEditor.Framework.Scene;

namespace GameEditor.UI
{
    public static unsafe class GameWindow
    {
        public static readonly OffscreenTarget Target = new OffscreenTarget();

        private static int          _viewW = 1280;
        private static int          _viewH = 720;
        // Pending size is written by Draw() and applied by PrepareRenderTarget() next frame
        private static int          _pendingW = 1280;
        private static int          _pendingH = 720;
        private static bool         _hasCamera;   // set by PrepareRenderTarget, read by Draw
        private static bool         _requestUndock;

        public static int ViewWidth  => _viewW;
        public static int ViewHeight => _viewH;

        public static void Init()
        {
            Target.Create(_viewW, _viewH);
        }

        public static void RequestUndock()
        {
            _requestUndock = true;
        }

        public static void FocusWindow()
        {
            igSetWindowFocus_Str("Game");
        }

        /// <summary>
        /// Must be called at the very start of Frame(), BEFORE any sokol render passes.
        /// Resizes the offscreen target to the size requested by the previous Draw() call.
        /// </summary>
        public static void PrepareRenderTarget(bool hasCamera)
        {
            _hasCamera = hasCamera;
            if (_pendingW > 0 && _pendingH > 0)
            {
                _viewW = _pendingW;
                _viewH = _pendingH;
                Target.Resize(_viewW, _viewH);
            }
        }

        public static void Draw()
        {
            var currentMode = SceneManager.PlayMode;

            if (_requestUndock)
            {
                igSetNextWindowDockID(0, ImGuiCond.Always);
                _requestUndock = false;
            }

            ImGuiWindowClass gameClass = default;
            gameClass.DockingAlwaysTabBar = 1;
            igSetNextWindowClass(&gameClass);

            igPushStyleVar_Vec2(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            byte open = 1;
            bool visible = igBegin("Game", ref open, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            igPopStyleVar(1);

            if (!visible)
            {
                igEnd();
                return;
            }

            Vector2 avail = default;
            igGetContentRegionAvail(ref avail);
            int newW = (int)MathF.Max(4, avail.X);
            int newH = (int)MathF.Max(4, avail.Y);
            // Store requested size — target will be resized at start of NEXT frame (before render)
            _pendingW = newW;
            _pendingH = newH;

            if (currentMode == PlayModeState.Playing || currentMode == PlayModeState.Paused)
            {
                if (Target.IsValid)
                {
                    ulong texId = simgui_imtextureid(Target.TexView);
                    var texRef = new ImTextureRef { _TexData = null, _TexID = texId };
                    igImage(texRef, avail, Vector2.Zero, Vector2.One);

                    // Overlay: "No Main Camera" when nothing is rendering
                    if (!_hasCamera)
                    {
                        var dl   = igGetWindowDrawList();
                        Vector2 wMin = default; igGetItemRectMin(ref wMin);
                        Vector2 wMax = default; igGetItemRectMax(ref wMax);
                        var center   = (wMin + wMax) * 0.5f;
                        const string msg = "No Main Camera";
                        Vector2 ts = default; igCalcTextSize(ref ts, msg, null, false, -1f);
                        ImguiNative.ImDrawList_AddText_Vec2(dl, center - ts * 0.5f + Vector2.One, 0xFF000000, msg, null);
                        ImguiNative.ImDrawList_AddText_Vec2(dl, center - ts * 0.5f,               0xFF4488FF, msg, null);
                    }
                }
            }
            else
            {
                // Edit mode: show instruction text
                string msg = "  Press Play to preview";
                Vector2 textSize = default;
                igCalcTextSize(ref textSize, msg, null, false, -1f);
                var cursor = new Vector2(
                    (avail.X - textSize.X) * 0.5f,
                    (avail.Y - textSize.Y) * 0.5f);
                igSetCursorPos(cursor);
                igTextDisabled(msg);
            }

            igEnd();
        }

        public static void Cleanup()
        {
            Target.Destroy();
        }
    }
}
