using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "VaultMarker" (Phase H E2): where a character may vault, and the one fixed move it makes.
    public static partial class SceneSerializer
    {
        private static void RegisterVaultMarker()
        {
            RegisterComponentExtension("VaultMarker", WriteVaultMarker, ReadVaultMarker, static (w, e) => w.RemoveComponent<VaultMarker>(e));
        }

        private static string? WriteVaultMarker(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<VaultMarker>(e, out var m)) return null;
            return "{\"Size\":[" + F(m.Size.X) + "," + F(m.Size.Y) + "," + F(m.Size.Z) + "],\"Distance\":" + F(m.Distance) + ",\"Height\":" + F(m.Height)
                 + ",\"Seconds\":" + F(m.Seconds) + ",\"MaxAngle\":" + F(m.MaxAngle) + "}";
        }

        private static void ReadVaultMarker(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var m = VaultMarker.Default;
            if (el.TryGetProperty("Size", out var s) && s.ValueKind == JsonValueKind.Array && s.GetArrayLength() == 3)
                m.Size = new System.Numerics.Vector3(MathF.Max(0f, s[0].GetSingle()), MathF.Max(0f, s[1].GetSingle()), MathF.Max(0f, s[2].GetSingle()));
            m.Distance = MathF.Max(0f, Number(el, "Distance", m.Distance));
            m.Height = MathF.Max(0f, Number(el, "Height", m.Height));
            m.Seconds = MathF.Max(0.05f, Number(el, "Seconds", m.Seconds));
            m.MaxAngle = Math.Clamp(Number(el, "MaxAngle", m.MaxAngle), 0f, 180f);
            world.AddComponent(e, m);
        }
    }
}
