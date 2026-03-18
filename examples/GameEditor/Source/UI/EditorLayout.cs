using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;

namespace GameEditor.UI
{
    /// <summary>
    /// Full-window dockspace with a Unity-style panel layout built via DockBuilder.
    ///
    ///  ┌──────────┬──────────────────┬───────────┐
    ///  │ Hierarchy│      Scene       │ Inspector │
    ///  │  ~200 px │   (top center)   │  ~260 px  │
    ///  ├──────────┼──────────────────┼───────────┤
    ///  │          │       Game       │           │
    ///  │          │  (bottom center) │           │
    ///  ├──────────┴──────────────────┴───────────┤
    ///  │                Console                  │
    ///  │               ~180 px tall              │
    ///  └─────────────────────────────────────────┘
    /// </summary>
    public static unsafe class EditorLayout
    {
        public enum LayoutPreset
        {
            SplitSceneGame,
            TabbedSceneGame
        }

        private static bool _layoutBuilt = false;
        private static bool _layoutResetRequested = false;
        private static LayoutPreset _layoutPreset = LayoutPreset.TabbedSceneGame;
        private static uint _sceneGameDockNodeId;
        private static uint _dockspaceId;

        public static uint DockspaceId => _dockspaceId;
        public static LayoutPreset CurrentPreset => _layoutPreset;
        public static uint SceneGameDockNodeId => _sceneGameDockNodeId;

        public static void RequestResetLayout()
        {
            _layoutResetRequested = true;
        }

        public static void RequestLayoutPreset(LayoutPreset preset)
        {
            _layoutPreset = preset;
            _layoutResetRequested = true;
        }

        public static void BeginDockspace()
        {
            var viewport = igGetMainViewport();

            // Reserve vertical space for the main menu bar so top dock tabs remain visible.
            float menuBarH = igGetFrameHeight();
            Vector2 dockPos = new Vector2(viewport->Pos.X, viewport->Pos.Y + menuBarH);
            Vector2 dockSize = new Vector2(viewport->Size.X, MathF.Max(0f, viewport->Size.Y - menuBarH));

            igSetNextWindowPos(dockPos, ImGuiCond.Always, Vector2.Zero);
            igSetNextWindowSize(dockSize, ImGuiCond.Always);

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
            ImGuiWindowClass dockClass = default;
            dockClass.DockingAlwaysTabBar = 1;
            igDockSpace(_dockspaceId, Vector2.Zero, ImGuiDockNodeFlags.None, &dockClass);

            // Build default layout once per app launch.
            // DockBuilder always resets the node (even if igDockSpace just created it),
            // so windows get their Unity-style positions on the first frame.
            if (!_layoutBuilt || _layoutResetRequested)
            {
                _layoutResetRequested = false;
                _layoutBuilt = true;
                BuildLayout(dockSize);
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
            uint dockInspector, dockMain;
            DockBuilder.igDockBuilderSplitNode(
                dockCenter, ImGuiDir.Right, 0.22f,
                &dockInspector, &dockMain);

            uint dockGame = 0;
            uint dockScene = dockMain;
            if (_layoutPreset == LayoutPreset.SplitSceneGame)
            {
                // ── Step 4: split center into Scene (top) and Game (bottom) ────────────
                DockBuilder.igDockBuilderSplitNode(
                    dockMain, ImGuiDir.Down, 0.35f,
                    &dockGame, &dockScene);
            }

                    _sceneGameDockNodeId = dockScene;

            // ── Assign windows to nodes ──────────────────────────────────────────
            DockBuilder.igDockBuilderDockWindow("Hierarchy", dockHierarchy);
            DockBuilder.igDockBuilderDockWindow("Scene",     dockScene);
            DockBuilder.igDockBuilderDockWindow("Game",      _layoutPreset == LayoutPreset.SplitSceneGame ? dockGame : dockScene);
            DockBuilder.igDockBuilderDockWindow("Inspector", dockInspector);
            DockBuilder.igDockBuilderDockWindow("Console",      dockConsole);
            DockBuilder.igDockBuilderDockWindow("Build & Deploy", dockConsole);  // tab alongside Console
            DockBuilder.igDockBuilderDockWindow("Assets",        dockConsole);  // tab alongside Console

            DockBuilder.igDockBuilderFinish(_dockspaceId);
        }
    }
}
