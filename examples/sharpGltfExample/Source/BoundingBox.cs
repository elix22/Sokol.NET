using System.Numerics;

namespace Sokol
{
 public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;

        public Vector3 Size { get { return Vector3.Abs(Max - Min); } }

        public Vector3 Center { get { return (Min + Max) * 0.5f; } }

        public float Radius
        {
            get
            {
                Vector3 halfSize = Size * 0.5f;
                return halfSize.Length();
            }
        }

        public float DiagonalLength
        {
            get
            {
                return Vector3.Distance(Min, Max);
            }
        }

        public float Volume
        {
            get
            {
                Vector3 size = Size;
                return size.X * size.Y * size.Z;
            }
        }

        Vector3[] corners = new Vector3[8];
        
        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
    
            corners = new Vector3[8]
            {
                new Vector3(Min.X, Min.Y, Min.Z),
                new Vector3(Max.X, Min.Y, Min.Z),
                new Vector3(Min.X, Max.Y, Min.Z),
                new Vector3(Max.X, Max.Y, Min.Z),
                new Vector3(Min.X, Min.Y, Max.Z),
                new Vector3(Max.X, Min.Y, Max.Z),
                new Vector3(Min.X, Max.Y, Max.Z),
                new Vector3(Max.X, Max.Y, Max.Z)
            };
        }
        
        // Transform bounding box by a matrix
        public BoundingBox Transform(Matrix4x4 matrix)
        {
            Vector3 newMin = new Vector3(float.MaxValue);
            Vector3 newMax = new Vector3(float.MinValue);
            
            foreach (var corner in corners)
            {
                Vector3 transformed = Vector3.Transform(corner, matrix);
                newMin = Vector3.Min(newMin, transformed);
                newMax = Vector3.Max(newMax, transformed);
            }
            
            return new BoundingBox(newMin, newMax);
        }
    }
}