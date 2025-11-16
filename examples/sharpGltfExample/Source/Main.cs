using System;
using Sokol;
using System.Runtime.InteropServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SLog;
using SharpGLTF.Schema2;

/// <summary>
/// Skinning system mode for animated meshes
/// </summary>
public enum SkinningMode
{
    /// <summary>
    /// Uniform-based skinning (main branch): Fast on mobile, passes bone matrices via shader uniforms.
    /// Limited to ~85 bones but very efficient. Best for mobile/low-end devices.
    /// </summary>
    UniformBased,
    
    /// <summary>
    /// Texture-based skinning (pbr-transition): Supports unlimited bones via texture lookup.
    /// Requires GPU texture upload every frame - slower on mobile but no bone limit.
    /// </summary>
    TextureBased
}

public static unsafe partial class SharpGLTFApp
{
    // Please check the license of each model before using it for commercial purposes!
    static readonly string[] availableModels = new string[]
    {
        "CommercialRefrigerator/glTF/CommercialRefrigerator.gltf",
        "littlest_tokyo/LittleTokio.gltf" ,
        "BusterDrone/BusterDrone.gltf",
        "FishAndShark/FishAndShark.gltf",
        "Fish/Fish.gltf",
        "Shark/Shark.gltf",
        "Dragon/Dragon.gltf",
        "DancingGangster/glTF-Binary/DancingGangster.glb",
        "Airplane/Airplane.gltf",
        "Airplane/Airplane2.glb",
        "Lee-Perry-Smith-Head/head.glb",
        "Ferrari/Ferrari.glb",
        "CarConcept/glTF/CarConcept.gltf",
        "GlassHurricaneCandleHolder/glTF/GlassHurricaneCandleHolder.gltf",
        "MosquitoInAmber/glTF/MosquitoInAmber.gltf",
        "MorphStressTest/glTF/MorphStressTest.gltf",
        "ChronographWatch/glTF/ChronographWatch.gltf",
        "DamagedHelmet/glTF/DamagedHelmet.gltf",
        "DiffuseTransmissionPlant/glTF/DiffuseTransmissionPlant.gltf",
        "GlassVaseFlowers/glTF/GlassVaseFlowers.gltf",
        "PotOfCoalsAnimationPointer/glTF/PotOfCoalsAnimationPointer.gltf",
        "WaterBottle/glTF/WaterBottle.gltf",
        "FlightHelmet/glTF/FlightHelmet.gltf",
        "ABeautifulGame/glTF/ABeautifulGame.gltf",
        "BoomBox/glTF-Binary/BoomBox.glb",
        "IridescenceLamp/glTF-Binary/IridescenceLamp.glb",
        "DragonAttenuation/glTF/DragonAttenuation.gltf", 
/* Rmoved to conserve space
        "MetalRoughSpheres/glTF/MetalRoughSpheres.gltf",
        "ChairDamaskPurplegold/glTF/ChairDamaskPurplegold.gltf",
        "TransmissionTest/glTF/TransmissionTest.gltf",
        "CompareTransmission/glTF/CompareTransmission.gltf",
        "ClearCoatCarPaint/glTF/ClearCoatCarPaint.gltf",
        "CesiumMilkTruck/glTF/CesiumMilkTruck.gltf",
        "CompareIor/glTF/CompareIor.gltf",
        "EmissiveStrengthTest/glTF-Binary/EmissiveStrengthTest.glb",
        "EnvironmentTest/glTF/EnvironmentTest.gltf",
        "GlassBrokenWindow/glTF/GlassBrokenWindow.gltf",
        "PotOfCoals/glTF/PotOfCoals.gltf",
        "LightsPunctualLamp/glTF/LightsPunctualLamp.gltf",
        "InterpolationTest/glTF/InterpolationTest.gltf",
        "IORTestGrid/glTF/IORTestGrid.gltf",
        "Lantern/glTF/Lantern.gltf",
        "MetalRoughSpheres/glTF/MetalRoughSpheres.gltf",
        "AlphaBlendModeTest/glTF-Binary/AlphaBlendModeTest.glb",
        "AntiqueCamera/glTF-Binary/AntiqueCamera.glb",
        "AttenuationTest/glTF-Binary/AttenuationTest.glb",
        "ClearcoatRing/glTF/ClearcoatRing.gltf",
*/
    };

    static string filename => availableModels[state.currentModelIndex];


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
        public sg_pipeline scene_morphing_pipeline;
        public sg_pipeline scene_skinned_morphing_pipeline;
        public sg_pipeline scene_standard_blend_pipeline;
        public sg_pipeline scene_skinned_blend_pipeline;
        public sg_pipeline scene_morphing_blend_pipeline;
        public sg_pipeline scene_skinned_morphing_blend_pipeline;
        public sg_pipeline scene_standard_mask_pipeline;
        public sg_pipeline scene_skinned_mask_pipeline;
        public sg_pipeline scene_morphing_mask_pipeline;
        public sg_pipeline scene_skinned_morphing_mask_pipeline;
        
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
        public sg_image dummy_depth_img;     // 1x1 dummy depth for WebGL compatibility
        
        public sg_sampler sampler;           // Linear sampler for all passes
    }

    struct TransmissionPass
    {
        // Two-pass rendering: opaque objects first, then transparent with refraction
        public sg_pass opaque_pass;          // Render opaque objects to screen texture
        
        // Pipelines for opaque rendering (captures scene behind transparent objects)
        public sg_pipeline opaque_standard_pipeline;
        public sg_pipeline opaque_skinned_pipeline;
        public sg_pipeline opaque_morphing_pipeline;
        public sg_pipeline opaque_skinned_morphing_pipeline;
        public sg_pipeline opaque_standard_blend_pipeline;
        public sg_pipeline opaque_skinned_blend_pipeline;
        public sg_pipeline opaque_standard_mask_pipeline;
        public sg_pipeline opaque_skinned_mask_pipeline;
        
        public sg_image screen_color_img;    // Screen texture for refraction sampling
        public sg_image screen_depth_img;    // Depth buffer
        public sg_view screen_color_view;    // View for screen texture (created once, reused)
        public sg_sampler sampler;           // Linear sampler for screen texture
        
    }

    struct UIState
    {
        public bool model_info_open;
        public bool model_browser_open;
        public bool animation_open;
        public bool lighting_open;
        public bool bloom_open;
        public bool tonemap_open;            // Tone mapping controls
        public bool glass_materials_open;
        public bool ibl_open;                // IBL controls
        public bool culling_open;
        public bool statistics_open;
        public bool camera_info_open;
        public bool camera_controls_open;
        public bool help_open;
        public bool debug_view_open;
        public int theme;
        
        // Debug view state
        public int debug_view_enabled;  // 0 = disabled, 1 = enabled
        public int debug_view_mode;     // Which debug view to display
    }

    class _state
    {
        public sg_color clear_color = new sg_color { r = 0.5f, g = 0.5f, b = 0.5f, a = 1.0f };
        public sg_pass_action pass_action;
        public Sokol.Camera camera = new Sokol.Camera();
        public SharpGltfModel? model;
        public SharpGltfAnimator? animator;
        public bool modelLoaded = false;
        public bool cameraInitialized = false;  // Track if camera has been auto-positioned
        public bool isMixamoModel = false;      // Track if this is a Mixamo model needing special transforms
        public BoundingBox modelBounds;

        // Skinning system configuration
        public SkinningMode skinningMode = SkinningMode.UniformBased;  // Default to fast uniform-based skinning

        // Joint matrix texture for skinning (texture-based mode only)
        public sg_image jointMatrixTexture;
        public sg_view jointMatrixView;
        public sg_sampler jointMatrixSampler;
        public int jointTextureWidth = 0;  // Calculated based on bone count

        public float[]? jointTextureData = null;

        // Morph target texture (texture2DArray for vertex displacements)
        public sg_image morphTargetTexture;
        public sg_view morphTargetView;
        public sg_sampler morphTargetSampler;
        public int morphTextureWidth = 0;  // Calculated based on vertex count
        public int morphTextureLayerCount = 0;  // Number of layers in texture array

        // Model browser
        public int currentModelIndex = 0;
        public bool isLoadingModel = false;
        public string loadingStage = "";
        public int loadingProgress = 0;  // 0-100

        // Async loading state for GLTF dependencies
        public ModelRoot? pendingModelRoot = null;
        public SharpGLTF.Schema2.ModelRoot.AsyncSatelliteLoadState? asyncLoadState = null;
        public string? pendingModelPath = null;

        // Model rotation (middle mouse button)
        public float modelRotationX = 0.0f;     // Rotation around X-axis (vertical mouse movement)
        public float modelRotationY = 0.0f;     // Rotation around Y-axis (horizontal mouse movement)
        public bool middleMouseDown = false;    // Track middle mouse button state

        // Culling statistics
        public int totalMeshes = 0;
        public int visibleMeshes = 0;
        public int culledMeshes = 0;
        public bool enableFrustumCulling = true;

        // Rendering statistics
        public int totalVertices = 0;
        public int totalIndices = 0;
        public int totalFaces = 0;

        // Lighting system
        public List<Light> lights = new List<Light>();
        public float ambientStrength = 0.8f;    // Test: very low ambient
        
        // Light nodes from glTF (for animation updates)
        // Using SharpGltfNode wrapper (not Schema2.Node) because animator updates wrapper transforms
        public List<(SharpGltfNode node, int lightIndex)> lightNodes = new List<(SharpGltfNode, int)>();
        
        // Bloom post-processing
        public BloomPass bloom;
        public bool enableBloom = false;
        public float bloomIntensity = 1.5f;      // Bloom intensity (0.0 - 2.0)
        public float bloomThreshold = 0.8f;      // Brightness threshold (0.0 - 10.0)

        // Tone mapping settings
        public float exposure = 1.0f;            // Exposure (0.1 - 10.0)
        public int tonemapType = 4;              // 1=ACES Narkowicz, 2=ACES Hill, 3=ACES Hill+Boost, 4=Khronos PBR Neutral

        // Transmission (glass/refraction) rendering
        // Automatically enabled when model contains meshes with transmission_factor > 0
        public TransmissionPass transmission;
        
        // Skybox renderer for environment map background
        public SkyboxRenderer skybox = new SkyboxRenderer();

        // Glass material overrides (for testing/debugging)
        public bool overrideGlassMaterials = false;
        public float overrideIOR = 1.5f;               // 1.0-2.4 (water=1.33, glass=1.5, diamond=2.4)
        public float overrideTransmission = 1.0f;      // 0.0-1.0
        public Vector3 overrideAttenuationColor = new Vector3(1.0f, 1.0f, 1.0f);  // RGB color
        public float overrideAttenuationDistance = 1.0f;  // Distance for Beer's Law
        public float overrideThickness = 1.0f;         // Thickness multiplier

        // Image-Based Lighting (IBL)
        public EnvironmentMap? environmentMap;
        public bool useIBL = true;                     // Enable/disable IBL
        public float iblIntensity = 1.0f;              // IBL brightness multiplier
        public float iblRotationDegrees = 0.0f;        // Environment rotation in degrees
        public bool renderEnvironmentMap = false;      // Render environment map as background

        // UI state
        public UIState ui;
    }

    static _state state = new _state();
    static bool _loggedMeshInfoOnce = false;  // Debug flag for mesh info
    static bool _loggedTransmissionDebug = false;  // Debug flag for transmission info
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
