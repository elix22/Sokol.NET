using System.Numerics;
using Assimp;

namespace Sokol
{
    public static class AssimpHelpers
    {
        // Assimp matrices are column-major (OpenGL style), but System.Numerics matrices are row-major (DirectX style)
        // We need to transpose to convert between them
        public static Matrix4x4 ToNumerics(Matrix4x4 matIn)
        {
            // Assimp: columns are X, Y, Z axes and 4th column is translation
            // Numerics: rows are X, Y, Z axes and 4th row is translation
            // Convert Columns => Rows (transpose)
            return new Matrix4x4(
                matIn.M11, matIn.M21, matIn.M31, matIn.M41, // X axis (was column 1)
                matIn.M12, matIn.M22, matIn.M32, matIn.M42, // Y axis (was column 2)
                matIn.M13, matIn.M23, matIn.M33, matIn.M43, // Z axis (was column 3)
                matIn.M14, matIn.M24, matIn.M34, matIn.M44  // Translation (was column 4)
            );
        }

    }
}
