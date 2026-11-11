using System.Numerics;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene blocks of the 2D components. They go through the same door as a plugin's components
    // (RegisterComponentExtension), so loading, saving, prefab overrides and an editor's commands treat them
    // as they treat every other block.
    public static partial class SceneSerializer
    {
        static SceneSerializer()
        {
            RegisterComponentExtension("Sprite", WriteSprite, ReadSprite, static (w, e) => w.RemoveComponent<SpriteRenderer>(e));
            RegisterComponentExtension("SpriteAnim", WriteSpriteAnim, ReadSpriteAnim, static (w, e) => w.RemoveComponent<SpriteAnimator>(e));
            RegisterComponentExtension("Tilemap", WriteTilemap, ReadTilemap, static (w, e) => w.RemoveComponent<TilemapRenderer>(e));
        }

        private static string? WriteSprite(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<SpriteRenderer>(e, out var s)) return null;
            var sb = new StringBuilder();
            sb.Append("{\"Tex\":\"").Append(Esc(s.Texture ?? ""))
              .Append("\",\"Color\":[").Append(F(s.Color.X)).Append(',').Append(F(s.Color.Y)).Append(',')
                                        .Append(F(s.Color.Z)).Append(',').Append(F(s.Color.W))
              .Append("],\"FlipX\":").Append(s.FlipX ? "true" : "false")
              .Append(",\"FlipY\":").Append(s.FlipY ? "true" : "false")
              .Append(",\"Layer\":").Append(s.Layer)
              .Append(",\"Order\":").Append(s.Order)
              .Append(",\"Vis\":").Append(s.Visible ? "true" : "false").Append('}');
            return sb.ToString();
        }

        private static void ReadSprite(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var s = SpriteRenderer.Default;
            if (el.TryGetProperty("Tex", out var tex)) s.Texture = tex.GetString() ?? "";
            if (el.TryGetProperty("Color", out var c) && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() == 4)
                s.Color = new Vector4(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle(), c[3].GetSingle());
            s.FlipX = el.TryGetProperty("FlipX", out var fx) && fx.GetBoolean();
            s.FlipY = el.TryGetProperty("FlipY", out var fy) && fy.GetBoolean();
            if (el.TryGetProperty("Layer", out var layer)) s.Layer = layer.GetInt32();
            if (el.TryGetProperty("Order", out var order)) s.Order = order.GetInt32();
            s.Visible = !el.TryGetProperty("Vis", out var vis) || vis.GetBoolean();
            world.AddComponent(e, s);
        }

        private static string? WriteTilemap(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<TilemapRenderer>(e, out var t)) return null;
            return "{\"Map\":\"" + Esc(t.Map ?? "") + "\",\"Layer\":" + t.Layer + ",\"Order\":" + t.Order
                 + ",\"Color\":[" + F(t.Color.X) + "," + F(t.Color.Y) + "," + F(t.Color.Z) + "," + F(t.Color.W)
                 + "],\"Vis\":" + (t.Visible ? "true" : "false") + "}";
        }

        private static void ReadTilemap(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var t = TilemapRenderer.Default;
            if (el.TryGetProperty("Map", out var map)) t.Map = map.GetString() ?? "";
            if (el.TryGetProperty("Layer", out var layer)) t.Layer = layer.GetInt32();
            if (el.TryGetProperty("Order", out var order)) t.Order = order.GetInt32();
            if (el.TryGetProperty("Color", out var c) && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() == 4)
                t.Color = new Vector4(c[0].GetSingle(), c[1].GetSingle(), c[2].GetSingle(), c[3].GetSingle());
            t.Visible = !el.TryGetProperty("Vis", out var vis) || vis.GetBoolean();
            world.AddComponent(e, t);
        }

        // What a running game keeps in the component (the frame shown, the time into the clip) is not written.
        private static string? WriteSpriteAnim(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<SpriteAnimator>(e, out var a)) return null;
            return "{\"Clip\":\"" + Esc(a.Clip ?? "") + "\",\"Speed\":" + F(a.Speed) + ",\"Playing\":" + (a.Playing ? "true" : "false") + "}";
        }

        private static void ReadSpriteAnim(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var a = SpriteAnimator.Default;
            if (el.TryGetProperty("Clip", out var clip)) a.Clip = clip.GetString() ?? "";
            if (el.TryGetProperty("Speed", out var speed) && speed.ValueKind == JsonValueKind.Number) a.Speed = speed.GetSingle();
            a.Playing = !el.TryGetProperty("Playing", out var playing) || playing.ValueKind != JsonValueKind.False;
            world.AddComponent(e, a);
        }
    }
}
