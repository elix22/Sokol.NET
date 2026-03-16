// Thin C wrapper over ImGuizmo for P/Invoke from C#.
// imgui.h must be included before ImGuizmo.h so all imgui types are available.
#include "cimgui/imgui/imgui.h"
#include "ImGuizmo/ImGuizmo.h"

extern "C" {

/// Must be called once per frame, immediately after simgui_new_frame().
void imguizmo_begin_frame(void)
{
    ImGuizmo::BeginFrame();
}

/// Set the viewport rect (screen-pixel coords) where gizmos are drawn.
void imguizmo_set_rect(float x, float y, float w, float h)
{
    ImGuizmo::SetRect(x, y, w, h);
}

void imguizmo_set_orthographic(int isOrtho)
{
    ImGuizmo::SetOrthographic(isOrtho != 0);
}

/// Redirect gizmo rendering to the current ImGui window's draw list.
/// Call this inside the window where you want the gizmo to appear,
/// BEFORE calling imguizmo_set_rect / imguizmo_manipulate.
/// Without this the gizmo draws into the early-created "gizmo" window
/// which is behind all docked panels.
void imguizmo_set_drawlist_window(void)
{
    ImGuizmo::SetDrawlist(ImGui::GetWindowDrawList());
}

/// Draw and interact with the transform gizmo.
/// All matrix pointers are float[16] in column-major (OpenGL) order.
/// Returns 1 if the matrix was modified this frame, 0 otherwise.
int imguizmo_manipulate(
    const float* view, const float* projection,
    int operation, int mode,
    float* matrix, float* deltaMatrix, const float* snap)
{
    return ImGuizmo::Manipulate(
        view, projection,
        (ImGuizmo::OPERATION)operation,
        (ImGuizmo::MODE)mode,
        matrix, deltaMatrix, snap) ? 1 : 0;
}

/// Returns 1 if the mouse cursor is over any gizmo control.
int imguizmo_is_over(void)
{
    return ImGuizmo::IsOver() ? 1 : 0;
}

/// Returns 1 if a gizmo is actively being dragged.
int imguizmo_is_using(void)
{
    return ImGuizmo::IsUsing() ? 1 : 0;
}

/// Draw the orientation cube (ViewManipulate) — call after imguizmo_manipulate.
/// view         : float[16] row-major view matrix (read/write)
/// length       : camera distance / scene size hint for cube size
/// pos_x/pos_y  : screen-pixel position of the cube widget top-left
/// size         : side length in pixels
/// bg_color     : ImU32 RGBA background color (0 = transparent)
/// Returns 1 if the view was changed by the user clicking the cube.
int imguizmo_view_manipulate(
    float* view, float length,
    float pos_x, float pos_y, float size,
    unsigned int bg_color)
{
    ImVec2 pos  = { pos_x, pos_y };
    ImVec2 sz   = { size,  size  };
    ImGuizmo::ViewManipulate(view, length, pos, sz, (ImU32)bg_color);
    // ViewManipulate modifies view in-place; return whether it is being used
    return ImGuizmo::IsUsingViewManipulate() ? 1 : 0;
}

/// Decompose a column-major float[16] matrix into translation/rotation(degrees)/scale float[3] arrays.
void imguizmo_decompose_matrix(
    const float* matrix,
    float* translation, float* rotation, float* scale)
{
    ImGuizmo::DecomposeMatrixToComponents(matrix, translation, rotation, scale);
}

/// Recompose a column-major float[16] matrix from translation/rotation(degrees)/scale float[3] arrays.
void imguizmo_recompose_matrix(
    const float* translation, const float* rotation, const float* scale,
    float* matrix)
{
    ImGuizmo::RecomposeMatrixFromComponents(translation, rotation, scale, matrix);
}

} // extern "C"
