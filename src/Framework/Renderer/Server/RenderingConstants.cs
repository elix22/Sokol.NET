namespace GameEditor.Framework.Renderer.Server
{
    /// <summary>
    /// Global rendering constants. All pre-allocated arrays are sized by these values.
    /// Changing a value here requires restarting the editor (buffers are allocated in Init()).
    /// </summary>
    /// <summary>
    /// A project's Rendering Path (G8.2): Forward loops every light on every pixel; Forward+ puts the lights in a cluster
    /// grid and shades a pixel with its cluster's lights only (examples/GameEditor/docs/CLUSTERED_LIGHTING_DESIGN.md).
    /// </summary>
    public enum RenderingPath { Forward, ForwardPlus }

    /// <summary>The scene's tone curve (G8.3, examples/GameEditor/docs/HDR_TONEMAP_DESIGN.md): None (the picture of before),
    /// Neutral (Khronos PBR Neutral), ACES.</summary>
    public enum TonemapMode { None, Neutral, Aces }

    public static class RenderingConstants
    {
        /// <summary>Maximum number of active lights in a scene (uniform-block limit).</summary>
        public const int MAX_LIGHTS = 32;

        /// <summary>Maximum instances submitted in a single sg_draw call (per draw group).</summary>
        public const int MAX_INSTANCES_PER_DRAW = 1024;

        /// <summary>Total instance slots across all draw groups per frame.</summary>
        public const int MAX_INSTANCES = 65536;

        /// <summary>Maximum number of DrawCommands emitted per frame.</summary>
        public const int MAX_DRAWS = 16384;

        /// <summary>Maximum number of simultaneous render views (scene + game + thumbnails).</summary>
        public const int MAX_VIEWS = 8;

        /// <summary>Number of frames in the instance-buffer ring (triple-buffered).</summary>
        public const int INSTANCE_RING_FRAMES = 3;

        /// <summary>Parallel cull chunk size — tunable; 256 is a good starting point.</summary>
        public const int CULL_CHUNK_SIZE = 256;

        /// <summary>Maximum bones per skeleton for GPU skinning (M4+).</summary>
        public const int MAX_BONES = 128;
    }
}
