using System;
using System.Collections.Generic;
using System.Numerics;

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

        public Scene(Node rootNode)
        {
            RootNode = rootNode;
            
            // Collect all meshes from the node hierarchy
            if (RootNode != null)
            {
                RootNode.CollectMeshes(AllMeshes);
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
