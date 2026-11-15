using System;
using System.Numerics;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "ReflectionProbe" (M8c).
    public static partial class SceneSerializer
    {
        private static void RegisterReflectionProbe()
        {
            RegisterComponentExtension("ReflectionProbe", WriteReflectionProbe, ReadReflectionProbe, static (w, e) => w.RemoveComponent<ReflectionProbe>(e));
        }

        private static string? WriteReflectionProbe(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<ReflectionProbe>(e, out var r)) return null;
            return "{\"Size\":" + V3(r.Size) + ",\"Offset\":" + V3(r.Offset) + ",\"Box\":" + (r.BoxProjection ? "true" : "false")
                 + ",\"Int\":" + F(r.Intensity) + ",\"Imp\":" + F(r.Importance) + ",\"Res\":" + r.Resolution + "}";
        }

        private static void ReadReflectionProbe(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var r = ReflectionProbe.Default;
            if (el.TryGetProperty("Size", out _)) r.Size = Vector3.Max(RV3(el, "Size"), new Vector3(0.01f));
            if (el.TryGetProperty("Offset", out _)) r.Offset = RV3(el, "Offset");
            r.BoxProjection = el.TryGetProperty("Box", out var b) && b.ValueKind == JsonValueKind.True;
            r.Intensity = MathF.Max(0f, Number(el, "Int", r.Intensity));
            r.Importance = Number(el, "Imp", r.Importance);
            r.Resolution = Math.Clamp((int)Number(el, "Res", r.Resolution), 16, 1024);
            world.AddComponent(e, r);
        }
    }
}
