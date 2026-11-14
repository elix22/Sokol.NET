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
            var textureAverage = new Dictionary<string, Vector3>(StringComparer.Ordinal);

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
                    mats[s] = Material(key, textureAverage);
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
            return input;
        }

        // A material's albedo (base colour × its texture's average, linear; at most 0.9 so bounces converge) and emission.
        private static BakeMaterial Material(string key, Dictionary<string, Vector3> textureAverage)
        {
            if (string.IsNullOrEmpty(key) || RenderingServer.Materials.GetByKey(key) is not PbrMaterial pm)
                return new BakeMaterial { Albedo = new Vector3(0.9f) };   // the engine's default white surface
            var c = new Vector3(pm.BaseColorFactor.X, pm.BaseColorFactor.Y, pm.BaseColorFactor.Z) * (1f - Math.Clamp(pm.MetallicFactor, 0f, 1f));
            if (!string.IsNullOrEmpty(pm.BaseColorMapPath)) c *= Average(pm.BaseColorMapPath, textureAverage);
            float max = MathF.Max(c.X, MathF.Max(c.Y, c.Z));
            if (max > 0.9f) c *= 0.9f / max;
            var emission = pm.EmissiveFactor * pm.EmissiveStrength;
            if (!string.IsNullOrEmpty(pm.EmissiveMapPath)) emission *= Average(pm.EmissiveMapPath, textureAverage);
            return new BakeMaterial { Albedo = c, Emission = emission };
        }

        // A texture's average colour, linear (its file decoded once; white when it cannot be read).
        private static unsafe Vector3 Average(string key, Dictionary<string, Vector3> cache)
        {
            if (cache.TryGetValue(key, out var hit)) return hit;
            var avg = Vector3.One;
            try
            {
                string path = ConfigManager.HasProject ? Path.Combine(ConfigManager.ProjectFolder!, "Assets", key) : key;
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    int w = 0, h = 0, ch = 0;
                    byte* px = Sokol.StbImage.stbi_load_csharp(in bytes[0], bytes.Length, ref w, ref h, ref ch, 4);
                    if (px != null)
                    {
                        double r = 0, g = 0, b = 0; long n = (long)w * h;
                        int step = Math.Max(1, (int)(n / 65536));
                        long count = 0;
                        for (long i = 0; i < n; i += step, count++)
                        {
                            r += AmbientSH.SrgbToLinear(px[i * 4] / 255f); g += AmbientSH.SrgbToLinear(px[i * 4 + 1] / 255f); b += AmbientSH.SrgbToLinear(px[i * 4 + 2] / 255f);
                        }
                        Sokol.StbImage.stbi_image_free_csharp(px);
                        if (count > 0) avg = new Vector3((float)(r / count), (float)(g / count), (float)(b / count));
                    }
                }
            }
            catch (Exception e) { Logger.Warning($"[LightBake] texture '{key}': {e.Message}"); }
            cache[key] = avg;
            return avg;
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
