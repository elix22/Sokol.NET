// ShaderVariantCache.cs — maps (MaterialKind, flags) → sg_shader + sg_pipeline.
//
// Depends on blinn_phong_shader_cs.Shaders (auto-generated from
// src/Framework/Renderer/Server/shaders/blinn_phong.glsl by sokol-shdc).
// The shader is compiled for all platforms (glsl430/hlsl5/metal_macos/metal_ios/glsl300es)
// into blinn_phong_shader.cs which is part of Framework.csproj.
//
// Vertex layout (buffer 0, per-vertex — ObjVertex 48 B):
//   loc 0 : in_pos      float3
//   loc 1 : in_normal   float3
//   loc 2 : in_uv       float2
//   loc 3 : in_tangent  float4
//
// Vertex layout (buffer 1, per-instance — InstanceData 80 B):
//   loc 4-7 : in_model_{0..3}  float4  (mat4 row)
//   loc 8   : in_custom        float4

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using static Sokol.SG;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SGlue;
using static blinn_phong_shader_cs.Shaders;
using PbrShaders = pbr_shader_cs_pbr.Shaders;
using PbrCsm1 = pbr_csm1_shader_cs_pbr_csm1.Shaders;
using PbrCsm4 = pbr_csm4_shader_cs_pbr_csm4.Shaders;
using PbrSkinning = pbr_skinning_shader_cs_pbr_skinning.Shaders;
using PbrSkinningCsm4 = pbr_skinning_csm4_shader_cs_pbr_skinning_csm4.Shaders;
using PbrSkinningCsm1 = pbr_skinning_csm1_shader_cs_pbr_skinning_csm1.Shaders;
using PbrSkinMorph = pbr_skinning_morphing_shader_cs_pbr_skinning_morphing.Shaders;
using PbrSkinMorphCsm1 = pbr_skinning_morphing_csm1_shader_cs_pbr_skinning_morphing_csm1.Shaders;
using PbrSkinMorphCsm4 = pbr_skinning_morphing_csm4_shader_cs_pbr_skinning_morphing_csm4.Shaders;
using PbrTransmission = pbr_transmission_shader_cs_pbr_transmission.Shaders;
using BlinnLow = blinn_phong_low_shader_cs_blinn_phong_low.Shaders;
using PbrLow = pbr_low_shader_cs_pbr_low.Shaders;
using PbrCsm1Low = pbr_csm1_low_shader_cs_pbr_csm1_low.Shaders;
using PbrCsm4Low = pbr_csm4_low_shader_cs_pbr_csm4_low.Shaders;
using PbrSkinningLow = pbr_skinning_low_shader_cs_pbr_skinning_low.Shaders;
using PbrSkinningCsm1Low = pbr_skinning_csm1_low_shader_cs_pbr_skinning_csm1_low.Shaders;
using PbrSkinningCsm4Low = pbr_skinning_csm4_low_shader_cs_pbr_skinning_csm4_low.Shaders;
using PbrSkinMorphLow = pbr_skinning_morphing_low_shader_cs_pbr_skinning_morphing_low.Shaders;
using PbrSkinMorphCsm1Low = pbr_skinning_morphing_csm1_low_shader_cs_pbr_skinning_morphing_csm1_low.Shaders;
using PbrSkinMorphCsm4Low = pbr_skinning_morphing_csm4_low_shader_cs_pbr_skinning_morphing_csm4_low.Shaders;
using PbrTransmissionLow = pbr_transmission_low_shader_cs_pbr_transmission_low.Shaders;
using PbrC = pbr_clustered_shader_cs_pbr_clustered.Shaders;
using PbrCsm1C = pbr_csm1_clustered_shader_cs_pbr_csm1_clustered.Shaders;
using PbrCsm4C = pbr_csm4_clustered_shader_cs_pbr_csm4_clustered.Shaders;
using PbrSkinningC = pbr_skinning_clustered_shader_cs_pbr_skinning_clustered.Shaders;
using PbrSkinningCsm1C = pbr_skinning_csm1_clustered_shader_cs_pbr_skinning_csm1_clustered.Shaders;
using PbrSkinningCsm4C = pbr_skinning_csm4_clustered_shader_cs_pbr_skinning_csm4_clustered.Shaders;
using PbrSkinMorphC = pbr_skinning_morphing_clustered_shader_cs_pbr_skinning_morphing_clustered.Shaders;
using PbrSkinMorphCsm1C = pbr_skinning_morphing_csm1_clustered_shader_cs_pbr_skinning_morphing_csm1_clustered.Shaders;
using PbrSkinMorphCsm4C = pbr_skinning_morphing_csm4_clustered_shader_cs_pbr_skinning_morphing_csm4_clustered.Shaders;
using PbrTransmissionC = pbr_transmission_clustered_shader_cs_pbr_transmission_clustered.Shaders;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server.Materials;

namespace GameEditor.Framework.Renderer.Server
{
    [Flags]
    public enum PipelineFlags : byte
    {
        None         = 0,
        AlphaBlend   = 1 << 0,
        DoubleSided  = 1 << 1,
        OffscreenRt  = 1 << 2,   // rendering to an offscreen RenderView
        Hdr          = 1 << 3,   // ... a float one (G8.3: the scene before its tone curve); with OffscreenRt
    }

    public sealed class ShaderVariantCache : IDisposable
    {
        // ── Families ─────────────────────────────────────────────────────────────────
        // A family = one shader + its 8 pipelines (alpha × double-sided × offscreen). A family is made the first time
        // a draw asks for one of its pipelines, never before: a 2D game, or a 3D scene that has no skinned mesh, never
        // compiles the shaders it does not draw with (a low-end GPU spends seconds on each — the PowerVR GE8320 of a
        // Redmi 6A took over a minute for all twelve when they were made at start).
        //
        // LOW_TIER: when the GPU refuses a family's full shader (its uniform limits: the GE8320 has 1024 fragment
        // components, the full light block alone needs 1028), the family is made from its LOW_TIER shader instead
        // (16 lights, 4 spot shadows, 1 point shadow, skinning through the joint texture; GLSL ES 3.0 only) and every
        // family made after it goes straight to LOW_TIER. A GPU that compiles the full shaders never loads a LOW_TIER one.
        private sealed class Family
        {
            public readonly string Name;
            public readonly Func<sg_backend, sg_shader_desc> FullDesc;
            public readonly Func<sg_backend, sg_shader_desc>? LowDesc;
            public readonly Func<sg_shader, PipelineFlags, sg_pipeline> Build;
            public readonly int LowLightBytes;   // the LOW_TIER shader's light block, as sokol-shdc sized it (0 = not checked)
            public readonly int ExpectedLowBytes;
            public readonly sg_pipeline[] Pipelines = new sg_pipeline[16];   // the flags' 4 bits (Hdr: made when the GPU has an HDR format)
            public sg_shader Shader;
            public bool Made;
            public bool Low;
            /// <summary>This family's Forward+ (CLUSTERED) shader, made when a Forward+ view first draws with it.</summary>
            public Family? Clustered;

            public Family(string name, Func<sg_backend, sg_shader_desc> full, Func<sg_backend, sg_shader_desc>? low,
                          Func<sg_shader, PipelineFlags, sg_pipeline> build, int lowLightBytes, int expectedLowBytes)
            {
                Name = name; FullDesc = full; LowDesc = low; Build = build;
                LowLightBytes = lowLightBytes; ExpectedLowBytes = expectedLowBytes;
            }
        }

        private static bool A(PipelineFlags f) => (f & PipelineFlags.AlphaBlend)  != 0;
        private static bool D(PipelineFlags f) => (f & PipelineFlags.DoubleSided) != 0;
        private static bool O(PipelineFlags f) => (f & PipelineFlags.OffscreenRt) != 0;

        private static readonly int PbrLowLight = Unsafe.SizeOf<PbrLow.pbr_low_pbr_light_params_t>();

        private readonly Family _blinn = new("blinn-phong",
            blinn_phong_shader_desc, BlinnLow.blinn_phong_low_blinn_phong_shader_desc,
            (s, f) => BuildPipeline(s, A(f), D(f), O(f)),
            Unsafe.SizeOf<BlinnLow.blinn_phong_low_blinn_phong_fs_params_t>(), LowTierUniforms.BlinnFsBytes);
        private readonly Family _pbr = new("pbr",
            PbrShaders.pbr_pbr_program_shader_desc, PbrLow.pbr_low_pbr_program_shader_desc,
            (s, f) => BuildPbrPipeline(s, A(f), D(f), O(f)), PbrLowLight, LowTierUniforms.PbrLightBytes);
        private readonly Family _pbrCsm1 = new("pbr-csm1",
            PbrCsm1.pbr_csm1_pbr_program_shader_desc, PbrCsm1Low.pbr_csm1_low_pbr_program_shader_desc,
            (s, f) => BuildPbrCsm1Pipeline(s, A(f), D(f), O(f)), PbrLowLight, LowTierUniforms.PbrLightBytes);
        private readonly Family _pbrCsm4 = new("pbr-csm4",
            PbrCsm4.pbr_csm4_pbr_program_shader_desc, PbrCsm4Low.pbr_csm4_low_pbr_program_shader_desc,
            (s, f) => BuildPbrCsm4Pipeline(s, A(f), D(f), O(f)), PbrLowLight, LowTierUniforms.PbrLightBytes);
        // The skinning families share one 80 B vertex layout, so all six build with BuildPbrSkinningPipeline;
        // their check is the vertex block without its bones (LowTierUniforms.ApplySkinnedVs).
        private readonly Family _pbrSkin = new("pbr-skinning",
            PbrSkinning.pbr_skinning_pbr_program_shader_desc, PbrSkinningLow.pbr_skinning_low_pbr_program_shader_desc,
            (s, f) => BuildPbrSkinningPipeline(s, A(f), D(f), O(f)),
            Unsafe.SizeOf<PbrSkinningLow.pbr_skinning_low_pbr_vs_params_t>(), LowTierUniforms.SkinnedVsBytes(0));
        private readonly Family _pbrSkinCsm1 = new("pbr-skinning-csm1",
            PbrSkinningCsm1.pbr_skinning_csm1_pbr_program_shader_desc, PbrSkinningCsm1Low.pbr_skinning_csm1_low_pbr_program_shader_desc,
            (s, f) => BuildPbrSkinningPipeline(s, A(f), D(f), O(f)),
            Unsafe.SizeOf<PbrSkinningCsm1Low.pbr_skinning_csm1_low_pbr_vs_params_t>(), LowTierUniforms.SkinnedVsBytes(1));
        private readonly Family _pbrSkinCsm4 = new("pbr-skinning-csm4",
            PbrSkinningCsm4.pbr_skinning_csm4_pbr_program_shader_desc, PbrSkinningCsm4Low.pbr_skinning_csm4_low_pbr_program_shader_desc,
            (s, f) => BuildPbrSkinningPipeline(s, A(f), D(f), O(f)),
            Unsafe.SizeOf<PbrSkinningCsm4Low.pbr_skinning_csm4_low_pbr_vs_params_t>(), LowTierUniforms.SkinnedVsBytes(4));
        // Skinning + morph targets (morph-only meshes take this path too, with identity bones).
        private readonly Family _pbrSkinMorph = new("pbr-skinning-morphing",
            PbrSkinMorph.pbr_skinning_morphing_pbr_program_shader_desc, PbrSkinMorphLow.pbr_skinning_morphing_low_pbr_program_shader_desc,
            (s, f) => BuildPbrSkinningPipeline(s, A(f), D(f), O(f)),
            Unsafe.SizeOf<PbrSkinMorphLow.pbr_skinning_morphing_low_pbr_vs_params_t>(), LowTierUniforms.SkinnedVsBytes(0));
        private readonly Family _pbrSkinMorphCsm1 = new("pbr-skinning-morphing-csm1",
            PbrSkinMorphCsm1.pbr_skinning_morphing_csm1_pbr_program_shader_desc, PbrSkinMorphCsm1Low.pbr_skinning_morphing_csm1_low_pbr_program_shader_desc,
            (s, f) => BuildPbrSkinningPipeline(s, A(f), D(f), O(f)),
            Unsafe.SizeOf<PbrSkinMorphCsm1Low.pbr_skinning_morphing_csm1_low_pbr_vs_params_t>(), LowTierUniforms.SkinnedVsBytes(1));
        private readonly Family _pbrSkinMorphCsm4 = new("pbr-skinning-morphing-csm4",
            PbrSkinMorphCsm4.pbr_skinning_morphing_csm4_pbr_program_shader_desc, PbrSkinMorphCsm4Low.pbr_skinning_morphing_csm4_low_pbr_program_shader_desc,
            (s, f) => BuildPbrSkinningPipeline(s, A(f), D(f), O(f)),
            Unsafe.SizeOf<PbrSkinMorphCsm4Low.pbr_skinning_morphing_csm4_low_pbr_vs_params_t>(), LowTierUniforms.SkinnedVsBytes(4));
        // Transmission (screen-space refraction): static meshes only; no alpha-blend variant of its own.
        private readonly Family _pbrTransmission = new("pbr-transmission",
            PbrTransmission.pbr_transmission_pbr_program_shader_desc, PbrTransmissionLow.pbr_transmission_low_pbr_program_shader_desc,
            (s, f) => BuildPbrTransmissionPipeline(s, D(f), O(f)), PbrLowLight, LowTierUniforms.PbrLightBytes);

        private readonly Family[] All;
        private bool _disposed;

        public ShaderVariantCache()
        {
            // Forward+ (G8.2, CLUSTERED_LIGHTING_DESIGN.md): the same pipelines (vertex layouts) over the CLUSTERED shaders.
            _pbr.Clustered              = new("pbr-clustered", PbrC.pbr_clustered_pbr_program_shader_desc, null, _pbr.Build, 0, 0);
            _pbrCsm1.Clustered          = new("pbr-csm1-clustered", PbrCsm1C.pbr_csm1_clustered_pbr_program_shader_desc, null, _pbrCsm1.Build, 0, 0);
            _pbrCsm4.Clustered          = new("pbr-csm4-clustered", PbrCsm4C.pbr_csm4_clustered_pbr_program_shader_desc, null, _pbrCsm4.Build, 0, 0);
            _pbrSkin.Clustered          = new("pbr-skinning-clustered", PbrSkinningC.pbr_skinning_clustered_pbr_program_shader_desc, null, _pbrSkin.Build, 0, 0);
            _pbrSkinCsm1.Clustered      = new("pbr-skinning-csm1-clustered", PbrSkinningCsm1C.pbr_skinning_csm1_clustered_pbr_program_shader_desc, null, _pbrSkinCsm1.Build, 0, 0);
            _pbrSkinCsm4.Clustered      = new("pbr-skinning-csm4-clustered", PbrSkinningCsm4C.pbr_skinning_csm4_clustered_pbr_program_shader_desc, null, _pbrSkinCsm4.Build, 0, 0);
            _pbrSkinMorph.Clustered     = new("pbr-skinning-morphing-clustered", PbrSkinMorphC.pbr_skinning_morphing_clustered_pbr_program_shader_desc, null, _pbrSkinMorph.Build, 0, 0);
            _pbrSkinMorphCsm1.Clustered = new("pbr-skinning-morphing-csm1-clustered", PbrSkinMorphCsm1C.pbr_skinning_morphing_csm1_clustered_pbr_program_shader_desc, null, _pbrSkinMorphCsm1.Build, 0, 0);
            _pbrSkinMorphCsm4.Clustered = new("pbr-skinning-morphing-csm4-clustered", PbrSkinMorphCsm4C.pbr_skinning_morphing_csm4_clustered_pbr_program_shader_desc, null, _pbrSkinMorphCsm4.Build, 0, 0);
            _pbrTransmission.Clustered  = new("pbr-transmission-clustered", PbrTransmissionC.pbr_transmission_clustered_pbr_program_shader_desc, null, _pbrTransmission.Build, 0, 0);
            var all = new List<Family> { _blinn, _pbr, _pbrCsm1, _pbrCsm4, _pbrSkin, _pbrSkinCsm1, _pbrSkinCsm4,
                                         _pbrSkinMorph, _pbrSkinMorphCsm1, _pbrSkinMorphCsm4, _pbrTransmission };
            foreach (var f in all.ToArray()) if (f.Clustered != null) all.Add(f.Clustered);
            All = all.ToArray();
        }

        /// <summary>
        /// Set by the renderer for each view: the view draws in Forward+ (a perspective view, the project's Rendering
        /// Path Forward+, a GPU that is not LOW_TIER). The pipelines handed out are then the CLUSTERED ones.
        /// </summary>
        public static bool UseClustered { get; set; }

        /// <summary>True once the GPU refused a CLUSTERED shader: Forward from then on (logged once).</summary>
        public static bool ClusteredRefused { get; private set; }

        /// <summary>
        /// Forward+ can be drawn: not LOW_TIER — nor asked for (<see cref="ForceLowTier"/> on a GLES 3 / WebGL 2 GPU, which
        /// a game may set after the start: the next family made is LOW_TIER) — and no CLUSTERED shader refused.
        /// </summary>
        public static bool ClusteredAllowed => !LowTier && !ClusteredRefused && !(ForceLowTier && _gles);
        private static bool _gles;

        /// <summary>True when the pipeline handed out last is a CLUSTERED one: the draw binds the view's cluster data.</summary>
        public bool CurrentClustered { get; private set; }

        /// <summary>
        /// True once a full shader was refused by the GPU (or <see cref="ForceLowTier"/> asked for it): every family
        /// made from then on uses its LOW_TIER shader, and every skinned mesh takes the joint-texture path
        /// (<see cref="Animation.SkinnedCharacterRegistry.Entry.UsesTextureSkinning"/>).
        /// </summary>
        public static bool LowTier { get; private set; }

        /// <summary>
        /// Makes every family LOW_TIER from the start, as on a GPU that refuses the full shaders — so a check can see
        /// the LOW_TIER picture on any GLES 3 / WebGL 2 device. Set it before the first 3D draw; it does nothing on
        /// Metal, D3D11 and desktop GL (the LOW_TIER shaders are GLSL ES 3.0 only).
        /// </summary>
        public static bool ForceLowTier { get; set; }

        /// <summary>
        /// Every skinned mesh reads its bones from the joint texture, not from the bone array of its shader: on LOW_TIER,
        /// and on a GLES 3 / WebGL 2 GPU whose reported vertex uniform components cannot hold that array (128 bones, 2,084
        /// components with the rest of the block). Adreno GPUs report 1,024 and compile the shader all the same, but a bone
        /// past their limit reads nothing right: a dancer's upper body came apart on a Galaxy A71 and an S20 FE.
        /// </summary>
        public static bool TextureSkinning => LowTier || _smallVertexUniforms;
        private static bool _smallVertexUniforms;
        private const int SkinnedVertexComponents = 2084;

        /// <summary>
        /// True when the pipeline handed out last belongs to a LOW_TIER shader: the uniform blocks applied after it go
        /// through <see cref="LowTierUniforms"/> (as sokol's own state: a pipeline is applied, then its uniforms).
        /// </summary>
        public bool CurrentLow { get; private set; }

        /// <summary>The names of the families made, with a shader (a check: which CLUSTERED ones a scene drew with).</summary>
        public IEnumerable<string> MadeNames { get { foreach (var f in All) if (f.Made && f.Shader.id != 0) yield return f.Name; } }

        /// <summary>How many families have been made (a check: a 2D scene makes none).</summary>
        public int FamiliesMade { get { int n = 0; foreach (var f in All) if (f.Made) n++; return n; } }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Init()
        {
            // Nothing is made here: each family is made by the first draw that asks for it (Get).
            var backend = sg_query_backend();
            _gles = backend == sg_backend.SG_BACKEND_GLES3;
            HdrFormat = FindHdrFormat();
            if (ForceLowTier && backend == sg_backend.SG_BACKEND_GLES3) LowTier = true;
            int vertexComponents = sg_query_limits().gl_max_vertex_uniform_components;
            _smallVertexUniforms = backend == sg_backend.SG_BACKEND_GLES3 && vertexComponents < SkinnedVertexComponents;
            if (backend == sg_backend.SG_BACKEND_GLES3)
                unsafe
                {
                    Sokol.SLog.slog_func_native("Framework", 3, 0,
                        $"GPU: vertex uniform components {vertexComponents}; skinning through the joint texture: {(TextureSkinning ? "yes" : "no")}", 0, "ShaderVariantCache.cs", null);
                }
        }

        /// <summary>A full shader named <paramref name="what"/> was refused by the GPU: LOW_TIER from now on.</summary>
        internal static void MarkLowTier(string what)
        {
            if (!LowTier)
                Logger.Warning($"[ShaderVariantCache] the GPU refused the '{what}' shader (its uniform limits) — " +
                               "using the LOW_TIER shaders from now on: 16 lights, 4 spot shadows, 1 point shadow");
            LowTier = true;
        }

        private sg_pipeline Get(Family f, PipelineFlags flags)
        {
            if (UseClustered && f.Clustered is { } c && ClusteredAllowed)
            {
                if (!c.Made) MakeClustered(c);
                if (!ClusteredRefused)
                {
                    CurrentLow = false;
                    CurrentClustered = true;
                    return Pipeline(c, flags);
                }
            }
            CurrentClustered = false;
            if (!f.Made) Make(f);
            CurrentLow = f.Low;
            return Pipeline(f, flags);
        }

        /// <summary>
        /// The float format an HDR target is drawn in (G8.3): RG11B10F when the GPU can draw into it and blend, else
        /// RGBA16F, else none (<see cref="sg_pixel_format._SG_PIXELFORMAT_DEFAULT"/>: no HDR pipelines are made).
        /// </summary>
        public static sg_pixel_format HdrFormat { get; private set; }
        public static bool HasHdr => HdrFormat != sg_pixel_format._SG_PIXELFORMAT_DEFAULT && !ForceNoHdr;
        /// <summary>As on a GPU without float render targets (a check sees the in-shader fallback anywhere).</summary>
        public static bool ForceNoHdr { get; set; }
        private static bool _buildHdr;   // while a family's HDR pipelines are made
        private static sg_pixel_format OffscreenColor => _buildHdr ? HdrFormat : SG_PIXELFORMAT_RGBA8;

        private static sg_pixel_format FindHdrFormat()
        {
            foreach (var f in new[] { sg_pixel_format.SG_PIXELFORMAT_RG11B10F, sg_pixel_format.SG_PIXELFORMAT_RGBA16F })
            {
                var info = sg_query_pixelformat(f);
                if (info.render && info.blend) return f;
            }
            return sg_pixel_format._SG_PIXELFORMAT_DEFAULT;
        }

        // A family's pipeline for these flags, made the first time it is asked for: a family holds only the variants a
        // project draws with (21 families × 16 kinds made at once overran the pipeline pool — G8.3's regression).
        private static sg_pipeline Pipeline(Family f, PipelineFlags flags)
        {
            int i = (int)flags & 0x0F;
            if (f.Pipelines[i].id == 0 && f.Shader.id != 0)
            {
                _buildHdr = (i & (int)PipelineFlags.Hdr) != 0;
                f.Pipelines[i] = f.Build(f.Shader, (PipelineFlags)i);
                _buildHdr = false;
            }
            return f.Pipelines[i];
        }

        // A CLUSTERED family: no LOW_TIER shader of its own — a GPU that refuses it draws in Forward from then on.
        private void MakeClustered(Family f)
        {
            f.Made = true;
            var shader = sg_make_shader(f.FullDesc(sg_query_backend()));
            if (sg_query_shader_state(shader) == sg_resource_state.SG_RESOURCESTATE_FAILED)
            {
                sg_destroy_shader(shader);
                if (!ClusteredRefused)
                    Logger.Warning($"[ShaderVariantCache] the GPU refused the '{f.Name}' shader — Forward (32 lights) from now on");
                ClusteredRefused = true;
                return;
            }
            f.Shader = shader;
            // its pipelines are made as they are asked for (Pipeline)
        }

        private void Make(Family f)
        {
            f.Made = true;
            var backend = sg_query_backend();
            if (ForceLowTier && backend == sg_backend.SG_BACKEND_GLES3) LowTier = true;
            bool lowAvailable = backend == sg_backend.SG_BACKEND_GLES3;
            sg_shader shader = default;
            if (!LowTier || !lowAvailable)
            {
                shader = sg_make_shader(f.FullDesc(backend));
                if (sg_query_shader_state(shader) == sg_resource_state.SG_RESOURCESTATE_FAILED && lowAvailable)
                {
                    sg_destroy_shader(shader);
                    shader = default;
                    MarkLowTier(f.Name);
                }
            }
            if (shader.id == 0)
            {
                shader = sg_make_shader(f.LowDesc!(backend));
                f.Low = true;
                if (f.LowLightBytes != f.ExpectedLowBytes)
                    Logger.Error($"[ShaderVariantCache] LOW_TIER '{f.Name}': block is {f.LowLightBytes} bytes, LowTierUniforms packs {f.ExpectedLowBytes}");
                if (sg_query_shader_state(shader) == sg_resource_state.SG_RESOURCESTATE_FAILED)
                    Logger.Error($"[ShaderVariantCache] the GPU refused the LOW_TIER '{f.Name}' shader too — its meshes are not drawn");
            }
            f.Shader = shader;
            // its pipelines are made as they are asked for (Pipeline)
        }

        /// <summary>Look up the Blinn-Phong pipeline for the given flag combination.</summary>
        public sg_pipeline GetPipeline(PipelineFlags flags) => Get(_blinn, flags);

        /// <summary>Look up the cached PBR (base-variant) pipeline for the given flag combination.</summary>
        public sg_pipeline GetPbrPipeline(PipelineFlags flags) => Get(_pbr, flags);

        /// <summary>PBR pipeline that also samples the 1-cascade directional shadow.</summary>
        public sg_pipeline GetPbrCsm1Pipeline(PipelineFlags flags) => Get(_pbrCsm1, flags);

        /// <summary>PBR pipeline that also samples the 4-cascade directional shadow.</summary>
        public sg_pipeline GetPbrCsm4Pipeline(PipelineFlags flags) => Get(_pbrCsm4, flags);

        /// <summary>PBR pipeline with GPU skinning (80 B per-vertex buffer: +joints_0,+weights_0).</summary>
        public sg_pipeline GetPbrSkinningPipeline(PipelineFlags flags) => Get(_pbrSkin, flags);

        /// <summary>GPU-skinning PBR pipeline that also samples the 4-cascade directional shadow.</summary>
        public sg_pipeline GetPbrSkinningCsm4Pipeline(PipelineFlags flags) => Get(_pbrSkinCsm4, flags);

        /// <summary>GPU-skinning PBR pipeline that also samples the 1-cascade directional shadow.</summary>
        public sg_pipeline GetPbrSkinningCsm1Pipeline(PipelineFlags flags) => Get(_pbrSkinCsm1, flags);

        /// <summary>GPU-skinning + morph-target PBR pipeline (no directional CSM).</summary>
        public sg_pipeline GetPbrSkinMorphPipeline(PipelineFlags flags) => Get(_pbrSkinMorph, flags);

        /// <summary>GPU-skinning + morph-target PBR pipeline that also samples the 1-cascade directional shadow.</summary>
        public sg_pipeline GetPbrSkinMorphCsm1Pipeline(PipelineFlags flags) => Get(_pbrSkinMorphCsm1, flags);

        /// <summary>GPU-skinning + morph-target PBR pipeline that also samples the 4-cascade directional shadow.</summary>
        public sg_pipeline GetPbrSkinMorphCsm4Pipeline(PipelineFlags flags) => Get(_pbrSkinMorphCsm4, flags);

        /// <summary>PBR transmission pipeline (screen-space refraction; samples the captured screen color).</summary>
        public sg_pipeline GetPbrTransmissionPipeline(PipelineFlags flags) => Get(_pbrTransmission, flags);

        // ── pipeline builder ─────────────────────────────────────────────────────────

        private static sg_pipeline BuildPipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode    = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var faceWinding = sg_face_winding.SG_FACEWINDING_CCW; // Sokol default
            Logger.Info($"[ShaderVariantCache] Building pipeline: alpha={alpha} doubleSide={doubleSide} offscreen={offscreen}");
            Logger.Info($"[ShaderVariantCache]   cull_mode={cullMode} face_winding={faceWinding}");
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = faceWinding,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,   // don't write depth for transparent geometry
                },
                label = $"blinn-phong-pip-{(int)(alpha?PipelineFlags.AlphaBlend:0)}"
            };

            // ── Per-vertex attributes (buffer slot 0) ────────────────────────────────
            // ObjVertex layout: vec3 pos + vec3 normal + vec2 uv + vec4 tangent = 48 B
            desc.layout.buffers[0].stride = 56;   // ObjVertex: pos+normal+uv0+tangent+uv1
            desc.layout.attrs[ATTR_blinn_phong_in_pos]     = new sg_vertex_attr_state
            {
                buffer_index = 0, offset =  0,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3
            };
            desc.layout.attrs[ATTR_blinn_phong_in_normal]  = new sg_vertex_attr_state
            {
                buffer_index = 0, offset = 12,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3
            };
            desc.layout.attrs[ATTR_blinn_phong_in_uv]      = new sg_vertex_attr_state
            {
                buffer_index = 0, offset = 24,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2
            };
            desc.layout.attrs[ATTR_blinn_phong_in_tangent] = new sg_vertex_attr_state
            {
                buffer_index = 0, offset = 32,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };

            // ── Per-instance attributes (buffer slot 1) ──────────────────────────────
            // InstanceData layout: mat4 model (64 B) + vec4 custom (16 B) = 80 B
            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;

            desc.layout.attrs[ATTR_blinn_phong_in_model_0] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset =  0,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_model_1] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 16,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_model_2] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 32,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_model_3] = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 48,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };
            desc.layout.attrs[ATTR_blinn_phong_in_custom]  = new sg_vertex_attr_state
            {
                buffer_index = 1, offset = 64,
                format       = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4
            };

            // ── Blend state ──────────────────────────────────────────────────────────
            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled        = true,
                    src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            // ── Pixel format ─────────────────────────────────────────────────────────
            if (offscreen)
            {
                desc.colors[0].pixel_format = OffscreenColor;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                // Swapchain: match the real swapchain formats so Sokol validation passes.
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // ── PBR pipeline builder ───────────────────────────────────────────────────────
        // Same per-vertex (buffer 0, 48 B) + per-instance (buffer 1, 80 B) layout as the
        // blinn-phong pipeline; only the shader and the ATTR_ binding constants differ.
        // Kept separate from BuildPipeline because the skinned PBR variants will extend
        // buffer 0 with joints/weights at attribute locations 9-10.
        private static sg_pipeline BuildPbrPipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-pip"
            };

            // Per-vertex attributes (buffer slot 0) — PbrVertex 48 B: pos+normal+uv+tangent.
            desc.layout.buffers[0].stride = 56;   // ObjVertex: pos+normal+uv0+tangent+uv1
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_texcoord_1] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };

            // Per-instance attributes (buffer slot 1) — InstanceData 80 B: mat4 model + vec4 custom.
            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrShaders.ATTR_pbr_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = OffscreenColor;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // PBR skinning pipeline — per-vertex buffer 0 is 80 B: the base 48 B (pos+normal+uv+tangent)
        // PLUS joints_0 (vec4 @48) + weights_0 (vec4 @64) at attribute locations 9-10. Per-instance
        // buffer 1 (80 B model+custom) is unchanged. The bone matrices ride in the binding-0 UBO.
        private static sg_pipeline BuildPbrSkinningPipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-skinning-pip"
            };

            // Per-vertex attributes (buffer slot 0) — SkinnedVertex 80 B.
            desc.layout.buffers[0].stride = 88;   // SkinnedVertex: +uv1 (TEXCOORD_1)
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_joints_0]   = new sg_vertex_attr_state
            { buffer_index = 0, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_weights_0]  = new sg_vertex_attr_state
            { buffer_index = 0, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_texcoord_1] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 80, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };

            // Per-instance attributes (buffer slot 1) — InstanceData 80 B: mat4 model + vec4 custom.
            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrSkinning.ATTR_pbr_skinning_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = OffscreenColor;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // PBR CSM1 pipeline — identical per-vertex (48 B) + per-instance (80 B) layout as the
        // base PBR pipeline; only the shader and the ATTR_ binding constants differ.
        private static sg_pipeline BuildPbrCsm1Pipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-csm1-pip"
            };

            desc.layout.buffers[0].stride = 56;   // ObjVertex: pos+normal+uv0+tangent+uv1
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_texcoord_1] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };

            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm1.ATTR_pbr_csm1_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = OffscreenColor;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // PBR CSM4 pipeline — identical layout to base/CSM1; only the shader + ATTR constants differ.
        private static sg_pipeline BuildPbrCsm4Pipeline(sg_shader shader, bool alpha, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = !alpha,
                },
                label = "pbr-csm4-pip"
            };

            desc.layout.buffers[0].stride = 56;   // ObjVertex: pos+normal+uv0+tangent+uv1
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_texcoord_1] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };

            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrCsm4.ATTR_pbr_csm4_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (alpha)
            {
                desc.colors[0].blend = new sg_blend_state
                {
                    enabled          = true,
                    src_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA,
                    dst_factor_rgb   = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
                    src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE,
                    dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ZERO,
                };
            }

            if (offscreen)
            {
                desc.colors[0].pixel_format = OffscreenColor;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // PBR transmission pipeline — identical 48 B vertex + 80 B instance layout to base PBR.
        // No alpha blend: transmissive surfaces write an opaque colour sampled (refracted) from the
        // captured screen texture. Depth write is ON so multiple glass layers occlude correctly; the
        // transmission pass LOADs the opaque depth so glass is occluded by opaque geometry in front.
        private static sg_pipeline BuildPbrTransmissionPipeline(sg_shader shader, bool doubleSide, bool offscreen)
        {
            var cullMode = doubleSide ? sg_cull_mode.SG_CULLMODE_NONE : sg_cull_mode.SG_CULLMODE_BACK;
            var desc = new sg_pipeline_desc
            {
                shader       = shader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = cullMode,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    pixel_format  = SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = true,
                },
                label = "pbr-transmission-pip"
            };

            desc.layout.buffers[0].stride = 56;   // ObjVertex: pos+normal+uv0+tangent+uv1
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_position]   = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_normal]     = new sg_vertex_attr_state
            { buffer_index = 0, offset = 12, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_texcoord_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 24, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_tangent]    = new sg_vertex_attr_state
            { buffer_index = 0, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_texcoord_1] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT2 };

            desc.layout.buffers[1].stride    = 80;
            desc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_in_model_0] = new sg_vertex_attr_state
            { buffer_index = 1, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_in_model_1] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_in_model_2] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_in_model_3] = new sg_vertex_attr_state
            { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            desc.layout.attrs[PbrTransmission.ATTR_pbr_transmission_pbr_program_in_custom]  = new sg_vertex_attr_state
            { buffer_index = 1, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };

            if (offscreen)
            {
                desc.colors[0].pixel_format = OffscreenColor;
                desc.depth.pixel_format     = SG_PIXELFORMAT_DEPTH;
                desc.sample_count           = 1;
            }
            else
            {
                var sc = sglue_swapchain();
                desc.colors[0].pixel_format = sc.color_format;
                desc.depth.pixel_format     = sc.depth_format;
                desc.sample_count           = sc.sample_count;
            }

            return sg_make_pipeline(desc);
        }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Shutdown()
        {
            foreach (var f in All)
            {
                for (int i = 0; i < f.Pipelines.Length; i++)
                {
                    if (f.Pipelines[i].id != 0) sg_destroy_pipeline(f.Pipelines[i]);
                    f.Pipelines[i] = default;
                }
                if (f.Shader.id != 0) sg_destroy_shader(f.Shader);
                f.Shader = default;
                f.Made = false;
                f.Low = false;
            }
            CurrentLow = false;
            CurrentClustered = false;
            LowTier = false;
            ClusteredRefused = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
        }
    }
}
