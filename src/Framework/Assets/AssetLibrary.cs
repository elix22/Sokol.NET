using System;
using System.Collections.Generic;
using GameEditor.Framework.Core;
using GameEditor.Framework.Renderer.Server;
using GameEditor.Framework.Renderer.Server.Materials;
using Sokol;

namespace GameEditor.Framework.Assets
{
    /// <summary>
    /// The one place that turns a key — a file's path below Assets ("Models/duck.obj") — into a loaded mesh,
    /// material or texture, as <c>PrefabLibrary</c> does for prefabs. It brings a path to its key, reads the file
    /// — at once on the desktop, when it arrives on the web and on phones — and does not ask twice for a file that
    /// is on its way. A scene's load, a prefab placed while the game runs, and an editor's panels all ask here;
    /// the renderer then finds what was loaded by the same key. Main thread only.
    /// </summary>
    public static class AssetLibrary
    {
        /// <summary>
        /// An <c>.obj</c> mesh was loaded and the <c>.mtl</c> it names was found beside it: the mesh's key and the
        /// <c>.mtl</c>'s. (A scene writes the material into the entities that show the mesh and name none.)
        /// </summary>
        public static event Action<string, string>? MeshMaterialFound;

        /// <summary>
        /// Asks for a glTF / GLB file's meshes, materials and textures, by the file's key — once, however many
        /// entities name the model.
        /// </summary>
        public static void Gltf(string file)
        {
            if (!_pendingGltfPreloads.Add(file)) return;
            string captured = file;
            RenderingServer.PreloadGltfAsync(captured, () => _pendingGltfPreloads.Remove(captured));
        }

        /// <summary>
        /// Asks for the material a key names: a <c>.pbrmat</c> file, or a <c>.mtl</c> file (all of its materials).
        /// What is loaded already is not read again. Any other key — a material of a glTF, which comes with its
        /// model — is left alone.
        /// </summary>
        public static void Material(string key)
        {
            if (key.EndsWith(PbrMaterialSerializer.Extension, StringComparison.OrdinalIgnoreCase)) LoadPbrMaterialAsset(key);
            else if (key.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) && RenderingServer.Materials.GetFirstIdByMtlFile(key) == 0) LoadMtl(key);
        }

        // Async mesh loads in flight (keyed by MeshPath); avoids duplicate SFilesystem requests.
        private static readonly HashSet<string> _pendingMeshLoads = new(StringComparer.Ordinal);
        // glTF/GLB files whose resources are currently being (re-)registered — dedupes the
        // per-file preload when many entities reference the same imported model.
        private static readonly HashSet<string> _pendingGltfPreloads = new(StringComparer.Ordinal);


        // ── Path helpers ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Normalizes a path to Assets-relative (e.g. "Models/duck.obj").
        /// Already-relative paths are returned unchanged.
        /// Absolute paths are stripped at the "/Assets/" marker.
        /// Returns null if an absolute path has no "/Assets/" component.
        /// </summary>
        public static string? ToAssetsRelativePath(string path)
        {
            if (!System.IO.Path.IsPathRooted(path)) return path; // already relative
            const string marker = "/Assets/";
            string normalised = path.Replace('\\', '/');
            int idx = normalised.IndexOf(marker, StringComparison.Ordinal);
            return idx >= 0 ? normalised.Substring(idx + marker.Length) : null;
        }

        /// <summary>
        /// Resolves an Assets-relative path to an absolute filesystem path on desktop
        /// using <see cref="ConfigManager.ProjectFolder"/>. Returns null when no project
        /// is loaded or when the input is already absolute.
        /// </summary>
        private static string? ToAbsoluteDesktopPath(string relPath)
        {
            if (System.IO.Path.IsPathRooted(relPath)) return relPath; // already absolute
            if (!ConfigManager.HasProject) return null;
            return System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", relPath);
        }

        /// <summary>Asks for a mesh file by its Assets-relative path, unless it is loaded or on its way.</summary>
        public static void Mesh(string path)
        {
            if (RenderingServer.Meshes.GetByPath(path) != null) return;

            // glTF/GLB resource key ("<file>.glb#m{i}p{j}"): re-register the file's meshes,
            // materials, and textures (NOT entities — those are already deserialized). One
            // preload per unique file covers every primitive it references.
            int hashIdx = path.IndexOf('#');
            if (hashIdx > 0)
            {
                string gltfFile = path.Substring(0, hashIdx);
                if (gltfFile.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ||
                    gltfFile.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                {
                    Gltf(gltfFile);
                    return;
                }
            }

            if (_pendingMeshLoads.Contains(path)) return;

            // Try desktop sync path first (resolves relative → absolute via ProjectFolder).
            string? absPath = ToAbsoluteDesktopPath(path);
            if (absPath != null && System.IO.File.Exists(absPath))
            {
                RenderingServer.Meshes.Load(path, System.IO.File.ReadAllBytes(absPath));
                TryLoadMeshMtlDesktop(path);
            }
            else
            {
                // Web / mobile: async load using the Assets-relative path.
                _pendingMeshLoads.Add(path);
                string captured = path;
                SFilesystem.LoadFileAsync(path, (_, buffer, status) =>
                {
                    _pendingMeshLoads.Remove(captured);
                    if (status == SFileLoadStatus.Success && buffer != null)
                    {
                        RenderingServer.Meshes.Load(captured, buffer);
                        TryLoadMeshMtlAsync(captured);
                    }
                    else
                        Logger.Warning($"[Assets] Failed to load mesh: '{captured}'");
                });
            }
        }

        /// <summary>
        /// After an OBJ mesh has been loaded into MeshRegistry, load its co-located .mtl.
        /// Selects sync (desktop) or async (web/mobile) path automatically.
        /// <paramref name="meshRelPath"/> must be Assets-relative (e.g. "Models/duck.obj").
        /// </summary>
        public static void TryLoadMeshMtl(string meshRelPath)
        {
            string? absPath = ToAbsoluteDesktopPath(meshRelPath);
            if (absPath != null && System.IO.File.Exists(absPath))
                TryLoadMeshMtlDesktop(meshRelPath);
            else
                TryLoadMeshMtlAsync(meshRelPath);
        }

        /// <summary>
        /// After an OBJ mesh has been loaded, load its co-located .mtl file synchronously.
        /// <paramref name="meshRelPath"/> is Assets-relative (e.g. "Models/duck.obj").
        /// </summary>
        private static void TryLoadMeshMtlDesktop(string meshRelPath)
        {
            var meshRes = RenderingServer.Meshes.GetByPath(meshRelPath);
            if (meshRes == null || string.IsNullOrEmpty(meshRes.MtlLib)) return;

            string mtlRelPath = Renderer.Server.Resources.MeshRegistry.MtlKey(meshRelPath, meshRes.MtlLib);
            string? mtlAbs    = ToAbsoluteDesktopPath(mtlRelPath);
            if (mtlAbs == null || !System.IO.File.Exists(mtlAbs)) return;

            string basePath = System.IO.Path.GetDirectoryName(mtlAbs) ?? string.Empty;
            byte[] bytes    = System.IO.File.ReadAllBytes(mtlAbs);
            RenderingServer.Materials.LoadMtl(mtlRelPath, bytes, basePath, relPath =>
            {
                string abs = System.IO.Path.IsPathRooted(relPath) ? relPath : System.IO.Path.Combine(basePath, relPath);
                return System.IO.File.Exists(abs) ? System.IO.File.ReadAllBytes(abs) : null;
            });
            MeshMaterialFound?.Invoke(meshRelPath, mtlRelPath);
        }

        /// <summary>
        /// After an OBJ mesh has been loaded async, load its co-located .mtl file async.
        /// <paramref name="meshRelPath"/> is Assets-relative (e.g. "Models/duck.obj").
        /// Textures fall back to placeholders on web; material colors and shininess apply.
        /// </summary>
        private static void TryLoadMeshMtlAsync(string meshRelPath)
        {
            var meshRes = RenderingServer.Meshes.GetByPath(meshRelPath);
            if (meshRes == null || string.IsNullOrEmpty(meshRes.MtlLib)) return;

            string meshDir    = System.IO.Path.GetDirectoryName(meshRelPath)?.Replace('\\', '/') ?? string.Empty;
            string mtlRelPath = Renderer.Server.Resources.MeshRegistry.MtlKey(meshRelPath, meshRes.MtlLib);
            string capturedKey          = mtlRelPath;
            string capturedDir          = meshDir;
            string capturedMeshRelPath  = meshRelPath;
            string capturedMtlRelPath   = mtlRelPath;
            SFilesystem.LoadFileAsync(mtlRelPath, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    RenderingServer.Materials.LoadMtl(capturedKey, buffer, capturedDir, null);
                    LoadMaterialTexturesAsync(capturedKey);
                    MeshMaterialFound?.Invoke(capturedMeshRelPath, capturedMtlRelPath);
                }
                else
                    Logger.Warning($"[Assets] Failed to load MTL: '{mtlRelPath}'");
            });
        }

        /// <summary>Loads a <c>.mtl</c> file by its Assets-relative path: at once on desktop, when it arrives on web and phones.</summary>
        public static void LoadMtl(string relPath)
        {
            // The registry key is the file's own key, as in sub.MaterialKey ("Models/duck.mtl#materialName"):
            // two .mtl files of one name in two folders are two sets of materials.
            string mtlFileName = relPath.Replace('\\', '/');

            // Desktop: synchronous load.
            string? absPath = ToAbsoluteDesktopPath(relPath);
            if (absPath != null && System.IO.File.Exists(absPath))
            {
                string basePath = System.IO.Path.GetDirectoryName(absPath) ?? string.Empty;
                byte[] bytes    = System.IO.File.ReadAllBytes(absPath);
                RenderingServer.Materials.LoadMtl(mtlFileName, bytes, basePath, rp =>
                {
                    string abs = System.IO.Path.IsPathRooted(rp) ? rp : System.IO.Path.Combine(basePath, rp);
                    return System.IO.File.Exists(abs) ? System.IO.File.ReadAllBytes(abs) : null;
                });
                return;
            }

            // Web / mobile: async load, then async-load any textures referenced by the MTL.
            string capturedKey = mtlFileName;
            string capturedDir = System.IO.Path.GetDirectoryName(relPath)?.Replace('\\', '/') ?? string.Empty;
            SFilesystem.LoadFileAsync(relPath, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    RenderingServer.Materials.LoadMtl(capturedKey, buffer, capturedDir, null);
                    LoadMaterialTexturesAsync(capturedKey);
                }
                else
                    Logger.Warning($"[Assets] Failed to load MTL: '{relPath}'");
            });
        }

        /// <summary>
        /// After materials have been registered for <paramref name="mtlKey"/>, async-load every
        /// texture path they reference via SFilesystem and patch the GPU views on arrival.
        /// </summary>
        private static void LoadMaterialTexturesAsync(string mtlKey)
        {
            var texPaths = RenderingServer.Materials.GetTexturePaths(mtlKey);
            foreach (string texPath in texPaths)
            {
                string captured = texPath;
                SFilesystem.LoadFileAsync(texPath, (_, buffer, status) =>
                {
                    if (status == SFileLoadStatus.Success && buffer != null)
                        RenderingServer.Materials.ApplyTextureBytes(captured, buffer);
                    else
                        Logger.Warning($"[Assets] Failed to load texture: '{captured}'");
                });
            }
        }

        /// <summary>
        /// Load the image at <paramref name="uiPath"/> and assign it to a PBR texture
        /// <paramref name="slot"/> of the live material registered under <paramref name="materialKey"/>.
        /// An empty path clears the slot. Desktop reads synchronously; web/mobile load async via
        /// SFilesystem. Used by the Inspector's PBR material editor.
        /// </summary>
        public static void ApplyPbrMaterialTexture(
            string materialKey, MaterialRegistry.PbrTextureSlot slot, string? uiPath)
        {
            if (RenderingServer.Materials.GetByKey(materialKey) is not PbrMaterial mat) return;

            string trimmed = uiPath?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                RenderingServer.Materials.ClearPbrTexture(mat, slot);
                return;
            }

            // Normalize to an Assets-relative key so the cache entry is portable across platforms.
            string rel = ToAssetsRelativePath(trimmed) ?? trimmed;

            // Desktop: synchronous load.
            string? absPath = ToAbsoluteDesktopPath(rel);
            if (absPath != null && System.IO.File.Exists(absPath))
            {
                RenderingServer.Materials.LoadPbrTexture(mat, slot, rel, System.IO.File.ReadAllBytes(absPath));
                return;
            }

            // Web / mobile: async load.
            string capturedKey = materialKey;
            SFilesystem.LoadFileAsync(rel, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    if (RenderingServer.Materials.GetByKey(capturedKey) is PbrMaterial m)
                        RenderingServer.Materials.LoadPbrTexture(m, slot, rel, buffer);
                }
                else
                    Logger.Warning($"[Assets] Failed to load PBR texture: '{rel}'");
            });
        }

        // ── .pbrmat material assets ─────────────────────────────────────────────────────────

        /// <summary>
        /// Load a <c>.pbrmat</c> material asset, register it under its Assets-relative key
        /// (= <see cref="MeshRenderer.MaterialPath"/>), and load each referenced texture map.
        /// No-op when the material is already registered (keeps in-memory edits/views). Desktop
        /// reads synchronously; web/mobile load async via SFilesystem.
        /// </summary>
        public static void LoadPbrMaterialAsset(string relPath)
        {
            string key = relPath.Replace('\\', '/');
            if (RenderingServer.Materials.GetByKey(key) is PbrMaterial) return; // already loaded this session

            string? absPath = ToAbsoluteDesktopPath(relPath);
            if (absPath != null && System.IO.File.Exists(absPath))
            {
                RegisterAndLoadPbr(key, System.IO.File.ReadAllBytes(absPath));
                return;
            }

            SFilesystem.LoadFileAsync(relPath, (_, buffer, status) =>
            {
                if (status == SFileLoadStatus.Success && buffer != null) RegisterAndLoadPbr(key, buffer);
                else Logger.Warning($"[Assets] Failed to load .pbrmat: '{relPath}'");
            });
        }

        private static void RegisterAndLoadPbr(string key, byte[] json)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(key);
            PbrMaterial mat = PbrMaterialSerializer.Deserialize(json, name);
            RenderingServer.Materials.RegisterOrReplacePbr(key, mat);

            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.BaseColor,         mat.BaseColorMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.MetallicRoughness, mat.MetallicRoughnessMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.Normal,            mat.NormalMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.Occlusion,         mat.OcclusionMapPath);
            ApplyMapIfSet(MaterialRegistry.PbrTextureSlot.Emissive,          mat.EmissiveMapPath);

            void ApplyMapIfSet(MaterialRegistry.PbrTextureSlot slot, string? p)
            {
                if (!string.IsNullOrEmpty(p)) ApplyPbrMaterialTexture(key, slot, p);
            }
        }

        /// <summary>
        /// Write <paramref name="mat"/> to a new <c>Assets/Materials/&lt;name&gt;.pbrmat</c>
        /// (deduped), register it under the resulting Assets-relative key, and return that key.
        /// Editor-only (desktop authoring); returns null with no project or on write failure.
        /// </summary>
        public static string? SaveNewPbrMaterialAsset(PbrMaterial mat, string preferredName)
        {
            if (!ConfigManager.HasProject) return null;
            string matsDirAbs = System.IO.Path.Combine(ConfigManager.ProjectFolder!, "Assets", "Materials");
            try { SokolFile.EnsureDirectory(matsDirAbs); } catch { }

            string baseName = SanitizeFileName(string.IsNullOrWhiteSpace(preferredName) ? "Material" : preferredName);
            string fileName = baseName;
            string absPath  = System.IO.Path.Combine(matsDirAbs, fileName + PbrMaterialSerializer.Extension);
            int n = 1;
            while (System.IO.File.Exists(absPath))
            {
                fileName = $"{baseName}_{n++}";
                absPath  = System.IO.Path.Combine(matsDirAbs, fileName + PbrMaterialSerializer.Extension);
            }

            mat.Name = fileName;
            string relKey = $"Materials/{fileName}{PbrMaterialSerializer.Extension}";
            try { System.IO.File.WriteAllText(absPath, Assets.AssetRefs.Stamp(PbrMaterialSerializer.Serialize(mat))); }
            catch (Exception ex) { Logger.Warning($"[Assets] Failed to write .pbrmat: {ex.Message}"); return null; }

            RenderingServer.Materials.RegisterOrReplacePbr(relKey, mat);
            return relKey;
        }

        /// <summary>Re-serialize an already file-backed material to its <c>.pbrmat</c> (auto-save on edit). Desktop-only.</summary>
        public static void SavePbrMaterialAsset(string relKey, PbrMaterial mat)
        {
            string? absPath = ToAbsoluteDesktopPath(relKey);
            if (absPath == null) return; // web/mobile bundle is read-only
            try
            {
                string? dir = System.IO.Path.GetDirectoryName(absPath);
                if (dir != null) SokolFile.EnsureDirectory(dir);
                System.IO.File.WriteAllText(absPath, Assets.AssetRefs.Stamp(PbrMaterialSerializer.Serialize(mat)));
            }
            catch (Exception ex) { Logger.Warning($"[Assets] Failed to save .pbrmat '{relKey}': {ex.Message}"); }
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(System.Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
