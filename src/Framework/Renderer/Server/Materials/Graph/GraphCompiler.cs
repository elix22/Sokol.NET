using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace GameEditor.Framework.Renderer.Server.Materials.Graph
{
    /// <summary>An error of a graph, at a node (0: the graph itself).</summary>
    public readonly record struct GraphError(int Node, string Message);

    /// <summary>Where a graph's property lives in the shader: a value in graph_params[Slot] (its first Size components) or a
    /// texture in material map slot Slot (0-4).</summary>
    public readonly record struct GraphPropertySlot(string Name, GraphPropertyType Type, int Slot, int Size);

    /// <summary>What compiling a graph gives: its GLSL function (and the unlit colour), the properties' places, or errors.</summary>
    public sealed class GraphCompileResult
    {
        public readonly List<GraphError> Errors = new();
        public string Function = "";
        public string UnlitColor = "";
        public readonly List<GraphPropertySlot> Slots = new();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>The compiler's state while it walks a graph, handed to the nodes' code.</summary>
    public sealed class GraphCompileContext
    {
        /// <summary>Values in graph_params[0..6]; graph_params[7].x is the time.</summary>
        public const int ValueSlots = 7;
        public const int TextureSlots = 5;
        internal static readonly string[] Textures = { "u_BaseColorTexture", "u_MetallicRoughnessTexture", "u_NormalTexture", "u_OcclusionTexture", "u_EmissiveTexture" };
        internal static readonly string[] Samplers = { "u_BaseColorSampler", "u_MetallicRoughnessSampler", "u_NormalSampler", "u_OcclusionSampler", "u_EmissiveSampler" };

        internal readonly StringBuilder Body = new();
        internal readonly GraphCompileResult Result;
        internal readonly Dictionary<string, GraphPropertySlot> Props = new(StringComparer.Ordinal);
        internal int CurrentNode;
        public bool NeedNoise;
        public string TimeExpr => "graph_params[7].x";

        internal GraphCompileContext(ShaderGraph g, GraphCompileResult r)
        {
            Result = r;
            int values = 0, textures = 0;
            foreach (var p in g.Properties)
            {
                if (Props.ContainsKey(p.Name)) { r.Errors.Add(new GraphError(0, $"two properties are named '{p.Name}'")); continue; }
                GraphPropertySlot slot;
                if (p.Type == GraphPropertyType.Texture2D)
                {
                    if (textures == TextureSlots) { r.Errors.Add(new GraphError(0, $"'{p.Name}': a graph can have at most {TextureSlots} textures")); continue; }
                    slot = new GraphPropertySlot(p.Name, p.Type, textures++, 4);
                }
                else
                {
                    if (values == ValueSlots) { r.Errors.Add(new GraphError(0, $"'{p.Name}': a graph can have at most {ValueSlots} value properties")); continue; }
                    slot = new GraphPropertySlot(p.Name, p.Type, values++, ShaderGraph.Components(p.Type));
                }
                Props[p.Name] = slot;
                r.Slots.Add(slot);
            }
        }

        /// <summary>A statement of the graph's function (e.g. a texture sample's variable).</summary>
        public void Line(string glsl) => Body.Append("    ").Append(glsl).Append('\n');

        internal int PropertySize(string name) => Props.TryGetValue(name, out var s) && s.Type != GraphPropertyType.Texture2D ? s.Size : 1;

        public string PropertyExpr(string name)
        {
            if (!Props.TryGetValue(name, out var s) || s.Type == GraphPropertyType.Texture2D)
            {
                Result.Errors.Add(new GraphError(CurrentNode, $"no value property named '{name}'"));
                return "0.0";
            }
            string p = $"graph_params[{s.Slot}]";
            return s.Size switch { 1 => p + ".x", 2 => p + ".xy", 3 => p + ".xyz", _ => p };
        }

        /// <summary>A sample of a texture property at uv: the name of a vec4 variable (declared on its own line).</summary>
        public string Sample(string texture, string uv, int key)
        {
            if (!Props.TryGetValue(texture, out var s) || s.Type != GraphPropertyType.Texture2D)
            {
                Result.Errors.Add(new GraphError(CurrentNode, texture == "" ? "no texture chosen" : $"no texture property named '{texture}'"));
                return "vec4(0.0)";
            }
            string v = $"tex{key}";
            Line($"vec4 {v} = texture(sampler2D({Textures[s.Slot]}, {Samplers[s.Slot]}), {uv});");
            return v;
        }

        /// <summary>A swizzle mask of 1-4 of x y z w (r g b a taken as x y z w); anything else is "x".</summary>
        public static string Mask(string mask)
        {
            var sb = new StringBuilder();
            foreach (char ch in mask.ToLowerInvariant())
            {
                char m = ch switch { 'r' => 'x', 'g' => 'y', 'b' => 'z', 'a' => 'w', _ => ch };
                if (m is 'x' or 'y' or 'z' or 'w' && sb.Length < 4) sb.Append(m);
            }
            return sb.Length == 0 ? "x" : sb.ToString();
        }
    }

    /// <summary>
    /// Turns a shader graph into GLSL (G8.10, docs SHADER_GRAPH_DESIGN.md §3.2): walks the graph from its master node,
    /// emits each node's code once in dependency order into <c>void graphSurface(inout Surface s)</c>, and makes the graph's
    /// whole shader - the engine's pbr.glsl with its @includes expanded and the GRAPH_ comments filled in - for sokol-shdc
    /// (bare_yaml in the editor, sokol_csharp with --reflection for a built game).
    /// </summary>
    public static class GraphCompiler
    {
        public static GraphCompileResult Compile(ShaderGraph g)
        {
            var r = new GraphCompileResult();
            var c = new GraphCompileContext(g, r);
            var master = g.Master();
            if (master == null) { r.Errors.Add(new GraphError(0, "the graph has no master node")); return r; }

            var outputs = new Dictionary<int, (string[] exprs, int[] sizes)>();
            var visiting = new HashSet<int>();
            if (!Visit(g, master, c, outputs, visiting)) return r;
            if (!r.Ok) return r;

            var type = GraphNodeLibrary.Get(master.Type)!;
            string In(string port)
            {
                int k = Array.FindIndex(type.Inputs, p => p.Name == port);
                return InputExpr(g, master, type.Inputs[k], c, outputs);
            }
            bool Connected(string port) => g.Edges.Exists(e => e.ToNode == master.Id && e.ToPort == port);

            c.Line($"s.baseColor = vec4({In("Base Color")}, {In("Alpha")});");
            if (Connected("Alpha Clip Threshold") || Literal(master, type, "Alpha Clip Threshold") > 0f)
                c.Line($"if (s.baseColor.a < {In("Alpha Clip Threshold")}) discard;");
            if (g.Target == GraphTarget.Lit)
            {
                c.Line($"s.metallic = {In("Metallic")};");
                c.Line($"s.roughness = 1.0 - {In("Smoothness")};");
                c.Line(Connected("Normal (Tangent Space)")
                    ? $"s.normal = normalize(v_TBN * normalize({In("Normal (Tangent Space)")}));"
                    : "s.normal = normalize(v_Normal);");
                c.Line($"s.occlusion = {In("Ambient Occlusion")};");
                c.Line($"s.emissive = {In("Emission")};");
            }
            c.Line("s.baseColor.rgb *= v_Custom.rgb;   // the instance's tint (mesh particles)");
            if (!r.Ok) return r;

            var f = new StringBuilder();
            f.Append("// The shader graph's surface (generated by GraphCompiler - do not edit).\n");
            if (c.NeedNoise) f.Append(NoiseFunctions);
            f.Append("void graphSurface(inout Surface s)\n{\n").Append(c.Body).Append("}\n");
            r.Function = f.ToString();
            if (g.Target == GraphTarget.Unlit) r.UnlitColor = "    color = surf.baseColor.rgb;\n    finalAlpha = surf.baseColor.a;\n";
            return r;
        }

        private static float Literal(GraphNode n, GraphNodeType t, string port)
        {
            if (n.Inputs.TryGetValue(port, out var v) && v.Length > 0) return v[0];
            var p = Array.Find(t.Inputs, x => x.Name == port);
            return p.Default is { Length: > 0 } d ? d[0] : 0f;
        }

        // Emits a node (after what it reads); false on a cycle.
        private static bool Visit(ShaderGraph g, GraphNode n, GraphCompileContext c, Dictionary<int, (string[] exprs, int[] sizes)> outputs, HashSet<int> visiting)
        {
            if (outputs.ContainsKey(n.Id)) return true;
            if (!visiting.Add(n.Id)) { c.Result.Errors.Add(new GraphError(n.Id, "the graph goes round in a circle through this node")); return false; }
            var type = GraphNodeLibrary.Get(n.Type);
            if (type == null) { c.Result.Errors.Add(new GraphError(n.Id, $"no such node: '{n.Type}'")); return false; }
            foreach (var e in g.Edges)
            {
                if (e.ToNode != n.Id) continue;
                var from = g.Node(e.FromNode);
                if (from == null) { c.Result.Errors.Add(new GraphError(n.Id, $"its input '{e.ToPort}' is wired to a node that is not there")); return false; }
                if (!Visit(g, from, c, outputs, visiting)) return false;
            }
            visiting.Remove(n.Id);
            if (type.Outputs.Length == 0) { outputs[n.Id] = (Array.Empty<string>(), Array.Empty<int>()); return true; }

            // The dynamic size: the smallest vector wired into a dynamic input (floats are promoted), else 1.
            int dyn = 0;
            foreach (var p in type.Inputs)
            {
                if (p.Size != 0) continue;
                var e = Array.Find(g.Edges.ToArray(), x => x.ToNode == n.Id && x.ToPort == p.Name);
                if (e.ToPort == null) continue;
                int s = OutputSize(g, e, outputs);
                if (s > 1) dyn = dyn == 0 ? s : Math.Min(dyn, s);
            }
            if (n.Type == "Property") dyn = c.PropertySize(n.Settings.GetValueOrDefault("Property", ""));
            if (dyn == 0) dyn = 1;

            var ins = new string[type.Inputs.Length];
            for (int i = 0; i < ins.Length; i++)
            {
                var p = type.Inputs[i];
                ins[i] = InputExpr(g, n, p.Size == 0 ? p with { Size = dyn } : p, c, outputs);
            }
            c.CurrentNode = n.Id;
            string[] exprs = type.Emit(ins, n, c);
            var names = new string[exprs.Length];
            var sizes = new int[exprs.Length];
            for (int k = 0; k < exprs.Length; k++)
            {
                sizes[k] = type.Outputs[k].Size == 0 ? dyn : type.Outputs[k].Size;
                names[k] = $"n{n.Id}_{k}";
                c.Line($"{Glsl(sizes[k])} {names[k]} = {exprs[k]};");
            }
            outputs[n.Id] = (names, sizes);
            return true;
        }

        private static int OutputSize(ShaderGraph g, GraphEdge e, Dictionary<int, (string[] exprs, int[] sizes)> outputs)
        {
            var from = g.Node(e.FromNode);
            var t = from == null ? null : GraphNodeLibrary.Get(from.Type);
            if (t == null || !outputs.TryGetValue(e.FromNode, out var o)) return 1;
            int k = Array.FindIndex(t.Outputs, p => p.Name == e.FromPort);
            return k < 0 ? 1 : o.sizes[k];
        }

        // An input's expression of the port's size: the wire's output converted, else its value or default, else implicit.
        private static string InputExpr(ShaderGraph g, GraphNode n, GraphPort port, GraphCompileContext c, Dictionary<int, (string[] exprs, int[] sizes)> outputs)
        {
            foreach (var e in g.Edges)
            {
                if (e.ToNode != n.Id || e.ToPort != port.Name) continue;
                var from = g.Node(e.FromNode)!;
                var t = GraphNodeLibrary.Get(from.Type)!;
                int k = Array.FindIndex(t.Outputs, p => p.Name == e.FromPort);
                if (k < 0) { c.Result.Errors.Add(new GraphError(n.Id, $"'{from.Type}' has no output '{e.FromPort}'")); return "0.0"; }
                if (Array.FindIndex(GraphNodeLibrary.Get(n.Type)!.Inputs, p => p.Name == port.Name) < 0)
                {
                    c.Result.Errors.Add(new GraphError(n.Id, $"'{n.Type}' has no input '{port.Name}'"));
                    return "0.0";
                }
                var o = outputs[from.Id];
                return Convert(o.exprs[k], o.sizes[k], port.Size);
            }
            if (n.Inputs.TryGetValue(port.Name, out var v)) return Literal(v, port.Size);
            if (port.Default != null) return Literal(port.Default, port.Size);
            return Convert(GraphNodeLibrary.Implicit(port.Name), ImplicitSize(port.Name), port.Size);
        }

        private static int ImplicitSize(string port) => port switch { "UV" => 2, "Position" or "Normal" or "View Dir" => 3, _ => 1 };

        public static string Glsl(int size) => size switch { 1 => "float", 2 => "vec2", 3 => "vec3", _ => "vec4" };

        /// <summary>A value of one size as another (the reference editor's rules: a float fills every component, a longer
        /// vector is cut, a shorter one is padded with 0).</summary>
        public static string Convert(string expr, int from, int to)
        {
            if (from == to) return expr;
            if (from == 1) return $"{Glsl(to)}({expr})";
            if (to == 1) return $"({expr}).x";
            if (from > to) return $"({expr}).{"xyzw"[..to]}";
            return to switch
            {
                3 => $"vec3({expr}, 0.0)",
                _ => from == 2 ? $"vec4({expr}, 0.0, 0.0)" : $"vec4({expr}, 0.0)",
            };
        }

        private static string Literal(float[] v, int size)
        {
            string X(int i) => ShaderGraph.F(i < v.Length ? v[i] : (v.Length == 1 ? v[0] : 0f));
            return size switch
            {
                1 => X(0),
                2 => $"vec2({X(0)}, {X(1)})",
                3 => $"vec3({X(0)}, {X(1)}, {X(2)})",
                _ => $"vec4({X(0)}, {X(1)}, {X(2)}, {X(3)})",
            };
        }

        // The reference editor's Simple Noise: value noise, three octaves.
        private const string NoiseFunctions = @"float graph_rand(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
float graph_value_noise(vec2 uv)
{
    vec2 i = floor(uv), f = fract(uv);
    f = f * f * (3.0 - 2.0 * f);
    float a = graph_rand(i), b = graph_rand(i + vec2(1.0, 0.0)), c = graph_rand(i + vec2(0.0, 1.0)), d = graph_rand(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}
float graph_simple_noise(vec2 uv)
{
    float t = 0.0;
    for (int i = 0; i < 3; i++) t += graph_value_noise(uv / pow(2.0, float(i))) * pow(0.5, float(3 - i));
    return t;
}
";

        // ── The whole shader ────────────────────────────────────────────────────────────────────────────────

        private static readonly Regex Include = new(@"^\s*@include\s+(\S+)\s*$", RegexOptions.Multiline);

        /// <summary>
        /// The graph's shader: <paramref name="engineShaders"/>/pbr.glsl with every @include expanded (so sokol-shdc finds
        /// nothing outside the file) and the GRAPH_ comments filled in. Null - with errors - when the graph did not compile.
        /// </summary>
        public static string? Shader(GraphCompileResult compiled, string engineShaders)
        {
            if (!compiled.Ok) return null;
            string text = Expand(Path.Combine(engineShaders, "pbr.glsl"), new HashSet<string>(StringComparer.Ordinal));
            text = Replace(text, "// GRAPH_SURFACE_FUNCTION", compiled.Function);
            text = Replace(text, "// GRAPH_SURFACE_CALL", "    graphSurface(surf);\n");
            text = Replace(text, "// GRAPH_PARAMS", "    vec4 graph_params[8];   // the shader graph's properties ([0..6]) and time ([7].x)\n");
            if (compiled.UnlitColor.Length > 0) text = Replace(text, "// GRAPH_UNLIT_COLOR", compiled.UnlitColor);
            return text;
        }

        // Every @include replaced by its file, as sokol-shdc does (a file included in two blocks appears in both); a file that
        // includes itself, directly or not, is an error.
        private static string Expand(string file, HashSet<string> stack)
        {
            string full = Path.GetFullPath(file);
            if (!stack.Add(full)) throw new InvalidOperationException($"{Path.GetFileName(full)} includes itself");
            string dir = Path.GetDirectoryName(full)!;
            string text = Include.Replace(File.ReadAllText(full), m => Expand(Path.Combine(dir, m.Groups[1].Value), stack));
            stack.Remove(full);
            return text;
        }

        // Replaces the line holding the marker (the whole comment line).
        private static string Replace(string text, string marker, string with)
        {
            int at = text.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) throw new InvalidOperationException($"pbr.glsl has no '{marker}' line");
            int start = text.LastIndexOf('\n', at) + 1, end = text.IndexOf('\n', at);
            return text[..start] + with + (end < 0 ? "" : text[(end + 1)..]);
        }
    }
}
