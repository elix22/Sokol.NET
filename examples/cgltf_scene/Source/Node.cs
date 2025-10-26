using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sokol
{
    /// <summary>
    /// Represents a node in the scene graph hierarchy
    /// </summary>
    public class Node
    {
        public string Name { get; set; } = "";
        public Matrix4x4 LocalTransform { get; set; } = Matrix4x4.Identity;
        public Matrix4x4 WorldTransform { get; private set; } = Matrix4x4.Identity;
        
        public Node? Parent { get; private set; }
        public List<Node> Children { get; private set; } = new List<Node>();
        public List<Mesh> Meshes { get; private set; } = new List<Mesh>();
        
        // Hierarchical bounding box (encompasses all children and meshes)
        public BoundingBox HierarchicalBounds { get; private set; }

        public Node(string name, Matrix4x4 localTransform)
        {
            Name = name;
            LocalTransform = localTransform;
            HierarchicalBounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
        }

        public void AddChild(Node child)
        {
            if (child.Parent != null)
            {
                child.Parent.Children.Remove(child);
            }
            
            child.Parent = this;
            Children.Add(child);
        }

        public void AddMesh(Mesh mesh)
        {
            Meshes.Add(mesh);
        }

        /// <summary>
        /// Updates the world transform based on parent's world transform
        /// Call this after modifying LocalTransform
        /// </summary>
        public void UpdateTransform(Matrix4x4? parentWorldTransform = null)
        {
            if (parentWorldTransform.HasValue)
            {
                WorldTransform = LocalTransform * parentWorldTransform.Value;
            }
            else
            {
                WorldTransform = LocalTransform;
            }

            // Recursively update all children
            foreach (var child in Children)
            {
                child.UpdateTransform(WorldTransform);
            }
            
            // Update hierarchical bounding box after transforms
            UpdateHierarchicalBounds();
        }
        
        /// <summary>
        /// Updates the hierarchical bounding box to encompass all meshes and children
        /// Call after adding meshes or children
        /// </summary>
        public void UpdateHierarchicalBounds()
        {
            if (Meshes.Count == 0 && Children.Count == 0)
            {
                HierarchicalBounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
                return;
            }
            
            bool first = true;
            Vector3 min = Vector3.Zero;
            Vector3 max = Vector3.Zero;
            
            // Include all mesh bounds
            foreach (var mesh in Meshes)
            {
                var meshBounds = mesh.Bounds;
                if (first)
                {
                    min = meshBounds.Min;
                    max = meshBounds.Max;
                    first = false;
                }
                else
                {
                    min = Vector3.Min(min, meshBounds.Min);
                    max = Vector3.Max(max, meshBounds.Max);
                }
            }
            
            // Include all children's hierarchical bounds (in local space)
            foreach (var child in Children)
            {
                if (child.Meshes.Count == 0 && child.Children.Count == 0)
                    continue;
                    
                // Transform child's hierarchical bounds to this node's local space
                var childWorldBounds = child.HierarchicalBounds.Transform(child.WorldTransform);
                
                // Transform back to local space if we have world transform
                Matrix4x4 worldToLocal;
                if (Matrix4x4.Invert(WorldTransform, out worldToLocal))
                {
                    var childLocalBounds = childWorldBounds.Transform(worldToLocal);
                    
                    if (first)
                    {
                        min = childLocalBounds.Min;
                        max = childLocalBounds.Max;
                        first = false;
                    }
                    else
                    {
                        min = Vector3.Min(min, childLocalBounds.Min);
                        max = Vector3.Max(max, childLocalBounds.Max);
                    }
                }
            }
            
            HierarchicalBounds = new BoundingBox(min, max);
        }

        /// <summary>
        /// Draws all meshes in this node and recursively draws children
        /// </summary>
        public void Draw()
        {
            // Draw all meshes attached to this node
            foreach (var mesh in Meshes)
            {
                mesh.Draw();
            }

            // Recursively draw all children
            foreach (var child in Children)
            {
                child.Draw();
            }
        }

        /// <summary>
        /// Collects all meshes from this node and all children recursively
        /// </summary>
        public void CollectMeshes(List<Mesh> meshList)
        {
            meshList.AddRange(Meshes);
            
            foreach (var child in Children)
            {
                child.CollectMeshes(meshList);
            }
        }
    }
}
