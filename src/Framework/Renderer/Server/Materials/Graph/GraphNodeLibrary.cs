using System;
using System.Collections.Generic;

namespace GameEditor.Framework.Renderer.Server.Materials.Graph
{
    /// <summary>A port of a node type: its name, its size (1-4 components; 0 = dynamic - the widest of the node's dynamic
    /// inputs, the reference editor's "Dynamic Vector") and, for an input, its value when nothing is connected.</summary>
    public readonly record struct GraphPort(string Name, int Size, float[]? Default = null);

    /// <summary>What a node's code is made from: its inputs as GLSL expressions (already of the port's size), its node
    /// (settings) and the graph; it returns one GLSL expression per output.</summary>
    public delegate string[] GraphEmit(string[] inputs, GraphNode node, GraphCompileContext context);

    /// <summary>A kind of node (G8.10): its category for the Create Node menu, ports, and code.</summary>
    public sealed class GraphNodeType
    {
        public string Name = "", Category = "";
        public GraphPort[] Inputs = Array.Empty<GraphPort>();
        public GraphPort[] Outputs = Array.Empty<GraphPort>();
        public GraphEmit Emit = (_, _, _) => Array.Empty<string>();
        /// <summary>The node's settings and their default (a property's name, a mask, a texture property).</summary>
        public (string Name, string Default)[] Settings = Array.Empty<(string, string)>();
    }

    /// <summary>
    /// The nodes a shader graph is made of (G8.10, the reference editor's library v1: Input, Math, Channel, UV, Procedural,
    /// Utility, and the masters). Each emits GLSL in pbr.glsl's fragment shader (its varyings: v_Position, v_Normal,
    /// v_TexCoord0/1, v_Color, v_TBN; u_Camera; the material maps' textures for texture properties).
    /// </summary>
    public static class GraphNodeLibrary
    {
        private static readonly Dictionary<string, GraphNodeType> _types = new(StringComparer.Ordinal);

        public static IReadOnlyCollection<GraphNodeType> All => _types.Values;

        public static GraphNodeType? Get(string name) => _types.TryGetValue(name, out var t) ? t : null;

        private static float[] V(params float[] v) => v;

        private static void Add(string category, string name, GraphPort[] inputs, GraphPort[] outputs, GraphEmit emit,
                                params (string, string)[] settings)
            => _types[name] = new GraphNodeType { Name = name, Category = category, Inputs = inputs, Outputs = outputs, Emit = emit, Settings = settings };

        // A one-output node of dynamic size whose code is a function of its inputs.
        private static void Math(string name, string[] ins, Func<string[], string> code, float[]? def = null)
        {
            var ports = new GraphPort[ins.Length];
            for (int i = 0; i < ins.Length; i++) ports[i] = new GraphPort(ins[i], 0, def ?? V(0f));
            Add("Math", name, ports, new[] { new GraphPort("Out", 0) }, (i, _, _) => new[] { code(i) });
        }

        static GraphNodeLibrary()
        {
            // ── Masters ──────────────────────────────────────────────────────────────────────────────────────
            Add("Master", "Lit Master", new[]
            {
                new GraphPort("Base Color", 3, V(0.5f, 0.5f, 0.5f)), new GraphPort("Normal (Tangent Space)", 3, V(0f, 0f, 1f)),
                new GraphPort("Metallic", 1, V(0f)), new GraphPort("Smoothness", 1, V(0.5f)), new GraphPort("Emission", 3, V(0f, 0f, 0f)),
                new GraphPort("Ambient Occlusion", 1, V(1f)), new GraphPort("Alpha", 1, V(1f)), new GraphPort("Alpha Clip Threshold", 1, V(0f)),
            }, Array.Empty<GraphPort>(), (_, _, _) => Array.Empty<string>());
            Add("Master", "Unlit Master", new[]
            {
                new GraphPort("Base Color", 3, V(0.5f, 0.5f, 0.5f)), new GraphPort("Alpha", 1, V(1f)), new GraphPort("Alpha Clip Threshold", 1, V(0f)),
            }, Array.Empty<GraphPort>(), (_, _, _) => Array.Empty<string>());

            // ── Input ────────────────────────────────────────────────────────────────────────────────────────
            Add("Input", "UV", Array.Empty<GraphPort>(), new[] { new GraphPort("Out", 2) },
                (_, n, _) => new[] { n.Settings.GetValueOrDefault("Channel", "UV0") == "UV1" ? "v_TexCoord1" : "v_TexCoord0" }, ("Channel", "UV0"));
            Add("Input", "Position", Array.Empty<GraphPort>(), new[] { new GraphPort("Out", 3) }, (_, _, _) => new[] { "v_Position" });
            Add("Input", "Normal Vector", Array.Empty<GraphPort>(), new[] { new GraphPort("Out", 3) }, (_, _, _) => new[] { "normalize(v_Normal)" });
            Add("Input", "View Direction", Array.Empty<GraphPort>(), new[] { new GraphPort("Out", 3) }, (_, _, _) => new[] { "normalize(u_Camera - v_Position)" });
            Add("Input", "Vertex Color", Array.Empty<GraphPort>(), new[] { new GraphPort("Out", 4) }, (_, _, _) => new[] { "v_Color" });
            Add("Input", "Time", Array.Empty<GraphPort>(),
                new[] { new GraphPort("Time", 1), new GraphPort("Sine Time", 1), new GraphPort("Cosine Time", 1) },
                (_, _, c) => new[] { c.TimeExpr, $"sin({c.TimeExpr})", $"cos({c.TimeExpr})" });
            Add("Input", "Float", new[] { new GraphPort("X", 1, V(0f)) }, new[] { new GraphPort("Out", 1) }, (i, _, _) => new[] { i[0] });
            Add("Input", "Vector 2", new[] { new GraphPort("X", 1, V(0f)), new GraphPort("Y", 1, V(0f)) }, new[] { new GraphPort("Out", 2) },
                (i, _, _) => new[] { $"vec2({i[0]}, {i[1]})" });
            Add("Input", "Vector 3", new[] { new GraphPort("X", 1, V(0f)), new GraphPort("Y", 1, V(0f)), new GraphPort("Z", 1, V(0f)) },
                new[] { new GraphPort("Out", 3) }, (i, _, _) => new[] { $"vec3({i[0]}, {i[1]}, {i[2]})" });
            Add("Input", "Vector 4", new[] { new GraphPort("X", 1, V(0f)), new GraphPort("Y", 1, V(0f)), new GraphPort("Z", 1, V(0f)), new GraphPort("W", 1, V(0f)) },
                new[] { new GraphPort("Out", 4) }, (i, _, _) => new[] { $"vec4({i[0]}, {i[1]}, {i[2]}, {i[3]})" });
            Add("Input", "Color", new[] { new GraphPort("Color", 4, V(1f, 1f, 1f, 1f)) }, new[] { new GraphPort("Out", 4) }, (i, _, _) => new[] { i[0] });
            Add("Input", "Property", Array.Empty<GraphPort>(), new[] { new GraphPort("Out", 0) },
                (_, n, c) => new[] { c.PropertyExpr(n.Settings.GetValueOrDefault("Property", "")) }, ("Property", ""));
            Add("Input", "Sample Texture 2D", new[] { new GraphPort("UV", 2, null) },
                new[] { new GraphPort("RGBA", 4), new GraphPort("R", 1), new GraphPort("G", 1), new GraphPort("B", 1), new GraphPort("A", 1) },
                (i, n, c) =>
                {
                    string t = c.Sample(n.Settings.GetValueOrDefault("Texture", ""), i[0], n.Id);
                    return new[] { t, $"{t}.r", $"{t}.g", $"{t}.b", $"{t}.a" };
                }, ("Texture", ""));

            // ── Math ─────────────────────────────────────────────────────────────────────────────────────────
            Math("Add", new[] { "A", "B" }, i => $"({i[0]} + {i[1]})");
            Math("Subtract", new[] { "A", "B" }, i => $"({i[0]} - {i[1]})");
            Math("Multiply", new[] { "A", "B" }, i => $"({i[0]} * {i[1]})", V(1f));
            Math("Divide", new[] { "A", "B" }, i => $"({i[0]} / {i[1]})", V(1f));
            Math("Power", new[] { "A", "B" }, i => $"pow(max({i[0]}, 0.0), {i[1]})", V(1f));
            Math("Minimum", new[] { "A", "B" }, i => $"min({i[0]}, {i[1]})");
            Math("Maximum", new[] { "A", "B" }, i => $"max({i[0]}, {i[1]})");
            Math("Lerp", new[] { "A", "B", "T" }, i => $"mix({i[0]}, {i[1]}, {i[2]})");
            Math("Clamp", new[] { "In", "Min", "Max" }, i => $"clamp({i[0]}, {i[1]}, {i[2]})");
            Math("Step", new[] { "Edge", "In" }, i => $"step({i[0]}, {i[1]})");
            Math("Smoothstep", new[] { "Edge1", "Edge2", "In" }, i => $"smoothstep({i[0]}, {i[1]}, {i[2]})");
            Math("Saturate", new[] { "In" }, i => $"clamp({i[0]}, 0.0, 1.0)");
            Math("One Minus", new[] { "In" }, i => $"(1.0 - {i[0]})");
            Math("Negate", new[] { "In" }, i => $"(-{i[0]})");
            Math("Absolute", new[] { "In" }, i => $"abs({i[0]})");
            Math("Floor", new[] { "In" }, i => $"floor({i[0]})");
            Math("Fraction", new[] { "In" }, i => $"fract({i[0]})");
            Math("Sine", new[] { "In" }, i => $"sin({i[0]})");
            Math("Cosine", new[] { "In" }, i => $"cos({i[0]})");
            Math("Normalize", new[] { "In" }, i => $"normalize({i[0]})", V(1f));
            Add("Math", "Length", new[] { new GraphPort("In", 0, V(0f)) }, new[] { new GraphPort("Out", 1) }, (i, _, _) => new[] { $"length({i[0]})" });
            Add("Math", "Dot Product", new[] { new GraphPort("A", 0, V(0f)), new GraphPort("B", 0, V(0f)) }, new[] { new GraphPort("Out", 1) },
                (i, _, _) => new[] { $"dot({i[0]}, {i[1]})" });
            Add("Math", "Cross Product", new[] { new GraphPort("A", 3, V(0f, 0f, 0f)), new GraphPort("B", 3, V(0f, 0f, 0f)) }, new[] { new GraphPort("Out", 3) },
                (i, _, _) => new[] { $"cross({i[0]}, {i[1]})" });
            Add("Math", "Remap", new[] { new GraphPort("In", 0, V(0f)), new GraphPort("In Min Max", 2, V(-1f, 1f)), new GraphPort("Out Min Max", 2, V(0f, 1f)) },
                new[] { new GraphPort("Out", 0) },
                (i, _, _) => new[] { $"({i[2]}.x + ({i[0]} - {i[1]}.x) * ({i[2]}.y - {i[2]}.x) / ({i[1]}.y - {i[1]}.x))" });

            // ── Channel ──────────────────────────────────────────────────────────────────────────────────────
            Add("Channel", "Split", new[] { new GraphPort("In", 4, V(0f, 0f, 0f, 0f)) },
                new[] { new GraphPort("R", 1), new GraphPort("G", 1), new GraphPort("B", 1), new GraphPort("A", 1) },
                (i, _, _) => new[] { $"{i[0]}.x", $"{i[0]}.y", $"{i[0]}.z", $"{i[0]}.w" });
            Add("Channel", "Combine", new[] { new GraphPort("R", 1, V(0f)), new GraphPort("G", 1, V(0f)), new GraphPort("B", 1, V(0f)), new GraphPort("A", 1, V(0f)) },
                new[] { new GraphPort("RGBA", 4), new GraphPort("RGB", 3), new GraphPort("RG", 2) },
                (i, _, _) => new[] { $"vec4({i[0]}, {i[1]}, {i[2]}, {i[3]})", $"vec3({i[0]}, {i[1]}, {i[2]})", $"vec2({i[0]}, {i[1]})" });
            Add("Channel", "Swizzle", new[] { new GraphPort("In", 4, V(0f, 0f, 0f, 0f)) }, new[] { new GraphPort("Out", 0) },
                (i, n, _) => new[] { $"{i[0]}.{GraphCompileContext.Mask(n.Settings.GetValueOrDefault("Mask", "xyzw"))}" }, ("Mask", "xyzw"));

            // ── UV ───────────────────────────────────────────────────────────────────────────────────────────
            Add("UV", "Tiling And Offset", new[] { new GraphPort("UV", 2, null), new GraphPort("Tiling", 2, V(1f, 1f)), new GraphPort("Offset", 2, V(0f, 0f)) },
                new[] { new GraphPort("Out", 2) }, (i, _, _) => new[] { $"({i[0]} * {i[1]} + {i[2]})" });
            Add("UV", "Triplanar", new[] { new GraphPort("Position", 3, null), new GraphPort("Normal", 3, null), new GraphPort("Tile", 1, V(1f)), new GraphPort("Blend", 1, V(1f)) },
                new[] { new GraphPort("Out", 4) },
                (i, n, c) =>
                {
                    string tex = n.Settings.GetValueOrDefault("Texture", ""), id = "t" + n.Id;
                    c.Line($"vec3 {id}_w = pow(abs({i[1]}), vec3({i[3]})); {id}_w /= max({id}_w.x + {id}_w.y + {id}_w.z, 1e-5);");
                    string x = c.Sample(tex, $"{i[0]}.zy * {i[2]}", n.Id * 10 + 1), y = c.Sample(tex, $"{i[0]}.xz * {i[2]}", n.Id * 10 + 2), z = c.Sample(tex, $"{i[0]}.xy * {i[2]}", n.Id * 10 + 3);
                    return new[] { $"({x} * {id}_w.x + {y} * {id}_w.y + {z} * {id}_w.z)" };
                }, ("Texture", ""));

            // ── Procedural ───────────────────────────────────────────────────────────────────────────────────
            Add("Procedural", "Simple Noise", new[] { new GraphPort("UV", 2, null), new GraphPort("Scale", 1, V(500f)) }, new[] { new GraphPort("Out", 1) },
                (i, _, c) => { c.NeedNoise = true; return new[] { $"graph_simple_noise({i[0]} * {i[1]})" }; });
            Add("Procedural", "Checkerboard", new[] { new GraphPort("UV", 2, null), new GraphPort("Color A", 3, V(0.2f, 0.2f, 0.2f)),
                    new GraphPort("Color B", 3, V(0.7f, 0.7f, 0.7f)), new GraphPort("Frequency", 2, V(1f, 1f)) },
                new[] { new GraphPort("Out", 3) },
                (i, _, _) => new[] { $"mix({i[1]}, {i[2]}, mod(floor({i[0]}.x * {i[3]}.x * 2.0) + floor({i[0]}.y * {i[3]}.y * 2.0), 2.0))" });

            // ── Utility ──────────────────────────────────────────────────────────────────────────────────────
            Add("Utility", "Fresnel Effect", new[] { new GraphPort("Normal", 3, null), new GraphPort("View Dir", 3, null), new GraphPort("Power", 1, V(1f)) },
                new[] { new GraphPort("Out", 1) },
                (i, _, _) => new[] { $"pow(1.0 - clamp(dot(normalize({i[0]}), normalize({i[1]})), 0.0, 1.0), {i[2]})" });
        }

        /// <summary>The expression an unconnected input with no default stands for (the reference editor's: a UV input is
        /// the mesh's UV0, a Position the world position, a Normal the world normal, a View Dir the view direction).</summary>
        internal static string Implicit(string port) => port switch
        {
            "UV" => "v_TexCoord0",
            "Position" => "v_Position",
            "Normal" => "normalize(v_Normal)",
            "View Dir" => "normalize(u_Camera - v_Position)",
            _ => "0.0",
        };
    }
}
