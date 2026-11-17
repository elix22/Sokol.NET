// GameTypesReflection.cs — a game's [GameComponent] structs and [GameSystem] classes registered by reflection: for the
// editor (JIT, the project's DLL loaded while it runs) and a game run without ahead-of-time compilation. A game compiled
// ahead of time (NativeAOT: phones, a published desktop app) never comes here: the builder's generated code registers
// them (RegisteredScripts.g.cs) - no reflection there (GAME_COMPONENTS_SYSTEMS_DESIGN.md §5).
#if !__ANDROID__ && !__IOS__
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;

namespace GameEditor.Framework.Scripting
{
    public static class GameTypesReflection
    {
        /// <summary>Registers every [GameComponent] and [GameSystem] of an assembly; how many of each.</summary>
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only for the editor and runs that are not compiled ahead of time.")]
        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "Only for the editor and runs that are not compiled ahead of time.")]
        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Only for the editor and runs that are not compiled ahead of time.")]
        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Only for the editor and runs that are not compiled ahead of time.")]
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Only for the editor and runs that are not compiled ahead of time.")]
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only for the editor and runs that are not compiled ahead of time.")]
        public static (int Components, int Systems) Register(Assembly assembly)
        {
            int components = 0, systems = 0;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = Array.FindAll(e.Types, t => t != null)!; }
            foreach (var type in types)
            {
                if (type.IsValueType && type.GetCustomAttribute<GameComponentAttribute>() is { } c)
                {
                    try
                    {
                        typeof(GameTypesReflection).GetMethod(nameof(RegisterComponent), BindingFlags.NonPublic | BindingFlags.Static)!
                            .MakeGenericMethod(type).Invoke(null, new object[] { c.Label });
                        components++;
                    }
                    catch (Exception ex) { Logger.Error($"[GameComponents] {type.Name} is not registered: {(ex.InnerException ?? ex).Message}"); }
                }
                else if (!type.IsAbstract && typeof(IGameSystem).IsAssignableFrom(type) && type.GetCustomAttribute<GameSystemAttribute>() is { } s)
                {
                    if (type.GetConstructor(Type.EmptyTypes) == null) { Logger.Error($"[GameSystems] {type.Name} has no public constructor without parameters."); continue; }
                    GameSystems.Register((IGameSystem)Activator.CreateInstance(type)!, s.Order);
                    systems++;
                }
            }
            return (components, systems);
        }

        // One [GameComponent] struct: its block written and read field by field (each public field not marked [Runtime]).
        private static void RegisterComponent<T>(string label) where T : struct
        {
            var fields = Saved(typeof(T));
            GameComponents.Register(typeof(T).Name, label, typeof(T),
                static e => ECSWorld.Instance.HasComponent<T>(e),
                static e => ECSWorld.Instance.AddComponent(e, Activator.CreateInstance<T>()),   // its field initialisers run
                static e => ECSWorld.Instance.RemoveComponent<T>(e),
                static e => ECSWorld.Instance.TryGetComponent<T>(e, out var v) ? v : null,
                (w, e) =>
                {
                    if (!ECSWorld.Instance.TryGetComponent<T>(e, out var value)) return false;
                    object boxed = value;
                    foreach (var f in fields) Write(w, f.Name, f.FieldType, f.GetValue(boxed));
                    return true;
                },
                (r, e) =>
                {
                    var world = ECSWorld.Instance;
                    object boxed = world.TryGetComponent<T>(e, out var had) ? had : Activator.CreateInstance<T>();
                    foreach (var f in fields) f.SetValue(boxed, Read(r, f.Name, f.FieldType, f.GetValue(boxed)));
                    world.AddComponent(e, (T)boxed);
                });
        }

        /// <summary>The fields a game component or [GameData] struct saves: public, not static, not [Runtime].</summary>
        public static FieldInfo[] Saved(Type t)
            => Array.FindAll(t.GetFields(BindingFlags.Public | BindingFlags.Instance), f => f.GetCustomAttribute<RuntimeAttribute>() == null && !f.IsInitOnly);

        private static Type? ElementOf(Type t)
            => t.IsArray ? t.GetElementType() : t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) ? t.GetGenericArguments()[0] : null;

        private static void Write(GameDataWriter w, string name, Type t, object? v)
        {
            if (t == typeof(int)) w.Int(name, (int)v!);
            else if (t == typeof(float)) w.Float(name, (float)v!);
            else if (t == typeof(double)) w.Double(name, (double)v!);
            else if (t == typeof(bool)) w.Bool(name, (bool)v!);
            else if (t == typeof(string)) w.String(name, (string?)v);
            else if (t.IsEnum) w.String(name, v?.ToString());
            else if (t == typeof(Vector2)) w.Vector2(name, (Vector2)v!);
            else if (t == typeof(Vector3)) w.Vector3(name, (Vector3)v!);
            else if (t == typeof(Vector4)) w.Vector4(name, (Vector4)v!);
            else if (t == typeof(Quaternion)) w.Quaternion(name, (Quaternion)v!);
            else if (t == typeof(Entity)) w.Entity(name, (Entity)v!);
            else if (ElementOf(t) is { } e)
            {
                var items = new List<object?>();
                if (v is IEnumerable list) foreach (var x in list) items.Add(x);
                w.List<object?>(name, items, (iw, x) => WriteItem(iw, e, x));
            }
        }

        private static void WriteItem(GameDataWriter w, Type t, object? v)
        {
            if (t == typeof(int)) w.Item((int)v!);
            else if (t == typeof(float)) w.Item((float)v!);
            else if (t == typeof(bool)) w.Item((bool)v!);
            else if (t == typeof(string)) w.Item((string?)v);
            else if (t.IsEnum) w.Item(v?.ToString());
            else if (t == typeof(Vector2)) w.Item((Vector2)v!);
            else if (t == typeof(Vector3)) w.Item((Vector3)v!);
            else if (t == typeof(Vector4)) w.Item((Vector4)v!);
            else if (t.IsValueType && t.GetCustomAttribute<GameDataAttribute>() != null)
                foreach (var f in Saved(t)) Write(w, f.Name, f.FieldType, f.GetValue(v));
        }

        private static object? Read(GameDataReader r, string name, Type t, object? had)
        {
            if (t == typeof(int)) return r.Int(name, (int)had!);
            if (t == typeof(float)) return r.Float(name, (float)had!);
            if (t == typeof(double)) return r.Double(name, (double)had!);
            if (t == typeof(bool)) return r.Bool(name, (bool)had!);
            if (t == typeof(string)) return r.String(name, (string?)had ?? "");
            if (t.IsEnum)
            {
                string s = r.String(name, "");
                return s.Length > 0 && Enum.TryParse(t, s, true, out var parsed) ? parsed : had;
            }
            if (t == typeof(Vector2)) return r.Vector2(name, (Vector2)had!);
            if (t == typeof(Vector3)) return r.Vector3(name, (Vector3)had!);
            if (t == typeof(Vector4)) return r.Vector4(name, (Vector4)had!);
            if (t == typeof(Quaternion)) return r.Quaternion(name, (Quaternion)had!);
            if (t == typeof(Entity)) return r.Entity(name, (Entity)had!);
            if (ElementOf(t) is { } e)
            {
                var items = r.List(name, ir => ReadItem(ir, e));
                if (items == null) return had;
                if (t.IsArray)
                {
                    var a = Array.CreateInstance(e, items.Count);
                    for (int i = 0; i < items.Count; i++) a.SetValue(items[i], i);
                    return a;
                }
                var l = (IList)Activator.CreateInstance(t)!;
                foreach (var x in items) l.Add(x);
                return l;
            }
            return had;
        }

        private static object? ReadItem(GameDataReader r, Type t)
        {
            if (t == typeof(int)) return r.ItemInt();
            if (t == typeof(float)) return r.ItemFloat();
            if (t == typeof(bool)) return r.ItemBool();
            if (t == typeof(string)) return r.ItemString();
            if (t.IsEnum) return Enum.TryParse(t, r.ItemString(), true, out var e) ? e : Activator.CreateInstance(t);
            if (t == typeof(Vector2)) return r.ItemVector2();
            if (t == typeof(Vector3)) return r.ItemVector3();
            if (t == typeof(Vector4)) return r.ItemVector4();
            if (t.IsValueType && t.GetCustomAttribute<GameDataAttribute>() != null)
            {
                object boxed = Activator.CreateInstance(t)!;
                foreach (var f in Saved(t)) f.SetValue(boxed, Read(r, f.Name, f.FieldType, f.GetValue(boxed)));
                return boxed;
            }
            return null;
        }
    }
}
#endif
