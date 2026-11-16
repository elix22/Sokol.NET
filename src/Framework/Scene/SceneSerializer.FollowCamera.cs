using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene blocks "FollowCamera" and "CameraZone" (Phase H E3): a third-person camera's boom, and a box with its own view.
    public static partial class SceneSerializer
    {
        private static void RegisterFollowCamera()
        {
            RegisterComponentExtension("FollowCamera", WriteFollowCamera, ReadFollowCamera, static (w, e) => w.RemoveComponent<FollowCameraComponent>(e));
            RegisterComponentExtension("CameraZone", WriteCameraZone, ReadCameraZone, static (w, e) => w.RemoveComponent<CameraZone>(e));
        }

        private static string? WriteFollowCamera(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<FollowCameraComponent>(e, out var c)) return null;
            return "{\"Target\":\"" + Esc(c.Target ?? "") + "\",\"Distance\":" + F(c.Distance) + ",\"Height\":" + F(c.Height) + ",\"Pitch\":" + F(c.Pitch)
                 + ",\"Fov\":" + F(c.Fov) + ",\"Smoothing\":" + F(c.Smoothing) + ",\"Radius\":" + F(c.Radius) + ",\"MinDistance\":" + F(c.MinDistance)
                 + ",\"DistanceScale\":" + F(c.DistanceScale) + "}";
        }

        private static void ReadFollowCamera(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var c = FollowCameraComponent.Default;
            c.Target = Text(el, "Target", c.Target);
            c.Distance = MathF.Max(0f, Number(el, "Distance", c.Distance));
            c.Height = Number(el, "Height", c.Height);
            c.Pitch = Math.Clamp(Number(el, "Pitch", c.Pitch), -89f, 89f);
            c.Fov = Math.Clamp(Number(el, "Fov", c.Fov), 1f, 170f);
            c.Smoothing = MathF.Max(0f, Number(el, "Smoothing", c.Smoothing));
            c.Radius = MathF.Max(0f, Number(el, "Radius", c.Radius));
            c.MinDistance = MathF.Max(0f, Number(el, "MinDistance", c.MinDistance));
            c.DistanceScale = MathF.Max(0.01f, Number(el, "DistanceScale", c.DistanceScale));
            world.AddComponent(e, c);
        }

        private static string? WriteCameraZone(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<CameraZone>(e, out var z)) return null;
            return "{\"Size\":[" + F(z.Size.X) + "," + F(z.Size.Y) + "," + F(z.Size.Z) + "],\"Distance\":" + F(z.Distance) + ",\"Height\":" + F(z.Height)
                 + ",\"Pitch\":" + F(z.Pitch) + ",\"Fov\":" + F(z.Fov) + ",\"FixedHeading\":" + (z.FixedHeading ? "true" : "false") + "}";
        }

        private static void ReadCameraZone(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var z = CameraZone.Default;
            if (el.TryGetProperty("Size", out var s) && s.ValueKind == JsonValueKind.Array && s.GetArrayLength() == 3)
                z.Size = new System.Numerics.Vector3(MathF.Max(0f, s[0].GetSingle()), MathF.Max(0f, s[1].GetSingle()), MathF.Max(0f, s[2].GetSingle()));
            z.Distance = MathF.Max(0f, Number(el, "Distance", z.Distance));
            z.Height = Number(el, "Height", z.Height);
            z.Pitch = Math.Clamp(Number(el, "Pitch", z.Pitch), -89f, 89f);
            z.Fov = Math.Clamp(Number(el, "Fov", z.Fov), 1f, 170f);
            z.FixedHeading = !el.TryGetProperty("FixedHeading", out var fh) || fh.ValueKind != JsonValueKind.False;
            world.AddComponent(e, z);
        }
    }
}
