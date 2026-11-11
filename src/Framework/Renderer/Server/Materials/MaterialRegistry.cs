// MaterialRegistry.cs — path → Material mapping with 16-bit id allocation.
// Loads .mtl files via MtlLoader, resolves texture views from TextureCache,
// and builds BlinnPhongMaterial records ready for GPU binding.
// Not thread-safe (Sokol main-thread constraint).

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Materials
{
    using Resources;

    public sealed class MaterialRegistry : IDisposable
    {
        private readonly Dictionary<string, Material> _byPath = new(StringComparer.Ordinal);
        private readonly Material?[] _byId;     // sparse id → material
        private ushort   _nextId = 1;           // 0 = invalid
        private readonly TextureCache _textures;
        private bool _disposed;

        public MaterialRegistry(TextureCache textures, int capacity = 4096)
        {
            _textures = textures;
            _byId     = new Material?[capacity];
        }

        // ── public API ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Parse an MTL file and register all materials it defines.
        /// Returns the id of the first material in the file (or 0 on failure).
        /// basePath is the directory of the .obj file — texture paths are resolved relative to it.
        /// </summary>
        public ushort LoadMtl(string mtlFilePath, byte[]? mtlData, string basePath,
                              Func<string, byte[]?>? fileLoader = null)
        {
            var parsed = MtlLoader.Load(mtlData);
            ushort firstId = 0;

            foreach (var (name, mtl) in parsed)
            {
                string key = mtlFilePath + "#" + name;
                if (_byPath.TryGetValue(key, out var existing))
                {
                    if (firstId == 0) firstId = existing.Id;
                    continue;
                }

                if (_nextId >= _byId.Length) break; // registry full

                var mat = BuildBlinnPhong(mtl, basePath, fileLoader);
                ushort id = _nextId++;
                mat.Id   = id;
                mat.Name = name;

                _byPath[key] = mat;
                _byId[id]    = mat;
                if (firstId == 0) firstId = id;
            }

            return firstId;
        }

        /// <summary>
        /// Register a pre-built PBR material under <paramref name="key"/> and assign it an id.
        /// Returns the assigned id (or the existing id if the key is already registered, or 0
        /// when the registry is full).  Used by the glTF importer and the Inspector's PBR
        /// material authoring path.
        /// </summary>
        public ushort RegisterPbr(string key, PbrMaterial mat)
        {
            if (_byPath.TryGetValue(key, out var existing)) return existing.Id;
            if (_nextId >= _byId.Length) return 0;

            ushort id = _nextId++;
            mat.Id = id;
            if (string.IsNullOrEmpty(mat.Name)) mat.Name = key;
            if (mat.Sampler.id == 0) mat.Sampler = _textures.DefaultSampler;

            _byPath[key] = mat;
            _byId[id]    = mat;
            return id;
        }

        /// <summary>
        /// Register a PBR material under <paramref name="key"/>, replacing any material already
        /// registered under that key (keeping its id so existing DrawCommand references stay valid).
        /// Used when a <c>.pbrmat</c> asset is (re)loaded — e.g. on scene reload after edits.
        /// </summary>
        public ushort RegisterOrReplacePbr(string key, PbrMaterial mat)
        {
            if (!_byPath.TryGetValue(key, out var existing)) return RegisterPbr(key, mat);

            mat.Id = existing.Id;
            if (string.IsNullOrEmpty(mat.Name)) mat.Name = key;
            if (mat.Sampler.id == 0) mat.Sampler = _textures.DefaultSampler;
            _byPath[key]       = mat;
            _byId[existing.Id] = mat;
            return existing.Id;
        }

        /// <summary>Look up a material by 16-bit id. Returns null if not registered.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Material? GetById(ushort id)
            => (id > 0 && id < _byId.Length) ? _byId[id] : null;

        /// <summary>
        /// True if any registered material refracts the background (KHR_materials_transmission).
        /// Cheap O(material-count) scan — lets a host skip the offscreen render-target + screen-copy
        /// blit entirely (rendering straight to the swapchain) when no transmission is in play.
        /// </summary>
        public bool AnyTransmissive
        {
            get
            {
                for (int i = 1; i < _nextId; i++)
                    if (_byId[i] is PbrMaterial { IsTransmissive: true }) return true;
                return false;
            }
        }

        /// <summary>Look up a material by "mtlFile#materialName" key. Returns null if not registered.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Material? GetByKey(string key)
            => _byPath.TryGetValue(key, out var m) ? m : null;

        /// <summary>Returns the Id of the material for <paramref name="key"/>, or 0 if not registered.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetIdByKey(string key)
            => _byPath.TryGetValue(key, out var m) ? m.Id : (ushort)0;

        /// <summary>
        /// Returns the id of the first material registered from the given MTL file, by the file's key
        /// (its path below Assets, "Models/duck.mtl") — the registry keys are "&lt;key&gt;#&lt;materialName&gt;".
        /// Returns 0 if no material from that file is registered.
        /// </summary>
        public ushort GetFirstIdByMtlFile(string mtlPath)
        {
            string prefix = mtlPath.Replace('\\', '/') + "#";
            foreach (var (key, mat) in _byPath)
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    return mat.Id;
            return 0;
        }

        /// <summary>
        /// Returns the unique set of texture paths (Assets-relative or absolute) that were
        /// referenced by materials registered under <paramref name="mtlFilePath"/>.  These may
        /// have been left as GPU placeholders when LoadMtl was called without a fileLoader.
        /// </summary>
        public HashSet<string> GetTexturePaths(string mtlFilePath)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            string prefix = mtlFilePath + "#";
            foreach (var (key, mat) in _byPath)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (mat is not BlinnPhongMaterial bp) continue;
                if (!string.IsNullOrEmpty(bp.DiffuseMapPath))  result.Add(bp.DiffuseMapPath);
                if (!string.IsNullOrEmpty(bp.SpecularMapPath)) result.Add(bp.SpecularMapPath);
                if (!string.IsNullOrEmpty(bp.NormalMapPath))   result.Add(bp.NormalMapPath);
                if (!string.IsNullOrEmpty(bp.OpacityMapPath))  result.Add(bp.OpacityMapPath);
            }
            return result;
        }

        /// <summary>
        /// Upload <paramref name="data"/> to the GPU under key <paramref name="texKey"/> and
        /// patch every material that references it so it uses the real texture view.
        /// Call this after an async file load delivers bytes for a texture path.
        /// </summary>
        public void ApplyTextureBytes(string texKey, byte[] data)
        {
            sg_view view = _textures.LoadFromBytes(texKey, data);
            foreach (var mat in _byPath.Values)
            {
                if (mat is not BlinnPhongMaterial bp) continue;
                if (bp.DiffuseMapPath  == texKey) bp.DiffuseMap  = view;
                if (bp.SpecularMapPath == texKey) bp.SpecularMap = view;
                if (bp.NormalMapPath   == texKey) bp.NormalMap   = view;
                if (bp.OpacityMapPath  == texKey) bp.OpacityMap  = view;
            }
        }

        // ── PBR texture slots (Inspector material editor) ──────────────────────────────

        /// <summary>Identifies one of the five PBR metallic-roughness texture slots.</summary>
        public enum PbrTextureSlot { BaseColor, MetallicRoughness, Normal, Occlusion, Emissive }

        /// <summary>
        /// Decode and upload <paramref name="bytes"/> under <paramref name="key"/> and assign the
        /// resulting GPU view to the given PBR <paramref name="slot"/> on <paramref name="mat"/>.
        /// Base-colour and emissive maps upload as sRGB; the data maps (metallic-roughness, normal,
        /// occlusion) upload linear — matching the glTF importer's per-slot conventions
        /// (<see cref="Assets.GltfImporter"/>). Used by the Inspector's PBR material editor.
        /// </summary>
        public void LoadPbrTexture(PbrMaterial mat, PbrTextureSlot slot, string key, ReadOnlySpan<byte> bytes)
        {
            bool srgb = slot is PbrTextureSlot.BaseColor or PbrTextureSlot.Emissive;
            sg_view view = _textures.LoadFromBytes(key, bytes, srgb);
            if (mat.Sampler.id == 0) mat.Sampler = _textures.DefaultSampler;
            switch (slot)
            {
                case PbrTextureSlot.BaseColor:         mat.BaseColorMap         = view; mat.BaseColorMapPath         = key; break;
                case PbrTextureSlot.MetallicRoughness: mat.MetallicRoughnessMap = view; mat.MetallicRoughnessMapPath = key; break;
                case PbrTextureSlot.Normal:            mat.NormalMap            = view; mat.NormalMapPath            = key; break;
                case PbrTextureSlot.Occlusion:         mat.OcclusionMap         = view; mat.OcclusionMapPath         = key; break;
                case PbrTextureSlot.Emissive:          mat.EmissiveMap          = view; mat.EmissiveMapPath          = key; break;
            }
        }

        /// <summary>Clear one PBR texture slot back to "no map" (factor-only shading).</summary>
        public void ClearPbrTexture(PbrMaterial mat, PbrTextureSlot slot)
        {
            switch (slot)
            {
                case PbrTextureSlot.BaseColor:         mat.BaseColorMap         = default; mat.BaseColorMapPath         = ""; break;
                case PbrTextureSlot.MetallicRoughness: mat.MetallicRoughnessMap = default; mat.MetallicRoughnessMapPath = ""; break;
                case PbrTextureSlot.Normal:            mat.NormalMap            = default; mat.NormalMapPath            = ""; break;
                case PbrTextureSlot.Occlusion:         mat.OcclusionMap         = default; mat.OcclusionMapPath         = ""; break;
                case PbrTextureSlot.Emissive:          mat.EmissiveMap          = default; mat.EmissiveMapPath          = ""; break;
            }
        }

        // ── builder ──────────────────────────────────────────────────────────────────

        private BlinnPhongMaterial BuildBlinnPhong(
            MtlMaterial mtl, string basePath, Func<string, byte[]?>? fileLoader)
        {
            var mat = new BlinnPhongMaterial
            {
                DiffuseColor  = mtl.Kd,
                SpecularColor = mtl.Ks,
                Shininess     = MathF.Max(mtl.Ns, 1f),
                Opacity       = mtl.D,
                IsTransparent = mtl.D < 1f || !string.IsNullOrEmpty(mtl.MapD),

                DiffuseMap  = _textures.PlaceholderWhite,
                SpecularMap = _textures.PlaceholderWhite,
                NormalMap   = _textures.PlaceholderNormal,
                OpacityMap  = _textures.PlaceholderWhite,
            };

            mat.DiffuseMap  = LoadTexture(mtl.MapKd,   basePath, fileLoader, ref mat.DiffuseMapPath);
            mat.SpecularMap = LoadTexture(mtl.MapKs,   basePath, fileLoader, ref mat.SpecularMapPath);
            mat.NormalMap   = LoadTexture(mtl.MapBump, basePath, fileLoader, ref mat.NormalMapPath,
                                          fallback: _textures.PlaceholderNormal);
            mat.OpacityMap  = LoadTexture(mtl.MapD,    basePath, fileLoader, ref mat.OpacityMapPath);
            mat.Sampler     = _textures.DefaultSampler;

            return mat;
        }

        private sg_view LoadTexture(
            string relPath, string basePath,
            Func<string, byte[]?>? loader,
            ref string outPath,
            sg_view fallback = default)
        {
            if (string.IsNullOrEmpty(relPath))
                return fallback.id != 0 ? fallback : _textures.PlaceholderWhite;

            string absPath = Path.IsPathRooted(relPath)
                ? relPath
                : Path.Combine(basePath, relPath);

            outPath = absPath;

            byte[]? data = loader?.Invoke(absPath);
            if (data == null || data.Length == 0)
            {
                Console.Error.WriteLine($"[MaterialRegistry] texture not found: {absPath}");
                return fallback.id != 0 ? fallback : _textures.PlaceholderWhite;
            }

            return _textures.LoadFromBytes(absPath, data);
        }

        // ── lifecycle ────────────────────────────────────────────────────────────────

        public void Shutdown()
        {
            // TextureCache.Release() handles GPU texture teardown.
            foreach (var mat in _byPath.Values)
            {
                if (mat is BlinnPhongMaterial bp)
                {
                    if (!string.IsNullOrEmpty(bp.DiffuseMapPath))  _textures.Release(bp.DiffuseMapPath);
                    if (!string.IsNullOrEmpty(bp.SpecularMapPath)) _textures.Release(bp.SpecularMapPath);
                    if (!string.IsNullOrEmpty(bp.NormalMapPath))   _textures.Release(bp.NormalMapPath);
                    if (!string.IsNullOrEmpty(bp.OpacityMapPath))  _textures.Release(bp.OpacityMapPath);
                }
            }
            _byPath.Clear();
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
