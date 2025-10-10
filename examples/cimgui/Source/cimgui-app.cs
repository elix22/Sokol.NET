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
using static Sokol.SImgui;
using static Imgui.ImguiNative;
using Imgui;
using static Sokol.SLog;
using static Imgui.ImGuiHelpers;

public static unsafe class CImguiApp
{

    struct _state
    {
        public byte show_test_window;
        public byte show_another_window;
        public sg_pass_action pass_action;
    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Console.WriteLine("Initialize() Enter");

        sg_setup(new sg_desc()
        {
            environment = sglue_environment()
        });

        simgui_setup(new simgui_desc_t
        {
            logger = {
                func = &slog_func,
            }
        });

        ImGuiIO* io= igGetIO_Nil();
        io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;    // Enable Docking

        state.show_test_window = 1;

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };


    }


    static float flt = 0.0f;
    static byte[] inputBuffer = new byte[256]; // Buffer for text input
    
    [UnmanagedCallersOnly]
    static unsafe int TextInputCallbackImpl(ImGuiInputTextCallbackData* data)
    {
        // Log the callback event
        // Console.WriteLine($"Callback Event: {data->EventFlag}, Char: {(char)data->EventChar}, CursorPos: {data->CursorPos}");
        
        // CallbackCharFilter: Filter and modify character input
        if ((data->EventFlag & ImGuiInputTextFlags.CallbackCharFilter) != 0)
        {
            char c = (char)data->EventChar;
            
            // Convert lowercase to uppercase
            if (c >= 'a' && c <= 'z')
            {
                data->EventChar = (ushort)(c - 32);
                Console.WriteLine($"Converted '{c}' to '{(char)data->EventChar}'");
            }
            
            // Filter out digits
            if (c >= '0' && c <= '9')
            {
                Console.WriteLine($"Filtered out digit: '{c}'");
                return 1; // Return non-zero to filter out this character
            }
        }
        

        
        return 0; // Return 0 to accept the character
    }
    
    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
            // dpi_scale = sapp_dpi_scale() // TBD elix22 , Android wrong scaling
            // dpi_scale = 1.0f
        });

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });


        igText("Hello, world from Eli !");
        igSliderFloat("float value", ref flt, 0.0f, 1.0f, "%.3f", 0);
        igColorEdit3("clear color", ref state.pass_action.colors[0].clear_value.AsVector3, 0);
       
        // Input text with callback - converts to uppercase, filters digits
        // Using ImGuiHelpers for simplified callback usage (just like &Init, &Frame, etc.)
        // Pass &TextInputCallbackImpl to use callback, or null for no callback (safe - won't crash)
        InputText("(uppercase only)", ref inputBuffer[0], (uint)inputBuffer.Length, 
            ImGuiInputTextFlags.CallbackCharFilter | ImGuiInputTextFlags.CallbackAlways, 
            &TextInputCallbackImpl);  // Change to null to disable callback
        if (igButton("Test Window", Vector2.Zero)) state.show_test_window ^= 1;
        if (igButton("Another Window", Vector2.Zero)) state.show_another_window ^= 1;

        // 2. Show another simple window, this time using an explicit Begin/End pair
        if (state.show_another_window == 1)
        {
            igSetNextWindowSize(new Vector2(200, 100), ImGuiCond.FirstUseEver);
            igBegin("Another Window", ref state.show_another_window, 0);
            igText("Hello");
            igEnd();
        }

        // 3. Show the ImGui test window. Most of the sample code is in ImGui::ShowDemoWindow()
        if (state.show_test_window == 1)
        {
            igSetNextWindowPos(new Vector2(460, 20), ImGuiCond.FirstUseEver, Vector2.Zero);
            byte show_demo_window = 1;
            igShowDemoWindow(ref show_demo_window);
        }

        var io = igGetIO_Nil();
        igText($"Application average {1000.0f / io->Framerate:F3} ms/frame ({io->Framerate:F0} FPS)");


        simgui_render();

        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        // Safety check for null pointer
        if (e == null)
        {
            Console.WriteLine("Warning: Null event pointer received");
            return;
        }

        // Handle all events but with safety measures to prevent crashes
        try
        {
            // Pass all events to ImGui, but with enhanced error handling
            simgui_handle_event(in *e);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in simgui_handle_event: {ex.Message}");
            // Log the event type that caused the crash for debugging
            Console.WriteLine($"Problematic event type: {e->type}");
            // Don't crash the entire application due to event handling errors
        }
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        simgui_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 0,  // Let iOS determine the size based on orientation
            height = 0, // Let iOS determine the size based on orientation
            sample_count = 4,
            window_title = "cimgui (sokol-app)",
            icon = { sokol_default = true },
            enable_clipboard = true, // Re-enabled to debug gibberish text issue
            clipboard_size = 8192, // Set proper clipboard buffer size (default is 8192 bytes)
        };
    }

}
