using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;

namespace GameEditor.UI
{
    /// <summary>
    /// Full-window dockspace with a Unity-style panel layout built via DockBuilder.
    ///
    ///  ┌──────────┬──────────────────┬───────────┐
    ///  │ Hierarchy│     Scene        │ Inspector │
    ///  │  ~200 px │   (remainder)    │  ~260 px  │
    ///  ├──────────┴──────────────────┴───────────┤
    ///  │                Console                  │
    ///  │               ~180 px tall              │
    ///  └─────────────────────────────────────────┘
    /// </summary>
    public static unsafe class EditorLayout
    {
        private static bool _layoutBuilt = false;
        private static uint _dockspaceId;

        public static uint DockspaceId => _dockspaceId;

        public static void BeginDockspace()
        {
            var viewport = igGetMainViewport();

            igSetNextWindowPos(viewport->Pos, ImGuiCond.Always, Vector2.Zero);
            igSetNextWindowSize(viewport->Size, ImGuiCond.Always);

            var windowFlags =
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoBackground;

            igPushStyleVar_Float(ImGuiStyleVar.WindowRounding, 0f);
            igPushStyleVar_Float(ImGuiStyleVar.WindowBorderSize, 0f);
            igPushStyleVar_Vec2(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            byte open = 1;
            igBegin("##DockspaceRoot", ref open, windowFlags);
            igPopStyleVar(3);

            _dockspaceId = igGetID_Str("MainDockspace");
            igDockSpace(_dockspaceId, Vector2.Zero, ImGuiDockNodeFlags.PassthruCentralNode, null);

            // Build default layout once per app launch.
            // DockBuilder always resets the node (even if igDockSpace just created it),
            // so windows get their Unity-style positions on the first frame.
            if (!_layoutBuilt)
            {
                _layoutBuilt = true;
                BuildLayout(viewport->Size);
            }

            igEnd();
        }

        private static void BuildLayout(Vector2 size)
        {
            // ImGuiDockNodeFlags_DockSpace = 1 << 10 = 1024 (internal flag)
            const ImGuiDockNodeFlags dockSpaceFlag = (ImGuiDockNodeFlags)1024;

            DockBuilder.igDockBuilderRemoveNode(_dockspaceId);
            DockBuilder.igDockBuilderAddNode(_dockspaceId, dockSpaceFlag);
            DockBuilder.igDockBuilderSetNodeSize(_dockspaceId, size);

            // ── Step 1: split off Console at the bottom (~20 % of height) ──────────
            uint dockConsole, dockUpper;
            DockBuilder.igDockBuilderSplitNode(
                _dockspaceId, ImGuiDir.Down, 0.22f,
                &dockConsole, &dockUpper);

            // ── Step 2: split off Hierarchy on the left (~17 % of width) ───────────
            uint dockHierarchy, dockCenter;
            DockBuilder.igDockBuilderSplitNode(
                dockUpper, ImGuiDir.Left, 0.17f,
                &dockHierarchy, &dockCenter);

            // ── Step 3: split off Inspector on the right (~22 % of remaining) ──────
            uint dockInspector, dockScene;
            DockBuilder.igDockBuilderSplitNode(
                dockCenter, ImGuiDir.Right, 0.22f,
                &dockInspector, &dockScene);

            // ── Assign windows to nodes ──────────────────────────────────────────
            DockBuilder.igDockBuilderDockWindow("Hierarchy", dockHierarchy);
            DockBuilder.igDockBuilderDockWindow("Scene",     dockScene);
            DockBuilder.igDockBuilderDockWindow("Game",      dockScene);  // tab alongside Scene
            DockBuilder.igDockBuilderDockWindow("Inspector", dockInspector);
            DockBuilder.igDockBuilderDockWindow("Console",      dockConsole);
            DockBuilder.igDockBuilderDockWindow("Build & Deploy", dockConsole);  // tab alongside Console
            DockBuilder.igDockBuilderDockWindow("Assets",        dockConsole);  // tab alongside Console

            DockBuilder.igDockBuilderFinish(_dockspaceId);
        }
    }
}
