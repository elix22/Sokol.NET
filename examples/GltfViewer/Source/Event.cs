
using static Sokol.SApp;
using static Sokol.SImgui;

public static unsafe partial class GltfViewer
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

        // Handle camera input based on active camera type
        if (state.usingGltfCamera && state.gltfCamera != null)
        {
            // === GltfCamera Input ===
            
            // Mouse drag - free-look rotation
            if (e->type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE)
            {
                if (e->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
                {
                    state.gltfCamera.Rotate(e->mouse_dx, -e->mouse_dy);
                }
            }
            
            // Keyboard - free movement
            float dt = (float)sapp_frame_duration();
            
            if (e->type == sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN || 
                e->type == sapp_event_type.SAPP_EVENTTYPE_CHAR)
            {
                switch (e->key_code)
                {
                    case sapp_keycode.SAPP_KEYCODE_W:
                        state.gltfCamera.MoveForward(dt);
                        break;
                    case sapp_keycode.SAPP_KEYCODE_S:
                        state.gltfCamera.MoveForward(-dt);
                        break;
                    case sapp_keycode.SAPP_KEYCODE_A:
                        state.gltfCamera.MoveRight(-dt);
                        break;
                    case sapp_keycode.SAPP_KEYCODE_D:
                        state.gltfCamera.MoveRight(dt);
                        break;
                    case sapp_keycode.SAPP_KEYCODE_Q:
                        state.gltfCamera.MoveUp(-dt);
                        break;
                    case sapp_keycode.SAPP_KEYCODE_E:
                        state.gltfCamera.MoveUp(dt);
                        break;
                }
            }
        }
        else
        {
            // === Orbit Camera Input ===
            // Camera handles all input events including keyboard and touch
            state.camera.HandleEvent(e);
        }

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

