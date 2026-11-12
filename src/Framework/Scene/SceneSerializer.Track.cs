using System;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "Track": the key of the track's file.
    public static partial class SceneSerializer
    {
        private static void RegisterTrack()
        {
            RegisterComponentExtension("Track", WriteTrack, ReadTrack, static (w, e) => w.RemoveComponent<TrackComponent>(e));
        }

        private static string? WriteTrack(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<TrackComponent>(e, out var t)) return null;
            return "{\"Key\":\"" + Esc(t.Key ?? "") + "\"}";
        }

        private static void ReadTrack(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var t = TrackComponent.Default;
            if (el.TryGetProperty("Key", out var k) && k.ValueKind == JsonValueKind.String) t.Key = k.GetString() ?? "";
            world.AddComponent(e, t);
        }
    }
}
