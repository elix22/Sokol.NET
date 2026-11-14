using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "LightProbeGroup" (M8b): its probes' positions, local to the entity.
    public static partial class SceneSerializer
    {
        private static void RegisterLightProbes()
        {
            RegisterComponentExtension("LightProbeGroup", WriteLightProbes, ReadLightProbes, static (w, e) => w.RemoveComponent<LightProbeGroup>(e));
        }

        private static string? WriteLightProbes(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<LightProbeGroup>(e, out var g)) return null;
            var sb = new StringBuilder("{\"P\":[");
            if (g.Positions != null)
                for (int i = 0; i < g.Positions.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var p = g.Positions[i];
                    sb.Append('[').Append(F(p.X)).Append(',').Append(F(p.Y)).Append(',').Append(F(p.Z)).Append(']');
                }
            return sb.Append("]}").ToString();
        }

        private static void ReadLightProbes(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var list = new List<Vector3>();
            if (el.TryGetProperty("P", out var ps) && ps.ValueKind == JsonValueKind.Array)
                foreach (var p in ps.EnumerateArray())
                    if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() == 3)
                        list.Add(new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()));
            world.AddComponent(e, new LightProbeGroup { Positions = list });
        }
    }
}
