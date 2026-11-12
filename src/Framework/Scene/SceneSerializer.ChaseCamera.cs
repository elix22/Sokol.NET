using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "ChaseCamera": what a camera follows and how.
    public static partial class SceneSerializer
    {
        private static void RegisterChaseCamera()
        {
            RegisterComponentExtension("ChaseCamera", WriteChaseCamera, ReadChaseCamera, static (w, e) => w.RemoveComponent<ChaseCameraComponent>(e));
        }

        private static string? WriteChaseCamera(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<ChaseCameraComponent>(e, out var c)) return null;
            return "{\"Target\":\"" + Esc(c.Target ?? "") + "\",\"Distance\":" + F(c.Distance) + ",\"Height\":" + F(c.Height) + ",\"LookAhead\":" + F(c.LookAhead)
                 + ",\"Spring\":" + F(c.Spring) + ",\"LookBack\":\"" + Esc(c.LookBack ?? "") + "\"}";
        }

        private static void ReadChaseCamera(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var c = ChaseCameraComponent.Default;
            c.Target = Text(el, "Target", c.Target);
            c.Distance = Number(el, "Distance", c.Distance);
            c.Height = Number(el, "Height", c.Height);
            c.LookAhead = Number(el, "LookAhead", c.LookAhead);
            c.Spring = Number(el, "Spring", c.Spring);
            c.LookBack = Text(el, "LookBack", c.LookBack);
            world.AddComponent(e, c);
        }
    }
}
