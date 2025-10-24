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

        public Node(string name, Matrix4x4 localTransform)
        {
            Name = name;
            LocalTransform = localTransform;
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
