// IblSkyboxLoader.cs — loads the editor's default IBL environment from 6 pre-baked
// skybox face images and builds an EnvironmentMap from them.
//
// Mirrors CGltfViewer's InitializeIBL(useCubemapFaces=true) → LoadCubemapFacesAsync:
// 6 JPG faces decoded with stb_image (cross-platform, Web included — no TinyEXR / no
// HDR-panorama prefilter), assembled into a cube + box-filter mips + BRDF LUT.
//
// File access matches the glTF importer: the editor runs an external project (as the
// common editors do), so runtime assets live in the OPEN PROJECT's Assets folder, read
// directly on desktop. On Web/mobile assets are bundled flat and SFilesystem resolves
// them. On any failure the caller keeps the procedural EnvironmentMap fallback.

using System;
using System.IO;
using Sokol;
using GameEditor.Framework.Core;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    public static unsafe class IblSkyboxLoader
    {
        // Default face file names (sokol cube convention: +X,-X,+Y,-Y,+Z,-Z), joined to the
        // configured folder when a per-face override is not supplied.
        private static readonly string[] DefaultNames =
        {
            "skybox_px.jpg", "skybox_nx.jpg", "skybox_py.jpg",
            "skybox_ny.jpg", "skybox_pz.jpg", "skybox_nz.jpg",
        };

        private sealed class State
        {
            public string[] Paths = System.Array.Empty<string>();
            public readonly byte[]?[] Faces = new byte[6][];
            public int  Loaded;
            public int  FaceSize;
            public bool Failed;
            public Action<EnvironmentMap?>? OnComplete;
        }

        /// <summary>
        /// Resolves the 6 face paths from <paramref name="folder"/> (Assets-relative) plus any
        /// non-empty per-face <paramref name="faceOverrides"/>, loads + decodes them, and builds
        /// an <see cref="EnvironmentMap"/>. <paramref name="onComplete"/> fires on the main
        /// thread with the env, or null on any failure (caller keeps a procedural fallback).
        /// On desktop the faces read synchronously, so the callback runs inline.
        /// </summary>
        public static void LoadAsync(string folder, string[]? faceOverrides, Action<EnvironmentMap?> onComplete)
        {
            var state = new State { OnComplete = onComplete, Paths = BuildFacePaths(folder, faceOverrides) };
            for (int i = 0; i < 6; i++)
            {
                int face = i;
                LoadBytes(state.Paths[face], bytes => OnFaceBytes(state, face, bytes));
                if (state.Failed) return;
            }
        }

        // folder/skybox_*.jpg, unless a non-empty override gives a full Assets-relative path.
        private static string[] BuildFacePaths(string folder, string[]? overrides)
        {
            string baseDir = (folder ?? "").Trim().TrimEnd('/');
            var paths = new string[6];
            for (int i = 0; i < 6; i++)
            {
                string? ov = overrides != null && i < overrides.Length ? overrides[i] : null;
                paths[i] = !string.IsNullOrWhiteSpace(ov)
                    ? ov!.Trim()
                    : (baseDir.Length > 0 ? baseDir + "/" + DefaultNames[i] : DefaultNames[i]);
            }
            return paths;
        }

        private static void OnFaceBytes(State state, int face, byte[]? encoded)
        {
            if (state.Failed) return;

            if (encoded == null || encoded.Length == 0)
            {
                Fail(state, $"face {face} ('{state.Paths[face]}') not found");
                return;
            }

            byte[]? rgba = Decode(encoded, out int w, out int h);
            if (rgba == null)
            {
                Fail(state, $"face {face} decode failed");
                return;
            }
            if (w != h)
            {
                Fail(state, $"face {face} is {w}×{h} (must be square)");
                return;
            }
            if (state.FaceSize == 0) state.FaceSize = w;
            else if (w != state.FaceSize)
            {
                Fail(state, $"face {face} size {w} ≠ {state.FaceSize}");
                return;
            }

            state.Faces[face] = rgba;
            state.Loaded++;
            if (state.Loaded != 6) return;

            var faces = new byte[6][];
            for (int f = 0; f < 6; f++) faces[f] = state.Faces[f]!;
            EnvironmentMap env;
            try { env = EnvironmentMap.CreateFromFaces(faces, state.FaceSize); }
            catch (Exception ex) { Fail(state, $"build failed: {ex.Message}"); return; }

            var cb = state.OnComplete;
            state.OnComplete = null;
            cb?.Invoke(env);
        }

        private static void Fail(State state, string reason)
        {
            if (state.Failed) return;
            state.Failed = true;
            Core.Logger.Warning($"[IBL] skybox load failed ({reason}) → keeping procedural env");
            var cb = state.OnComplete;
            state.OnComplete = null;
            cb?.Invoke(null);
        }

        // RGBA8 decode via stb_image (4 forced channels).
        private static byte[]? Decode(byte[] encoded, out int w, out int h)
        {
            w = 0; h = 0; int ch = 0;
            byte* px = StbImage.stbi_load_csharp(in encoded[0], encoded.Length, ref w, ref h, ref ch, 4);
            if (px == null) return null;
            var rgba = new byte[w * h * 4];
            fixed (byte* dst = rgba) Buffer.MemoryCopy(px, dst, rgba.Length, rgba.Length);
            StbImage.stbi_image_free_csharp(px);
            return rgba;
        }

        private static void LoadBytes(string relPath, Action<byte[]?> cb)
        {
            // Desktop editor: read straight from the open project's Assets folder — the
            // same resolution the glTF/OBJ loaders use (sokol-fetch can't reach an external
            // project dir on desktop). Returns synchronously, so the env builds in Init.
            if (ConfigManager.HasProject)
            {
                try
                {
                    string abs = Path.Combine(ConfigManager.ProjectFolder!, "Assets",
                        relPath.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(abs)) { cb(File.ReadAllBytes(abs)); return; }
                }
                catch (Exception ex) { Core.Logger.Warning($"[IBL] read '{relPath}' failed: {ex.Message}"); }
            }
            // Web/mobile (or desktop miss): bundled flat, resolved by sokol-fetch.
            SFilesystem.LoadFileAsync(relPath, (p, b, s) =>
                cb(s == SFileLoadStatus.Success ? b : null));
        }
    }
}
