// MeshRegistry.cs — path → MeshResource mapping with 16-bit id allocation.
// Loads OBJ files, uploads sub-mesh VB+IB pairs to Sokol, and assigns a
// stable ushort MeshId for use in DrawCommand.MeshId.
// Not thread-safe (Sokol main-thread constraint).

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameEditor.Framework.Assets;
using GameEditor.Framework.Renderer;
using static Sokol.SG;
using static Sokol.SShape;
using static Sokol.Utils;

namespace GameEditor.Framework.Renderer.Server.Resources
{
    public sealed unsafe class MeshRegistry : IDisposable
    {
        /// <summary>Key prefix of meshes their owner builds and registers in memory (<see cref="RegisterMesh"/>): never a file.</summary>
        public const string InMemoryPrefix = "mem:";

        /// <summary>
        /// A mesh the engine makes from numbers, not a file to load: a primitive ("prim:…"), one handed over in memory
        /// ("mem:…"), or a part of a race track ("Tracks/loop.track#road", "#walls").
        /// </summary>
        public static bool IsMadeInCode(string? path)
            => path != null && (path.StartsWith("prim:", StringComparison.Ordinal) || path.StartsWith(InMemoryPrefix, StringComparison.Ordinal)
                                || TrackData.TryParseMeshPath(path, out _, out _));

        // The track parts made, with the TrackLibrary.Version they were made at: made again when it moves on.
        private readonly Dictionary<string, int> _trackMade = new(StringComparer.Ordinal);

        private readonly Dictionary<string, MeshResource> _byPath = new(StringComparer.Ordinal);
        private readonly MeshResource?[] _byId;    // sparse id → resource
        private ushort   _nextId = 1;              // 0 = invalid
        private bool     _disposed;

        public MeshRegistry(int capacity = 4096)
        {
            _byId = new MeshResource?[capacity];
        }

        // ── public API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Load an OBJ (or, by a ".stl" path, STL) file from raw bytes and upload to GPU.
        /// Returns the 16-bit MeshId. Ref-counted: safe to call multiple times for the same path.
        /// Returns 0 on failure.
        /// </summary>
        public ushort Load(string path, ReadOnlySpan<byte> data)
        {
            if (_byPath.TryGetValue(path, out var existing))
            {
                existing.RefCount++;
                return existing.Id;
            }

            ObjMesh obj = path.EndsWith(".stl", StringComparison.OrdinalIgnoreCase)
                ? StlLoader.Load(data, path)
                : ObjLoader.Load(System.Text.Encoding.UTF8.GetString(data).AsSpan(), path);

            if (obj.SubMeshes.Length == 0) return 0;

            if (_nextId >= _byId.Length) return 0; // registry full

            var subResources = new MeshSubResource[obj.SubMeshes.Length];
            for (int i = 0; i < obj.SubMeshes.Length; i++)
            {
                var sm    = obj.SubMeshes[i];
                var vbRes = UploadVertexBuffer(sm, path, i);
                var ibRes = UploadIndexBuffer(sm, path, i);

                subResources[i] = new MeshSubResource
                {
                    VertexBuffer = vbRes,
                    IndexBuffer  = ibRes,
                    IndexCount   = sm.Indices.Length,
                    MaterialName = sm.MaterialName,
                    MaterialKey  = (!string.IsNullOrEmpty(obj.MtlLib) && !string.IsNullOrEmpty(sm.MaterialName))
                                       ? string.Concat(MtlKey(path, obj.MtlLib), "#", sm.MaterialName)
                                       : ""
                };
            }

            System.Console.WriteLine($"[MeshRegistry] Loaded '{path}': {obj.SubMeshes.Length} sub-mesh(es), bounds={obj.Bounds}");
            ushort id = _nextId++;
            var res = new MeshResource
            {
                Id          = id,
                SourcePath  = path,
                MtlLib      = obj.MtlLib,
                SubMeshes   = subResources,
                LocalBounds = obj.Bounds,
                RefCount    = 1
            };

            _byPath[path] = res;
            _byId[id]     = res;
            return id;
        }

        /// <summary>
        /// The key of the <c>.mtl</c> file an <c>.obj</c> names: the file of that name beside the mesh
        /// ("Models/duck.obj" + "duck.mtl" = "Models/duck.mtl"). Its materials are registered as "&lt;this key&gt;#&lt;name&gt;",
        /// so two files of one name in two folders are two sets of materials.
        /// </summary>
        public static string MtlKey(string meshPath, string mtlLib)
        {
            string dir = System.IO.Path.GetDirectoryName(meshPath)?.Replace('\\', '/') ?? string.Empty;
            return string.IsNullOrEmpty(dir) ? mtlLib : dir + "/" + mtlLib;
        }

        /// <summary>
        /// Register a pre-built mesh (already split into sub-meshes) under <paramref name="key"/>
        /// and upload its GPU buffers. Used by the glTF importer, which assembles vertices itself
        /// instead of parsing OBJ text. Ref-counted like <see cref="Load"/>; returns the 16-bit id
        /// (0 on failure or when the registry is full).
        /// </summary>
        public ushort RegisterMesh(string key, ObjSubMesh[] subMeshes, in Aabb bounds)
        {
            if (_byPath.TryGetValue(key, out var existing))
            {
                existing.RefCount++;
                return existing.Id;
            }
            if (subMeshes.Length == 0 || _nextId >= _byId.Length) return 0;

            var subResources = new MeshSubResource[subMeshes.Length];
            for (int i = 0; i < subMeshes.Length; i++)
            {
                subResources[i] = new MeshSubResource
                {
                    VertexBuffer = UploadVertexBuffer(subMeshes[i], key, i),
                    IndexBuffer  = UploadIndexBuffer(subMeshes[i], key, i),
                    IndexCount   = subMeshes[i].Indices.Length,
                    MaterialName = subMeshes[i].MaterialName,
                    MaterialKey  = ""
                };
            }

            ushort id = _nextId++;
            var res = new MeshResource
            {
                Id          = id,
                SourcePath  = key,
                MtlLib      = "",
                SubMeshes   = subResources,
                LocalBounds = bounds,
                RefCount    = 1
            };
            _byPath[key] = res;
            _byId[id]    = res;
            return id;
        }

        /// <summary>Decrement ref-count; destroys GPU buffers when count reaches zero.</summary>
        public void Release(string path)
        {
            if (!_byPath.TryGetValue(path, out var res)) return;
            res.RefCount--;
            if (res.RefCount > 0) return;

            _byId[res.Id] = null;
            _byPath.Remove(path);
            res.Destroy();
        }

        /// <summary>Look up a mesh by 16-bit id. Returns null if not loaded.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MeshResource? GetById(ushort id)
            => (id > 0 && id < _byId.Length) ? _byId[id] : null;

        /// <summary>Look up a mesh by file path. Returns null if not loaded, or when <paramref name="path"/> is null
        /// (a MeshRenderer with no mesh assigned yet).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MeshResource? GetByPath(string? path)
        {
            if (path == null) return null;
            if (_byPath.TryGetValue(path, out var r))
            {
                if (_trackMade.Count != 0 && _trackMade.TryGetValue(path, out int made) && made != TrackLibrary.Version) RemakeTrackPart(path, r);
                return r;
            }

            if (TrackData.TryParseMeshPath(path, out string trackKey, out string part))
                return TryCreateTrackPart(path, trackKey, part, out var trackRes) ? trackRes : null;

            if (!path.StartsWith("prim:", StringComparison.Ordinal))
                return null;

            return TryCreatePrimitive(path, out var primRes) ? primRes : null;
        }

        private bool TryCreatePrimitive(string path, out MeshResource resource)
        {
            resource = default!;

            if (!PrimitiveMeshSpec.TryParse(path, out PrimitiveMeshSpec spec))
                return false;

            ObjVertex[] vertices;
            uint[]      indices;

            switch (spec.Kind)
            {
                case PrimitiveKind.Box:
                {
                    var sizes = sshape_box_sizes((uint)spec.Tiles);
                    var sv = new sshape_vertex_t[sizes.vertices.num];
                    var si = new ushort[sizes.indices.num];
                    var buf = new sshape_buffer_t
                    {
                        vertices = { buffer = SSHAPE_RANGE(sv) },
                        indices  = { buffer = SSHAPE_RANGE(si) }
                    };
                    buf = sshape_build_box(in buf, new sshape_box_t
                    {
                        width  = spec.Width,
                        height = spec.Height,
                        depth  = spec.Depth,
                        tiles  = (ushort)spec.Tiles
                    });
                    if (!buf.valid) return false;
                    (vertices, indices) = ConvertSShape(sv, si);
                    break;
                }
                case PrimitiveKind.Sphere:
                {
                    var sizes = sshape_sphere_sizes((uint)spec.Slices, (uint)spec.Stacks);
                    var sv = new sshape_vertex_t[sizes.vertices.num];
                    var si = new ushort[sizes.indices.num];
                    var buf = new sshape_buffer_t
                    {
                        vertices = { buffer = SSHAPE_RANGE(sv) },
                        indices  = { buffer = SSHAPE_RANGE(si) }
                    };
                    buf = sshape_build_sphere(in buf, new sshape_sphere_t
                    {
                        radius = spec.Radius,
                        slices = (ushort)spec.Slices,
                        stacks = (ushort)spec.Stacks
                    });
                    if (!buf.valid) return false;
                    (vertices, indices) = ConvertSShape(sv, si);
                    break;
                }
                case PrimitiveKind.Plane:
                {
                    var sizes = sshape_plane_sizes((uint)spec.Tiles);
                    var sv = new sshape_vertex_t[sizes.vertices.num];
                    var si = new ushort[sizes.indices.num];
                    var buf = new sshape_buffer_t
                    {
                        vertices = { buffer = SSHAPE_RANGE(sv) },
                        indices  = { buffer = SSHAPE_RANGE(si) }
                    };
                    buf = sshape_build_plane(in buf, new sshape_plane_t
                    {
                        width  = spec.Width,
                        depth  = spec.Depth,
                        tiles  = (ushort)spec.Tiles
                    });
                    if (!buf.valid) return false;
                    (vertices, indices) = ConvertSShape(sv, si);
                    break;
                }
                case PrimitiveKind.Cylinder:
                {
                    var sizes = sshape_cylinder_sizes((uint)spec.Slices, (uint)spec.Stacks);
                    var sv = new sshape_vertex_t[sizes.vertices.num];
                    var si = new ushort[sizes.indices.num];
                    var buf = new sshape_buffer_t
                    {
                        vertices = { buffer = SSHAPE_RANGE(sv) },
                        indices  = { buffer = SSHAPE_RANGE(si) }
                    };
                    buf = sshape_build_cylinder(in buf, new sshape_cylinder_t
                    {
                        radius = spec.Radius,
                        height = spec.Height,
                        slices = (ushort)spec.Slices,
                        stacks = (ushort)spec.Stacks
                    });
                    if (!buf.valid) return false;
                    (vertices, indices) = ConvertSShape(sv, si);
                    break;
                }
                case PrimitiveKind.Ring:
                {
                    var sizes = sshape_torus_sizes((uint)spec.Sides, (uint)spec.Rings);
                    var sv = new sshape_vertex_t[sizes.vertices.num];
                    var si = new ushort[sizes.indices.num];
                    var buf = new sshape_buffer_t
                    {
                        vertices = { buffer = SSHAPE_RANGE(sv) },
                        indices  = { buffer = SSHAPE_RANGE(si) }
                    };
                    buf = sshape_build_torus(in buf, new sshape_torus_t
                    {
                        radius      = spec.Radius,
                        ring_radius = spec.RingRadius,
                        sides       = (ushort)spec.Sides,
                        rings       = (ushort)spec.Rings
                    });
                    if (!buf.valid) return false;
                    (vertices, indices) = ConvertSShape(sv, si);
                    break;
                }
                case PrimitiveKind.Capsule:
                    (vertices, indices) = BuildPrimCapsule(spec);
                    break;
                case PrimitiveKind.Cone:
                    (vertices, indices) = BuildPrimConeLike(spec.Radius, spec.Height, spec.Slices, smoothSides: true);
                    break;
                case PrimitiveKind.Pyramid:
                    (vertices, indices) = BuildPrimConeLike(spec.Radius, spec.Height, spec.Sides, smoothSides: false);
                    break;
                default:
                    return false;
            }

            if (vertices.Length == 0 || indices.Length == 0)
                return false;

            var bmin = new Vector3(float.MaxValue);
            var bmax = new Vector3(float.MinValue);
            foreach (var v in vertices)
            {
                bmin = Vector3.Min(bmin, v.Position);
                bmax = Vector3.Max(bmax, v.Position);
            }

            var sub = new ObjSubMesh
            {
                MaterialName = "",
                Vertices     = vertices,
                Indices      = indices
            };

            ushort id = _nextId++;
            if (id == 0 || id >= _byId.Length)
                return false;

            var subResources = new MeshSubResource[1];
            subResources[0] = new MeshSubResource
            {
                VertexBuffer = UploadVertexBuffer(sub, path, 0),
                IndexBuffer  = UploadIndexBuffer(sub, path, 0),
                IndexCount   = sub.Indices.Length,
                MaterialName = "",
                MaterialKey  = ""
            };

            resource = new MeshResource
            {
                Id          = id,
                SourcePath  = path,
                MtlLib      = "",
                SubMeshes   = subResources,
                LocalBounds = new Aabb(bmin, bmax),
                RefCount    = 1
            };

            _byPath[path] = resource;
            _byId[id]     = resource;
            return true;
        }

        // ── a race track's road and walls ─────────────────────────────────────────────

        // The part's vertices and triangles, from the track as the library has it now; false while it is not read
        // (a built game: its file is on its way) or has nothing to draw (walls of height 0).
        private static bool TrackPart(string key, string part, out ObjVertex[] vertices, out uint[] indices, out Aabb bounds)
        {
            vertices = Array.Empty<ObjVertex>(); indices = Array.Empty<uint>(); bounds = default;
            if (TrackLibrary.Track(key) is not { } track) return false;
            var p = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tg = new List<Vector4>(); var ix = new List<uint>();
            if (part == TrackData.RoadPart) track.RoadMesh(p, n, uv, tg, ix); else track.WallMesh(p, n, uv, tg, ix);
            if (ix.Count == 0) return false;
            vertices = new ObjVertex[p.Count];
            var bmin = new Vector3(float.MaxValue); var bmax = new Vector3(float.MinValue);
            for (int i = 0; i < p.Count; i++)
            {
                vertices[i] = new ObjVertex { Position = p[i], Normal = n[i], Uv = uv[i], Tangent = tg[i] };
                bmin = Vector3.Min(bmin, p[i]); bmax = Vector3.Max(bmax, p[i]);
            }
            indices = ix.ToArray();
            bounds = new Aabb(bmin, bmax);
            return true;
        }

        private bool TryCreateTrackPart(string path, string key, string part, out MeshResource resource)
        {
            resource = default!;
            int version = TrackLibrary.Version;
            if (!TrackPart(key, part, out var vertices, out var indices, out var bounds)) return false;
            ushort id = _nextId;
            if (id == 0 || id >= _byId.Length) return false;
            _nextId++;
            var sub = new ObjSubMesh { MaterialName = "", Vertices = vertices, Indices = indices };
            resource = new MeshResource
            {
                Id          = id,
                SourcePath  = path,
                MtlLib      = "",
                SubMeshes   = new[] { new MeshSubResource { VertexBuffer = UploadVertexBuffer(sub, path, 0), IndexBuffer = UploadIndexBuffer(sub, path, 0),
                                                            IndexCount = indices.Length, MaterialName = "", MaterialKey = "" } },
                LocalBounds = bounds,
                RefCount    = 1
            };
            _byPath[path] = resource;
            _byId[id]     = resource;
            _trackMade[path] = version;
            return true;
        }

        // The track changed: the same resource (and id) gets the part's new buffers. A part with nothing to draw now
        // keeps no triangles.
        private void RemakeTrackPart(string path, MeshResource resource)
        {
            int version = TrackLibrary.Version;
            _trackMade[path] = version;
            foreach (var old in resource.SubMeshes) old.Destroy();
            if (!TrackPart(TrackData.KeyOf(path)!, path.Substring(path.IndexOf('#') + 1), out var vertices, out var indices, out var bounds))
            {
                resource.SubMeshes = Array.Empty<MeshSubResource>();
                return;
            }
            var sub = new ObjSubMesh { MaterialName = "", Vertices = vertices, Indices = indices };
            resource.SubMeshes = new[] { new MeshSubResource { VertexBuffer = UploadVertexBuffer(sub, path, 0), IndexBuffer = UploadIndexBuffer(sub, path, 0),
                                                               IndexCount = indices.Length, MaterialName = "", MaterialKey = "" } };
            resource.LocalBounds = bounds;
        }

        // ── primitive vertex builders ─────────────────────────────────────────────────

        /// <summary>
        /// Convert sshape_vertex_t (packed byte4-SNORM normals, ushort-UNORM UVs) to ObjVertex.
        /// Tangents are recomputed from the unpacked normal.
        /// </summary>
        private static (ObjVertex[] vertices, uint[] indices) ConvertSShape(sshape_vertex_t[] sv, ushort[] si)
        {
            var vertices = new ObjVertex[sv.Length];
            for (int i = 0; i < sv.Length; i++)
            {
                var s = sv[i];
                // Unpack normal: sshape packs as byte4 SNORM (x,y,z in bytes 0,1,2)
                float nx = (sbyte)(s.normal & 0xFF) / 127f;
                float ny = (sbyte)((s.normal >> 8) & 0xFF) / 127f;
                float nz = (sbyte)((s.normal >> 16) & 0xFF) / 127f;
                Vector3 n = new Vector3(nx, ny, nz);
                if (n.LengthSquared() > 1e-10f)
                    n = Vector3.Normalize(n);
                else
                    n = Vector3.UnitY;

                // Unpack UVs: sshape packs as ushort UNORM (value / 65535)
                float u = s.u / 65535f;
                float v = s.v / 65535f;

                Vector3 tangentAxis = MathF.Abs(n.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 t = Vector3.Normalize(Vector3.Cross(tangentAxis, n));
                vertices[i] = new ObjVertex
                {
                    Position = new Vector3(s.x, s.y, s.z),
                    Normal   = n,
                    Uv       = new Vector2(u, v),
                    Tangent  = new Vector4(t, 1f)
                };
            }
            // sshape uses CW-front winding (Sokol default), but RenderingServer uses CCW.
            // Flip each triangle by swapping the 2nd and 3rd indices.
            var indices = new uint[si.Length];
            for (int i = 0; i < si.Length; i += 3)
            {
                indices[i]     = si[i];
                indices[i + 1] = si[i + 2];
                indices[i + 2] = si[i + 1];
            }
            return (vertices, indices);
        }

        /// <summary>Build capsule ObjVertex geometry (two hemispheres joined by a cylinder band).</summary>
        private static (ObjVertex[], uint[]) BuildPrimCapsule(in PrimitiveMeshSpec spec)
        {
            float r      = MathF.Max(0.01f, spec.Radius);
            float hh     = MathF.Max(0f, spec.Height * 0.5f - r);
            int   slices = Math.Max(3, spec.Slices);
            int   stacks = Math.Max(1, spec.Stacks);

            var verts   = new List<ObjVertex>();
            var indices = new List<uint>();

            int AddV(float x, float y, float z, float nx, float ny, float nz)
            {
                Vector3 n = Vector3.Normalize(new Vector3(nx, ny, nz));
                Vector3 tangentAxis = MathF.Abs(n.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 tang = Vector3.Normalize(Vector3.Cross(tangentAxis, n));
                verts.Add(new ObjVertex
                {
                    Position = new Vector3(x, y, z),
                    Normal   = n,
                    Uv       = Vector2.Zero,
                    Tangent  = new Vector4(tang, 1f)
                });
                return verts.Count - 1;
            }

            int totalRings = 2 * stacks + 2;
            int[][] rings  = new int[totalRings][];

            for (int i = 0; i <= stacks; i++)
            {
                float theta  = (i / (float)stacks) * (MathF.PI / 2f);
                float sinT   = MathF.Sin(theta), cosT = MathF.Cos(theta);
                float y      = hh + r * cosT;
                float ring_r = r * sinT;
                rings[i] = new int[slices];
                for (int j = 0; j < slices; j++)
                {
                    float az = j * MathF.Tau / slices;
                    float ca = MathF.Cos(az), sa = MathF.Sin(az);
                    rings[i][j] = AddV(ring_r * ca, y, ring_r * sa, sinT * ca, cosT, sinT * sa);
                }
            }
            for (int i = 0; i <= stacks; i++)
            {
                float theta  = MathF.PI / 2f + (i / (float)stacks) * (MathF.PI / 2f);
                float sinT   = MathF.Sin(theta), cosT = MathF.Cos(theta);
                float y      = -hh + r * cosT;
                float ring_r = r * sinT;
                rings[stacks + 1 + i] = new int[slices];
                for (int j = 0; j < slices; j++)
                {
                    float az = j * MathF.Tau / slices;
                    float ca = MathF.Cos(az), sa = MathF.Sin(az);
                    rings[stacks + 1 + i][j] = AddV(ring_r * ca, y, ring_r * sa, sinT * ca, cosT, sinT * sa);
                }
            }

            for (int ri = 0; ri < totalRings - 1; ri++)
            {
                int[] r0 = rings[ri], r1 = rings[ri + 1];
                for (int j = 0; j < slices; j++)
                {
                    int nj = (j + 1) % slices;
                    // CCW winding (RenderingServer pipeline)
                    indices.Add((uint)r0[j]);  indices.Add((uint)r1[nj]); indices.Add((uint)r1[j]);
                    indices.Add((uint)r0[j]);  indices.Add((uint)r0[nj]); indices.Add((uint)r1[nj]);
                }
            }
            return (verts.ToArray(), indices.ToArray());
        }

        /// <summary>Build cone (smoothSides=true) or pyramid (smoothSides=false) ObjVertex geometry.</summary>
        private static (ObjVertex[], uint[]) BuildPrimConeLike(float radius, float height, int sides, bool smoothSides)
        {
            radius = MathF.Max(0.01f, radius);
            height = MathF.Max(0.01f, height);
            sides  = Math.Max(3, sides);

            var verts   = new List<ObjVertex>(sides * 6);
            var indices = new List<uint>(sides * 6);
            float slope = radius / height;
            float yTop  = height * 0.5f;
            float yBot  = -height * 0.5f;

            int AddV(Vector3 p, Vector3 n)
            {
                n = Vector3.Normalize(n);
                Vector3 tangentAxis = MathF.Abs(n.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
                Vector3 tang = Vector3.Normalize(Vector3.Cross(tangentAxis, n));
                verts.Add(new ObjVertex
                {
                    Position = p,
                    Normal   = n,
                    Uv       = Vector2.Zero,
                    Tangent  = new Vector4(tang, 1f)
                });
                return verts.Count - 1;
            }

            if (smoothSides)
            {
                int apex = AddV(new Vector3(0f, yTop, 0f), new Vector3(0f, 1f, 0f));
                var ring = new int[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * MathF.Tau / sides;
                    float cx = MathF.Cos(a), cz = MathF.Sin(a);
                    ring[i] = AddV(new Vector3(cx * radius, yBot, cz * radius), new Vector3(cx, slope, cz));
                }
                for (int i = 0; i < sides; i++)
                {
                    int ni = (i + 1) % sides;
                    // CCW winding
                    indices.Add((uint)apex); indices.Add((uint)ring[ni]); indices.Add((uint)ring[i]);
                }
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    int ni = (i + 1) % sides;
                    float a0 = i * MathF.Tau / sides, a1 = ni * MathF.Tau / sides;
                    Vector3 p0    = new Vector3(MathF.Cos(a0) * radius, yBot, MathF.Sin(a0) * radius);
                    Vector3 p1    = new Vector3(MathF.Cos(a1) * radius, yBot, MathF.Sin(a1) * radius);
                    Vector3 apexP = new Vector3(0f, yTop, 0f);
                    // Cross(p1-apex, p0-apex) gives the outward normal for CCW winding.
                    Vector3 faceN = Vector3.Normalize(Vector3.Cross(p1 - apexP, p0 - apexP));
                    indices.Add((uint)AddV(apexP, faceN));
                    indices.Add((uint)AddV(p1,    faceN));
                    indices.Add((uint)AddV(p0,    faceN));
                }
            }

            int baseCenter = AddV(new Vector3(0f, yBot, 0f), new Vector3(0f, -1f, 0f));
            var baseRing = new int[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * MathF.Tau / sides;
                baseRing[i] = AddV(new Vector3(MathF.Cos(a) * radius, yBot, MathF.Sin(a) * radius), new Vector3(0f, -1f, 0f));
            }
            for (int i = 0; i < sides; i++)
            {
                int ni = (i + 1) % sides;
                // CCW winding for bottom cap (normal points -Y / outward)
                indices.Add((uint)baseCenter); indices.Add((uint)baseRing[i]); indices.Add((uint)baseRing[ni]);
            }

            return (verts.ToArray(), indices.ToArray());
        }

        // ── upload helpers ───────────────────────────────────────────────────────────

        // Without sokol_gfx (a tool that runs with no window, e.g. the editor's headless command line) a mesh
        // is registered with its bounds and sub-meshes but no GPU buffers: nothing draws there.
        private static sg_buffer UploadVertexBuffer(ObjSubMesh sm, string path, int subIdx)
        {
            if (!sg_isvalid()) return default;
            var span = sm.Vertices.AsSpan();
            return sg_make_buffer(new sg_buffer_desc
            {
                data  = SG_RANGE(span),
                usage = new sg_buffer_usage { vertex_buffer = true, immutable = true },
                label = $"{path}:vb[{subIdx}]"
            });
        }

        private static sg_buffer UploadIndexBuffer(ObjSubMesh sm, string path, int subIdx)
        {
            if (!sg_isvalid()) return default;
            var span = sm.Indices.AsSpan();
            return sg_make_buffer(new sg_buffer_desc
            {
                usage = new sg_buffer_usage { index_buffer = true, immutable = true },
                data  = SG_RANGE(span),
                label = $"{path}:ib[{subIdx}]"
            });
        }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Shutdown()
        {
            foreach (var res in _byPath.Values) res.Destroy();
            _byPath.Clear();
            _trackMade.Clear();
            Array.Clear(_byId, 0, _byId.Length);
            _nextId = 1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
        }
    }
}
