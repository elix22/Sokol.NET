using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using Frent;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Physics;
using GameEditor.Framework.Scene;
using static Sokol.SLog;
using Input = GameEditor.Framework.Input;

namespace GameEditor.Framework.Scripting
{
    /// <summary>
    /// Base class for all game scripts (analogous to Unity's MonoBehaviour).
    /// Subclasses are discovered by the ScriptSystem and run during play mode.
    ///
    /// Attach to entities via a <see cref="ScriptComponent"/> whose TypeName matches
    /// the subclass name.
    /// </summary>
    public abstract class GameBehaviour
    {
        /// <summary>The ECS entity this behaviour is attached to.</summary>
        public Entity EntityId { get; internal set; }

        // ── ECS convenience ─────────────────────────────────────────────────

        /// <summary>Access to the global ECS world.</summary>
        protected ECSWorld World => ECSWorld.Instance;

        /// <summary>Gets the entity's Transform component by reference.</summary>
        protected ref Transform Transform => ref World.GetComponent<Transform>(EntityId);

        /// <summary>Try to get a component; returns false if not present.</summary>
        protected bool TryGetComponent<T>(out T component) where T : struct
            => World.TryGetComponent<T>(EntityId, out component);

        /// <summary>Gets a component by reference; throws if absent.</summary>
        protected ref T GetComponent<T>() where T : struct
            => ref World.GetComponent<T>(EntityId);

        /// <summary>Adds or replaces a component on this entity.</summary>
        protected void SetComponent<T>(T component) where T : struct
            => World.AddComponent(EntityId, component);

        /// <summary>Returns true when this entity has the given component type.</summary>
        protected bool HasComponent<T>() where T : struct
            => World.HasComponent<T>(EntityId);

        // ── Lifecycle ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by the editor before <see cref="OnStart"/> to apply serialized
        /// public field values from the scene file. Override in the editor proxy.
        /// </summary>
        public virtual void ApplySerializedProperties(Dictionary<string, string> properties)
        {
            foreach (var kvp in properties)
            {
                var fi = this.GetType().GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                if (fi == null) continue;
                try
                {
                    object? value = ParseFieldValue(fi.FieldType, kvp.Value);
                    if (value != null) fi.SetValue(this, value);
                }
                catch (Exception ex)
                {
                       Error($"[ScriptSystem] Failed to create '{this.GetType().Name}': {ex.Message}");
                }
            }
        }


        public static object? ParseFieldValue(Type t, string s)
        {
            // Scalar numerics
            if (t == typeof(float))
                return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : null;
            if (t == typeof(double))
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : null;
            if (t == typeof(int))
                return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;
            if (t == typeof(uint))
                return uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint ui) ? ui : null;
            if (t == typeof(long))
                return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : null;
            if (t == typeof(ulong))
                return ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong ul) ? ul : null;
            if (t == typeof(short))
                return short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out short sh) ? sh : null;
            if (t == typeof(byte))
                return byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b) ? b : null;

            // Other primitives
            if (t == typeof(bool))
                return s is "true" or "True" or "1";
            if (t == typeof(string))
                return s;
            if (t == typeof(char))
                return s.Length > 0 ? s[0] : null;

            // Numeric vectors (format: [x, y, ...])
            if (t == typeof(Vector2))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 2 &&
                    float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    return new Vector2(x, y);
            }
            if (t == typeof(Vector3))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 3 &&
                    float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    return new Vector3(x, y, z);
            }
            if (t == typeof(Vector4))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 4 &&
                    float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z) &&
                    float.TryParse(p[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float w))
                    return new Vector4(x, y, z, w);
            }
            if (t == typeof(Quaternion))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 4 &&
                    float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float z) &&
                    float.TryParse(p[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float w))
                    return new Quaternion(x, y, z, w);
            }

            // Matrix types — row-major, elements listed left-to-right, top-to-bottom
            if (t == typeof(Matrix3x2))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 6 &&
                    float.TryParse(p[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m11) &&
                    float.TryParse(p[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m12) &&
                    float.TryParse(p[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m21) &&
                    float.TryParse(p[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m22) &&
                    float.TryParse(p[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m31) &&
                    float.TryParse(p[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m32))
                    return new Matrix3x2(m11, m12, m21, m22, m31, m32);
            }
            if (t == typeof(Matrix4x4))
            {
                var p = s.Trim('[', ']').Split(',');
                if (p.Length == 16 &&
                    float.TryParse(p[ 0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m11) &&
                    float.TryParse(p[ 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m12) &&
                    float.TryParse(p[ 2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m13) &&
                    float.TryParse(p[ 3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m14) &&
                    float.TryParse(p[ 4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m21) &&
                    float.TryParse(p[ 5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m22) &&
                    float.TryParse(p[ 6].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m23) &&
                    float.TryParse(p[ 7].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m24) &&
                    float.TryParse(p[ 8].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m31) &&
                    float.TryParse(p[ 9].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m32) &&
                    float.TryParse(p[10].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m33) &&
                    float.TryParse(p[11].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m34) &&
                    float.TryParse(p[12].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m41) &&
                    float.TryParse(p[13].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m42) &&
                    float.TryParse(p[14].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m43) &&
                    float.TryParse(p[15].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float m44))
                    return new Matrix4x4(m11, m12, m13, m14, m21, m22, m23, m24,
                                        m31, m32, m33, m34, m41, m42, m43, m44);
            }

            // A prefab, by its key (its path below Assets): "Prefabs/Bullet.prefab"
            if (t == typeof(Prefab))
                return s.Length > 0 ? PrefabLibrary.Get(s) : null;

            // Enum
            if (t.IsEnum)
                return Enum.TryParse(t, s, ignoreCase: true, out object? ev) ? ev : null;

            return null;
        }
        
        /// <summary>Called once when play mode starts. Use for initialization.</summary>
        public virtual void OnStart() { }

        /// <summary>Called every frame while in play mode.</summary>
        /// <param name="deltaTime">Seconds elapsed since the previous frame.</param>
        public virtual void OnUpdate(float deltaTime) { }

        /// <summary>Called when play mode stops or the entity is destroyed.</summary>
        public virtual void OnDestroy() { }

        /// <summary>Called when this entity begins overlapping a non-trigger body.</summary>
        public virtual void OnCollisionEnter(Entity other) { }

        /// <summary>Called when this entity stops overlapping a non-trigger body.</summary>
        public virtual void OnCollisionExit(Entity other) { }

        /// <summary>Called every fixed step while this entity is overlapping a non-trigger body.</summary>
        public virtual void OnCollisionStay(Entity other) { }

        /// <summary>Called when this entity enters a trigger volume.</summary>
        public virtual void OnTriggerEnter(Entity other) { }

        /// <summary>Called when this entity exits a trigger volume.</summary>
        public virtual void OnTriggerExit(Entity other) { }

        // ── Prefabs: entities that come and go while the game runs ───────────

        /// <summary>
        /// Places an instance of a prefab in the scene — its entities with their bodies and scripts — and returns
        /// its root. <paramref name="position"/> and <paramref name="rotation"/> are the root's: in the world, or
        /// under <paramref name="parent"/> when one is given. The instance's scripts start (OnStart) before their
        /// first OnUpdate, in the next frame, so it can be set up here first.
        /// A prefab comes from a public <see cref="Prefab"/> field (the Inspector shows a picker for it), or from
        /// <see cref="Prefabs.Load"/>. Returns <see cref="Entity.Null"/> when there is none.
        /// </summary>
        protected Entity Instantiate(Prefab? prefab, Vector3 position, Quaternion rotation, Entity? parent = null)
            => SceneManager.Instantiate(prefab, position, rotation, parent);

        /// <summary>
        /// Takes an entity out of the scene at the end of this frame, with everything under it: OnDestroy runs on
        /// its scripts and its bodies leave the physics world. <c>Destroy(EntityId)</c> removes this script's own entity.
        /// </summary>
        protected void Destroy(Entity entity) => SceneManager.Destroy(entity);

        // ── Physics helpers (Unity/Godot-style convenience) ────────────────

        protected bool TryGetLinearVelocity(out Vector3 velocity)
            => SceneManager.TryGetLinearVelocity(EntityId, out velocity);

        protected bool SetLinearVelocity(Vector3 velocity)
            => SceneManager.SetLinearVelocity(EntityId, velocity);

        protected bool TryGetAngularVelocity(out Vector3 velocity)
            => SceneManager.TryGetAngularVelocity(EntityId, out velocity);

        protected bool SetAngularVelocity(Vector3 velocity)
            => SceneManager.SetAngularVelocity(EntityId, velocity);

        protected bool AddForce(Vector3 force)
            => SceneManager.AddForce(EntityId, force);

        protected bool AddImpulse(Vector3 impulse)
            => SceneManager.AddImpulse(EntityId, impulse);

        protected bool AddTorque(Vector3 torque)
            => SceneManager.AddTorque(EntityId, torque);

        protected bool TeleportTo(Vector3 position, Quaternion rotation)
            => SceneManager.TeleportBody(EntityId, position, rotation);

        // ── 2D queries (the Box2D world; the calls above act on a 2D body too) ──────────

        /// <summary>The first 2D body a ray meets, from <paramref name="origin"/> along <paramref name="direction"/> for <paramref name="distance"/> units. Triggers are not met.</summary>
        protected bool Raycast2D(Vector2 origin, Vector2 direction, float distance, out Entity hitEntity, out Physics2D.RaycastHit2D hit)
            => Physics2D.Physics2DWorld.Raycast(origin, direction, distance, out hitEntity, out hit);

        /// <summary>The entities whose 2D body holds a point. Returns how many.</summary>
        protected int OverlapPoint2D(Vector2 point, List<Entity> results) => Physics2D.Physics2DWorld.OverlapPoint(point, results);

        /// <summary>The entities whose 2D body a circle reaches.</summary>
        protected int OverlapCircle2D(Vector2 center, float radius, List<Entity> results) => Physics2D.Physics2DWorld.OverlapCircle(center, radius, results);

        /// <summary>The entities whose 2D body a box reaches: its middle, its width and height, its turn in degrees.</summary>
        protected int OverlapBox2D(Vector2 center, Vector2 size, float angle, List<Entity> results) => Physics2D.Physics2DWorld.OverlapBox(center, size, angle, results);

        protected bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Entity hitEntity, out RaycastHit hit)
            => SceneManager.Raycast(origin, direction, maxDistance, out hitEntity, out hit);

        protected int OverlapSphere(Vector3 center, float radius, List<Entity> results, int maxResults = 64)
            => SceneManager.OverlapSphere(center, radius, results, maxResults);

        protected int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, List<Entity> results, int maxResults = 64)
            => SceneManager.OverlapBox(center, halfExtents, rotation, results, maxResults);

        // ---- Character controller helpers ----------------------------------

        protected bool IsGrounded()
            => SceneManager.IsCharacterGrounded(EntityId);

        protected void MoveCharacter(Vector3 velocity)
            => SceneManager.MoveCharacter(EntityId, velocity);

        protected bool TryGetCharacterGroundNormal(out Vector3 normal)
            => SceneManager.TryGetCharacterGroundNormal(EntityId, out normal);

        // ---- Vehicle helpers ------------------------------------------------

        protected bool SetVehicleInput(float steer, float throttle, float brake, float handBrake = 0f)
            => SceneManager.SetVehicleInput(EntityId, steer, throttle, brake, handBrake);

        protected bool IsWheelOnGround(int wheelIndex)
            => SceneManager.IsWheelOnGround(EntityId, wheelIndex);

        protected float GetWheelRotationSpeed(int wheelIndex)
            => SceneManager.GetWheelRotationSpeed(EntityId, wheelIndex);

        protected Matrix4x4 GetWheelWorldTransform(int wheelIndex)
            => SceneManager.GetWheelWorldTransform(EntityId, wheelIndex);

        // ── Input helpers ────────────────────────────────────────────────────

        /// <summary>True while the key is held.</summary>
        protected static bool GetKey(Input.Key key)               => Input.InputManager.GetKey(key);
        /// <summary>True only during the first frame the key is pressed.</summary>
        protected static bool GetKeyDown(Input.Key key)           => Input.InputManager.GetKeyDown(key);
        /// <summary>True only during the frame the key is released.</summary>
        protected static bool GetKeyUp(Input.Key key)             => Input.InputManager.GetKeyUp(key);

        /// <summary>True while the mouse button is held (0=left, 1=right, 2=middle).</summary>
        protected static bool GetMouseButton(int button)          => Input.InputManager.GetMouseButton(button);
        /// <summary>True during the first frame the mouse button is pressed.</summary>
        protected static bool GetMouseButtonDown(int button)      => Input.InputManager.GetMouseButtonDown(button);
        /// <summary>True during the frame the mouse button is released.</summary>
        protected static bool GetMouseButtonUp(int button)        => Input.InputManager.GetMouseButtonUp(button);

        /// <summary>Value of a named axis in [-1,1]. Built-ins: "Horizontal", "Vertical", "Jump", "Mouse X", etc.</summary>
        protected static float GetAxis(string name)               => Input.InputManager.GetAxis(name);
        /// <summary>True while the named button is held.</summary>
        protected static bool GetButton(string name)              => Input.InputManager.GetButton(name);
        /// <summary>True only during the first frame the named button is pressed.</summary>
        protected static bool GetButtonDown(string name)          => Input.InputManager.GetButtonDown(name);
        /// <summary>True only during the frame the named button is released.</summary>
        protected static bool GetButtonUp(string name)            => Input.InputManager.GetButtonUp(name);

        /// <summary>Number of currently active touch points.</summary>
        protected static int TouchCount                           => Input.InputManager.TouchCount;
        /// <summary>Returns the touch at the given index (0 ≤ index &lt; TouchCount).</summary>
        protected static Input.Touch GetTouch(int index)          => Input.InputManager.GetTouch(index);

        /// <summary>Current mouse position in screen pixels.</summary>
        protected static Vector2 MousePosition                    => Input.InputManager.MousePosition;
        /// <summary>Mouse movement delta accumulated this frame.</summary>
        protected static Vector2 MouseDelta                       => Input.InputManager.MouseDelta;
        /// <summary>Vertical scroll delta this frame.</summary>
        protected static float   ScrollDelta                      => Input.InputManager.ScrollDelta;

        // ── Logging helpers ──────────────────────────────────────────────────
        // These route through the framework Logger (Default ALC) so the message
        // reaches the GameEditor console panel in-editor and stdout when standalone,
        // regardless of which AssemblyLoadContext the script was loaded into.

        /// <summary>Logs an info-level message to the GameEditor console and stdout.</summary>
        protected void Log(string msg)        => Logger.Info(msg);
        /// <summary>Logs a warning-level message to the GameEditor console and stdout.</summary>
        protected void LogWarning(string msg) => Logger.Warning(msg);
        /// <summary>Logs an error-level message to the GameEditor console and stdout.</summary>
        protected void LogError(string msg)   => Logger.Error(msg);
    }
}

