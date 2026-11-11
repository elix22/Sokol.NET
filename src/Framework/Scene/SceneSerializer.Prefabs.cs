using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Scene
{
    // Prefab instances in a scene file (and in a prefab file, which has the same format).
    //
    // A prefab file is a scene file with one root entity:
    //   {"prefab":1,"name":"Enemy","entities":[{"id":1,"c":{…}},{"id":2,"c":{…,"Transform":{…,"Par":1}}}],"nextId":3}
    //
    // A file that holds an instance stores every entity of it as an address plus the values that differ:
    //   {"id":12,"src":{"prefab":"Prefabs/Enemy.prefab"},"c":{"NameTag":{…},"Transform":{"P":…,"E":…,"Q":…}}}   the root
    //   {"id":13,"src":{"in":12,"e":[2]}}                                             the prefab's entity 2, unchanged
    //   {"id":14,"src":{"in":12,"e":[3]},"c":{"Light":{"Int":3}},"removed":["Rigidbody"]}
    //   {"id":15,"src":{"in":12,"e":[4,2]}}        entity 2 of the prefab nested in Enemy at its entity 4
    //
    // "c" is merged over the prefab's values field by field (null = the field is absent); a component the prefab
    // does not have is a whole block; "removed" names components the instance took off. The root's name, position,
    // rotation and parent are always the instance's own. Entity references inside the prefab's values are the
    // prefab's ids and are mapped to the instance's entities.
    //
    // Reading expands such entries into complete entities (ExpandEntities) and then creates them like any other;
    // writing compares each instance entity with the prefab and writes the differences (WritePrefabInstanceEntity).
    public static partial class SceneSerializer
    {
        // Component key → the fields of its block that hold an entity id.
        private static readonly Dictionary<string, string[]> _referenceFields = new(StringComparer.Ordinal)
        {
            ["Transform"]   = ["Par"],
            ["Constraint"]  = ["A", "B"],
            ["WheelFollow"] = ["V"],
        };

        /// <summary>
        /// Names the fields of a component extension's block that hold an entity id (top-level number fields).
        /// Inside a prefab such a field holds a prefab entity's id; it is mapped to the instance's entity when the
        /// prefab is placed. Register with the extension, at startup.
        /// </summary>
        public static void RegisterEntityReferenceFields(string componentKey, params string[] fields)
            => _referenceFields[componentKey] = fields;

        // ── Reading ─────────────────────────────────────────────────────────────────────

        // One entity of a file after its instances are expanded.
        private sealed class Expanded
        {
            public int Id;
            public JsonElement C;       // all its components; ValueKind Undefined = none
            public int RootId;          // the id of the instance root it belongs to (its own on the root); 0 = an ordinary entity
            public int[] Path = [];     // its address inside the instance's prefab
            public string? Asset;       // on an instance root: the prefab's key
            public string? Extra;       // unknown members of "src"
            public bool Missing;        // the prefab could not be loaded: Line is all there is
            public JsonElement Line;
        }

        private sealed class InstanceRoot
        {
            public string Asset = "";
            public Prefab? Prefab;
            public readonly Dictionary<string, int> Lines = new(StringComparer.Ordinal);   // path key → the id of the file's entry for it
            public readonly Dictionary<int, int> Map = new();                                // prefab entity id → id in this file
            public readonly List<PrefabEntity> Unlisted = new();                             // prefab entities the file has no entry for
        }

        private static bool HasPrefabInstances(JsonElement entities)
        {
            if (entities.ValueKind != JsonValueKind.Array) return false;
            foreach (var e in entities.EnumerateArray())
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("src", out var src) && src.ValueKind == JsonValueKind.Object)
                    return true;
            return false;
        }

        private static int IdOf(JsonElement line)
            => line.ValueKind == JsonValueKind.Object && line.TryGetProperty("id", out var id) && id.TryGetInt32(out int v) ? v : 0;

        // An entry's "src": a root names its prefab, any other entity of an instance its root's id and its address.
        private static bool ReadSrc(JsonElement line, out string? asset, out int rootId, out int[] path, out string? extra)
        {
            asset = null; rootId = 0; path = []; extra = null;
            if (line.ValueKind != JsonValueKind.Object || !line.TryGetProperty("src", out var src) || src.ValueKind != JsonValueKind.Object)
                return false;
            StringBuilder? unknown = null;
            foreach (var p in src.EnumerateObject())
            {
                if (p.NameEquals("prefab") && p.Value.ValueKind == JsonValueKind.String) asset = p.Value.GetString();
                else if (p.NameEquals("in") && p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int r)) rootId = r;
                else if (p.NameEquals("e") && p.Value.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<int>();
                    foreach (var n in p.Value.EnumerateArray())
                        if (n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out int v)) list.Add(v);
                    path = list.ToArray();
                }
                else
                {
                    unknown ??= new StringBuilder();
                    if (unknown.Length > 0) unknown.Append(',');
                    unknown.Append('"').Append(Esc(p.Name)).Append("\":").Append(Compact(p.Value));
                }
            }
            extra = unknown?.ToString();
            if (asset != null) { rootId = 0; path = []; }
            return asset != null || rootId > 0;
        }

        /// <summary>
        /// The entities of a file with every prefab instance expanded: each has an id in the file's id space and
        /// all its components, with entity references in that id space. An entity of a prefab the file has no
        /// entry for (the prefab gained it) gets a new id, from <paramref name="firstFreeId"/> upwards. An entry
        /// for an entity the prefab no longer has is left out and recorded in <paramref name="dropped"/> (its id →
        /// its instance root's id). <paramref name="unresolved"/>: why an instance could not be expanded, if one
        /// could not — its entries are then returned as they are (<see cref="Expanded.Missing"/>).
        /// </summary>
        private static List<Expanded> ExpandEntities(JsonElement entities, int firstFreeId, HashSet<string>? loading,
                                                     List<string> notes, Dictionary<int, int> dropped, out string? unresolved)
        {
            unresolved = null;
            var roots = new Dictionary<int, InstanceRoot>();
            var rootOrder = new List<int>();
            var rootLineIndex = new Dictionary<int, int>();
            int maxId = 0, index = -1;
            foreach (var line in entities.EnumerateArray())
            {
                index++;
                int id = IdOf(line);
                if (id > maxId) maxId = id;
                if (id > 0 && ReadSrc(line, out string? asset, out _, out _, out _) && asset != null
                    && roots.TryAdd(id, new InstanceRoot { Asset = asset }))
                {
                    rootOrder.Add(id);
                    rootLineIndex[id] = index;
                }
            }

            HashSet<int>? duplicates = null;   // indices of entries for an address another entry already has
            index = -1;
            foreach (var line in entities.EnumerateArray())
            {
                index++;
                if (!ReadSrc(line, out string? asset, out int rootId, out int[] path, out _) || asset != null) continue;
                if (roots.TryGetValue(rootId, out var r) && path.Length > 0 && !r.Lines.TryAdd(Prefab.PathKey(path), IdOf(line)))
                    (duplicates ??= new HashSet<int>()).Add(index);
            }

            int next = Math.Max(firstFreeId, maxId + 1);
            foreach (int rid in rootOrder)
            {
                var r = roots[rid];
                r.Prefab = PrefabLibrary.Get(r.Asset, loading);
                if (r.Prefab == null)
                {
                    unresolved ??= loading != null && loading.Contains(r.Asset)
                        ? $"prefab '{r.Asset}' contains itself"
                        : PrefabLibrary.ErrorOf(r.Asset) ?? $"prefab '{r.Asset}' could not be loaded";
                    continue;
                }
                foreach (var pe in r.Prefab.Entities)
                {
                    if (pe.Path.Length == 0) r.Map[pe.Id] = rid;
                    else if (r.Lines.TryGetValue(Prefab.PathKey(pe.Path), out int lineId)) r.Map[pe.Id] = lineId;
                    else { r.Map[pe.Id] = next++; r.Unlisted.Add(pe); }
                }
            }

            var result = new List<Expanded>();
            index = -1;
            foreach (var line in entities.EnumerateArray())
            {
                index++;
                int id = IdOf(line);
                JsonElement c = line.ValueKind == JsonValueKind.Object && line.TryGetProperty("c", out var cEl) && cEl.ValueKind == JsonValueKind.Object ? cEl : default;
                if (!ReadSrc(line, out string? asset, out int rootId, out int[] path, out string? extra))
                {
                    result.Add(new Expanded { Id = id, C = c });
                    continue;
                }

                JsonElement removed = line.TryGetProperty("removed", out var rm) && rm.ValueKind == JsonValueKind.Array ? rm : default;

                if (asset != null)
                {
                    // A root entry. (One with no usable id, or a second one with the same id, is kept as it is.)
                    if (!roots.TryGetValue(id, out var r) || rootLineIndex[id] != index || r.Prefab == null)
                    {
                        result.Add(new Expanded { Id = id, RootId = id, Asset = asset, Missing = true, Line = line });
                        continue;
                    }
                    result.Add(new Expanded
                    {
                        Id = id, RootId = id, Asset = asset, Extra = extra,
                        C = MergeComponents(r.Prefab.Root.Components, r.Map, c, removed, isRoot: true),
                    });
                    foreach (var pe in r.Unlisted)
                        result.Add(new Expanded
                        {
                            Id = r.Map[pe.Id], RootId = id, Path = pe.Path,
                            C = MergeComponents(pe.Components, r.Map, default, default, isRoot: false),
                        });
                    continue;
                }

                if (!roots.TryGetValue(rootId, out var owner))
                {
                    notes.Add($"entity {id} belongs to a prefab instance (root {rootId}) that is not in the file; its entry is kept as it is");
                    result.Add(new Expanded { Id = id, RootId = id, Path = path, Missing = true, Line = line });
                    continue;
                }
                if (owner.Prefab == null)
                {
                    result.Add(new Expanded { Id = id, RootId = rootId, Path = path, Missing = true, Line = line });
                    continue;
                }
                PrefabEntity? source = path.Length > 0 && duplicates?.Contains(index) != true ? owner.Prefab.ByPath(path) : null;
                if (source == null)
                {
                    dropped[id] = rootId;
                    notes.Add($"prefab '{owner.Asset}' has no entity at {Prefab.PathKey(path)} any more: the entry of entity {id} is dropped");
                    continue;
                }
                result.Add(new Expanded
                {
                    Id = id, RootId = rootId, Path = source.Path, Extra = extra,
                    C = MergeComponents(source.Components, owner.Map, c, removed, isRoot: false),
                });
            }
            return result;
        }

        private static void ReadEntitiesWithPrefabs(ECSWorld world, JsonElement entitiesEl)
        {
            var notes = new List<string>();
            var dropped = new Dictionary<int, int>();
            var expanded = ExpandEntities(entitiesEl, world.NextSceneId, null, notes, dropped, out _);
            foreach (string note in notes) Logger.Warning("[Prefab] " + note);
            CreateExpanded(world, expanded, dropped, keepIds: true, null);
        }

        // Creates the entities of an expanded list. keepIds: they get the list's ids as scene ids (a file being
        // loaded); otherwise the ids only connect the list's entities to each other (a new instance).
        private static void CreateExpanded(ECSWorld world, List<Expanded> expanded, Dictionary<int, int>? dropped,
                                           bool keepIds, List<Entity>? created)
        {
            var idMap = new Dictionary<int, Entity>();
            var entities = new Entity[expanded.Count];
            var fx = new Fixups();
            for (int i = 0; i < expanded.Count; i++)
            {
                var x = expanded[i];
                Entity e = world.CreateEntity();   // auto-adds NameTag/ActiveFlag/Transform
                entities[i] = e;
                idMap.TryAdd(x.Id, e);
                if (keepIds) world.SetSceneId(e, x.Id);
                created?.Add(e);
                if (x.Missing) ReadMissing(world, e, x.Line, fx);
                else if (x.C.ValueKind == JsonValueKind.Object) ReadEntityComponents(world, e, x.C, fx);
            }

            Entity Resolve(int id) => idMap.TryGetValue(id, out Entity e) ? e : Entity.Null;
            // What the file had put under an entity its prefab no longer has moves to the instance's root.
            Entity ResolveParent(int id)
                => idMap.TryGetValue(id, out Entity e) ? e
                 : dropped != null && dropped.TryGetValue(id, out int rootId) ? Resolve(rootId) : Entity.Null;
            ApplyFixups(world, fx, Resolve, ResolveParent);

            for (int i = 0; i < expanded.Count; i++)
            {
                var x = expanded[i];
                if (x.RootId == 0 || !idMap.TryGetValue(x.RootId, out Entity rootEntity)) continue;
                world.AddComponent(entities[i], new PrefabLink
                {
                    Root = rootEntity, Asset = x.Asset, Path = x.Path, Extra = x.Extra,
                    Missing = x.Missing, Raw = x.Missing ? Compact(x.Line) : null,
                });
            }
        }

        // An entity of an instance whose prefab is not there: its name and its place, so the scene keeps its shape.
        private static void ReadMissing(ECSWorld world, Entity e, JsonElement line, Fixups fx)
        {
            if (line.ValueKind != JsonValueKind.Object || !line.TryGetProperty("c", out var c) || c.ValueKind != JsonValueKind.Object) return;
            if (c.TryGetProperty("NameTag", out var nt) && nt.ValueKind == JsonValueKind.Object
                && nt.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String)
                world.AddComponent(e, new NameTag { Name = name.GetString() ?? "Entity" });
            if (!c.TryGetProperty("Transform", out var t) || t.ValueKind != JsonValueKind.Object) return;
            var tr = Transform.Default;
            if (t.TryGetProperty("P", out _)) tr.Position = RV3(t, "P");
            if (t.TryGetProperty("S", out _)) tr.Scale = RV3(t, "S");
            if (t.TryGetProperty("E", out _)) tr.EulerAngles = RV3(t, "E");
            if (t.TryGetProperty("Q", out var q) && q.ValueKind == JsonValueKind.Array && q.GetArrayLength() == 4)
                tr.Rotation = new System.Numerics.Quaternion(q[0].GetSingle(), q[1].GetSingle(), q[2].GetSingle(), q[3].GetSingle());
            if (t.TryGetProperty("Par", out var par) && par.TryGetInt32(out int parentId)) fx.Parent[e] = parentId;
            world.AddComponent(e, tr);
        }

        /// <summary>
        /// Creates an instance of a prefab in the open scene: its entities, each with a <see cref="PrefabLink"/>,
        /// with the prefab's values and no parent. Returns the instance's root, or <see cref="Entity.Null"/> with
        /// <paramref name="error"/> when the prefab cannot be loaded. <paramref name="created"/> gets every entity
        /// made, root first.
        /// </summary>
        public static Entity InstantiatePrefab(string key, out string? error, List<Entity>? created = null)
        {
            error = null;
            if (PrefabLibrary.Get(key) == null)
            {
                error = PrefabLibrary.ErrorOf(key) ?? $"prefab '{key}' could not be loaded";
                return Entity.Null;
            }
            List<Expanded> expanded;
            using (var doc = JsonDocument.Parse("[{\"id\":1,\"src\":{\"prefab\":\"" + Esc(key) + "\"}}]"))
                expanded = ExpandEntities(doc.RootElement, 2, null, new List<string>(), new Dictionary<int, int>(), out _);
            var list = created ?? new List<Entity>();
            int first = list.Count;
            CreateExpanded(ECSWorld.Instance, expanded, null, keepIds: false, list);
            return list.Count > first ? list[first] : Entity.Null;
        }

        // Called by PrefabLibrary: a prefab file's text → the loaded prefab, nested instances expanded.
        internal static Prefab? LoadPrefab(string key, string json, HashSet<string> loading, out string? error)
        {
            error = null;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch (JsonException ex) { error = $"prefab '{key}' is not valid JSON: {ex.Message}"; return null; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("entities", out var entitiesEl)
                    || entitiesEl.ValueKind != JsonValueKind.Array || entitiesEl.GetArrayLength() == 0)
                {
                    error = $"prefab '{key}' has no entities";
                    return null;
                }
                int nextId = root.TryGetProperty("nextId", out var n) && n.TryGetInt32(out int v) ? v : 1;
                var notes = new List<string>();
                var dropped = new Dictionary<int, int>();
                var expanded = ExpandEntities(entitiesEl, nextId, loading, notes, dropped, out string? unresolved);
                if (unresolved != null)
                {
                    // Not loaded at all rather than without that part: a scene would otherwise drop, and on its
                    // next save lose, what it holds for the entities that are missing only for now.
                    error = $"prefab '{key}' cannot be loaded: {unresolved}";
                    return null;
                }
                foreach (string note in notes) Logger.Warning($"[Prefab] '{key}': {note}");

                var prefab = new Prefab
                {
                    Key = key,
                    Name = root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                        ? nameEl.GetString() ?? "" : System.IO.Path.GetFileNameWithoutExtension(key),
                };

                var components = new JsonElement[expanded.Count];
                int rootIndex = -1;
                for (int i = 0; i < expanded.Count; i++)
                {
                    var x = expanded[i];
                    JsonElement c = x.C.ValueKind == JsonValueKind.Object ? x.C.Clone() : EmptyObject;
                    int parent = ParentOf(c);
                    if (parent > 0 && dropped.TryGetValue(parent, out int newParent))
                    {
                        using var over = JsonDocument.Parse("{\"Transform\":{\"Par\":" + newParent + "}}");
                        c = MergeComponents(c, null, over.RootElement, default, isRoot: false);
                        parent = newParent;
                    }
                    components[i] = c;
                    if (parent > 0) continue;
                    if (rootIndex >= 0)
                    {
                        error = $"prefab '{key}' has more than one root: entities {expanded[rootIndex].Id} and {x.Id} have no parent";
                        return null;
                    }
                    rootIndex = i;
                }
                if (rootIndex < 0)
                {
                    error = $"prefab '{key}' has no root: every entity has a parent";
                    return null;
                }

                int rootId = expanded[rootIndex].Id;
                AddPrefabEntity(prefab, expanded[rootIndex], components[rootIndex], rootId);
                for (int i = 0; i < expanded.Count; i++)
                    if (i != rootIndex) AddPrefabEntity(prefab, expanded[i], components[i], rootId);
                return prefab;
            }
        }

        private static void AddPrefabEntity(Prefab prefab, Expanded x, JsonElement components, int rootId)
        {
            bool nestedRoot = x.RootId == x.Id && x.Asset != null;
            int[] path;
            if (x.Id == rootId) path = [];
            else if (x.RootId == 0 || x.RootId == x.Id) path = [x.Id];
            else
            {
                path = new int[x.Path.Length + 1];
                path[0] = x.RootId;
                x.Path.CopyTo(path, 1);
            }
            prefab.Add(new PrefabEntity { Id = x.Id, Path = path, Components = components, NestedAsset = nestedRoot ? x.Asset : null });
            if (nestedRoot)
            {
                prefab.Dependencies.Add(x.Asset!);
                if (PrefabLibrary.Get(x.Asset!) is { } nested)
                    foreach (string d in nested.Dependencies) prefab.Dependencies.Add(d);
            }
        }

        private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

        private static int ParentOf(JsonElement c)
            => c.ValueKind == JsonValueKind.Object && c.TryGetProperty("Transform", out var t) && t.ValueKind == JsonValueKind.Object
               && t.TryGetProperty("Par", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out int v) ? v : 0;

        // ── Merging an entry over the prefab's values ──────────────────────────────────

        /// <summary>
        /// A prefab entity's components for one instance: entity references mapped through <paramref name="map"/>
        /// (prefab entity id → id in the file; null = as they are), the instance's blocks
        /// <paramref name="over"/> merged over them, the components named in <paramref name="removed"/> left out.
        /// </summary>
        private static JsonElement MergeComponents(JsonElement baseC, Dictionary<int, int>? map, JsonElement over,
                                                   JsonElement removed, bool isRoot)
        {
            var buffer = new ArrayBufferWriter<byte>(512);
            using (var w = new Utf8JsonWriter(buffer))
            {
                w.WriteStartObject();
                bool hasOver = over.ValueKind == JsonValueKind.Object;
                if (baseC.ValueKind == JsonValueKind.Object)
                    foreach (var b in baseC.EnumerateObject())
                    {
                        if (IsRemoved(removed, b.Name)) continue;
                        JsonElement o = default;
                        if (hasOver && over.TryGetProperty(b.Name, out o) && o.ValueKind == JsonValueKind.Null) continue;
                        w.WritePropertyName(b.Name);
                        WriteMergedBlock(w, b.Name, b.Value, o, map, isRoot);
                    }
                if (hasOver)
                    foreach (var o in over.EnumerateObject())
                    {
                        if (o.Value.ValueKind == JsonValueKind.Null) continue;
                        if (baseC.ValueKind == JsonValueKind.Object && baseC.TryGetProperty(o.Name, out _)) continue;
                        o.WriteTo(w);   // a component the instance added
                    }
                w.WriteEndObject();
            }
            using var doc = JsonDocument.Parse(buffer.WrittenMemory);
            return doc.RootElement.Clone();
        }

        private static bool IsRemoved(JsonElement removed, string key)
        {
            if (removed.ValueKind != JsonValueKind.Array) return false;
            foreach (var r in removed.EnumerateArray())
                if (r.ValueKind == JsonValueKind.String && r.ValueEquals(key)) return true;
            return false;
        }

        private static void WriteMergedBlock(Utf8JsonWriter w, string key, JsonElement b, JsonElement o,
                                             Dictionary<int, int>? map, bool isRoot)
        {
            bool hasOver = o.ValueKind != JsonValueKind.Undefined;
            if (b.ValueKind != JsonValueKind.Object || (hasOver && o.ValueKind != JsonValueKind.Object))
            {
                (hasOver ? o : b).WriteTo(w);   // not a block of fields (the "Scripts" array): the instance's value, whole
                return;
            }
            _referenceFields.TryGetValue(key, out string[]? refs);
            bool transform = key == "Transform";
            // "Q" is the exact rotation and wins over "E" when both are read: Euler degrees given alone replace it.
            bool dropQ = transform && hasOver && o.TryGetProperty("E", out _) && !o.TryGetProperty("Q", out _);

            w.WriteStartObject();
            foreach (var p in b.EnumerateObject())
            {
                if (hasOver && o.TryGetProperty(p.Name, out var ov))
                {
                    if (ov.ValueKind == JsonValueKind.Null) continue;   // the instance has no such field
                    w.WritePropertyName(p.Name);
                    if (ov.ValueKind == JsonValueKind.Object && p.Value.ValueKind == JsonValueKind.Object) WriteMergedObject(w, p.Value, ov);
                    else ov.WriteTo(w);
                    continue;
                }
                if (transform && ((isRoot && p.NameEquals("Par")) || (dropQ && p.NameEquals("Q")))) continue;
                if (map != null && refs != null && Array.IndexOf(refs, p.Name) >= 0
                    && p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int reference) && reference > 0)
                {
                    int mapped = map.TryGetValue(reference, out int m) ? m : -1;
                    if (mapped <= 0 && transform) continue;   // no such parent: none
                    w.WriteNumber(p.Name, mapped);
                    continue;
                }
                p.WriteTo(w);
            }
            if (hasOver)
                foreach (var p in o.EnumerateObject())
                    if (p.Value.ValueKind != JsonValueKind.Null && !b.TryGetProperty(p.Name, out _)) p.WriteTo(w);
            w.WriteEndObject();
        }

        private static void WriteMergedObject(Utf8JsonWriter w, JsonElement b, JsonElement o)
        {
            w.WriteStartObject();
            foreach (var p in b.EnumerateObject())
            {
                if (!o.TryGetProperty(p.Name, out var ov)) { p.WriteTo(w); continue; }
                if (ov.ValueKind == JsonValueKind.Null) continue;
                w.WritePropertyName(p.Name);
                if (ov.ValueKind == JsonValueKind.Object && p.Value.ValueKind == JsonValueKind.Object) WriteMergedObject(w, p.Value, ov);
                else ov.WriteTo(w);
            }
            foreach (var p in o.EnumerateObject())
                if (p.Value.ValueKind != JsonValueKind.Null && !b.TryGetProperty(p.Name, out _)) p.WriteTo(w);
            w.WriteEndObject();
        }

        private static string Compact(JsonElement value)
        {
            var buffer = new ArrayBufferWriter<byte>(256);
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                value.WriteTo(w);
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        // ── Writing ─────────────────────────────────────────────────────────────────────

        // What a save needs to know about the instances of the world: who belongs to which.
        private sealed class PrefabWriteContext
        {
            private readonly Dictionary<Entity, Dictionary<string, Entity>> _members = new();   // instance root → path key → entity
            private readonly Dictionary<Entity, Dictionary<int, int>> _maps = new();

            /// <summary>Null when the world has no entity of a prefab instance: nothing is written differently then.</summary>
            public static PrefabWriteContext? Create(ECSWorld world)
            {
                if (!world.HasAnyComponent<PrefabLink>()) return null;
                var ctx = new PrefabWriteContext();
                foreach (Entity e in world.Entities)
                {
                    if (!world.TryGetComponent<PrefabLink>(e, out var link) || link.Missing || link.Root.IsNull) continue;
                    if (!ctx._members.TryGetValue(link.Root, out var members))
                        ctx._members[link.Root] = members = new Dictionary<string, Entity>(StringComparer.Ordinal);
                    members.TryAdd(Prefab.PathKey(link.Path), e);
                }
                return ctx;
            }

            /// <summary>For one instance: prefab entity id → the scene id of the instance's entity for it.</summary>
            public Dictionary<int, int> MapOf(ECSWorld world, Entity root, Prefab prefab)
            {
                if (_maps.TryGetValue(root, out var map)) return map;
                _maps[root] = map = new Dictionary<int, int>();
                if (_members.TryGetValue(root, out var members))
                    foreach (var pe in prefab.Entities)
                        if (members.TryGetValue(Prefab.PathKey(pe.Path), out Entity e)) map[pe.Id] = world.GetSceneId(e);
                return map;
            }
        }

        // Writes an entity of a prefab instance as its address and its differences from the prefab.
        // False (nothing written) for an entity that is not one: it is written like any other.
        private static bool WritePrefabInstanceEntity(StringBuilder sb, ECSWorld world, Entity id, PrefabWriteContext ctx)
        {
            if (!world.TryGetComponent<PrefabLink>(id, out var link)) return false;
            if (link.Missing)
            {
                if (link.Raw == null) return false;
                sb.Append(link.Raw);   // the prefab is not there: the entry stays as it was read
                return true;
            }
            Entity rootEntity = link.Root;
            if (rootEntity.IsNull || !rootEntity.IsAlive
                || !world.TryGetComponent<PrefabLink>(rootEntity, out var rootLink) || rootLink.Asset == null) return false;
            bool isRoot = rootEntity.Equals(id);

            var full = new StringBuilder();
            WriteEntities(full, world, new List<Entity> { id }, prefabLinks: false);
            using var doc = JsonDocument.Parse(full.ToString());
            JsonElement current = doc.RootElement.GetProperty("c");

            sb.Append("{\"id\":").Append(world.GetSceneId(id)).Append(",\"src\":{");
            if (isRoot) sb.Append("\"prefab\":\"").Append(Esc(rootLink.Asset)).Append('"');
            else
            {
                sb.Append("\"in\":").Append(world.GetSceneId(rootEntity)).Append(",\"e\":[");
                for (int i = 0; link.Path != null && i < link.Path.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(link.Path[i]);
                }
                sb.Append(']');
            }
            if (!string.IsNullOrEmpty(link.Extra)) sb.Append(',').Append(link.Extra);
            sb.Append('}');

            Prefab? prefab = PrefabLibrary.Get(rootLink.Asset);
            PrefabEntity? source = prefab?.ByPath(link.Path);
            if (source == null)
            {
                // The prefab, or this entity of it, went away while the scene was open: all values are written,
                // so nothing is lost; with the file back, they read as overrides.
                sb.Append(",\"c\":").Append(current.GetRawText()).Append('}');
                return true;
            }
            JsonElement baseC = MergeComponents(source.Components, ctx.MapOf(world, rootEntity, prefab!), default, default, isRoot);
            WriteOverrides(sb, baseC, current, isRoot);
            sb.Append('}');
            return true;
        }

        // Appends ,"c":{…} and ,"removed":[…] — what `current` has that differs from `baseC` — when there is any.
        private static void WriteOverrides(StringBuilder sb, JsonElement baseC, JsonElement current, bool isRoot)
        {
            var c = new StringBuilder();
            foreach (var p in current.EnumerateObject())
            {
                string? difference =
                    !baseC.TryGetProperty(p.Name, out var b) ? p.Value.GetRawText()          // a component the instance added
                    : isRoot && p.NameEquals("NameTag") ? p.Value.GetRawText()               // the root's name is the instance's own
                    : DiffBlock(p.Name, b, p.Value, isRoot);
                if (difference == null) continue;
                if (c.Length > 0) c.Append(',');
                c.Append('"').Append(Esc(p.Name)).Append("\":").Append(difference);
            }
            if (c.Length > 0) sb.Append(",\"c\":{").Append(c).Append('}');

            bool anyRemoved = false;
            foreach (var b in baseC.EnumerateObject())
            {
                if (current.TryGetProperty(b.Name, out _)) continue;
                sb.Append(anyRemoved ? "," : ",\"removed\":[").Append('"').Append(Esc(b.Name)).Append('"');
                anyRemoved = true;
            }
            if (anyRemoved) sb.Append(']');
        }

        // The fields of a component block that differ from the prefab's, as a JSON object; null when none does.
        private static string? DiffBlock(string key, JsonElement b, JsonElement c, bool isRoot)
        {
            if (b.ValueKind != JsonValueKind.Object || c.ValueKind != JsonValueKind.Object)
                return JsonValueEquals(b, c) ? null : c.GetRawText();
            if (key != "Transform") return DiffObject(b, c, 0);
            // The root's position and rotation are the instance's own. "E" is "Q" in degrees: written with it, never alone.
            bool rotation = isRoot || !(b.TryGetProperty("Q", out var bq) && c.TryGetProperty("Q", out var cq) && JsonValueEquals(bq, cq));
            return DiffObject(b, c, (isRoot ? 1 : 0) | (rotation ? 2 : 4));
        }

        // transformRule: 0 = compare every field; bit 0 = always "P"; bit 1 = always "E" and "Q"; bit 2 = never "E" and "Q".
        private static string? DiffObject(JsonElement b, JsonElement c, int transformRule)
        {
            StringBuilder? sb = null;
            foreach (var p in c.EnumerateObject())
            {
                bool rotationField = transformRule != 0 && (p.NameEquals("E") || p.NameEquals("Q"));
                if (rotationField && (transformRule & 4) != 0) continue;
                bool always = (rotationField && (transformRule & 2) != 0) || ((transformRule & 1) != 0 && p.NameEquals("P"));
                string value = p.Value.GetRawText();
                if (!always && b.TryGetProperty(p.Name, out var bv))
                {
                    if (bv.ValueKind == JsonValueKind.Object && p.Value.ValueKind == JsonValueKind.Object)
                    {
                        string? inner = DiffObject(bv, p.Value, 0);
                        if (inner == null) continue;
                        value = inner;
                    }
                    else if (JsonValueEquals(bv, p.Value)) continue;
                }
                sb ??= new StringBuilder("{");
                if (sb.Length > 1) sb.Append(',');
                sb.Append('"').Append(Esc(p.Name)).Append("\":").Append(value);
            }
            foreach (var p in b.EnumerateObject())
            {
                if (c.TryGetProperty(p.Name, out _)) continue;
                sb ??= new StringBuilder("{");
                if (sb.Length > 1) sb.Append(',');
                sb.Append('"').Append(Esc(p.Name)).Append("\":null");   // the prefab has the field, the instance does not
            }
            return sb?.Append('}').ToString();
        }

        // Equal as the reader sees them: a number by its value as a float (the file may hold more digits than a float keeps).
        private static bool JsonValueEquals(JsonElement a, JsonElement b)
        {
            if (a.ValueKind != b.ValueKind) return false;
            switch (a.ValueKind)
            {
                case JsonValueKind.Number:
                    if (a.TryGetInt64(out long la) && b.TryGetInt64(out long lb)) return la == lb;
                    return (float)a.GetDouble() == (float)b.GetDouble();
                case JsonValueKind.String:
                    return a.GetString() == b.GetString();
                case JsonValueKind.Array:
                {
                    if (a.GetArrayLength() != b.GetArrayLength()) return false;
                    var eb = b.EnumerateArray();
                    foreach (var x in a.EnumerateArray())
                    {
                        eb.MoveNext();
                        if (!JsonValueEquals(x, eb.Current)) return false;
                    }
                    return true;
                }
                case JsonValueKind.Object:
                {
                    int count = 0;
                    foreach (var p in a.EnumerateObject())
                    {
                        count++;
                        if (!b.TryGetProperty(p.Name, out var bv) || !JsonValueEquals(p.Value, bv)) return false;
                    }
                    foreach (var _ in b.EnumerateObject()) count--;
                    return count == 0;
                }
                default:
                    return true;   // true, false, null
            }
        }
    }
}
