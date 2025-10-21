using System;
using System.IO;
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
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using Assimp;
public static unsafe class AssimpSimpleApp
{
    private const int NUM_FONTS = 3;
    private const int FONT_KC854 = 0;
    private const int FONT_C64 = 1;
    private const int FONT_ORIC = 2;
    struct color_t
    {
        public byte r, g, b;
    };

    enum state_loading_enum
    {
        STATE_IDLE = 0,
        STATE_LOADING,
        STATE_LOADED,
        STATE_FAILED
    }

    class _state
    {
        public sg_pass_action pass_action;
        public color_t[] palette = new color_t[NUM_FONTS];
        public SharedBuffer fetch_buffer = SharedBuffer.Create(1024 * 1024);
        public state_loading_enum state_loading = state_loading_enum.STATE_IDLE;
    }

    static _state state = new _state();


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        Console.WriteLine("Assimp: Init()");
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = {
                func = &slog_func,
            }
        });

        state.palette[FONT_KC854] = new color_t { r = 0xf4, g = 0x43, b = 0x36 };
        state.palette[FONT_C64] = new color_t { r = 0x21, g = 0x96, b = 0xf3 };
        state.palette[FONT_ORIC] = new color_t { r = 0x4c, g = 0xaf, b = 0x50 };

        sdtx_desc_t desc = default;
        desc.fonts[FONT_KC854] = sdtx_font_kc854();
        desc.fonts[FONT_C64] = sdtx_font_c64();
        desc.fonts[FONT_ORIC] = sdtx_font_oric();
        sdtx_setup(desc);

        // setup sokol-fetch with the minimal "resource limits"
        sfetch_setup(new sfetch_desc_t()
        {
            max_requests = 1,
            num_channels = 1,
            num_lanes = 1,
            logger = {
                func = &slog_func,
            }
        });


        state.pass_action = default;
        state.pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        state.pass_action.colors[0].clear_value = new sg_color { r = 0.25f, g = 0.5f, b = 0.75f, a = 1.0f };

        sfetch_request_t request = default;
        // Use .collada extension to prevent iOS from converting the XML file to binary plist
        request.path = util_get_file_path("duck.collada");
        request.callback = &fetch_callback;
        request.buffer = new sfetch_range_t()
        {
            ptr = Unsafe.AsPointer(ref state.fetch_buffer.Buffer[0]),
            size = (uint)state.fetch_buffer.Buffer.Length
        };
        sfetch_send(request);

        state.state_loading = state_loading_enum.STATE_LOADING;

    }

    [UnmanagedCallersOnly]
    static void fetch_callback(sfetch_response_t* response)
    {
        if (response->fetched)
        {
            Console.WriteLine($"Assimp: File fetched, size: {response->data.size} bytes");
        }

        if (response->finished)
        {
            if (response->failed)
            {
                Console.WriteLine($"Assimp: Failed to fetch file: {response->error_code}");
                 state.state_loading = state_loading_enum.STATE_FAILED;
                return;
            }

            Console.WriteLine($"Assimp: Fetch completed successfully, parsing {response->data.size} bytes");

            // Check first few bytes to verify data is XML (not binary plist)
            Console.WriteLine($"Assimp: First 10 bytes: {BitConverter.ToString(state.fetch_buffer.Buffer, 0, Math.Min(10, (int)response->data.size))}");

            // File successfully loaded, now parse with Assimp
            var stream = new MemoryStream(state.fetch_buffer.Buffer, 0, (int)response->data.size);
            PostProcessSteps ppSteps = PostProcessPreset.TargetRealTimeQuality | PostProcessSteps.FlipWindingOrder;
            AssimpContext importer = new AssimpContext();
            importer.SetConfig(new NormalSmoothingAngleConfig(66f));

            // Extract file extension from the path to use as format hint
            string path = response->path != IntPtr.Zero ? Marshal.PtrToStringUTF8((IntPtr)response->path) ?? "" : "";
            string formatHint = Path.GetExtension(path).TrimStart('.'); // Get extension without the dot (e.g., "dae", "obj", "fbx")

            Scene scene = importer.ImportFileFromStream(stream, ppSteps, formatHint);
            if (scene != null)
            {
                    state.state_loading = state_loading_enum.STATE_LOADED;
                Console.WriteLine($"Assimp: Successfully loaded model (format: {formatHint}).");
            }
            else
            {
                state.state_loading = state_loading_enum.STATE_FAILED;
                Console.WriteLine($"Assimp: Failed to load model (format: {formatHint}).");
            }
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        sfetch_dowork();

        sdtx_canvas(sapp_width() * 0.5f, sapp_height() * 0.5f);
        sdtx_origin(3.0f, 3.0f);
        color_t color = state.palette[0];
        sdtx_color3b(color.r, color.g, color.b);
        sdtx_font((uint)0);
        sdtx_print("Assimp Simple App\n");
        sdtx_print("Loading State: {0}\n", state.state_loading.ToString());

        float g = state.pass_action.colors[0].clear_value.g + 0.01f;
        state.pass_action.colors[0].clear_value.g = (g > 1.0f) ? 0.0f : g;

        sg_begin_pass(new sg_pass { action = state.pass_action, swapchain = sglue_swapchain() });
        sdtx_draw();
        sg_end_pass();
        sg_commit();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {

    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        state.fetch_buffer.Dispose();
        sfetch_shutdown();
        sdtx_shutdown();
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
            width = 0,
            height = 0,
            sample_count = 4,
            window_title = "Clear (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
