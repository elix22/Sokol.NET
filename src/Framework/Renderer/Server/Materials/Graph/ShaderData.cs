using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using static Sokol.SG;
using static Sokol.SG.sg_backend;

namespace GameEditor.Framework.Renderer.Server.Materials.Graph
{
    /// <summary>
    /// A shader as data (G8.10, docs SHADER_GRAPH_DESIGN.md §3.2): the files of <c>sokol-shdc --format bare_yaml</c> - each
    /// program's source per backend and the <c>_reflection.yaml</c> - turned into an <c>sg_shader_desc</c> at load time,
    /// as the C# that <c>-f sokol_csharp</c> writes does. The editor's live path for a shader graph (a built game has the
    /// compiled C#). The YAML is read by a small parser of its own (indentation, "key: value", "-" items): no .NET
    /// reflection, no YAML library.
    /// </summary>
    public sealed class ShaderData
    {
        private readonly Dictionary<string, object> _root;
        private readonly string _folder;

        private ShaderData(Dictionary<string, object> root, string folder)
        {
            _root = root;
            _folder = folder;
        }

        /// <summary>Reads <paramref name="reflectionYaml"/>; its sources are looked for beside it (by file name: the paths
        /// sokol-shdc wrote are the editor's, absolute).</summary>
        public static ShaderData Load(string reflectionYaml)
            => new(Yaml.Parse(File.ReadAllText(reflectionYaml)), Path.GetDirectoryName(Path.GetFullPath(reflectionYaml))!);

        /// <summary>The slang sokol-shdc names for a backend.</summary>
        public static string Slang(sg_backend backend) => backend switch
        {
            SG_BACKEND_GLCORE => "glsl430",
            SG_BACKEND_GLES3 => "glsl300es",
            SG_BACKEND_D3D11 => "hlsl5",
            SG_BACKEND_METAL_MACOS => "metal_macos",
            SG_BACKEND_METAL_IOS => "metal_ios",
            _ => "",
        };

        private Dictionary<string, object>? Program(sg_backend backend, string program)
        {
            string slang = Slang(backend);
            foreach (var s in List(_root, "shaders"))
                if (Str(s, "slang") == slang)
                    foreach (var p in List(s, "programs"))
                        if (Str(p, "name") == program) return p;
            return null;
        }

        /// <summary>The uniform block of <paramref name="structName"/>: its slot and size (for the material's block, whose
        /// last member is a graph's graph_params - the YAML gives a block's members flattened, not by name).</summary>
        public (int Slot, int Size) UniformBlock(sg_backend backend, string program, string structName)
        {
            var p = Program(backend, program);
            if (p != null)
                foreach (var ub in List(p, "uniform_blocks"))
                    if (Str(ub, "struct_name") == structName) return (Int(ub, "slot"), Int(ub, "size"));
            return (-1, 0);
        }

        /// <summary>The <c>sg_shader_desc</c> of <paramref name="program"/> for <paramref name="backend"/>; false (and why)
        /// when the backend or the program is not in the files.</summary>
        public bool Describe(sg_backend backend, string program, string label, out sg_shader_desc desc, out string error)
        {
            desc = default;
            error = "";
            var p = Program(backend, program);
            if (p == null) { error = $"no program '{program}' for {Slang(backend)}"; return false; }
            var vf = (Dictionary<string, object>)p["vertex_func"];
            var ff = (Dictionary<string, object>)p["fragment_func"];
            desc.vertex_func.source = File.ReadAllText(Path.Combine(_folder, Path.GetFileName(Str(vf, "path"))));
            desc.vertex_func.entry = Str(vf, "entry_point");
            desc.fragment_func.source = File.ReadAllText(Path.Combine(_folder, Path.GetFileName(Str(ff, "path"))));
            desc.fragment_func.entry = Str(ff, "entry_point");
            if (backend == SG_BACKEND_D3D11)
            {
                desc.vertex_func.d3d11_target = "vs_5_0";
                desc.fragment_func.d3d11_target = "ps_5_0";
            }
            foreach (var a in List(p, "attrs"))
            {
                int i = Int(a, "slot");
                desc.attrs[i].base_type = Str(a, "base_type") switch
                {
                    "sint" => sg_shader_attr_base_type.SG_SHADERATTRBASETYPE_SINT,
                    "uint" => sg_shader_attr_base_type.SG_SHADERATTRBASETYPE_UINT,
                    _ => sg_shader_attr_base_type.SG_SHADERATTRBASETYPE_FLOAT,
                };
                if (a.ContainsKey("glsl_name")) desc.attrs[i].glsl_name = Str(a, "glsl_name");
                if (a.ContainsKey("hlsl_sem_name"))
                {
                    desc.attrs[i].hlsl_sem_name = Str(a, "hlsl_sem_name");
                    desc.attrs[i].hlsl_sem_index = (byte)Int(a, "hlsl_sem_index");
                }
            }
            foreach (var ub in List(p, "uniform_blocks"))
            {
                int i = Int(ub, "slot");
                ref var b = ref desc.uniform_blocks[i];
                b.stage = Stage(ub);
                b.layout = sg_uniform_layout.SG_UNIFORMLAYOUT_STD140;
                b.size = (uint)Int(ub, "size");
                if (ub.ContainsKey("hlsl_register_b_n")) b.hlsl_register_b_n = (byte)Int(ub, "hlsl_register_b_n");
                if (ub.ContainsKey("msl_buffer_n")) b.msl_buffer_n = (byte)Int(ub, "msl_buffer_n");
                // GLSL names a member through the block's instance ("_183.view_proj"); a block flattened into one vec4 array
                // is named after its struct (sokol-shdc's own rule).
                int k = 0;
                string structName = Str(ub, "struct_name"), inst = Str(ub, "inst_name");
                foreach (var u in List(ub, "glsl_uniforms"))
                {
                    string name = Str(u, "glsl_name");
                    b.glsl_uniforms[k].type = UniformType(Str(u, "type"));
                    b.glsl_uniforms[k].array_count = (ushort)Int(u, "array_count");
                    b.glsl_uniforms[k].glsl_name = name == structName || inst.Length == 0 ? name : inst + "." + name;
                    k++;
                }
            }
            foreach (var v in List(p, "views"))
            {
                if (!v.TryGetValue("texture", out var to)) continue;
                var t = (Dictionary<string, object>)to;
                int i = Int(t, "slot");
                ref var tex = ref desc.views[i].texture;
                tex.stage = Stage(t);
                tex.image_type = Str(t, "type") switch
                {
                    "cube" => sg_image_type.SG_IMAGETYPE_CUBE, "3d" => sg_image_type.SG_IMAGETYPE_3D,
                    "array" => sg_image_type.SG_IMAGETYPE_ARRAY, _ => sg_image_type.SG_IMAGETYPE_2D,
                };
                tex.sample_type = Str(t, "sample_type") switch
                {
                    "unfilterable_float" => sg_image_sample_type.SG_IMAGESAMPLETYPE_UNFILTERABLE_FLOAT,
                    "depth" => sg_image_sample_type.SG_IMAGESAMPLETYPE_DEPTH,
                    "sint" => sg_image_sample_type.SG_IMAGESAMPLETYPE_SINT,
                    "uint" => sg_image_sample_type.SG_IMAGESAMPLETYPE_UINT,
                    _ => sg_image_sample_type.SG_IMAGESAMPLETYPE_FLOAT,
                };
                tex.multisampled = Str(t, "multisampled") == "true";
                if (t.ContainsKey("hlsl_register_t_n")) tex.hlsl_register_t_n = (byte)Int(t, "hlsl_register_t_n");
                if (t.ContainsKey("msl_texture_n")) tex.msl_texture_n = (byte)Int(t, "msl_texture_n");
            }
            foreach (var s in List(p, "samplers"))
            {
                int i = Int(s, "slot");
                ref var smp = ref desc.samplers[i];
                smp.stage = Stage(s);
                smp.sampler_type = Str(s, "sampler_type") switch
                {
                    "nonfiltering" => sg_sampler_type.SG_SAMPLERTYPE_NONFILTERING,
                    "comparison" => sg_sampler_type.SG_SAMPLERTYPE_COMPARISON,
                    _ => sg_sampler_type.SG_SAMPLERTYPE_FILTERING,
                };
                if (s.ContainsKey("hlsl_register_s_n")) smp.hlsl_register_s_n = (byte)Int(s, "hlsl_register_s_n");
                if (s.ContainsKey("msl_sampler_n")) smp.msl_sampler_n = (byte)Int(s, "msl_sampler_n");
            }
            foreach (var tsp in List(p, "texture_sampler_pairs"))
            {
                int i = Int(tsp, "slot");
                ref var pair = ref desc.texture_sampler_pairs[i];
                pair.stage = Stage(tsp);
                pair.view_slot = (byte)Int(tsp, "view_slot");
                pair.sampler_slot = (byte)Int(tsp, "sampler_slot");
                if (tsp.ContainsKey("glsl_name")) pair.glsl_name = Str(tsp, "glsl_name");
            }
            desc.label = label;
            return true;
        }

        private static sg_shader_stage Stage(Dictionary<string, object> d)
            => Str(d, "stage") == "vertex" ? sg_shader_stage.SG_SHADERSTAGE_VERTEX : sg_shader_stage.SG_SHADERSTAGE_FRAGMENT;

        private static sg_uniform_type UniformType(string t) => t switch
        {
            "float" => sg_uniform_type.SG_UNIFORMTYPE_FLOAT, "vec2" => sg_uniform_type.SG_UNIFORMTYPE_FLOAT2,
            "vec3" => sg_uniform_type.SG_UNIFORMTYPE_FLOAT3, "vec4" => sg_uniform_type.SG_UNIFORMTYPE_FLOAT4,
            "int" => sg_uniform_type.SG_UNIFORMTYPE_INT, "ivec2" => sg_uniform_type.SG_UNIFORMTYPE_INT2,
            "ivec3" => sg_uniform_type.SG_UNIFORMTYPE_INT3, "ivec4" => sg_uniform_type.SG_UNIFORMTYPE_INT4,
            "mat4" => sg_uniform_type.SG_UNIFORMTYPE_MAT4, _ => sg_uniform_type.SG_UNIFORMTYPE_FLOAT4,
        };

        private static string Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) && v is string s ? s : "";

        private static int Int(Dictionary<string, object> d, string key)
            => int.TryParse(Str(d, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        private static IEnumerable<Dictionary<string, object>> List(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v is List<object> l)
                foreach (var x in l) if (x is Dictionary<string, object> m) yield return m;
        }

        /// <summary>The YAML subset sokol-shdc writes: maps of "key: value" / "key:" (a nested block), lists of "-" items on
        /// their own line, two-space indentation. Values are kept as strings.</summary>
        internal static class Yaml
        {
            public static Dictionary<string, object> Parse(string text)
            {
                var lines = new List<(int indent, string text)>();
                foreach (var raw in text.Split('\n'))
                {
                    string l = raw.TrimEnd('\r');
                    if (l.Trim().Length == 0) continue;
                    lines.Add((l.Length - l.TrimStart(' ').Length, l.Trim()));
                }
                int i = 0;
                return (Dictionary<string, object>)Block(lines, ref i, 0);
            }

            private static object Block(List<(int indent, string text)> lines, ref int i, int indent)
            {
                if (i < lines.Count && lines[i].text == "-")
                {
                    var list = new List<object>();
                    while (i < lines.Count && lines[i].indent == indent && lines[i].text == "-")
                    {
                        i++;
                        list.Add(i < lines.Count && lines[i].indent > indent ? Block(lines, ref i, lines[i].indent) : new Dictionary<string, object>());
                    }
                    return list;
                }
                var map = new Dictionary<string, object>(StringComparer.Ordinal);
                while (i < lines.Count && lines[i].indent == indent && lines[i].text != "-")
                {
                    string t = lines[i].text;
                    int colon = t.IndexOf(':');
                    string key = colon < 0 ? t : t[..colon];
                    string value = colon < 0 ? "" : t[(colon + 1)..].Trim();
                    i++;
                    if (value.Length == 0 && i < lines.Count && lines[i].indent > indent) map[key] = Block(lines, ref i, lines[i].indent);
                    else map[key] = value;
                }
                return map;
            }
        }
    }
}
