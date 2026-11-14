// AnimatorController.cs — a state machine of clips (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §23): parameters,
// states (a clip or a 1D blend tree, at a speed), a default state, and transitions with conditions, an exit time and a
// duration. The `.controller.json` file; the running instance (ControllerInstance.cs).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Animation
{
    public enum ParameterType : byte { Float, Int, Bool, Trigger }

    /// <summary>Greater / Less (Float, Int), Equals / NotEqual (Int), If / IfNot (Bool; If for a Trigger: it is set).</summary>
    public enum ConditionMode : byte { Greater, Less, Equals, NotEqual, If, IfNot }

    public sealed class AnimatorParameter
    {
        public string Name = "";
        public ParameterType Type;
        /// <summary>The value it starts with (a Bool: 0 / 1; a Trigger starts unset).</summary>
        public float Default;
    }

    public struct TransitionCondition
    {
        public string Parameter;
        public ConditionMode Mode;
        public float Threshold;
    }

    /// <summary>A clip on a 1D blend tree's line: at <see cref="Threshold"/> of the tree's parameter it plays alone.</summary>
    public struct BlendMotion
    {
        public string Clip;
        public float Threshold;
    }

    public sealed class AnimatorState
    {
        public string Name = "";
        /// <summary>The clip it plays (a clip of the character's file, by name); ignored when <see cref="Blend"/> has motions.</summary>
        public string Clip = "";
        /// <summary>A 1D blend tree: clips by threshold on <see cref="BlendParameter"/> (sorted by threshold).</summary>
        public readonly List<BlendMotion> Blend = new();
        public string BlendParameter = "";
        public float Speed = 1f;
        /// <summary>Where the editor's graph draws it.</summary>
        public Vector2 Position;
        public bool IsBlendTree => Blend.Count > 0;
    }

    public sealed class AnimatorTransition
    {
        /// <summary>The state it leaves; "" = Any State (from every state but its target).</summary>
        public string From = "";
        public string To = "";
        public readonly List<TransitionCondition> Conditions = new();
        public bool HasExitTime;
        /// <summary>Normalized: 0.75 = three quarters into the source state's clip.</summary>
        public float ExitTime = 0.75f;
        /// <summary>The crossfade, seconds.</summary>
        public float Duration = 0.25f;
    }

    public sealed class AnimatorController
    {
        public string Name = "";
        public readonly List<AnimatorParameter> Parameters = new();
        public readonly List<AnimatorState> States = new();
        public readonly List<AnimatorTransition> Transitions = new();
        public string DefaultState = "";

        public AnimatorState? State(string name) => States.Find(s => s.Name == name);
        public AnimatorParameter? Parameter(string name) => Parameters.Find(p => p.Name == name);
    }

    /// <summary>A controller as a <c>.controller.json</c> file and back (JsonDocument / Utf8JsonWriter: no reflection).</summary>
    public static class ControllerJson
    {
        public const int Version = 1;
        public const string Extension = ".controller.json";

        public static string Write(AnimatorController c)
        {
            var buf = new MemoryStream();
            using (var w = new Utf8JsonWriter(buf, new JsonWriterOptions { Indented = true }))
            {
                w.WriteStartObject();
                w.WriteNumber("sokolnetController", Version);
                w.WriteString("name", c.Name);
                w.WriteString("default", c.DefaultState);
                w.WriteStartArray("parameters");
                foreach (var p in c.Parameters)
                {
                    w.WriteStartObject();
                    w.WriteString("name", p.Name);
                    w.WriteString("type", p.Type.ToString());
                    if (p.Default != 0f) w.WriteNumber("default", p.Default);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("states");
                foreach (var s in c.States)
                {
                    w.WriteStartObject();
                    w.WriteString("name", s.Name);
                    if (s.IsBlendTree)
                    {
                        w.WriteString("blendParameter", s.BlendParameter);
                        w.WriteStartArray("blend");
                        foreach (var m in s.Blend) { w.WriteStartObject(); w.WriteString("clip", m.Clip); w.WriteNumber("threshold", m.Threshold); w.WriteEndObject(); }
                        w.WriteEndArray();
                    }
                    else w.WriteString("clip", s.Clip);
                    if (s.Speed != 1f) w.WriteNumber("speed", s.Speed);
                    w.WriteStartArray("position"); w.WriteNumberValue(s.Position.X); w.WriteNumberValue(s.Position.Y); w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("transitions");
                foreach (var t in c.Transitions)
                {
                    w.WriteStartObject();
                    w.WriteString("from", t.From);
                    w.WriteString("to", t.To);
                    if (t.HasExitTime) w.WriteNumber("exitTime", t.ExitTime);
                    w.WriteNumber("duration", t.Duration);
                    w.WriteStartArray("conditions");
                    foreach (var k in t.Conditions)
                    {
                        w.WriteStartObject();
                        w.WriteString("parameter", k.Parameter);
                        w.WriteString("mode", k.Mode.ToString());
                        if (k.Mode is not (ConditionMode.If or ConditionMode.IfNot)) w.WriteNumber("threshold", k.Threshold);
                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buf.ToArray());
        }

        /// <summary>The controller of a <c>.controller.json</c> text; a <see cref="FormatException"/> says what is wrong.</summary>
        public static AnimatorController Read(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (!r.TryGetProperty("sokolnetController", out var v) || v.GetInt32() > Version)
                throw new FormatException("not a .controller.json file of a version this engine reads (\"sokolnetController\": 1)");
            var c = new AnimatorController { Name = Str(r, "name"), DefaultState = Str(r, "default") };
            if (r.TryGetProperty("parameters", out var ps))
                foreach (var p in ps.EnumerateArray())
                    c.Parameters.Add(new AnimatorParameter
                    {
                        Name = Str(p, "name"),
                        Type = Enum<ParameterType>(p, "type", ParameterType.Float),
                        Default = p.TryGetProperty("default", out var d) ? d.GetSingle() : 0f,
                    });
            if (r.TryGetProperty("states", out var ss))
                foreach (var s in ss.EnumerateArray())
                {
                    var st = new AnimatorState
                    {
                        Name = Str(s, "name"),
                        Clip = Str(s, "clip"),
                        BlendParameter = Str(s, "blendParameter"),
                        Speed = s.TryGetProperty("speed", out var sp) ? sp.GetSingle() : 1f,
                    };
                    if (s.TryGetProperty("position", out var pos) && pos.GetArrayLength() == 2) st.Position = new Vector2(pos[0].GetSingle(), pos[1].GetSingle());
                    if (s.TryGetProperty("blend", out var bl))
                        foreach (var m in bl.EnumerateArray()) st.Blend.Add(new BlendMotion { Clip = Str(m, "clip"), Threshold = m.GetProperty("threshold").GetSingle() });
                    st.Blend.Sort((a, b) => a.Threshold.CompareTo(b.Threshold));
                    c.States.Add(st);
                }
            if (r.TryGetProperty("transitions", out var ts))
                foreach (var t in ts.EnumerateArray())
                {
                    var tr = new AnimatorTransition
                    {
                        From = Str(t, "from"),
                        To = Str(t, "to"),
                        HasExitTime = t.TryGetProperty("exitTime", out var et),
                        Duration = t.TryGetProperty("duration", out var du) ? du.GetSingle() : 0.25f,
                    };
                    if (tr.HasExitTime) tr.ExitTime = et.GetSingle();
                    if (t.TryGetProperty("conditions", out var cs))
                        foreach (var k in cs.EnumerateArray())
                            tr.Conditions.Add(new TransitionCondition
                            {
                                Parameter = Str(k, "parameter"),
                                Mode = Enum<ConditionMode>(k, "mode", ConditionMode.If),
                                Threshold = k.TryGetProperty("threshold", out var th) ? th.GetSingle() : 0f,
                            });
                    c.Transitions.Add(tr);
                }
            return c;
        }

        private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

        private static T Enum<T>(JsonElement e, string name, T fallback) where T : struct, System.Enum
        {
            if (!e.TryGetProperty(name, out var v)) return fallback;
            string s = v.GetString() ?? "";
            if (System.Enum.TryParse<T>(s, ignoreCase: false, out var r)) return r;
            throw new FormatException($"'{name}': '{s}' is not one of {string.Join(", ", System.Enum.GetNames<T>())}");
        }
    }
}
