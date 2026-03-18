using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.ECS
{
    public sealed class ECSWorld
    {
        private int _nextId = 1;
        private readonly List<int> _entities = new();
        private readonly Dictionary<Type, object> _stores = new();

        public static ECSWorld Instance { get; private set; } = new ECSWorld();

        /// <summary>
        /// Replaces the global singleton. Called by the GameEditor's GameAssemblyRunner
        /// to ensure the dynamically-loaded game assembly shares the editor's ECS world
        /// instead of its own statically-initialised copy.
        /// </summary>
        public static void SetInstance(ECSWorld world)
        {
            Instance = world;
        }

        private ECSWorld() { }

        public int CreateEntity(string name = "Entity")
        {
            int id = _nextId++;
            _entities.Add(id);
            AddComponent(id, new NameTag { Name = name });
            AddComponent(id, new ActiveFlag { Active = true });
            AddComponent(id, Transform.Default);
            EventBus.RaiseEntityCreated(id);
            return id;
        }

        public void DestroyEntity(int id)
        {
            _entities.Remove(id);
            foreach (var store in _stores.Values)
            {
                if (store is Dictionary<int, object> dict)
                    dict.Remove(id);
            }
            EventBus.RaiseEntityDestroyed(id);
        }

        public IReadOnlyList<int> Entities => _entities;

        public void AddComponent<T>(int id, T component) where T : struct
        {
            GetOrCreateStore<T>()[id] = component;
        }

        public bool HasComponent<T>(int id) where T : struct
        {
            return GetOrCreateStore<T>().ContainsKey(id);
        }

        public ref T GetComponent<T>(int id) where T : struct
        {
            var store = GetOrCreateStore<T>();
            ref var val = ref CollectionsMarshal.GetValueRefOrNullRef(store, id);
            if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref val))
                throw new KeyNotFoundException($"Entity {id} has no component {typeof(T).Name}");
            return ref val;
        }

        public bool TryGetComponent<T>(int id, out T component) where T : struct
        {
            var store = GetOrCreateStore<T>();
            ref var val = ref CollectionsMarshal.GetValueRefOrNullRef(store, id);
            if (System.Runtime.CompilerServices.Unsafe.IsNullRef(ref val))
            {
                component = default;
                return false;
            }
            component = val;
            return true;
        }

        public void RemoveComponent<T>(int id) where T : struct
        {
            GetOrCreateStore<T>().Remove(id);
        }

        public Dictionary<int, T> GetAllComponents<T>() where T : struct
            => GetOrCreateStore<T>();

        private Dictionary<int, T> GetOrCreateStore<T>() where T : struct
        {
            if (!_stores.TryGetValue(typeof(T), out var raw))
            {
                raw = new Dictionary<int, T>();
                _stores[typeof(T)] = raw;
            }
            return (Dictionary<int, T>)raw;
        }

        public void Clear()
        {
            var ids = new List<int>(_entities);
            foreach (var id in ids)
                DestroyEntity(id);
            _stores.Clear();
            _nextId = 1;
        }
    }
}
