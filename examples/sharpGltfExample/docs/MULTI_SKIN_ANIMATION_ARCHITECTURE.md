# Multi-Skin Animation Architecture Design

## Executive Summary

This document outlines the architectural redesign required to support glTF models with multiple skins (independent character rigs) and their corresponding animations. The current single-animator architecture cannot properly handle multiple independent characters in one model, as it attempts to animate all characters from a single animation source, causing cross-contamination and deformation.

## Problem Statement

### Current Architecture (Broken)

The existing implementation uses a single-animator pattern:

```
SharpGltfModel
├── Animation (single)
├── SharpGltfAnimator (single instance)
│   └── _currentAnimation (contains ALL bones from ALL characters)
├── BoneInfoMap (mixed bone data)
└── Meshes (all meshes, no character separation)
```

**Fatal Flaw**: When a glTF file contains multiple skins (e.g., Clown with 100 bones + Shark with 56 bones), the current architecture loads them as a single animation containing bones from both characters. When `UpdateAnimation()` runs, it processes all bones simultaneously, causing:

- Cross-contamination: Shark receives Clown's animation transforms (or vice versa)
- Deformation: Characters render with incorrect bone matrices
- No isolation: Impossible to control characters independently

### Example of the Problem

**glTF File Structure** (Multi-Character Scene):
```
skins: [
  {
    name: "Clown",
    joints: [100 bones: "root", "Clown1:Root", "Clown1:head", ...]
    inverseBindMatrices: [...]
  },
  {
    name: "Shark", 
    joints: [56 bones: "shark_root", "Head", "Tail", ...]
    inverseBindMatrices: [...]
  }
]
animations: [
  {
    name: "ClownDance",
    channels: [...] // Targets Clown's joints
  },
  {
    name: "SharkSwim",
    channels: [...] // Targets Shark's joints
  }
]
```

**Current Broken Flow**:
1. Model loads single `Animation` containing bones from both Clown AND Shark
2. Single `SharpGltfAnimator` updates all bones at once
3. `CalculateBoneTransform()` traverses entire hierarchy, writing to both characters
4. Result: Shark gets Clown's transforms → deformation

## Correct Architecture

### High-Level Design

```
SharpGltfModel (Scene Container)
├── List<AnimatedCharacter> Characters
│   ├── AnimatedCharacter (Clown)
│   │   ├── int SkinIndex = 0
│   │   ├── SharpGltfAnimation Animation (ClownDance)
│   │   ├── SharpGltfAnimator Animator (dedicated instance)
│   │   ├── Matrix4x4[] BoneMatrices (100 bones)
│   │   ├── Dictionary<string, BoneInfo> BoneInfoMap (Clown's bones only)
│   │   └── List<Mesh> Meshes (references Skin 0)
│   │
│   └── AnimatedCharacter (Shark)
│       ├── int SkinIndex = 1
│       ├── SharpGltfAnimation Animation (SharkSwim)
│       ├── SharpGltfAnimator Animator (dedicated instance)
│       ├── Matrix4x4[] BoneMatrices (56 bones)
│       ├── Dictionary<string, BoneInfo> BoneInfoMap (Shark's bones only)
│       └── List<Mesh> Meshes (references Skin 1)
│
├── List<Mesh> NonSkinnedMeshes (static geometry)
└── Dictionary<int, List<Mesh>> MaterialToMeshMap (for property animations)
```

### Key Principles

1. **Character Encapsulation**: Each character is a self-contained unit with its own animation system
2. **Animation Isolation**: Each animator updates only its character's bones
3. **Independent Control**: Characters can play different animations at different speeds
4. **Mesh Grouping**: Meshes are grouped by character for efficient rendering

## Implementation Plan

### Phase 1: Create AnimatedCharacter Class

**File**: `src/Sokol/SharpGLTF/SharpGltfAnimatedCharacter.cs`

```csharp
namespace Sokol
{
    /// <summary>
    /// Represents a single animated character (skinned mesh) in a glTF scene.
    /// Each character has its own skeleton, animation, and animator.
    /// </summary>
    public class AnimatedCharacter
    {
        /// <summary>
        /// The skin index this character uses from the glTF file
        /// </summary>
        public int SkinIndex { get; }
        
        /// <summary>
        /// The character's name (from glTF skin name or auto-generated)
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// The animation for this character
        /// </summary>
        public SharpGltfAnimation Animation { get; private set; }
        
        /// <summary>
        /// The animator that updates this character's bones
        /// </summary>
        public SharpGltfAnimator Animator { get; }
        
        /// <summary>
        /// All meshes that belong to this character
        /// </summary>
        public List<Mesh> Meshes { get; }
        
        /// <summary>
        /// Number of bones in this character's skeleton
        /// </summary>
        public int BoneCount { get; }
        
        /// <summary>
        /// Whether this character uses texture-based skinning
        /// (automatically true if BoneCount >= 100)
        /// </summary>
        public bool UsesTextureSkinning => BoneCount >= AnimationConstants.MAX_BONES;
        
        public AnimatedCharacter(
            int skinIndex,
            string name,
            SharpGltfAnimation animation,
            List<Mesh> meshes,
            Dictionary<int, List<Mesh>> materialToMeshMap,
            List<SharpGltfNode> nodes,
            int boneCount)
        {
            SkinIndex = skinIndex;
            Name = name;
            Animation = animation;
            Meshes = meshes;
            BoneCount = boneCount;
            
            // Each character gets its own animator instance
            Animator = new SharpGltfAnimator(
                animation, 
                materialToMeshMap, 
                nodes, 
                boneCount
            );
        }
        
        /// <summary>
        /// Update this character's animation
        /// </summary>
        public void Update(float dt)
        {
            Animator.UpdateAnimation(dt);
        }
        
        /// <summary>
        /// Change this character's animation
        /// </summary>
        public void SetAnimation(SharpGltfAnimation animation)
        {
            Animation = animation;
            Animator.SetAnimation(animation);
        }
        
        /// <summary>
        /// Get the bone matrices for rendering
        /// </summary>
        public Matrix4x4[] GetBoneMatrices()
        {
            return Animator.GetFinalBoneMatrices();
        }
    }
}
```

**Key Design Decisions**:
- Each character owns its animator (no sharing)
- Encapsulates all character-specific data
- Simple API: `Update()`, `SetAnimation()`, `GetBoneMatrices()`
- Exposes `BoneCount` and `UsesTextureSkinning` for renderer

### Phase 2: Update SharpGltfModel

**File**: `src/Sokol/SharpGLTF/SharpGltfModel.cs`

#### 2.1 Remove Old Multi-Skin Complexity

**Remove**:
- `private Dictionary<int, Dictionary<string, BoneInfo>> _skinBoneInfoMaps`
- `private Dictionary<int, int> _skinBoneCounters`
- `private Dictionary<int, HashSet<string>> _skinJointNames`
- Methods: `GetSkinBoneMap()`, `GetSkinBoneCount()`, `GetSkinJointNames()`

#### 2.2 Add New Properties

```csharp
/// <summary>
/// Animated characters in this model (each has its own skeleton and animation)
/// </summary>
public List<AnimatedCharacter> Characters { get; private set; } = new List<AnimatedCharacter>();

/// <summary>
/// Non-skinned meshes (static geometry)
/// </summary>
public List<Mesh> StaticMeshes { get; private set; } = new List<Mesh>();

/// <summary>
/// Legacy single animation accessor (returns first character's animation if available)
/// </summary>
[Obsolete("Use Characters[i].Animation instead")]
public SharpGltfAnimation? Animation => Characters.Count > 0 ? Characters[0].Animation : null;

/// <summary>
/// Total bone count across all characters
/// </summary>
public int TotalBoneCount => Characters.Sum(c => c.BoneCount);
```

#### 2.3 Update ProcessSkinning Method

**Current Signature**:
```csharp
private void ProcessSkinning(...)
```

**New Approach**:
```csharp
/// <summary>
/// Process a SINGLE skin and return its bone info.
/// Called once per skin in the glTF file.
/// </summary>
private (Dictionary<string, BoneInfo> boneInfoMap, int boneCount) ProcessSkin(
    SharpGLTF.Schema2.Skin skin,
    int skinIndex,
    Dictionary<int, SharpGLTF.Schema2.Node> nodesById)
{
    var boneInfoMap = new Dictionary<string, BoneInfo>();
    int boneCounter = 0;
    
    foreach (var (jointNode, invBindMatrix) in skin.JointsCount > 0 
        ? skin.GetEnumerable(rootNode) 
        : Enumerable.Empty<(SharpGLTF.Schema2.Node, Matrix4x4)>())
    {
        string boneName = jointNode.Name;
        
        if (!boneInfoMap.ContainsKey(boneName))
        {
            boneInfoMap[boneName] = new BoneInfo(boneCounter++, invBindMatrix);
        }
    }
    
    Info($"Processed Skin {skinIndex}: {boneCounter} bones", "SharpGLTF");
    return (boneInfoMap, boneCounter);
}
```

**Key Changes**:
- Processes ONE skin at a time
- Returns isolated bone info for that skin only
- No global state contamination
- Simple bone ID assignment: 0, 1, 2, ... N

#### 2.4 Update Model Loading Flow

**New Loading Sequence** (in `LoadGltf()` or constructor):

```csharp
// Step 1: Process all skins first
var skinDataList = new List<(int skinIndex, Dictionary<string, BoneInfo> boneInfoMap, int boneCount)>();
for (int skinIndex = 0; skinIndex < model.LogicalSkins.Count; skinIndex++)
{
    var skin = model.LogicalSkins[skinIndex];
    var (boneInfoMap, boneCount) = ProcessSkin(skin, skinIndex, nodesById);
    skinDataList.Add((skinIndex, boneInfoMap, boneCount));
}

// Step 2: Load nodes and meshes (assign SkinIndex to each mesh)
ProcessNodes(model, ...);

// Step 3: Group meshes by skin
var meshesBySkin = Meshes
    .Where(m => m.SkinIndex >= 0)
    .GroupBy(m => m.SkinIndex)
    .ToDictionary(g => g.Key, g => g.ToList());

// Step 4: Separate static meshes
StaticMeshes = Meshes.Where(m => m.SkinIndex < 0).ToList();

// Step 5: Load animations
var animations = LoadAnimations(model, nodesById);

// Step 6: Create AnimatedCharacter for each skin
foreach (var (skinIndex, boneInfoMap, boneCount) in skinDataList)
{
    if (!meshesBySkin.TryGetValue(skinIndex, out var skinMeshes))
        continue; // Skin has no meshes
    
    // Find appropriate animation for this skin
    var animation = FindAnimationForSkin(animations, skinIndex, boneInfoMap);
    
    if (animation == null)
    {
        Warn($"No animation found for Skin {skinIndex}", "SharpGLTF");
        continue;
    }
    
    // Create character
    string characterName = model.LogicalSkins[skinIndex].Name 
        ?? $"Character_{skinIndex}";
    
    var character = new AnimatedCharacter(
        skinIndex,
        characterName,
        animation,
        skinMeshes,
        MaterialToMeshMap,
        Nodes,
        boneCount
    );
    
    Characters.Add(character);
    Info($"Created character '{characterName}': Skin {skinIndex}, {boneCount} bones, {skinMeshes.Count} meshes", "SharpGLTF");
}
```

#### 2.5 Animation-to-Skin Mapping

**New Method**:
```csharp
/// <summary>
/// Find the appropriate animation for a skin.
/// Strategy:
/// 1. Check if animation channels reference this skin's joints
/// 2. Use animation name matching (e.g., "ClownDance" for skin named "Clown")
/// 3. Return first animation if only one exists
/// 4. Return null if no match found
/// </summary>
private SharpGltfAnimation? FindAnimationForSkin(
    List<SharpGltfAnimation> animations,
    int skinIndex,
    Dictionary<string, BoneInfo> skinBoneInfoMap)
{
    if (animations.Count == 0)
        return null;
    
    if (animations.Count == 1)
        return animations[0]; // Only one animation, use it
    
    // Strategy 1: Check if animation's bones match this skin's bones
    var skinBoneNames = new HashSet<string>(skinBoneInfoMap.Keys);
    
    foreach (var anim in animations)
    {
        var animBoneNames = new HashSet<string>(anim.GetBoneIDMap().Keys);
        
        // If 80%+ of animation bones exist in this skin, it's a match
        int matchCount = animBoneNames.Intersect(skinBoneNames).Count();
        float matchRatio = (float)matchCount / animBoneNames.Count;
        
        if (matchRatio >= 0.8f)
        {
            Info($"Matched animation '{anim.AnimationName}' to Skin {skinIndex} ({matchRatio:P0} bone match)", "SharpGLTF");
            return anim;
        }
    }
    
    // Strategy 2: Name matching (skin name in animation name)
    string skinName = model.LogicalSkins[skinIndex].Name ?? "";
    if (!string.IsNullOrEmpty(skinName))
    {
        var matchedAnim = animations.FirstOrDefault(a => 
            a.AnimationName.Contains(skinName, StringComparison.OrdinalIgnoreCase));
        
        if (matchedAnim != null)
        {
            Info($"Matched animation '{matchedAnim.AnimationName}' to Skin {skinIndex} by name", "SharpGLTF");
            return matchedAnim;
        }
    }
    
    // Fallback: Return first animation
    Warn($"No clear animation match for Skin {skinIndex}, using first animation", "SharpGLTF");
    return animations[0];
}
```

### Phase 3: Update SharpGltfAnimator

**File**: `src/Sokol/SharpGLTF/SharpGltfAnimator.cs`

#### 3.1 Remove Multi-Skin Complexity

**Remove**:
- `private Dictionary<int, Matrix4x4[]> _skinBoneMatrices`
- `private SharpGltfModel _model`
- Method: `InitializePerSkinMatrices()`
- Method: `GetFinalBoneMatrices(int skinIndex)` (overload)

**Keep**:
- `private Matrix4x4[] _finalBoneMatrices` (single array per animator)
- `public Matrix4x4[] GetFinalBoneMatrices()` (parameterless version)

#### 3.2 Simplify Constructor

```csharp
public SharpGltfAnimator(
    SharpGltfAnimation? animation, 
    Dictionary<int, List<Mesh>> materialToMeshMap, 
    List<SharpGltfNode> nodes, 
    int boneCount)
{
    _currentTime = 0.0f;
    _currentAnimation = animation;
    _materialToMeshMap = materialToMeshMap;
    
    // Simple: One array for this animator's character
    _finalBoneMatrices = new Matrix4x4[Math.Max(1, boneCount)];
    
    // Build node lookup (non-skinned animated nodes)
    BuildNodeLookup(nodes);
    
    // Initialize with identity matrices
    Array.Fill(_finalBoneMatrices, Matrix4x4.Identity);
    
    // Initial pose at time 0
    if (_currentAnimation != null)
    {
        ref SharpGltfNodeData rootNode = ref _currentAnimation.GetRootNode();
        CalculateBoneTransform(rootNode, Matrix4x4.Identity);
    }
}
```

**Key Changes**:
- No multi-skin awareness needed
- Each animator instance handles ONE character only
- Simple, clean, focused responsibility

#### 3.3 Remove Multi-Skin Logic from CalculateBoneTransform

**Before** (complex multi-skin version):
```csharp
// Loop through all skins and update each one
foreach (var kvp in _skinBoneMatrices) {
    int skinIndex = kvp.Key;
    var skinBoneMap = _model.GetSkinBoneMap(skinIndex);
    if (skinBoneMap.ContainsKey(nodeName)) {
        kvp.Value[skinBoneIndex] = skinOffset * globalTransformation;
    }
}
```

**After** (simple single-character version):
```csharp
// Just update our single bone array
var boneInfoMap = _currentAnimation?.GetBoneIDMap();
if (boneInfoMap != null && boneInfoMap.ContainsKey(nodeName))
{
    int index = boneInfoMap[nodeName].Id;
    Matrix4x4 offset = boneInfoMap[nodeName].Offset;
    _finalBoneMatrices[index] = offset * globalTransformation;
}
```

**Result**: Animator is now simple, focused, and efficient.

### Phase 4: Update Frame_Skinning.cs Rendering

**File**: `examples/sharpGltfExample/Source/Frame_Skinning.cs`

#### 4.1 Remove Old Rendering Logic

**Remove**:
- Single animator update loop
- Per-mesh skin index lookups

#### 4.2 Add New Rendering Loop

```csharp
public void Draw()
{
    // Update and render each animated character
    foreach (var character in state.model.Characters)
    {
        // Update this character's animation
        character.Update(state.deltaTime);
        
        // Get bone matrices for this character
        Matrix4x4[] boneMatrices = character.GetBoneMatrices();
        
        // Determine skinning mode for this character
        SkinningMode skinningMode = character.UsesTextureSkinning 
            ? SkinningMode.TextureBased 
            : SkinningMode.UniformBased;
        
        // Render all meshes for this character
        foreach (var mesh in character.Meshes)
        {
            RenderSkinnedMesh(mesh, boneMatrices, skinningMode);
        }
    }
    
    // Render static meshes (no animation)
    foreach (var mesh in state.model.StaticMeshes)
    {
        RenderStaticMesh(mesh);
    }
}

private void RenderSkinnedMesh(Mesh mesh, Matrix4x4[] boneMatrices, SkinningMode skinningMode)
{
    // Setup shader bindings
    sg_bindings bindings = new sg_bindings
    {
        vertex_buffers = new sg_buffer_array
        {
            [0] = mesh.VertexBuffer
        },
        index_buffer = mesh.IndexBuffer
    };
    
    // Bind bone texture or uniform buffer depending on mode
    if (skinningMode == SkinningMode.TextureBased)
    {
        bindings.vs_images[Constant.SLOT_vs_bone_matrices] = UploadBoneMatricesToTexture(boneMatrices);
    }
    else
    {
        // Upload bone matrices as uniforms (max 100)
        var boneData = CreateBoneUniformData(boneMatrices);
        sg_apply_uniforms(...);
    }
    
    // Bind textures and render
    sg_apply_bindings(ref bindings);
    sg_draw(0, mesh.IndexCount, 1);
}
```

**Key Changes**:
- Loop through `model.Characters` instead of all meshes
- Update each character's animator independently
- Get bone matrices from character, not global animator
- Skinning mode determined per-character
- Clean separation: character loop → mesh loop

#### 4.3 Add Character Control API (Optional)

```csharp
/// <summary>
/// Set playback speed for a specific character
/// </summary>
public void SetCharacterSpeed(int characterIndex, float speed)
{
    if (characterIndex < state.model.Characters.Count)
    {
        state.model.Characters[characterIndex].Animator.PlaybackSpeed = speed;
    }
}

/// <summary>
/// Change animation for a specific character
/// </summary>
public void SetCharacterAnimation(int characterIndex, SharpGltfAnimation animation)
{
    if (characterIndex < state.model.Characters.Count)
    {
        state.model.Characters[characterIndex].SetAnimation(animation);
    }
}

/// <summary>
/// Pause/resume a specific character
/// </summary>
public void SetCharacterPaused(int characterIndex, bool paused)
{
    // Implementation: Set PlaybackSpeed to 0 or restore previous value
}
```

### Phase 5: Testing & Validation

#### 5.1 Test Cases

1. **Single-Skin Model** (e.g., DancingGangster.glb)
   - Expected: Works as before, single character in `Characters[0]`
   - Verify: Animation plays correctly, no regression

2. **Multi-Skin Model** (e.g., ClownShark.glb)
   - Expected: Two characters, independent animations
   - Verify:
     * Clown animates with ClownDance
     * Shark animates with SharkSwim
     * No cross-contamination
     * No deformation

3. **100+ Bone Character**
   - Expected: Automatic texture-based skinning
   - Verify: `character.UsesTextureSkinning == true`

4. **Mixed Scene** (skinned + static meshes)
   - Expected: Characters in `Characters` list, static in `StaticMeshes`
   - Verify: Both render correctly

#### 5.2 Debug Logging

Add logging to verify correct behavior:

```csharp
// In SharpGltfModel loading:
Info($"Loaded model: {Characters.Count} characters, {StaticMeshes.Count} static meshes", "SharpGLTF");
foreach (var character in Characters)
{
    Info($"  Character '{character.Name}': Skin {character.SkinIndex}, " +
         $"{character.BoneCount} bones, {character.Meshes.Count} meshes, " +
         $"Skinning: {(character.UsesTextureSkinning ? "Texture" : "Uniform")}", "SharpGLTF");
}

// In Frame.cs rendering:
static int _frameCount = 0;
if (_frameCount++ < 5) // First 5 frames only
{
    foreach (var character in state.model.Characters)
    {
        Info($"Rendering character '{character.Name}': {character.Meshes.Count} meshes", "SharpGLTF");
    }
}
```

## Migration Guide

### For Existing Code Using Single Animation

**Before** (old API):
```csharp
var animator = new SharpGltfAnimator(model);
animator.UpdateAnimation(dt);
var boneMatrices = animator.GetFinalBoneMatrices();
```

**After** (new API):
```csharp
// Option 1: Use first character (backward compatible)
if (model.Characters.Count > 0)
{
    var character = model.Characters[0];
    character.Update(dt);
    var boneMatrices = character.GetBoneMatrices();
}

// Option 2: Update all characters
foreach (var character in model.Characters)
{
    character.Update(dt);
    // Render character.Meshes with character.GetBoneMatrices()
}
```

### Deprecated APIs

Mark as obsolete but keep for compatibility:

```csharp
[Obsolete("Use model.Characters instead")]
public SharpGltfAnimation? Animation { get; }

[Obsolete("Use model.TotalBoneCount instead")]
public int BoneCounter { get; }
```

## Performance Considerations

### Memory Impact

**Before**: Single animator, one bone array
- Memory: `1 × boneCount × sizeof(Matrix4x4)` = 64 bytes per bone

**After**: One animator per character
- Memory: `numCharacters × avgBonesPerCharacter × 64 bytes`
- Example: 2 characters, 80 bones each = 2 × 80 × 64 = 10,240 bytes (~10 KB)

**Verdict**: Negligible impact. Multi-character scenes are rare, and memory cost is minimal.

### CPU Impact

**Before**: Single `UpdateAnimation()` call, but complex multi-skin logic
- Cost: O(totalBones) + multi-skin overhead per bone

**After**: Multiple `UpdateAnimation()` calls, simple per-character logic
- Cost: O(bones₁) + O(bones₂) + ... = O(totalBones) total
- Benefit: Better cache locality (each animator processes contiguous bones)

**Verdict**: Neutral or slight improvement. Cleaner code, better cache usage.

### GPU Impact

**No change**: Still uploading same total number of bone matrices, just organized differently.

## Future Enhancements

### 1. Animation Blending Per Character
```csharp
public class AnimatedCharacter
{
    public void BlendToAnimation(SharpGltfAnimation target, float blendTime);
    public void CrossFade(SharpGltfAnimation from, SharpGltfAnimation to, float t);
}
```

### 2. Character-Level LOD
```csharp
public class AnimatedCharacter
{
    public LODLevel CurrentLOD { get; set; }
    public void UpdateLOD(float distanceToCamera);
}
```

### 3. Hierarchical Character Management
```csharp
public class CharacterGroup
{
    public List<AnimatedCharacter> Characters { get; }
    public void UpdateAll(float dt);
    public void SetGroupSpeed(float speed);
}
```

### 4. Animation Events Per Character
```csharp
public class AnimatedCharacter
{
    public event Action<string> OnAnimationEvent;
    public void TriggerEventAtTime(float time, string eventName);
}
```

## Summary

This architecture redesign provides:

1. ✅ **Correct multi-skin support**: Each character has isolated animation system
2. ✅ **Independent control**: Set different speeds, animations per character
3. ✅ **Clean separation**: Clear boundaries between characters
4. ✅ **Backward compatible**: Existing single-character code still works
5. ✅ **Extensible**: Foundation for advanced features (blending, LOD, events)
6. ✅ **Simple**: Easier to understand and maintain than multi-skin hacks

**Implementation Effort**: ~2-3 days for core architecture, 1 day for testing/refinement.

**Risk**: Low. Changes are isolated, can revert to current architecture if needed.

**Benefit**: High. Proper multi-character support, cleaner codebase, foundation for future features.

---

## Appendix: Class Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        SharpGltfModel                           │
├─────────────────────────────────────────────────────────────────┤
│ + List<AnimatedCharacter> Characters                            │
│ + List<Mesh> StaticMeshes                                       │
│ + Dictionary<int, List<Mesh>> MaterialToMeshMap                 │
│ + int TotalBoneCount                                            │
├─────────────────────────────────────────────────────────────────┤
│ + LoadGltf(path)                                                │
│ - ProcessSkin(skin, skinIndex) → (boneInfoMap, boneCount)      │
│ - FindAnimationForSkin(animations, skinIndex) → Animation?     │
└─────────────────────────────────────────────────────────────────┘
                            │ contains
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      AnimatedCharacter                          │
├─────────────────────────────────────────────────────────────────┤
│ + int SkinIndex                                                 │
│ + string Name                                                   │
│ + SharpGltfAnimation Animation                                  │
│ + SharpGltfAnimator Animator                                    │
│ + List<Mesh> Meshes                                             │
│ + int BoneCount                                                 │
│ + bool UsesTextureSkinning                                      │
├─────────────────────────────────────────────────────────────────┤
│ + Update(dt)                                                    │
│ + SetAnimation(animation)                                       │
│ + GetBoneMatrices() → Matrix4x4[]                               │
└─────────────────────────────────────────────────────────────────┘
                            │ owns
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                     SharpGltfAnimator                           │
├─────────────────────────────────────────────────────────────────┤
│ - Matrix4x4[] _finalBoneMatrices                                │
│ - SharpGltfAnimation? _currentAnimation                         │
│ - float _currentTime                                            │
│ + float PlaybackSpeed                                           │
├─────────────────────────────────────────────────────────────────┤
│ + UpdateAnimation(dt)                                           │
│ + SetAnimation(animation)                                       │
│ + GetFinalBoneMatrices() → Matrix4x4[]                          │
│ - CalculateBoneTransform(node, parentTransform)                 │
└─────────────────────────────────────────────────────────────────┘
```

## Appendix: Sequence Diagram (Rendering)

```
Frame.Draw()                Model                Character           Animator
     │                        │                      │                  │
     ├───foreach character────▶                     │                  │
     │                        │                      │                  │
     ├─────────────────────────────character.Update(dt)────────────────▶
     │                        │                      │                  │
     │                        │                      │    UpdateAnimation(dt)
     │                        │                      │    CalculateBoneTransform()
     │                        │                      │                  │
     ├─────────────────────────────character.GetBoneMatrices()─────────▶
     │                        │                      │                  │
     │◀────────────────────────────────────────boneMatrices─────────────┤
     │                        │                      │                  │
     ├───foreach mesh─────────▶                     │                  │
     │                        │                      │                  │
     ├──RenderSkinnedMesh(mesh, boneMatrices, mode)──────────────────────
     │                        │                      │                  │
     └───next character───────▶                     │                  │
```
