using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "ItemBox": how long a box stays hidden after a racer takes its item. (Puddles and bolts are the race's, not written.)
    public static partial class SceneSerializer
    {
        private static void RegisterItems()
        {
            RegisterComponentExtension("ItemBox", WriteItemBox, ReadItemBox, static (w, e) => w.RemoveComponent<ItemBoxComponent>(e));
        }

        private static string? WriteItemBox(ECSWorld world, Frent.Entity e)
            => world.TryGetComponent<ItemBoxComponent>(e, out var b) ? "{\"Respawn\":" + F(b.Respawn) + "}" : null;

        private static void ReadItemBox(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var b = ItemBoxComponent.Default;
            b.Respawn = Number(el, "Respawn", b.Respawn);
            world.AddComponent(e, b);
        }
    }
}
