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
    // const string filename = "DamagedHelmet.glb";
    //   const string filename = "assimpScene.glb";
    // const string filename = "gltf/DamagedHelmet/DamagedHelmet.gltf";

    // const string filename = "DancingGangster.glb";
    // const string filename = "Gangster.glb";

    //race_track
    // const string filename = "race_track.glb";
    // const string filename = "mainsponza/NewSponza_Main_glTF_003.gltf";

    // const string filename = "glb/2CylinderEngine.glb";

    // const string filename = "ABeautifulGame/glTF/ABeautifulGame.gltf";
    // const string filename = "glb/AlphaBlendModeTest.glb";
    // const string filename = "glb/AntiqueCamera.glb";

    // const string filename = "AttenuationTest/glTF-Binary/AttenuationTest.glb";


    // const string filename = "glb/BoomBox.glb";

    // const string filename = "glb/ClearCoatCarPaint.glb";

    // const string filename = "ClearcoatRing/glTF/ClearcoatRing.gltf";

    // const string filename = "glb/DragonAttenuation.glb";

    const string filename = "EmissiveStrengthTest/glTF-Binary/EmissiveStrengthTest.glb";

    // const string filename = "glb/MetalRoughSpheres.glb";

    // const string filename = "MosquitoInAmber/glTF-Binary/MosquitoInAmber.glb";

    //  const string filename = "Sponza/glTF/Sponza.gltf";

    // Bloom post-processing structures
    struct BloomPass
    {
        public sg_pass scene_pass;       // Main scene render target
        public sg_pass bright_pass;      // Bright pass extraction
        public sg_pass blur_h_pass;      // Horizontal blur
        public sg_pass blur_v_pass;      // Vertical blur
        // Note: composite pass renders to swapchain and is created each frame
        
        // Model rendering pipelines for offscreen scene pass (sample_count = 1)
        public sg_pipeline scene_standard_pipeline;
        public sg_pipeline scene_skinned_pipeline;
        public sg_pipeline scene_standard_blend_pipeline;
        public sg_pipeline scene_skinned_blend_pipeline;
        public sg_pipeline scene_standard_mask_pipeline;
        public sg_pipeline scene_skinned_mask_pipeline;
        
        // Bloom post-processing pipelines
        public sg_pipeline bright_pipeline;
        public sg_pipeline blur_h_pipeline; 
        public sg_pipeline blur_v_pipeline;
        public sg_pipeline composite_pipeline;
        
        public sg_bindings bright_bindings;
        public sg_bindings blur_h_bindings;
        public sg_bindings blur_v_bindings;
        public sg_bindings composite_bindings;
        
        public sg_image scene_color_img;     // Main scene color buffer
        public sg_image scene_depth_img;     // Main scene depth buffer
        public sg_image bright_img;          // Bright pass result
        public sg_image blur_h_img;          // Horizontal blur result
        public sg_image blur_v_img;          // Vertical blur result (final bloom)
        
        public sg_sampler sampler;           // Linear sampler for all passes
    }

    class _state
    {
        public sg_pass_action pass_action;
        public Sokol.Camera camera = new Sokol.Camera();
        public SharpGltfModel? model;
        public SharpGltfAnimator? animator;
        public bool modelLoaded = false;
        public bool cameraInitialized = false;  // Track if camera has been auto-positioned
        public bool isMixamoModel = false;      // Track if this is a Mixamo model needing special transforms
        public Vector3 modelBoundsMin;
        public Vector3 modelBoundsMax;

        // Model rotation (middle mouse button)
        public float modelRotationX = 0.0f;     // Rotation around X-axis (vertical mouse movement)
        public float modelRotationY = 0.0f;     // Rotation around Y-axis (horizontal mouse movement)
        public bool middleMouseDown = false;    // Track middle mouse button state

        // Culling statistics
        public int totalMeshes = 0;
        public int visibleMeshes = 0;
        public int culledMeshes = 0;
        public bool enableFrustumCulling = true;

        // Lighting system
        public List<Light> lights = new List<Light>();
        public float ambientStrength = 0.03f;   // Ambient light strength (0.0 to 1.0)
        
        // Bloom post-processing
        public BloomPass bloom;
        public bool enableBloom = false;
        public float bloomIntensity = 1.5f;      // Bloom intensity (0.0 - 2.0)
        public float bloomThreshold = 0.8f;      // Brightness threshold (0.0 - 10.0)
    }

    static _state state = new _state();
    static bool _loggedMeshInfoOnce = false;  // Debug flag for mesh info
    static int _frameCount = 0;  // Frame counter for debugging


    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        InitApplication();

    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        RunSingleFrame();
    }


    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
        HandleEvent(e);
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        ApplicationCleanup();
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
            window_title = "SharpGLTF  (sokol-app)",
            icon = { sokol_default = true },
            logger = {
                func = &slog_func,
            }
        };
    }

}
