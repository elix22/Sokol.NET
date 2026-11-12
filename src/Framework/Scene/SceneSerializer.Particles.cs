using System;
using System.Numerics;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "Particles": an emitter of particles in the world (docs/H5_DESIGN.md §16).
    public static partial class SceneSerializer
    {
        private static void RegisterParticles()
        {
            RegisterComponentExtension("Particles", WriteParticles, ReadParticles, static (w, e) => w.RemoveComponent<ParticlesComponent>(e));
        }

        private static string? WriteParticles(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<ParticlesComponent>(e, out var p)) return null;
            return "{\"Texture\":\"" + Esc(p.Texture ?? "") + "\",\"Emitting\":" + (p.Emitting ? "true" : "false") + ",\"Rate\":" + F(p.Rate)
                 + ",\"Burst\":" + p.Burst + ",\"Life\":" + Pair(p.Life) + ",\"Speed\":" + Pair(p.Speed)
                 + ",\"Direction\":[" + F(p.Direction.X) + "," + F(p.Direction.Y) + "," + F(p.Direction.Z) + "]"
                 + ",\"Spread\":" + F(p.Spread) + ",\"Gravity\":" + F(p.Gravity) + ",\"Drag\":" + F(p.Drag) + ",\"Size\":" + Pair(p.Size)
                 + ",\"ColorStart\":" + Four(p.ColorStart) + ",\"ColorEnd\":" + Four(p.ColorEnd)
                 + ",\"World\":" + (p.World ? "true" : "false") + ",\"Max\":" + p.Max + "}";
        }

        private static void ReadParticles(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var p = ParticlesComponent.Default;
            p.Texture = Text(el, "Texture", p.Texture);
            if (el.TryGetProperty("Emitting", out var em)) p.Emitting = em.ValueKind == JsonValueKind.True;
            p.Rate = Number(el, "Rate", p.Rate);
            p.Burst = (int)Number(el, "Burst", p.Burst);
            Pair(el, "Life", ref p.Life);
            Pair(el, "Speed", ref p.Speed);
            if (el.TryGetProperty("Direction", out var d) && d.ValueKind == JsonValueKind.Array && d.GetArrayLength() == 3)
                p.Direction = new Vector3(d[0].GetSingle(), d[1].GetSingle(), d[2].GetSingle());
            p.Spread = Number(el, "Spread", p.Spread);
            p.Gravity = Number(el, "Gravity", p.Gravity);
            p.Drag = Number(el, "Drag", p.Drag);
            Pair(el, "Size", ref p.Size);
            Four(el, "ColorStart", ref p.ColorStart);
            Four(el, "ColorEnd", ref p.ColorEnd);
            if (el.TryGetProperty("World", out var wd)) p.World = wd.ValueKind == JsonValueKind.True;
            p.Max = Math.Clamp((int)Number(el, "Max", p.Max), 1, 4096);
            world.AddComponent(e, p);
        }
    }
}
