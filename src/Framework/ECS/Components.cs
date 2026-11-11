using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.ECS;
using GameEditor.Framework.Physics;

namespace GameEditor.Framework.ECS.Components
{
    public struct Transform
    {
        public Vector3    Position;
        /// <summary>Canonical rotation storage. Use this in all hot paths (physics, rendering).</summary>
        public Quaternion Rotation;
        public Vector3    Scale;
        public Entity?    Parent;

        /// <summary>Euler angles in degrees (YXZ order). Computed from <see cref="Rotation"/>.
        /// Prefer <see cref="Rotation"/> in hot paths to avoid decomposition overhead.</summary>
        public Vector3 EulerAngles
        {
            get => QuaternionToEuler(Rotation);
            set => Rotation = Quaternion.CreateFromYawPitchRoll(
                value.Y * MathF.PI / 180f,
                value.X * MathF.PI / 180f,
                value.Z * MathF.PI / 180f);
        }

        public static Transform Default => new Transform
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale    = Vector3.One,
            Parent   = null
        };

        public Matrix4x4 LocalMatrix =>
            Matrix4x4.CreateScale(Scale) *
            Matrix4x4.CreateFromQuaternion(Rotation) *
            Matrix4x4.CreateTranslation(Position);

        // Walks the parent chain to compute the world-space matrix.
        public static Matrix4x4 GetWorldMatrix(ECSWorld world, in Transform transform, int depth = 0)
        {
            Matrix4x4 local = transform.LocalMatrix;
            if (!transform.Parent.HasValue || depth > 32) return local;
            Entity parent = transform.Parent.Value;
            if (!parent.IsAlive || !world.TryGetComponent<Transform>(parent, out var parentTransform))
                return local;
            return local * GetWorldMatrix(world, parentTransform, depth + 1);
        }

        // Converts a quaternion to Euler angles (degrees) matching CreateFromYawPitchRoll(Y,X,Z).
        public static Vector3 QuaternionToEuler(Quaternion q)
        {
            // Extract rotation matrix entries for M = Ry * Rx * Rz (row-vector convention)
            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(q);
            // atan2, not asin(-M32): asin turns float rounding near ±1 into ~0.03° (-90 read back as -89.98).
            float cosPitch = MathF.Sqrt(m.M12 * m.M12 + m.M22 * m.M22);
            float pitch = MathF.Atan2(-m.M32, cosPitch);
            float yaw, roll;
            if (cosPitch > 1e-5f)   // only true gimbal lock merges yaw and roll (a 0.9999 threshold dropped roll within 0.8° of ±90°)
            {
                yaw  = MathF.Atan2(m.M31, m.M33);
                roll = MathF.Atan2(m.M12, m.M22);
            }
            else
            {
                yaw  = MathF.Atan2(-m.M13, m.M11);
                roll = 0f;
            }
            const float Rad2Deg = 180f / MathF.PI;
            return new Vector3(pitch * Rad2Deg, yaw * Rad2Deg, roll * Rad2Deg);
        }

        public Vector3 Forward
        {
            get
            {
                var rot = Matrix4x4.CreateFromQuaternion(Rotation);
                return new Vector3(rot.M31, rot.M32, rot.M33);
            }
        }
    }

    public struct NameTag
    {
        public string Name;
    }

    public struct ActiveFlag
    {
        public bool Active;
    }

    public struct MeshRenderer
    {
        public const string NoMaterialSentinel = "__none__";

        public string MeshPath;
        public string MaterialPath;   // "mtlFile#materialName" key, empty for sub-mesh defaults, "__none__" to force no material
        public bool Visible;
        public bool ReceivesShadows;
        public bool CastsShadows;
    }

    public struct CameraComponent
    {
        public float Fov;
        public float NearZ;
        public float FarZ;
        public bool IsMain;
        public bool IsOrthographic;
        public float OrthoSize;   // half-height in world units (ortho mode only)
        public bool Enabled;      // when false the camera does not render
    }

    public enum LightType { Directional, Point, Spot }

    public struct LightComponent
    {
        public LightType Type;
        public System.Numerics.Vector3 Color;
        public float Intensity;
        public float Range;
        public float InnerAngle;  // spot only — half-angle of inner cone, degrees (default 25)
        public float OuterAngle;  // spot only — half-angle of outer cone, degrees (default 35)
        public bool CastsShadows;
        public bool Enabled;      // when false the light does not contribute to scene lighting
    }

    public struct RigidbodyComponent
    {
        public RigidbodyMotionType MotionType;
        public float Mass;
        public bool UseGravity;
        public float Friction;
        public float Restitution;
        public float LinearDamping;
        public float AngularDamping;
        /// <summary>All collision shapes on this body. Always has at least one entry at play time.</summary>
        public List<ShapeEntry>? Shapes;
        public bool IsTrigger;
        public ushort Layer;
        public ushort LayerMask;

        /// <summary>Convenience accessor — type of the first (or only) shape entry.</summary>
        public readonly ColliderShape Shape => Shapes?.Count > 0 ? Shapes[0].Shape : ColliderShape.Box;

        public bool IsStatic
        {
            readonly get => MotionType == RigidbodyMotionType.Static;
            set => MotionType = value ? RigidbodyMotionType.Static : RigidbodyMotionType.Dynamic;
        }

        public bool IsKinematic
        {
            readonly get => MotionType == RigidbodyMotionType.Kinematic;
            set => MotionType = value ? RigidbodyMotionType.Kinematic : RigidbodyMotionType.Dynamic;
        }
    }

    /// <summary>Which Jolt character backend to use.</summary>
    public enum CharacterMode
    {
        /// <summary>JPH::CharacterVirtual — no physics body, moves via ExtendedUpdate each step.</summary>
        Virtual,
        /// <summary>JPH::Character — IS a kinematic physics body; gravity applied by the physics system.</summary>
        Kinematic,
    }

    public struct CharacterComponent
    {
        /// <summary>Virtual or Kinematic character controller backend.</summary>
        public CharacterMode Mode;
        /// <summary>Total height of the capsule (cylindrical portion + two hemispheres), in metres.</summary>
        public float Height;
        /// <summary>Capsule radius in metres.</summary>
        public float Radius;
        /// <summary>Additional offset applied to the capsule shape relative to the character's feet position (metres).
        /// The default automatic offset already centres the capsule so its base sits at Transform.Position;
        /// use this to nudge the shape further (e.g. +Y to raise the collision volume).</summary>
        public Vector3 ShapeOffset;
        /// <summary>Maximum climbable slope angle in radians (default π/4 = 45°).</summary>
        public float MaxSlopeAngle;
        /// <summary>Maximum push force against other bodies, in Newtons (Virtual mode only).</summary>
        public float MaxStrength;
        /// <summary>Character mass in kg.</summary>
        public float Mass;
        /// <summary>Friction coefficient (Kinematic mode; default 0.5).</summary>
        public float Friction;
        /// <summary>Gravity scale multiplier (Kinematic mode; default 1.0).</summary>
        public float GravityFactor;
        /// <summary>Maximum separation distance used in PostSimulation (Kinematic mode; default 0.05).</summary>
        public float CollisionTolerance;
        /// <summary>Physics layer this character occupies (typically the Moving layer).</summary>
        public ushort Layer;
        /// <summary>Bitmask of layers this character collides with.</summary>
        public ushort LayerMask;

        public static CharacterComponent Default => new CharacterComponent
        {
            Mode               = CharacterMode.Virtual,
            Height             = 1.8f,
            Radius             = 0.35f,
            MaxSlopeAngle      = MathF.PI / 4f,
            MaxStrength        = 100f,
            Mass               = 70f,
            Friction           = 0.5f,
            GravityFactor      = 1.0f,
            CollisionTolerance = 0.05f,
            ShapeOffset        = Vector3.Zero,
            Layer              = 1,
            LayerMask          = 0xFFFF,
        };
    }

    public struct ScriptComponent
    {
        public string TypeName;
        /// <summary>Serialized public field values, keyed by field name. Null = no overrides.</summary>
        public Dictionary<string, string>? Properties;
    }

    public struct ScriptCollectionComponent
    {
        /// <summary>Additional scripts attached to the entity (beyond primary ScriptComponent).</summary>
        public List<ScriptComponent>? Scripts;
    }

    /// <summary>
    /// Constraint (joint) between two bodies. BodyA/BodyB reference sibling entities that
    /// carry <see cref="RigidbodyComponent"/>. At play-mode start SceneManager resolves them
    /// to <see cref="GameEditor.Framework.Physics.PhysicsBodyHandle"/> references and calls
    /// <see cref="GameEditor.Framework.Physics.IPhysicsWorld.CreateConstraint"/>.
    /// </summary>
    public struct ConstraintComponent
    {
        public GameEditor.Framework.Physics.ConstraintType Type;
        /// <summary>Entity with a RigidbodyComponent. Null entity = world anchor.</summary>
        public Frent.Entity BodyA;
        /// <summary>Entity with a RigidbodyComponent.</summary>
        public Frent.Entity BodyB;
        public System.Numerics.Vector3 LocalAnchorA;
        public System.Numerics.Vector3 LocalAnchorB;
        /// <summary>Primary axis in body-A space (hinge axis, slider axis, cone twist axis).</summary>
        public System.Numerics.Vector3 LocalAxisA;
        /// <summary>Primary axis in body-B space.</summary>
        public System.Numerics.Vector3 LocalAxisB;
        public float MinLimit;
        public float MaxLimit;

        /// <summary>Runtime-only: handle returned by CreateConstraint. Not serialized.</summary>
        public GameEditor.Framework.Physics.ConstraintHandle RuntimeHandle;
    }

    /// <summary>Vehicle controller type.</summary>
    public enum VehicleType { Wheeled, Motorcycle, Tracked }

    /// <summary>Per-wheel configuration stored inside <see cref="VehicleComponent"/>.</summary>
    public struct VehicleWheelSettings
    {
        /// <summary>Wheel centre position in chassis-local space (relative to body origin).</summary>
        public System.Numerics.Vector3 Position;
        public float Radius;
        public float Width;
        public float SuspMinLength;
        public float SuspMaxLength;
        public float SuspFrequency;
        public float SuspDamping;
        /// <summary>Maximum steering angle in radians. 0 = non-steering wheel.</summary>
        public float MaxSteerAngle;
        /// <summary>Maximum hand-brake torque in N·m. 0 = no hand brake on this wheel.</summary>
        public float MaxHandBrakeTorque;
        /// <summary>Whether this wheel is driven by the differential.</summary>
        public bool IsDriven;
    }

    /// <summary>
    /// Attaches a Jolt vehicle constraint to an entity. The entity must also carry a
    /// <see cref="Transform"/>; the chassis body is created automatically at play-mode start.
    /// Drive the vehicle each frame by calling <c>SetVehicleInput</c> from a
    /// <see cref="GameEditor.Framework.Scripting.GameBehaviour"/> script.
    /// </summary>
    public struct VehicleComponent
    {
        public VehicleType Type;
        /// <summary>Half-extents of the chassis box shape (metres).</summary>
        public System.Numerics.Vector3 ChassisHalfExtent;
        /// <summary>Chassis mass in kg.</summary>
        public float Mass;
        /// <summary>Y offset for the chassis centre-of-mass (negative lowers CoM for stability).</summary>
        public float COMOffsetY;
        /// <summary>Maximum engine torque in N·m.</summary>
        public float MaxEngineTorque;
        /// <summary>Clutch strength (affects gear-shift smoothness).</summary>
        public float ClutchStrength;
        /// <summary>Maximum allowed pitch/roll angle in radians (keeps vehicle from tipping too far).</summary>
        public float MaxRollAngle;
        public float Friction;
        public VehicleWheelSettings[] Wheels;
        public ushort Layer;
        public ushort LayerMask;

        /// <summary>Runtime-only: handle returned by CreateVehicle. Not serialized.</summary>
        public GameEditor.Framework.Physics.VehicleHandle RuntimeHandle;

        /// <summary>Default four-wheel car configuration.</summary>
        public static VehicleComponent DefaultCar => new VehicleComponent
        {
            Type              = VehicleType.Wheeled,
            ChassisHalfExtent = new System.Numerics.Vector3(0.9f, 0.2f, 2.0f),
            Mass              = 1500f,
            COMOffsetY        = -0.2f,
            MaxEngineTorque   = 500f,
            ClutchStrength    = 10f,
            MaxRollAngle      = 60f * MathF.PI / 180f,
            Friction          = 0.5f,
            Layer             = 1,
            LayerMask         = 0xFFFF,
            Wheels = new VehicleWheelSettings[]
            {
                // Front-left
                new VehicleWheelSettings { Position = new System.Numerics.Vector3(-0.9f, -0.3f,  1.4f), Radius = 0.3f, Width = 0.1f, SuspMinLength = 0.3f, SuspMaxLength = 0.5f, SuspFrequency = 1.5f, SuspDamping = 0.5f, MaxSteerAngle = 30f * MathF.PI / 180f, MaxHandBrakeTorque = 0f,   IsDriven = false },
                // Front-right
                new VehicleWheelSettings { Position = new System.Numerics.Vector3( 0.9f, -0.3f,  1.4f), Radius = 0.3f, Width = 0.1f, SuspMinLength = 0.3f, SuspMaxLength = 0.5f, SuspFrequency = 1.5f, SuspDamping = 0.5f, MaxSteerAngle = 30f * MathF.PI / 180f, MaxHandBrakeTorque = 0f,   IsDriven = false },
                // Rear-left
                new VehicleWheelSettings { Position = new System.Numerics.Vector3(-0.9f, -0.3f, -1.4f), Radius = 0.3f, Width = 0.1f, SuspMinLength = 0.3f, SuspMaxLength = 0.5f, SuspFrequency = 1.5f, SuspDamping = 0.5f, MaxSteerAngle = 0f,                      MaxHandBrakeTorque = 4000f, IsDriven = true  },
                // Rear-right
                new VehicleWheelSettings { Position = new System.Numerics.Vector3( 0.9f, -0.3f, -1.4f), Radius = 0.3f, Width = 0.1f, SuspMinLength = 0.3f, SuspMaxLength = 0.5f, SuspFrequency = 1.5f, SuspDamping = 0.5f, MaxSteerAngle = 0f,                      MaxHandBrakeTorque = 4000f, IsDriven = true  },
            }
        };

        /// <summary>Default two-wheel motorcycle configuration.</summary>
        public static VehicleComponent DefaultMotorcycle => new VehicleComponent
        {
            Type              = VehicleType.Motorcycle,
            ChassisHalfExtent = new System.Numerics.Vector3(0.2f, 0.3f, 0.8f),
            Mass              = 250f,
            COMOffsetY        = -0.3f,
            MaxEngineTorque   = 150f,
            ClutchStrength    = 10f,
            MaxRollAngle      = 60f * MathF.PI / 180f,
            Friction          = 0.5f,
            Layer             = 1,
            LayerMask         = 0xFFFF,
            Wheels = new VehicleWheelSettings[]
            {
                // Front
                new VehicleWheelSettings { Position = new System.Numerics.Vector3(0f, -0.3f,  0.75f), Radius = 0.3f, Width = 0.05f, SuspMinLength = 0.3f, SuspMaxLength = 0.5f, SuspFrequency = 1.5f, SuspDamping = 0.5f, MaxSteerAngle = 45f * MathF.PI / 180f, MaxHandBrakeTorque = 0f,   IsDriven = false },
                // Rear
                new VehicleWheelSettings { Position = new System.Numerics.Vector3(0f, -0.3f, -0.75f), Radius = 0.3f, Width = 0.05f, SuspMinLength = 0.3f, SuspMaxLength = 0.5f, SuspFrequency = 1.5f, SuspDamping = 0.5f, MaxSteerAngle = 0f,                      MaxHandBrakeTorque = 4000f, IsDriven = true  },
            }
        };

        /// <summary>Default tracked-vehicle (tank) configuration — 18 wheels, 9 per side.</summary>
        public static VehicleComponent DefaultTank => new VehicleComponent
        {
            Type              = VehicleType.Tracked,
            ChassisHalfExtent = new System.Numerics.Vector3(1.5f, 0.3f, 2.8f),
            Mass              = 4000f,
            COMOffsetY        = -0.3f,
            MaxEngineTorque   = 2000f,
            ClutchStrength    = 10f,
            MaxRollAngle      = 60f * MathF.PI / 180f,
            Friction          = 0.5f,
            Layer             = 1,
            LayerMask         = 0xFFFF,
            Wheels            = BuildTankWheels()
        };

        private static VehicleWheelSettings[] BuildTankWheels()
        {
            float[] zpos = { 2.4f, 1.6f, 0.8f, 0.0f, -0.8f, -1.6f, -2.4f, -3.2f, 2.0f };
            float[] ypos = { -0.3f, -0.3f, -0.3f, -0.3f, -0.3f, -0.3f, -0.3f, -0.3f, 0.1f };
            float[] suspMax = { 0.1f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.1f, 0.3f };
            float suspMin = 0.1f;
            var wheels = new VehicleWheelSettings[18];
            for (int side = 0; side < 2; side++)
            {
                float x = (side == 0) ? 1.5f : -1.5f;
                for (int w = 0; w < 9; w++)
                {
                    wheels[side * 9 + w] = new VehicleWheelSettings
                    {
                        Position           = new System.Numerics.Vector3(x, ypos[w], zpos[w]),
                        Radius             = 0.35f,
                        Width              = 0.1f,
                        SuspMinLength      = suspMin,
                        SuspMaxLength      = suspMax[w],
                        SuspFrequency      = 1.5f,
                        SuspDamping        = 0.5f,
                        MaxSteerAngle      = 0f,
                        MaxHandBrakeTorque = 0f,
                        IsDriven           = (w == 8), // last wheel per track is the driven wheel
                    };
                }
            }
            return wheels;
        }
    }

    /// <summary>Attaches this entity's Transform to a specific wheel of a VehicleComponent entity.</summary>
    public struct WheelFollowerComponent
    {
        public Entity VehicleEntity;
        public int    WheelIndex;
    }

    /// <summary>
    /// Scene-file component blocks this build neither knows nor has a registered extension for
    /// (e.g. plugin data opened without the plugin). SceneSerializer writes them back verbatim.
    /// </summary>
    public struct PreservedComponents
    {
        /// <summary>Component key → raw JSON value, in file order.</summary>
        public Dictionary<string, string> Blocks;
    }

    /// <summary>
    /// On every entity that belongs to an instance of a prefab: which instance, and which entity of the
    /// prefab it stands for. The scene file stores such an entity as that address plus the values that
    /// differ from the prefab (see <c>SceneSerializer</c> and <c>PrefabLibrary</c>).
    /// </summary>
    public struct PrefabLink
    {
        /// <summary>The instance's root entity; the entity itself on the root.</summary>
        public Entity Root;
        /// <summary>On the root only: the prefab's key, its path below Assets ("Prefabs/Enemy.prefab").</summary>
        public string? Asset;
        /// <summary>The entity's address inside the prefab: empty for the root, else the prefab's entity id,
        /// preceded by the id of each nested instance's root on the way down.</summary>
        public int[] Path;
        /// <summary>The prefab could not be loaded: the entity holds only a name and a place, and
        /// <see cref="Raw"/> is written back as it is, so nothing is lost when the file returns.</summary>
        public bool Missing;
        /// <summary>The scene file's entry of a <see cref="Missing"/> entity.</summary>
        public string? Raw;
        /// <summary>Members of the entry's "src" object this build does not know (<c>"key":value,…</c>), written back.</summary>
        public string? Extra;

        public readonly bool IsRoot => Asset != null;
    }
}
