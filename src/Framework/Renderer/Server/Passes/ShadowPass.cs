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
            public Aabb Bounds;   // local: the slice's culling
        }

        // ── Instanced static casters (2026-11-16) ─────────────────────────────────────
        // A slice drew every caster of the world, each sub-mesh a draw of its own (a town of 1,030 objects: 1,562 draws a
        // map, 6,248 with four cascades). Now a caster outside the slice's light frustum is left out (it gives the map no
        // pixel: the picture is the same), and the rest are drawn one instanced draw per sub-mesh.
        private sg_shader _instShader;
        private sg_pipeline _instPipeline, _instPointPipeline;
        private InstanceBuffer? _instances;
        private int _instancesThisFrame;
        private const int InstanceCapacity = RenderingConstants.MAX_VIEWS * RenderingConstants.MAX_DRAWS;   // the buffer's, per frame

        private struct CasterDraw
        {
            public sg_buffer Vb, Ib;
            public int IndexCount;
            public Matrix4x4 Model;
            public ulong Key;   // the sub-mesh: its vertex and index buffers
        }
        private CasterDraw[] _draws = new CasterDraw[1024];
        private ulong[] _keys = new ulong[1024];
        private int[] _order = new int[1024];
        private int _drawCount;

        // ── The frame's casters and receivers, gathered once (every slice and view of the frame uses them) ─────────
        // Gathered once a frame - the ECS walked once, a mesh found by its path through a cache (a primitive's name was parsed
        // for every caster of every slice) - and their world matrices and boxes worked out in parallel (on the web in order).
        private struct Caster
        {
            public Transform Tf;
            public MeshResource? Mesh;
            public PrimitiveShadowMesh Prim;
            public bool IsPrim, Casts, Receives, BoxKnown, InSlice, Lightmapped;
            public Matrix4x4 Model;
            public Aabb Box;   // world
        }
        private Caster[] _casters = new Caster[1024];

        /// <summary>The maps rendered now leave out the lightmapped casters (the shadowmask holds their shadow: the sun's maps
        /// of a view whose level uses it).</summary>
        public bool SkipLightmapped { get; set; }
        private int _casterCount;
        private bool _castersReady;
        private ECSWorld? _castersWorld;
        private readonly Dictionary<string, (bool IsPrim, string Key)> _meshKinds = new();

        /// <summary>The frame starts: the instance buffer is appended to anew, the casters gathered again.</summary>
        public void BeginFrame()
        {
            _instances?.BeginFrame();
            _instancesThisFrame = 0;
            _castersReady = false;
        }

        private void EnsureCasters(ECSWorld world, MeshRegistry meshRegistry)
        {
            if (_castersReady && ReferenceEquals(_castersWorld, world)) return;
            _casterCount = 0;
            bool baked = GI.Lightmaps.Active;
            foreach (var row in world.Query<ActiveFlag, MeshRenderer, Transform>().EnumerateWithEntities<ActiveFlag, MeshRenderer, Transform>())
            {
                if (!row.Item1.Value.Active) continue;
                ref readonly var mr = ref row.Item2.Value;
                if (!mr.Visible || (!mr.CastsShadows && !mr.ReceivesShadows) || mr.MeshPath == null) continue;
                if (!_meshKinds.TryGetValue(mr.MeshPath, out var kind))
                {
                    kind = PrimitiveMeshSpec.TryParse(mr.MeshPath, out var spec) ? (true, PrimitiveMeshSpec.ToMeshPath(spec)) : (false, mr.MeshPath);
                    _meshKinds[mr.MeshPath] = kind;
                }
                var c = new Caster { Tf = row.Item3.Value, IsPrim = kind.IsPrim, Casts = mr.CastsShadows, Receives = mr.ReceivesShadows,
                                     Lightmapped = baked && mr.ContributeGI && GI.Lightmaps.MeshFor(world.GetSceneId(row.Entity)) != null };
                if (kind.IsPrim)
                {
                    c.Prim = GetOrCreatePrimitiveShadowMesh(kind.Key);
                    if (c.Prim.IndexCount == 0 || c.Prim.VertexBuffer.id == 0 || c.Prim.IndexBuffer.id == 0) continue;
                }
                else
                {
                    c.Mesh = meshRegistry.GetByPath(mr.MeshPath);   // looked up each frame: a mesh loads later, or again
                    if (c.Mesh == null || c.Mesh.SubMeshes == null || c.Mesh.SubMeshes.Length == 0) continue;
                }
                if (_casterCount == _casters.Length) Array.Resize(ref _casters, _casters.Length * 2);
                _casters[_casterCount++] = c;
            }
            var casters = _casters;
            Concurrency.ParallelWork.For(_casterCount, 128, (from, to) =>
            {
                for (int i = from; i < to; i++)
                {
                    ref var c = ref casters[i];
                    c.Model = Transform.GetWorldMatrix(world, c.Tf);
                    Aabb local = c.IsPrim ? c.Prim.Bounds : c.Mesh!.LocalBounds;
                    c.BoxKnown = local.Min.X <= local.Max.X;
                    if (c.BoxKnown) c.Box = local.Transform(c.Model);
                }
            });
            _castersReady = true;
            _castersWorld = world;
        }

        private void AddDraw(sg_buffer vb, sg_buffer ib, int indexCount, in Matrix4x4 model)
        {
            if (_drawCount == _draws.Length)
            {
                Array.Resize(ref _draws, _draws.Length * 2);
                Array.Resize(ref _keys, _draws.Length);
                Array.Resize(ref _order, _draws.Length);
            }
            _draws[_drawCount] = new CasterDraw { Vb = vb, Ib = ib, IndexCount = indexCount, Model = model,
                                                  Key = ((ulong)vb.id << 32) | ib.id };
            _drawCount++;
        }

        // The view the sun's maps are made for (its camera) and the sun's direction: a caster whose shadow cannot reach
        // that view is left out too. Set for the sun's slices only (a spot's or a point's map is bounded by its range).
        private Frustum _receivers;
        private Vector3 _sunDir;
        private bool _cullByReceivers;

        private Aabb _receiverBox;      // the shadow receivers the camera sees, one box round them all
        private bool _receiverBoxKnown; // false: not limited (a skinned mesh receives - its bounds are not known here)

        /// <summary>The sun's slices that follow are for this camera: a caster whose shadow cannot fall on a receiver in its
        /// view is left out.</summary>
        public void SetReceivers(ECSWorld world, MeshRegistry meshRegistry, in Matrix4x4 cameraViewProj, Vector3 lightDirection)
        {
            _receivers = Frustum.ExtractFromViewProj(in cameraViewProj);
            _sunDir = lightDirection;
            _cullByReceivers = lightDirection.LengthSquared() > 1e-8f;

            // Where a shadow can show: the receivers in the view (a camera's frustum reaches under the floor, where nothing
            // receives - a caster whose shadow went only there was drawn).
            _receiverBoxKnown = !world.HasAnyComponent<SkinnedMeshRenderer>();
            if (!_receiverBoxKnown) return;
            EnsureCasters(world, meshRegistry);
            var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
            for (int i = 0; i < _casterCount; i++)
            {
                ref readonly var c = ref _casters[i];
                if (!c.Receives) continue;
                if (!c.BoxKnown) { _receiverBoxKnown = false; return; }   // a receiver of no known size: not limited
                if (!_receivers.Intersects(in c.Box)) continue;
                lo = Vector3.Min(lo, c.Box.Min); hi = Vector3.Max(hi, c.Box.Max);
            }
            _receiverBox = new Aabb(lo, hi);   // none in view: an empty box - no shadow can be seen
        }

        /// <summary>The slices that follow are not the sun's: no camera test.</summary>
        public void ClearReceivers() => _cullByReceivers = false;

        // A caster's world box: in the slice's light frustum, and (the sun's slices) its shadow able to reach the camera's
        // view. A box with no size (not known) is drawn.
        private bool InSlice(in Frustum frustum, in Aabb box, bool known)
        {
            if (!known) return true;
            if (!frustum.Intersects(in box)) return false;
            if (!_cullByReceivers) return true;
            // Its shadow: the box swept along the sun to the map's far side (beyond it the map holds nothing), in the view.
            float t = frustum.SweepToFar(in box, _sunDir);
            if (float.IsPositiveInfinity(t)) return _receivers.SweptIntersects(in box, _sunDir);
            var end = _sunDir * t;
            var lo = Vector3.Min(box.Min, box.Min + end);
            var hi = Vector3.Max(box.Max, box.Max + end);
            if (_receiverBoxKnown)
            {
                // only the part of it where a receiver in the view is
                lo = Vector3.Max(lo, _receiverBox.Min);
                hi = Vector3.Min(hi, _receiverBox.Max);
                if (lo.X > hi.X || lo.Y > hi.Y || lo.Z > hi.Z) return false;
            }
            return _receivers.Intersects(new Aabb(lo, hi));
        }

        private sg_shader _shader;
        private sg_pipeline _pipeline;
        // A point light's cube faces keep the front faces culled: shown right by point_shadow_check, and the far sides at a
        // contact are not what its own bias was tuned for. (The directional cascades and spot lights cull back faces.)
        private sg_pipeline _pointPipeline;
        private sg_pass_action _passAction;
        // Skinned caster: 80 B vertex (pos + joints + weights) + a bone UBO. Rendered into the
        // directional cascades with depth LOAD so the static casters' depth is preserved.
        // Made by the first skinned caster (MakeSkinned), not at Init: a scene with no skinned mesh never compiles it.
        // LOW_TIER (the GPU refused the full one, ShaderVariantCache.LowTier): no bone array, bones from the joint texture.
        private sg_shader _skinnedShader;
        private sg_pipeline _skinnedPipeline;
        private sg_pipeline _skinnedPointPipeline;   // a point light's cube faces: front faces culled (see _pointPipeline)
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
                // Back faces culled: the casters' light-facing sides go into the map. Culling the front faces (as at first,
                // against acne) stored their far sides - at a contact within the depth bias of the floor, so the floor beside
                // an object's base read as lit: a white line between the object and its shadow. The normal-offset and depth
                // biases keep lit surfaces clean (shadow_contact_check). The reference editor culls back faces too.
                cull_mode = sg_cull_mode.SG_CULLMODE_BACK,
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
            desc.cull_mode = sg_cull_mode.SG_CULLMODE_FRONT;
            desc.label = "shadow-caster-point-faces";
            _pointPipeline = sg_make_pipeline(desc);

            // The instanced casters: the vertices' positions (buffer 0, as above) and InstanceData per instance (buffer 1).
            _instShader = sg_make_shader(shadow_caster_instanced_shader_desc(sg_query_backend()));
            var idesc = desc;
            idesc.shader = _instShader;
            idesc.cull_mode = sg_cull_mode.SG_CULLMODE_BACK;
            idesc.label = "shadow-caster-instanced";
            idesc.layout = default;
            idesc.layout.buffers[0].stride = 56;
            idesc.layout.buffers[1].stride = 80;
            idesc.layout.buffers[1].step_func = sg_vertex_step.SG_VERTEXSTEP_PER_INSTANCE;
            idesc.layout.attrs[ATTR_shadow_caster_instanced_in_pos] = new sg_vertex_attr_state
                { buffer_index = 0, offset = 0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT3 };
            idesc.layout.attrs[ATTR_shadow_caster_instanced_in_model_0] = new sg_vertex_attr_state
                { buffer_index = 1, offset = 0, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            idesc.layout.attrs[ATTR_shadow_caster_instanced_in_model_1] = new sg_vertex_attr_state
                { buffer_index = 1, offset = 16, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            idesc.layout.attrs[ATTR_shadow_caster_instanced_in_model_2] = new sg_vertex_attr_state
                { buffer_index = 1, offset = 32, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            idesc.layout.attrs[ATTR_shadow_caster_instanced_in_model_3] = new sg_vertex_attr_state
                { buffer_index = 1, offset = 48, format = sg_vertex_format.SG_VERTEXFORMAT_FLOAT4 };
            _instPipeline = sg_make_pipeline(idesc);
            idesc.cull_mode = sg_cull_mode.SG_CULLMODE_FRONT;
            idesc.label = "shadow-caster-instanced-point-faces";
            _instPointPipeline = sg_make_pipeline(idesc);
            _instances = new InstanceBuffer(RenderingConstants.MAX_DRAWS);


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

            MakeDummies(ShadowAtlas.DefaultSliceSize, CubeShadowArray.DefaultFaceSize);

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

        // The colour attachments a depth-only shadow pass needs, as large as the slices it draws into (G8.5: the quality
        // level's shadow resolution) — made again when the size changes, the old ones freed.
        private int _dummyAtlasSize, _dummyCubeSize;

        /// <summary>The shadow passes draw into slices of these sizes from now on (the atlas's and the cube array's).</summary>
        public void Resize(int atlasSize, int cubeSize)
        {
            if (!_initialized || (atlasSize == _dummyAtlasSize && cubeSize == _dummyCubeSize)) return;
            FreeDummies();
            MakeDummies(atlasSize, cubeSize);
        }

        private void MakeDummies(int atlasSize, int cubeSize)
        {
            _dummyAtlasSize = atlasSize;
            _dummyCubeSize = cubeSize;
            _atlasDummyColorImage = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_2D,
                width = atlasSize,
                height = atlasSize,
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
                width = cubeSize,
                height = cubeSize,
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
        }

        private void FreeDummies()
        {
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
            if (_skinnedPointPipeline.id != 0)
            {
                sg_destroy_pipeline(_skinnedPointPipeline);
                _skinnedPointPipeline = default;
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
            if (_pointPipeline.id != 0)
            {
                sg_destroy_pipeline(_pointPipeline);
                _pointPipeline = default;
            }
            if (_shader.id != 0)
            {
                sg_destroy_shader(_shader);
                _shader = default;
            }
            if (_instPipeline.id != 0) { sg_destroy_pipeline(_instPipeline); _instPipeline = default; }
            if (_instPointPipeline.id != 0) { sg_destroy_pipeline(_instPointPipeline); _instPointPipeline = default; }
            if (_instShader.id != 0) { sg_destroy_shader(_instShader); _instShader = default; }
            _instances?.Dispose();
            _instances = null;

            FreeDummies();
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
                cull_mode    = sg_cull_mode.SG_CULLMODE_BACK,   // as the static casters (a character's feet on the floor)
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
            sdesc.cull_mode = sg_cull_mode.SG_CULLMODE_FRONT;
            sdesc.label = "shadow-caster-skinned-point-faces";
            _skinnedPointPipeline = sg_make_pipeline(sdesc);
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
                    if (!applied) { sg_apply_pipeline(_skinnedPointPipeline); applied = true; }
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
                    if (Diagnostics.FrameDebugger.Draw("shadow caster", mesh.IndexCount, 1)) sg_draw(0, (uint)mesh.IndexCount, 1);
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

            RenderSlice(world, meshRegistry, _cubeDummyColorView, cubeArray.GetDepthFaceView(pointLightIndex, faceIndex), in lightViewProj, out _, point: true);
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

            RenderSlice(world, meshRegistry, _cubeDummyColorView, cubeArray.GetDepthFaceView(pointLightIndex, faceIndex), in lightViewProj, out int draws, point: true);
            return draws;
        }

        private void RenderSlice(
            ECSWorld world,
            MeshRegistry meshRegistry,
            sg_view colorView,
            sg_view depthSliceView,
            in Matrix4x4 lightViewProj,
            out int drawCallCount,
            bool point = false)
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
                // The static casters in the slice's light frustum, then drawn: instanced, one draw per sub-mesh.
                var frustum = Frustum.ExtractFromViewProj(in lightViewProj);
                EnsureCasters(world, meshRegistry);
                var casters = _casters;
                bool skipLightmapped = SkipLightmapped;
                Concurrency.ParallelWork.For(_casterCount, 128, (from, to) =>
                {
                    for (int i = from; i < to; i++)
                    {
                        ref var c = ref casters[i];
                        c.InSlice = c.Casts && !(skipLightmapped && c.Lightmapped) && InSlice(in frustum, in c.Box, c.BoxKnown);
                    }
                });
                _drawCount = 0;
                for (int i = 0; i < _casterCount; i++)
                {
                    ref readonly var c = ref _casters[i];
                    if (!c.InSlice) continue;
                    if (c.IsPrim) { AddDraw(c.Prim.VertexBuffer, c.Prim.IndexBuffer, c.Prim.IndexCount, in c.Model); continue; }
                    foreach (MeshSubResource sub in c.Mesh!.SubMeshes)
                    {
                        if (sub.IndexCount == 0 || sub.VertexBuffer.id == 0 || sub.IndexBuffer.id == 0) continue;
                        AddDraw(sub.VertexBuffer, sub.IndexBuffer, sub.IndexCount, in c.Model);
                    }
                }
                drawCallCount += DrawGathered(in lightViewProj, point);

                sg_apply_pipeline(point ? _pointPipeline : _pipeline);

                // Mesh particles that cast shadows (G8.8 Q6), as gathered this frame.
                var meshBatches = Scene.WorldParticles.MeshBatches;
                var meshParticles = Scene.WorldParticles.Meshes;
                for (int b = 0; b < meshBatches.Length; b++)
                {
                    var batch = meshBatches[b];
                    if (!batch.CastShadows) continue;
                    sg_buffer vb = default, ib = default;
                    int indexCount = 0;
                    if (PrimitiveMeshSpec.TryParse(batch.Mesh, out var pspec))
                    {
                        PrimitiveShadowMesh prim = GetOrCreatePrimitiveShadowMesh(PrimitiveMeshSpec.ToMeshPath(pspec));
                        vb = prim.VertexBuffer; ib = prim.IndexBuffer; indexCount = prim.IndexCount;
                    }
                    else if (meshRegistry.GetByPath(batch.Mesh) is { SubMeshes: { Length: > 0 } subs } && subs[0].IndexCount > 0)
                    {
                        vb = subs[0].VertexBuffer; ib = subs[0].IndexBuffer; indexCount = subs[0].IndexCount;
                    }
                    if (indexCount == 0 || vb.id == 0 || ib.id == 0) continue;
                    sg_apply_bindings(new sg_bindings { vertex_buffers = { [0] = vb }, index_buffer = ib });
                    for (int k = batch.Start; k < batch.Start + batch.Count; k++)
                    {
                        var vsp = new shadow_caster_vs_params_t { mvp = meshParticles[k].Model * lightViewProj };
                        sg_apply_uniforms(UB_shadow_caster_vs_params, SG_RANGE(ref vsp));
                        if (Diagnostics.FrameDebugger.Draw("shadow caster", indexCount, 1)) sg_draw(0, (uint)indexCount, 1);
                        drawCallCount++;
                    }
                }
            }
            finally
            {
                sg_end_pass();
            }
        }

        // The gathered casters: grouped by sub-mesh, their model matrices appended to the instance buffer, a draw per group.
        // Past the buffer's room for the frame (a scene of very many casters and lights): a draw each, as before.
        private int DrawGathered(in Matrix4x4 lightViewProj, bool point)
        {
            if (_drawCount == 0) return 0;
            for (int i = 0; i < _drawCount; i++) { _keys[i] = _draws[i].Key; _order[i] = i; }
            Array.Sort(_keys, _order, 0, _drawCount);
            int draws = 0;
            if (_instances != null && _instancePipelineOk && _instancesThisFrame + _drawCount <= InstanceCapacity)
            {
                _instances.ResetStaging();
                for (int i = 0; i < _drawCount; i++)
                    _instances.TryAppend(new InstanceData(in _draws[_order[i]].Model, Vector4.Zero));
                int baseOffset = _instances.Flush();
                _instancesThisFrame += _drawCount;
                sg_apply_pipeline(point ? _instPointPipeline : _instPipeline);
                var p = new shadow_caster_instanced_vs_params_t { light_view_proj = lightViewProj };
                sg_apply_uniforms(UB_shadow_caster_instanced_vs_params, SG_RANGE(ref p));
                const int InstanceBytes = 80;
                for (int i = 0; i < _drawCount;)
                {
                    int j = i + 1;
                    while (j < _drawCount && _keys[j] == _keys[i]) j++;
                    ref readonly var d = ref _draws[_order[i]];
                    sg_apply_bindings(new sg_bindings
                    {
                        vertex_buffers = { [0] = d.Vb, [1] = _instances.Buffer },
                        vertex_buffer_offsets = { [1] = baseOffset + i * InstanceBytes },
                        index_buffer = d.Ib,
                    });
                    if (Diagnostics.FrameDebugger.Draw("shadow casters", d.IndexCount, j - i)) sg_draw(0, (uint)d.IndexCount, (uint)(j - i));
                    draws++;
                    i = j;
                }
                return draws;
            }
            sg_apply_pipeline(point ? _pointPipeline : _pipeline);
            for (int i = 0; i < _drawCount; i++)
            {
                ref readonly var d = ref _draws[_order[i]];
                var vsParams = new shadow_caster_vs_params_t { mvp = d.Model * lightViewProj };
                sg_apply_uniforms(UB_shadow_caster_vs_params, SG_RANGE(ref vsParams));
                sg_apply_bindings(new sg_bindings { vertex_buffers = { [0] = d.Vb }, index_buffer = d.Ib });
                if (Diagnostics.FrameDebugger.Draw("shadow caster", d.IndexCount, 1)) sg_draw(0, (uint)d.IndexCount, 1);
                draws++;
            }
            return draws;
        }

        private bool _instancePipelineOk => _instPipeline.id != 0 && sg_query_pipeline_state(_instPipeline) == sg_resource_state.SG_RESOURCESTATE_VALID;

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
            var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
            foreach (var v in verts) { lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
            created.Bounds = new Aabb(lo, hi);

            _primitiveMeshes[key] = created;
            return created;
        }
    }
}