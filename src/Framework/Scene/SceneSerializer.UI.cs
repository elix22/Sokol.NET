using System.Numerics;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene blocks of a game's UI: "UI" (where an element is) and "UIText", "UIImage", "UIButton", "UIPanel"
    // (what it is). Registered with the 2D blocks.
    public static partial class SceneSerializer
    {
        private static void RegisterUI()
        {
            RegisterComponentExtension("UI", WriteUI, ReadUI, static (w, e) => w.RemoveComponent<UIElement>(e));
            RegisterComponentExtension("UIText", WriteUIText, ReadUIText, static (w, e) => w.RemoveComponent<UIText>(e));
            RegisterComponentExtension("UIImage", WriteUIImage, ReadUIImage, static (w, e) => w.RemoveComponent<UIPicture>(e));
            RegisterComponentExtension("UIButton", WriteUIButton, ReadUIButton, static (w, e) => w.RemoveComponent<UIButton>(e));
            RegisterComponentExtension("UIMiniMap", WriteUIMiniMap, ReadUIMiniMap, static (w, e) => w.RemoveComponent<UIMiniMap>(e));
            RegisterComponentExtension("UIPanel", WriteUIPanel, ReadUIPanel, static (w, e) => w.RemoveComponent<UIPanel>(e));
            RegisterComponentExtension("UIStick", WriteUIStick, ReadUIStick, static (w, e) => w.RemoveComponent<UIStick>(e));
            RegisterComponentExtension("UILayout", WriteUILayout, ReadUILayout, static (w, e) => w.RemoveComponent<UILayout>(e));
        }

        private static string Pair(Vector2 v) => "[" + F(v.X) + "," + F(v.Y) + "]";
        private static string Four(Vector4 v) => "[" + F(v.X) + "," + F(v.Y) + "," + F(v.Z) + "," + F(v.W) + "]";

        private static bool Pair(JsonElement el, string name, ref Vector2 into)
        {
            if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != 2) return false;
            into = new Vector2(v[0].GetSingle(), v[1].GetSingle());
            return true;
        }

        private static bool Four(JsonElement el, string name, ref Vector4 into)
        {
            if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != 4) return false;
            into = new Vector4(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle(), v[3].GetSingle());
            return true;
        }

        private static string Text(JsonElement el, string name, string fallback)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

        private static float Number(JsonElement el, string name, float fallback)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : fallback;

        private static string? WriteUI(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<UIElement>(e, out var u)) return null;
            return "{\"Anchor\":\"" + u.Anchor + "\",\"Offset\":" + Pair(u.Offset) + ",\"Size\":" + Pair(u.Size) + ",\"Order\":" + u.Order
                 + ",\"Vis\":" + (u.Visible ? "true" : "false") + "}";
        }

        private static void ReadUI(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var u = UIElement.Default;
            if (Enum.TryParse(Text(el, "Anchor", ""), true, out UIAnchor anchor)) u.Anchor = anchor;
            Pair(el, "Offset", ref u.Offset);
            Pair(el, "Size", ref u.Size);
            if (el.TryGetProperty("Order", out var order) && order.ValueKind == JsonValueKind.Number) u.Order = order.GetInt32();
            u.Visible = !el.TryGetProperty("Vis", out var vis) || vis.ValueKind != JsonValueKind.False;
            world.AddComponent(e, u);
        }

        private static string? WriteUIText(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<UIText>(e, out var t)) return null;
            return "{\"Text\":\"" + Esc(t.Text ?? "") + "\",\"Font\":\"" + Esc(t.Font ?? "") + "\",\"FontSize\":" + F(t.FontSize) + ",\"Color\":" + Four(t.Color)
                 + ",\"Align\":\"" + t.Align + "\",\"Wrap\":" + (t.Wrap ? "true" : "false") + KeyOf(t.Key) + "}";
        }

        private static void ReadUIText(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var t = UIText.Default;
            t.Text = Text(el, "Text", t.Text);
            t.Font = Text(el, "Font", t.Font);
            t.FontSize = Number(el, "FontSize", t.FontSize);
            Four(el, "Color", ref t.Color);
            if (Enum.TryParse(Text(el, "Align", ""), true, out UIAlign align)) t.Align = align;
            t.Wrap = el.TryGetProperty("Wrap", out var wrap) && wrap.ValueKind == JsonValueKind.True;
            t.Key = Text(el, "Key", "");
            world.AddComponent(e, t);
        }

        private static string? WriteUIStick(ECSWorld world, Frent.Entity e)
            => world.TryGetComponent<UIStick>(e, out var s)
                ? "{\"X\":\"" + Esc(s.X ?? "") + "\",\"Y\":\"" + Esc(s.Y ?? "") + "\",\"Dead\":" + F(s.DeadZone) + ",\"Color\":" + Four(s.Color) + "}" : null;

        private static void ReadUIStick(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var s = UIStick.Default;
            s.X = Text(el, "X", s.X);
            s.Y = Text(el, "Y", s.Y);
            s.DeadZone = Math.Clamp(Number(el, "Dead", s.DeadZone), 0f, 0.9f);
            Four(el, "Color", ref s.Color);
            world.AddComponent(e, s);
        }

        // A text table's key, written only when there is one (scenes made before keys stay as they were).
        private static string KeyOf(string? key) => string.IsNullOrEmpty(key) ? "" : ",\"Key\":\"" + Esc(key) + "\"";

        private static string? WriteUIImage(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<UIPicture>(e, out var p)) return null;
            return "{\"Tex\":\"" + Esc(p.Texture ?? "") + "\",\"Color\":" + Four(p.Color) + "}";
        }

        private static void ReadUIImage(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var p = UIPicture.Default;
            p.Texture = Text(el, "Tex", p.Texture);
            Four(el, "Color", ref p.Color);
            world.AddComponent(e, p);
        }

        private static string? WriteUIButton(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<UIButton>(e, out var b)) return null;
            return "{\"Text\":\"" + Esc(b.Text ?? "") + "\",\"Font\":\"" + Esc(b.Font ?? "") + "\",\"FontSize\":" + F(b.FontSize) + ",\"Color\":" + Four(b.Color)
                 + ",\"TextColor\":" + Four(b.TextColor) + ",\"Radius\":" + F(b.Radius)
                 + ",\"Holds\":\"" + Esc(b.Holds ?? "") + "\"" + KeyOf(b.Key) + "}";
        }

        private static string? WriteUIMiniMap(ECSWorld world, Frent.Entity e)
            => world.TryGetComponent<UIMiniMap>(e, out var m)
                ? "{\"Road\":" + Four(m.Road) + ",\"Kart\":" + Four(m.Kart) + ",\"Player\":" + Four(m.Player) + ",\"Line\":" + F(m.Line) + "}" : null;

        private static void ReadUIMiniMap(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var m = UIMiniMap.Default;
            Four(el, "Road", ref m.Road);
            Four(el, "Kart", ref m.Kart);
            Four(el, "Player", ref m.Player);
            m.Line = Number(el, "Line", m.Line);
            world.AddComponent(e, m);
        }

        private static void ReadUIButton(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var b = UIButton.Default;
            b.Text = Text(el, "Text", b.Text);
            b.Font = Text(el, "Font", b.Font);
            b.FontSize = Number(el, "FontSize", b.FontSize);
            Four(el, "Color", ref b.Color);
            Four(el, "TextColor", ref b.TextColor);
            b.Radius = Number(el, "Radius", b.Radius);
            b.Holds = Text(el, "Holds", "");
            b.Key = Text(el, "Key", "");
            world.AddComponent(e, b);
        }

        private static string? WriteUIPanel(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<UIPanel>(e, out var p)) return null;
            return "{\"Color\":" + Four(p.Color) + ",\"Radius\":" + F(p.Radius) + "}";
        }

        private static void ReadUIPanel(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var p = UIPanel.Default;
            Four(el, "Color", ref p.Color);
            p.Radius = Number(el, "Radius", p.Radius);
            world.AddComponent(e, p);
        }

        private static string? WriteUILayout(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<UILayout>(e, out var l)) return null;
            return "{\"Dir\":\"" + l.Dir + "\",\"Spacing\":" + F(l.Spacing) + ",\"Pad\":" + Four(l.Padding) + ",\"Align\":\"" + l.Align
                 + "\",\"Fit\":" + (l.Fit ? "true" : "false") + "}";
        }

        private static void ReadUILayout(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var l = UILayout.Default;
            if (Enum.TryParse(Text(el, "Dir", ""), true, out UILayoutDir dir)) l.Dir = dir;
            l.Spacing = Number(el, "Spacing", l.Spacing);
            Four(el, "Pad", ref l.Padding);
            if (Enum.TryParse(Text(el, "Align", ""), true, out UILayoutAlign align)) l.Align = align;
            l.Fit = el.TryGetProperty("Fit", out var fit) && fit.ValueKind == JsonValueKind.True;
            world.AddComponent(e, l);
        }
    }
}
