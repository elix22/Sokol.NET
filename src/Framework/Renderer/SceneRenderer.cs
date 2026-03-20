using System.Collections.Generic;
using System.Numerics;
using static Sokol.SG;
using static Sokol.SG.sg_compare_func;
using static Sokol.SG.sg_pixel_format;
using static Sokol.SShape;
using static Sokol.Utils;
using static phong_shader_cs.Shaders;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Renderer
{
    /// <summary>
    /// Renders ECS entities with a MeshRenderer component using a Phong-lit pipeline.
    /// Primitive geometry is built on demand and cached by primitive spec.
    /// </summary>
    public static unsafe class SceneRenderer
    {
        private const int MaxCachedMeshes = 128;

        private struct MeshResource
        {
            public sg_buffer VertexBuffer;
            public sg_buffer IndexBuffer;
            public uint BaseElement;
            public uint NumElements;
        }

        private static sg_pipeline _pip;
        private static readonly Dictionary<string, MeshResource> _meshCache = new();
        private static bool _initialized;

        // Default light for the scene view
        public static Vector3 LightDir     = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.3f));
        public static Vector3 LightColor   = new Vector3(1f, 0.95f, 0.85f);
        public static Vector3 AmbientColor = new Vector3(0.12f, 0.12f, 0.15f);

        public static void Init()
        {
            _pip = sg_make_pipeline(new sg_pipeline_desc
            {
                shader = sg_make_shader(phong_shader_desc(sg_query_backend())),
                layout =
                {
                    attrs =
                    {
                        [ATTR_phong_in_pos]    = sshape_position_vertex_attr_state(),
                        [ATTR_phong_in_normal] = sshape_normal_vertex_attr_state(),
                        [ATTR_phong_in_uv]     = sshape_texcoord_vertex_attr_state(),
                        [ATTR_phong_in_color]  = sshape_color_vertex_attr_state()
                    },
                    buffers = { [0] = sshape_vertex_buffer_layout_state() }
                },
                index_type = sg_index_type.SG_INDEXTYPE_UINT16,
                cull_mode = sg_cull_mode.SG_CULLMODE_BACK,
#if GAME_EDITOR
                sample_count = 1,
                depth = new sg_depth_state
                {
                    pixel_format = SG_PIXELFORMAT_DEPTH,
                    compare = SG_COMPAREFUNC_LESS_EQUAL,
                    write_enabled = true
                },
                colors = { [0] = new sg_color_target_state { pixel_format = SG_PIXELFORMAT_RGBA8 } },
#endif
                label = "scene-pipeline"
            });

            _initialized = true;
        }

        public static void DrawScene(Matrix4x4 viewProj)
        {
            if (!_initialized) return;

            var world = ECSWorld.Instance;
            if (world.Entities.Count == 0) return;

            // Aggressively release cached primitive meshes that are no longer referenced
            // by any entity, so replaced meshes free GPU buffers quickly.
            ReleaseUnreferencedMeshes(world);

            sg_apply_pipeline(_pip);

            // Gather up to 16 active lights from ECS (directional / point / spot).
            const int MaxLights = 16;
            var fsParams = new phong_fs_params_t();
            int lightCount = 0;

            foreach (int lid in world.Entities)
            {
                if (lightCount >= MaxLights) break;
                if (!world.TryGetComponent<ActiveFlag>(lid, out var la) || !la.Active) continue;
                if (!world.TryGetComponent<LightComponent>(lid, out var lc)) continue;
                if (!world.TryGetComponent<Transform>(lid, out var lt)) continue;

                var rot = Matrix4x4.CreateFromYawPitchRoll(
                    lt.EulerAngles.Y * MathF.PI / 180f,
                    lt.EulerAngles.X * MathF.PI / 180f,
                    lt.EulerAngles.Z * MathF.PI / 180f);
                var fwd = new Vector3(rot.M31, rot.M32, rot.M33);

                int b = lightCount * 4;

                if (lc.Type == LightType.Directional)
                {
                    fsParams.lights_data[b + 0] = new Vector4(fwd, 0f);
                    fsParams.lights_data[b + 1] = Vector4.Zero;
                }
                else
                {
                    float typeW = lc.Type == LightType.Point ? 1f : 2f;
                    fsParams.lights_data[b + 0] = new Vector4(lt.Position, typeW);
                    fsParams.lights_data[b + 1] = new Vector4(fwd, lc.Range);
                }

                fsParams.lights_data[b + 2] = new Vector4(lc.Color, lc.Intensity);

                float inner = lc.InnerAngle > 0f ? lc.InnerAngle : 25f;
                float outer = lc.OuterAngle > inner ? lc.OuterAngle : inner + 5f;
                fsParams.lights_data[b + 3] = new Vector4(
                    MathF.Cos(inner * MathF.PI / 180f),
                    MathF.Cos(outer * MathF.PI / 180f),
                    0f, 0f);

                lightCount++;
            }

            fsParams.ambient_and_count = new Vector4(AmbientColor, (float)lightCount);
            sg_apply_uniforms(UB_phong_fs_params, SG_RANGE(ref fsParams));

            foreach (int id in world.Entities)
            {
                if (!world.TryGetComponent<ActiveFlag>(id, out var active) || !active.Active)
                    continue;
                if (!world.TryGetComponent<MeshRenderer>(id, out var meshRenderer) || !meshRenderer.Visible)
                    continue;
                if (!world.TryGetComponent<Transform>(id, out var transform))
                    continue;

                PrimitiveMeshSpec spec = PrimitiveMeshSpec.Default(PrimitiveKind.Box);
                if (!PrimitiveMeshSpec.TryParse(meshRenderer.MeshPath, out spec))
                    spec = PrimitiveMeshSpec.Default(PrimitiveKind.Box);

                MeshResource mesh = GetOrCreateMesh(spec);
                if (mesh.NumElements == 0 || mesh.VertexBuffer.id == 0 || mesh.IndexBuffer.id == 0)
                    continue;

                Matrix4x4 model = transform.LocalMatrix;
                Matrix4x4 mvp = model * viewProj;
                var vsParams = new phong_vs_params_t { mvp = mvp, model = model };
                sg_apply_uniforms(UB_phong_vs_params, SG_RANGE(ref vsParams));

                sg_apply_bindings(new sg_bindings
                {
                    vertex_buffers = { [0] = mesh.VertexBuffer },
                    index_buffer = mesh.IndexBuffer
                });

                sg_draw(mesh.BaseElement, mesh.NumElements, 1);
            }
        }

        private static void ReleaseUnreferencedMeshes(ECSWorld world)
        {
            if (_meshCache.Count == 0)
                return;

            var liveKeys = new HashSet<string>();
            foreach (int id in world.Entities)
            {
                if (!world.TryGetComponent<MeshRenderer>(id, out var mr))
                    continue;
                if (!PrimitiveMeshSpec.TryParse(mr.MeshPath, out var spec))
                    continue;
                liveKeys.Add(PrimitiveMeshSpec.ToMeshPath(spec));
            }

            if (liveKeys.Count == _meshCache.Count)
                return;

            var deadKeys = new List<string>();
            foreach (var kv in _meshCache)
            {
                if (!liveKeys.Contains(kv.Key))
                    deadKeys.Add(kv.Key);
            }

            foreach (string key in deadKeys)
            {
                MeshResource mesh = _meshCache[key];
                if (mesh.VertexBuffer.id != 0) sg_destroy_buffer(mesh.VertexBuffer);
                if (mesh.IndexBuffer.id != 0) sg_destroy_buffer(mesh.IndexBuffer);
                _meshCache.Remove(key);
            }
        }

        private static MeshResource GetOrCreateMesh(in PrimitiveMeshSpec spec)
        {
            string key = PrimitiveMeshSpec.ToMeshPath(spec);
            if (_meshCache.TryGetValue(key, out var cached))
                return cached;

            if (_meshCache.Count >= MaxCachedMeshes)
            {
                // Keep memory bounded when users tweak primitive params rapidly.
                foreach (var old in _meshCache.Values)
                {
                    if (old.VertexBuffer.id != 0) sg_destroy_buffer(old.VertexBuffer);
                    if (old.IndexBuffer.id != 0) sg_destroy_buffer(old.IndexBuffer);
                }
                _meshCache.Clear();
            }

            MeshResource mesh = spec.Kind switch
            {
                PrimitiveKind.Box => BuildBox(spec),
                PrimitiveKind.Sphere => BuildSphere(spec),
                PrimitiveKind.Plane => BuildPlane(spec),
                PrimitiveKind.Cylinder => BuildCylinder(spec),
                PrimitiveKind.Ring => BuildRing(spec),
                PrimitiveKind.Cone => BuildCone(spec),
                PrimitiveKind.Pyramid => BuildPyramid(spec),
                _ => BuildBox(PrimitiveMeshSpec.Default(PrimitiveKind.Box))
            };

            _meshCache[key] = mesh;
            return mesh;
        }

        private static MeshResource BuildBox(in PrimitiveMeshSpec spec)
        {
            var sizes = sshape_box_sizes((uint)spec.Tiles);
            var vertices = new sshape_vertex_t[sizes.vertices.num];
            var indices = new ushort[sizes.indices.num];
            var buf = new sshape_buffer_t
            {
                vertices = { buffer = SSHAPE_RANGE(vertices) },
                indices = { buffer = SSHAPE_RANGE(indices) }
            };
            buf = sshape_build_box(in buf, new sshape_box_t
            {
                width = spec.Width,
                height = spec.Height,
                depth = spec.Depth,
                tiles = (ushort)spec.Tiles
            });
            return CreateGpuMesh(in buf, "prim-box");
        }

        private static MeshResource BuildSphere(in PrimitiveMeshSpec spec)
        {
            var sizes = sshape_sphere_sizes((uint)spec.Slices, (uint)spec.Stacks);
            var vertices = new sshape_vertex_t[sizes.vertices.num];
            var indices = new ushort[sizes.indices.num];
            var buf = new sshape_buffer_t
            {
                vertices = { buffer = SSHAPE_RANGE(vertices) },
                indices = { buffer = SSHAPE_RANGE(indices) }
            };
            buf = sshape_build_sphere(in buf, new sshape_sphere_t
            {
                radius = spec.Radius,
                slices = (ushort)spec.Slices,
                stacks = (ushort)spec.Stacks
            });
            return CreateGpuMesh(in buf, "prim-sphere");
        }

        private static MeshResource BuildPlane(in PrimitiveMeshSpec spec)
        {
            var sizes = sshape_plane_sizes((uint)spec.Tiles);
            var vertices = new sshape_vertex_t[sizes.vertices.num];
            var indices = new ushort[sizes.indices.num];
            var buf = new sshape_buffer_t
            {
                vertices = { buffer = SSHAPE_RANGE(vertices) },
                indices = { buffer = SSHAPE_RANGE(indices) }
            };
            buf = sshape_build_plane(in buf, new sshape_plane_t
            {
                width = spec.Width,
                depth = spec.Depth,
                tiles = (ushort)spec.Tiles
            });
            return CreateGpuMesh(in buf, "prim-plane");
        }

        private static MeshResource BuildCylinder(in PrimitiveMeshSpec spec)
        {
            var sizes = sshape_cylinder_sizes((uint)spec.Slices, (uint)spec.Stacks);
            var vertices = new sshape_vertex_t[sizes.vertices.num];
            var indices = new ushort[sizes.indices.num];
            var buf = new sshape_buffer_t
            {
                vertices = { buffer = SSHAPE_RANGE(vertices) },
                indices = { buffer = SSHAPE_RANGE(indices) }
            };
            buf = sshape_build_cylinder(in buf, new sshape_cylinder_t
            {
                radius = spec.Radius,
                height = spec.Height,
                slices = (ushort)spec.Slices,
                stacks = (ushort)spec.Stacks
            });
            return CreateGpuMesh(in buf, "prim-cylinder");
        }

        private static MeshResource BuildRing(in PrimitiveMeshSpec spec)
        {
            var sizes = sshape_torus_sizes((uint)spec.Sides, (uint)spec.Rings);
            var vertices = new sshape_vertex_t[sizes.vertices.num];
            var indices = new ushort[sizes.indices.num];
            var buf = new sshape_buffer_t
            {
                vertices = { buffer = SSHAPE_RANGE(vertices) },
                indices = { buffer = SSHAPE_RANGE(indices) }
            };
            buf = sshape_build_torus(in buf, new sshape_torus_t
            {
                radius = spec.Radius,
                ring_radius = spec.RingRadius,
                sides = (ushort)spec.Sides,
                rings = (ushort)spec.Rings
            });
            return CreateGpuMesh(in buf, "prim-ring");
        }

        private static MeshResource BuildCone(in PrimitiveMeshSpec spec)
        {
            return BuildConeLike(spec.Radius, spec.Height, spec.Slices, smoothSides: true, label: "prim-cone");
        }

        private static MeshResource BuildPyramid(in PrimitiveMeshSpec spec)
        {
            return BuildConeLike(spec.Radius, spec.Height, spec.Sides, smoothSides: false, label: "prim-pyramid");
        }

        private static MeshResource BuildConeLike(float radius, float height, int sides, bool smoothSides, string label)
        {
            radius = MathF.Max(0.01f, radius);
            height = MathF.Max(0.01f, height);
            sides = Math.Max(3, sides);

            var vertices = new List<sshape_vertex_t>(smoothSides ? sides * 2 + 2 : sides * 6 + 1);
            var indices = new List<ushort>(sides * 6);
            uint color = 0xFFFFFFFFu;

            float yTop = height * 0.5f;
            float yBot = -height * 0.5f;
            float slope = radius / height;

            ushort AddV(Vector3 p, Vector3 n)
            {
                n = Vector3.Normalize(n);
                vertices.Add(new sshape_vertex_t
                {
                    x = p.X,
                    y = p.Y,
                    z = p.Z,
                    normal = PackNormalByte4N(n),
                    u = 0,
                    v = 0,
                    color = color
                });
                return (ushort)(vertices.Count - 1);
            }

            if (smoothSides)
            {
                ushort apex = AddV(new Vector3(0f, yTop, 0f), new Vector3(0f, 1f, 0f));
                var sideRing = new ushort[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * MathF.Tau / sides;
                    float cx = MathF.Cos(a);
                    float cz = MathF.Sin(a);
                    sideRing[i] = AddV(new Vector3(cx * radius, yBot, cz * radius), new Vector3(cx, slope, cz));
                }

                for (int i = 0; i < sides; i++)
                {
                    int ni = (i + 1) % sides;
                    // Outside-facing winding.
                    indices.Add(apex);
                    indices.Add(sideRing[i]);
                    indices.Add(sideRing[ni]);
                }
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    int ni = (i + 1) % sides;
                    float a0 = i * MathF.Tau / sides;
                    float a1 = ni * MathF.Tau / sides;
                    Vector3 p0 = new Vector3(MathF.Cos(a0) * radius, yBot, MathF.Sin(a0) * radius);
                    Vector3 p1 = new Vector3(MathF.Cos(a1) * radius, yBot, MathF.Sin(a1) * radius);
                    Vector3 apexP = new Vector3(0f, yTop, 0f);
                    Vector3 faceN = Vector3.Normalize(Vector3.Cross(p0 - apexP, p1 - apexP));

                    ushort ia = AddV(apexP, faceN);
                    ushort i0 = AddV(p0, faceN);
                    ushort i1 = AddV(p1, faceN);
                    indices.Add(ia);
                    indices.Add(i0);
                    indices.Add(i1);
                }
            }

            ushort baseCenter = AddV(new Vector3(0f, yBot, 0f), new Vector3(0f, -1f, 0f));
            var baseRing = new ushort[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * MathF.Tau / sides;
                baseRing[i] = AddV(new Vector3(MathF.Cos(a) * radius, yBot, MathF.Sin(a) * radius), new Vector3(0f, -1f, 0f));
            }
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                // Down-facing winding.
                indices.Add(baseCenter);
                indices.Add(baseRing[ni]);
                indices.Add(baseRing[i]);
            }

            return CreateGpuMesh(vertices.ToArray(), indices.ToArray(), label);
        }

        private static MeshResource CreateGpuMesh(in sshape_buffer_t buf, string label)
        {
            var vbuf = sg_make_buffer(sshape_vertex_buffer_desc(buf) with { label = $"{label}-vbuf" });
            var ibuf = sg_make_buffer(sshape_index_buffer_desc(buf) with { label = $"{label}-ibuf" });
            var range = sshape_make_element_range(buf);

            if (vbuf.id == 0 || ibuf.id == 0)
            {
                if (vbuf.id != 0) sg_destroy_buffer(vbuf);
                if (ibuf.id != 0) sg_destroy_buffer(ibuf);
                return default;
            }

            return new MeshResource
            {
                VertexBuffer = vbuf,
                IndexBuffer = ibuf,
                BaseElement = range.base_element,
                NumElements = range.num_elements
            };
        }

        private static MeshResource CreateGpuMesh(sshape_vertex_t[] vertices, ushort[] indices, string label)
        {
            var vbuf = sg_make_buffer(new sg_buffer_desc
            {
                data = SG_RANGE(vertices),
                label = $"{label}-vbuf"
            });

            var ibuf = sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = $"{label}-ibuf"
            });

            if (vbuf.id == 0 || ibuf.id == 0)
            {
                if (vbuf.id != 0) sg_destroy_buffer(vbuf);
                if (ibuf.id != 0) sg_destroy_buffer(ibuf);
                return default;
            }

            return new MeshResource
            {
                VertexBuffer = vbuf,
                IndexBuffer = ibuf,
                BaseElement = 0,
                NumElements = (uint)indices.Length
            };
        }

        private static uint PackNormalByte4N(Vector3 n)
        {
            n = Vector3.Normalize(n);
            int sx = Math.Clamp((int)MathF.Round(n.X * 127f), -127, 127);
            int sy = Math.Clamp((int)MathF.Round(n.Y * 127f), -127, 127);
            int sz = Math.Clamp((int)MathF.Round(n.Z * 127f), -127, 127);

            byte bx = unchecked((byte)(sbyte)sx);
            byte by = unchecked((byte)(sbyte)sy);
            byte bz = unchecked((byte)(sbyte)sz);
            byte bw = 0;

            return (uint)(bx | (by << 8) | (bz << 16) | (bw << 24));
        }

        public static void Cleanup()
        {
            foreach (var mesh in _meshCache.Values)
            {
                if (mesh.VertexBuffer.id != 0) sg_destroy_buffer(mesh.VertexBuffer);
                if (mesh.IndexBuffer.id != 0) sg_destroy_buffer(mesh.IndexBuffer);
            }
            _meshCache.Clear();

            if (_pip.id != 0) { sg_destroy_pipeline(_pip); _pip = default; }
            _initialized = false;
        }
    }
}
