using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using SharpGLTF.Schema2;
using static Sokol.SG;
using static Sokol.SLog;

namespace Sokol
{
    /// <summary>
    /// Scene node that supports parent-dependent transforms with dirty flagging.
    /// Similar to Urho3D's Node class, transforms are cached and only recalculated when dirty.
    /// </summary>
    public class SharpGltfNode
    {
        // Local transform components (relative to parent)
        private Vector3 _position = Vector3.Zero;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;
        
        // Cached world transform (calculated on-demand when dirty flag is set)
        private Matrix4x4 _worldTransform = Matrix4x4.Identity;
        
        // Dirty flag - when true, world transform needs recalculation
        private bool _dirty = true;
        
        // Scene hierarchy
        private SharpGltfNode? _parent = null;
        private List<SharpGltfNode> _children = new List<SharpGltfNode>();
        
        // Rendering properties
        public int MeshIndex = -1;  // Index into SharpGltfModel.Meshes
        public string? NodeName = null;  // Name of the original glTF node (for matching with animations)
        public bool HasAnimation = false;  // Pre-calculated flag to avoid expensive LINQ calls
        public bool IsSkinned = false;  // True if this node is part of a skin (bone hierarchy)
        
        // Morph target data (cached from glTF at load time)
        public int NodeIndex = -1;  // Index in ModelRoot.LogicalNodes (for animator morph weight lookup)
        public IReadOnlyList<float>? NodeMorphWeights = null;  // Node-level morph weights (if any)
        public IReadOnlyList<float>? MeshMorphWeights = null;  // Mesh-level morph weights (fallback)
        
        
        // ========================================================================
        // Properties - Local Transform (Parent Space)
        // ========================================================================
        
        /// <summary>Position in parent space.</summary>
        public Vector3 Position
        {
            get => _position;
            set
            {
                _position = value;
                MarkDirty();
            }
        }
        
        /// <summary>Rotation in parent space.</summary>
        public Quaternion Rotation
        {
            get => _rotation;
            set
            {
                _rotation = value;
                MarkDirty();
            }
        }
        
        /// <summary>Scale in parent space.</summary>
        public Vector3 Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                MarkDirty();
            }
        }
        
        /// <summary>Parent node (null if root).</summary>
        public SharpGltfNode? Parent
        {
            get => _parent;
            set
            {
                if (_parent != value)
                {
                    _parent?._children.Remove(this);
                    _parent = value;
                    _parent?._children.Add(this);
                    MarkDirty();
                }
            }
        }
        
        /// <summary>Child nodes.</summary>
        public IReadOnlyList<SharpGltfNode> Children => _children;
        
        
        // ========================================================================
        // Properties - World Transform (World Space)
        // ========================================================================
        
        /// <summary>
        /// World transform matrix. Automatically recalculated from local transform
        /// and parent hierarchy when dirty flag is set.
        /// </summary>
        public Matrix4x4 WorldTransform
        {
            get
            {
                if (_dirty)
                    UpdateWorldTransform();
                return _worldTransform;
            }
        }
        
        /// <summary>
        /// Legacy property for compatibility. Use WorldTransform instead.
        /// </summary>
        public Matrix4x4 Transform
        {
            get => WorldTransform;
            set => SetWorldTransform(value);
        }
        
        
        // ========================================================================
        // Transform Manipulation
        // ========================================================================
        
        /// <summary>Get local transform matrix (parent space).</summary>
        public Matrix4x4 GetLocalTransform()
        {
            return Matrix4x4.CreateScale(_scale) *
                   Matrix4x4.CreateFromQuaternion(_rotation) *
                   Matrix4x4.CreateTranslation(_position);
        }
        
        /// <summary>
        /// Set local transform from position, rotation, and scale.
        /// </summary>
        public void SetLocalTransform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            _position = position;
            _rotation = rotation;
            _scale = scale;
            MarkDirty();
        }
        
        /// <summary>
        /// Set local transform from a matrix. Decomposes into position, rotation, and scale.
        /// </summary>
        public void SetLocalTransform(Matrix4x4 localMatrix)
        {
            Matrix4x4.Decompose(localMatrix, out _scale, out _rotation, out _position);
            MarkDirty();
        }
        
        /// <summary>
        /// Set world transform directly. Converts to local space if node has a parent.
        /// </summary>
        public void SetWorldTransform(Matrix4x4 worldTransform)
        {
            if (_parent != null)
            {
                // Convert world transform to local space
                Matrix4x4.Invert(_parent.WorldTransform, out var parentInverse);
                var localMatrix = worldTransform * parentInverse;
                SetLocalTransform(localMatrix);
            }
            else
            {
                // No parent, world == local
                SetLocalTransform(worldTransform);
            }
        }
        
        
        // ========================================================================
        // Hierarchy Management
        // ========================================================================
        
        /// <summary>Add a child node.</summary>
        public void AddChild(SharpGltfNode child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));
            
            if (child._parent != this)
            {
                child.Parent = this;
            }
        }
        
        /// <summary>Remove a child node.</summary>
        public void RemoveChild(SharpGltfNode child)
        {
            if (child?._parent == this)
            {
                child.Parent = null;
            }
        }
        
        /// <summary>Remove all children.</summary>
        public void RemoveAllChildren()
        {
            while (_children.Count > 0)
            {
                RemoveChild(_children[0]);
            }
        }
        
        
        // ========================================================================
        // Dirty Flag System (similar to Urho3D Node)
        // ========================================================================
        
        /// <summary>
        /// Mark this node and all its children as dirty (world transform needs recalculation).
        /// Uses iterative approach with tail-call optimization like Urho3D.
        /// </summary>
        private void MarkDirty()
        {
            SharpGltfNode? cur = this;
            
            while (cur != null)
            {
                // Precondition (from Urho3D):
                // a) When a node is marked dirty, all its children are marked dirty too.
                // b) When a node is cleared from being dirty, all its parents must be cleared too.
                // Therefore, if we're recursing to mark this node dirty and it already is,
                // then all children must also be dirty, and we don't need to reflag them.
                if (cur._dirty)
                    return;
                
                cur._dirty = true;
                
                // Tail call optimization: Process first child in current context,
                // recursively mark remaining children
                if (cur._children.Count > 0)
                {
                    SharpGltfNode? next = cur._children[0];
                    for (int i = 1; i < cur._children.Count; i++)
                    {
                        cur._children[i].MarkDirty();
                    }
                    cur = next;
                }
                else
                {
                    return;
                }
            }
        }
        
        /// <summary>
        /// Update world transform from local transform and parent hierarchy.
        /// Called automatically when accessing WorldTransform if dirty flag is set.
        /// </summary>
        private void UpdateWorldTransform()
        {
            Matrix4x4 localTransform = GetLocalTransform();
            
            if (_parent != null)
            {
                // Combine with parent's world transform
                _worldTransform = localTransform * _parent.WorldTransform;
            }
            else
            {
                // Root node, world == local
                _worldTransform = localTransform;
            }
            
            _dirty = false;
        }
        
        
        // ========================================================================
        // Utility Methods
        // ========================================================================
        
        /// <summary>
        /// Get world position (convenience method).
        /// </summary>
        public Vector3 GetWorldPosition()
        {
            return WorldTransform.Translation;
        }
        
        /// <summary>
        /// Get world rotation (convenience method).
        /// </summary>
        public Quaternion GetWorldRotation()
        {
            Matrix4x4.Decompose(WorldTransform, out _, out var rotation, out _);
            return rotation;
        }
        
        /// <summary>
        /// Get world scale (convenience method).
        /// </summary>
        public Vector3 GetWorldScale()
        {
            Matrix4x4.Decompose(WorldTransform, out var scale, out _, out _);
            return scale;
        }
        
        /// <summary>
        /// Convert local position to world space.
        /// </summary>
        public Vector3 LocalToWorld(Vector3 localPosition)
        {
            return Vector3.Transform(localPosition, WorldTransform);
        }
        
        /// <summary>
        /// Convert world position to local space.
        /// </summary>
        public Vector3 WorldToLocal(Vector3 worldPosition)
        {
            Matrix4x4.Invert(WorldTransform, out var inverse);
            return Vector3.Transform(worldPosition, inverse);
        }
    }
}