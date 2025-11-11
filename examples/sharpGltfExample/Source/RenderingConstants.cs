using System;

namespace Sokol
{
    /// <summary>
    /// Global rendering constants shared between C# and shaders.
    /// These values must match the corresponding #define values in GLSL shader files.
    /// </summary>
    public static class RenderingConstants
    {
        /// <summary>
        /// Maximum number of lights supported in the scene.
        /// This value must match MAX_LIGHTS in pbr_fs_uniforms.glsl.
        /// If you change this, you must:
        /// 1. Update MAX_LIGHTS in pbr_fs_uniforms.glsl
        /// 2. Recompile all shaders using the build system
        /// 3. Rebuild the application
        /// </summary>
        public const int MAX_LIGHTS = 4;
    }
}
