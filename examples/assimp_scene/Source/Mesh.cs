using System;
using System.IO;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using Assimp;

namespace Sokol
{
    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;
        
        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }
        
        // Transform bounding box by a matrix
        public BoundingBox Transform(Matrix4x4 matrix)
        {
            // Transform all 8 corners of the box
            Vector3[] corners = new Vector3[8]
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

    public class Mesh
    {
        // Lazy-initialized default white texture for meshes without textures
        private static Texture? _defaultTexture = null;

        public Mesh( Vertex[] vertices, UInt16[] indices, List<Texture> textures)
        {

            Textures = textures;
            
            // Calculate bounding box from vertices
            if (vertices.Length > 0)
            {
                Vector3 min = vertices[0].Position;
                Vector3 max = vertices[0].Position;
                
                for (int i = 1; i < vertices.Length; i++)
                {
                    min = Vector3.Min(min, vertices[i].Position);
                    max = Vector3.Max(max, vertices[i].Position);
                }
                
                Bounds = new BoundingBox(min, max);
            }
            else
            {
                Bounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
            }

            VertexBuffer = sg_make_buffer(new sg_buffer_desc()
            {
                data = SG_RANGE(vertices),
                label = "assimp-simple-vertex-buffer"
            });
            IndexBuffer = sg_make_buffer(new sg_buffer_desc()
            {
                usage = new sg_buffer_usage { index_buffer = true },
                data = SG_RANGE(indices),
                label = "assimp-simple-index-buffer"
            });

            VertexCount = vertices.Length;
            IndexCount = indices.Length;
        }       

        private static unsafe Texture GetDefaultTexture()
        {
            if (_defaultTexture == null)
            {
                // Create a 1x1 white texture as fallback for meshes without textures
                byte* whitePixel = stackalloc byte[4] { 255, 255, 255, 255 };
                _defaultTexture = new Texture(whitePixel, 1, 1, "default-white-texture");
            }
            return _defaultTexture;
        }

        public void Draw()
        {
            // Skip meshes with no indices
            if (IndexCount == 0)
            {
                return;
            }

            // Use default white texture if mesh has no textures
            Texture textureToUse;
            if (Textures.Count == 0 || Textures[0] == null || !Textures[0].IsValid)
            {
                textureToUse = GetDefaultTexture();
            }
            else
            {
                textureToUse = Textures[0];
            }

            sg_bindings bind = default;
            bind.vertex_buffers[0] = VertexBuffer;
            bind.index_buffer = IndexBuffer;
            bind.views[0] = textureToUse.View;
            bind.samplers[0] = textureToUse.Sampler;
            sg_apply_bindings(bind);
            sg_draw(0, (uint)IndexCount, 1);
        }



        public sg_buffer VertexBuffer;
        public sg_buffer IndexBuffer;

        public int VertexCount;
        public int IndexCount;
        
        public BoundingBox Bounds;

        public List<Texture> Textures = new List<Texture>();
        
        // Check if bounding box is visible in camera frustum
        public bool IsVisible(Matrix4x4 worldTransform, Matrix4x4 viewProjection)
        {
            // Transform bounding box to world space
            BoundingBox worldBounds = Bounds.Transform(worldTransform);
            
            // Extract frustum planes from view-projection matrix
            Matrix4x4 vp = viewProjection;
            
            // Left plane
            Vector4 left = new Vector4(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41);
            // Right plane  
            Vector4 right = new Vector4(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41);
            // Bottom plane
            Vector4 bottom = new Vector4(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42);
            // Top plane
            Vector4 top = new Vector4(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42);
            // Near plane
            Vector4 near = new Vector4(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43);
            // Far plane
            Vector4 far = new Vector4(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43);
            
            // Normalize planes
            left = NormalizePlane(left);
            right = NormalizePlane(right);
            bottom = NormalizePlane(bottom);
            top = NormalizePlane(top);
            near = NormalizePlane(near);
            far = NormalizePlane(far);
            
            Vector4[] planes = { left, right, bottom, top, near, far };
            
            // Test bounding box against each plane
            foreach (var plane in planes)
            {
                Vector3 planeNormal = new Vector3(plane.X, plane.Y, plane.Z);
                float planeDistance = plane.W;
                
                // Find the positive vertex (furthest in the direction of the plane normal)
                Vector3 positiveVertex = new Vector3(
                    planeNormal.X >= 0 ? worldBounds.Max.X : worldBounds.Min.X,
                    planeNormal.Y >= 0 ? worldBounds.Max.Y : worldBounds.Min.Y,
                    planeNormal.Z >= 0 ? worldBounds.Max.Z : worldBounds.Min.Z
                );
                
                float distance = Vector3.Dot(planeNormal, positiveVertex) + planeDistance;
                
                // If positive vertex is outside this plane, box is completely outside frustum
                if (distance < 0)
                    return false;
            }
            
            return true;
        }
        
        private static Vector4 NormalizePlane(Vector4 plane)
        {
            Vector3 normal = new Vector3(plane.X, plane.Y, plane.Z);
            float length = normal.Length();
            if (length > 0.0001f)
            {
                return new Vector4(
                    plane.X / length,
                    plane.Y / length,
                    plane.Z / length,
                    plane.W / length
                );
            }
            return plane;
        }
    }
}




