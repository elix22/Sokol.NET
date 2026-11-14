using System.Diagnostics;
using GameEditor.Framework.Renderer.Server.Materials.Graph;
using Xunit;

namespace Scene.Tests
{
    /// <summary>The shader graph's compiler (G8.10, examples/GameEditor/docs/SHADER_GRAPH_DESIGN.md): the graph's code in
    /// dependency order, the reference editor's size rules, properties' places, errors at their nodes, the file read back,
    /// and the whole shader compiled by sokol-shdc for every variant a graph needs - as data (bare_yaml, the editor's) and
    /// as C# with reflection (a built game's).</summary>
    public sealed class GraphCompilerTests
    {
        static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        static string EngineShaders => Path.Combine(Root, "src", "Framework", "Renderer", "Server", "shaders");

        // A lit graph: the base colour a property tinted by the world position's height, smoothness from a texture's red,
        // emission a noise; a time-driven metallic.
        static ShaderGraph Sample()
        {
            var g = ShaderGraph.New(GraphTarget.Lit);
            g.Properties.Add(new GraphProperty { Name = "Tint", Type = GraphPropertyType.Color, Default = new[] { 1f, 0.5f, 0.25f, 1f } });
            g.Properties.Add(new GraphProperty { Name = "Mask", Type = GraphPropertyType.Texture2D });
            g.Nodes.Add(new GraphNode { Id = 2, Type = "Property", Settings = { ["Property"] = "Tint" } });
            g.Nodes.Add(new GraphNode { Id = 3, Type = "Position" });
            g.Nodes.Add(new GraphNode { Id = 4, Type = "Split" });
            g.Nodes.Add(new GraphNode { Id = 5, Type = "Multiply" });
            g.Nodes.Add(new GraphNode { Id = 6, Type = "Sample Texture 2D", Settings = { ["Texture"] = "Mask" } });
            g.Nodes.Add(new GraphNode { Id = 7, Type = "Simple Noise" });
            g.Nodes.Add(new GraphNode { Id = 8, Type = "Time" });
            g.Nodes.Add(new GraphNode { Id = 9, Type = "Saturate" });
            g.Connect(3, "Out", 4, "In");
            g.Connect(2, "Out", 5, "A");
            g.Connect(4, "G", 5, "B");
            g.Connect(5, "Out", 1, "Base Color");
            g.Connect(6, "R", 1, "Smoothness");
            g.Connect(7, "Out", 1, "Emission");
            g.Connect(8, "Sine Time", 9, "In");
            g.Connect(9, "Out", 1, "Metallic");
            return g;
        }

        [Fact]
        public void A_graph_compiles_to_its_nodes_code_in_dependency_order_with_the_reference_editors_size_rules()
        {
            var r = GraphCompiler.Compile(Sample());
            Assert.True(r.Ok, string.Join("; ", r.Errors));
            string f = r.Function;
            Assert.Contains("void graphSurface(inout Surface s)", f);
            // Multiply's dynamic size: the colour (4) times a float (promoted) - a vec4.
            Assert.Contains("vec4 n2_0 = graph_params[0];", f);
            Assert.Contains("vec4 n5_0 = (n2_0 * vec4(n4_1));", f);
            Assert.True(f.IndexOf("n4_1 =", StringComparison.Ordinal) < f.IndexOf("n5_0 =", StringComparison.Ordinal), "Split before Multiply");
            Assert.Contains("texture(sampler2D(u_BaseColorTexture, u_BaseColorSampler), v_TexCoord0)", f);   // the first texture: slot 0; its UV implicit
            Assert.Contains("s.baseColor = vec4((n5_0).xyz, 1.0);", f);                                       // vec4 → the master's vec3
            Assert.Contains("s.roughness = 1.0 - n6_1;", f);
            Assert.Contains("s.emissive = vec3(n7_0);", f);                                                     // a float fills a vec3
            Assert.Contains("float n8_1 = sin(graph_params[7].x);", f);
            Assert.Contains("graph_simple_noise(", f);
            Assert.Contains("s.normal = normalize(v_Normal);", f);                                             // Normal unconnected
            Assert.Equal(new[] { new GraphPropertySlot("Tint", GraphPropertyType.Color, 0, 4), new GraphPropertySlot("Mask", GraphPropertyType.Texture2D, 0, 4) }, r.Slots);
        }

        [Fact]
        public void Errors_name_their_node_a_cycle_an_unknown_node_or_property_and_too_many_textures()
        {
            var g = ShaderGraph.New(GraphTarget.Lit);
            g.Nodes.Add(new GraphNode { Id = 2, Type = "Add" });
            g.Nodes.Add(new GraphNode { Id = 3, Type = "Add" });
            g.Connect(2, "Out", 3, "A");
            g.Connect(3, "Out", 2, "A");
            g.Connect(3, "Out", 1, "Metallic");
            var r = GraphCompiler.Compile(g);
            Assert.Contains(r.Errors, e => e.Message.Contains("circle") && (e.Node == 2 || e.Node == 3));

            var u = ShaderGraph.New(GraphTarget.Unlit);
            u.Nodes.Add(new GraphNode { Id = 2, Type = "Wobble" });
            u.Connect(2, "Out", 1, "Base Color");
            Assert.Contains(GraphCompiler.Compile(u).Errors, e => e.Node == 2 && e.Message.Contains("Wobble"));

            var p = ShaderGraph.New(GraphTarget.Unlit);
            p.Nodes.Add(new GraphNode { Id = 2, Type = "Property", Settings = { ["Property"] = "Nope" } });
            p.Connect(2, "Out", 1, "Base Color");
            Assert.Contains(GraphCompiler.Compile(p).Errors, e => e.Node == 2 && e.Message.Contains("Nope"));

            var t = ShaderGraph.New(GraphTarget.Lit);
            for (int i = 0; i < 6; i++) t.Properties.Add(new GraphProperty { Name = "T" + i, Type = GraphPropertyType.Texture2D });
            Assert.Contains(GraphCompiler.Compile(t).Errors, e => e.Message.Contains("at most 5 textures"));
        }

        [Fact]
        public void The_file_is_written_and_read_back()
        {
            var g = Sample();
            g.Nodes[1].Inputs["Smoothness"] = new[] { 0.25f };
            var back = ShaderGraph.FromJson(g.ToJson(), out string error)!;
            Assert.Equal("", error);
            Assert.Equal(g.ToJson(), back.ToJson());
            Assert.Equal(g.Edges, back.Edges);
            Assert.Null(ShaderGraph.FromJson("{\"nodes\": []}", out error));
            Assert.Contains("master", error);
        }

        [Fact]
        public void An_unlit_graph_replaces_the_lit_colour_and_the_engine_shader_is_untouched_by_the_markers()
        {
            var u = ShaderGraph.New(GraphTarget.Unlit);
            u.Nodes.Add(new GraphNode { Id = 2, Type = "Color", Inputs = { ["Color"] = new[] { 0f, 1f, 0f, 1f } } });
            u.Connect(2, "Out", 1, "Base Color");
            string shader = GraphCompiler.Shader(GraphCompiler.Compile(u), EngineShaders)!;
            Assert.Contains("color = surf.baseColor.rgb;", shader);
            Assert.Contains("vec4 graph_params[8];", shader);
            Assert.Contains("graphSurface(surf);", shader);
            Assert.DoesNotMatch(@"(?m)^\s*@include", shader);
            Assert.DoesNotContain("GRAPH_SURFACE_CALL", shader);
            string engine = File.ReadAllText(Path.Combine(EngineShaders, "pbr.glsl"));
            Assert.DoesNotMatch(@"(?m)^\s*graphSurface\(surf\);", engine);
        }

        // The variants a graph needs (static meshes): none, CSM1, CSM4, each with Forward+, and LOW_TIER (GLSL ES only).
        public static readonly (string Defines, string Slangs)[] Variants =
        {
            ("", "glsl430:hlsl5:metal_macos:metal_ios:glsl300es"), ("CSM1", "glsl430:hlsl5:metal_macos:metal_ios:glsl300es"),
            ("CSM4", "glsl430:hlsl5:metal_macos:metal_ios:glsl300es"), ("CLUSTERED", "glsl430:hlsl5:metal_macos:metal_ios:glsl300es"),
            ("CLUSTERED:CSM1", "glsl430:hlsl5:metal_macos:metal_ios:glsl300es"), ("CLUSTERED:CSM4", "glsl430:hlsl5:metal_macos:metal_ios:glsl300es"),
            ("LOW_TIER", "glsl300es"), ("LOW_TIER:CSM1", "glsl300es"), ("LOW_TIER:CSM4", "glsl300es"),
        };

        [Fact]
        public void The_graphs_shader_compiles_with_sokol_shdc_for_every_variant_as_data_and_as_csharp_with_reflection()
        {
            string shdc = Path.Combine(Root, "tools", "bin", OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win32" : "linux",
                                       System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64",
                                       OperatingSystem.IsWindows() ? "sokol-shdc.exe" : "sokol-shdc");
            Assert.True(File.Exists(shdc), shdc);
            string dir = Path.Combine(Path.GetTempPath(), "graph_compiler_test_" + Environment.ProcessId);
            Directory.CreateDirectory(dir);
            try
            {
                string glsl = Path.Combine(dir, "sample.glsl");
                File.WriteAllText(glsl, GraphCompiler.Shader(GraphCompiler.Compile(Sample()), EngineShaders)!);
                foreach (var (defines, slangs) in Variants)
                {
                    string tag = defines == "" ? "base" : defines.Replace(':', '_').ToLowerInvariant();
                    foreach (string format in new[] { "bare_yaml", "sokol_csharp" })
                    {
                        string args = $"--input \"{glsl}\" --output \"{Path.Combine(dir, $"sample_{tag}" + (format == "sokol_csharp" ? ".cs" : ""))}\" " +
                                      $"--module graph_sample_{tag} --slang {slangs} --format {format}" + (format == "sokol_csharp" ? " --reflection" : "") +
                                      (defines == "" ? "" : $" --defines {defines}");
                        var p = Process.Start(new ProcessStartInfo(shdc, args) { RedirectStandardOutput = true, RedirectStandardError = true })!;
                        string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                        p.WaitForExit();
                        Assert.True(p.ExitCode == 0, $"{defines} {format}: {output}");
                    }
                }
                string cs = File.ReadAllText(Path.Combine(dir, "sample_base.cs"));
                Assert.Contains("graph_sample_base_pbr_program_shader_desc", cs);
                Assert.Contains("uniform_offset", cs);    // the reflection: places queried by name
                Assert.Contains("\"graph_params\"", cs);    // the C# reflection finds the graph's properties by name
                // The data path's reflection flattens a block (no member names): graph_params is the material block's last
                // member, at its size - 128; the block is the engine's plus those 128 bytes.
                string yaml = File.ReadAllText(Directory.GetFiles(dir, "sample_base*reflection.yaml").Single());
                Assert.Contains("struct_name: pbr_material_params", yaml);
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
