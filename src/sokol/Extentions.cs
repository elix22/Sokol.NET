using System.Numerics;
using System.Text;
using static Sokol.SSpine;

namespace Sokol
{
    /// <summary>
    /// Extension methods to provide .NET 10+ System.Numerics API compatibility for .NET 8 (Web builds)
    /// </summary>
    public static class SokolExtensions
    {
#if NET8_0
        // Vector2 extensions
        public static Vector2 AsVector2(this Vector3 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }

        public static Vector2 AsVector2(this Vector4 vector)
        {
            return new Vector2(vector.X, vector.Y);
        }

        // Vector3 extensions
        public static Vector3 AsVector3(this Vector2 vector)
        {
            return new Vector3(vector.X, vector.Y, 0.0f);
        }

        public static Vector3 AsVector3(this Vector4 vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }

        // Vector4 extensions
        public static Vector4 AsVector4(this Vector2 vector)
        {
            return new Vector4(vector.X, vector.Y, 0.0f, 0.0f);
        }

        public static Vector4 AsVector4(this Vector3 vector)
        {
            return new Vector4(vector.X, vector.Y, vector.Z, 0.0f);
        }

        public static Vector4 AsVector4Unsafe(this Vector3 vector)
        {
            // Unsafe version that doesn't initialize W component
            return new Vector4(vector.X, vector.Y, vector.Z, 0.0f);
        }

        // Plane extensions
        public static Vector3 AsVector3(this Plane plane)
        {
            return plane.Normal;
        }

        public static Vector4 AsVector4(this Plane plane)
        {
            return new Vector4(plane.Normal, plane.D);
        }

        // Quaternion extensions
        public static Vector3 AsVector3(this Quaternion quaternion)
        {
            return new Vector3(quaternion.X, quaternion.Y, quaternion.Z);
        }

        public static Vector4 AsVector4(this Quaternion quaternion)
        {
            return new Vector4(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }
#endif

        public static string String(this sspine_string str)
        {
            byte[] data = new byte[str.len];
            for (int i = 0; i < str.len; i++)
            {
            data[i] = (byte)str.cstr[i];
            }
            return Encoding.UTF8.GetString(data);
        }

    }
}
