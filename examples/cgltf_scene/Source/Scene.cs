using System;
using System.Collections.Generic;
using System.Numerics;
using static Sokol.SLog;

namespace Sokol
{
    /// <summary>
    /// Represents an entire scene with hierarchical node structure
    /// </summary>
    public class Scene
    {
        public Node? RootNode { get; private set; }
        public List<Mesh> AllMeshes { get; private set; } = new List<Mesh>();
        public Dictionary<string, BoneInfo> BoneInfoMap { get; private set; } = new Dictionary<string, BoneInfo>();
        public int BoneCount { get; private set; } = 0;
        public Octree? SpatialIndex { get; set; }

        public Scene(Node rootNode)
        {
            RootNode = rootNode;
            
            // Collect all meshes from the node hierarchy
            if (RootNode != null)
            {
                RootNode.CollectMeshes(AllMeshes);
            }
        }
        
        /// <summary>
        /// Builds the octree spatial index for efficient culling
        /// Call this after the scene is fully loaded and transforms are updated
        /// </summary>
        public void BuildSpatialIndex()
        {
            if (RootNode != null)
            {
                SpatialIndex = Octree.BuildFromScene(this);
                
                var stats = SpatialIndex.GetStats();
                Info($"Octree built: {stats.totalNodes} nodes, {stats.leafNodes} leaves, {stats.totalMeshReferences} mesh references");
            }
        }

        public void SetBoneInfoMap(Dictionary<string, BoneInfo> boneInfoMap, int boneCount)
        {
            BoneInfoMap = boneInfoMap;
            BoneCount = boneCount;
        }

        /// <summary>
        /// Updates all transforms in the scene graph
        /// </summary>
        public void UpdateTransforms()
        {
            RootNode?.UpdateTransform();
        }

        /// <summary>
        /// Draws the entire scene by recursively drawing from root node
        /// </summary>
        public void Draw()
        {
            RootNode?.Draw();
        }

        /// <summary>
        /// Gets the bounding box of the entire scene in world space
        /// </summary>
        public BoundingBox GetSceneBounds()
        {
            if (RootNode == null)
            {
                return new BoundingBox(Vector3.Zero, Vector3.Zero);
            }
            
            // Return the hierarchical bounds transformed to world space
            return RootNode.HierarchicalBounds.Transform(RootNode.WorldTransform);
        }

        /// <summary>
        /// Finds a node by name in the scene hierarchy
        /// </summary>
        public Node? FindNode(string name)
        {
            return FindNodeRecursive(RootNode, name);
        }

        private Node? FindNodeRecursive(Node? node, string name)
        {
            if (node == null) return null;
            if (node.Name == name) return node;

            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
