using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "WindZone" (G8.8 Q7): wind for the particles' External Forces.
    public static partial class SceneSerializer
    {
        private static void RegisterWindZone()
        {
            RegisterComponentExtension("WindZone", WriteWindZone, ReadWindZone, static (w, e) => w.RemoveComponent<WindZone>(e));
        }

        private static string? WriteWindZone(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<WindZone>(e, out var z)) return null;
            return "{\"Mode\":\"" + (z.Mode == WindZoneMode.Spherical ? "Spherical" : "Directional") + "\",\"Radius\":" + F(z.Radius) + ",\"Main\":" + F(z.Main)
                 + ",\"Turbulence\":" + F(z.Turbulence) + ",\"PulseMagnitude\":" + F(z.PulseMagnitude) + ",\"PulseFrequency\":" + F(z.PulseFrequency) + "}";
        }

        private static void ReadWindZone(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var z = WindZone.Default;
            z.Mode = Text(el, "Mode", "Directional") == "Spherical" ? WindZoneMode.Spherical : WindZoneMode.Directional;
            z.Radius = MathF.Max(0f, Number(el, "Radius", z.Radius));
            z.Main = Number(el, "Main", z.Main);
            z.Turbulence = MathF.Max(0f, Number(el, "Turbulence", z.Turbulence));
            z.PulseMagnitude = MathF.Max(0f, Number(el, "PulseMagnitude", z.PulseMagnitude));
            z.PulseFrequency = MathF.Max(0f, Number(el, "PulseFrequency", z.PulseFrequency));
            world.AddComponent(e, z);
        }
    }
}
