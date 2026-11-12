using System;
using System.Numerics;
using System.Collections.Generic;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Lighting;
using GameEditor.Framework.Renderer.Server.Resources;
using GameEditor.Framework.Renderer.Server.Animation;
using static Sokol.SG;
using static Sokol.Utils;
using static shadow_caster_shader_cs.Shaders;
using SkinnedCaster = shadow_caster_skinned_shader_cs.Shaders;
using SkinnedCasterLow = shadow_caster_skinned_low_shader_cs_shadow_caster_skinned_low.Shaders;

namespace GameEditor.Framework.Renderer.Server.Passes
{
    /// <summary>
    /// Renders shadow casters into a depth-only atlas slice.
    /// </summary>
    public sealed class ShadowPass
    {
        private struct PrimitiveShadowMesh
        {
            public sg_buffer VertexBuffer;
            public sg_buffer IndexBuffer;
            public int IndexCount;
        }

        private sg_shader _shader;
        private sg_pipeline _pipeline;
        private sg_pass_action _passAction;
        // Skinned caster: 80 B vertex (pos + joints + weights) + a bone UBO. Rendered into the
        // directional cascades with depth LOAD so the static casters' depth is preserved.
        // Made by the first skinned caster (MakeSkinned), not at Init: a scene with no skinned mesh never compiles it.
        // LOW_TIER (the GPU refused the full one, ShaderVariantCache.LowTier): no bone array, bones from the joint texture.
        private sg_shader _skinnedShader;
        private sg_pipeline _skinnedPipeline;
        private bool _skinnedMade;
        private bool _skinnedLow;
        private sg_pass_action _passActionLoad;
        private sg_image _atlasDummyColorImage;
        private sg_view _atlasDummyColorView;
        private sg_image _cubeDummyColorImage;
        private sg_view _cubeDummyColorView;
        // Joint-matrix texture bindings for the >128-bone skinned caster: a 1×1 sampleable placeholder
        // (bound but never sampled on the ≤128 uniform path) + a NEAREST/nonfiltering sampler matching
        // the shader's `nonfiltering` u_jointsSampler_Smp. The real per-character RGBA32F texture comes
        // from SkinnedCharacterRegistry.Entry on the texture path.
        private sg_image _jointPlaceholderImage;
        private sg_view _jointPlaceholderView;
        private sg_sampler _jointSampler;
        private readonly Dictionary<string, PrimitiveShadowMesh> _primitiveMeshes = new();
        private bool _initialized;

        public void Init()
        {
            if (_initialized) return;

            _shader = sg_make_shader(shadow_caster_shader_desc(sg_query_backend()));

            var desc = new sg_pipeline_desc
            {
                shader = _shader,
                index_type = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode = sg_cull_mode.SG_CULLMODE_FRONT,
                color_count = 1,
                sample_count = 1,
                depth = new sg_depth_state
                {
                    pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                    compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = true
                },
                colors =
                {
                    [0] = new sg_color_target_state
                    {
                        pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                        write_mask = sg_color_mask.SG_COLORMASK_NONE
                    }
                },
                label = "shadow-caster-pipeline"
            };

            desc.layout.buffers[0].stride = 56;   // ObjVertex now carries TEXCOORD_1; caster reads position only
            desc.layout.attrs[ATTR_shadow_caster_in_pos] = new sg_vertex_attr_state
            {
                buffer_index = 0,
                offset = 0,
                format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3,
            };

            _pipeline = sg_make_pipeline(desc);


            _passAction = default;
            _passAction.colors[0].load_action = sg_load_action.SG_LOADACTION_DONTCARE;
            _passAction.colors[0].store_action = sg_store_action.SG_STOREACTION_DONTCARE;
            _passAction.depth.load_action = sg_load_action.SG_LOADACTION_CLEAR;
            _passAction.depth.store_action = sg_store_action.SG_STOREACTION_STORE;
            _passAction.depth.clear_value = 1.0f;

            // Load variant: preserves the slice's existing depth so skinned casters render ON TOP
            // of the static casters already rendered into the cascade (no second clear).
            _passActionLoad = _passAction;
            _passActionLoad.depth.load_action = sg_load_action.SG_LOADACTION_LOAD;

            _atlasDummyColorImage = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_2D,
                width = ShadowAtlas.SliceSize,
                height = ShadowAtlas.SliceSize,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                sample_count = 1,
                usage = { color_attachment = true },
                label = "shadow-pass-dummy-color-atlas"
            });
            _atlasDummyColorView = sg_make_view(new sg_view_desc
            {
                color_attachment = new sg_image_view_desc { image = _atlasDummyColorImage },
                label = "shadow-pass-dummy-color-atlas-view"
            });

            _cubeDummyColorImage = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_2D,
                width = CubeShadowArray.FaceSize,
                height = CubeShadowArray.FaceSize,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                sample_count = 1,
                usage = { color_attachment = true },
                label = "shadow-pass-dummy-color-cube"
            });
            _cubeDummyColorView = sg_make_view(new sg_view_desc
            {
                color_attachment = new sg_image_view_desc { image = _cubeDummyColorImage },
                label = "shadow-pass-dummy-color-cube-view"
            });

            // 1×1 sampleable placeholder for the skinned caster's joints-texture slot (never sampled
            // on the uniform path; the texture path binds the real per-character RGBA32F texture).
            _jointPlaceholderImage = sg_make_image(new sg_image_desc
            {
                width = 1,
                height = 1,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                usage = new sg_image_usage { stream_update = true },
                label = "shadow-joint-placeholder"
            });
            _jointPlaceholderView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = _jointPlaceholderImage },
                label = "shadow-joint-placeholder-view"
            });
            _jointSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter    = sg_filter.SG_FILTER_NEAREST,
                mag_filter    = sg_filter.SG_FILTER_NEAREST,
                mipmap_filter = sg_filter.SG_FILTER_NEAREST,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label = "shadow-joint-sampler"
            });

            _initialized = true;
        }

        public void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;

            if (_skinnedPipeline.id != 0)
            {
                sg_destroy_pipeline(_skinnedPipeline);
                _skinnedPipeline = default;
            }
            if (_skinnedShader.id != 0)
            {
                sg_destroy_shader(_skinnedShader);
                _skinnedShader = default;
            }
            _skinnedMade = false;
            _skinnedLow = false;
            if (_pipeline.id != 0)
            {
                sg_destroy_pipeline(_pipeline);
                _pipeline = default;
            }
            if (_shader.id != 0)
            {
                sg_destroy_shader(_shader);
                _shader = default;
            }

            if (_atlasDummyColorView.id != 0)
            {
                sg_destroy_view(_atlasDummyColorView);
                _atlasDummyColorView = default;
            }
            if (_atlasDummyColorImage.id != 0)
            {
                sg_destroy_image(_atlasDummyColorImage);
                _atlasDummyColorImage = default;
            }
            if (_cubeDummyColorView.id != 0)
            {
                sg_destroy_view(_cubeDummyColorView);
                _cubeDummyColorView = default;
            }
            if (_cubeDummyColorImage.id != 0)
            {
                sg_destroy_image(_cubeDummyColorImage);
                _cubeDummyColorImage = default;
            }
            if (_jointPlaceholderView.id != 0)
            {
                sg_destroy_view(_jointPlaceholderView);
                _jointPlaceholderView = default;
            }
            if (_jointPlaceholderImage.id != 0)
            {
                sg_destroy_image(_jointPlaceholderImage);
                _jointPlaceholderImage = default;
            }
            if (_jointSampler.id != 0)
            {
                sg_destroy_sampler(_jointSampler);
                _jointSampler = default;
            }

            foreach (var kv in _primitiveMeshes)
            {
                if (kv.Value.VertexBuffer.id != 0)
                    sg_destroy_buffer(kv.Value.VertexBuffer);
                if (kv.Value.IndexBuffer.id != 0)
                    sg_destroy_buffer(kv.Value.IndexBuffer);
            }
            _primitiveMeshes.Clear();
        }

        public void RenderDirectional(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out _);
        }

        public int RenderDirectionalCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return 0;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out int draws);
            return draws;
        }

        /// <summary>
        /// Renders all CSM cascades into consecutive atlas slices starting at <paramref name="baseSlice"/>.
        /// Each element of <paramref name="cascadeVPs"/> corresponds to one cascade, rendered into
        /// slice <c>baseSlice + i</c>.
        /// </summary>
        /// <returns>Total shadow draw calls across all cascades.</returns>
        public int RenderDirectionalCsmCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int baseSlice,
            ReadOnlySpan<Matrix4x4> cascadeVPs)
        {
            if (!_initialized || !atlas.IsValid) return 0;

            int totalDraws = 0;
            for (int i = 0; i < cascadeVPs.Length; i++)
            {
                Matrix4x4 vp = cascadeVPs[i];
                RenderSlice(world, meshRegistry, _atlasDummyColorView,
                    atlas.GetDepthSliceView(baseSlice + i), in vp, out int draws);
                totalDraws += draws;
            }
            return totalDraws;
        }

        /// <summary>
        /// Renders skinned characters (CastsShadows) into the directional cascade slices with depth
        /// LOAD — so they cast onto static receivers ON TOP of the static casters already in each
        /// slice. Bones come from the registry's animator (which the RenderingServer advances before
        /// the shadow pass). <paramref name="cascadeVPs"/> may hold 4 (CSM4) or 1 (CSM1) VP.
        /// </summary>
        public int RenderSkinnedDirectionalCsm(
            ECSWorld world, ShadowAtlas atlas, int baseSlice, ReadOnlySpan<Matrix4x4> cascadeVPs)
        {
            if (!_initialized || !atlas.IsValid) return 0;
            int total = 0;
            for (int i = 0; i < cascadeVPs.Length; i++)
                total += RenderSkinnedSlice(world, _atlasDummyColorView, atlas.GetDepthSliceView(baseSlice + i), cascadeVPs[i]);
            return total;
        }

        /// <summary>Renders skinned casters into a spot atlas slice (depth LOAD, on top of statics).</summary>
        public int RenderSkinnedSpotCounted(ECSWorld world, ShadowAtlas atlas, int slice, in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return 0;
            return RenderSkinnedSlice(world, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj);
        }

        private void MakeSkinned()
        {
            _skinnedMade = true;
            // ── Skinned caster pipeline: 80 B SkinnedVertex (pos@0 + joints@48 + weights@64) ──
            var backend = sg_query_backend();
            bool lowAvailable = backend == sg_backend.SG_BACKEND_GLES3;
            if (!(ShaderVariantCache.LowTier && lowAvailable))
            {
                _skinnedShader = sg_make_shader(SkinnedCaster.shadow_caster_shader_desc(backend));
                if (sg_query_shader_state(_skinnedShader) == sg_resource_state.SG_RESOURCESTATE_FAILED && lowAvailable)
                {
                    sg_destroy_shader(_skinnedShader);
                    _skinnedShader = default;
                    ShaderVariantCache.MarkLowTier("shadow-caster-skinned");
                }
            }
            if (_skinnedShader.id == 0)
            {
                _skinnedShader = sg_make_shader(SkinnedCasterLow.shadow_caster_skinned_low_shadow_caster_shader_desc(backend));
                _skinnedLow = true;
                if (System.Runtime.CompilerServices.Unsafe.SizeOf<SkinnedCasterLow.shadow_caster_skinned_low_shadow_caster_vs_params_t>() != LowTierUniforms.SkinnedCasterBytes)
                    Logger.Error("[ShadowPass] LOW_TIER skinned caster: block size differs from LowTierUniforms");
            }
            var sdesc = new sg_pipeline_desc
            {
                shader       = _skinnedShader,
                index_type   = sg_index_type.SG_INDEXTYPE_UINT32,
                cull_mode    = sg_cull_mode.SG_CULLMODE_FRONT,
                color_count  = 1,
                sample_count = 1,
                depth = new sg_depth_state
                {
                    pixel_format  = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                    compare       = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = true
                },
                colors =
                {
                    [0] = new sg_color_target_state
                    {
                        pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                        write_mask   = sg_color_mask.SG_COLORMASK_NONE
                    }
                },
                label = "shadow-caster-skinned-pipeline"
            };
            sdesc.layout.buffers[0].stride = 88;   // SkinnedVertex now carries TEXCOORD_1; caster reads pos+joints+weights
            sdesc.layout.attrs[SkinnedCaster.ATTR_shadow_caster_in_pos]    = new sg_vertex_attr_state
            { buffer_index = 0, offset =  0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            sdesc.layout.attrs[SkinnedCaster.ATTR_shadow_caster_joints_0]  = new sg_vertex_attr_state
            { buffer_index = 0, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            sdesc.layout.attrs[SkinnedCaster.ATTR_shadow_caster_weights_0] = new sg_vertex_attr_state
            { buffer_index = 0, offset = 64, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            _skinnedPipeline = sg_make_pipeline(sdesc);
        }

        /// <summary>Renders skinned casters into one cube-map face of a point light (depth LOAD, on top of statics).</summary>
        public int RenderSkinnedPointFaceCounted(ECSWorld world, CubeShadowArray cubeArray, int pointLightIndex, int faceIndex, in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !cubeArray.IsValid) return 0;
            return RenderSkinnedSlice(world, _cubeDummyColorView, cubeArray.GetDepthFaceView(pointLightIndex, faceIndex), in lightViewProj);
        }

        private int RenderSkinnedSlice(ECSWorld world, sg_view colorView, sg_view depthSliceView, in Matrix4x4 lightViewProj)
        {
            if (colorView.id == 0 || depthSliceView.id == 0) return 0;

            int draws = 0;
            var pass = new sg_pass
            {
                action = _passActionLoad,
                attachments = { colors = { [0] = colorView }, depth_stencil = depthSliceView }
            };
            sg_begin_pass(pass);
            try
            {
                bool applied = false;
                foreach (var row in world.Query<ActiveFlag, SkinnedMeshRenderer, Transform>()
                                         .Enumerate<ActiveFlag, SkinnedMeshRenderer, Transform>())
                {
                    if (!row.Item1.Value.Active) continue;
                    ref readonly var smr = ref row.Item2.Value;
                    if (!smr.Visible || !smr.CastsShadows) continue;
                    if (!SkinnedCharacterRegistry.TryGet(smr.CharacterKey, out var entry)) continue;
                    if (!entry.Meshes.TryGetValue(smr.PrimIndex, out var mesh) || mesh.IndexCount == 0) continue;
                    ref readonly var tf = ref row.Item3.Value;

                    // >128-bone rigs can't fit finalBonesMatrices[128] → cast via the joint texture
                    // (use_uniform_skinning=0), the same per-character texture the color pass uses
                    // (uploaded by UpdateSkinnedAnimators before this pass). Else the uniform path.
                    // The caster's shader is made by the first skinned mesh that casts, not before.
                    if (!_skinnedMade) MakeSkinned();
                    if (!applied) { sg_apply_pipeline(_skinnedPipeline); applied = true; }
                    bool useTex = entry.UsesTextureSkinning && entry.JointTextureView.id != 0;
                    if (_skinnedLow && !useTex) continue;   // LOW_TIER: no bone array — the joint texture comes next frame
                    var vsParams = new SkinnedCaster.shadow_caster_vs_params_t
                    {
                        mvp                  = lightViewProj,
                        model                = Transform.GetWorldMatrix(world, tf),
                        use_uniform_skinning = useTex ? 0 : 1,
                    };
                    if (!useTex)
                    {
                        var bones = entry.Animator?.GetFinalBoneMatrices();
                        if (bones != null)
                        {
                            int bc = Math.Min(entry.BoneCount, Math.Min(bones.Length, AnimationConstants.MAX_BONES));
                            for (int b = 0; b < bc; b++) vsParams.finalBonesMatrices[b] = bones[b];
                        }
                    }
                    LowTierUniforms.ApplySkinnedCaster(SkinnedCaster.UB_shadow_caster_vs_params, in vsParams, _skinnedLow);

                    sg_apply_bindings(new sg_bindings
                    {
                        vertex_buffers = { [0] = mesh.VertexBuffer },
                        index_buffer   = mesh.IndexBuffer,
                        views    = { [SkinnedCaster.VIEW_u_jointsSampler_Tex] = useTex ? entry.JointTextureView : _jointPlaceholderView },
                        samplers = { [SkinnedCaster.SMP_u_jointsSampler_Smp]  = _jointSampler },
                    });
                    sg_draw(0, (uint)mesh.IndexCount, 1);
                    draws++;
                }
            }
            finally { sg_end_pass(); }
            return draws;
        }

        public void RenderSpot(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out _);
        }

        public int RenderSpotCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            ShadowAtlas atlas,
            int slice,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !atlas.IsValid) return 0;

            RenderSlice(world, meshRegistry, _atlasDummyColorView, atlas.GetDepthSliceView(slice), in lightViewProj, out int draws);
            return draws;
        }

        public void RenderPointFace(
            ECSWorld world,
            MeshRegistry meshRegistry,
            CubeShadowArray cubeArray,
            int pointLightIndex,
            int faceIndex,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !cubeArray.IsValid) return;

            RenderSlice(world, meshRegistry, _cubeDummyColorView, cubeArray.GetDepthFaceView(pointLightIndex, faceIndex), in lightViewProj, out _);
        }

        public int RenderPointFaceCounted(
            ECSWorld world,
            MeshRegistry meshRegistry,
            CubeShadowArray cubeArray,
            int pointLightIndex,
            int faceIndex,
            in Matrix4x4 lightViewProj)
        {
            if (!_initialized || !cubeArray.IsValid) return 0;

            RenderSlice(world, meshRegistry, _cubeDummyColorView, cubeArray.GetDepthFaceView(pointLightIndex, faceIndex), in lightViewProj, out int draws);
            return draws;
        }

        private void RenderSlice(
            ECSWorld world,
            MeshRegistry meshRegistry,
            sg_view colorView,
            sg_view depthSliceView,
            in Matrix4x4 lightViewProj,
            out int drawCallCount)
        {
            drawCallCount = 0;
            if (colorView.id == 0 || depthSliceView.id == 0) return;

            var pass = new sg_pass
            {
                action = _passAction,
                attachments =
                {
                    colors = { [0] = colorView },
                    depth_stencil = depthSliceView
                }
            };

            sg_begin_pass(pass);
            try
            {
                sg_apply_pipeline(_pipeline);

                foreach (var row in world.Query<ActiveFlag, MeshRenderer, Transform>()
                                         .Enumerate<ActiveFlag, MeshRenderer, Transform>())
                {
                    if (!row.Item1.Value.Active) continue;
                    ref readonly var mr = ref row.Item2.Value;
                    if (!mr.Visible || !mr.CastsShadows) continue;
                    ref readonly var tf = ref row.Item3.Value;

                    Matrix4x4 model = Transform.GetWorldMatrix(world, tf);
                    var vsParams = new shadow_caster_vs_params_t
                    {
                        mvp = model * lightViewProj
                    };
                    sg_apply_uniforms(UB_shadow_caster_vs_params, SG_RANGE(ref vsParams));

                    if (PrimitiveMeshSpec.TryParse(mr.MeshPath, out var primSpec))
                    {
                        PrimitiveShadowMesh prim = GetOrCreatePrimitiveShadowMesh(PrimitiveMeshSpec.ToMeshPath(primSpec));
                        if (prim.IndexCount == 0 || prim.VertexBuffer.id == 0 || prim.IndexBuffer.id == 0)
                            continue;

                        sg_apply_bindings(new sg_bindings
                        {
                            vertex_buffers = { [0] = prim.VertexBuffer },
                            index_buffer = prim.IndexBuffer,
                        });

                        sg_draw(0, (uint)prim.IndexCount, 1);
                        drawCallCount++;
                        continue;
                    }

                    MeshResource? mesh = meshRegistry.GetByPath(mr.MeshPath);
                    if (mesh == null || mesh.SubMeshes == null || mesh.SubMeshes.Length == 0) continue;

                    foreach (MeshSubResource sub in mesh.SubMeshes)
                    {
                        if (sub.IndexCount == 0 || sub.VertexBuffer.id == 0 || sub.IndexBuffer.id == 0)
                            continue;

                        sg_apply_bindings(new sg_bindings
                        {
                            vertex_buffers = { [0] = sub.VertexBuffer },
                            index_buffer = sub.IndexBuffer,
                        });

                        sg_draw(0, (uint)sub.IndexCount, 1);
                        drawCallCount++;
                    }
                }
            }
            finally
            {
                sg_end_pass();
            }
        }

        private PrimitiveShadowMesh GetOrCreatePrimitiveShadowMesh(string key)
        {
            if (_primitiveMeshes.TryGetValue(key, out var cached))
                return cached;

            PrimitiveShadowMesh created = default;
            if (!PrimitiveMeshSpec.TryParse(key, out var spec))
            {
                _primitiveMeshes[key] = created;
                return created;
            }

            var (verts, inds) = PrimitiveMeshGeometry.GetMeshTriangles(spec);
            if (verts.Length == 0 || inds.Length == 0)
            {
                _primitiveMeshes[key] = created;
                return created;
            }

            // One ObjVertex (56 bytes = 14 floats) per vertex, position first: the caster pipeline's stride.
            const int floatsPerVertex = ObjVertex.SizeBytes / sizeof(float);
            float[] packed = new float[verts.Length * floatsPerVertex];
            for (int i = 0; i < verts.Length; i++)
            {
                int o = i * floatsPerVertex;
                packed[o + 0] = verts[i].X;
                packed[o + 1] = verts[i].Y;
                packed[o + 2] = verts[i].Z;
            }

            created.VertexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                data = SG_RANGE(packed),
                label = "shadow-prim-vbuf"
            });
            created.IndexBuffer = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(inds),
                label = "shadow-prim-ibuf"
            });
            created.IndexCount = inds.Length;

            _primitiveMeshes[key] = created;
            return created;
        }
    }
}