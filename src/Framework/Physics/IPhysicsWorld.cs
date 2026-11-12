using System;
using System.Numerics;
using System.Collections.Generic;

namespace GameEditor.Framework.Physics
{
    /// <summary>Opaque handle to a physics body in whatever backend is active.</summary>
    public readonly struct PhysicsBodyHandle
    {
        public readonly int Value;
        public bool IsValid => Value > 0;

        public PhysicsBodyHandle(int value) => Value = value;
        public static readonly PhysicsBodyHandle Invalid = new PhysicsBodyHandle(0);
    }

    public enum ColliderShape { Box, Sphere, Capsule, Cylinder, Plane, ConvexHull, Mesh }

    public enum RigidbodyMotionType { Dynamic, Static, Kinematic }

    /// <summary>
    /// One collider shape within a compound body (or the sole shape of a single-shape body).
    /// Dimensions are in mesh-local units; <see cref="BodyDesc.Scale"/> is applied in CreateBody.
    /// </summary>
    public struct ShapeEntry
    {
        public ColliderShape Shape;

        // Box
        public Vector3 HalfExtent;
        // Sphere, Capsule, Cylinder
        public float Radius;
        // Capsule (half-height of cylindrical portion), Cylinder (half-height)
        public float HalfHeight;

        // Offset relative to body centre (compound bodies)
        public Vector3    Offset;
        public Quaternion OffsetRotation;

        // ConvexHull / Mesh source geometry (mesh-local coords; CreateBody scales by entity scale).
        // Not serialised — repopulated from MeshRenderer at play-mode start.
        public Vector3[]? MeshVertices;
        public uint[]?    MeshIndices;

        public static ShapeEntry Default(ColliderShape shape = ColliderShape.Box) => new ShapeEntry
        {
            Shape          = shape,
            HalfExtent     = new Vector3(0.5f, 0.5f, 0.5f),
            Radius         = 0.5f,
            HalfHeight     = 0.5f,
            OffsetRotation = Quaternion.Identity,
        };
    }

    public readonly struct BodyDesc
    {
        public readonly Vector3    Position;
        public readonly Quaternion Rotation;
        public readonly Vector3    Scale;
        public readonly RigidbodyMotionType MotionType;
        public readonly float      Mass;
        public readonly bool       UseGravity;
        public readonly float      Friction;
        public readonly float      Restitution;
        public readonly float      LinearDamping;
        public readonly float      AngularDamping;
        public readonly List<ShapeEntry> Shapes;
        public readonly bool       IsTrigger;
        public readonly ushort     Layer;
        public readonly ushort     LayerMask;

        public bool IsStatic    => MotionType == RigidbodyMotionType.Static;
        public bool IsKinematic => MotionType == RigidbodyMotionType.Kinematic;

        /// <summary>
        /// A dynamic body that weighs <see cref="Mass"/> exactly (its inertia from its shapes). Without it a body
        /// weighs what its shapes hold at the default density — as every body did before this flag, which is kept
        /// so that no existing scene changes. A kart asks for it: its forces are worked out from its mass.
        /// </summary>
        public bool ExactMass { get; init; }

        public BodyDesc(
            Vector3 position, Quaternion rotation, Vector3 scale,
            RigidbodyMotionType motionType, float mass, bool useGravity,
            List<ShapeEntry> shapes,
            float friction = 0.5f,
            float restitution = 0.0f,
            float linearDamping = 0.0f,
            float angularDamping = 0.05f,
            bool isTrigger = false,
            ushort layer = 1,
            ushort layerMask = 0xFFFF)
        {
            Position      = position;
            Rotation      = rotation;
            Scale         = scale;
            MotionType    = motionType;
            Mass          = mass;
            UseGravity    = useGravity;
            Shapes        = shapes ?? new List<ShapeEntry> { ShapeEntry.Default() };
            Friction      = friction;
            Restitution   = restitution;
            LinearDamping  = linearDamping;
            AngularDamping = angularDamping;
            IsTrigger  = isTrigger;
            Layer      = layer;
            LayerMask  = layerMask;
        }
    }

    public readonly struct RaycastHit
    {
        public readonly PhysicsBodyHandle Body;
        public readonly Vector3           Point;
        public readonly Vector3           Normal;
        public readonly float             Distance;

        public RaycastHit(PhysicsBodyHandle body, Vector3 point, Vector3 normal, float distance)
        {
            Body     = body;
            Point    = point;
            Normal   = normal;
            Distance = distance;
        }
    }

    /// <summary>Opaque handle to a character controller (CharacterVirtual).</summary>
    public readonly struct CharacterHandle
    {
        public readonly int Value;
        public bool IsValid => Value > 0;

        public CharacterHandle(int value) => Value = value;
        public static readonly CharacterHandle Invalid = new CharacterHandle(0);
    }

    /// <summary>Describes a character controller to create.</summary>
    public struct CharacterDesc
    {
        public Vector3    Position;
        public Quaternion Rotation;
        /// <summary>Total capsule height (cylindrical + 2 hemispheres), metres.</summary>
        public float Height;
        public float Radius;
        /// <summary>Additional offset added to the auto-centred capsule shape, metres.</summary>
        public Vector3 ShapeOffset;
        /// <summary>Maximum slope the character can climb, radians.</summary>
        public float MaxSlopeAngle;
        /// <summary>Maximum force the character exerts on other bodies, Newtons (Virtual mode only).</summary>
        public float MaxStrength;
        public float Mass;
        /// <summary>Friction (Kinematic mode only).</summary>
        public float Friction;
        /// <summary>Gravity scale (Kinematic mode only; 1.0 = normal gravity).</summary>
        public float GravityFactor;
        /// <summary>PostSimulation max separation distance (Kinematic mode only; default 0.05).</summary>
        public float CollisionTolerance;
        /// <summary>When true, use JPH::Character (kinematic body); otherwise JPH::CharacterVirtual.</summary>
        public bool IsKinematic;
        public ushort Layer;
        public ushort LayerMask;
    }

    /// <summary>Opaque handle to a physics constraint.</summary>
    public readonly struct ConstraintHandle
    {
        public readonly int Value;
        public bool IsValid => Value > 0;

        public ConstraintHandle(int value) => Value = value;
        public static readonly ConstraintHandle Invalid = new ConstraintHandle(0);
    }

    public enum ConstraintType
    {
        Fixed,
        Point,
        Hinge,
        Slider,
        Distance,
        Cone,
        SwingTwist,
        SixDOF,
    }

    public readonly struct ConstraintDesc
    {
        public readonly ConstraintType    Type;
        /// <summary>Invalid = world anchor (static world body).</summary>
        public readonly PhysicsBodyHandle BodyA;
        public readonly PhysicsBodyHandle BodyB;
        public readonly Vector3           LocalAnchorA;
        public readonly Vector3           LocalAnchorB;
        /// <summary>Hinge axis / slider axis / cone twist axis (body-A space).</summary>
        public readonly Vector3           LocalAxisA;
        /// <summary>Hinge axis / slider axis / cone twist axis (body-B space).</summary>
        public readonly Vector3           LocalAxisB;
        /// <summary>Minimum angle (rad) or distance limit.</summary>
        public readonly float             MinLimit;
        /// <summary>Maximum angle (rad) or distance limit.</summary>
        public readonly float             MaxLimit;

        public ConstraintDesc(
            ConstraintType type,
            PhysicsBodyHandle bodyA, PhysicsBodyHandle bodyB,
            Vector3 localAnchorA, Vector3 localAnchorB,
            Vector3 localAxisA, Vector3 localAxisB,
            float minLimit = 0f, float maxLimit = 0f)
        {
            Type         = type;
            BodyA        = bodyA;
            BodyB        = bodyB;
            LocalAnchorA = localAnchorA;
            LocalAnchorB = localAnchorB;
            LocalAxisA   = localAxisA;
            LocalAxisB   = localAxisB;
            MinLimit     = minLimit;
            MaxLimit     = maxLimit;
        }
    }

    /// <summary>
    /// Physics engine abstraction — implemented by JoltPhysicsWorld (3D) and Box2DPhysicsWorld (2D).
    /// All methods are called from the main thread.
    /// </summary>
    public interface IPhysicsWorld
    {
        void Initialize(Vector3 gravity);
        void Step(float deltaTime);

        /// <summary>
        /// Called before each fixed step of the world, with the step's length: forces a system adds here act on that
        /// step (a force added once a frame would act only on the first of the frame's steps).
        /// </summary>
        Action<float>? BeforeStep { get; set; }
        void Shutdown();

        PhysicsBodyHandle CreateBody(BodyDesc desc);
        void DestroyBody(PhysicsBodyHandle handle);

        void SetPosition(PhysicsBodyHandle handle, Vector3 position);
        void SetRotation(PhysicsBodyHandle handle, Quaternion rotation);
        /// <summary>Move a kinematic body to a target pose. Computes implicit velocity so
        /// the body correctly pushes dynamic bodies it collides with.</summary>
        void MoveKinematic(PhysicsBodyHandle handle, Vector3 targetPosition, Quaternion targetRotation, float deltaTime);
        Vector3    GetPosition(PhysicsBodyHandle handle);
        Quaternion GetRotation(PhysicsBodyHandle handle);

        void SetLinearVelocity(PhysicsBodyHandle handle, Vector3 velocity);
        Vector3 GetLinearVelocity(PhysicsBodyHandle handle);
        void SetAngularVelocity(PhysicsBodyHandle handle, Vector3 velocity);
        Vector3 GetAngularVelocity(PhysicsBodyHandle handle);
        void AddForce(PhysicsBodyHandle handle, Vector3 force);
        void AddImpulse(PhysicsBodyHandle handle, Vector3 impulse);
        void AddTorque(PhysicsBodyHandle handle, Vector3 torque);

        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit);
        int OverlapSphere(Vector3 center, float radius, List<PhysicsBodyHandle> results, int maxResults = 64);
        int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, List<PhysicsBodyHandle> results, int maxResults = 64);

        ConstraintHandle CreateConstraint(ConstraintDesc desc);
        void             DestroyConstraint(ConstraintHandle handle);
        void             SetConstraintEnabled(ConstraintHandle handle, bool enabled);

        void SetCollisionListener(ICollisionListener? listener);

        // ── Character controllers ────────────────────────────────────────────

        CharacterHandle CreateCharacter(CharacterDesc desc);
        void            DestroyCharacter(CharacterHandle handle);
        /// <summary>Sets the desired world-space velocity for the character (XZ = movement, Y is managed internally by gravity/ground).</summary>
        void            SetCharacterLinearVelocity(CharacterHandle handle, Vector3 velocity);
        Vector3         GetCharacterPosition(CharacterHandle handle);
        Quaternion      GetCharacterRotation(CharacterHandle handle);
        void            SetCharacterPosition(CharacterHandle handle, Vector3 position);
        bool            IsCharacterGrounded(CharacterHandle handle);
        Vector3         GetCharacterGroundNormal(CharacterHandle handle);

        // ── Vehicle controllers ──────────────────────────────────────────────

        VehicleHandle   CreateVehicle(VehicleDesc desc);
        void            DestroyVehicle(VehicleHandle handle);
        /// <summary>
        /// Apply driver input each frame.
        /// <paramref name="steer"/>    : [-1, +1] — left/right steering.
        /// <paramref name="throttle"/> : [-1, +1] — forward (+) / reverse (-).
        /// <paramref name="brake"/>    : [0, 1]   — foot brake.
        /// <paramref name="handBrake"/>: [0, 1]   — hand brake (rear wheels).
        /// </summary>
        void            SetVehicleInput(VehicleHandle handle, float steer, float throttle, float brake, float handBrake);
        /// <summary>Returns true when the specified wheel is in contact with the ground.</summary>
        bool            IsWheelOnGround(VehicleHandle handle, int wheelIndex);
        /// <summary>Returns the spin speed of the specified wheel in rad/s.</summary>
        float           GetWheelRotationSpeed(VehicleHandle handle, int wheelIndex);
        /// <summary>Number of wheels on the vehicle.</summary>
        int             GetWheelCount(VehicleHandle handle);
        /// <summary>World-space transform of wheel <paramref name="wheelIndex"/>.</summary>
        Matrix4x4       GetWheelWorldTransform(VehicleHandle handle, int wheelIndex);
        Vector3         GetVehiclePosition(VehicleHandle handle);
        Quaternion      GetVehicleRotation(VehicleHandle handle);
        /// <summary>Returns the body handle for the vehicle chassis (for applying forces etc.).</summary>
        PhysicsBodyHandle GetVehicleBodyHandle(VehicleHandle handle);
    }

    /// <summary>Opaque handle to a vehicle constraint.</summary>
    public readonly struct VehicleHandle
    {
        public readonly int Value;
        public bool IsValid => Value > 0;

        public VehicleHandle(int value) => Value = value;
        public static readonly VehicleHandle Invalid = new VehicleHandle(0);
    }

    /// <summary>Per-wheel parameters used by <see cref="VehicleDesc"/>.</summary>
    public struct VehicleWheelDesc
    {
        public Vector3 LocalPosition;
        public float   Radius;
        public float   Width;
        public float   SuspMinLength;
        public float   SuspMaxLength;
        public float   SuspFrequency;
        public float   SuspDamping;
        public float   MaxSteerAngle;
        public float   MaxHandBrakeTorque;
        public bool    IsDriven;
    }

    /// <summary>Vehicle creation parameters for <see cref="IPhysicsWorld.CreateVehicle"/>.</summary>
    public struct VehicleDesc
    {
        public GameEditor.Framework.ECS.Components.VehicleType Type;
        public Vector3    Position;
        public Quaternion Rotation;
        public Vector3    ChassisHalfExtent;
        public float      Mass;
        public float      COMOffsetY;
        public float      MaxEngineTorque;
        public float      ClutchStrength;
        public float      MaxRollAngle;
        public float      Friction;
        public VehicleWheelDesc[] Wheels;
        public ushort     Layer;
        public ushort     LayerMask;
    }
}
