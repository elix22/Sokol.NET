# Hierarchical Transform System Implementation

## Overview

This document describes the implementation of a parent-dependent transform system with dirty flagging in `SharpGltfNode`, inspired by Urho3D's `Node` class. This system provides efficient transform calculation by caching world transforms and only recalculating them when necessary.

---

## Key Concepts

### 1. Local vs World Space

- **Local Transform**: Position, rotation, and scale relative to the parent node
- **World Transform**: The final 4x4 matrix in world space, calculated by combining local transform with parent's world transform

### 2. Dirty Flag System

When any transform property changes (position, rotation, scale, or parent):
1. The node marks itself as **dirty**
2. All children are recursively marked dirty
3. When `WorldTransform` is accessed, it checks the dirty flag
4. If dirty, it recalculates the transform recursively from parents
5. The dirty flag is cleared after recalculation

### 3. Lazy Evaluation

World transforms are only calculated when requested, not immediately when local transforms change. This is efficient because:
- Multiple transform changes can be batched
- Transforms are only calculated for visible/used nodes
- Parent transforms are only calculated once per frame

---

## Implementation Details

### SharpGltfNode Structure

```csharp
public class SharpGltfNode
{
    // Local transform (parent space)
    private Vector3 _position;
    private Quaternion _rotation;
    private Vector3 _scale;
    
    // Cached world transform
    private Matrix4x4 _worldTransform;
    private bool _dirty;
    
    // Hierarchy
    private SharpGltfNode? _parent;
    private List<SharpGltfNode> _children;
    
    // Rendering data
    public int MeshIndex;
    public string? NodeName;
    public Node? CachedGltfNode;
    public bool HasAnimation;
}
```

### Key Methods

#### `MarkDirty()`

Marks this node and all descendants as dirty using an iterative algorithm with tail-call optimization (from Urho3D):

```csharp
private void MarkDirty()
{
    SharpGltfNode? cur = this;
    while (cur != null)
    {
        // Early exit: if already dirty, all children must be dirty too
        if (cur._dirty)
            return;
        
        cur._dirty = true;
        
        // Tail-call optimization: process first child iteratively,
        // recurse for remaining children
        if (cur._children.Count > 0)
        {
            SharpGltfNode? next = cur._children[0];
            for (int i = 1; i < cur._children.Count; i++)
                cur._children[i].MarkDirty();
            cur = next;
        }
        else
            return;
    }
}
```

**Why this approach?**
- Avoids deep recursion (prevents stack overflow for deep hierarchies)
- Optimizes the common case (first child) with tail-call optimization
- Early exit when node is already dirty (exploits dirty flag invariant)

#### `UpdateWorldTransform()`

Recalculates world transform when accessed:

```csharp
private void UpdateWorldTransform()
{
    Matrix4x4 localTransform = GetLocalTransform();
    
    if (_parent != null)
    {
        // Combine with parent's world transform (recursive call if parent is dirty)
        _worldTransform = localTransform * _parent.WorldTransform;
    }
    else
    {
        // Root node: world == local
        _worldTransform = localTransform;
    }
    
    _dirty = false;
}
```

**Key insight:** This recursively updates parent transforms first by accessing `_parent.WorldTransform`, ensuring the entire hierarchy is up-to-date.

---

## Integration with Existing Code

### 1. SharpGltfModel Changes

**Before:**
```csharp
private void ProcessNode(Node node, Matrix4x4 parentTransform, ...)
{
    Matrix4x4 worldTransform = localMatrix * parentTransform;
    var renderNode = new SharpGltfNode
    {
        Transform = worldTransform,  // Baked world transform
        ...
    };
}
```

**After:**
```csharp
private void ProcessNodeWithParent(Node node, SharpGltfNode? parentRenderNode, ...)
{
    Matrix4x4.Decompose(localMatrix, out var scale, out var rotation, out var position);
    var renderNode = new SharpGltfNode
    {
        Position = position,
        Rotation = rotation,
        Scale = scale,
        Parent = parentRenderNode,  // Link to parent
        ...
    };
}
```

**Benefits:**
- Builds proper parent-child hierarchy
- Stores local transforms instead of baked world transforms
- Enables dynamic hierarchy manipulation

### 2. Animation System Changes

**Before:**
```csharp
// Animation multiplied local with baked parent transform
nodeTransform = node.CachedGltfNode.LocalMatrix * node.Transform;
```

**After:**
```csharp
// Update node's local transform from animation
Matrix4x4.Decompose(animatedLocal, out var scale, out var rotation, out var position);
node.SetLocalTransform(position, rotation, scale);

// WorldTransform is automatically calculated through hierarchy
nodeTransform = node.WorldTransform;
```

**Benefits:**
- Animation updates local transform
- Dirty flag automatically propagates to children
- World transform is calculated on-demand

### 3. Rendering Code Changes

**Before:**
```csharp
// Direct access to baked transform
Matrix4x4 nodeTransform = node.Transform;
```

**After:**
```csharp
// Access through property (triggers recalculation if dirty)
Matrix4x4 nodeTransform = node.WorldTransform;
```

**Benefits:**
- Automatic dirty checking
- Transparent hierarchy calculation
- Backward compatible through `Transform` property alias

---

## Performance Characteristics

### Memory

- **Before**: Each node stores one 4x4 matrix (64 bytes)
- **After**: Each node stores:
  - Local TRS: Vector3 + Quaternion + Vector3 = 40 bytes
  - Cached world matrix: 64 bytes
  - Dirty flag: 1 byte
  - Parent/children references: 16-24 bytes
  - **Total: ~120 bytes per node**

Trade-off: ~2x memory per node, but enables dynamic hierarchies

### CPU

**Static scenes:**
- One-time cost during load to build hierarchy
- Same cost as before for rendering (cached world transforms)

**Animated scenes:**
- **Before**: O(1) per node (pre-baked world transforms)
- **After**: O(depth) per animated node (recursive parent updates)
  - Amortized to O(1) due to dirty flag (only recalculate when needed)
  - Worst case: Deep hierarchy with all nodes animated

**Key optimization:** Dirty flag ensures transforms are only recalculated once per frame, even if accessed multiple times.

---

## Advantages Over Previous System

### 1. **Proper Hierarchies**
- Parent transforms automatically affect children
- Easy to add/remove/rearrange nodes at runtime

### 2. **Memory Efficiency for Static Scenes**
- Only calculates transforms for visible nodes
- Caches results until next change

### 3. **Correct Animation Behavior**
- Animated parent rotations properly affect children
- Matches glTF specification's hierarchical transforms

### 4. **Debugging & Visualization**
- Can easily query local vs world space
- Can traverse hierarchy (parent/children access)

### 5. **Future Extensibility**
- Can add features like:
  - Transform constraints (look-at, follow, etc.)
  - Dynamic parenting during runtime
  - Scene graph manipulation tools

---

## Comparison with Urho3D Node

### Similarities

1. **Dirty flag system**: Same logic and invariants
2. **Lazy evaluation**: Transforms calculated on-demand
3. **Tail-call optimization**: Iterative MarkDirty() algorithm
4. **Local/world transform separation**: Same architecture

### Differences

1. **No component system**: SharpGltfNode is simpler (just transforms + mesh reference)
2. **No event system**: Urho3D has OnMarkedDirty() listeners
3. **No network replication**: Urho3D has network state tracking
4. **Simpler hierarchy**: No scene/node distinction (all nodes are equal)

---

## Usage Examples

### Creating a Hierarchy

```csharp
var root = new SharpGltfNode { Position = new Vector3(0, 0, 0) };
var child = new SharpGltfNode { Position = new Vector3(1, 0, 0) };
child.Parent = root;  // Automatically adds to root's children

// Child's world position is (1, 0, 0)
Console.WriteLine(child.WorldTransform.Translation);
```

### Animating with Automatic Child Updates

```csharp
// Rotate parent
root.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);

// Child is automatically marked dirty
// Next access to child.WorldTransform will recalculate
Console.WriteLine(child.WorldTransform.Translation);  // Now (0, 0, -1)
```

### Converting World to Local Space

```csharp
Vector3 worldPos = new Vector3(10, 5, 0);
Vector3 localPos = node.WorldToLocal(worldPos);
```

---

## Testing & Validation

### Test Cases

1. **Single node transforms**: Verify world == local for root nodes
2. **Two-level hierarchy**: Parent rotation affects child position
3. **Deep hierarchy**: 10+ levels of nested transforms
4. **Animation propagation**: Animated parent affects static children
5. **Reparenting**: Move nodes between parents preserves world position
6. **Dirty flag optimization**: Multiple changes only recalculate once

### Known Limitations

1. **No automatic reparenting with position preservation**: When changing parent, world position is not automatically preserved
2. **No transform change events**: Code can't be notified when transforms change
3. **Single-threaded**: No thread-safety for concurrent hierarchy modifications

---

## Future Improvements

### 1. Transform Preservation During Reparenting

```csharp
public void SetParentPreservingWorldTransform(SharpGltfNode? newParent)
{
    var oldWorld = WorldTransform;
    Parent = newParent;
    SetWorldTransform(oldWorld);  // Convert world back to new local space
}
```

### 2. Transform Change Callbacks

```csharp
public event Action<SharpGltfNode>? OnTransformChanged;

private void MarkDirty()
{
    // ... existing code ...
    OnTransformChanged?.Invoke(this);
}
```

### 3. Batch Transform Updates

```csharp
public void BeginBatchUpdate() { _batchMode = true; }
public void EndBatchUpdate() 
{ 
    _batchMode = false; 
    MarkDirty(); 
}
```

### 4. Transform Constraints

```csharp
public class LookAtConstraint
{
    public SharpGltfNode Target { get; set; }
    
    public void Apply(SharpGltfNode node)
    {
        var direction = (Target.GetWorldPosition() - node.GetWorldPosition()).Normalize();
        node.Rotation = Quaternion.LookRotation(direction, Vector3.UnitY);
    }
}
```

---

---

## Critical Bug Fix: Multi-Primitive Animation

### Problem Discovery

After implementing the hierarchical transform system, animated nodes (like the watch's seconds hand) were positioned incorrectly. The issue manifested as:

- **Symptom**: Animated seconds hand appeared far from its original position
- **Initial Hypothesis**: Coordinate space mismatch between bone transforms and node transforms
- **Second Hypothesis**: Non-animated channels (translation/rotation/scale) were being overwritten

### Root Cause Analysis

Through extensive debugging and log analysis, the actual issue was discovered:

**glTF Architecture**: A single glTF node can have a mesh with multiple primitives (e.g., different materials):

```
glTF Node "Hand Seconds"
└── Mesh
    ├── Primitive 0 (material A)
    └── Primitive 1 (material B)
```

**Our Implementation**: We create one `SharpGltfNode` per primitive:

```csharp
// In SharpGltfModel.ProcessNodeWithParent()
for (int i = 0; i < node.Mesh.Primitives.Count; i++)
{
    var renderNode = new SharpGltfNode
    {
        NodeName = node.Name,  // ⚠️ All primitives share same name!
        // ... other properties
    };
    nodes.Add(renderNode);
}
```

**The Bug**: Animation system used a `Dictionary<string, SharpGltfNode>` for O(1) node lookup:

```csharp
// SharpGltfAnimator - BEFORE FIX
private Dictionary<string, SharpGltfNode> _nodesByName;

private void BuildNodeLookup(List<SharpGltfNode> nodes)
{
    foreach (var node in nodes)
    {
        if (!_nodesByName.ContainsKey(node.NodeName))
        {
            _nodesByName[node.NodeName] = node;  // ⚠️ Only stores FIRST node!
        }
    }
}
```

**Result**: When "Hand Seconds" has 2 primitives:
1. Both primitives create `SharpGltfNode` instances with `NodeName = "Hand Seconds"`
2. Dictionary stores only ONE (the first added)
3. Animation updates only that ONE node
4. The second primitive never receives animation updates → stays at wrong position

### Debug Log Evidence

```
[NodeLookup] Hand Seconds: IsSkinned=False, HasMesh=0
[NodeLookup] Hand Seconds: IsSkinned=False, HasMesh=0
Built node lookup with 1 non-skinned animated nodes
```

Two nodes logged during creation, but dictionary contained only 1 entry.

### The Fix

Changed from single-node storage to list-based storage:

```csharp
// SharpGltfAnimator - AFTER FIX
private Dictionary<string, List<SharpGltfNode>> _nodesByName;

private void BuildNodeLookup(List<SharpGltfNode> nodes)
{
    _nodesByName.Clear();
    int totalNodes = 0;
    
    foreach (var node in nodes)
    {
        if (!string.IsNullOrEmpty(node.NodeName) && !node.IsSkinned)
        {
            if (!_nodesByName.ContainsKey(node.NodeName))
            {
                _nodesByName[node.NodeName] = new List<SharpGltfNode>();
            }
            _nodesByName[node.NodeName].Add(node);  // ✅ Stores ALL nodes
            totalNodes++;
        }
    }
    Info($"Built node lookup with {_nodesByName.Count} unique names, {totalNodes} total non-skinned nodes");
}
```

**Animation Update Loop**:

```csharp
private void ApplyAnimationToNodes()
{
    if (_currentAnimation == null) return;

    var bones = _currentAnimation.GetBones();
    foreach (var bone in bones)
    {
        // Update ALL nodes with this name
        if (_nodesByName.TryGetValue(bone.Name, out var renderNodes))
        {
            bone.GetAnimatedChannels(out bool hasTranslation, out bool hasRotation, out bool hasScale,
                                     out Vector3 translation, out Quaternion rotation, out Vector3 scale);
            
            // Apply to all nodes (all primitives) with this name
            foreach (var renderNode in renderNodes)
            {
                Vector3 finalTranslation = hasTranslation ? translation : renderNode.Position;
                Quaternion finalRotation = hasRotation ? rotation : renderNode.Rotation;
                Vector3 finalScale = hasScale ? scale : renderNode.Scale;
                
                renderNode.SetLocalTransform(finalTranslation, finalRotation, finalScale);
            }
        }
    }
}
```

### Key Insights

1. **glTF Mesh Primitives ≠ Scene Nodes**: A single logical node in glTF can correspond to multiple renderable primitives
2. **Dictionary Keys Must Be Unique**: Using node names as keys assumes uniqueness, which breaks with multi-primitive meshes
3. **One-to-Many Relationship**: One animated bone → Many render nodes (one per primitive)

### Impact

**Before Fix**:
- Only first primitive of multi-primitive meshes received animation
- Other primitives remained at initial pose or incorrect transforms
- Affected any model where a single node has multiple materials

**After Fix**:
- All primitives of the same glTF node receive identical animation updates
- Correctly handles models with multiple materials per node
- Maintains proper transform hierarchy across all primitives

### Performance Considerations

**Memory**:
- Before: `Dictionary<string, SharpGltfNode>` → 1 reference per unique name
- After: `Dictionary<string, List<SharpGltfNode>>` → List + references per unique name
- Overhead: ~24 bytes for List wrapper per unique animated node name

**CPU**:
- Before: O(1) dictionary lookup → 1 node update
- After: O(1) dictionary lookup → O(p) node updates (p = primitive count)
- Typical case: p = 1-3 primitives per node
- Worst case: p could be 10+ for complex materials

**Real-world impact**: Negligible - most nodes have 1-2 primitives, and the cost of transform updates is minimal compared to rendering.

### Testing Validation

Verified with watch model:
- "Hand Seconds" node has 2 primitives
- Both primitives now animate correctly
- Seconds hand rotates smoothly at correct position
- No visual artifacts or position jumps

### Related Systems

This fix required understanding interaction between:

1. **SharpGltfModel.ProcessNodeWithParent()**: Creates SharpGltfNode instances (one per primitive)
2. **SharpGltfAnimator.BuildNodeLookup()**: Maps node names to instances for animation
3. **SharpGltfAnimator.ApplyAnimationToNodes()**: Updates local transforms from bone data
4. **SharpGltfNode.SetLocalTransform()**: Triggers dirty flag propagation
5. **SharpGltfNode.WorldTransform**: Lazy evaluation calculates final transform for rendering

The bug demonstrated how a seemingly simple data structure choice (single vs list) can have significant correctness implications in a hierarchical system.

---

## Conclusion

The hierarchical transform system provides a robust, efficient foundation for scene graph management. By adopting Urho3D's proven dirty flag architecture, we achieve:

- **Correctness**: Proper parent-child transform relationships
- **Performance**: Lazy evaluation with caching
- **Simplicity**: Clean API with automatic dirty propagation
- **Flexibility**: Easy to extend with new features

The implementation successfully balances memory usage, CPU performance, and code clarity while maintaining compatibility with the existing animation and rendering systems.

**Critical lesson learned**: When mapping from one data model (glTF's logical nodes) to another (rendering nodes), carefully consider one-to-many relationships and ensure data structures support them correctly.
