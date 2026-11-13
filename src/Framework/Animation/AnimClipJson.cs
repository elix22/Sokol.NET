// AnimClipJson.cs — a clip as a `.anim.json` file and back (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §17.2).
// JsonDocument / Utf8JsonWriter, as the scene file: no reflection (NativeAOT).
//
// { "sokolnetAnim": 1, "name": "DoorOpen", "length": 1.5, "frameRate": 60, "wrap": "Once",
//   "curves": [ { "path": "Door/Panel", "component": "Transform", "property": "LocalEulerAngles.y",
//                 "pre": "ClampForever", "post": "ClampForever",
//                 "keys": [ { "t": 0, "v": 0, "in": 0, "out": 0, "mi": "ClampedAuto", "mo": "ClampedAuto" }, ... ] } ],
//   "events": [ { "t": 0.2, "fn": "OnDoorCreak", "s": "", "f": 0, "i": 0 } ] }
//
// Left out: a key's modes = ClampedAuto, "broken" = false, a curve's wraps = ClampForever. An infinite tangent (Constant)
// is the string "Infinity". On reading, every tangent but a Free one is set again from its mode (the file's numbers are
// what the modes give anyway, unless the file was written by hand).

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Animation
{
    public static class AnimClipJson
    {
        public const int Version = 1;

        public static string Write(AnimationClip clip)
        {
            var buf = new MemoryStream();
            using (var w = new Utf8JsonWriter(buf, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("sokolnetAnim", Version);
                w.WriteString("name", clip.Name);
                w.WriteNumber("length", clip.Length);
                w.WriteNumber("frameRate", clip.FrameRate);
                w.WriteString("wrap", clip.Wrap.ToString());
                w.WriteStartArray("curves");
                foreach (var (b, c) in clip.Curves)
                {
                    w.WriteStartObject();
                    w.WriteString("path", b.Path);
                    w.WriteString("component", b.Component);
                    w.WriteString("property", b.Property);
                    if (c.PreWrap != WrapMode.ClampForever) w.WriteString("pre", c.PreWrap.ToString());
                    if (c.PostWrap != WrapMode.ClampForever) w.WriteString("post", c.PostWrap.ToString());
                    w.WriteStartArray("keys");
                    foreach (var k in c.Keys)
                    {
                        w.WriteStartObject();
                        w.WriteNumber("t", k.Time);
                        w.WriteNumber("v", k.Value);
                        Tangent(w, "in", k.InTangent);
                        Tangent(w, "out", k.OutTangent);
                        if (k.ModeIn != TangentMode.ClampedAuto) w.WriteString("mi", k.ModeIn.ToString());
                        if (k.ModeOut != TangentMode.ClampedAuto) w.WriteString("mo", k.ModeOut.ToString());
                        if (k.Broken) w.WriteBoolean("broken", true);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("events");
                foreach (var e in clip.Events)
                {
                    w.WriteStartObject();
                    w.WriteNumber("t", e.Time);
                    w.WriteString("fn", e.FunctionName ?? "");
                    w.WriteString("s", e.StringParameter ?? "");
                    w.WriteNumber("f", e.FloatParameter);
                    w.WriteNumber("i", e.IntParameter);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buf.ToArray());
        }

        private static void Tangent(Utf8JsonWriter w, string name, float v)
        {
            if (float.IsPositiveInfinity(v)) w.WriteString(name, "Infinity");
            else if (float.IsNegativeInfinity(v)) w.WriteString(name, "-Infinity");
            else w.WriteNumber(name, v);
        }

        /// <summary>The clip of a `.anim.json` text; an exception (<see cref="FormatException"/>) names what is wrong.</summary>
        public static AnimationClip Read(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("sokolnetAnim", out var ver) || ver.GetInt32() > Version)
                throw new FormatException("not a .anim.json file of a version this engine reads (\"sokolnetAnim\": 1)");
            var clip = new AnimationClip
            {
                Name = Str(root, "name"),
                FrameRate = root.TryGetProperty("frameRate", out var fr) ? fr.GetSingle() : 60f,
                Wrap = Enum<WrapMode>(root, "wrap", WrapMode.Once),
            };
            if (root.TryGetProperty("curves", out var curves))
                foreach (var ce in curves.EnumerateArray())
                {
                    var keys = new System.Collections.Generic.List<Keyframe>();
                    if (ce.TryGetProperty("keys", out var ks))
                        foreach (var ke in ks.EnumerateArray())
                            keys.Add(new Keyframe
                            {
                                Time = ke.GetProperty("t").GetSingle(),
                                Value = ke.GetProperty("v").GetSingle(),
                                InTangent = Num(ke, "in"),
                                OutTangent = Num(ke, "out"),
                                ModeIn = Enum<TangentMode>(ke, "mi", TangentMode.ClampedAuto),
                                ModeOut = Enum<TangentMode>(ke, "mo", TangentMode.ClampedAuto),
                                Broken = ke.TryGetProperty("broken", out var br) && br.ValueKind == JsonValueKind.True,
                            });
                    var curve = new AnimationCurve(keys.ToArray())
                    {
                        PreWrap = Enum<WrapMode>(ce, "pre", WrapMode.ClampForever),
                        PostWrap = Enum<WrapMode>(ce, "post", WrapMode.ClampForever),
                    };
                    clip.SetCurve(Str(ce, "path"), Str(ce, "component"), Str(ce, "property"), curve);
                }
            if (root.TryGetProperty("events", out var events))
                foreach (var ee in events.EnumerateArray())
                    clip.Events.Add(new AnimationEvent
                    {
                        Time = ee.GetProperty("t").GetSingle(),
                        FunctionName = Str(ee, "fn"),
                        StringParameter = Str(ee, "s"),
                        FloatParameter = ee.TryGetProperty("f", out var f) ? f.GetSingle() : 0f,
                        IntParameter = ee.TryGetProperty("i", out var i) ? i.GetInt32() : 0,
                    });
            if (root.TryGetProperty("length", out var len)) clip.Length = MathF.Max(clip.Length, len.GetSingle());
            return clip;
        }

        private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

        private static float Num(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v)) return 0f;
            if (v.ValueKind == JsonValueKind.String)
                return float.Parse(v.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);   // "Infinity", "-Infinity"
            return v.GetSingle();
        }

        private static T Enum<T>(JsonElement e, string name, T fallback) where T : struct, Enum
        {
            if (!e.TryGetProperty(name, out var v)) return fallback;
            string s = v.GetString() ?? "";
            if (System.Enum.TryParse<T>(s, ignoreCase: false, out var r)) return r;
            throw new FormatException($"'{name}': '{s}' is not one of {string.Join(", ", System.Enum.GetNames<T>())}");
        }
    }
}
