using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Renderer.Server.Lod;

namespace GameEditor.Framework.Scene
{
    // The scene block "LodGroup" (Phase H E6): its levels, nearest first - the coverage at which each is drawn, its margin and
    // its mesh ("" = the entity's own). Below the last level's coverage the entity is not drawn.
    public static partial class SceneSerializer
    {
        private static void RegisterLodGroup()
        {
            RegisterComponentExtension("LodGroup", WriteLodGroup, ReadLodGroup, static (w, e) => w.RemoveComponent<LodGroup>(e));
        }

        private static string? WriteLodGroup(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<LodGroup>(e, out var g)) return null;
            var sb = new StringBuilder("{\"Levels\":[");
            var levels = g.Levels ?? Array.Empty<LodLevel>();
            for (int i = 0; i < levels.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"Coverage\":").Append(F(levels[i].ScreenCoverageThreshold))
                  .Append(",\"Margin\":").Append(F(levels[i].HysteresisMargin))
                  .Append(",\"Mesh\":\"").Append(Esc(levels[i].Mesh ?? "")).Append("\"}");
            }
            return sb.Append("]}").ToString();
        }

        private static void ReadLodGroup(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var levels = new List<LodLevel>();
            if (el.TryGetProperty("Levels", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var l in arr.EnumerateArray())
                {
                    float coverage = MathF.Max(0f, Number(l, "Coverage", 0.1f));
                    float margin = MathF.Max(0f, Number(l, "Margin", 0.01f));
                    levels.Add(new LodLevel { ScreenCoverageThreshold = coverage, HysteresisMargin = margin, Mesh = Text(l, "Mesh", "") });
                }
            levels.Sort((a, b) => b.ScreenCoverageThreshold.CompareTo(a.ScreenCoverageThreshold));   // nearest first
            // A margin wider than the gap to the next level would pin the entity there: kept under half the gap.
            for (int i = 0; i < levels.Count; i++)
            {
                float next = i + 1 < levels.Count ? levels[i + 1].ScreenCoverageThreshold : 0f;
                var l = levels[i];
                l.HysteresisMargin = MathF.Min(l.HysteresisMargin, MathF.Max(0f, (l.ScreenCoverageThreshold - next) * 0.5f));
                levels[i] = l;
            }
            world.AddComponent(e, new LodGroup { Levels = levels.ToArray() });
        }
    }
}
