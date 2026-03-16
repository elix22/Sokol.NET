using System.Runtime.InteropServices;
using System.Numerics;

namespace Imgui
{
    /// <summary>
    /// C# P/Invoke bindings for ImGuizmo — render 3D transform gizmos inside a
    /// Dear ImGui viewport window.
    ///
    /// Matrix convention note:
    ///   ImGuizmo internally uses row-major matrices (same as System.Numerics.Matrix4x4).
    ///   Pass view/proj/object matrices directly WITHOUT transposing.
    ///   The output matrix from <see cref="Manipulate"/> is also row-major
    ///   — pass it directly to <see cref="DecomposeMatrix"/>.
    ///   The "column-major" claim in older comments was incorrect.
    /// </summary>
    public static unsafe class ImGuizmo
    {
        private const string Lib = ImguiNative.NativeLibraryName;

        /// <summary>
        /// Call once per frame immediately after <c>simgui_new_frame()</c>.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_begin_frame")]
        public static extern void BeginFrame();

        /// <summary>
        /// Set the viewport rect (screen-pixel coords) where gizmos will render.
        /// Call right before <see cref="Manipulate"/> each frame.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_set_rect")]
        public static extern void SetRect(float x, float y, float w, float h);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_set_orthographic")]
        private static extern void SetOrthographic(int isOrtho);
        public static void SetOrthographic(bool isOrtho) => SetOrthographic(isOrtho ? 1 : 0);

        /// <summary>
        /// Redirect gizmo rendering to the current ImGui window's draw list.
        /// Call this inside the Scene window BEFORE <see cref="SetRect"/>/<see cref="Manipulate"/>.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_set_drawlist_window")]
        public static extern void SetDrawlistWindow();

        /// <summary>
        /// Draw and interact with the transform gizmo for one entity.
        /// All matrix pointers are float[16] column-major.
        /// Returns true if the <paramref name="matrix"/> was modified this frame.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_manipulate")]
        private static extern int ManipulateNative(
            float* view, float* projection,
            int operation, int mode,
            float* matrix, float* deltaMatrix, float* snap);

        public static bool Manipulate(
            float* view, float* projection,
            Operation operation, Mode mode,
            float* matrix, float* deltaMatrix = null, float* snap = null)
            => ManipulateNative(view, projection,
                   (int)operation, (int)mode,
                   matrix, deltaMatrix, snap) != 0;

        /// <summary>True if the mouse is over any gizmo handle this frame.</summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_is_over")]
        private static extern int IsOverNative();
        public static bool IsOver() => IsOverNative() != 0;

        /// <summary>True while a gizmo is actively being dragged.</summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_is_using")]
        private static extern int IsUsingNative();
        public static bool IsUsing() => IsUsingNative() != 0;

        /// <summary>
        /// Draws the orientation cube in the corner of the viewport and allows
        /// the user to click it to snap the camera to a direction.
        /// Call after <see cref="Manipulate"/> each frame.
        /// </summary>
        /// <param name="view">Row-major view matrix (read/write — updated when user clicks).</param>
        /// <param name="length">Scene size hint (use camera distance).</param>
        /// <param name="posX">Screen X of the cube's top-left corner.</param>
        /// <param name="posY">Screen Y of the cube's top-left corner.</param>
        /// <param name="size">Cube widget size in pixels.</param>
        /// <param name="bgColor">ImU32 background color (0 = transparent).</param>
        /// <returns>True if the view matrix was modified this frame.</returns>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_view_manipulate")]
        private static extern int ViewManipulateNative(
            float* view, float length,
            float posX, float posY, float size,
            uint bgColor);
        public static bool ViewManipulate(float* view, float length,
            float posX, float posY, float size, uint bgColor = 0x10101080)
            => ViewManipulateNative(view, length, posX, posY, size, bgColor) != 0;

        /// <summary>
        /// Decompose a column-major float[16] matrix into translation, rotation
        /// (Euler angles in degrees), and scale float[3] arrays.
        /// </summary>
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_decompose_matrix")]
        public static extern void DecomposeMatrix(
            float* matrix, float* translation, float* rotation, float* scale);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "imguizmo_recompose_matrix")]
        public static extern void RecomposeMatrix(
            float* translation, float* rotation, float* scale, float* matrix);

        // ── Enums ─────────────────────────────────────────────────────────────

        public enum Operation : int
        {
            TranslateX    = 1 << 0,
            TranslateY    = 1 << 1,
            TranslateZ    = 1 << 2,
            RotateX       = 1 << 3,
            RotateY       = 1 << 4,
            RotateZ       = 1 << 5,
            RotateScreen  = 1 << 6,
            ScaleX        = 1 << 7,
            ScaleY        = 1 << 8,
            ScaleZ        = 1 << 9,

            Translate     = TranslateX | TranslateY | TranslateZ,
            Rotate        = RotateX | RotateY | RotateZ | RotateScreen,
            Scale         = ScaleX | ScaleY | ScaleZ,
        }

        public enum Mode : int
        {
            Local = 0,
            World = 1,
        }
    }
}
