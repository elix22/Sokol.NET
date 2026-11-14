using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // The scene block "Particles": a particle system in the world (examples/GameEditor/docs/PARTICLES_3D_DESIGN.md §3.0; H5_DESIGN.md
    // §16 before it). Written whole, module by module; a block of before G8.7 (no "Duration") reads into the modules with the
    // very same particles: its Size and ColorStart / ColorEnd into Start Size, Size over Lifetime and Color over Lifetime, its
    // Drag into Limit Velocity, its burst at the start (Burst while Emitting) into the Bursts.
    public static partial class SceneSerializer
    {
        private static readonly string[] ShapeNames = { "Cone", "Sphere", "Hemisphere", "Box", "Circle", "Edge" };
        private static readonly string[] ConeFromNames = { "Base", "Volume" };
        private static readonly string[] BoxFromNames = { "Volume", "Shell", "Edge" };
        private static readonly string[] RenderModeNames = { "Billboard", "Stretched", "Horizontal", "Vertical" };
        private static readonly string[] BlendNames = { "Alpha", "Additive" };
        private static readonly string[] SortNames = { "None", "ByDistance", "OldestInFront", "YoungestInFront" };
        private static readonly string[] SheetTimeNames = { "Lifetime", "Fps" };

        private static void RegisterParticles()
        {
            RegisterComponentExtension("Particles", WriteParticles, ReadParticles, static (w, e) => w.RemoveComponent<ParticlesComponent>(e));
        }

        private static string? WriteParticles(ECSWorld world, Frent.Entity e)
        {
            if (!world.TryGetComponent<ParticlesComponent>(e, out var p)) return null;
            var b = new StringBuilder(1024);
            b.Append("{\"Texture\":\"").Append(Esc(p.Texture ?? "")).Append('"');
            Bool(b, "Emitting", p.Emitting);
            // Main
            Num(b, "Duration", p.Duration); Bool(b, "Looping", p.Looping); Bool(b, "Prewarm", p.Prewarm); Num(b, "StartDelay", p.StartDelay);
            Bool(b, "PlayOnAwake", p.PlayOnAwake); Num(b, "SimulationSpeed", p.SimulationSpeed);
            Key(b, "Life").Append(Pair(p.Life)); Key(b, "Speed").Append(Pair(p.Speed)); Key(b, "StartSize").Append(Pair(p.StartSize));
            Key(b, "StartRotation").Append(Pair(p.StartRotation)); Key(b, "StartColor").Append(Four(p.StartColor)); Key(b, "StartColor2").Append(Four(p.StartColor2));
            Num(b, "Gravity", p.Gravity); Bool(b, "World", p.World); Key(b, "Max").Append(p.Max.ToString(CultureInfo.InvariantCulture));
            Bool(b, "AutoRandomSeed", p.AutoRandomSeed); Key(b, "RandomSeed").Append(p.RandomSeed.ToString(CultureInfo.InvariantCulture));
            // Emission
            Num(b, "Rate", p.Rate); Num(b, "RateOverDistance", p.RateOverDistance); Key(b, "Burst").Append(p.Burst.ToString(CultureInfo.InvariantCulture));
            Key(b, "Bursts").Append('[');
            for (int i = 0; i < p.Bursts.Count; i++)
            {
                ParticleBurst u = p.Bursts.Items[i];
                if (i > 0) b.Append(',');
                b.Append('[').Append(F(u.Time)).Append(',').Append(u.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                 .Append(u.Cycles.ToString(CultureInfo.InvariantCulture)).Append(',').Append(F(u.Interval)).Append(',').Append(F(u.Probability)).Append(']');
            }
            b.Append(']');
            // Shape
            Bool(b, "ShapeOn", p.ShapeOn); Name(b, "Shape", ShapeNames, (int)p.Shape); Key(b, "Direction").Append(Three(p.Direction));
            Num(b, "Spread", p.Spread); Num(b, "Radius", p.Radius); Num(b, "RadiusThickness", p.RadiusThickness); Num(b, "Arc", p.Arc); Num(b, "Length", p.Length);
            Name(b, "ConeFrom", ConeFromNames, (int)p.ConeFrom); Name(b, "BoxFrom", BoxFromNames, (int)p.BoxFrom);
            Key(b, "ShapePosition").Append(Three(p.ShapePosition)); Key(b, "ShapeScale").Append(Three(p.ShapeScale));
            Num(b, "RandomizeDirection", p.RandomizeDirection); Num(b, "SpherizeDirection", p.SpherizeDirection);
            // Over lifetime
            Bool(b, "VelocityOn", p.VelocityOn); Key(b, "Velocity").Append(Three(p.Velocity)); Bool(b, "VelocityWorld", p.VelocityWorld);
            Bool(b, "LimitOn", p.LimitOn); Num(b, "SpeedLimit", p.SpeedLimit); Num(b, "Dampen", p.Dampen); Num(b, "Drag", p.Drag);
            Bool(b, "ForceOn", p.ForceOn); Key(b, "Force").Append(Three(p.Force)); Bool(b, "ForceWorld", p.ForceWorld);
            Bool(b, "ColorOn", p.ColorOn); Key(b, "ColorOverLifetime"); Gradient(b, in p.ColorOverLifetime);
            Bool(b, "SizeOn", p.SizeOn); Key(b, "SizeOverLifetime"); Curve(b, in p.SizeOverLifetime);
            Bool(b, "RotationOn", p.RotationOn); Key(b, "AngularVelocity").Append(Pair(p.AngularVelocity));
            // Noise
            Bool(b, "NoiseOn", p.NoiseOn); Num(b, "NoiseStrength", p.NoiseStrength); Num(b, "NoiseFrequency", p.NoiseFrequency); Num(b, "NoiseScroll", p.NoiseScroll);
            // Texture Sheet Animation
            Bool(b, "SheetOn", p.SheetOn); Key(b, "Sheet").Append('[').Append(p.SheetColumns.ToString(CultureInfo.InvariantCulture)).Append(',')
                                                       .Append(p.SheetRows.ToString(CultureInfo.InvariantCulture)).Append(']');
            Name(b, "SheetTime", SheetTimeNames, (int)p.SheetTime); Num(b, "SheetFps", p.SheetFps); Num(b, "SheetCycles", p.SheetCycles);
            Key(b, "SheetStartFrame").Append(Pair(p.SheetStartFrame));
            // Renderer
            Name(b, "RenderMode", RenderModeNames, (int)p.RenderMode); Num(b, "SpeedScale", p.SpeedScale); Num(b, "LengthScale", p.LengthScale);
            Name(b, "Blend", BlendNames, (int)p.Blend); Name(b, "Sort", SortNames, (int)p.Sort);
            return b.Append('}').ToString();

            static StringBuilder Key(StringBuilder b, string k) => b.Append(",\"").Append(k).Append("\":");
            static void Num(StringBuilder b, string k, float v) => Key(b, k).Append(F(v));
            static void Bool(StringBuilder b, string k, bool v) => Key(b, k).Append(v ? "true" : "false");
            static void Name(StringBuilder b, string k, string[] names, int v) => Key(b, k).Append('"').Append(names[Math.Clamp(v, 0, names.Length - 1)]).Append('"');
            static string Three(Vector3 v) => "[" + F(v.X) + "," + F(v.Y) + "," + F(v.Z) + "]";
            static void Gradient(StringBuilder b, in ParticleGradient g)
            {
                b.Append("{\"Colors\":[");
                for (int i = 0; i < g.ColorCount; i++) { if (i > 0) b.Append(','); b.Append(Four(g.Colors[i])); }
                b.Append("],\"Alphas\":[");
                for (int i = 0; i < g.AlphaCount; i++) { if (i > 0) b.Append(','); b.Append(Pair(g.Alphas[i])); }
                b.Append("],\"Fixed\":").Append(g.Fixed ? "true" : "false").Append('}');
            }
            static void Curve(StringBuilder b, in ParticleCurve c)
            {
                b.Append("{\"Keys\":[");
                for (int i = 0; i < c.Count; i++) { if (i > 0) b.Append(','); b.Append(Pair(c.Keys[i])); }
                b.Append("],\"Multiplier\":").Append(F(c.Multiplier)).Append('}');
            }
        }

        private static void ReadParticles(ECSWorld world, Frent.Entity e, JsonElement el, Func<int, Frent.Entity> resolve)
        {
            var p = ParticlesComponent.Default;
            bool old = !el.TryGetProperty("Duration", out _);
            p.Texture = Text(el, "Texture", p.Texture);
            p.Emitting = Flag(el, "Emitting", p.Emitting);
            // Main
            p.Duration = MathF.Max(0.05f, Number(el, "Duration", p.Duration));
            p.Looping = Flag(el, "Looping", p.Looping); p.Prewarm = Flag(el, "Prewarm", p.Prewarm);
            p.StartDelay = MathF.Max(0f, Number(el, "StartDelay", p.StartDelay));
            p.PlayOnAwake = Flag(el, "PlayOnAwake", p.PlayOnAwake); p.SimulationSpeed = MathF.Max(0f, Number(el, "SimulationSpeed", p.SimulationSpeed));
            Pair(el, "Life", ref p.Life); Pair(el, "Speed", ref p.Speed); Pair(el, "StartSize", ref p.StartSize); Pair(el, "StartRotation", ref p.StartRotation);
            Four(el, "StartColor", ref p.StartColor); Four(el, "StartColor2", ref p.StartColor2);
            p.Gravity = Number(el, "Gravity", p.Gravity);
            p.World = Flag(el, "World", p.World);
            p.Max = Math.Clamp((int)Number(el, "Max", p.Max), 1, 4096);
            p.AutoRandomSeed = Flag(el, "AutoRandomSeed", p.AutoRandomSeed);
            if (el.TryGetProperty("RandomSeed", out var rs) && rs.ValueKind == JsonValueKind.Number && rs.TryGetUInt32(out uint seed)) p.RandomSeed = seed;
            // Emission
            p.Rate = Number(el, "Rate", p.Rate); p.RateOverDistance = Number(el, "RateOverDistance", p.RateOverDistance);
            p.Burst = (int)Number(el, "Burst", p.Burst);
            p.Bursts = default;
            if (el.TryGetProperty("Bursts", out var bs) && bs.ValueKind == JsonValueKind.Array)
                foreach (var x in bs.EnumerateArray())
                    if (x.ValueKind == JsonValueKind.Array && x.GetArrayLength() == 5)
                        p.Bursts.Add(new ParticleBurst
                        {
                            Time = MathF.Max(0f, x[0].GetSingle()), Count = Math.Max(0, (int)x[1].GetSingle()), Cycles = Math.Max(0, (int)x[2].GetSingle()),
                            Interval = MathF.Max(0.001f, x[3].GetSingle()), Probability = Math.Clamp(x[4].GetSingle(), 0f, 1f),
                        });
            // Shape
            p.ShapeOn = Flag(el, "ShapeOn", p.ShapeOn);
            p.Shape = (ParticleShape)Named(el, "Shape", ShapeNames, (int)p.Shape);
            Three(el, "Direction", ref p.Direction);
            p.Spread = Number(el, "Spread", p.Spread); p.Radius = MathF.Max(0f, Number(el, "Radius", p.Radius));
            p.RadiusThickness = Math.Clamp(Number(el, "RadiusThickness", p.RadiusThickness), 0f, 1f);
            p.Arc = Math.Clamp(Number(el, "Arc", p.Arc), 0f, 360f); p.Length = MathF.Max(0f, Number(el, "Length", p.Length));
            p.ConeFrom = (ParticleConeFrom)Named(el, "ConeFrom", ConeFromNames, (int)p.ConeFrom);
            p.BoxFrom = (ParticleBoxFrom)Named(el, "BoxFrom", BoxFromNames, (int)p.BoxFrom);
            Three(el, "ShapePosition", ref p.ShapePosition); Three(el, "ShapeScale", ref p.ShapeScale);
            p.RandomizeDirection = Math.Clamp(Number(el, "RandomizeDirection", p.RandomizeDirection), 0f, 1f);
            p.SpherizeDirection = Math.Clamp(Number(el, "SpherizeDirection", p.SpherizeDirection), 0f, 1f);
            // Over lifetime
            p.VelocityOn = Flag(el, "VelocityOn", p.VelocityOn); Three(el, "Velocity", ref p.Velocity); p.VelocityWorld = Flag(el, "VelocityWorld", p.VelocityWorld);
            p.LimitOn = Flag(el, "LimitOn", p.LimitOn); p.SpeedLimit = MathF.Max(0f, Number(el, "SpeedLimit", p.SpeedLimit));
            p.Dampen = Math.Clamp(Number(el, "Dampen", p.Dampen), 0f, 1f); p.Drag = Number(el, "Drag", p.Drag);
            p.ForceOn = Flag(el, "ForceOn", p.ForceOn); Three(el, "Force", ref p.Force); p.ForceWorld = Flag(el, "ForceWorld", p.ForceWorld);
            p.ColorOn = Flag(el, "ColorOn", p.ColorOn);
            if (el.TryGetProperty("ColorOverLifetime", out var g) && g.ValueKind == JsonValueKind.Object) p.ColorOverLifetime = ReadGradient(g);
            p.SizeOn = Flag(el, "SizeOn", p.SizeOn);
            if (el.TryGetProperty("SizeOverLifetime", out var cv) && cv.ValueKind == JsonValueKind.Object) p.SizeOverLifetime = ReadCurve(cv);
            p.RotationOn = Flag(el, "RotationOn", p.RotationOn); Pair(el, "AngularVelocity", ref p.AngularVelocity);
            // Noise
            p.NoiseOn = Flag(el, "NoiseOn", p.NoiseOn); p.NoiseStrength = Number(el, "NoiseStrength", p.NoiseStrength);
            p.NoiseFrequency = MathF.Max(0f, Number(el, "NoiseFrequency", p.NoiseFrequency)); p.NoiseScroll = Number(el, "NoiseScroll", p.NoiseScroll);
            // Texture Sheet Animation
            p.SheetOn = Flag(el, "SheetOn", p.SheetOn);
            if (el.TryGetProperty("Sheet", out var sh) && sh.ValueKind == JsonValueKind.Array && sh.GetArrayLength() == 2)
            {
                p.SheetColumns = Math.Clamp((int)sh[0].GetSingle(), 1, 64);
                p.SheetRows = Math.Clamp((int)sh[1].GetSingle(), 1, 64);
            }
            p.SheetTime = (ParticleSheetTime)Named(el, "SheetTime", SheetTimeNames, (int)p.SheetTime);
            p.SheetFps = MathF.Max(0f, Number(el, "SheetFps", p.SheetFps)); p.SheetCycles = MathF.Max(0f, Number(el, "SheetCycles", p.SheetCycles));
            Pair(el, "SheetStartFrame", ref p.SheetStartFrame);
            // Renderer
            p.RenderMode = (ParticleRenderMode)Named(el, "RenderMode", RenderModeNames, (int)p.RenderMode);
            p.SpeedScale = MathF.Max(0f, Number(el, "SpeedScale", p.SpeedScale)); p.LengthScale = MathF.Max(0f, Number(el, "LengthScale", p.LengthScale));
            p.Blend = (ParticleBlend)Named(el, "Blend", BlendNames, (int)p.Blend);
            p.Sort = (ParticleSort)Named(el, "Sort", SortNames, (int)p.Sort);

            if (old)
            {
                // A block of before G8.7: its fields into the modules, the very same particles (§3.0).
                p.ColorOverLifetime = ParticleGradient.Two(p.ColorStart, p.ColorEnd); p.ColorOn = true;
                p.StartColor = p.StartColor2 = Vector4.One;
                p.LimitOn = p.Drag != 0f; p.Dampen = 0f;
                p.ShapeOn = true; p.Shape = ParticleShape.Cone; p.Radius = 0f; p.Arc = 360f; p.ConeFrom = ParticleConeFrom.Base;
                p.ShapePosition = Vector3.Zero; p.ShapeScale = Vector3.One;
                p.Duration = 5f; p.Looping = true; p.PlayOnAwake = true; p.SimulationSpeed = 1f;
                if (p.Emitting && p.Burst > 0) p.Bursts.Add(ParticleBurst.At(0f, p.Burst));
            }
            // Size, ColorStart, ColorEnd (the fields of before; given to entity.set too): over what the modules say.
            var size = p.Size;
            if (Pair(el, "Size", ref size)) p.Size = new Vector2(MathF.Max(0f, size.X), MathF.Max(0f, size.Y));
            Vector4 c0 = p.ColorStart, c1 = p.ColorEnd;
            if (Four(el, "ColorStart", ref c0)) p.ColorStart = c0;
            if (Four(el, "ColorEnd", ref c1)) p.ColorEnd = c1;
            world.AddComponent(e, p);

            static bool Flag(JsonElement el, string k, bool fallback) =>
                !el.TryGetProperty(k, out var v) ? fallback : v.ValueKind == JsonValueKind.True ? true : v.ValueKind == JsonValueKind.False ? false : fallback;
            static int Named(JsonElement el, string k, string[] names, int fallback)
            {
                if (!el.TryGetProperty(k, out var v) || v.ValueKind != JsonValueKind.String) return fallback;
                int i = Array.IndexOf(names, v.GetString());
                return i >= 0 ? i : fallback;
            }
            static void Three(JsonElement el, string k, ref Vector3 into)
            {
                if (el.TryGetProperty(k, out var d) && d.ValueKind == JsonValueKind.Array && d.GetArrayLength() == 3)
                    into = new Vector3(d[0].GetSingle(), d[1].GetSingle(), d[2].GetSingle());
            }
            static ParticleGradient ReadGradient(JsonElement g)
            {
                var r = new ParticleGradient();
                if (g.TryGetProperty("Colors", out var cs) && cs.ValueKind == JsonValueKind.Array)
                    foreach (var k in cs.EnumerateArray())
                        if (k.ValueKind == JsonValueKind.Array && k.GetArrayLength() == 4)
                            r.AddColor(k[3].GetSingle(), new Vector3(k[0].GetSingle(), k[1].GetSingle(), k[2].GetSingle()), out _);
                if (g.TryGetProperty("Alphas", out var al) && al.ValueKind == JsonValueKind.Array)
                    foreach (var k in al.EnumerateArray())
                        if (k.ValueKind == JsonValueKind.Array && k.GetArrayLength() == 2)
                            r.AddAlpha(k[1].GetSingle(), k[0].GetSingle(), out _);
                r.Fixed = g.TryGetProperty("Fixed", out var f) && f.ValueKind == JsonValueKind.True;
                if (r.ColorCount == 0 || r.AlphaCount == 0) return ParticleGradient.White;
                return r;
            }
            static ParticleCurve ReadCurve(JsonElement c)
            {
                var r = new ParticleCurve { Multiplier = 1f };
                if (c.TryGetProperty("Keys", out var ks) && ks.ValueKind == JsonValueKind.Array)
                    foreach (var k in ks.EnumerateArray())
                        if (k.ValueKind == JsonValueKind.Array && k.GetArrayLength() == 2) r.Add(k[0].GetSingle(), k[1].GetSingle());
                r.Multiplier = Number(c, "Multiplier", 1f);
                if (r.Count == 0) return ParticleCurve.Line(1f, 1f);
                return r;
            }
        }
    }
}
