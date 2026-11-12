using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "Kart": the handling numbers and whether the player drives it. Its inputs and what the
    // controller found are the game's, each frame, and are not written.
    public static partial class SceneSerializer
    {
        private static void RegisterKart()
        {
            RegisterComponentExtension("Kart", WriteKart, ReadKart, static (w, e) => w.RemoveComponent<KartComponent>(e));
        }

        private static string? WriteKart(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<KartComponent>(e, out var k)) return null;
            return "{\"TopSpeed\":" + F(k.TopSpeed) + ",\"ReverseSpeed\":" + F(k.ReverseSpeed) + ",\"Acceleration\":" + F(k.Acceleration)
                 + ",\"Braking\":" + F(k.Braking) + ",\"Coasting\":" + F(k.Coasting) + ",\"TurnRate\":" + F(k.TurnRate)
                 + ",\"TurnFullSpeed\":" + F(k.TurnFullSpeed) + ",\"Grip\":" + F(k.Grip) + ",\"WheelSpan\":" + Pair(k.WheelSpan)
                 + ",\"WheelHeight\":" + F(k.WheelHeight) + ",\"Suspension\":" + F(k.Suspension) + ",\"Spring\":" + F(k.Spring)
                 + ",\"Damper\":" + F(k.Damper) + ",\"Player\":" + (k.Player ? "true" : "false") + "}";
        }

        private static void ReadKart(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var k = KartComponent.Default;
            k.TopSpeed = Number(el, "TopSpeed", k.TopSpeed);
            k.ReverseSpeed = Number(el, "ReverseSpeed", k.ReverseSpeed);
            k.Acceleration = Number(el, "Acceleration", k.Acceleration);
            k.Braking = Number(el, "Braking", k.Braking);
            k.Coasting = Number(el, "Coasting", k.Coasting);
            k.TurnRate = Number(el, "TurnRate", k.TurnRate);
            k.TurnFullSpeed = Number(el, "TurnFullSpeed", k.TurnFullSpeed);
            k.Grip = Number(el, "Grip", k.Grip);
            Pair(el, "WheelSpan", ref k.WheelSpan);
            k.WheelHeight = Number(el, "WheelHeight", k.WheelHeight);
            k.Suspension = Number(el, "Suspension", k.Suspension);
            k.Spring = Number(el, "Spring", k.Spring);
            k.Damper = Number(el, "Damper", k.Damper);
            k.Player = el.TryGetProperty("Player", out var p) && p.ValueKind == JsonValueKind.True;
            world.AddComponent(e, k);
        }
    }
}
