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
using static Imgui.ImguiNative;
using Imgui;
using Mediapipe;
using System.Collections.Generic;

public static unsafe class MediapipetestApp
{
    // // Preload OpenCL framework on macOS to satisfy MediaPipe dependencies
    // [DllImport("/System/Library/Frameworks/OpenCL.framework/OpenCL")]
    // private static extern int clGetPlatformIDs(uint num_entries, IntPtr platforms, out uint num_platforms);

    // static MediapipetestApp()
    // {
    //     // Force OpenCL to load by calling a function
    //     try
    //     {
    //         clGetPlatformIDs(0, IntPtr.Zero, out _);
    //     }
    //     catch
    //     {
    //         // OpenCL not available, but that's okay - MediaPipe might work without it
    //     }
    // }

    struct _state
    {
        public sg_pass_action pass_action;
    }

    static _state state = new _state();
    static List<string> logMessages = new List<string>();
    static byte autoScroll = 1;

    private static void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        logMessages.Add($"[{timestamp}] {message}");
        
        // Keep only last 1000 messages to avoid memory issues
        if (logMessages.Count > 1000)
        {
            logMessages.RemoveAt(0);
        }
    }


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        AddLog("Initialize Sokol and MediaPipe");
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });

        // Initialize ImGui
        simgui_setup(new simgui_desc_t
        {
            logger = {
                func = &slog_func,
            }
        });

        ImGuiIO* io = igGetIO_Nil();
        io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;

        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        var configText = @"
input_stream: ""in""
output_stream: ""out""
node {
  calculator: ""PassThroughCalculator""
  input_stream: ""in""
  output_stream: ""out1""
}
node {
  calculator: ""PassThroughCalculator""
  input_stream: ""out1""
  output_stream: ""out""
}
";

        var graph = new CalculatorGraph(configText);
        var poller = graph.AddOutputStreamPoller<string>("out");
        graph.StartRun();

        for (var i = 0; i < 10; i++)
        {
            var input = Packet.CreateStringAt("Hello World!", i);
            graph.AddPacketToInputStream("in", input);
        }

        graph.CloseInputStream("in");

        // Initialize an empty packet
        var output = new Packet<string>();

        AddLog("Before poller loop");
        while (poller.Next(output))
        {
            AddLog($"Received packet with value: {output.Get()}");
        }

        graph.WaitUntilDone();
        poller.Dispose();
        graph.Dispose();
        output.Dispose();

        AddLog("Done - MediaPipe test completed successfully");
    }

    private static unsafe void DrawLogWindow()
    {
        float windowWidth = sapp_widthf();
        float windowHeight = sapp_heightf();
        
        // Position log window to take most of the screen
        igSetNextWindowSize(new Vector2(windowWidth * 0.9f, windowHeight * 0.8f), ImGuiCond.FirstUseEver);
        igSetNextWindowPos(new Vector2(windowWidth * 0.05f, windowHeight * 0.1f), ImGuiCond.FirstUseEver, Vector2.Zero);

        byte open = 1;
        if (igBegin("MediaPipe Log", ref open, ImGuiWindowFlags.None))
        {
            // Header with controls
            if (igButton("Clear", new Vector2(80, 0)))
            {
                logMessages.Clear();
            }
            igSameLine(0, 10);
            igCheckbox("Auto-scroll", ref autoScroll);
            igSeparator();
            
            // Scrollable log area
            igBeginChild_Str("LogScrollArea", new Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.HorizontalScrollbar);
            
            foreach (var message in logMessages)
            {
                igTextUnformatted(message, null);
            }
            
            // Auto-scroll to bottom when new messages arrive
            if (autoScroll == 1 && igGetScrollY() >= igGetScrollMaxY())
            {
                igSetScrollHereY(1.0f);
            }
            
            igEndChild();
            igEnd();
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        float g = state.pass_action.colors[0].clear_value.g + 0.01f;
        state.pass_action.colors[0].clear_value.g = (g > 1.0f) ? 0.0f : g;

        // Start ImGui frame
        simgui_new_frame(new simgui_frame_desc_t
        {
            width = sapp_width(),
            height = sapp_height(),
            delta_time = sapp_frame_duration(),
            dpi_scale = 1
        });

        // Draw log window
        DrawLogWindow();

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        simgui_render();
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        simgui_handle_event(*e);
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
            width = 1280,
            height = 720,
            sample_count = 4,
            window_title = "MediaPipe Test - Log Viewer",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
