// GltfImporter.cs — loads .glb/.gltf via the native cgltf bindings (Sokol.CGltf)
// and translates the result into RenderingServer resources + ECS entities:
//   • each glTF primitive  → a single-sub-mesh MeshResource in MeshRegistry
//   • each glTF material   → a PbrMaterial in MaterialRegistry (RegisterPbr)
//   • each glTF image      → an sg texture in TextureCache
//   • each glTF node       → an ECS entity (Transform + optional MeshRenderer),
//                            parented to mirror the glTF node hierarchy.
//
// Modelled on examples/CGltfViewer/Source/CGltfModel.cs (the proven reference) but
// targets the Framework's 48 B ObjVertex layout so imported meshes render through the
// existing PBR/blinn pipelines unchanged.
//
// CROSS-PLATFORM: all file access goes through SFilesystem (sokol-fetch) — never
// System.IO.File — so it works on Desktop, Mobile, and Web. Paths are Assets-relative
// (resolved by SFilesystem against the current project's Assets folder). Because Web
// cannot block on I/O, the import is ASYNC: the main file, external .bin buffers, and
// external texture images are fetched via callbacks; the scene is built once all bytes
// have arrived, then onComplete fires on the main thread.
//
// Scope (M4): STATIC meshes + PBR metallic-roughness materials only. Skinning/morph
// extraction is deferred — skinned models import in bind pose (joints/weights dropped
// from the 48 B vertex). Not thread-safe (Sokol main-thread constraint; sfetch callbacks
// fire on the main thread during SFilesystem.Update()).

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Materials;
using GameEditor.Framework.Renderer.Server.Resources;
using GameEditor.Framework.Renderer.Server.Animation;
using Frent;
using Sokol;
using static Sokol.CGltf;

namespace GameEditor.Framework.Renderer.Server.Assets
{
    public static class GltfImporter
    {
        /// <summary>
        /// Imports the glTF/GLB at <paramref name="assetsRelativePath"/> (e.g. "Models/Helmet.glb",
        /// resolved against the current project's Assets folder via SFilesystem). Registers all
        /// meshes/materials/textures and builds the node hierarchy under one container entity, then
        /// invokes <paramref name="onComplete"/> with it (or null on failure) on the main thread.
        /// Asynchronous: external .bin/texture files are fetched before the scene is built.
        /// </summary>
        public static void ImportAsync(
            string assetsRelativePath,
            MeshRegistry meshReg, MaterialRegistry matReg, TextureCache texCache, ECSWorld world,
            Action<Entity?>? onComplete = null)
            => Start(assetsRelativePath, meshReg, matReg, texCache, world, buildEntities: true, onComplete);

        /// <summary>
        /// Re-registers a glTF/GLB's meshes, materials, and textures WITHOUT creating entities.
        /// Used on scene reload: the entities are already deserialized, only their resources —
        /// keyed <c>"&lt;file&gt;#m{i}p{j}"</c> / <c>"&lt;file&gt;#mat{i}"</c> — need re-populating.
        /// Cross-platform + async, same as <see cref="ImportAsync"/>.
        /// </summary>
        public static void PreloadAsync(
            string assetsRelativePath,
            MeshRegistry meshReg, MaterialRegistry matReg, TextureCache texCache, ECSWorld world,
            Action? onComplete = null)
            => Start(assetsRelativePath, meshReg, matReg, texCache, world, buildEntities: false,
                     _ => onComplete?.Invoke());

        /// <summary>
        /// Reads a model's file again and replaces what was registered from it, key for key, without creating
        /// entities: a mesh and a material keep their id, the file's textures are made anew, and a mesh key the
        /// new file no longer has is removed. When the file cannot be read or parsed, nothing is touched.
        /// <paramref name="onComplete"/>: whether it was read, and the mesh keys that were removed.
        /// </summary>
        public static void ReimportAsync(
            string assetsRelativePath,
            MeshRegistry meshReg, MaterialRegistry matReg, TextureCache texCache, ECSWorld world,
            Action<bool, IReadOnlyList<string>>? onComplete = null)
        {
            Logger.Info($"[glTF] reading '{assetsRelativePath}' again");
            ImportJob? job = null;
            job = new ImportJob(assetsRelativePath, meshReg, matReg, texCache, world, buildEntities: false,
                                _ => onComplete?.Invoke(job!.Succeeded, job.Lost)) { Replace = true };
            LoadFile(GameEditor.Framework.Assets.AssetLibrary.FileOf(assetsRelativePath), job.OnMainFileLoaded);
        }

        private static void Start(
            string assetsRelativePath,
            MeshRegistry meshReg, MaterialRegistry matReg, TextureCache texCache, ECSWorld world,
            bool buildEntities, Action<Entity?>? onComplete)
        {
            Logger.Info($"[glTF] {(buildEntities ? "importing" : "preloading")} '{assetsRelativePath}' (project: {ConfigManager.ProjectFolder ?? "<none>"})");
            var job = new ImportJob(assetsRelativePath, meshReg, matReg, texCache, world, buildEntities, onComplete);
            // The keys are the model's own ("Models/ship.blend#m0p0"); a .blend's bytes are in the GLB beside it.
            LoadFile(GameEditor.Framework.Assets.AssetLibrary.FileOf(assetsRelativePath), job.OnMainFileLoaded);
        }

        // Loads an Assets-relative file cross-platform. On DESKTOP the editor targets an external
        // project folder that sokol-fetch's fileutil_get_path does NOT resolve (it returns the path
        // verbatim, relative to the editor's cwd), so read straight from <project>/Assets — the same
        // resolution the OBJ loader uses. On WEB/MOBILE assets are bundled flat next to the app, where
        // SFilesystem resolves correctly. The desktop branch invokes the callback synchronously; the
        // async ImportJob handles either timing.
        private static void LoadFile(string relPath, SFileLoadCallback callback)
        {
            if (ConfigManager.HasProject)
            {
                string abs = Path.Combine(ConfigManager.ProjectFolder!, "Assets",
                    relPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(abs))
                {
                    byte[]? bytes = null;
                    try { bytes = File.ReadAllBytes(abs); }
                    catch (Exception ex) { Logger.Error($"[glTF] read failed '{abs}': {ex.Message}"); }
                    callback(relPath, bytes, bytes != null ? SFileLoadStatus.Success : SFileLoadStatus.Failed);
                    return;
                }
                Logger.Info($"[glTF] '{abs}' not on disk → falling back to SFilesystem");
            }
            SFilesystem.LoadFileAsync(relPath, callback);
        }

        // One in-flight import. Owns the pinned glTF bytes + cgltf_data* across the async fetches
        // and frees them once the scene is built (or the import fails).
        private sealed unsafe class ImportJob
        {
            private readonly string _path;     // assets-relative path of the .gltf/.glb
            private readonly string _baseDir;  // assets-relative directory ("" when at Assets root)
            private readonly MeshRegistry     _meshReg;
            private readonly MaterialRegistry _matReg;
            private readonly TextureCache     _texCache;
            private readonly ECSWorld         _world;
            private readonly bool             _buildEntities;
            private readonly Action<Entity?>? _onComplete;

            private GCHandle _mainPin;
            private readonly List<GCHandle> _extPins = new();
            private cgltf_data* _data;
            private readonly Dictionary<int, byte[]> _images = new(); // image index → encoded bytes
            private int  _pending;
            private bool _failed;
            private bool _done;

            /// <summary>Replace what is registered under this file's keys (a file read again), not keep it.</summary>
            public bool Replace;
            /// <summary>The scene was built: the file was read and parsed and its resources are registered.</summary>
            public bool Succeeded;
            /// <summary>With <see cref="Replace"/>: mesh keys that were registered from the file and are not in it now.</summary>
            public readonly List<string> Lost = new();

            public ImportJob(string path, MeshRegistry meshReg, MaterialRegistry matReg,
                             TextureCache texCache, ECSWorld world, bool buildEntities, Action<Entity?>? onComplete)
            {
                _path          = path;
                _baseDir       = DirOf(path);
                _meshReg       = meshReg;
                _matReg        = matReg;
                _texCache      = texCache;
                _world         = world;
                _buildEntities = buildEntities;
                _onComplete    = onComplete;
            }

            // ── Stage 1: main file loaded → parse + discover external resources ──────────────
            public void OnMainFileLoaded(string _, byte[]? buffer, SFileLoadStatus status)
            {
                if (status != SFileLoadStatus.Success || buffer == null) { Fail($"could not load file (status={status})"); return; }

                _mainPin = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                cgltf_options options = default;
                cgltf_data* data = null;
                cgltf_result r = cgltf_parse(options, (void*)_mainPin.AddrOfPinnedObject(),
                                             (nuint)buffer.Length, out data);
                if (r != cgltf_result.cgltf_result_success || data == null) { Fail($"cgltf_parse failed ({r})"); return; }
                _data = data;
                Logger.Info($"[glTF] parsed '{_path}': {(int)data->meshes_count} mesh(es), "
                    + $"{(int)data->materials_count} material(s), {(int)data->images_count} image(s)");

                // GLB embedded binary chunk → buffers[0].
                if (data->file_type == cgltf_file_type.cgltf_file_type_glb &&
                    data->buffers_count > 0 && data->buffers[0].data == null && data->bin != null)
                {
                    data->buffers[0].data             = data->bin;
                    data->buffers[0].data_free_method = cgltf_data_free_method.cgltf_data_free_method_none;
                }

                var bufLoads = new List<(int idx, string rel)>();
                for (int i = 0; i < (int)data->buffers_count; i++)
                {
                    cgltf_buffer* buf = &data->buffers[i];
                    if (buf->data != null) continue;
                    string? uri = PtrToStr(buf->uri);
                    if (uri == null || uri.StartsWith("data:", StringComparison.Ordinal)) continue;
                    bufLoads.Add((i, Combine(_baseDir, uri)));
                }

                var imgLoads = new List<(int idx, string rel)>();
                for (int i = 0; i < (int)data->images_count; i++)
                {
                    cgltf_image* img = &data->images[i];
                    if (img->buffer_view != null) continue;   // embedded — read from the buffer view
                    string? uri = PtrToStr(img->uri);
                    if (uri == null || uri.StartsWith("data:", StringComparison.Ordinal)) continue;
                    imgLoads.Add((i, Combine(_baseDir, uri)));
                }

                _pending = bufLoads.Count + imgLoads.Count;
                if (_pending == 0) { Finish(); return; }
                Logger.Info($"[glTF] fetching {bufLoads.Count} external buffer(s) + {imgLoads.Count} image(s)");

                foreach (var (idx, rel) in bufLoads)
                {
                    int captured = idx;
                    LoadFile(rel, (_, b, s) => OnBufferLoaded(captured, b, s));
                }
                foreach (var (idx, rel) in imgLoads)
                {
                    int captured = idx;
                    LoadFile(rel, (_, b, s) => OnImageLoaded(captured, b, s));
                }
            }

            // ── Stage 2: external resources arrive ───────────────────────────────────────────
            private void OnBufferLoaded(int idx, byte[]? buffer, SFileLoadStatus status)
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                {
                    var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    _extPins.Add(pin);
                    cgltf_buffer* buf = &_data->buffers[idx];
                    buf->data             = (void*)pin.AddrOfPinnedObject();
                    buf->data_free_method = cgltf_data_free_method.cgltf_data_free_method_none;
                }
                else
                {
                    _failed = true;   // a missing geometry buffer makes the model unusable
                    Logger.Error($"[glTF] missing buffer in '{_path}'");
                }
                if (--_pending == 0) FinishOrFail();
            }

            private void OnImageLoaded(int idx, byte[]? buffer, SFileLoadStatus status)
            {
                if (status == SFileLoadStatus.Success && buffer != null)
                    _images[idx] = buffer;
                else
                    Logger.Warning($"[glTF] missing texture image {idx} in '{_path}' (using placeholder)");
                if (--_pending == 0) FinishOrFail();
            }

            private void FinishOrFail()
            {
                if (_failed) { Fail("missing external buffer"); return; }
                Finish();
            }

            // ── Stage 3: all bytes present → build the scene ─────────────────────────────────
            private void Finish()
            {
                bool ok = false;
                Entity? result = null;
                try { result = BuildScene(); ok = true; Succeeded = true; }
                catch (Exception ex) { Logger.Error($"[glTF] {(_buildEntities ? "import" : "preload")} of '{_path}' failed: {ex}"); }
                Cleanup();
                if (ok) Logger.Info($"[glTF] {(_buildEntities ? "imported" : "preloaded resources for")} '{_path}'");
                if (!_done) { _done = true; _onComplete?.Invoke(result); }
            }

            private void Fail(string why)
            {
                Logger.Warning($"[glTF] {why} for '{_path}'");
                Cleanup();
                if (!_done) { _done = true; _onComplete?.Invoke(null); }
            }

            private void Cleanup()
            {
                if (_data != null) { cgltf_free(_data); _data = null; }
                foreach (var p in _extPins) if (p.IsAllocated) p.Free();
                _extPins.Clear();
                if (_mainPin.IsAllocated) _mainPin.Free();
            }

            // ── Scene construction (synchronous, once all bytes are present) ─────────────────
            // Always registers the resources; only builds entities for a full import (drag-drop).
            // Preload (scene reload) skips node building — the entities already exist.
            private Entity? BuildScene()
            {
                // A glTF mesh takes the per-character path (80 B verts + animator + SkinnedCharacter-
                // Registry) when some node references it WITH A SKIN *or* when it has MORPH TARGETS
                // (blend shapes). Morph-only meshes (no skin) ride the same skinned pipeline with
                // identity bones, so one draw path covers skinning, morphing, and both — and their
                // animated morph weights come from the shared CGltfAnimator. Their static 48 B
                // registration is skipped so we don't create unused mesh GPU buffers.
                // A mesh takes the skinned/morph path when some node references it WITH A SKIN or it has
                // MORPH TARGETS. Static meshes in the same model are still built (BuildNodes runs too).
                var charMeshPtrs = new HashSet<IntPtr>();
                for (int ni = 0; ni < (int)_data->nodes_count; ni++)
                {
                    cgltf_node* n = &_data->nodes[ni];
                    if (n->mesh == null) continue;
                    if (n->skin != null || MeshHasMorphTargets(n->mesh)) charMeshPtrs.Add((IntPtr)n->mesh);
                }

                RegisterResources(charMeshPtrs, out var primKeysByMesh, out var primMatsByMesh);

                // Character meshes register their GPU meshes + shared animator into
                // SkinnedCharacterRegistry ALWAYS — even in preload (_buildEntities == false) — so a
                // cold scene reload repopulates the registry that the already-deserialized
                // SkinnedMeshRenderer entities resolve against. Entity creation stays gated.
                // Rigid/node animation clips for animated NON-joint nodes (skeleton joints are driven by
                // CGltfAnimator, so exclude them). Runs for skinned models too — a mixed scene
                // (e.g. LittleTokio) animates its static props via node TRS.
                if (_data->animations_count > 0)
                    BuildNodeAnimationClips(CollectJointNodeNames());

                if (!_buildEntities)
                {
                    // Preload (scene reload): registries only — BuildSkinnedNodes repopulates
                    // SkinnedCharacterRegistry; static meshes + node clips are already registered.
                    if (charMeshPtrs.Count > 0) BuildSkinnedNodes(false, default);
                    return null;
                }

                // Full import: ONE container; skinned/morph flat + static-mesh hierarchy + node anim.
                string rootName = Path.GetFileNameWithoutExtension(_path);
                Entity container = _world.CreateEntity(string.IsNullOrEmpty(rootName) ? "GltfModel" : rootName);

                if (charMeshPtrs.Count > 0)
                    BuildSkinnedNodes(true, container);

                // Static meshes + transform hierarchy + NodeAnimationPlayer under the SAME container.
                // ImportNode attaches MeshRenderer only to STATIC mesh nodes (skinned/morph nodes have
                // empty primKeys), so a mixed model renders both. Pure-static models use only this walk.
                BuildNodes(container, primKeysByMesh, primMatsByMesh);
                return container;
            }

            // Names of every skeleton-joint node across all skins — these are driven by CGltfAnimator,
            // so node animation must NOT also drive them (would double-apply / fight the skinning).
            private HashSet<string> CollectJointNodeNames()
            {
                var joints = new HashSet<string>(StringComparer.Ordinal);
                for (int si = 0; si < (int)_data->skins_count; si++)
                {
                    cgltf_skin* skin = &_data->skins[si];
                    for (int ji = 0; ji < (int)skin->joints_count; ji++)
                    {
                        string? jn = PtrToStr(skin->joints[ji]->name);
                        if (jn != null) joints.Add(jn);
                    }
                }
                return joints;
            }

            // Registers every primitive as a MeshResource ("<file>#m{i}p{j}") and every material as a
            // PbrMaterial ("<file>#mat{i}") + its textures. Same keys the serialized scene references,
            // so a reload only needs this (no entity creation).
            private void RegisterResources(
                HashSet<IntPtr> skinnedMeshPtrs,
                out Dictionary<IntPtr, string[]> primKeysByMesh,
                out Dictionary<IntPtr, string[]> primMatsByMesh)
            {
                cgltf_data* data = _data;

                // A file read again: its images are made anew (a cached one would be handed back as it was), and
                // every material of the file is replaced below, so none keeps a view of an image that is gone.
                if (Replace) _texCache.DropPrefix($"{_path}#img");

                var matKeyByPtr = new Dictionary<IntPtr, string>();
                for (int i = 0; i < (int)data->materials_count; i++)
                {
                    cgltf_material* mat = &data->materials[i];
                    string key = $"{_path}#mat{i}";
                    if (Replace) _matReg.RegisterOrReplacePbr(key, BuildMaterial(mat, i));
                    else _matReg.RegisterPbr(key, BuildMaterial(mat, i));
                    matKeyByPtr[(IntPtr)mat] = key;
                }
                if (Replace)   // a material the file had and has no more: plain, under its key and id
                    for (int i = (int)data->materials_count; _matReg.GetByKey($"{_path}#mat{i}") != null; i++)
                        _matReg.RegisterOrReplacePbr($"{_path}#mat{i}", new PbrMaterial { Name = $"mat{i}" });
                var meshKeys = Replace ? new HashSet<string>(StringComparer.Ordinal) : null;

                primKeysByMesh = new Dictionary<IntPtr, string[]>();
                primMatsByMesh = new Dictionary<IntPtr, string[]>();
                for (int mi = 0; mi < (int)data->meshes_count; mi++)
                {
                    cgltf_mesh* mesh = &data->meshes[mi];
                    if (skinnedMeshPtrs.Contains((IntPtr)mesh))
                    {
                        // Character mesh (skinned and/or morphed) — registered via the per-character
                        // path in BuildSkinnedNodes; skipped here so no unused 48 B buffers are made.
                        primKeysByMesh[(IntPtr)mesh] = Array.Empty<string>();
                        primMatsByMesh[(IntPtr)mesh] = Array.Empty<string>();
                        continue;
                    }
                    int primCount = (int)mesh->primitives_count;
                    var keys = new List<string>(primCount);
                    var mats = new List<string>(primCount);

                    for (int pi = 0; pi < primCount; pi++)
                    {
                        cgltf_primitive* prim = &mesh->primitives[pi];
                        if (prim->type != cgltf_primitive_type.cgltf_primitive_type_triangles) continue;
                        if (!BuildPrimitive(prim, out ObjVertex[] verts, out uint[] indices, out Aabb bounds)) continue;

                        string key = $"{_path}#m{mi}p{pi}";
                        var sub = new ObjSubMesh { MaterialName = "", Vertices = verts, Indices = indices };
                        if ((Replace ? _meshReg.RegisterOrReplaceMesh(key, new[] { sub }, in bounds)
                                     : _meshReg.RegisterMesh(key, new[] { sub }, in bounds)) == 0) continue;
                        meshKeys?.Add(key);

                        keys.Add(key);
                        mats.Add(prim->material != null && matKeyByPtr.TryGetValue((IntPtr)prim->material, out var mk)
                            ? mk : "");
                    }

                    primKeysByMesh[(IntPtr)mesh] = keys.ToArray();
                    primMatsByMesh[(IntPtr)mesh] = mats.ToArray();
                }

                if (meshKeys != null)   // mesh keys of this file ("<file>#m{i}p{j}") that the new file does not have
                {
                    string prefix = $"{_path}#m";
                    foreach (string key in _meshReg.KeysWithPrefix(prefix))
                    {
                        if (meshKeys.Contains(key) || key.Length == prefix.Length || !char.IsAsciiDigit(key[prefix.Length])) continue;
                        _meshReg.Remove(key);
                        Lost.Add(key);
                    }
                    Lost.Sort(StringComparer.Ordinal);
                }

                // Rigid/node animation (e.g. ChronographWatch hands). Done here — in BOTH import and
                // preload — so the NodeAnimationRegistry is repopulated on a cold scene reload, exactly
                // like SkinnedCharacterRegistry. The call is in BuildScene (after this) so it can pass
                // the skeleton joint names to exclude — runs for skinned models too (their rigid-
                // animated static props, e.g. LittleTokio, still need node animation).
            }

            // Builds the ECS node hierarchy (Transform + parent for every node; MeshRenderer for STATIC
            // mesh nodes; NodeAnimationPlayer for animated non-joint nodes) under the given container.
            private Entity BuildNodes(
                Entity container,
                Dictionary<IntPtr, string[]> primKeysByMesh, Dictionary<IntPtr, string[]> primMatsByMesh)
            {
                cgltf_data* data = _data;

                if (data->scene != null)
                {
                    cgltf_scene* scene = data->scene;
                    for (int i = 0; i < (int)scene->nodes_count; i++)
                        ImportNode(scene->nodes[i], container, primKeysByMesh, primMatsByMesh);
                }
                else
                {
                    for (int i = 0; i < (int)data->nodes_count; i++)
                    {
                        cgltf_node* n = &data->nodes[i];
                        if (n->parent == null)
                            ImportNode(n, container, primKeysByMesh, primMatsByMesh);
                    }
                }

                return container;
            }

            // Skinned glTF: extract the skeleton + clips once, build one shared CGltfAnimator, and
            // create one entity per skinned primitive carrying a SkinnedMeshRenderer. The mesh node's
            // own transform is ignored (per the glTF skinning spec — the joints place the mesh); the
            // entity sits at identity under the container and the bone matrices (joint globals from
            // the scene root) do the placement.
            private Entity? BuildSkinnedNodes(bool buildEntities, Entity container)
            {
                cgltf_data* data = _data;
                int nSkins = (int)data->skins_count;
                bool multiSkin = nSkins > 1;

                // Character key per skin index (-1 = morph-only / no skin). Single-skin and pure-morph
                // models keep the LEGACY key "<path>" (no saved-scene breakage); only multi-skin models
                // (FishAndShark: fish + shark) split into "<path>#skin{i}" so each skin gets its OWN
                // skeleton + animator + meshes. The draw path resolves SkinnedMeshRenderer.CharacterKey,
                // so multiple characters per model are already supported.
                string KeyFor(int si) => si < 0 ? (nSkins == 0 ? _path : $"{_path}#morph")
                                                : (multiSkin ? $"{_path}#skin{si}" : _path);

                // One extractor + registry Entry per character, created lazily (keyed by skin index).
                var entries       = new Dictionary<int, SkinnedCharacterRegistry.Entry>();
                var nodeIndexMaps = new Dictionary<int, Dictionary<string, int>>();
                var runningPrims  = new Dictionary<int, int>();

                SkinnedCharacterRegistry.Entry EnsureCharacter(int si)
                {
                    if (entries.TryGetValue(si, out var existing)) return existing;
                    var ex = CGltfSkinExtractor.Extract(data, si);   // si < 0 → no-skin (morph/node) clips
                    var ent = SkinnedCharacterRegistry.GetOrCreateFresh(KeyFor(si));
                    ent.BoneCount = ex.BoneCount;
                    ent.Animator  = ex.HasAnimations
                        ? new CGltfAnimator(ex.Animations[0], ex.Nodes, ex.BoneCount, ex.BoneInfoMap)
                        : null;
                    var nim = new Dictionary<string, int>();
                    foreach (var rn in ex.Nodes)
                        if (!string.IsNullOrEmpty(rn.NodeName)) nim.TryAdd(rn.NodeName!, rn.NodeIndex);
                    nodeIndexMaps[si] = nim;
                    runningPrims[si]  = 0;
                    entries[si]       = ent;
                    return ent;
                }

                int totalPrims = 0;
                for (int ni = 0; ni < (int)data->nodes_count; ni++)
                {
                    cgltf_node* node = &data->nodes[ni];
                    if (node->mesh == null) continue;
                    bool nodeSkinned = node->skin != null;
                    bool nodeMorphed = MeshHasMorphTargets(node->mesh);
                    if (!nodeSkinned && !nodeMorphed) continue;   // pure-static mesh on a character model — skip
                    string nodeName = PtrToStr(node->name) ?? "SkinnedMesh";
                    int skinIdx = nodeSkinned ? (int)(node->skin - data->skins) : -1;
                    var entry = EnsureCharacter(skinIdx);
                    var nodeNameToIndex = nodeIndexMaps[skinIdx];

                    // Skinned meshes are placed by their joints (node transform ignored, per the glTF
                    // skinning spec). A morph-only mesh has no joints, so it is placed by the node's own
                    // world transform instead. Computed once per node (not per primitive).
                    Transform nodeTransform = new Transform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One, Parent = container };
                    if (!nodeSkinned)
                    {
                        float* wm = stackalloc float[16];
                        cgltf_node_transform_world(in *node, wm);
                        var world = new Matrix4x4(
                            wm[0],  wm[1],  wm[2],  wm[3],
                            wm[4],  wm[5],  wm[6],  wm[7],
                            wm[8],  wm[9],  wm[10], wm[11],
                            wm[12], wm[13], wm[14], wm[15]);
                        if (Matrix4x4.Decompose(world, out var ws, out var wr, out var wp))
                        { nodeTransform.Position = wp; nodeTransform.Rotation = wr; nodeTransform.Scale = ws; }
                    }

                    int primCount = (int)node->mesh->primitives_count;
                    for (int pi = 0; pi < primCount; pi++)
                    {
                        cgltf_primitive* prim = &node->mesh->primitives[pi];
                        if (prim->type != cgltf_primitive_type.cgltf_primitive_type_triangles) continue;
                        if (!BuildSkinnedPrimitive(prim, out SkinnedVertex[] sverts, out uint[] sidx, out Aabb sbounds)) continue;

                        int primIndex = runningPrims[skinIdx]++;
                        totalPrims++;
                        var skinnedMesh = SkinnedMesh.Create(sverts, sidx, in sbounds, $"{nodeName}_p{pi}");

                        // Morph targets: build the immutable displacement texture once; record the node
                        // index + static weights for per-frame weight resolution at draw time.
                        if (prim->targets_count > 0 &&
                            MorphTargetTexture.Build(prim, sverts.Length, out var morphImg, out var morphView, out int morphCount))
                        {
                            skinnedMesh.MorphImage        = morphImg;
                            skinnedMesh.MorphView         = morphView;
                            skinnedMesh.MorphTargetCount  = morphCount;
                            skinnedMesh.MorphNodeIndex    = nodeNameToIndex.TryGetValue(nodeName, out int mni) ? mni : -1;
                            skinnedMesh.StaticMorphWeights = ReadStaticMorphWeights(node);
                        }

                        entry.Meshes[primIndex] = skinnedMesh;

                        if (!buildEntities) continue;   // preload: registry only, no entities

                        string matKey = prim->material != null
                            ? $"{_path}#mat{(int)(prim->material - data->materials)}" : "";

                        Entity e = _world.CreateEntity($"{nodeName}_{(nodeSkinned ? "skin" : "morph")}{pi}");
                        _world.AddComponent(e, nodeTransform);
                        _world.AddComponent(e, new SkinnedMeshRenderer
                        {
                            CharacterKey    = KeyFor(skinIdx),
                            PrimIndex       = primIndex,
                            MaterialKey     = matKey,
                            Visible         = true,
                            ReceivesShadows = true,
                            CastsShadows    = true,
                        });
                    }
                }

                Logger.Info($"[glTF] character {(buildEntities ? "import" : "preload")} '{_path}': {entries.Count} character(s), {totalPrims} prim(s)");
                return buildEntities ? container : (Entity?)null;
            }

            // ── Nodes ────────────────────────────────────────────────────────────────────────
            private void ImportNode(
                cgltf_node* node, Entity parent,
                Dictionary<IntPtr, string[]> primKeysByMesh, Dictionary<IntPtr, string[]> primMatsByMesh, bool underTurned = false)
            {
                Vector3    pos   = Vector3.Zero;
                Quaternion rot   = Quaternion.Identity;
                Vector3    scale = Vector3.One;

                if (node->has_matrix != 0)
                {
                    var m = new Matrix4x4(
                        node->matrix[0],  node->matrix[1],  node->matrix[2],  node->matrix[3],
                        node->matrix[4],  node->matrix[5],  node->matrix[6],  node->matrix[7],
                        node->matrix[8],  node->matrix[9],  node->matrix[10], node->matrix[11],
                        node->matrix[12], node->matrix[13], node->matrix[14], node->matrix[15]);
                    Matrix4x4.Decompose(m, out scale, out rot, out pos);
                }
                else
                {
                    if (node->has_translation != 0) pos   = new Vector3(node->translation[0], node->translation[1], node->translation[2]);
                    if (node->has_rotation    != 0) rot   = new Quaternion(node->rotation[0], node->rotation[1], node->rotation[2], node->rotation[3]);
                    if (node->has_scale       != 0) scale = new Vector3(node->scale[0], node->scale[1], node->scale[2]);
                }

                // A light or a camera looks down its node's -Z, the engine's down their entity's +Z: the entity is turned
                // half a turn about its Y, and what stands under it is placed so that it stays where the file has it.
                bool turned = node->light != null || node->camera != null;
                if (underTurned) GltfSceneParts.UnderTurned(ref pos, ref rot);
                if (turned) rot = GltfSceneParts.Turned(rot);

                string name = PtrToStr(node->name) ?? "Node";
                Entity entity = _world.CreateEntity(name);
                // Transform already exists (CreateEntity) → AddComponent overwrites with no structural
                // change, so adding MeshRenderer afterwards is safe.
                _world.AddComponent(entity, new Transform { Position = pos, Rotation = rot, Scale = scale, Parent = parent });

                // Rigid/node animation: if this node has a registered TRS clip, drive its Transform.
                // (Resolved by node name from NodeAnimationRegistry, populated in RegisterResources.)
                var nodeClip = NodeAnimationRegistry.Resolve(_path, name);
                if (nodeClip != null)
                    _world.AddComponent(entity, new NodeAnimationPlayer { Clip = nodeClip, Time = 0f, Playing = true, Loop = true });

                if (node->mesh != null && primKeysByMesh.TryGetValue((IntPtr)node->mesh, out var primKeys))
                {
                    var matKeys = primMatsByMesh[(IntPtr)node->mesh];
                    if (primKeys.Length == 1)
                    {
                        AddMeshRenderer(entity, primKeys[0], matKeys[0]);
                    }
                    else
                    {
                        // Multi-primitive mesh: one child entity per primitive so each carries its
                        // own material (the draw loop keys a group by a single material).
                        for (int p = 0; p < primKeys.Length; p++)
                        {
                            Entity primE = _world.CreateEntity($"{name}_prim{p}");
                            _world.AddComponent(primE, new Transform { Position = Vector3.Zero, Rotation = Quaternion.Identity, Scale = Vector3.One, Parent = entity });
                            AddMeshRenderer(primE, primKeys[p], matKeys[p]);
                        }
                    }
                }

                if (node->light != null)
                {
                    cgltf_light* light = node->light;
                    int kind = light->type == cgltf_light_type.cgltf_light_type_directional ? 0
                             : light->type == cgltf_light_type.cgltf_light_type_point ? 1 : 2;
                    _world.AddComponent(entity, GltfSceneParts.Light(kind, new Vector3(light->color[0], light->color[1], light->color[2]),
                        light->intensity, light->range, light->spot_inner_cone_angle, light->spot_outer_cone_angle));
                }
                if (node->camera != null)
                {
                    cgltf_camera* camera = node->camera;
                    if (camera->type == cgltf_camera_type.cgltf_camera_type_orthographic)
                        _world.AddComponent(entity, GltfSceneParts.Camera(true, camera->data.orthographic.ymag, camera->data.orthographic.znear, camera->data.orthographic.zfar));
                    else if (camera->type == cgltf_camera_type.cgltf_camera_type_perspective)
                        _world.AddComponent(entity, GltfSceneParts.Camera(false, camera->data.perspective.yfov, camera->data.perspective.znear,
                            camera->data.perspective.has_zfar != 0 ? camera->data.perspective.zfar : 0f));
                }

                for (int c = 0; c < (int)node->children_count; c++)
                    ImportNode(node->children[c], entity, primKeysByMesh, primMatsByMesh, turned);
            }

            private void AddMeshRenderer(Entity e, string meshKey, string matKey)
            {
                _world.AddComponent(e, new MeshRenderer
                {
                    MeshPath        = meshKey,
                    MaterialPath    = matKey,
                    Visible         = true,
                    ReceivesShadows = true,
                    CastsShadows    = true,
                });
            }

            // ── Materials ──────────────────────────────────────────────────────────────────
            private PbrMaterial BuildMaterial(cgltf_material* mat, int matIndex)
            {
                var m = new PbrMaterial { Name = PtrToStr(mat->name) ?? $"mat{matIndex}" };

                if (mat->has_pbr_metallic_roughness != 0)
                {
                    ref var pbr = ref mat->pbr_metallic_roughness;
                    m.BaseColorFactor = new Vector4(pbr.base_color_factor[0], pbr.base_color_factor[1],
                                                    pbr.base_color_factor[2], pbr.base_color_factor[3]);
                    m.MetallicFactor  = pbr.metallic_factor;
                    m.RoughnessFactor = pbr.roughness_factor;
                    m.BaseColorMap         = LoadTexture(pbr.base_color_texture,         srgb: true,  out m.BaseColorMapPath);
                    m.MetallicRoughnessMap = LoadTexture(pbr.metallic_roughness_texture, srgb: false, out m.MetallicRoughnessMapPath);
                }
                else
                {
                    m.BaseColorFactor = Vector4.One;
                    m.MetallicFactor  = 0f;
                    m.RoughnessFactor = 0.5f;
                }

                m.NormalMap         = LoadTexture(mat->normal_texture,    srgb: false, out m.NormalMapPath);
                m.NormalMapScale    = mat->normal_texture.scale != 0f ? mat->normal_texture.scale : 1f;
                m.OcclusionMap      = LoadTexture(mat->occlusion_texture, srgb: false, out m.OcclusionMapPath);
                m.OcclusionStrength = mat->occlusion_texture.scale != 0f ? mat->occlusion_texture.scale : 1f;
                m.EmissiveFactor    = new Vector3(mat->emissive_factor[0], mat->emissive_factor[1], mat->emissive_factor[2]);
                m.EmissiveMap       = LoadTexture(mat->emissive_texture,  srgb: true,  out m.EmissiveMapPath);
                m.EmissiveStrength  = mat->has_emissive_strength != 0 ? mat->emissive_strength.emissive_strength : 1f;

                m.AlphaMode = mat->alpha_mode switch
                {
                    cgltf_alpha_mode.cgltf_alpha_mode_mask  => 1,
                    cgltf_alpha_mode.cgltf_alpha_mode_blend => 2,
                    _                                       => 0,
                };
                m.AlphaCutoff   = mat->alpha_cutoff > 0f ? mat->alpha_cutoff : 0.5f;
                m.IsTransparent = m.AlphaMode == 2;
                m.DoubleSided   = mat->double_sided != 0;
                m.Sampler       = _texCache.DefaultSampler;

                // ── KHR_materials_ior / _transmission / _volume (screen-space refraction) ──
                // Ported from examples/CGltfViewer/Source/CGltfModel.cs:562-613. Transmission
                // routes the material through RenderingServer's post-opaque transmission pass.
                m.Ior = mat->has_ior != 0 ? mat->ior.ior : 1.5f;

                if (mat->has_transmission != 0)
                {
                    m.TransmissionFactor   = mat->transmission.transmission_factor;
                    m.TransmissionMap      = LoadTexture(mat->transmission.transmission_texture, srgb: false, out m.TransmissionMapPath);
                    m.TransmissionTexcoord = mat->transmission.transmission_texture.texcoord;
                }

                if (mat->has_volume != 0)
                {
                    m.ThicknessFactor     = mat->volume.thickness_factor;
                    m.AttenuationDistance = mat->volume.attenuation_distance > 0f
                        ? mat->volume.attenuation_distance : float.MaxValue;
                    m.AttenuationColor    = new Vector3(mat->volume.attenuation_color[0],
                                                        mat->volume.attenuation_color[1],
                                                        mat->volume.attenuation_color[2]);
                }

                // ── KHR_materials_clearcoat (thin glossy coat — e.g. car paint) ──
                if (mat->has_clearcoat != 0)
                {
                    m.ClearcoatFactor    = mat->clearcoat.clearcoat_factor;
                    m.ClearcoatRoughness = mat->clearcoat.clearcoat_roughness_factor;
                }

                // ── Per-texture coordinate set + KHR_texture_transform (offset/rotation/scale) ──
                // The shader already selects v_TexCoord0/1 per texture (*_texcoord) and applies the
                // transform (applyTextureTransform); these were dormant because the importer never
                // filled them and every draw path hardcoded identity. (CarConcept tiles normal maps
                // via large texture-transform scales — without this they sample wrong → "nasty spots".)
                if (mat->has_pbr_metallic_roughness != 0)
                {
                    ref var pbr = ref mat->pbr_metallic_roughness;
                    m.BaseColorTexcoord         = pbr.base_color_texture.texcoord;
                    m.MetallicRoughnessTexcoord = pbr.metallic_roughness_texture.texcoord;
                    ExtractTexTransform(pbr.base_color_texture,         ref m.BaseColorTexOffset,         ref m.BaseColorTexRotation,         ref m.BaseColorTexScale);
                    ExtractTexTransform(pbr.metallic_roughness_texture, ref m.MetallicRoughnessTexOffset, ref m.MetallicRoughnessTexRotation, ref m.MetallicRoughnessTexScale);
                }
                m.NormalTexcoord    = mat->normal_texture.texcoord;
                m.OcclusionTexcoord = mat->occlusion_texture.texcoord;
                m.EmissiveTexcoord  = mat->emissive_texture.texcoord;
                ExtractTexTransform(mat->normal_texture,    ref m.NormalTexOffset,    ref m.NormalTexRotation,    ref m.NormalTexScale);
                ExtractTexTransform(mat->occlusion_texture, ref m.OcclusionTexOffset, ref m.OcclusionTexRotation, ref m.OcclusionTexScale);
                ExtractTexTransform(mat->emissive_texture,  ref m.EmissiveTexOffset,  ref m.EmissiveTexRotation,  ref m.EmissiveTexScale);

                return m;
            }

            // Ported from examples/CGltfViewer/Source/CGltfModel.cs (ExtractTexTransform). Reads a
            // KHR_texture_transform off a texture view; leaves the identity defaults untouched if absent.
            private static void ExtractTexTransform(cgltf_texture_view view,
                ref Vector2 offset, ref float rotation, ref Vector2 scale)
            {
                if (view.has_transform == 0) return;
                offset   = new Vector2(view.transform.offset[0], view.transform.offset[1]);
                rotation = view.transform.rotation;
                scale    = new Vector2(view.transform.scale[0],  view.transform.scale[1]);
            }

            // ── Node / rigid animation (e.g. ChronographWatch hands) ─────────────────────────────
            // Collects every node-targeted TRS channel across all animations into one clip per node,
            // then registers them in NodeAnimationRegistry keyed by node NAME (stable across loads;
            // the cgltf_node* is freed after import, and the scene serializer round-trips the entity
            // name). LINEAR/STEP keyframes are read directly; CUBICSPLINE tangents collapse to the
            // keyframe value. Skeleton-joint channels are SKIPPED (jointNames) — those drive the
            // CGltfAnimator; node animation only covers the rigid (non-joint) transform hierarchy.
            private void BuildNodeAnimationClips(HashSet<string> jointNames)
            {
                var clips = new Dictionary<IntPtr, NodeAnimationClip>();
                cgltf_data* data = _data;
                for (int ai = 0; ai < (int)data->animations_count; ai++)
                {
                    cgltf_animation* anim = &data->animations[ai];
                    for (int ci = 0; ci < (int)anim->channels_count; ci++)
                    {
                        cgltf_animation_channel* ch = &anim->channels[ci];
                        if (ch->target_node == null || ch->sampler == null) continue;
                        // Skip skeleton joints — the skinned animator owns those.
                        string? tn0 = PtrToStr(ch->target_node->name);
                        if (tn0 != null && jointNames.Contains(tn0)) continue;
                        var path = ch->target_path;
                        bool isT = path == cgltf_animation_path_type.cgltf_animation_path_type_translation;
                        bool isR = path == cgltf_animation_path_type.cgltf_animation_path_type_rotation;
                        bool isS = path == cgltf_animation_path_type.cgltf_animation_path_type_scale;
                        if (!isT && !isR && !isS) continue;   // weights/pointer channels not handled here

                        cgltf_animation_sampler* samp = ch->sampler;
                        if (samp->input == null || samp->output == null) continue;
                        int nKeys = (int)samp->input->count;
                        if (nKeys == 0) continue;
                        bool step  = samp->interpolation == cgltf_interpolation_type.cgltf_interpolation_type_step;
                        bool cubic = samp->interpolation == cgltf_interpolation_type.cgltf_interpolation_type_cubic_spline;

                        float[] times = UnpackFloats(samp->input, nKeys);

                        IntPtr nodeKey = (IntPtr)ch->target_node;
                        if (!clips.TryGetValue(nodeKey, out var clip))
                        {
                            clip = new NodeAnimationClip();
                            SetBaseTrs(ch->target_node, clip);
                            clips[nodeKey] = clip;
                        }

                        int comp = isR ? 4 : 3;
                        int groups = cubic ? nKeys * 3 : nKeys;
                        float[] raw = UnpackFloats(samp->output, groups * comp);

                        if (isR)
                        {
                            var vals = new Quaternion[nKeys];
                            for (int i = 0; i < nKeys; i++)
                            {
                                int g = cubic ? (i * 3 + 1) : i;   // middle of in/value/out triple
                                vals[i] = new Quaternion(raw[g * 4], raw[g * 4 + 1], raw[g * 4 + 2], raw[g * 4 + 3]);
                            }
                            clip.RotTimes = times; clip.RotVals = vals; clip.RotStep = step;
                        }
                        else
                        {
                            var vals = new Vector3[nKeys];
                            for (int i = 0; i < nKeys; i++)
                            {
                                int g = cubic ? (i * 3 + 1) : i;
                                vals[i] = new Vector3(raw[g * 3], raw[g * 3 + 1], raw[g * 3 + 2]);
                            }
                            if (isT) { clip.TransTimes = times; clip.TransVals = vals; clip.TransStep = step; }
                            else     { clip.ScaleTimes = times; clip.ScaleVals = vals; clip.ScaleStep = step; }
                        }

                        if (nKeys > 0 && times[nKeys - 1] > clip.Duration) clip.Duration = times[nKeys - 1];
                    }
                }

                // Map node-ptr → node NAME (matching ImportNode's naming) and register by name so a
                // deserialized scene entity can re-resolve its clip.
                var byName = new Dictionary<string, NodeAnimationClip>(StringComparer.Ordinal);
                for (int ni = 0; ni < (int)data->nodes_count; ni++)
                {
                    cgltf_node* n = &data->nodes[ni];
                    if (clips.TryGetValue((IntPtr)n, out var clip))
                        byName[PtrToStr(n->name) ?? "Node"] = clip;
                }
                NodeAnimationRegistry.Register(_path, byName);
            }

            // Node bind TRS — used as the fallback for channels a clip does not animate.
            private static void SetBaseTrs(cgltf_node* node, NodeAnimationClip clip)
            {
                if (node->has_matrix != 0)
                {
                    var m = new Matrix4x4(
                        node->matrix[0],  node->matrix[1],  node->matrix[2],  node->matrix[3],
                        node->matrix[4],  node->matrix[5],  node->matrix[6],  node->matrix[7],
                        node->matrix[8],  node->matrix[9],  node->matrix[10], node->matrix[11],
                        node->matrix[12], node->matrix[13], node->matrix[14], node->matrix[15]);
                    Matrix4x4.Decompose(m, out var s, out var r, out var t);
                    clip.BaseTranslation = t; clip.BaseRotation = r; clip.BaseScale = s;
                }
                else
                {
                    if (node->has_translation != 0) clip.BaseTranslation = new Vector3(node->translation[0], node->translation[1], node->translation[2]);
                    if (node->has_rotation    != 0) clip.BaseRotation    = new Quaternion(node->rotation[0], node->rotation[1], node->rotation[2], node->rotation[3]);
                    if (node->has_scale       != 0) clip.BaseScale       = new Vector3(node->scale[0], node->scale[1], node->scale[2]);
                }
            }

            // Decodes the texture referenced by <paramref name="view"/> — from its embedded buffer
            // view (GLB) or the pre-fetched external image bytes — and uploads it. Zero view = absent.
            private Sokol.SG.sg_view LoadTexture(cgltf_texture_view view, bool srgb, out string outPath)
            {
                outPath = "";
                if (view.texture == null) return default;
                cgltf_image* img = view.texture->image;
                if (img == null) return default;

                int imgIndex = (int)(img - _data->images);
                string key = $"{_path}#img{imgIndex}";

                byte[]? imageData = null;
                if (img->buffer_view != null)
                {
                    byte* ptr = cgltf_buffer_view_data(*img->buffer_view);
                    if (ptr == null) return default;
                    int sz = (int)img->buffer_view->size;
                    imageData = new byte[sz];
                    Marshal.Copy((IntPtr)ptr, imageData, 0, sz);
                }
                else if (_images.TryGetValue(imgIndex, out var ext))
                {
                    imageData = ext;
                }
                if (imageData == null) return default;

                outPath = key;
                return _texCache.LoadFromBytes(key, imageData, srgb);
            }

            // ── Primitives ─────────────────────────────────────────────────────────────────
            private bool BuildPrimitive(cgltf_primitive* prim, out ObjVertex[] vertices, out uint[] indices, out Aabb bounds)
            {
                vertices = Array.Empty<ObjVertex>();
                indices  = Array.Empty<uint>();
                bounds   = default;

                cgltf_accessor* posAcc = null, normAcc = null, tanAcc = null, tc0Acc = null, tc1Acc = null;
                for (int ai = 0; ai < (int)prim->attributes_count; ai++)
                {
                    cgltf_attribute* a = &prim->attributes[ai];
                    switch (a->type)
                    {
                        case cgltf_attribute_type.cgltf_attribute_type_position: posAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_normal:   normAcc = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_tangent:  tanAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_texcoord:
                            if      (a->index == 0) tc0Acc = a->data;
                            else if (a->index == 1) tc1Acc = a->data;
                            break;
                    }
                }

                if (posAcc == null) return false;
                int vc = (int)posAcc->count;
                if (vc == 0) return false;

                float[] posData  = UnpackFloats(posAcc,  vc * 3);
                float[] normData = normAcc != null ? UnpackFloats(normAcc, vc * 3) : Array.Empty<float>();
                float[] tanData  = tanAcc  != null ? UnpackFloats(tanAcc,  vc * 4) : Array.Empty<float>();
                float[] tc0Data  = tc0Acc  != null ? UnpackFloats(tc0Acc,  vc * 2) : Array.Empty<float>();
                float[] tc1Data  = tc1Acc  != null ? UnpackFloats(tc1Acc,  vc * 2) : Array.Empty<float>();

                uint[] idx;
                if (prim->indices != null)
                {
                    int ic = (int)prim->indices->count;
                    idx = new uint[ic];
                    for (int i = 0; i < ic; i++)
                        idx[i] = (uint)cgltf_accessor_read_index(*prim->indices, (nuint)i);
                }
                else
                {
                    idx = new uint[vc];
                    for (int i = 0; i < vc; i++) idx[i] = (uint)i;
                }

                // glTF tangents are optional; synthesise them (Lengyel) when a UV set + normals exist.
                if (tanData.Length == 0 && tc0Data.Length > 0 && normData.Length > 0)
                    tanData = ComputeTangents(posData, normData, tc0Data, idx, vc);

                vertices = new ObjVertex[vc];
                var bmin = new Vector3(float.MaxValue);
                var bmax = new Vector3(float.MinValue);
                for (int i = 0; i < vc; i++)
                {
                    var p = new Vector3(posData[i * 3], posData[i * 3 + 1], posData[i * 3 + 2]);
                    vertices[i] = new ObjVertex
                    {
                        Position = p,
                        Normal   = normData.Length > 0 ? new Vector3(normData[i * 3], normData[i * 3 + 1], normData[i * 3 + 2]) : Vector3.UnitY,
                        Uv       = tc0Data.Length  > 0 ? new Vector2(tc0Data[i * 2], tc0Data[i * 2 + 1]) : Vector2.Zero,
                        Tangent  = tanData.Length  > 0 ? new Vector4(tanData[i * 4], tanData[i * 4 + 1], tanData[i * 4 + 2], tanData[i * 4 + 3]) : new Vector4(1f, 0f, 0f, 1f),
                        Uv1      = tc1Data.Length  > 0 ? new Vector2(tc1Data[i * 2], tc1Data[i * 2 + 1]) : Vector2.Zero,
                    };
                    bmin = Vector3.Min(bmin, p);
                    bmax = Vector3.Max(bmax, p);
                }

                indices = idx;
                bounds  = new Aabb(bmin, bmax);
                return true;
            }

            // Skinned variant of BuildPrimitive: also reads JOINTS_0 + WEIGHTS_0 into an 80 B
            // SkinnedVertex (joint indices are unpacked as floats — the shader casts them back).
            private bool BuildSkinnedPrimitive(cgltf_primitive* prim, out SkinnedVertex[] vertices, out uint[] indices, out Aabb bounds)
            {
                vertices = Array.Empty<SkinnedVertex>();
                indices  = Array.Empty<uint>();
                bounds   = default;

                cgltf_accessor* posAcc = null, normAcc = null, tanAcc = null, tc0Acc = null, tc1Acc = null, jntAcc = null, wgtAcc = null;
                for (int ai = 0; ai < (int)prim->attributes_count; ai++)
                {
                    cgltf_attribute* a = &prim->attributes[ai];
                    switch (a->type)
                    {
                        case cgltf_attribute_type.cgltf_attribute_type_position: posAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_normal:   normAcc = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_tangent:  tanAcc  = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_texcoord:
                            if      (a->index == 0) tc0Acc = a->data;
                            else if (a->index == 1) tc1Acc = a->data;
                            break;
                        case cgltf_attribute_type.cgltf_attribute_type_joints:   if (a->index == 0) jntAcc = a->data; break;
                        case cgltf_attribute_type.cgltf_attribute_type_weights:  if (a->index == 0) wgtAcc = a->data; break;
                    }
                }

                if (posAcc == null) return false;
                int vc = (int)posAcc->count;
                if (vc == 0) return false;

                float[] posData  = UnpackFloats(posAcc,  vc * 3);
                float[] normData = normAcc != null ? UnpackFloats(normAcc, vc * 3) : Array.Empty<float>();
                float[] tanData  = tanAcc  != null ? UnpackFloats(tanAcc,  vc * 4) : Array.Empty<float>();
                float[] tc0Data  = tc0Acc  != null ? UnpackFloats(tc0Acc,  vc * 2) : Array.Empty<float>();
                float[] tc1Data  = tc1Acc  != null ? UnpackFloats(tc1Acc,  vc * 2) : Array.Empty<float>();
                float[] jntData  = jntAcc  != null ? UnpackFloats(jntAcc,  vc * 4) : Array.Empty<float>();
                float[] wgtData  = wgtAcc  != null ? UnpackFloats(wgtAcc,  vc * 4) : Array.Empty<float>();

                uint[] idx;
                if (prim->indices != null)
                {
                    int ic = (int)prim->indices->count;
                    idx = new uint[ic];
                    for (int i = 0; i < ic; i++)
                        idx[i] = (uint)cgltf_accessor_read_index(*prim->indices, (nuint)i);
                }
                else
                {
                    idx = new uint[vc];
                    for (int i = 0; i < vc; i++) idx[i] = (uint)i;
                }

                if (tanData.Length == 0 && tc0Data.Length > 0 && normData.Length > 0)
                    tanData = ComputeTangents(posData, normData, tc0Data, idx, vc);

                vertices = new SkinnedVertex[vc];
                var bmin = new Vector3(float.MaxValue);
                var bmax = new Vector3(float.MinValue);
                for (int i = 0; i < vc; i++)
                {
                    var p = new Vector3(posData[i * 3], posData[i * 3 + 1], posData[i * 3 + 2]);
                    vertices[i] = new SkinnedVertex
                    {
                        Position = p,
                        Normal   = normData.Length > 0 ? new Vector3(normData[i * 3], normData[i * 3 + 1], normData[i * 3 + 2]) : Vector3.UnitY,
                        Uv       = tc0Data.Length  > 0 ? new Vector2(tc0Data[i * 2], tc0Data[i * 2 + 1]) : Vector2.Zero,
                        Tangent  = tanData.Length  > 0 ? new Vector4(tanData[i * 4], tanData[i * 4 + 1], tanData[i * 4 + 2], tanData[i * 4 + 3]) : new Vector4(1f, 0f, 0f, 1f),
                        Joints   = jntData.Length  > 0 ? new Vector4(jntData[i * 4], jntData[i * 4 + 1], jntData[i * 4 + 2], jntData[i * 4 + 3]) : Vector4.Zero,
                        Weights  = wgtData.Length  > 0 ? new Vector4(wgtData[i * 4], wgtData[i * 4 + 1], wgtData[i * 4 + 2], wgtData[i * 4 + 3]) : new Vector4(1f, 0f, 0f, 0f),
                        Uv1      = tc1Data.Length  > 0 ? new Vector2(tc1Data[i * 2], tc1Data[i * 2 + 1]) : Vector2.Zero,
                    };
                    bmin = Vector3.Min(bmin, p);
                    bmax = Vector3.Max(bmax, p);
                }

                indices = idx;
                bounds  = new Aabb(bmin, bmax);
                return true;
            }

            private static float[] UnpackFloats(cgltf_accessor* acc, int totalFloats)
            {
                if (acc == null || totalFloats <= 0) return Array.Empty<float>();
                var buf = new float[totalFloats];
                fixed (float* p = buf)
                    cgltf_accessor_unpack_floats(*acc, p, (nuint)totalFloats);
                return buf;
            }

            // True when any primitive of the mesh declares morph targets (blend shapes).
            private static bool MeshHasMorphTargets(cgltf_mesh* mesh)
            {
                for (int pi = 0; pi < (int)mesh->primitives_count; pi++)
                    if (mesh->primitives[pi].targets_count > 0) return true;
                return false;
            }

            // Static (non-animated) morph weights: node override first (glTF spec), else the mesh
            // defaults. Used as the fallback when no animation drives the weights. Null when neither set.
            private static float[]? ReadStaticMorphWeights(cgltf_node* node)
            {
                if (node->weights != null && node->weights_count > 0)
                {
                    var w = new float[(int)node->weights_count];
                    for (int i = 0; i < w.Length; i++) w[i] = node->weights[i];
                    return w;
                }
                cgltf_mesh* mesh = node->mesh;
                if (mesh != null && mesh->weights != null && mesh->weights_count > 0)
                {
                    var w = new float[(int)mesh->weights_count];
                    for (int i = 0; i < w.Length; i++) w[i] = mesh->weights[i];
                    return w;
                }
                return null;
            }
        }

        // ── Static helpers ───────────────────────────────────────────────────────────────────

        // Lengyel's method (ported from CGltfModel.CalculateTangents) → packed float[vc*4],
        // xyz = orthonormalised tangent, w = bitangent handedness.
        private static float[] ComputeTangents(float[] pos, float[] nrm, float[] uv, uint[] indices, int vc)
        {
            var tan1 = new Vector3[vc];
            var tan2 = new Vector3[vc];

            for (int ii = 0; ii + 2 < indices.Length; ii += 3)
            {
                int i0 = (int)indices[ii], i1 = (int)indices[ii + 1], i2 = (int)indices[ii + 2];
                Vector3 p0 = V3(pos, i0), p1 = V3(pos, i1), p2 = V3(pos, i2);
                Vector2 u0 = V2(uv, i0),  u1 = V2(uv, i1),  u2 = V2(uv, i2);

                float x1 = p1.X - p0.X, x2 = p2.X - p0.X;
                float y1 = p1.Y - p0.Y, y2 = p2.Y - p0.Y;
                float z1 = p1.Z - p0.Z, z2 = p2.Z - p0.Z;
                float s1 = u1.X - u0.X, s2 = u2.X - u0.X;
                float t1 = u1.Y - u0.Y, t2 = u2.Y - u0.Y;
                float denom = s1 * t2 - s2 * t1;
                if (MathF.Abs(denom) < 1e-7f) continue;
                float rcp = 1f / denom;
                var sdir = new Vector3((t2 * x1 - t1 * x2) * rcp, (t2 * y1 - t1 * y2) * rcp, (t2 * z1 - t1 * z2) * rcp);
                var tdir = new Vector3((s1 * x2 - s2 * x1) * rcp, (s1 * y2 - s2 * y1) * rcp, (s1 * z2 - s2 * z1) * rcp);

                tan1[i0] += sdir; tan1[i1] += sdir; tan1[i2] += sdir;
                tan2[i0] += tdir; tan2[i1] += tdir; tan2[i2] += tdir;
            }

            var outp = new float[vc * 4];
            for (int i = 0; i < vc; i++)
            {
                Vector3 n = V3(nrm, i);
                Vector3 t = tan1[i];
                Vector3 ortho = t - n * Vector3.Dot(n, t);
                ortho = ortho.LengthSquared() > 1e-12f ? Vector3.Normalize(ortho) : new Vector3(1f, 0f, 0f);
                float hand = Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f ? -1f : 1f;
                outp[i * 4 + 0] = ortho.X;
                outp[i * 4 + 1] = ortho.Y;
                outp[i * 4 + 2] = ortho.Z;
                outp[i * 4 + 3] = hand;
            }
            return outp;
        }

        private static Vector3 V3(float[] a, int i) => new Vector3(a[i * 3], a[i * 3 + 1], a[i * 3 + 2]);
        private static Vector2 V2(float[] a, int i) => new Vector2(a[i * 2], a[i * 2 + 1]);

        private static unsafe string? PtrToStr(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);

        // Assets-relative directory of a file path (forward-slash; "" when at the Assets root).
        private static string DirOf(string rel)
        {
            rel = rel.Replace('\\', '/');
            int i = rel.LastIndexOf('/');
            return i < 0 ? "" : rel.Substring(0, i);
        }

        // Joins an Assets-relative directory with a (possibly URL-encoded) glTF-relative URI.
        private static string Combine(string dir, string uri)
        {
            uri = Uri.UnescapeDataString(uri).Replace('\\', '/');
            return string.IsNullOrEmpty(dir) ? uri : dir + "/" + uri;
        }
    }
}
