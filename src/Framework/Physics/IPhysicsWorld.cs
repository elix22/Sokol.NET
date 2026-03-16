using System.Numerics;

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

    public enum ColliderShape { Box, Sphere, Capsule }

    public readonly struct BodyDesc
    {
        public readonly Vector3    Position;
        public readonly Quaternion Rotation;
        public readonly Vector3    Scale;
        public readonly bool       IsStatic;
        public readonly float      Mass;
        public readonly bool       UseGravity;
        public readonly ColliderShape Shape;

        public BodyDesc(
            Vector3 position, Quaternion rotation, Vector3 scale,
            bool isStatic, float mass, bool useGravity,
            ColliderShape shape = ColliderShape.Box)
        {
            Position   = position;
            Rotation   = rotation;
            Scale      = scale;
            IsStatic   = isStatic;
            Mass       = mass;
            UseGravity = useGravity;
            Shape      = shape;
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

    /// <summary>
    /// Physics engine abstraction — implemented by JoltPhysicsWorld (3D) and Box2DPhysicsWorld (2D).
    /// All methods are called from the main thread.
    /// </summary>
    public interface IPhysicsWorld
    {
        void Initialize(Vector3 gravity);
        void Step(float deltaTime);
        void Shutdown();

        PhysicsBodyHandle CreateBody(BodyDesc desc);
        void DestroyBody(PhysicsBodyHandle handle);

        void SetPosition(PhysicsBodyHandle handle, Vector3 position);
        void SetRotation(PhysicsBodyHandle handle, Quaternion rotation);
        Vector3    GetPosition(PhysicsBodyHandle handle);
        Quaternion GetRotation(PhysicsBodyHandle handle);

        void SetLinearVelocity(PhysicsBodyHandle handle, Vector3 velocity);
        Vector3 GetLinearVelocity(PhysicsBodyHandle handle);
        void AddForce(PhysicsBodyHandle handle, Vector3 force);
        void AddImpulse(PhysicsBodyHandle handle, Vector3 impulse);

        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit);
    }
}
