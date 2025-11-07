using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SImgui;
using Imgui;
using static Imgui.ImguiNative;
using SharpGLTF.Schema2;

public static unsafe partial class SharpGLTFApp
{
    private static unsafe void HandleEvent(sapp_event* e)
    {
        // Handle window resize - destroy ALL sokol resources and recreate from scratch
        // This ensures we don't leak any resources (pipelines, shaders, images, views, etc.)
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_RESIZED)
        {
            CleanupAllResources();
            InitializeTransmission();
            InitializeBloom();
            return;
        }
        
        // Handle ImGui events first
        if (simgui_handle_event(in *e))
        {
            return; // ImGui consumed the event
        }

        // Handle middle mouse button for model rotation
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN &&
            e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE)
        {
            state.middleMouseDown = true;
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP &&
                 e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE)
        {
            state.middleMouseDown = false;
        }
        else if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE && state.middleMouseDown)
        {
            // Rotate model: horizontal mouse movement rotates around Y-axis, vertical around X-axis
            state.modelRotationY += e->mouse_dx * 0.01f;  // Horizontal movement -> Y-axis rotation
            state.modelRotationX += e->mouse_dy * 0.01f;  // Vertical movement -> X-axis rotation
            return; // Don't pass to camera
        }

        // Camera handles all input events including keyboard and touch
        state.camera.HandleEvent(e);

        // After camera processes touch, check if 2-finger touch is active and handle model rotation
        if (e->type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED && 
            e->num_touches >= 2 && 
            state.camera.IsTwoFingerTouchActive())
        {
            var (dx, dy) = state.camera.GetTwoFingerTouchDelta(e->touches[0].pos_x, e->touches[0].pos_y);
            state.modelRotationY += dx * 0.01f;
            state.modelRotationX += dy * 0.01f;
        }

    }
}

