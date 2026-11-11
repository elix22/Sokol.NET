using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Animation;

namespace GameEditor.Framework.Scene
{
    /// <summary>
    /// What <see cref="SceneSerializer.RefreshPrefabInstances"/> took out of the scene because a prefab no longer
    /// has it. Handed to a later refresh, the same entities (handles and scene ids) come back when the prefab has
    /// them again — an undo and redo of a prefab change then leaves the scene as it was.
    /// </summary>
    public sealed class PrefabRefreshMemo
    {
        internal readonly Dictionary<(Entity Root, string Path), ECSWorld.DetachedSubtree> Removed = new();

        /// <summary>Deletes for good what is still held (the undo entry that owned the memo is gone).</summary>
        public void Discard()
        {
            foreach (var d in Removed.Values) ECSWorld.Instance.Discard(d);
            Removed.Clear();
        }
    }

    // Editing with prefabs: what an editor needs beyond loading and saving a scene (SceneSerializer.Prefabs.cs) —
    // writing a prefab file from entities, bringing open instances up to date with their files, taking a prefab's
    // values back, and saying what an instance changed.
    public static partial class SceneSerializer
    {
        // ── An entity's components made exactly a given set ─────────────────────────────

        private static readonly (string Key, Action<ECSWorld, Entity> Remove)[] BuiltInRemovers =
        [
            ("MeshRenderer",        static (w, e) => w.RemoveComponent<MeshRenderer>(e)),
            ("SkinnedMeshRenderer", static (w, e) => w.RemoveComponent<SkinnedMeshRenderer>(e)),
            ("Camera",              static (w, e) => w.RemoveComponent<CameraComponent>(e)),
            ("Light",               static (w, e) => w.RemoveComponent<LightComponent>(e)),
            ("Rigidbody",           static (w, e) => w.RemoveComponent<RigidbodyComponent>(e)),
            ("Script",              static (w, e) => w.RemoveComponent<ScriptComponent>(e)),
            ("Scripts",             static (w, e) => w.RemoveComponent<ScriptCollectionComponent>(e)),
            ("Constraint",          static (w, e) => w.RemoveComponent<ConstraintComponent>(e)),
            ("Character",           static (w, e) => w.RemoveComponent<CharacterComponent>(e)),
            ("Vehicle",             static (w, e) => w.RemoveComponent<VehicleComponent>(e)),
            ("WheelFollow",         static (w, e) => w.RemoveComponent<WheelFollowerComponent>(e)),
        ];

        // Gives an existing entity exactly the components of `c` (an entity's "c"): the ones it has and `c` lacks go.
        private static void SetExactComponents(ECSWorld world, Entity e, JsonElement c, Fixups fx)
        {
            foreach (var (key, remove) in BuiltInRemovers)
                if (!c.TryGetProperty(key, out _)) remove(world, e);
            foreach (var (key, ext) in _extensions)
                if (ext.Remove != null && !c.TryGetProperty(key, out _)) ext.Remove(world, e);
            world.RemoveComponent<PreservedComponents>(e);
            ReadEntityComponents(world, e, c, fx);
        }

        private static JsonElement FullComponents(ECSWorld world, Entity e)
        {
            var sb = new StringBuilder();
            WriteEntities(sb, world, new List<Entity> { e }, prefabLinks: false);
            using var doc = JsonDocument.Parse(sb.ToString());
            return doc.RootElement.GetProperty("c").Clone();
        }

        // The instance an entity belongs to and the prefab entity it stands for (null: the prefab, or this entity of
        // it, is not there). False for an entity that is not part of an instance, or of one whose prefab was missing at load.
        private static bool TryGetPrefabSource(ECSWorld world, Entity e, out PrefabLink link, out Entity rootEntity,
                                               out string asset, out Prefab? prefab, out PrefabEntity? source)
        {
            rootEntity = default; asset = ""; prefab = null; source = null;
            if (!world.TryGetComponent<PrefabLink>(e, out link) || link.Missing) return false;
            rootEntity = link.Root;
            if (rootEntity.IsNull || !rootEntity.IsAlive
                || !world.TryGetComponent<PrefabLink>(rootEntity, out var rootLink) || rootLink.Asset == null) return false;
            asset = rootLink.Asset;
            prefab = PrefabLibrary.Get(asset);
            source = prefab?.ByPath(link.Path);
            return true;
        }

        /// <summary>Compact JSON as the scene file has it on disk: indented.</summary>
        public static string Indent(string compactJson)
        {
            using var doc = JsonDocument.Parse(compactJson);
            var buffer = new ArrayBufferWriter<byte>(compactJson.Length * 3);
            using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = RelaxedJson.Encoder }))
                doc.RootElement.WriteTo(w);
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        // ── What an instance changed ────────────────────────────────────────────────────

        /// <summary>
        /// An entity's place in a prefab instance, and what it changed, as JSON:
        /// <c>{"asset":"Prefabs/Enemy.prefab","root":12,"path":[3],"overrides":{"Light":{"Int":3}},"added":["Script"],"removed":["Rigidbody"]}</c>
        /// — <c>overrides</c> holds the fields that differ from the prefab with their values, <c>added</c> and
        /// <c>removed</c> the components the entity has more or less than the prefab. The root's name, position,
        /// rotation and parent are its own and are not listed. <c>"missing":true</c>: the prefab could not be loaded.
        /// Null for an entity that is not part of an instance.
        /// </summary>
        public static string? DescribePrefabLink(Entity e)
        {
            var world = ECSWorld.Instance;
            if (!world.TryGetComponent<PrefabLink>(e, out var link)) return null;
            Entity rootEntity = link.Root;
            string? asset = link.Asset;
            if (!rootEntity.IsNull && rootEntity.IsAlive && world.TryGetComponent<PrefabLink>(rootEntity, out var rootLink)) asset = rootLink.Asset;

            var sb = new StringBuilder("{\"asset\":\"").Append(Esc(asset ?? "")).Append("\",\"root\":")
                .Append(rootEntity.IsNull || !rootEntity.IsAlive ? 0 : world.GetSceneId(rootEntity)).Append(",\"path\":[");
            for (int i = 0; link.Path != null && i < link.Path.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(link.Path[i]);
            }
            sb.Append(']');

            if (!TryGetPrefabSource(world, e, out _, out rootEntity, out _, out Prefab? prefab, out PrefabEntity? source) || source == null)
                return sb.Append(",\"missing\":true}").ToString();

            bool isRoot = rootEntity.Equals(e);
            var ctx = PrefabWriteContext.Create(world)!;
            JsonElement baseC = MergeComponents(source.Components, ctx.MapOf(world, rootEntity, prefab!), default, default, isRoot);
            JsonElement current = FullComponents(world, e);

            var overrides = new StringBuilder();
            var added = new StringBuilder();
            foreach (var p in current.EnumerateObject())
            {
                if (!baseC.TryGetProperty(p.Name, out var b))
                {
                    if (added.Length > 0) added.Append(',');
                    added.Append('"').Append(Esc(p.Name)).Append('"');
                    continue;
                }
                if (isRoot && p.NameEquals("NameTag")) continue;
                string? difference = DiffBlock(p.Name, b, p.Value, isRoot, describe: true);
                if (difference == null) continue;
                if (overrides.Length > 0) overrides.Append(',');
                overrides.Append('"').Append(Esc(p.Name)).Append("\":").Append(difference);
            }
            var removed = new StringBuilder();
            foreach (var b in baseC.EnumerateObject())
            {
                if (current.TryGetProperty(b.Name, out _)) continue;
                if (removed.Length > 0) removed.Append(',');
                removed.Append('"').Append(Esc(b.Name)).Append('"');
            }
            return sb.Append(",\"overrides\":{").Append(overrides).Append("},\"added\":[").Append(added)
                     .Append("],\"removed\":[").Append(removed).Append("]}").ToString();
        }

        // ── Taking the prefab's values back ─────────────────────────────────────────────

        /// <summary>
        /// Gives an entity of a prefab instance the prefab's values again: one field of a component, one component
        /// (one the instance added goes, one it removed comes back), or with no <paramref name="component"/> all of
        /// the entity's components. The root's name, position, rotation and parent are the instance's own and stay.
        /// Returns an error (nothing changed then), or null.
        /// </summary>
        public static string? RevertToPrefab(Entity e, string? component = null, string? field = null)
        {
            var world = ECSWorld.Instance;
            if (!TryGetPrefabSource(world, e, out _, out Entity rootEntity, out string asset, out Prefab? prefab, out PrefabEntity? source))
                return "the entity is not part of a prefab instance";
            if (source == null) return PrefabLibrary.ErrorOf(asset) ?? $"prefab '{asset}' no longer has this entity";

            bool isRoot = rootEntity.Equals(e);
            var ctx = PrefabWriteContext.Create(world)!;
            var map = ctx.MapOf(world, rootEntity, prefab!);
            JsonElement baseC = MergeComponents(source.Components, map, default, default, isRoot);
            JsonElement current = FullComponents(world, e);
            if (component != null && !baseC.TryGetProperty(component, out _) && !current.TryGetProperty(component, out _))
                return $"neither the entity nor the prefab has a '{component}'";
            if (component != null && field != null)
            {
                if (!current.TryGetProperty(component, out var block) || block.ValueKind != JsonValueKind.Object
                    || !baseC.TryGetProperty(component, out var baseBlock) || baseBlock.ValueKind != JsonValueKind.Object)
                    return $"'{component}' has no field to take back (the entity and the prefab must both have the component)";
                if (!block.TryGetProperty(field, out _) && !baseBlock.TryGetProperty(field, out _))
                    return $"'{component}' has no field '{field}'";
                if (isRoot && component == "Transform" && field is "P" or "E" or "Q" or "Par")
                    return "the position, rotation and parent of an instance's root are the instance's own";
            }

            // What stays the instance's own: everything the request does not name.
            var kept = new ArrayBufferWriter<byte>(256);
            var removed = new StringBuilder("[");
            using (var w = new Utf8JsonWriter(kept, RelaxedJson))
            {
                w.WriteStartObject();
                foreach (var p in current.EnumerateObject())
                {
                    bool named = component == null || p.NameEquals(component);
                    if (!named) { p.WriteTo(w); continue; }
                    if (isRoot && p.NameEquals("NameTag")) { p.WriteTo(w); continue; }
                    bool rootTransform = isRoot && p.NameEquals("Transform");
                    if (field == null && !rootTransform) continue;   // the whole component is the prefab's again
                    bool rotation = p.NameEquals("Transform") && field is "E" or "Q";   // one value, written twice
                    w.WritePropertyName(p.Name);
                    w.WriteStartObject();
                    foreach (var f in p.Value.EnumerateObject())
                    {
                        bool stays = field == null
                            ? f.NameEquals("P") || f.NameEquals("E") || f.NameEquals("Q") || f.NameEquals("Par")   // the root's own
                            : !(f.NameEquals(field) || (rotation && (f.NameEquals("E") || f.NameEquals("Q"))));
                        if (stays) f.WriteTo(w);
                    }
                    w.WriteEndObject();
                }
                w.WriteEndObject();
                foreach (var b in baseC.EnumerateObject())
                {
                    if (current.TryGetProperty(b.Name, out _)) continue;
                    if (component == null || b.NameEquals(component)) continue;   // comes back
                    removed.Append(removed.Length > 1 ? "," : "").Append('"').Append(Esc(b.Name)).Append('"');
                }
            }
            using var keptDoc = JsonDocument.Parse(kept.WrittenMemory);
            using var removedDoc = JsonDocument.Parse(removed.Append(']').ToString());
            JsonElement target = MergeComponents(source.Components, map, keptDoc.RootElement, removedDoc.RootElement, isRoot);

            var fx = new Fixups();
            SetExactComponents(world, e, target, fx);
            ApplyFixups(world, fx, id => world.TryGetEntityBySceneId(id, out Entity x) ? x : Entity.Null);
            return null;
        }

        // ── Unpacking ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// What unpacking an instance leaves of it: for each of its entities the link it then has — null for the
        /// prefab's own entities (they become ordinary ones), and for the entities of a prefab nested in it a link
        /// to that prefab, so nested instances stay instances; with <paramref name="completely"/> null for all.
        /// Nothing is changed: the caller sets or removes the links.
        /// </summary>
        public static Dictionary<Entity, PrefabLink?> UnpackedLinks(Entity root, bool completely)
        {
            var world = ECSWorld.Instance;
            var result = new Dictionary<Entity, PrefabLink?>();
            var byPath = new Dictionary<string, Entity>(StringComparer.Ordinal);
            foreach (Entity e in world.Entities)
                if (world.TryGetComponent<PrefabLink>(e, out var l) && l.Root.Equals(root))
                {
                    result[e] = null;
                    byPath.TryAdd(Prefab.PathKey(l.Path), e);
                }
            Prefab? prefab = world.TryGetComponent<PrefabLink>(root, out var rootLink) && rootLink.Asset != null && !rootLink.Missing
                ? PrefabLibrary.Get(rootLink.Asset) : null;
            if (completely || prefab == null) return result;

            foreach (var (pathKey, e) in byPath)
            {
                int[] path = world.GetComponent<PrefabLink>(e).Path ?? [];
                PrefabEntity? nestedRoot = path.Length == 0 ? prefab.Root : prefab.ById(path[0]);
                if (nestedRoot?.NestedAsset == null) continue;   // the prefab's own entity
                if (path.Length <= 1)
                {
                    result[e] = new PrefabLink { Root = e, Asset = nestedRoot.NestedAsset, Path = [] };
                    continue;
                }
                // An entity of the nested instance: its root is the entity at [id], or the instance's root for a variant.
                Entity nestedRootEntity = nestedRoot.Path.Length == 0 ? root
                    : byPath.TryGetValue(Prefab.PathKey(nestedRoot.Path), out Entity r) ? r : default;
                if (nestedRootEntity.IsNull) continue;
                result[e] = new PrefabLink { Root = nestedRootEntity, Path = path[1..] };
            }
            return result;
        }

        // ── Bringing the open scene's instances up to date with their files ─────────────

        /// <summary>
        /// Reads the prefab files again and gives every instance of the open scene their values, keeping what each
        /// instance changed itself. Entities keep their handles and scene ids; an entity a prefab gained appears,
        /// one it lost goes (what the scene had put under it moves to the instance's root).
        /// <paramref name="change"/> runs after the instances' own values were taken, against the prefabs as loaded,
        /// and before the files are read again: write or restore a prefab file in it.
        /// The instance whose root is <paramref name="except"/> is left as it is (the instance a change was applied
        /// from: its values are already the new ones). <paramref name="memo"/>: see <see cref="PrefabRefreshMemo"/>.
        /// </summary>
        public static void RefreshPrefabInstances(Action? change = null, Entity except = default, PrefabRefreshMemo? memo = null)
        {
            var world = ECSWorld.Instance;
            var entries = new StringBuilder("[");
            var ctx = PrefabWriteContext.Create(world);
            if (ctx != null)
                foreach (Entity e in world.Entities)
                {
                    if (!world.TryGetComponent<PrefabLink>(e, out var link)) continue;
                    if (!except.IsNull && (e.Equals(except) || link.Root.Equals(except))) continue;
                    int before = entries.Length;
                    if (before > 1) entries.Append(',');
                    if (!WritePrefabInstanceEntity(entries, world, e, ctx)) entries.Length = before;
                }
            entries.Append(']');

            change?.Invoke();
            PrefabLibrary.Invalidate();
            if (entries.Length <= 2) return;

            using var doc = JsonDocument.Parse(entries.ToString());
            var notes = new List<string>();
            var dropped = new Dictionary<int, int>();
            var expanded = ExpandEntities(doc.RootElement, world.NextSceneId, null, notes, dropped, out _);
            foreach (string note in notes) Logger.Warning("[Prefab] " + note);

            var fx = new Fixups();
            var entities = new Entity[expanded.Count];
            var byId = new Dictionary<int, Entity>();   // the expansion's ids → entities, for the ones that are new
            for (int i = 0; i < expanded.Count; i++)
            {
                var x = expanded[i];
                if (world.TryGetEntityBySceneId(x.Id, out Entity e))
                {
                    entities[i] = e;
                    if (!x.Missing && x.C.ValueKind == JsonValueKind.Object) SetExactComponents(world, e, x.C, fx);
                    continue;
                }
                if (x.Missing) continue;
                // An entity the prefab gained: the one a memo holds for this place, or a new one.
                Entity rootOfNew = world.TryGetEntityBySceneId(x.RootId, out Entity r) ? r : default;
                var key = (rootOfNew, Prefab.PathKey(x.Path));
                e = default;
                if (memo != null && memo.Removed.Remove(key, out var held))
                {
                    if (world.Reattach(held)) e = held.Root;
                    else world.Discard(held);
                }
                if (e.IsNull)
                {
                    e = world.CreateEntity();
                    world.SetSceneId(e, x.Id);
                }
                entities[i] = e;
                byId[x.Id] = e;
                if (x.C.ValueKind == JsonValueKind.Object) SetExactComponents(world, e, x.C, fx);
            }

            Entity Resolve(int id) => byId.TryGetValue(id, out Entity e) ? e : world.TryGetEntityBySceneId(id, out e) ? e : Entity.Null;
            ApplyFixups(world, fx, Resolve, Resolve);

            for (int i = 0; i < expanded.Count; i++)
            {
                var x = expanded[i];
                if (x.Missing || x.RootId == 0 || entities[i].IsNull) continue;
                Entity rootEntity = Resolve(x.RootId);
                if (rootEntity.IsNull) continue;
                world.AddComponent(entities[i], new PrefabLink { Root = rootEntity, Asset = x.Asset, Path = x.Path, Extra = x.Extra });
            }

            // Entities their prefab no longer has. Each leaves alone: what is under it goes to the instance's root first
            // (the prefab's other entities have their new parents already).
            // From the last in scene order to the first: each is then taken out with the ones before it still in place,
            // and a memo puts them back, first to last, exactly where they were.
            var leavingOrder = new List<(int Index, int Id)>();
            foreach (int droppedId in dropped.Keys)
                if (world.TryGetEntityBySceneId(droppedId, out Entity d)) leavingOrder.Add((world.Entities.IndexOf(d), droppedId));
            leavingOrder.Sort((a, b) => b.Index.CompareTo(a.Index));
            foreach (var (_, droppedId) in leavingOrder)
            {
                if (!world.TryGetEntityBySceneId(droppedId, out Entity gone)) continue;
                Entity rootEntity = Resolve(dropped[droppedId]);
                string pathKey = world.TryGetComponent<PrefabLink>(gone, out var goneLink) ? Prefab.PathKey(goneLink.Path) : "";
                var leaving = new List<Entity>(world.GetChildren(gone)) { gone };   // itself too: it comes back under the root, whatever its parent was
                foreach (Entity child in leaving)
                {
                    if (!world.TryGetComponent<Transform>(child, out var tr)) continue;
                    tr.Parent = rootEntity.IsNull || rootEntity.Equals(child) ? null : rootEntity;
                    world.AddComponent(child, tr);
                }
                if (memo != null && !rootEntity.IsNull && world.Detach(gone) is { } detached) memo.Removed[(rootEntity, pathKey)] = detached;
                else world.DestroyEntity(gone);
            }
        }

        // ── Writing a prefab file from entities ─────────────────────────────────────────

        private sealed class PrefabFileItem
        {
            public Entity E;
            public int Kind;                 // 0 = an entity of the file itself, 1 = the root of a nested instance, 2 = an entity of one
            public int Id;                   // its id in the file; 0 until given
            public string Group = "";        // nested instance it belongs to
            public int[] Inner = [];         // its address inside the nested prefab
            public PrefabEntity? Existing;   // apply: the entity of the prefab it stands for
        }

        private sealed class PrefabFileGroup
        {
            public string Asset = "";
            public Prefab? Prefab;
            public int RootId;
            public readonly Dictionary<int, int> Map = new();   // nested prefab entity id → id in the file
        }

        /// <summary>
        /// The text of a prefab file made from an entity and everything under it.
        /// Not <paramref name="apply"/>: a new prefab — the entities get new ids; an instance of another prefab among
        /// them is written as a nested instance (and when <paramref name="root"/> itself is one, the new prefab is a
        /// variant of that prefab).
        /// <paramref name="apply"/>: <paramref name="root"/> is the root of an instance, and the text is its prefab's
        /// file with the instance's values: the prefab's entities keep their ids, what the scene added under the
        /// instance becomes part of the prefab, and the prefab root keeps its own name, position and rotation.
        /// With <paramref name="onlyEntity"/> and <paramref name="onlyComponent"/>, only that component of that
        /// entity is taken from the instance and everything else stays as the prefab has it.
        /// <paramref name="links"/>: for each entity, the link it has as part of an instance of the written prefab
        /// (<paramref name="key"/> = the key the file will have). Returns null with <paramref name="error"/> when
        /// the entities cannot be a prefab.
        /// </summary>
        public static string? WritePrefabFile(Entity root, string name, string key, bool apply,
                                              out Dictionary<Entity, PrefabLink> links, out string? error,
                                              Entity onlyEntity = default, string? onlyComponent = null)
        {
            links = new Dictionary<Entity, PrefabLink>();
            error = null;
            var world = ECSWorld.Instance;
            if (root.IsNull || !root.IsAlive || !world.Entities.Contains(root)) { error = "no such entity"; return null; }

            Prefab? target = null;
            if (apply)
            {
                if (!world.TryGetComponent<PrefabLink>(root, out var rootLink) || rootLink.Asset == null || rootLink.Missing)
                { error = "the entity is not the root of a prefab instance"; return null; }
                key = rootLink.Asset;
                target = PrefabLibrary.Get(key);
                if (target == null) { error = PrefabLibrary.ErrorOf(key) ?? $"prefab '{key}' could not be loaded"; return null; }
            }
            bool only = apply && onlyComponent != null;

            // The entities: the root first, the rest in scene order.
            var inSubtree = new HashSet<Entity> { root };
            var stack = new Stack<Entity>();
            stack.Push(root);
            while (stack.Count > 0)
                foreach (Entity child in world.GetChildren(stack.Pop()))
                    if (inSubtree.Add(child)) stack.Push(child);
            var order = new List<Entity> { root };
            foreach (Entity e in world.Entities)
                if (inSubtree.Contains(e) && !e.Equals(root)) order.Add(e);

            var items = new List<PrefabFileItem>(order.Count);
            var groups = new Dictionary<string, PrefabFileGroup>(StringComparer.Ordinal);
            foreach (Entity e in order)
            {
                var item = new PrefabFileItem { E = e };
                if (world.TryGetComponent<PrefabLink>(e, out var link))
                {
                    if (link.Missing)
                    { error = $"'{NameOf(world, e)}' belongs to an instance of a prefab that is not there"; return null; }
                    if (apply && link.Root.Equals(root))
                    {
                        // Part of the instance being applied: it stands for an entity of the prefab's own file.
                        item.Existing = target!.ByPath(link.Path);
                        if (item.Existing != null) PlaceInPrefab(item, target, groups);
                    }
                    else if (inSubtree.Contains(link.Root))
                    {
                        // An instance of another prefab among the entities: a nested instance of the file.
                        item.Group = "e" + world.GetSceneId(link.Root);
                        if (link.Root.Equals(e))
                        {
                            item.Kind = 1;
                            groups[item.Group] = new PrefabFileGroup { Asset = link.Asset ?? "" };
                        }
                        else
                        {
                            item.Kind = 2;
                            item.Inner = link.Path ?? [];
                        }
                    }
                    else
                    {
                        error = $"'{NameOf(world, e)}' is part of a prefab instance whose root is not among the entities: " +
                                "make the prefab from the instance's root, or unpack the instance first";
                        return null;
                    }
                }
                if (only && item.Existing == null) continue;   // only what the prefab has already
                items.Add(item);
            }
            if (apply)
            {
                // Entities of the prefab no entity of the instance stands for stay in the file as they are.
                var covered = new HashSet<int>();
                foreach (var item in items) if (item.Existing != null) covered.Add(item.Existing.Id);
                foreach (var pe in target!.Entities)
                {
                    if (covered.Contains(pe.Id)) continue;
                    var item = new PrefabFileItem { Existing = pe };
                    PlaceInPrefab(item, target, groups);
                    items.Add(item);
                }
            }

            // Ids: the prefab's own for what it has, new ones for the rest.
            int nextId = apply ? target!.NextId : 1;
            var sceneToFile = new Dictionary<int, int>();
            foreach (var item in items)
            {
                if (item.Id == 0) item.Id = nextId++;
                if (!item.E.IsNull) sceneToFile[world.GetSceneId(item.E)] = item.Id;
            }
            foreach (var item in items)
            {
                if (item.Kind == 0) continue;
                if (!groups.TryGetValue(item.Group, out var group)) { item.Kind = 0; continue; }   // its nested root is not written
                if (group.Prefab == null)
                {
                    group.Prefab = PrefabLibrary.Get(group.Asset);
                    if (group.Prefab == null) { error = PrefabLibrary.ErrorOf(group.Asset) ?? $"prefab '{group.Asset}' could not be loaded"; return null; }
                    if (group.Asset == key || group.Prefab.Dependencies.Contains(key))
                    { error = $"prefab '{key}' would contain itself (through '{group.Asset}')"; return null; }
                }
                if (item.Kind == 1) { group.RootId = item.Id; group.Map[group.Prefab.Root.Id] = item.Id; }
                else if (group.Prefab.ByPath(item.Inner) is { } inner) group.Map[inner.Id] = item.Id;
            }

            var sb = new StringBuilder("{\"name\":\"").Append(Esc(name)).Append("\",\"entities\":[");
            bool first = true;
            foreach (var item in items)
            {
                bool isRoot = item.E.Equals(root);
                JsonElement values;
                if (only || item.E.IsNull)
                {
                    // The prefab's own values, with one component of one entity from the instance.
                    values = item.Existing!.Components;
                    if (only && item.E.Equals(onlyEntity))
                    {
                        JsonElement current = MergeComponents(FullComponents(world, item.E), sceneToFile, default, default, isRoot);
                        using var removedDoc = JsonDocument.Parse("[\"" + Esc(onlyComponent!) + "\"]");
                        values = MergeComponents(values, null, default, removedDoc.RootElement, isRoot: false);
                        if (current.TryGetProperty(onlyComponent!, out var block))
                        {
                            using var overDoc = JsonDocument.Parse("{\"" + Esc(onlyComponent!) + "\":" + block.GetRawText() + "}");
                            values = MergeComponents(values, null, overDoc.RootElement, default, isRoot: false);
                        }
                    }
                }
                else
                {
                    values = MergeComponents(FullComponents(world, item.E), sceneToFile, default, default, isRoot);
                    if (apply && isRoot) values = WithRootOwnValues(values, target!.Root.Components);
                }

                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(item.Id);
                var linkOut = new PrefabLink { Root = root, Asset = isRoot ? key : null };
                if (item.Kind == 0)
                {
                    sb.Append(",\"c\":").Append(values.GetRawText()).Append('}');
                    linkOut.Path = isRoot ? [] : [item.Id];
                }
                else
                {
                    var group = groups[item.Group];
                    PrefabEntity? source = item.Kind == 1 ? group.Prefab!.Root : group.Prefab!.ByPath(item.Inner);
                    if (item.Kind == 1)
                    {
                        sb.Append(",\"src\":{\"prefab\":\"").Append(Esc(group.Asset)).Append("\"}");
                        linkOut.Path = isRoot ? [] : [item.Id];
                    }
                    else
                    {
                        sb.Append(",\"src\":{\"in\":").Append(group.RootId).Append(",\"e\":[");
                        for (int i = 0; i < item.Inner.Length; i++)
                        {
                            if (i > 0) sb.Append(',');
                            sb.Append(item.Inner[i]);
                        }
                        sb.Append("]}");
                        linkOut.Path = new int[item.Inner.Length + 1];
                        linkOut.Path[0] = group.RootId;
                        item.Inner.CopyTo(linkOut.Path, 1);
                    }
                    if (source == null) sb.Append(",\"c\":").Append(values.GetRawText());
                    else WriteOverrides(sb, MergeComponents(source.Components, group.Map, default, default, item.Kind == 1), values, item.Kind == 1);
                    sb.Append('}');
                }
                if (!item.E.IsNull) links[item.E] = linkOut;
            }
            sb.Append("],\"nextId\":").Append(nextId);
            sb.Append(apply ? target!.OtherRootMembers : ",\"prefab\":1");
            sb.Append('}');
            return Assets.AssetRefs.Stamp(sb.ToString());   // with the id of each file it names
        }

        // Where an entity of an existing prefab goes in its file: the file's own entity, the root of a nested instance, or an entity of one.
        private static void PlaceInPrefab(PrefabFileItem item, Prefab target, Dictionary<string, PrefabFileGroup> groups)
        {
            PrefabEntity existing = item.Existing!;
            item.Id = existing.Id;
            if (existing.Path.Length <= 1)
            {
                if (existing.NestedAsset == null) return;
                item.Kind = 1;
                item.Group = "p" + item.Id;
                groups[item.Group] = new PrefabFileGroup { Asset = existing.NestedAsset, RootId = item.Id };
            }
            else if (target.ById(existing.Path[0]) is { } nestedRoot)
            {
                item.Kind = 2;
                item.Group = "p" + nestedRoot.Id;
                item.Inner = existing.Path[1..];
            }
        }

        // `values` with the name, position and rotation of `own` (the prefab root's): applying an instance does not move or rename the prefab.
        private static JsonElement WithRootOwnValues(JsonElement values, JsonElement own)
        {
            var over = new StringBuilder("{");
            if (own.TryGetProperty("NameTag", out var name)) over.Append("\"NameTag\":").Append(name.GetRawText());
            if (own.TryGetProperty("Transform", out var t) && t.ValueKind == JsonValueKind.Object)
            {
                if (over.Length > 1) over.Append(',');
                over.Append("\"Transform\":{");
                bool any = false;
                foreach (string f in new[] { "P", "E", "Q" })
                {
                    if (!t.TryGetProperty(f, out var v)) continue;
                    if (any) over.Append(',');
                    any = true;
                    over.Append('"').Append(f).Append("\":").Append(v.GetRawText());
                }
                over.Append('}');
            }
            over.Append('}');
            using var doc = JsonDocument.Parse(over.ToString());
            return MergeComponents(values, null, doc.RootElement, default, isRoot: true);
        }

        private static string NameOf(ECSWorld world, Entity e)
            => world.TryGetComponent<NameTag>(e, out var nt) && !string.IsNullOrEmpty(nt.Name) ? nt.Name : "Entity";
    }
}
