using System;
using System.Collections.Generic;
using System.IO;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server.Resources;
using Sokol;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.GI
{
    /// <summary>
    /// The active scene's baked lighting at run time (M8a, docs BAKED_GI_DESIGN.md §3.5): its lightmap pages as textures,
    /// each lightmapped entity's own mesh (registered under "lightmap:&lt;folder&gt;#&lt;scene id&gt;") and the Baked lights.
    /// The editor loads it from the project's folder (<see cref="LoadFromFolder"/>), a game from its files
    /// (<see cref="LoadForSceneAsync"/>). The bake sits beside the scene, in a folder of its name.
    /// </summary>
    public static class Lightmaps
    {
        private static LightingData? _data;
        private static string _folderKey = "";
        private static sg_view[] _light = Array.Empty<sg_view>(), _dir = Array.Empty<sg_view>();
        private static readonly Dictionary<int, MeshResource> _meshByScene = new();
        private static readonly Dictionary<ushort, int> _pageOfMesh = new();
        private static readonly HashSet<int> _bakedLights = new();
        private static int _version;
        private static System.Numerics.Vector3[] _probePos = Array.Empty<System.Numerics.Vector3>();
        private static System.Numerics.Vector4[] _probePacked = Array.Empty<System.Numerics.Vector4>();   // 9 a probe

        /// <summary>The loaded bake has light probes (M8b): objects that are not lightmapped take their light from them.</summary>
        public static bool HasProbes => _data != null && _probePos.Length > 0;
        public static int ProbeCount => _probePos.Length;

        /// <summary>
        /// The probes' light at <paramref name="at"/> (the packed form the shaders read: <see cref="Lighting.AmbientSH.Pack"/>,
        /// [2].W = 1): every probe weighted 1 / (d² + 0.01)², the weights summing to 1 - continuous as an object moves,
        /// a probe's own light at its place. False without probes.
        /// </summary>
        public static bool SampleProbes(System.Numerics.Vector3 at, Span<System.Numerics.Vector4> dst)
        {
            dst.Clear();
            if (!HasProbes) return false;
            Blend(_probePos, _probePacked, at, dst);
            return true;
        }

        /// <summary>The blend <see cref="SampleProbes"/> makes, of any probes (each 9 packed vec4).</summary>
        public static void Blend(ReadOnlySpan<System.Numerics.Vector3> positions, ReadOnlySpan<System.Numerics.Vector4> packed,
                                 System.Numerics.Vector3 at, Span<System.Numerics.Vector4> dst)
        {
            dst.Clear();
            float total = 0f;
            for (int i = 0; i < positions.Length; i++)
            {
                float d2 = System.Numerics.Vector3.DistanceSquared(at, positions[i]);
                float w = 1f / ((d2 + 0.01f) * (d2 + 0.01f));
                total += w;
                for (int k = 0; k < 9; k++) dst[k] += packed[i * 9 + k] * w;
            }
            float inv = 1f / total;
            for (int k = 0; k < 9; k++) dst[k] *= inv;
            dst[2] = new System.Numerics.Vector4(dst[2].X, dst[2].Y, dst[2].Z, 1f);
        }

        /// <summary>A bake is loaded for the scene.</summary>
        public static bool Active => _data != null;
        public static bool Directional => _data?.Directional == true;
        public static int PageCount => _light.Length;
        /// <summary>Grows each time a bake is loaded or unloaded (the editor's views follow it).</summary>
        public static int Version => _version;

        /// <summary>The folder (below Assets) of a scene's bake: "Scenes/Main.scene.json" → "Scenes/Main".</summary>
        public static string FolderOf(string sceneKey)
        {
            string k = sceneKey.Replace('\\', '/');
            if (k.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase)) k = k[..^".scene.json".Length];
            else if (k.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) k = k[..^".json".Length];
            return k;
        }

        /// <summary>A lightmapped entity's own mesh, or null.</summary>
        public static MeshResource? MeshFor(int sceneId) => _data != null && _meshByScene.TryGetValue(sceneId, out var m) ? m : null;

        /// <summary>The page a mesh is lightmapped on (-1: none).</summary>
        public static int PageOf(ushort meshId) => _data != null && _pageOfMesh.TryGetValue(meshId, out int p) ? p : -1;

        public static sg_view LightView(int page) => (uint)page < (uint)_light.Length ? _light[page] : default;
        public static sg_view DirectionView(int page) => (uint)page < (uint)_dir.Length ? _dir[page] : default;

        /// <summary>A Baked light of the loaded bake (drawn by the maps, not in real time).</summary>
        public static bool IsBakedLight(int sceneId) => _data != null && _bakedLights.Contains(sceneId);

        /// <summary>The editor: the bake of the scene <paramref name="sceneKey"/> from the project's Assets folder (none there:
        /// nothing loaded).</summary>
        public static bool LoadFromFolder(string assetsFolder, string sceneKey)
        {
            Unload();
            string folderKey = FolderOf(sceneKey);
            string dir = Path.Combine(assetsFolder, folderKey);
            string json = Path.Combine(dir, LightingData.JsonFile);
            if (!File.Exists(json)) return false;
            var data = LightingData.Parse(File.ReadAllText(json));
            if (data == null || !data.ReadMeshes(File.ReadAllBytes(Path.Combine(dir, LightingData.MeshFile)))) return false;
            var light = new byte[data.Pages.Count][]; var dirs = new byte[data.Pages.Count][];
            for (int p = 0; p < data.Pages.Count; p++)
            {
                light[p] = File.ReadAllBytes(Path.Combine(dir, data.Pages[p].Light));
                if (data.Directional && data.Pages[p].Direction.Length > 0) dirs[p] = File.ReadAllBytes(Path.Combine(dir, data.Pages[p].Direction));
            }
            var refl = new byte[data.ReflectionProbes.Count][][];
            for (int r = 0; r < refl.Length; r++)
            {
                refl[r] = new byte[6][];
                for (int k = 0; k < 6; k++)
                {
                    string f = Path.Combine(dir, data.ReflectionProbes[r].Faces[k]);
                    refl[r][k] = File.Exists(f) ? File.ReadAllBytes(f) : Array.Empty<byte>();
                }
            }
            Activate(folderKey, data, light, dirs, refl);
            return true;
        }

        /// <summary>A built game: the bake of the scene <paramref name="sceneKey"/> from its files, when there is one (asked
        /// for asynchronously; drawn from the frame it is all read).</summary>
        public static void LoadForSceneAsync(string sceneKey)
        {
            Unload();
            string folderKey = FolderOf(sceneKey);
            int asked = _version;
            SFilesystem.LoadFileAsync(folderKey + "/" + LightingData.JsonFile, (_, jsonBytes, status) =>
            {
                if (status != SFileLoadStatus.Success || asked != _version) return;   // no bake (or another scene since)
                var data = LightingData.Parse(System.Text.Encoding.UTF8.GetString(jsonBytes));
                if (data == null) return;
                SFilesystem.LoadFileAsync(folderKey + "/" + LightingData.MeshFile, (_, meshBytes, ms) =>
                {
                    if (ms != SFileLoadStatus.Success || asked != _version || !data.ReadMeshes(meshBytes)) return;
                    var light = new byte[data.Pages.Count][]; var dirs = new byte[data.Pages.Count][];
                    var refl = new byte[data.ReflectionProbes.Count][][];
                    for (int r = 0; r < refl.Length; r++) refl[r] = new byte[6][];
                    int waiting = data.Pages.Count * (data.Directional ? 2 : 1) + refl.Length * 6;
                    if (waiting == 0) { Activate(folderKey, data, light, dirs, refl); return; }
                    void One(string name, byte[][] into, int p)
                        => SFilesystem.LoadFileAsync(folderKey + "/" + name, (_, b, st) =>
                        {
                            if (asked != _version) return;
                            if (st == SFileLoadStatus.Success) into[p] = b;
                            if (--waiting == 0) Activate(folderKey, data, light, dirs, refl);
                        });
                    for (int p = 0; p < data.Pages.Count; p++)
                    {
                        One(data.Pages[p].Light, light, p);
                        if (data.Directional) One(data.Pages[p].Direction, dirs, p);
                    }
                    for (int r = 0; r < refl.Length; r++)
                        for (int k = 0; k < 6; k++) One(data.ReflectionProbes[r].Faces[k], refl[r], k);
                });
            });
        }

        private static void Activate(string folderKey, LightingData data, byte[][] light, byte[][] dirs, byte[][][] refl)
        {
            var tex = RenderingServer.Textures;
            _light = new sg_view[data.Pages.Count];
            _dir = new sg_view[data.Pages.Count];
            for (int p = 0; p < data.Pages.Count; p++)
            {
                _light[p] = light[p] is { Length: > 0 } lb ? tex.LoadFromBytes(TextureKey(folderKey, data.Pages[p].Light), lb, srgb: false) : tex.PlaceholderBlack;
                _dir[p] = dirs[p] is { Length: > 0 } db ? tex.LoadFromBytes(TextureKey(folderKey, data.Pages[p].Direction), db, srgb: false) : tex.PlaceholderWhite;
            }
            foreach (var (id, subs) in data.Meshes)
            {
                if (!data.EntityPage.TryGetValue(id, out int page)) continue;
                var bmin = new System.Numerics.Vector3(float.MaxValue); var bmax = new System.Numerics.Vector3(float.MinValue);
                foreach (var sm in subs) foreach (var v in sm.Vertices) { bmin = System.Numerics.Vector3.Min(bmin, v.Position); bmax = System.Numerics.Vector3.Max(bmax, v.Position); }
                ushort meshId = RenderingServer.Meshes.RegisterOrReplaceMesh(MeshKey(folderKey, id), subs, new Aabb(bmin, bmax));
                if (RenderingServer.Meshes.GetById(meshId) is not { } res) continue;
                // the source mesh's sub-mesh materials, by name (the renderer resolves them by key)
                _meshByScene[id] = res;
                _pageOfMesh[meshId] = page;
            }
            foreach (int id in data.BakedLights) _bakedLights.Add(id);
            // reflection probes (M8c): each one's six faces decoded, a mipped cube
            var faces = new List<byte[][]>();
            for (int r = 0; r < data.ReflectionProbes.Count; r++)
            {
                var one = new byte[6][];
                for (int k = 0; k < 6 && one != null; k++)
                    one[k] = refl[r][k] is { Length: > 0 } png && DecodeRgba(png, data.ReflectionProbes[r].Size) is { } rgba ? rgba : null!;
                faces.Add(Array.TrueForAll(one, f => f != null) ? one : null!);
            }
            ReflectionProbes.Load("lightmap:" + folderKey, data.ReflectionProbes, faces);
            _probePos = new System.Numerics.Vector3[data.Probes.Count];
            _probePacked = new System.Numerics.Vector4[data.Probes.Count * 9];
            for (int i = 0; i < data.Probes.Count; i++)
            {
                _probePos[i] = data.Probes[i].Position;
                Lighting.AmbientSH.FromRadianceSH(data.Probes[i].Radiance).Pack(_probePacked.AsSpan(i * 9, 9), 1f);
            }
            _folderKey = folderKey;
            _data = data;
            _version++;
            Logger.Info($"[Lightmaps] '{folderKey}': {data.Pages.Count} page(s), {_meshByScene.Count} lightmapped, {data.BakedLights.Count} baked light(s), {data.Probes.Count} probe(s), {ReflectionProbes.Count} reflection probe(s)");
        }

        /// <summary>Drops the loaded bake: its textures and meshes freed.</summary>
        public static void Unload()
        {
            _version++;
            if (_data == null) return;
            foreach (var p in _data.Pages)
            {
                RenderingServer.Textures.Release(TextureKey(_folderKey, p.Light));
                if (p.Direction.Length > 0) RenderingServer.Textures.Release(TextureKey(_folderKey, p.Direction));
            }
            foreach (int id in _meshByScene.Keys) RenderingServer.Meshes.Remove(MeshKey(_folderKey, id));
            ReflectionProbes.Unload();
            _meshByScene.Clear(); _pageOfMesh.Clear(); _bakedLights.Clear();
            _probePos = Array.Empty<System.Numerics.Vector3>(); _probePacked = Array.Empty<System.Numerics.Vector4>();
            _light = Array.Empty<sg_view>(); _dir = Array.Empty<sg_view>();
            _data = null; _folderKey = "";
        }

        private static unsafe byte[]? DecodeRgba(byte[] png, int size)
        {
            int w = 0, h = 0, ch = 0;
            byte* px = Sokol.StbImage.stbi_load_csharp(in png[0], png.Length, ref w, ref h, ref ch, 4);
            if (px == null) return null;
            try { return w == size && h == size ? new Span<byte>(px, w * h * 4).ToArray() : null; }
            finally { Sokol.StbImage.stbi_image_free_csharp(px); }
        }

        private static string TextureKey(string folderKey, string file) => "lightmap:" + folderKey + "/" + file;
        private static string MeshKey(string folderKey, int sceneId) => "lightmap:" + folderKey + "#" + sceneId;
    }
}
