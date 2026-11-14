using System.Diagnostics;
using GameEditor.Framework.Renderer.Server.Materials.Graph;
using Xunit;
using static Sokol.SG;
using static Sokol.SG.sg_backend;

namespace Scene.Tests
{
    /// <summary>A shader as data (G8.10, docs SHADER_GRAPH_DESIGN.md §3.2): the engine's own PBR shader built from sokol-shdc's
    /// bare_yaml files is the descriptor its compiled C# makes - every backend, field by field.</summary>
    public sealed class ShaderDataTests
    {
        static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        [Theory]
        [InlineData(SG_BACKEND_GLCORE)]
        [InlineData(SG_BACKEND_GLES3)]
        [InlineData(SG_BACKEND_D3D11)]
        [InlineData(SG_BACKEND_METAL_MACOS)]
        [InlineData(SG_BACKEND_METAL_IOS)]
        public unsafe void The_pbr_shader_from_its_yaml_is_the_compiled_descriptor(sg_backend backend)
        {
            string dir = Path.Combine(Path.GetTempPath(), $"shader_data_test_{Environment.ProcessId}_{backend}");
            Directory.CreateDirectory(dir);
            try
            {
                string shdc = Path.Combine(Root, "tools", "bin", "osx", "arm64", "sokol-shdc");
                if (!File.Exists(shdc)) return;   // macOS arm64 only (the editor's machine)
                var p = Process.Start(new ProcessStartInfo(shdc, $"--input \"{Path.Combine(Root, "src/Framework/Renderer/Server/shaders/pbr.glsl")}\" " +
                    $"--output \"{Path.Combine(dir, "pbr")}\" --slang glsl430:hlsl5:metal_macos:metal_ios:glsl300es --format bare_yaml") { RedirectStandardOutput = true })!;
                p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                Assert.Equal(0, p.ExitCode);
                var data = ShaderData.Load(Directory.GetFiles(dir, "*reflection.yaml").Single());
                Assert.True(data.Describe(backend, "pbr_program", "pbr_pbr_program_shader", out var got, out string error), error);
                var want = pbr_shader_cs_pbr.Shaders.pbr_pbr_program_shader_desc(backend);

                Assert.Equal(S(want.vertex_func.source), S(got.vertex_func.source));
                Assert.Equal(S(want.fragment_func.source), S(got.fragment_func.source));
                Assert.Equal(S(want.vertex_func.entry), S(got.vertex_func.entry));
                Assert.Equal(S(want.fragment_func.entry), S(got.fragment_func.entry));
                Assert.Equal(S(want.vertex_func.d3d11_target), S(got.vertex_func.d3d11_target));
                for (int i = 0; i < 16; i++)
                {
                    Assert.Equal(want.attrs[i].base_type, got.attrs[i].base_type);
                    Assert.Equal(S(want.attrs[i].glsl_name), S(got.attrs[i].glsl_name));
                    Assert.Equal(S(want.attrs[i].hlsl_sem_name), S(got.attrs[i].hlsl_sem_name));
                    Assert.Equal(want.attrs[i].hlsl_sem_index, got.attrs[i].hlsl_sem_index);
                }
                for (int i = 0; i < 8; i++)
                {
                    var (w, g) = (want.uniform_blocks[i], got.uniform_blocks[i]);
                    Assert.Equal((w.stage, w.size, w.layout, w.hlsl_register_b_n, w.msl_buffer_n), (g.stage, g.size, g.layout, g.hlsl_register_b_n, g.msl_buffer_n));
                    for (int k = 0; k < 16; k++)
                        Assert.Equal((w.glsl_uniforms[k].type, w.glsl_uniforms[k].array_count, S(w.glsl_uniforms[k].glsl_name)),
                                     (g.glsl_uniforms[k].type, g.glsl_uniforms[k].array_count, S(g.glsl_uniforms[k].glsl_name)));
                }
                for (int i = 0; i < 32; i++)
                {
                    var (w, g) = (want.views[i].texture, got.views[i].texture);
                    Assert.Equal((w.stage, w.image_type, w.sample_type, w.multisampled, w.hlsl_register_t_n, w.msl_texture_n),
                                 (g.stage, g.image_type, g.sample_type, g.multisampled, g.hlsl_register_t_n, g.msl_texture_n));
                }
                for (int i = 0; i < SG_MAX_SAMPLER_BINDSLOTS; i++)
                {
                    var (w, g) = (want.samplers[i], got.samplers[i]);
                    Assert.Equal((w.stage, w.sampler_type, w.hlsl_register_s_n, w.msl_sampler_n), (g.stage, g.sampler_type, g.hlsl_register_s_n, g.msl_sampler_n));
                }
                for (int i = 0; i < SG_MAX_TEXTURE_SAMPLER_PAIRS; i++)
                {
                    var (w, g) = (want.texture_sampler_pairs[i], got.texture_sampler_pairs[i]);
                    Assert.Equal((w.stage, w.view_slot, w.sampler_slot, S(w.glsl_name)), (g.stage, g.view_slot, g.sampler_slot, S(g.glsl_name)));
                }
            }
            finally { Directory.Delete(dir, true); }
        }

        static string S(string? s) => (s ?? "").TrimEnd((char)0);   // the compiled C# keeps the source's closing NUL
    }
}
