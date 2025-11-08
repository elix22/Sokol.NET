using System;
using Sokol;
using static Sokol.SG;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SImgui;

public static unsafe partial class SharpGLTFApp
{
    static void ApplicationCleanup()
    {
        // Print texture cache statistics before cleanup
        Info("[SharpGLTF] Cleanup - Texture Cache Statistics:");
        TextureCache.Instance.PrintStats();

        state.model?.Dispose();

        // Shutdown texture cache (will dispose all cached textures and cleanup Basis Universal)
        TextureCache.Instance.Shutdown();

        FileSystem.Instance.Shutdown();
        simgui_shutdown();
        sg_shutdown();

        // Force a complete shutdown if debugging
        if (Debugger.IsAttached)
        {
            Environment.Exit(0);
        }
    }
}