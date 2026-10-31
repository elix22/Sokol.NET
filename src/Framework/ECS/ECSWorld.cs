using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Frent;
using Frent.Core;
using Frent.Systems;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.ECS
{
    public sealed class ECSWorld : IDisposable
    {
        private World _world;
        private readonly List<Entity> _entities = new();

        // Parent→children index: maps a parent entity to its DIRECT children (entities whose
        // Transform.Parent == it). Kept in sync inside AddComponent<Transform> — the single write
        // path every Parent change passes through — so DestroyEntity can walk a subtree in
        // O(subtree) instead of rescanning the whole world per node. Empty lists are not retained.
        private readonly Dictionary<Entity, List<Entity>> _children = new();

        // Persistent scene ids: the id an entity has in the scene file. A loaded entity keeps the id it was
        // saved with, so every reference to it — built-in ones and opaque extension blocks alike — stays valid
        // through any number of load/save rounds. Ids are never reused within a scene (NextSceneId is saved
        // with it), so a reference to a deleted entity stays unresolvable instead of hitting a newcomer.
        private readonly Dictionary<Entity, int> _sceneIds = new();
        private readonly Dictionary<int, Entity> _bySceneId = new();
        private int _nextSceneId = 1;

        public static ECSWorld Instance { get; private set; } = new ECSWorld();

        private ECSWorld() { _world = new World(); }

        public Entity CreateEntity(string name = "Entity")
        {
            var e = _world.Create(
                new NameTag { Name = name },
                new ActiveFlag { Active = true },
                Transform.Default);
            _entities.Add(e);
            EventBus.RaiseEntityCreated(e);
            return e;
        }

        /// <summary>The entity's persistent scene id (see <see cref="SetSceneId"/>); a new entity gets the next free one.</summary>
        public int GetSceneId(Entity e)
        {
            if (_sceneIds.TryGetValue(e, out int id)) return id;
            id = _nextSceneId++;
            _sceneIds[e] = id;
            _bySceneId[id] = e;
            return id;
        }

        /// <summary>
        /// Gives a loaded entity the id it was saved with. An id that is not positive or already held by another
        /// entity (a malformed file) is ignored and the entity gets a fresh id on first use; returns false then.
        /// </summary>
        public bool SetSceneId(Entity e, int id)
        {
            if (id <= 0 || _bySceneId.ContainsKey(id)) return false;
            if (_sceneIds.Remove(e, out int old)) _bySceneId.Remove(old);
            _sceneIds[e] = id;
            _bySceneId[id] = e;
            if (id >= _nextSceneId) _nextSceneId = id + 1;
            return true;
        }

        /// <summary>The live entity holding scene id <paramref name="id"/>, if any.</summary>
        public bool TryGetEntityBySceneId(int id, out Entity e) => _bySceneId.TryGetValue(id, out e) && e.IsAlive;

        /// <summary>The id the next new entity gets. Saved with the scene; loading only ever raises it.</summary>
        public int NextSceneId
        {
            get => _nextSceneId;
            set { if (value > _nextSceneId) _nextSceneId = value; }
        }

        public void DestroyEntity(Entity e)
        {
            if (!e.IsAlive) return;   // already gone (e.g. deleted as another entity's descendant)

            // Detach the deleted root from its (surviving) parent's child list.
            if (TryGetComponent<Transform>(e, out var rootTr) && rootTr.Parent.HasValue)
                RemoveChildLink(rootTr.Parent.Value, e);

            // Cascade to descendants — children of a deleted parent are deleted too (standard
            // scene-graph semantics), and each gets an EntityDestroyed event so the renderer forgets
            // its GPU state. Walks the parent→children index, so it is O(subtree) — not a per-node
            // scan of the whole world. `deleting` doubles as the visited set (guards cycles/dupes) and
            // the membership test for the single batched _entities removal below.
            var toDelete = new List<Entity> { e };
            var deleting = new HashSet<Entity> { e };
            for (int i = 0; i < toDelete.Count; i++)
            {
                if (!_children.TryGetValue(toDelete[i], out var kids)) continue;
                foreach (var c in kids)
                {
                    if (deleting.Contains(c)) continue;   // already queued (also breaks any parent cycle)
                    // Trust the index but verify the live link, so a (hypothetical) stale entry can
                    // never delete a non-child — at worst it is skipped. Appended → its own children
                    // are visited in a later iteration of this same loop.
                    if (c.IsAlive && TryGetComponent<Transform>(c, out var ct)
                        && ct.Parent.HasValue && ct.Parent.Value.Equals(toDelete[i]))
                    {
                        deleting.Add(c);
                        toDelete.Add(c);
                    }
                }
            }

            foreach (var d in toDelete)
            {
                _children.Remove(d);   // its children (if any) are also in the delete set
                if (_sceneIds.Remove(d, out int sid)) _bySceneId.Remove(sid);   // the id itself is not reused
                EventBus.RaiseEntityDestroyed(d);
                if (d.IsAlive) d.Delete();
            }
            // One batched O(n) pass — a per-entity List.Remove is O(n) each, i.e. O(subtree·n) for a
            // big subtree (the very stutter this index is meant to avoid).
            _entities.RemoveAll(deleting.Contains);
        }

        // ── Parent→children index maintenance ────────────────────────────────────────────────
        private void AddChildLink(Entity parent, Entity child)
        {
            if (!_children.TryGetValue(parent, out var list))
                _children[parent] = list = new List<Entity>();
            // No Contains() guard: a child has exactly one parent, and AddComponent removes it from
            // its old parent's list before adding it to the new one (and no-ops when unchanged), so a
            // child is never double-listed under one parent. Keeping this O(1) is what makes building
            // a wide node (thousands of direct children) linear instead of quadratic.
            list.Add(child);
        }

        private void RemoveChildLink(Entity parent, Entity child)
        {
            if (_children.TryGetValue(parent, out var list))
            {
                list.Remove(child);
                if (list.Count == 0) _children.Remove(parent);
            }
        }

        /// <summary>Direct children of <paramref name="e"/> (entities whose Transform.Parent == e).
        /// O(1) lookup backed by the maintained index; empty if none.</summary>
        public IReadOnlyList<Entity> GetChildren(Entity e)
            => _children.TryGetValue(e, out var list) ? list : System.Array.Empty<Entity>();

        /// <summary>Rebuilds the parent→children index from scratch (O(entities)). Only needed after a
        /// BULK structural change that wrote Transforms through the raw Frent API instead of
        /// <see cref="AddComponent{T}"/> — currently just entity duplication, which the per-write index
        /// maintenance in AddComponent can't observe. The create / glTF-import / deserialize / reparent
        /// paths all go through AddComponent and keep the index current incrementally; they must NOT
        /// call this (it would be wasted work).</summary>
        public void RebuildHierarchyIndex()
        {
            _children.Clear();
            foreach (var e in _entities)
                if (e.IsAlive && TryGetComponent<Transform>(e, out var tr) && tr.Parent.HasValue)
                    AddChildLink(tr.Parent.Value, e);
        }

        // Return the concrete List<Entity> to avoid IReadOnlyList<T> generic interface
        // dispatch, which causes a "function signature mismatch" crash in WASM NativeAOT.
        public List<Entity> Entities => _entities;
        
        // ── Name lookup with ALC bridge ───────────────────────────────────────
        // When the game DLL runs in an isolated AssemblyLoadContext its ECSWorld
        // singleton has an empty _entities list.  GameAssemblyRunner calls
        // RegisterFindByNameCallback so lookups are forwarded to the host world.

        private static Func<string, Entity?>? _findByNameBridge;

        public static void RegisterFindByNameCallback(Func<string, Entity?> fn)
            => _findByNameBridge = fn;

        /// <summary>
        /// Returns the first entity whose <see cref="NameTag.Name"/> equals
        /// <paramref name="name"/>, or <see langword="null"/> if not found.
        /// </summary>
        public Entity? FindEntityByName(string name)
        {
            if (_findByNameBridge != null) return _findByNameBridge(name);
            foreach (var e in _entities)
                if (TryGetComponent<NameTag>(e, out var tag) && tag.Name == name)
                    return e;
            return null;
        }

        /// <summary>
        /// Replaces the internal entity ordering with <paramref name="orderedEntities"/>.
        /// Entities not present in the input are appended in their existing relative order.
        /// </summary>
        public void SetEntityOrder(IEnumerable<Entity> orderedEntities)
        {
            var seen = new HashSet<Entity>();
            var next = new List<Entity>(_entities.Count);

            foreach (var e in orderedEntities)
            {
                if (!e.IsAlive) continue;
                if (!_entities.Contains(e)) continue;
                if (seen.Add(e)) next.Add(e);
            }

            // Keep any entities we didn't receive (defensive fallback).
            foreach (var e in _entities)
                if (seen.Add(e)) next.Add(e);

            _entities.Clear();
            _entities.AddRange(next);
        }

        public void AddComponent<T>(Entity e, T component) where T : struct
        {
            bool present = e.Has<T>();

            // Keep the parent→children index in sync whenever a Transform's Parent changes. EVERY
            // Parent write in the codebase — reparent (Hierarchy/EditorState), glTF import, scene
            // deserialize, play-mode ALC sync — lands here as a whole-Transform overwrite, so this one
            // chokepoint maintains the index with no cooperation from the mutation sites. The
            // typeof(T)==typeof(Transform) test is a JIT constant per generic instantiation (entirely
            // elided for other component types); Unsafe.As reinterprets without boxing so the per-frame
            // ALC write-back stays zero-alloc, and a no-op when the Parent is unchanged.
            if (typeof(T) == typeof(Transform))
            {
                Entity? newParent = Unsafe.As<T, Transform>(ref component).Parent;
                Entity? oldParent = present ? Unsafe.As<T, Transform>(ref e.Get<T>()).Parent : null;
                bool sameParent = (oldParent.HasValue && newParent.HasValue)
                                  ? oldParent.Value.Equals(newParent.Value)
                                  : oldParent.HasValue == newParent.HasValue;
                if (!sameParent)
                {
                    if (oldParent.HasValue) RemoveChildLink(oldParent.Value, e);
                    if (newParent.HasValue) AddChildLink(newParent.Value, e);
                }
            }

            if (present)
                e.Get<T>() = component;
            else
                e.Add(component);
        }

        public bool HasComponent<T>(Entity e) where T : struct
            => e.Has<T>();

        public ref T GetComponent<T>(Entity e) where T : struct
            => ref e.Get<T>();

        public bool TryGetComponent<T>(Entity e, out T component) where T : struct
        {
            if (e.TryGet<T>(out var r))
            {
                component = r.Value;
                return true;
            }
            component = default;
            return false;
        }

        /// <summary>
        /// Zero-copy component access for hot paths: returns a writable <see cref="Ref{T}"/>
        /// into archetype storage in a single lookup. Unlike <see cref="TryGetComponent{T}"/>
        /// it does not copy the struct out, and unlike <see cref="AddComponent{T}"/> it needs
        /// no copy-back — mutate through <c>component.Value</c> directly.
        /// Valid only while no structural change (add/remove/create/delete) occurs.
        /// </summary>
        public bool TryGetComponentRef<T>(Entity e, out Ref<T> component) where T : struct
            => e.TryGet<T>(out component);

        /// <summary>
        /// Returns a cached Frent <see cref="Frent.Systems.Query"/> over entities that have both
        /// <typeparamref name="T1"/> and <typeparamref name="T2"/>. The query is built once per
        /// component-set and reused; its <c>Enumerate</c>/<c>EnumerateWithEntities</c> enumerators
        /// are <see langword="ref"/> structs, so iterating allocates nothing. Prefer this over
        /// scanning <see cref="Entities"/> + per-entity <see cref="TryGetComponent{T}"/> on
        /// per-frame paths.
        /// </summary>
        public Query Query<T1, T2>() => _world.Query<T1, T2>();

        /// <inheritdoc cref="Query{T1,T2}()"/>
        public Query Query<T1, T2, T3>() => _world.Query<T1, T2, T3>();

        public void RemoveComponent<T>(Entity e) where T : struct
        {
            if (e.Has<T>()) e.Remove<T>();
        }

        public IEnumerable<T> GetAllComponents<T>() where T : struct
        {
            var result = new List<T>();
            foreach (var chunk in _world.Query<T>().EnumerateChunks<T>())
                foreach (ref var c in chunk.Span)
                    result.Add(c);
            return result;
        }

        /// <summary>
        /// Returns <see langword="true"/> if at least one entity currently carries
        /// component <typeparamref name="T"/>.  Uses the Frent chunk enumerator
        /// (a <see langword="ref"/> struct — zero allocation) for an O(archetypes) check.
        /// </summary>
        public bool HasAnyComponent<T>() where T : struct
        {
            foreach (var chunk in _world.Query<T>().EnumerateChunks<T>())
                if (chunk.Span.Length > 0) return true;
            return false;
        }

        public void Clear()
        {
            var copy = new List<Entity>(_entities);
            foreach (var e in copy)
            {
                EventBus.RaiseEntityDestroyed(e);
                if (e.IsAlive) e.Delete();
            }
            _entities.Clear();
            _children.Clear();
            _sceneIds.Clear();
            _bySceneId.Clear();
            _nextSceneId = 1;
            _world.Dispose();
            _world = new World();
        }

        public void Dispose() => _world.Dispose();
    }
}
