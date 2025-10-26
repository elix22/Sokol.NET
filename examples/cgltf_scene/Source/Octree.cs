using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    /// <summary>
    /// Octree node for spatial partitioning and efficient frustum culling
    /// </summary>
    public class OctreeNode
    {
        public BoundingBox Bounds { get; private set; }  // Subdivision bounds (fixed)
        public BoundingBox CullingBox { get; private set; }  // Expanded bounds for culling (prevents over-culling)
        public List<(Mesh mesh, Matrix4x4 transform)> Meshes { get; private set; }
        public OctreeNode[]? Children { get; private set; }
        
        private const int MAX_MESHES_PER_NODE = 8;
        private const int MAX_DEPTH = 8;
        private int _depth;

        public OctreeNode(BoundingBox bounds, int depth = 0)
        {
            Bounds = bounds;
            
            // Create expanded culling box like Urho3D does
            // This prevents over-culling of objects near octant boundaries
            Vector3 center = (bounds.Min + bounds.Max) * 0.5f;
            Vector3 halfSize = (bounds.Max - bounds.Min) * 0.5f;
            CullingBox = new BoundingBox(bounds.Min - halfSize, bounds.Max + halfSize);
            
            Meshes = new List<(Mesh, Matrix4x4)>();
            Children = null;
            _depth = depth;
        }

        /// <summary>
        /// Inserts a mesh with its world transform into the octree
        /// </summary>
        public void Insert(Mesh mesh, Matrix4x4 worldTransform)
        {
            // Transform mesh bounds to world space
            var worldBounds = mesh.Bounds.Transform(worldTransform);
            
            // Check if mesh intersects this node's bounds
            if (!BoundsIntersect(Bounds, worldBounds))
                return;

            // If we have children, decide where to insert
            if (Children != null)
            {
                // Check if mesh should stay at this level
                bool insertHere = CheckMeshFit(worldBounds);
                
                if (insertHere)
                {
                    // Large mesh or partially outside - keep at this level
                    Meshes.Add((mesh, worldTransform));
                }
                else
                {
                    // Small mesh - push to appropriate child
                    Vector3 meshCenter = (worldBounds.Min + worldBounds.Max) * 0.5f;
                    Vector3 octantCenter = (Bounds.Min + Bounds.Max) * 0.5f;
                    
                    int childIndex = 0;
                    if (meshCenter.X >= octantCenter.X) childIndex |= 1;
                    if (meshCenter.Y >= octantCenter.Y) childIndex |= 2;
                    if (meshCenter.Z >= octantCenter.Z) childIndex |= 4;

                    Children[childIndex].Insert(mesh, worldTransform);
                }
            }
            else
            {
                // No children yet - add to this node
                Meshes.Add((mesh, worldTransform));

                // Subdivide if we have too many meshes and haven't reached max depth
                if (Meshes.Count > MAX_MESHES_PER_NODE && _depth < MAX_DEPTH)
                {
                    Subdivide();
                }
            }
        }

        /// <summary>
        /// Checks if a mesh should be inserted at this octant level (Urho3D style)
        /// Returns true if mesh is too large for children or at max depth
        /// </summary>
        private bool CheckMeshFit(BoundingBox meshWorldBounds)
        {
            Vector3 meshSize = meshWorldBounds.Max - meshWorldBounds.Min;
            Vector3 halfSize = (Bounds.Max - Bounds.Min) * 0.5f;

            // If at max depth, always insert here
            if (_depth >= MAX_DEPTH)
                return true;

            // If mesh is at least half the size of this octant in any dimension, insert here
            if (meshSize.X >= halfSize.X || meshSize.Y >= halfSize.Y || meshSize.Z >= halfSize.Z)
                return true;

            // Check if mesh extends beyond child octant culling boxes
            // If it does, it must be inserted at this level
            Vector3 quarterSize = halfSize * 0.5f;
            
            if (meshWorldBounds.Min.X <= Bounds.Min.X - quarterSize.X ||
                meshWorldBounds.Max.X >= Bounds.Max.X + quarterSize.X ||
                meshWorldBounds.Min.Y <= Bounds.Min.Y - quarterSize.Y ||
                meshWorldBounds.Max.Y >= Bounds.Max.Y + quarterSize.Y ||
                meshWorldBounds.Min.Z <= Bounds.Min.Z - quarterSize.Z ||
                meshWorldBounds.Max.Z >= Bounds.Max.Z + quarterSize.Z)
                return true;

            // Mesh is small enough to go to a child octant
            return false;
        }

        /// <summary>
        /// Subdivides this node into 8 children
        /// </summary>
        private void Subdivide()
        {
            Vector3 center = (Bounds.Min + Bounds.Max) * 0.5f;
            Vector3 extent = (Bounds.Max - Bounds.Min) * 0.5f;

            Children = new OctreeNode[8];

            // Create 8 child nodes (octants)
            for (int i = 0; i < 8; i++)
            {
                Vector3 childMin = new Vector3(
                    (i & 1) == 0 ? Bounds.Min.X : center.X,
                    (i & 2) == 0 ? Bounds.Min.Y : center.Y,
                    (i & 4) == 0 ? Bounds.Min.Z : center.Z
                );
                
                Vector3 childMax = new Vector3(
                    (i & 1) == 0 ? center.X : Bounds.Max.X,
                    (i & 2) == 0 ? center.Y : Bounds.Max.Y,
                    (i & 4) == 0 ? center.Z : Bounds.Max.Z
                );

                Children[i] = new OctreeNode(new BoundingBox(childMin, childMax), _depth + 1);
            }

            // Redistribute meshes - only move small meshes that fit in children
            var meshesToRedistribute = new List<(Mesh, Matrix4x4)>(Meshes);
            Meshes.Clear();

            foreach (var (mesh, transform) in meshesToRedistribute)
            {
                var worldBounds = mesh.Bounds.Transform(transform);
                
                // Check if mesh should stay at this level
                if (CheckMeshFit(worldBounds))
                {
                    // Large or spanning mesh - keep it at this level
                    Meshes.Add((mesh, transform));
                }
                else
                {
                    // Small mesh - push to appropriate child
                    Vector3 meshCenter = (worldBounds.Min + worldBounds.Max) * 0.5f;
                    
                    int childIndex = 0;
                    if (meshCenter.X >= center.X) childIndex |= 1;
                    if (meshCenter.Y >= center.Y) childIndex |= 2;
                    if (meshCenter.Z >= center.Z) childIndex |= 4;

                    Children[childIndex].Insert(mesh, transform);
                }
            }
        }

        /// <summary>
        /// Queries visible meshes using frustum culling (optimized - minimal per-mesh tests)
        /// </summary>
        public void QueryVisible(
            Matrix4x4 viewProjection, 
            Vector3 cameraPosition,
            float maxDistance,
            bool enableDistanceCulling,
            List<(Mesh mesh, Matrix4x4 transform)> visibleMeshes,
            ref int nodesTestedCount,
            ref int nodesCulledCount,
            bool inside = false)  // inside flag: true if this node is completely inside frustum
        {
            nodesTestedCount++;

            // Test this node's CULLING BOX (expanded bounds) against frustum
            if (!inside)
            {
                var intersection = TestOctantVisibility(viewProjection);
                
                if (intersection == FrustumIntersection.Outside)
                {
                    nodesCulledCount++;
                    return;
                }
                else if (intersection == FrustumIntersection.Inside)
                {
                    inside = true;
                }
            }

            // Process meshes at this level
            // If octant is fully inside frustum, skip per-mesh frustum tests (huge optimization!)
            if (inside)
            {
                // Octant fully inside - add all meshes (only distance culling if enabled)
                if (enableDistanceCulling)
                {
                    foreach (var (mesh, transform) in Meshes)
                    {
                        var meshBounds = mesh.Bounds.Transform(transform);
                        Vector3 meshCenter = (meshBounds.Min + meshBounds.Max) * 0.5f;
                        if (Vector3.Distance(cameraPosition, meshCenter) <= maxDistance)
                        {
                            visibleMeshes.Add((mesh, transform));
                        }
                    }
                }
                else
                {
                    // No distance culling - add all meshes directly
                    visibleMeshes.AddRange(Meshes);
                }
            }
            else
            {
                // Octant intersects frustum - need per-mesh tests
                foreach (var (mesh, transform) in Meshes)
                {
                    // Distance culling
                    if (enableDistanceCulling)
                    {
                        var meshBounds = mesh.Bounds.Transform(transform);
                        Vector3 meshCenter = (meshBounds.Min + meshBounds.Max) * 0.5f;
                        
                        if (Vector3.Distance(cameraPosition, meshCenter) > maxDistance)
                            continue;
                        
                        // Per-mesh frustum test (only when octant intersects)
                        if (mesh.IsVisible(transform, viewProjection))
                        {
                            visibleMeshes.Add((mesh, transform));
                        }
                    }
                    else
                    {
                        // Per-mesh frustum test only
                        if (mesh.IsVisible(transform, viewProjection))
                        {
                            visibleMeshes.Add((mesh, transform));
                        }
                    }
                }
            }

            // Recursively query children if they exist
            if (Children != null)
            {
                foreach (var child in Children)
                {
                    child.QueryVisible(
                        viewProjection, 
                        cameraPosition, 
                        maxDistance, 
                        enableDistanceCulling, 
                        visibleMeshes,
                        ref nodesTestedCount,
                        ref nodesCulledCount,
                        inside);  // Pass inside flag to children
                }
            }
        }

        /// <summary>
        /// Intersection result for frustum testing
        /// </summary>
        private enum FrustumIntersection
        {
            Outside,  // Completely outside frustum
            Intersect, // Partially inside frustum
            Inside    // Completely inside frustum
        }

        /// <summary>
        /// Tests if this node's culling box is visible in the frustum
        /// Returns: Outside, Intersect, or Inside
        /// </summary>
        private FrustumIntersection TestOctantVisibility(Matrix4x4 viewProjection)
        {
            // Use CullingBox (expanded bounds) to prevent over-culling near boundaries
            BoundingBox testBounds = CullingBox;
            
            // Extract frustum planes from view-projection matrix
            Matrix4x4 m = viewProjection;
            
            Vector4 leftPlane = new Vector4(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41);
            Vector4 rightPlane = new Vector4(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41);
            Vector4 topPlane = new Vector4(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42);
            Vector4 bottomPlane = new Vector4(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42);
            Vector4 nearPlane = new Vector4(m.M14 + m.M13, m.M24 + m.M23, m.M34 + m.M33, m.M44 + m.M43);
            Vector4 farPlane = new Vector4(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43);
            
            // Normalize planes
            leftPlane = NormalizePlane(leftPlane);
            rightPlane = NormalizePlane(rightPlane);
            topPlane = NormalizePlane(topPlane);
            bottomPlane = NormalizePlane(bottomPlane);
            nearPlane = NormalizePlane(nearPlane);
            farPlane = NormalizePlane(farPlane);
            
            Vector4[] frustumPlanes = { leftPlane, rightPlane, topPlane, bottomPlane, nearPlane, farPlane };
            
            bool allInside = true;
            
            // Test bounding box against all frustum planes
            foreach (var plane in frustumPlanes)
            {
                Vector3 planeNormal = new Vector3(plane.X, plane.Y, plane.Z);
                float planeDistance = plane.W;
                
                // Find the positive vertex (furthest point in direction of plane normal)
                Vector3 positiveVertex = new Vector3(
                    planeNormal.X >= 0 ? testBounds.Max.X : testBounds.Min.X,
                    planeNormal.Y >= 0 ? testBounds.Max.Y : testBounds.Min.Y,
                    planeNormal.Z >= 0 ? testBounds.Max.Z : testBounds.Min.Z
                );
                
                // Find the negative vertex (nearest point in direction of plane normal)
                Vector3 negativeVertex = new Vector3(
                    planeNormal.X >= 0 ? testBounds.Min.X : testBounds.Max.X,
                    planeNormal.Y >= 0 ? testBounds.Min.Y : testBounds.Max.Y,
                    planeNormal.Z >= 0 ? testBounds.Min.Z : testBounds.Max.Z
                );
                
                // If positive vertex is outside this plane, the entire box is outside
                if (Vector3.Dot(planeNormal, positiveVertex) + planeDistance < 0)
                {
                    return FrustumIntersection.Outside;
                }
                
                // If negative vertex is outside this plane, box intersects
                if (Vector3.Dot(planeNormal, negativeVertex) + planeDistance < 0)
                {
                    allInside = false;
                }
            }
            
            return allInside ? FrustumIntersection.Inside : FrustumIntersection.Intersect;
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

        /// <summary>
        /// Checks if two bounding boxes intersect
        /// </summary>
        private bool BoundsIntersect(BoundingBox a, BoundingBox b)
        {
            return (a.Min.X <= b.Max.X && a.Max.X >= b.Min.X) &&
                   (a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y) &&
                   (a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z);
        }

        /// <summary>
        /// Gets statistics about the octree structure
        /// </summary>
        public void GetStats(ref int totalNodes, ref int leafNodes, ref int totalMeshReferences)
        {
            totalNodes++;
            
            // Count meshes at this level (parent nodes can have meshes too!)
            totalMeshReferences += Meshes.Count;
            
            if (Children == null)
            {
                leafNodes++;
            }
            else
            {
                foreach (var child in Children)
                {
                    child.GetStats(ref totalNodes, ref leafNodes, ref totalMeshReferences);
                }
            }
        }
    }

    /// <summary>
    /// Octree root that manages the entire spatial hierarchy
    /// </summary>
    public class Octree
    {
        public OctreeNode Root { get; private set; }
        
        public Octree(BoundingBox worldBounds)
        {
            Root = new OctreeNode(worldBounds);
        }

        /// <summary>
        /// Builds the octree from a scene graph
        /// </summary>
        public static Octree BuildFromScene(Scene scene)
        {
            // First pass: calculate world bounds of entire scene
            BoundingBox worldBounds = CalculateSceneBounds(scene.RootNode);
            
            // Add some padding to avoid edge cases
            Vector3 padding = (worldBounds.Max - worldBounds.Min) * 0.1f;
            worldBounds = new BoundingBox(worldBounds.Min - padding, worldBounds.Max + padding);
            
            // Create octree
            Octree octree = new Octree(worldBounds);
            
            // Second pass: insert all meshes with their world transforms
            InsertSceneNode(octree.Root, scene.RootNode);
            
            return octree;
        }

        private static BoundingBox CalculateSceneBounds(Node? node)
        {
            if (node == null)
                return new BoundingBox(Vector3.Zero, Vector3.Zero);

            bool first = true;
            Vector3 min = Vector3.Zero;
            Vector3 max = Vector3.Zero;

            // Include all meshes in this node
            foreach (var mesh in node.Meshes)
            {
                var meshWorldBounds = mesh.Bounds.Transform(node.WorldTransform);
                
                if (first)
                {
                    min = meshWorldBounds.Min;
                    max = meshWorldBounds.Max;
                    first = false;
                }
                else
                {
                    min = Vector3.Min(min, meshWorldBounds.Min);
                    max = Vector3.Max(max, meshWorldBounds.Max);
                }
            }

            // Include all children recursively
            foreach (var child in node.Children)
            {
                var childBounds = CalculateSceneBounds(child);
                
                if (first)
                {
                    min = childBounds.Min;
                    max = childBounds.Max;
                    first = false;
                }
                else
                {
                    min = Vector3.Min(min, childBounds.Min);
                    max = Vector3.Max(max, childBounds.Max);
                }
            }

            return new BoundingBox(min, max);
        }

        private static void InsertSceneNode(OctreeNode octreeNode, Node? sceneNode)
        {
            if (sceneNode == null)
                return;

            // Insert all meshes from this node
            foreach (var mesh in sceneNode.Meshes)
            {
                octreeNode.Insert(mesh, sceneNode.WorldTransform);
            }

            // Recursively insert children
            foreach (var child in sceneNode.Children)
            {
                InsertSceneNode(octreeNode, child);
            }
        }

        /// <summary>
        /// Queries all visible meshes using frustum culling
        /// </summary>
        public List<(Mesh mesh, Matrix4x4 transform)> QueryVisible(
            Matrix4x4 viewProjection,
            Vector3 cameraPosition,
            float maxDistance,
            bool enableDistanceCulling,
            out int nodesTestedCount,
            out int nodesCulledCount)
        {
            var visibleMeshes = new List<(Mesh mesh, Matrix4x4 transform)>(2048); // Pre-allocate for better performance
            nodesTestedCount = 0;
            nodesCulledCount = 0;

            Root.QueryVisible(
                viewProjection,
                cameraPosition,
                maxDistance,
                enableDistanceCulling,
                visibleMeshes,
                ref nodesTestedCount,
                ref nodesCulledCount);

            return visibleMeshes;
        }

        /// <summary>
        /// Gets statistics about the octree
        /// </summary>
        public (int totalNodes, int leafNodes, int totalMeshReferences) GetStats()
        {
            int totalNodes = 0;
            int leafNodes = 0;
            int totalMeshReferences = 0;
            
            Root.GetStats(ref totalNodes, ref leafNodes, ref totalMeshReferences);
            
            return (totalNodes, leafNodes, totalMeshReferences);
        }
    }
}
