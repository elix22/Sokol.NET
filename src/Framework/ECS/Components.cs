using System.Numerics;

namespace GameEditor.Framework.ECS.Components
{
    public struct Transform
    {
        public Vector3 Position;
        public Vector3 EulerAngles;
        public Vector3 Scale;
        public int? Parent;

        public static Transform Default => new Transform
        {
            Position = Vector3.Zero,
            EulerAngles = Vector3.Zero,
            Scale = Vector3.One,
            Parent = null
        };

        public Matrix4x4 LocalMatrix =>
            Matrix4x4.CreateScale(Scale) *
            Matrix4x4.CreateFromYawPitchRoll(
                EulerAngles.Y * MathF.PI / 180f,
                EulerAngles.X * MathF.PI / 180f,
                EulerAngles.Z * MathF.PI / 180f) *
            Matrix4x4.CreateTranslation(Position);
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
        public string MeshPath;
        public bool Visible;
    }

    public struct CameraComponent
    {
        public float Fov;
        public float NearZ;
        public float FarZ;
        public bool IsMain;
        public bool IsOrthographic;
        public float OrthoSize;   // half-height in world units (ortho mode only)
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
    }

    public struct RigidbodyComponent
    {
        public bool IsStatic;
        public float Mass;
        public bool UseGravity;
    }

    public struct ScriptComponent
    {
        public string TypeName;
    }
}
