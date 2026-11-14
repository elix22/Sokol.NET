using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Materials.Graph
{
    /// <summary>
    /// A shader graph's compiled shaders, per variant (G8.10, docs SHADER_GRAPH_DESIGN.md §3.2): asked for by the renderer
    /// for a variant and the GPU's backend; answers by name where the graph's properties live (the one query interface of
    /// both kinds: the editor's data, a built game's compiled C# with --reflection).
    /// </summary>
    public interface IGraphShaderSource
    {
        /// <summary>The graph's asset key ("Shaders/Water.sgraph").</summary>
        string Key { get; }
        /// <summary>Changes each time the graph's shaders are replaced (the editor's Save): the renderer remakes its pipelines.</summary>
        int Version { get; }
        bool Describe(string variant, sg_backend backend, out sg_shader_desc desc);
        /// <summary>The byte offset of graph_params in the material block (binding 1) of the variant, and the block's size.</summary>
        (int Offset, int Size) GraphParams(string variant, sg_backend backend);
        /// <summary>Where the graph's properties live (from its compile).</summary>
        IReadOnlyList<GraphPropertySlot> Slots { get; }
        /// <summary>A property's default: its value (1-4 numbers) or its texture's path.</summary>
        (float[] Value, string Texture) Default(string property);
    }

    /// <summary>
    /// The shader graphs a game or the editor can draw with: registered as data by the editor (live, on each Save) or as
    /// compiled C# by a built game's generated <c>GraphShaders.g.cs</c> (a <c>[ModuleInitializer]</c>). Data wins: in the
    /// editor the compiled copy in the game's scripts is older than what was just saved.
    /// </summary>
    public static class GraphShaders
    {
        /// <summary>The variants a graph is compiled in (static meshes): none / CSM1 / CSM4, each also in Forward+, and the
        /// LOW_TIER three; their sokol-shdc defines.</summary>
        public static readonly (string Tag, string Defines)[] Variants =
        {
            ("base", ""), ("csm1", "CSM1"), ("csm4", "CSM4"),
            ("clustered", "CLUSTERED"), ("clustered_csm1", "CLUSTERED:CSM1"), ("clustered_csm4", "CLUSTERED:CSM4"),
            ("low", "LOW_TIER"), ("low_csm1", "LOW_TIER:CSM1"), ("low_csm4", "LOW_TIER:CSM4"),
        };

        /// <summary>A variant's tag: its cascades (0, 1, 4), Forward+, LOW_TIER.</summary>
        public static string Variant(int cascades, bool clustered, bool low)
        {
            string c = cascades == 4 ? "csm4" : cascades == 1 ? "csm1" : "";
            if (low) return c == "" ? "low" : "low_" + c;
            if (clustered) return c == "" ? "clustered" : "clustered_" + c;
            return c == "" ? "base" : c;
        }

        private static readonly Dictionary<string, IGraphShaderSource> _data = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, IGraphShaderSource> _compiled = new(StringComparer.Ordinal);

        public static IGraphShaderSource? Get(string key)
            => _data.TryGetValue(key, out var d) ? d : _compiled.TryGetValue(key, out var c) ? c : null;

        /// <summary>The editor's: a graph's bare_yaml files, a folder per variant (its tag) holding one *reflection.yaml,
        /// and the graph with its compile (its properties' places and defaults).</summary>
        public static void RegisterData(string key, string folder, ShaderGraph graph, GraphCompileResult compiled)
        {
            int version = _data.TryGetValue(key, out var old) ? old.Version + 1 : 1;
            var defaults = new Dictionary<string, (float[], string)>(StringComparer.Ordinal);
            foreach (var p in graph.Properties) defaults[p.Name] = (p.Default, p.DefaultTexture);
            _data[key] = new DataSource(key, folder, version, compiled.Slots.ToArray(), defaults);
        }

        /// <summary>A graph's data registration dropped (its file deleted).</summary>
        public static void Forget(string key) => _data.Remove(key);

        /// <summary>Every data registration dropped (another project opened).</summary>
        public static void ForgetAll() => _data.Clear();

        /// <summary>A built game's (its generated GraphShaders.g.cs): the compiled descriptor of a variant and the place of
        /// graph_params by name (the generated reflection's uniform_offset / uniformblock_size).</summary>
        public static void RegisterCompiled(string key, Func<string, sg_backend, sg_shader_desc> describe, Func<string, int> paramsOffset, Func<string, int> blockSize,
                                            GraphPropertySlot[] slots, (string Name, float[] Value, string Texture)[] defaults)
        {
            var d = new Dictionary<string, (float[], string)>(StringComparer.Ordinal);
            foreach (var (name, value, texture) in defaults) d[name] = (value, texture);
            _compiled[key] = new CompiledSource(key, describe, paramsOffset, blockSize, slots, d);
        }

        private static (float[], string) DefaultOf(Dictionary<string, (float[], string)> d, string name)
            => d.TryGetValue(name, out var v) ? v : (new float[4], "");

        private sealed class DataSource : IGraphShaderSource
        {
            private readonly string _folder;
            private readonly Dictionary<string, ShaderData?> _loaded = new(StringComparer.Ordinal);
            private readonly Dictionary<string, (float[], string)> _defaults;
            public string Key { get; }
            public int Version { get; }
            public IReadOnlyList<GraphPropertySlot> Slots { get; }

            public DataSource(string key, string folder, int version, GraphPropertySlot[] slots, Dictionary<string, (float[], string)> defaults)
            {
                Key = key; _folder = folder; Version = version; Slots = slots; _defaults = defaults;
            }

            public (float[] Value, string Texture) Default(string property) => DefaultOf(_defaults, property);

            private ShaderData? Data(string variant)
            {
                if (_loaded.TryGetValue(variant, out var d)) return d;
                string dir = Path.Combine(_folder, variant);
                string[] yaml = Directory.Exists(dir) ? Directory.GetFiles(dir, "*reflection.yaml") : Array.Empty<string>();
                d = yaml.Length == 1 ? ShaderData.Load(yaml[0]) : null;
                _loaded[variant] = d;
                return d;
            }

            public bool Describe(string variant, sg_backend backend, out sg_shader_desc desc)
            {
                desc = default;
                var d = Data(variant);
                return d != null && d.Describe(backend, "pbr_program", $"graph-{Path.GetFileNameWithoutExtension(Key)}-{variant}", out desc, out _);
            }

            public (int Offset, int Size) GraphParams(string variant, sg_backend backend)
            {
                var d = Data(variant);
                if (d == null) return (-1, 0);
                var (_, size) = d.UniformBlock(backend, "pbr_program", "pbr_material_params");
                // The YAML gives a block's members flattened: graph_params (8 vec4) is the block's last member.
                return size > 0 ? (size - 8 * 16, size) : (-1, 0);
            }
        }

        private sealed class CompiledSource : IGraphShaderSource
        {
            private readonly Func<string, sg_backend, sg_shader_desc> _describe;
            private readonly Func<string, int> _offset, _size;
            private readonly Dictionary<string, (float[], string)> _defaults;
            public string Key { get; }
            public int Version => 1;
            public IReadOnlyList<GraphPropertySlot> Slots { get; }

            public CompiledSource(string key, Func<string, sg_backend, sg_shader_desc> describe, Func<string, int> offset, Func<string, int> size,
                                  GraphPropertySlot[] slots, Dictionary<string, (float[], string)> defaults)
            {
                Key = key; _describe = describe; _offset = offset; _size = size; Slots = slots; _defaults = defaults;
            }

            public (float[] Value, string Texture) Default(string property) => DefaultOf(_defaults, property);

            public bool Describe(string variant, sg_backend backend, out sg_shader_desc desc)
            {
                desc = _describe(variant, backend);
                return desc.vertex_func.source != null || desc.vertex_func.bytecode.size > 0;
            }

            public (int Offset, int Size) GraphParams(string variant, sg_backend backend) => (_offset(variant), _size(variant));
        }
    }
}
