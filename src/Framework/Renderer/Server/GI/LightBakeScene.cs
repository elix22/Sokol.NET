using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Lighting;
using GameEditor.Framework.Renderer.Server.Materials;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>
    /// The scene as the lightmap baker sees it (M8a, docs BAKED_GI_DESIGN.md §3.3), gathered on the main thread: every
    /// active, visible renderer with Contribute GI (its mesh's CPU copy - <see cref="Resources.MeshRegistry.KeepCpuCopies"/>
    /// - and where it is), its materials' albedo and emission, the Mixed and Baked lights as the renderer places them,
    /// and the environment's sky.
    /// </summary>
    public static class LightBakeScene
    {
        /// <summary>The bake's input; <paramref name="skipped"/> names the renderers left out (no mesh on the CPU).</summary>
        public static BakeInput Gather(LightingSettings settings, List<string> skipped)
        {
            var world = ECSWorld.Instance;
            var input = new BakeInput { Settings = settings, Sky = Sky(RenderingServer.CurrentEnvironmentSettings) };
            var maps = new Dictionary<string, BakeTexture?>(StringComparer.Ordinal);

            foreach (var row in world.Query<ActiveFlag, MeshRenderer, Transform>().EnumerateWithEntities<ActiveFlag, MeshRenderer, Transform>())
            {
                if (!row.Item1.Value.Active) continue;
                ref readonly var mr = ref row.Item2.Value;
                if (!mr.ContributeGI || !mr.Visible || string.IsNullOrEmpty(mr.MeshPath)) continue;
                var mesh = RenderingServer.Meshes.GetByPath(mr.MeshPath);
                if (mesh?.Cpu is not { } cpu)
                {
                    skipped.Add($"{mr.MeshPath} (no mesh in memory)");
                    continue;
                }
                var mats = new BakeMaterial[cpu.Length];
                for (int s = 0; s < cpu.Length; s++)
                {
                    string key = !string.IsNullOrEmpty(mr.MaterialPath) && mr.MaterialPath != MeshRenderer.NoMaterialSentinel ? mr.MaterialPath
                               : s < mesh.SubMeshes.Length ? mesh.SubMeshes[s].MaterialKey : "";
                    mats[s] = Material(key, maps);
                }
                input.Meshes.Add(new BakeMesh
                {
                    SceneId = (int)world.GetSceneId(row.Entity),
                    World = Transform.GetWorldMatrix(world, row.Item3.Value),
                    Source = cpu,
                    Materials = mats,
                    ScaleInLightmap = mr.ScaleInLightmap,
                });
            }

            foreach (var row in world.Query<ActiveFlag, LightComponent, Transform>().EnumerateWithEntities<ActiveFlag, LightComponent, Transform>())
            {
                if (!row.Item1.Value.Active) continue;
                ref readonly var lc = ref row.Item2.Value;
                if (!lc.Enabled || lc.Mode == LightMode.Realtime) continue;
                var m = Transform.GetWorldMatrix(world, row.Item3.Value);
                input.Lights.Add(new BakeLight
                {
                    SceneId = (int)world.GetSceneId(row.Entity),
                    Type = lc.Type,
                    Mode = lc.Mode,
                    Position = new Vector3(m.M41, m.M42, m.M43),
                    Direction = Vector3.Normalize(new Vector3(m.M31, m.M32, m.M33)),   // the renderer's light forward
                    Color = lc.Color * lc.Intensity,
                    Range = lc.Range,
                    InnerCos = MathF.Cos(lc.InnerAngle * MathF.PI / 180f),
                    OuterCos = MathF.Cos(lc.OuterAngle * MathF.PI / 180f),
                    IndirectMultiplier = lc.IndirectMultiplier,
                    Shadows = lc.CastsShadows,
                });
            }
            // the shadowmask's light: the strongest Mixed sun that casts shadows (the renderer's own pick among them)
            float sunIntensity = float.MinValue;
            foreach (var row in world.Query<ActiveFlag, LightComponent, Transform>().EnumerateWithEntities<ActiveFlag, LightComponent, Transform>())
            {
                ref readonly var lc = ref row.Item2.Value;
                if (!row.Item1.Value.Active || !lc.Enabled || lc.Mode != LightMode.Mixed || lc.Type != GameEditor.Framework.ECS.Components.LightType.Directional || !lc.CastsShadows) continue;
                if (lc.Intensity > sunIntensity) { sunIntensity = lc.Intensity; input.ShadowmaskLight = (int)world.GetSceneId(row.Entity); }
            }
            // the light probes (M8b): every active Light Probe Group's, in world space
            foreach (var row in world.Query<ActiveFlag, LightProbeGroup, Transform>().EnumerateWithEntities<ActiveFlag, LightProbeGroup, Transform>())
            {
                if (!row.Item1.Value.Active || row.Item2.Value.Positions is not { } local) continue;
                var m = Transform.GetWorldMatrix(world, row.Item3.Value);
                foreach (var p in local) input.Probes.Add(Vector3.Transform(p, m));
            }
            return input;
        }

        // A material's albedo (base colour × (1 - metallic), linear) and emission, each with its texture as a map the bake samples
        // per point (BAKED_GI_DESIGN.md §3.8); LightBaker keeps a point's albedo at most 0.9 a channel.
        private static BakeMaterial Material(string key, Dictionary<string, BakeTexture?> maps)
        {
            if (string.IsNullOrEmpty(key) || RenderingServer.Materials.GetByKey(key) is not PbrMaterial pm)
                return new BakeMaterial { Albedo = new Vector3(0.9f) };   // the engine's default white surface
            var c = new Vector3(pm.BaseColorFactor.X, pm.BaseColorFactor.Y, pm.BaseColorFactor.Z) * (1f - Math.Clamp(pm.MetallicFactor, 0f, 1f));
            return new BakeMaterial
            {
                Albedo = c, AlbedoMap = Map(pm.BaseColorMapPath, maps),
                Emission = pm.EmissiveFactor * pm.EmissiveStrength, EmissionMap = Map(pm.EmissiveMapPath, maps),
            };
        }

        // A colour texture (sRGB) as the bake's map, decoded once a bake; null when there is none or it cannot be read (the
        // material's colour alone then).
        private static unsafe BakeTexture? Map(string key, Dictionary<string, BakeTexture?> cache)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key, out var hit)) return hit;
            BakeTexture? map = null;
            try
            {
                if (ImageBytes(key) is { Length: > 0 } bytes)
                {
                    int w = 0, h = 0, ch = 0;
                    byte* px = Sokol.StbImage.stbi_load_csharp(in bytes[0], bytes.Length, ref w, ref h, ref ch, 4);
                    if (px != null)
                    {
                        map = BakeTexture.FromRgba8(px, w, h, srgb: true);
                        Sokol.StbImage.stbi_image_free_csharp(px);
                    }
                }
                if (map == null) Logger.Warning($"[LightBake] texture '{key}' could not be read: its material's colour alone");
            }
            catch (Exception e) { Logger.Warning($"[LightBake] texture '{key}': {e.Message}"); }
            cache[key] = map;
            return map;
        }

        // A texture's file bytes: an image file in Assets, or "<model>.glb#img<i>" / "<model>.gltf#img<i>" (the glTF
        // importer's key): that model's image i - in its binary chunk, a buffer, a file beside it or a data URI.
        private static byte[]? ImageBytes(string key)
        {
            static string Full(string k) => ConfigManager.HasProject ? Path.Combine(ConfigManager.ProjectFolder!, "Assets", k) : k;
            int at = key.LastIndexOf("#img", StringComparison.Ordinal);
            if (at < 0) { string p = Full(key); return File.Exists(p) ? File.ReadAllBytes(p) : null; }
            string file = Full(key[..at]);
            if (!int.TryParse(key.AsSpan(at + 4), out int index) || !File.Exists(file)) return null;
            byte[] data = File.ReadAllBytes(file);
            ReadOnlyMemory<byte> json, bin = default;
            if (data.Length >= 20 && BitConverter.ToUInt32(data, 0) == 0x46546C67)          // "glTF": a .glb
            {
                int jl = BitConverter.ToInt32(data, 12);
                json = new ReadOnlyMemory<byte>(data, 20, jl);
                int b = 20 + jl;
                if (b + 8 <= data.Length && BitConverter.ToUInt32(data, b + 4) == 0x004E4942) // "BIN\0"
                    bin = new ReadOnlyMemory<byte>(data, b + 8, Math.Min(BitConverter.ToInt32(data, b), data.Length - b - 8));
            }
            else json = data;
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("images", out var images) || index >= images.GetArrayLength()) return null;
            var img = images[index];
            string dir = Path.GetDirectoryName(file) ?? "";
            byte[]? Uri(string uri)
            {
                if (uri.StartsWith("data:", StringComparison.Ordinal)) return Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]);
                string p = Path.Combine(dir, System.Uri.UnescapeDataString(uri));
                return File.Exists(p) ? File.ReadAllBytes(p) : null;
            }
            if (img.TryGetProperty("uri", out var u)) return Uri(u.GetString() ?? "");
            if (!img.TryGetProperty("bufferView", out var bvi)) return null;
            var bv = root.GetProperty("bufferViews")[bvi.GetInt32()];
            int buffer = bv.TryGetProperty("buffer", out var bi) ? bi.GetInt32() : 0;
            int offset = bv.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0;
            int length = bv.GetProperty("byteLength").GetInt32();
            ReadOnlyMemory<byte> src;
            var buf = root.GetProperty("buffers")[buffer];
            if (buf.TryGetProperty("uri", out var bu)) src = Uri(bu.GetString() ?? "") ?? Array.Empty<byte>();
            else src = buffer == 0 ? bin : default;
            return offset + length <= src.Length ? src.Slice(offset, length).ToArray() : null;
        }

        /// <summary>The sky's linear radiance by world direction, as the environment lights the scene: the procedural sky
        /// (its display colours made linear) or, for a cubemap, its ambient harmonics (smooth: v1); × intensity, turned by
        /// its Y rotation; the ambient source Color: that colour everywhere; Disabled: none.</summary>
        public static Func<Vector3, Vector3> Sky(EnvironmentSettings env)
        {
            float energy = env.AmbientEnergy;
            switch (env.Ambient)
            {
                case AmbientSource.Color: { var c = env.AmbientColor * energy; return _ => c; }
                case AmbientSource.Disabled: return _ => Vector3.Zero;
            }
            float scale = env.Intensity * energy;
            var unrotate = Matrix4x4.CreateRotationY(-env.RotationDegrees * MathF.PI / 180f);
            if (env.Mode == EnvironmentMode.Procedural)
                return d => AmbientSH.SrgbToLinear(EnvironmentMap.SkyColor(Vector3.TransformNormal(d, unrotate))) * scale;
            var sh = RenderingServer.EnvironmentAmbient;
            if (sh == null) return _ => Vector3.Zero;
            return d => Vector3.Max(sh.Evaluate(Vector3.TransformNormal(d, unrotate)), Vector3.Zero) * scale;
        }
    }
}
