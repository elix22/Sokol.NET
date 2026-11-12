using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "Driver": how the computer drives a kart (skill, look-ahead, mistakes, catch-up).
    public static partial class SceneSerializer
    {
        private static void RegisterDriver()
        {
            RegisterComponentExtension("Driver", WriteDriver, ReadDriver, static (w, e) => w.RemoveComponent<DriverComponent>(e));
        }

        private static string? WriteDriver(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<DriverComponent>(e, out var d)) return null;
            return "{\"Skill\":" + F(d.Skill) + ",\"LookAhead\":" + F(d.LookAhead) + ",\"Mistakes\":" + F(d.Mistakes) + ",\"WideBy\":" + F(d.WideBy)
                 + ",\"CatchUp\":" + (d.CatchUp ? "true" : "false") + "}";
        }

        private static void ReadDriver(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var d = DriverComponent.Default;
            d.Skill = Number(el, "Skill", d.Skill);
            d.LookAhead = Number(el, "LookAhead", d.LookAhead);
            d.Mistakes = Number(el, "Mistakes", d.Mistakes);
            d.WideBy = Number(el, "WideBy", d.WideBy);
            d.CatchUp = el.TryGetProperty("CatchUp", out var c) && c.ValueKind == JsonValueKind.True;
            world.AddComponent(e, d);
        }
    }
}
