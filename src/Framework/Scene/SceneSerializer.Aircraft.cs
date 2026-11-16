using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "Aircraft" (Phase H E17): an arcade aircraft's rates and speeds. Its input and its flight now are not saved.
    public static partial class SceneSerializer
    {
        private static void RegisterAircraft()
        {
            RegisterComponentExtension("Aircraft", WriteAircraft, ReadAircraft, static (w, e) => w.RemoveComponent<AircraftComponent>(e));
        }

        private static string? WriteAircraft(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<AircraftComponent>(e, out var a)) return null;
            return "{\"Cruise\":" + F(a.CruiseSpeed) + ",\"Fast\":" + F(a.FastSpeed) + ",\"Accel\":" + F(a.Acceleration) + ",\"TurnRate\":" + F(a.TurnRate)
                 + ",\"ClimbRate\":" + F(a.ClimbRate) + ",\"Ceiling\":" + F(a.Ceiling) + ",\"Bank\":" + F(a.BankAngle) + ",\"Pitch\":" + F(a.PitchAngle)
                 + ",\"Tilt\":" + F(a.ForwardTilt) + ",\"LevelRate\":" + F(a.LevelRate) + ",\"Piloted\":" + (a.Piloted ? "true" : "false")
                 + ",\"Player\":" + (a.Player ? "true" : "false") + ",\"FastButton\":\"" + Esc(a.FastButton ?? "") + "\"}";
        }

        private static void ReadAircraft(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var a = AircraftComponent.Default;
            a.CruiseSpeed = MathF.Max(0f, Number(el, "Cruise", a.CruiseSpeed));
            a.FastSpeed = MathF.Max(0f, Number(el, "Fast", a.FastSpeed));
            a.Acceleration = MathF.Max(0f, Number(el, "Accel", a.Acceleration));
            a.TurnRate = MathF.Max(0f, Number(el, "TurnRate", a.TurnRate));
            a.ClimbRate = MathF.Max(0f, Number(el, "ClimbRate", a.ClimbRate));
            a.Ceiling = Number(el, "Ceiling", a.Ceiling);
            a.BankAngle = Number(el, "Bank", a.BankAngle);
            a.PitchAngle = Number(el, "Pitch", a.PitchAngle);
            a.ForwardTilt = Number(el, "Tilt", a.ForwardTilt);
            a.LevelRate = MathF.Max(0f, Number(el, "LevelRate", a.LevelRate));
            a.Piloted = el.TryGetProperty("Piloted", out var p) && p.ValueKind == JsonValueKind.True;
            a.Player = !el.TryGetProperty("Player", out var pl) || pl.ValueKind != JsonValueKind.False;
            a.FastButton = Text(el, "FastButton", a.FastButton);
            world.AddComponent(e, a);
        }
    }
}
