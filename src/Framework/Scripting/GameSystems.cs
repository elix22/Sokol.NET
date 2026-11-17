// GameSystems.cs — a game's own components and systems (examples/GameEditor/docs/GAME_COMPONENTS_SYSTEMS_DESIGN.md).
//
//     [GameComponent("Traffic Car")] public struct TrafficCar { public int Edge; public float Along; [Runtime] public float Speed; }
//     [GameSystem(Order = 100)] public sealed class TrafficSystem : GameSystem { public override void FixedStep(GameWorld w, float dt) { … } }
//
// They live in the game (its Source/ or a plugin's Runtime/), never in the engine: the engine only calls what is
// registered. A built game registers them by code the builder writes (Source/Generated/RegisteredScripts.g.cs: no
// reflection, NativeAOT); the editor registers them from the project's DLL when it loads it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;

namespace GameEditor.Framework.Scripting
{
    /// <summary>A game's component: a public struct whose public fields are saved in scenes and shown in the Inspector.</summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class GameComponentAttribute : Attribute
    {
        /// <summary>Its name in the editor (Add Component, the Inspector); the struct's name when empty.</summary>
        public string Label { get; }
        public GameComponentAttribute(string label = "") { Label = label; }
    }

    /// <summary>A plain struct a game component holds arrays or lists of (a graph's edges): its public fields are saved.</summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class GameDataAttribute : Attribute { }

    /// <summary>A game component's field that is not saved: what it does now (shown greyed while the game plays).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RuntimeAttribute : Attribute { }

    /// <summary>A game's system: one instance while the game plays, called by the engine at fixed places.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GameSystemAttribute : Attribute
    {
        /// <summary>Systems run in this order (then by name).</summary>
        public int Order { get; set; }
    }

    /// <summary>What a game system is called for while the game plays.</summary>
    public interface IGameSystem
    {
        /// <summary>Play begins (the scene loaded, its scripts started), or a new scene was entered.</summary>
        void Start(GameWorld world);
        /// <summary>Before each fixed physics step, with its length (after the engine's own karts and race).</summary>
        void FixedStep(GameWorld world, float dt);
        /// <summary>Each frame, after the scripts' OnUpdate.</summary>
        void Update(GameWorld world, float dt);
        /// <summary>Play ends, or the scene is left: forget what was kept.</summary>
        void Stop();
    }

    /// <summary>A game system with nothing to do at each place until it says so.</summary>
    public abstract class GameSystem : IGameSystem
    {
        public virtual void Start(GameWorld world) { }
        public virtual void FixedStep(GameWorld world, float dt) { }
        public virtual void Update(GameWorld world, float dt) { }
        public virtual void Stop() { }
    }

    /// <summary>What a game system works with: the entities and their components, the 3D physics, the main camera.</summary>
    public sealed class GameWorld
    {
        public static readonly GameWorld Instance = new();
        private GameWorld() { }

        /// <summary>The entities and their components (queries: <c>Ecs.Query&lt;A, B&gt;()</c>).</summary>
        public ECSWorld Ecs => ECSWorld.Instance;
        /// <summary>The 3D physics of the scene that plays (null without one).</summary>
        public IPhysicsWorld? Physics => SceneManager.Physics3D;
        /// <summary>The body an entity has in the 3D physics.</summary>
        public bool TryGetBody(Entity entity, out PhysicsBodyHandle body) => SceneManager.TryGetBody(entity, out body);
        /// <summary>Puts an entity's body (and its Transform) at a place, at once.</summary>
        public bool TeleportBody(Entity entity, Vector3 position, Quaternion rotation) => SceneManager.TeleportBody(entity, position, rotation);
        /// <summary>Puts a character at a place, at once.</summary>
        public bool TeleportCharacter(Entity entity, Vector3 position) => SceneManager.TeleportCharacter(entity, position);
        /// <summary>Places a character at once, held or free as it was (a scripted move steps a held character along).</summary>
        public bool PlaceCharacter(Entity entity, Vector3 position) => SceneManager.PlaceCharacter(entity, position);
        /// <summary>Holds a character (it neither moves by itself nor falls) or lets it go.</summary>
        public bool SetCharacterHeld(Entity entity, bool held) => SceneManager.SetCharacterHeld(entity, held);

        /// <summary>The main camera: where it is and which way it looks (its +Z).</summary>
        public bool TryMainCamera(out Vector3 position, out Vector3 forward)
        {
            position = forward = default;
            var world = ECSWorld.Instance;
            if (!world.HasAnyComponent<CameraComponent>()) return false;
            foreach (var row in world.Query<CameraComponent, Transform>().Enumerate<CameraComponent, Transform>())
            {
                if (!row.Item1.Value.IsMain) continue;
                position = row.Item2.Value.Position;
                forward = Vector3.Transform(Vector3.UnitZ, row.Item2.Value.Rotation);
                return true;
            }
            return false;
        }
    }

    /// <summary>The game systems registered: the engine calls them while the game plays.</summary>
    public static class GameSystems
    {
        private sealed class Entry { public IGameSystem System = null!; public int Order; public string Name = ""; public bool Off; }
        private static readonly List<Entry> _all = new();
        private static bool _running;

        public static int Count => _all.Count;

        /// <summary>Adds a system (the builder's generated code, or the editor from the project's DLL).</summary>
        public static void Register(IGameSystem system, int order = 0)
        {
            string name = system.GetType().Name;
            _all.RemoveAll(e => e.Name == name);           // registered again (the editor's next build): the newer one
            _all.Add(new Entry { System = system, Order = order, Name = name });
            _all.Sort(static (a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : string.CompareOrdinal(a.Name, b.Name));
        }

        /// <summary>Forgets every system (the editor, before it loads the project's DLL again).</summary>
        public static void Clear()
        {
            if (_running) StopAll();
            _all.Clear();
        }

        /// <summary>The systems' names in their order.</summary>
        public static IEnumerable<string> Names { get { foreach (var e in _all) yield return e.Name; } }

        internal static void StartAll()
        {
            _running = true;
            foreach (var e in _all) { e.Off = false; Call(e, "Start", static s => s.Start(GameWorld.Instance)); }
        }

        internal static void FixedStepAll(float dt)
        {
            if (!_running) return;
            foreach (var e in _all) if (!e.Off) Call(e, "FixedStep", s => s.FixedStep(GameWorld.Instance, dt));
        }

        internal static void UpdateAll(float dt)
        {
            if (!_running) return;
            foreach (var e in _all) if (!e.Off) Call(e, "Update", s => s.Update(GameWorld.Instance, dt));
        }

        internal static void StopAll()
        {
            if (!_running) return;
            _running = false;
            foreach (var e in _all) Call(e, "Stop", static s => s.Stop());
        }

        // A system that throws: said once, then left out for the rest of this play (one game's bug stops nothing else).
        private static void Call(Entry e, string what, Action<IGameSystem> call)
        {
            try { call(e.System); }
            catch (Exception ex)
            {
                e.Off = true;
                Logger.Error($"[GameSystems] {e.Name}.{what} failed - it is off until play starts again: {ex.Message}");
            }
        }
    }

    /// <summary>A game component type as the editor lists it (Add Component, the Inspector, the automation).</summary>
    public sealed class GameComponentType
    {
        public string Key = "", Label = "";
        public Type Type = null!;
        public Func<Entity, bool> Has = null!;
        public Action<Entity> AddDefault = null!;
        public Action<Entity> Remove = null!;
        /// <summary>The entity's component, boxed (null: it has none) - for the editor's tools, which do not know the type.</summary>
        public Func<Entity, object?> GetBoxed = null!;
    }

    /// <summary>The game components registered: saved in scenes under their key, listed for the editor.</summary>
    public static class GameComponents
    {
        /// <summary>Writes the entity's component into the writer; false when it has none.</summary>
        public delegate bool Writer(GameDataWriter w, Entity e);
        /// <summary>Reads the entity's component from the reader - over the one it has, else over a new one - and gives it.</summary>
        public delegate void Reader(GameDataReader r, Entity e);

        private static readonly Dictionary<string, GameComponentType> _types = new(StringComparer.Ordinal);

        /// <summary>The game's types were registered by the builder's generated code (a game compiled ahead of time): nothing
        /// is searched for by reflection.</summary>
        public static bool Generated { get; set; }

        public static IReadOnlyCollection<GameComponentType> All => _types.Values;

        public static GameComponentType? ByKey(string key) => _types.TryGetValue(key, out var t) ? t : null;

        /// <summary>
        /// Registers a game component under <paramref name="key"/>: its scene block (<paramref name="write"/> /
        /// <paramref name="read"/>) and its entry for the editor. Every delegate is plain (not generic): the generated code
        /// writes them in the game's own assembly, where the ECS calls over the game's struct are ordinary code - a web build,
        /// compiled ahead of time, could not call generic closures over it ("function signature mismatch").
        /// </summary>
        public static void Register(string key, string label, Type type, Func<Entity, bool> has, Action<Entity> addDefault,
                                    Action<Entity> remove, Func<Entity, object?> getBoxed, Writer write, Reader read)
        {
            SceneSerializer.RegisterComponentExtension(key,
                (world, e) =>
                {
                    var w = new GameDataWriter(world);
                    return write(w, e) ? w.ToString() : null;
                },
                (world, e, el, resolve) => read(new GameDataReader(el, resolve), e),
                (world, e) => remove(e));
            _types[key] = new GameComponentType
            {
                Key = key, Label = string.IsNullOrEmpty(label) ? key : label, Type = type,
                Has = has, AddDefault = addDefault, Remove = remove, GetBoxed = getBoxed,
            };
        }

        /// <summary>Forgets every game component (the editor, before it loads the project's DLL again): their scene blocks
        /// are kept as they are from then on (<see cref="PreservedComponents"/>) until registered again.</summary>
        public static void Clear()
        {
            foreach (var key in _types.Keys) SceneSerializer.UnregisterComponentExtension(key);
            _types.Clear();
        }
    }

    /// <summary>Writes one game component's block: a JSON object, one member per saved field.</summary>
    public sealed class GameDataWriter
    {
        private readonly StringBuilder _sb = new("{");
        private readonly ECSWorld _world;
        private bool _first = true;

        public GameDataWriter(ECSWorld world) { _world = world; }

        private StringBuilder Name(string name)
        {
            if (!_first) _sb.Append(',');
            _first = false;
            return Str(_sb, name).Append(':');
        }

        private static StringBuilder Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            return sb.Append('"');
        }

        private static string N(double v) => double.IsFinite(v) ? v.ToString("R", CultureInfo.InvariantCulture) : "0";

        public void Int(string name, int v) => Name(name).Append(v.ToString(CultureInfo.InvariantCulture));
        public void Float(string name, float v) => Name(name).Append(N(v));
        public void Double(string name, double v) => Name(name).Append(N(v));
        public void Bool(string name, bool v) => Name(name).Append(v ? "true" : "false");
        public void String(string name, string? v) => Str(Name(name), v ?? "");
        public void Enum<TEnum>(string name, TEnum v) where TEnum : struct, System.Enum => Str(Name(name), v.ToString());
        public void Vector2(string name, Vector2 v) => Name(name).Append('[').Append(N(v.X)).Append(',').Append(N(v.Y)).Append(']');
        public void Vector3(string name, Vector3 v) => Name(name).Append('[').Append(N(v.X)).Append(',').Append(N(v.Y)).Append(',').Append(N(v.Z)).Append(']');
        public void Vector4(string name, Vector4 v) => Name(name).Append('[').Append(N(v.X)).Append(',').Append(N(v.Y)).Append(',').Append(N(v.Z)).Append(',').Append(N(v.W)).Append(']');
        public void Quaternion(string name, Quaternion v) => Name(name).Append('[').Append(N(v.X)).Append(',').Append(N(v.Y)).Append(',').Append(N(v.Z)).Append(',').Append(N(v.W)).Append(']');
        /// <summary>An entity reference: its scene id (0: none).</summary>
        public void Entity(string name, Entity v) => Name(name).Append(v.IsNull || !v.IsAlive ? 0 : _world.GetSceneId(v));

        /// <summary>A list: each element written by <paramref name="each"/> into its own writer's array slot.</summary>
        public void List<TItem>(string name, IReadOnlyList<TItem>? items, Action<GameDataWriter, TItem> each)
        {
            Name(name).Append('[');
            if (items != null)
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) _sb.Append(',');
                    var w = new GameDataWriter(_world) { _single = true };
                    each(w, items[i]);
                    _sb.Append(w.Value());
                }
            _sb.Append(']');
        }

        // An element of a list: one value (a number, a vector …) or one object (a [GameData] struct).
        private bool _single;
        public void Item(int v) => _item = v.ToString(CultureInfo.InvariantCulture);
        public void Item(float v) => _item = N(v);
        public void Item(bool v) => _item = v ? "true" : "false";
        public void Item(string? v) => _item = Str(new StringBuilder(), v ?? "").ToString();
        public void Item(Vector2 v) => _item = "[" + N(v.X) + "," + N(v.Y) + "]";
        public void Item(Vector3 v) => _item = "[" + N(v.X) + "," + N(v.Y) + "," + N(v.Z) + "]";
        public void Item(Vector4 v) => _item = "[" + N(v.X) + "," + N(v.Y) + "," + N(v.Z) + "," + N(v.W) + "]";
        private string? _item;
        private string Value() => _item ?? ToString();

        public override string ToString() => _single && _item != null ? _item : _sb.ToString() + "}";
    }

    /// <summary>Reads one game component's block: each saved field by its name, the value it had when missing. A class (not a
    /// struct): a struct passed by value through the generated code's delegates broke the web's ahead-of-time build
    /// ("function signature mismatch").</summary>
    public sealed class GameDataReader
    {
        private readonly JsonElement _el;
        private readonly Func<int, Entity>? _resolve;

        public GameDataReader(JsonElement el, Func<int, Entity>? resolve) { _el = el; _resolve = resolve; }

        public JsonElement Element => _el;

        private bool Get(string name, out JsonElement v)
        {
            v = default;
            return _el.ValueKind == JsonValueKind.Object && _el.TryGetProperty(name, out v);
        }

        public int Int(string name, int fallback) => Get(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? i : fallback;
        public float Float(string name, float fallback) => Get(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : fallback;
        public double Double(string name, double fallback) => Get(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
        public bool Bool(string name, bool fallback) => Get(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;
        public string String(string name, string fallback) => Get(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : fallback;
        public TEnum Enum<TEnum>(string name, TEnum fallback) where TEnum : struct, System.Enum
            => Get(name, out var v) && v.ValueKind == JsonValueKind.String && System.Enum.TryParse(v.GetString(), true, out TEnum e) ? e : fallback;
        public Vector2 Vector2(string name, Vector2 fallback) => Get(name, out var v) && Floats(v, 2, out var f) ? new Vector2(f[0], f[1]) : fallback;
        public Vector3 Vector3(string name, Vector3 fallback) => Get(name, out var v) && Floats(v, 3, out var f) ? new Vector3(f[0], f[1], f[2]) : fallback;
        public Vector4 Vector4(string name, Vector4 fallback) => Get(name, out var v) && Floats(v, 4, out var f) ? new Vector4(f[0], f[1], f[2], f[3]) : fallback;
        public Quaternion Quaternion(string name, Quaternion fallback) => Get(name, out var v) && Floats(v, 4, out var f) ? new Quaternion(f[0], f[1], f[2], f[3]) : fallback;
        public Entity Entity(string name, Entity fallback)
        {
            if (!Get(name, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int id)) return fallback;
            if (id == 0) return default;
            return _resolve != null ? _resolve(id) : ECSWorld.Instance.TryGetEntityBySceneId(id, out var e) ? e : default;
        }

        /// <summary>A list: each element read by <paramref name="each"/> from its own reader; null when the field is missing.</summary>
        public List<TItem>? List<TItem>(string name, Func<GameDataReader, TItem> each)
        {
            if (!Get(name, out var v) || v.ValueKind != JsonValueKind.Array) return null;
            var list = new List<TItem>(v.GetArrayLength());
            foreach (var item in v.EnumerateArray()) list.Add(each(new GameDataReader(item, _resolve)));
            return list;
        }

        // An element of a list.
        public int ItemInt() => _el.ValueKind == JsonValueKind.Number && _el.TryGetInt32(out int i) ? i : 0;
        public float ItemFloat() => _el.ValueKind == JsonValueKind.Number ? _el.GetSingle() : 0f;
        public bool ItemBool() => _el.ValueKind == JsonValueKind.True;
        public string ItemString() => _el.ValueKind == JsonValueKind.String ? _el.GetString() ?? "" : "";
        public Vector2 ItemVector2() => Floats(_el, 2, out var f) ? new Vector2(f[0], f[1]) : default;
        public Vector3 ItemVector3() => Floats(_el, 3, out var f) ? new Vector3(f[0], f[1], f[2]) : default;
        public Vector4 ItemVector4() => Floats(_el, 4, out var f) ? new Vector4(f[0], f[1], f[2], f[3]) : default;

        private static bool Floats(JsonElement v, int n, out float[] f)
        {
            f = Array.Empty<float>();
            if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() < n) return false;
            f = new float[n];
            int i = 0;
            foreach (var x in v.EnumerateArray())
            {
                if (i == n) break;
                if (x.ValueKind != JsonValueKind.Number) return false;
                f[i++] = x.GetSingle();
            }
            return true;
        }
    }
}
