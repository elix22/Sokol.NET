using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GameEditor.Framework.Renderer.Server.Materials.Graph
{
    /// <summary>A shader graph's target (G8.10, examples/GameEditor/docs/SHADER_GRAPH_DESIGN.md): lit by the engine's PBR
    /// lighting, or its colour as it is.</summary>
    public enum GraphTarget { Lit, Unlit }

    /// <summary>A graph property's type: a value the material sets (and scripts), or a texture.</summary>
    public enum GraphPropertyType { Float, Vector2, Vector3, Vector4, Color, Texture2D }

    /// <summary>A property of a graph: exposed on its materials (the reference editor's Blackboard).</summary>
    public sealed class GraphProperty
    {
        public string Name = "";
        public GraphPropertyType Type;
        /// <summary>Its default value (x, y, z, w; a colour rgba); a texture's default is <see cref="DefaultTexture"/>.</summary>
        public float[] Default = { 0f, 0f, 0f, 0f };
        public string DefaultTexture = "";
        public float Min = float.NaN, Max = float.NaN;
    }

    /// <summary>A node of a graph: its type (a name of <see cref="GraphNodeLibrary"/>), its place on the canvas and its own
    /// values (an input's value when nothing is connected to it, a property's name, a swizzle mask...).</summary>
    public sealed class GraphNode
    {
        public int Id;
        public string Type = "";
        public float X, Y;
        /// <summary>The value of an input port by its name, when nothing is connected (1-4 numbers).</summary>
        public Dictionary<string, float[]> Inputs = new();
        /// <summary>The node's settings by name (a property's name, a swizzle mask, a texture property, a space...).</summary>
        public Dictionary<string, string> Settings = new();
    }

    /// <summary>A wire: an output port of one node into an input port of another.</summary>
    public readonly record struct GraphEdge(int FromNode, string FromPort, int ToNode, string ToPort);

    /// <summary>
    /// A shader graph (the `.sgraph` file, JSON): its target, properties, nodes and wires. Read and written with
    /// System.Text.Json's DOM and writer (no .NET reflection: a built game may read one).
    /// </summary>
    public sealed class ShaderGraph
    {
        public const int Version = 1;
        public GraphTarget Target = GraphTarget.Lit;
        public List<GraphProperty> Properties = new();
        public List<GraphNode> Nodes = new();
        public List<GraphEdge> Edges = new();

        /// <summary>A new graph of the target: its master node alone (the reference editor's new graph).</summary>
        public static ShaderGraph New(GraphTarget target)
        {
            var g = new ShaderGraph { Target = target };
            g.Nodes.Add(new GraphNode { Id = 1, Type = target == GraphTarget.Lit ? "Lit Master" : "Unlit Master", X = 400, Y = 0 });
            return g;
        }

        public GraphNode? Node(int id)
        {
            foreach (var n in Nodes) if (n.Id == id) return n;
            return null;
        }

        public GraphNode? Master()
        {
            foreach (var n in Nodes) if (n.Type is "Lit Master" or "Unlit Master") return n;
            return null;
        }

        public int NextId()
        {
            int id = 0;
            foreach (var n in Nodes) id = Math.Max(id, n.Id);
            return id + 1;
        }

        /// <summary>Connects an output to an input: a wire already in that input is replaced (an input takes one wire).</summary>
        public void Connect(int fromNode, string fromPort, int toNode, string toPort)
        {
            Edges.RemoveAll(e => e.ToNode == toNode && e.ToPort == toPort);
            Edges.Add(new GraphEdge(fromNode, fromPort, toNode, toPort));
        }

        // ── JSON ─────────────────────────────────────────────────────────────────────────────────────────────

        public string ToJson()
        {
            var o = new JsonWriterOptions { Indented = true };
            using var ms = new System.IO.MemoryStream();
            using (var w = new Utf8JsonWriter(ms, o))
            {
                w.WriteStartObject();
                w.WriteNumber("version", Version);
                w.WriteString("target", Target == GraphTarget.Lit ? "lit" : "unlit");
                w.WriteStartArray("properties");
                foreach (var p in Properties)
                {
                    w.WriteStartObject();
                    w.WriteString("name", p.Name);
                    w.WriteString("type", TypeName(p.Type));
                    if (p.Type == GraphPropertyType.Texture2D) w.WriteString("default", p.DefaultTexture);
                    else Numbers(w, "default", p.Default, Components(p.Type));
                    if (!float.IsNaN(p.Min)) w.WriteNumber("min", p.Min);
                    if (!float.IsNaN(p.Max)) w.WriteNumber("max", p.Max);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("nodes");
                foreach (var n in Nodes)
                {
                    w.WriteStartObject();
                    w.WriteNumber("id", n.Id);
                    w.WriteString("type", n.Type);
                    w.WriteNumber("x", n.X);
                    w.WriteNumber("y", n.Y);
                    if (n.Inputs.Count > 0)
                    {
                        w.WriteStartObject("inputs");
                        foreach (var kv in n.Inputs) Numbers(w, kv.Key, kv.Value, kv.Value.Length);
                        w.WriteEndObject();
                    }
                    if (n.Settings.Count > 0)
                    {
                        w.WriteStartObject("settings");
                        foreach (var kv in n.Settings) w.WriteString(kv.Key, kv.Value);
                        w.WriteEndObject();
                    }
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("edges");
                foreach (var e in Edges)
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(e.FromNode); w.WriteStringValue(e.FromPort);
                    w.WriteNumberValue(e.ToNode); w.WriteStringValue(e.ToPort);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        /// <summary>Reads a graph; null and a reason when the text is no graph.</summary>
        public static ShaderGraph? FromJson(string json, out string error)
        {
            error = "";
            try
            {
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;
                var g = new ShaderGraph
                {
                    Target = r.TryGetProperty("target", out var t) && t.GetString() == "unlit" ? GraphTarget.Unlit : GraphTarget.Lit,
                };
                if (r.TryGetProperty("properties", out var ps))
                    foreach (var p in ps.EnumerateArray())
                    {
                        var prop = new GraphProperty
                        {
                            Name = p.GetProperty("name").GetString() ?? "",
                            Type = TypeOf(p.TryGetProperty("type", out var pt) ? pt.GetString() : null),
                        };
                        if (p.TryGetProperty("default", out var d))
                        {
                            if (d.ValueKind == JsonValueKind.String) prop.DefaultTexture = d.GetString() ?? "";
                            else ReadNumbers(d, prop.Default);
                        }
                        if (p.TryGetProperty("min", out var mn)) prop.Min = mn.GetSingle();
                        if (p.TryGetProperty("max", out var mx)) prop.Max = mx.GetSingle();
                        g.Properties.Add(prop);
                    }
                if (r.TryGetProperty("nodes", out var ns))
                    foreach (var n in ns.EnumerateArray())
                    {
                        var node = new GraphNode
                        {
                            Id = n.GetProperty("id").GetInt32(),
                            Type = n.GetProperty("type").GetString() ?? "",
                            X = n.TryGetProperty("x", out var x) ? x.GetSingle() : 0f,
                            Y = n.TryGetProperty("y", out var y) ? y.GetSingle() : 0f,
                        };
                        if (n.TryGetProperty("inputs", out var ins))
                            foreach (var kv in ins.EnumerateObject())
                            {
                                var v = new float[kv.Value.ValueKind == JsonValueKind.Array ? kv.Value.GetArrayLength() : 1];
                                ReadNumbers(kv.Value, v);
                                node.Inputs[kv.Name] = v;
                            }
                        if (n.TryGetProperty("settings", out var ss))
                            foreach (var kv in ss.EnumerateObject()) node.Settings[kv.Name] = kv.Value.GetString() ?? "";
                        g.Nodes.Add(node);
                    }
                if (r.TryGetProperty("edges", out var es))
                    foreach (var e in es.EnumerateArray())
                        g.Edges.Add(new GraphEdge(e[0].GetInt32(), e[1].GetString() ?? "", e[2].GetInt32(), e[3].GetString() ?? ""));
                if (g.Master() == null) { error = "the graph has no master node"; return null; }
                return g;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                error = ex.Message;
                return null;
            }
        }

        public static int Components(GraphPropertyType t) => t switch
        {
            GraphPropertyType.Float => 1, GraphPropertyType.Vector2 => 2, GraphPropertyType.Vector3 => 3, GraphPropertyType.Texture2D => 0, _ => 4,
        };

        public static string TypeName(GraphPropertyType t) => t switch
        {
            GraphPropertyType.Float => "float", GraphPropertyType.Vector2 => "vector2", GraphPropertyType.Vector3 => "vector3",
            GraphPropertyType.Vector4 => "vector4", GraphPropertyType.Color => "color", _ => "texture2d",
        };

        public static GraphPropertyType TypeOf(string? name) => name switch
        {
            "vector2" => GraphPropertyType.Vector2, "vector3" => GraphPropertyType.Vector3, "vector4" => GraphPropertyType.Vector4,
            "color" => GraphPropertyType.Color, "texture2d" => GraphPropertyType.Texture2D, _ => GraphPropertyType.Float,
        };

        private static void Numbers(Utf8JsonWriter w, string name, float[] v, int count)
        {
            if (count <= 1) { w.WriteNumber(name, v.Length > 0 ? v[0] : 0f); return; }
            w.WriteStartArray(name);
            for (int i = 0; i < count; i++) w.WriteNumberValue(i < v.Length ? v[i] : 0f);
            w.WriteEndArray();
        }

        private static void ReadNumbers(JsonElement e, float[] into)
        {
            if (e.ValueKind == JsonValueKind.Number) { if (into.Length > 0) into[0] = e.GetSingle(); return; }
            int i = 0;
            foreach (var x in e.EnumerateArray()) { if (i < into.Length) into[i] = x.GetSingle(); i++; }
        }

        internal static string F(float v) => v.ToString("0.0#######", CultureInfo.InvariantCulture);
    }
}
