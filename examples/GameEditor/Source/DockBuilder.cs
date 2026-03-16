using System.Runtime.InteropServices;
using System.Numerics;
using Imgui;

namespace GameEditor.UI
{
    /// <summary>
    /// P/Invoke declarations for cimgui's DockBuilder internal API
    /// (imgui_internal.h — exported by cimgui).
    /// </summary>
    internal static unsafe class DockBuilder
    {
        private const string Lib = ImguiNative.NativeLibraryName;

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderRemoveNode(uint node_id);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint igDockBuilderAddNode(uint node_id, ImGuiDockNodeFlags flags);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderSetNodeSize(uint node_id, Vector2 size);

        // Returns the node ID at split direction; outputs via pointer params.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint igDockBuilderSplitNode(
            uint node_id,
            ImGuiDir split_dir,
            float size_ratio_for_node_at_dir,
            uint* out_id_at_dir,
            uint* out_id_at_opposite_dir);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderDockWindow(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string window_name,
            uint node_id);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderFinish(uint node_id);

        // Returns null if the node doesn't exist yet (layout not initialised).
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern nint igDockBuilderGetNode(uint node_id);
    }
}
