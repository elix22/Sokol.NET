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
            RegisterComponentExtension("Body2D", WriteBody2D, ReadBody2D, static (w, e) => w.RemoveComponent<Body2D>(e));
            RegisterComponentExtension("AudioSource", WriteAudioSource, ReadAudioSource, static (w, e) => w.RemoveComponent<AudioSource>(e));
            RegisterComponentExtension("AudioListener", static (w, e) => w.HasComponent<AudioListener>(e) ? "{}" : null,
                static (w, e, el, resolve) => w.AddComponent(e, new AudioListener()), static (w, e) => w.RemoveComponent<AudioListener>(e));
            RegisterUI();
            RegisterKart();
            RegisterTrack();
            RegisterRace();
            RegisterDriver();
            RegisterItems();
            RegisterChaseCamera();
            RegisterParticles();
            RegisterWindZone();
            RegisterComponentExtension("AnimationPlayer", WriteAnimationPlayer, ReadAnimationPlayer, static (w, e) => w.RemoveComponent<AnimationPlayer>(e));
            RegisterComponentExtension("Text3D", WriteText3D, ReadText3D, static (w, e) => w.RemoveComponent<Text3D>(e));
            RegisterComponentExtension("Animator",
                static (w, e) => w.TryGetComponent<ECS.Components.Animator>(e, out var a) ? "{\"Controller\":\"" + Esc(a.Controller ?? "") + "\"}" : null,
                static (w, e, el, resolve) => w.AddComponent(e, new ECS.Components.Animator { Controller = el.TryGetProperty("Controller", out var c) ? c.GetString() ?? "" : "" }),
                static (w, e) => w.RemoveComponent<ECS.Components.Animator>(e));
        }

        // G8.4: {"Text":"Shop","Font":"","Size":0.5,"Color":[1,1,1,1],"Align":"Center","Billboard":"Off","Outline":0,"OutlineColor":[0,0,0,1],"Key":""}.
        private static string? WriteText3D(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<Text3D>(e, out var t)) return null;
            return "{\"Text\":\"" + Esc(t.Text ?? "") + "\",\"Font\":\"" + Esc(t.Font ?? "") + "\",\"Size\":" + F(t.Size) + ",\"Color\":" + Four(t.Color)
                 + ",\"Align\":\"" + t.Align + "\",\"Billboard\":\"" + t.Billboard + "\",\"Outline\":" + F(t.Outline)
                 + ",\"OutlineColor\":" + Four(t.OutlineColor) + ",\"Key\":\"" + Esc(t.Key ?? "") + "\"}";
        }

        private static void ReadText3D(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var t = Text3D.Default;
            t.Text = Text(el, "Text", t.Text);
            t.Font = Text(el, "Font", t.Font);
            t.Size = Number(el, "Size", t.Size);
            Four(el, "Color", ref t.Color);
            if (Enum.TryParse(Text(el, "Align", ""), true, out UIAlign align)) t.Align = align;
            if (Enum.TryParse(Text(el, "Billboard", ""), true, out Text3DBillboard bb)) t.Billboard = bb;
            t.Outline = Number(el, "Outline", t.Outline);
            Four(el, "OutlineColor", ref t.OutlineColor);
            t.Key = Text(el, "Key", "");
            world.AddComponent(e, t);
        }

        // {"Clip":"Animations/DoorOpen.anim.json"} — "Auto":false and "Speed" only when not the defaults.
        private static string? WriteAnimationPlayer(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<AnimationPlayer>(e, out var a)) return null;
            var sb = new System.Text.StringBuilder("{\"Clip\":\"").Append(Esc(a.Clip ?? "")).Append('"');
            if (!a.PlayAutomatically) sb.Append(",\"Auto\":false");
            if (a.Speed != 1f) sb.Append(",\"Speed\":").Append(F(a.Speed));
            return sb.Append('}').ToString();
        }

        private static void ReadAnimationPlayer(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var a = AnimationPlayer.Default;
            if (el.TryGetProperty("Clip", out var clip)) a.Clip = clip.GetString() ?? "";
            a.PlayAutomatically = !el.TryGetProperty("Auto", out var auto) || auto.ValueKind != JsonValueKind.False;
            if (el.TryGetProperty("Speed", out var speed) && speed.ValueKind == JsonValueKind.Number) a.Speed = speed.GetSingle();
            a.Playing = a.PlayAutomatically;       // Play Automatically: it plays from the game's start
            world.AddComponent(e, a);
        }

        private static string? WriteAudioSource(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<AudioSource>(e, out var a)) return null;
            return "{\"Clip\":\"" + Esc(a.Clip ?? "") + "\",\"Volume\":" + F(a.Volume) + ",\"Pitch\":" + F(a.Pitch)
                 + ",\"Loop\":" + (a.Loop ? "true" : "false") + ",\"PlayOnStart\":" + (a.PlayOnStart ? "true" : "false")
                 // In space: written only when it is (a scene made before stays as it was).
                 + (a.Spatial ? ",\"Spatial\":true,\"Min\":" + F(a.MinDistance) + ",\"Max\":" + F(a.MaxDistance) + ",\"Rolloff\":\"" + a.Rolloff + "\"" : "") + "}";
        }

        private static void ReadAudioSource(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var a = AudioSource.Default;
            if (el.TryGetProperty("Clip", out var clip)) a.Clip = clip.GetString() ?? "";
            if (el.TryGetProperty("Volume", out var volume) && volume.ValueKind == JsonValueKind.Number) a.Volume = volume.GetSingle();
            if (el.TryGetProperty("Pitch", out var pitch) && pitch.ValueKind == JsonValueKind.Number) a.Pitch = pitch.GetSingle();
            a.Loop = el.TryGetProperty("Loop", out var loop) && loop.ValueKind == JsonValueKind.True;
            a.PlayOnStart = !el.TryGetProperty("PlayOnStart", out var start) || start.ValueKind != JsonValueKind.False;
            a.Spatial = el.TryGetProperty("Spatial", out var spatial) && spatial.ValueKind == JsonValueKind.True;
            if (el.TryGetProperty("Min", out var min) && min.ValueKind == JsonValueKind.Number) a.MinDistance = MathF.Max(0.001f, min.GetSingle());
            if (el.TryGetProperty("Max", out var max) && max.ValueKind == JsonValueKind.Number) a.MaxDistance = MathF.Max(a.MinDistance, max.GetSingle());
            if (el.TryGetProperty("Rolloff", out var roll) && Enum.TryParse(roll.GetString(), true, out AudioRolloff rolloff)) a.Rolloff = rolloff;
            world.AddComponent(e, a);
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

        private static string? WriteBody2D(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<Body2D>(e, out var b)) return null;
            var sb = new StringBuilder();
            sb.Append("{\"Type\":\"").Append(b.Type.ToString())
              .Append("\",\"GravityScale\":").Append(F(b.GravityScale))
              .Append(",\"LinearDamping\":").Append(F(b.LinearDamping))
              .Append(",\"AngularDamping\":").Append(F(b.AngularDamping))
              .Append(",\"FixedRotation\":").Append(b.FixedRotation ? "true" : "false")
              .Append(",\"Bullet\":").Append(b.Bullet ? "true" : "false")
              .Append(",\"Density\":").Append(F(b.Density))
              .Append(",\"Friction\":").Append(F(b.Friction))
              .Append(",\"Restitution\":").Append(F(b.Restitution))
              .Append(",\"IsTrigger\":").Append(b.IsTrigger ? "true" : "false")
              .Append(",\"Layer\":").Append(b.Layer)
              .Append(",\"Mask\":").Append(b.Mask)
              .Append(",\"Shapes\":[");
            if (b.Shapes != null)
                for (int i = 0; i < b.Shapes.Length; i++)
                {
                    Shape2D s = b.Shapes[i];
                    sb.Append(i == 0 ? "" : ",").Append("{\"Type\":\"").Append(s.Type.ToString()).Append('"');
                    if (s.Type == Shape2DType.Circle) sb.Append(",\"Radius\":").Append(F(s.Radius));
                    else if (s.Type == Shape2DType.Polygon)
                    {
                        sb.Append(",\"Points\":[");
                        if (s.Points != null)
                            for (int k = 0; k < s.Points.Length; k++)
                                sb.Append(k == 0 ? "" : ",").Append('[').Append(F(s.Points[k].X)).Append(',').Append(F(s.Points[k].Y)).Append(']');
                        sb.Append(']');
                    }
                    else sb.Append(",\"Size\":[").Append(F(s.Size.X)).Append(',').Append(F(s.Size.Y)).Append(']');
                    sb.Append(",\"Offset\":[").Append(F(s.Offset.X)).Append(',').Append(F(s.Offset.Y)).Append(']');
                    if (s.Type != Shape2DType.Circle) sb.Append(",\"Angle\":").Append(F(s.Angle));
                    sb.Append('}');
                }
            return sb.Append("]}").ToString();
        }

        private static void ReadBody2D(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var b = Body2D.Default;
            if (el.TryGetProperty("Type", out var type) && Enum.TryParse(type.GetString(), true, out Body2DType parsed)) b.Type = parsed;
            if (el.TryGetProperty("GravityScale", out var g)) b.GravityScale = g.GetSingle();
            if (el.TryGetProperty("LinearDamping", out var ld)) b.LinearDamping = ld.GetSingle();
            if (el.TryGetProperty("AngularDamping", out var ad)) b.AngularDamping = ad.GetSingle();
            b.FixedRotation = el.TryGetProperty("FixedRotation", out var fr) && fr.GetBoolean();
            b.Bullet = el.TryGetProperty("Bullet", out var bu) && bu.GetBoolean();
            if (el.TryGetProperty("Density", out var d)) b.Density = d.GetSingle();
            if (el.TryGetProperty("Friction", out var f)) b.Friction = f.GetSingle();
            if (el.TryGetProperty("Restitution", out var r)) b.Restitution = r.GetSingle();
            b.IsTrigger = el.TryGetProperty("IsTrigger", out var t) && t.GetBoolean();
            if (el.TryGetProperty("Layer", out var layer)) b.Layer = layer.GetInt32();
            if (el.TryGetProperty("Mask", out var mask)) b.Mask = mask.GetInt32();
            if (el.TryGetProperty("Shapes", out var shapes) && shapes.ValueKind == JsonValueKind.Array && shapes.GetArrayLength() > 0)
            {
                b.Shapes = new Shape2D[shapes.GetArrayLength()];
                int i = 0;
                foreach (JsonElement se in shapes.EnumerateArray())
                {
                    var s = new Shape2D { Size = Vector2.One, Radius = 0.5f };
                    if (se.TryGetProperty("Type", out var st) && Enum.TryParse(st.GetString(), true, out Shape2DType shapeType)) s.Type = shapeType;
                    if (se.TryGetProperty("Size", out var size) && size.ValueKind == JsonValueKind.Array && size.GetArrayLength() == 2) s.Size = new Vector2(size[0].GetSingle(), size[1].GetSingle());
                    if (se.TryGetProperty("Radius", out var radius)) s.Radius = radius.GetSingle();
                    if (se.TryGetProperty("Offset", out var offset) && offset.ValueKind == JsonValueKind.Array && offset.GetArrayLength() == 2) s.Offset = new Vector2(offset[0].GetSingle(), offset[1].GetSingle());
                    if (se.TryGetProperty("Angle", out var angle)) s.Angle = angle.GetSingle();
                    if (se.TryGetProperty("Points", out var points) && points.ValueKind == JsonValueKind.Array)
                    {
                        var list = new System.Collections.Generic.List<Vector2>();
                        foreach (JsonElement p in points.EnumerateArray())
                            if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() == 2) list.Add(new Vector2(p[0].GetSingle(), p[1].GetSingle()));
                        s.Points = list.ToArray();
                    }
                    b.Shapes[i++] = s;
                }
            }
            world.AddComponent(e, b);
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
