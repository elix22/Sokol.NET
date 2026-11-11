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
    }
}
