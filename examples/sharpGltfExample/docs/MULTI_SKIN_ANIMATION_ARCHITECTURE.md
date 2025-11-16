# Multi-Skin Animation Architecture

## Executive Summary

This document describes the implemented multi-character animation system that supports glTF models with multiple skins, node animations, and mixed animation scenarios. The architecture provides full independence for each animated character while correctly handling bone animations, node animations, and models without skinning data.

## Problem Statement & Solution

### Original Problem

The previous single-animator architecture couldn't handle:
1. **Multiple independent characters** in one glTF file (e.g., 2 characters with separate bone hierarchies)
2. **Node animations** (animations targeting non-skinned nodes like cameras or props)
3. **Mixed animation scenarios** (models with both skinned bones and node animations)

### Implemented Architecture

The new system uses a **per-character animation** approach:

```
SharpGltfModel
├── List<AnimatedCharacter> Characters (one per skin)
│   ├── AnimatedCharacter (Character 0)
│   │   ├── int SkinIndex = 0
│   │   ├── List<SharpGltfAnimation> Animations (filtered for this character's bones)
│   │   ├── SharpGltfAnimator Animator (dedicated instance, 0-N bone indexing)
│   │   ├── Dictionary<string, BoneInfo> BoneInfoMap (character-specific, 0-N indices)
│   │   └── List<Mesh> Meshes (references Skin 0)
│   │
│   └── AnimatedCharacter (Character 1)
│       ├── int SkinIndex = 1
│       ├── List<SharpGltfAnimation> Animations (filtered for this character's bones)
│       ├── SharpGltfAnimator Animator (dedicated instance, 0-N bone indexing)
│       ├── Dictionary<string, BoneInfo> BoneInfoMap (character-specific, 0-N indices)
│       └── List<Mesh> Meshes (references Skin 1)
│
├── SharpGltfAnimation? NodeAnimation (for models without skins)
├── List<Mesh> NonSkinnedMeshes (static geometry)
└── HashSet<string> SkinnedNodeNames (global: all joint names from all skins)
```

### Key Implementation Details

### Key Implementation Details

#### 1. Character-Specific Bone Indexing (0-N per Character)

Each character has **independent bone indices** starting from 0:

```csharp
// Character 0: bones indexed 0, 1, 2, ..., N₀
Dictionary<string, BoneInfo> character0BoneMap = {
    {"Root", new BoneInfo(0, ...)},
    {"Spine", new BoneInfo(1, ...)},
    {"Head", new BoneInfo(2, ...)},
    ...
};

// Character 1: bones ALSO indexed 0, 1, 2, ..., N₁ (independent!)
Dictionary<string, BoneInfo> character1BoneMap = {
    {"Root", new BoneInfo(0, ...)},     // Different bone, same index!
    {"Hips", new BoneInfo(1, ...)},
    {"Leg_L", new BoneInfo(2, ...)},
    ...
};
```

**Benefits**:
- Each character's bone array is compact and cache-friendly
- Shader uniforms/textures can use simple 0-N indexing
- No gaps in bone arrays
- Easy to determine skinning mode per character (if bones >= 100, use texture skinning)

#### 2. Character-Specific Animation Filtering

**Critical Fix**: Animations are filtered per character to include only channels that affect that character's bones OR non-skinned nodes:

```csharp
private List<SharpGltfAnimation> ProcessAnimationsForCharacter(
    string characterName,
    Dictionary<string, BoneInfo> characterBoneInfoMap,
    SharpGltfNode rootNode)
{
    var characterAnimations = new List<SharpGltfAnimation>();
    
    foreach (var gltfAnimation in _model.LogicalAnimations)
    {
        var animation = new SharpGltfAnimation(...);
        
        int boneChannelCount = 0;
        int nodeChannelCount = 0;
        
        foreach (var channel in gltfAnimation.Channels)
        {
            string nodeName = channel.TargetNode.Name;
            
            // Check if this is a bone belonging to THIS character
            bool isBone = characterBoneInfoMap.ContainsKey(nodeName);
            
            // Check if this is a non-skinned node (global animation like camera)
            bool isNodeAnimation = _skinnedNodeNames != null 
                && !_skinnedNodeNames.Contains(nodeName);
            
            // CRITICAL: Only include if it's THIS character's bone OR a global node
            if (!isBone && !isNodeAnimation)
                continue; // Skip bones from other characters!
            
            // Process channel...
            if (isBone)
                boneChannelCount++;
            else
                nodeChannelCount++;
        }
        
        // Only add animation if it has channels for this character
        if (boneChannelCount > 0 || nodeChannelCount > 0)
            characterAnimations.Add(animation);
    }
    
    return characterAnimations;
}
```

**Result**:
- Multi-character models: Each character gets only its own animations
- Single-character models: Character gets all animations
- Node animations: Included in all characters (global animations)

#### 3. Global Skinned Node Tracking

**Critical Fix**: `_skinnedNodeNames` must collect joint names from **ALL skins**, not just the first one:

```csharp
private void BuildSkinnedNodeNames()
{
    _skinnedNodeNames = new HashSet<string>();
    
    // Collect joint names from ALL skins
    foreach (var skin in _model.LogicalSkins)
    {
        foreach (var joint in skin.JointsIndices)
        {
            var node = _model.LogicalNodes[joint];
            _skinnedNodeNames.Add(node.Name);
        }
    }
}
```

**Why This Matters**:
- Distinguishes between skinned bones and non-skinned animated nodes
- Enables correct handling of mixed bone+node animations (e.g., LittleTokio)
- Prevents marking bones from character 2 as "non-skinned" just because they're not in character 1's skin

#### 4. Node-Only Animation Support

For models **without skins** (e.g., CommercialRefrigerator with animated doors):

```csharp
private void ProcessNodeAnimations()
{
    if (_model.LogicalSkins.Count > 0)
        return; // Has skins, use character animations instead
    
    // Create a single animation with all node channels
    var nodeAnimation = new SharpGltfAnimation(...);
    
    foreach (var gltfAnimation in _model.LogicalAnimations)
    {
        foreach (var channel in gltfAnimation.Channels)
        {
            // Process all nodes (no bone filtering)
            var node = new SharpGltfBone(nodeName, -1, targetNode);
            nodeAnimation.AddBone(node);
        }
    }
    
    NodeAnimation = nodeAnimation;
}
```

**Result**: Models without skinning data can still have animated nodes.

#### 5. AnimatedCharacter Encapsulation

Each character is a self-contained animation unit:

```csharp
public class AnimatedCharacter
{
    public int SkinIndex { get; }
    public string Name { get; }
    public List<SharpGltfAnimation> Animations { get; }
    public int CurrentAnimationIndex { get; private set; }
    public SharpGltfAnimator Animator { get; }
    public List<Mesh> Meshes { get; }
    public Dictionary<string, BoneInfo> BoneInfoMap { get; }
    public int BoneCount { get; }
    public SkinningMode SkinningMode { get; set; }
    
    public void Update(float dt)
    {
        Animator.UpdateAnimation(dt);
    }
    
    public void SetAnimation(int index)
    {
        if (index >= 0 && index < Animations.Count)
        {
            CurrentAnimationIndex = index;
            Animator.SetAnimation(Animations[index]);
        }
    }
    
    public Matrix4x4[] GetBoneMatrices()
    {
        return Animator.GetFinalBoneMatrices();
    }
}
```

**Benefits**:
- Complete animation independence
- Per-character animation selection
- Per-character skinning mode (Uniform vs Texture)
- Clean API for rendering code

## Implementation Details

### Phase 1: Model Loading & Character Creation

**File**: `SharpGltfModel.cs` (Lines ~165-200)

```csharp
// Step 1: Build global skinned node names (from ALL skins)
_skinnedNodeNames = new HashSet<string>();
foreach (var skin in _model.LogicalSkins)
{
    foreach (var jointIndex in skin.JointsIndices)
    {
        var node = _model.LogicalNodes[jointIndex];
        _skinnedNodeNames.Add(node.Name);
    }
}

// Step 2: Create a character for each skin
for (int skinIndex = 0; skinIndex < _model.LogicalSkins.Count; skinIndex++)
{
    var skin = _model.LogicalSkins[skinIndex];
    
    // Build character-specific bone map (0-N indexing)
    var characterBoneInfoMap = new Dictionary<string, BoneInfo>();
    int boneCounter = 0;
    
    foreach (var (jointNode, invBindMatrix) in skin.GetEnumerable(rootNode))
    {
        string boneName = jointNode.Name;
        if (!characterBoneInfoMap.ContainsKey(boneName))
        {
            characterBoneInfoMap[boneName] = new BoneInfo(boneCounter++, invBindMatrix);
        }
    }
    
    // Get meshes for this skin
    var skinMeshes = Meshes.Where(m => m.SkinIndex == skinIndex).ToList();
    
    if (skinMeshes.Count == 0)
        continue; // No meshes for this skin
    
    // Process animations for THIS character ONLY
    var characterAnimations = ProcessAnimationsForCharacter(
        characterName,
        characterBoneInfoMap,
        rootNode);
    
    if (characterAnimations.Count == 0)
    {
        Warn($"No animations found for character '{characterName}'");
        continue;
    }
    
    // Create character
    var character = new AnimatedCharacter(
        skinIndex,
        characterName,
        characterAnimations,
        skinMeshes,
        MaterialToMeshMap,
        Nodes,
        characterBoneInfoMap,
        boneCounter);
    
    Characters.Add(character);
}

// Step 3: Handle models without skins (node-only animations)
if (_model.LogicalSkins.Count == 0 && _model.LogicalAnimations.Count > 0)
{
    ProcessNodeAnimations();
}
```

### Phase 2: Animation Processing with Character Filtering

**File**: `SharpGltfModel.cs` (Lines ~1450-1530)

The critical fix that prevents animation sharing between characters:

```csharp
private List<SharpGltfAnimation> ProcessAnimationsForCharacter(
    string characterName,
    Dictionary<string, BoneInfo> characterBoneInfoMap,
    SharpGltfNode rootNode)
{
    var characterAnimations = new List<SharpGltfAnimation>();
    
    foreach (var gltfAnimation in _model.LogicalAnimations)
    {
        var animation = new SharpGltfAnimation(duration, ticksPerSecond, rootNode, characterBoneInfoMap);
        
        int boneChannelCount = 0;
        int nodeChannelCount = 0;
        
        foreach (var channel in gltfAnimation.Channels)
        {
            string nodeName = channel.TargetNode.Name;
            
            // Check if this is THIS character's bone
            bool isBone = characterBoneInfoMap.ContainsKey(nodeName);
            
            // Check if this is a global non-skinned node
            bool isNodeAnimation = _skinnedNodeNames != null 
                && !_skinnedNodeNames.Contains(nodeName);
            
            // CRITICAL FIX: Skip bones/nodes that don't belong to this character
            if (!isBone && !isNodeAnimation)
                continue;
            
            int boneId = isBone ? characterBoneInfoMap[nodeName].Id : -1;
            
            if (isBone)
                boneChannelCount++;
            else
                nodeChannelCount++;
            
            // Create bone entry and store samplers
            var bone = new SharpGltfBone(nodeName, boneId, targetNode);
            bone.SetSamplers(
                channel.GetTranslationSampler(),
                channel.GetRotationSampler(),
                channel.GetScaleSampler());
            animation.AddBone(bone);
        }
        
        // Only add if it has channels for THIS character
        if (boneChannelCount > 0 || nodeChannelCount > 0)
        {
            characterAnimations.Add(animation);
        }
    }
    
    return characterAnimations;
}
```

**Key Points**:
1. Each character gets its own `List<SharpGltfAnimation>`
2. Animations are filtered to include only channels affecting that character
3. Bone channels: Must match `characterBoneInfoMap` (this character's bones)
4. Node channels: Must be non-skinned nodes (global animations)
5. Prevents character 1 from getting character 2's animations

### Phase 3: Rendering with Per-Character Updates

**File**: `Frame_Skinning.cs`

```csharp
public void Draw()
{
    // Update and render each character independently
    foreach (var character in state.model.Characters)
    {
        // Update this character's animation
        character.Update(state.deltaTime);
        
        // Get bone matrices for this character
        Matrix4x4[] boneMatrices = character.GetBoneMatrices();
        
        // Render all meshes for this character
        foreach (var mesh in character.Meshes)
        {
            RenderSkinnedMesh(mesh, boneMatrices, character.SkinningMode);
        }
    }
    
    // Render static meshes
    foreach (var mesh in state.model.StaticMeshes)
    {
        RenderStaticMesh(mesh);
    }
    
    // Render node-animated meshes (models without skins)
    if (state.model.NodeAnimation != null)
    {
        state.model.NodeAnimator.UpdateAnimation(state.deltaTime);
        // Render node-animated meshes...
    }
}
```

### Phase 4: GUI Controls (Per-Character)

**File**: `Frame_Gui.cs`

```csharp
private void DrawCharacterControls()
{
    foreach (var character in state.model.Characters)
    {
        if (igTreeNode($"{character.Name} (Skin {character.SkinIndex})"))
        {
            // Animation selection
            int currentAnim = character.CurrentAnimationIndex;
            if (igCombo("Animation", ref currentAnim, 
                GetAnimationNames(character.Animations)))
            {
                character.SetAnimation(currentAnim);
            }
            
            // Skinning mode selection
            int skinningMode = (int)character.SkinningMode;
            if (igCombo("Skinning Mode", ref skinningMode, 
                "Uniform\0Texture\0"))
            {
                character.SkinningMode = (SkinningMode)skinningMode;
            }
            
            igTreePop();
        }
    }
}
```

## Critical Bugs Fixed

### Bug #1: `_skinnedNodeNames` Only Populated from First Skin

**Problem**: When building `_skinnedNodeNames`, only the first skin's joints were added. This caused bones from other characters to be incorrectly marked as "non-skinned nodes".

**Solution**: Collect joint names from ALL skins:

```csharp
// BEFORE (broken):
var skin = _model.LogicalSkins[0]; // Only first skin!
foreach (var joint in skin.JointsIndices)
    _skinnedNodeNames.Add(...);

// AFTER (fixed):
foreach (var skin in _model.LogicalSkins) // ALL skins!
{
    foreach (var joint in skin.JointsIndices)
        _skinnedNodeNames.Add(...);
}
```

### Bug #2: Node Animations Not Working (No Skins)

**Problem**: Models without skins (e.g., CommercialRefrigerator) have animations but no characters were created, so animations were never processed.

**Solution**: Added `ProcessNodeAnimations()` for skinless models:

```csharp
if (_model.LogicalSkins.Count == 0 && _model.LogicalAnimations.Count > 0)
{
    ProcessNodeAnimations(); // Creates NodeAnimation and NodeAnimator
}
```

### Bug #3: Mixed Bone+Node Animations Not Working

**Problem**: Models like LittleTokio have BOTH skinned bones AND node animations, but only bone animations were processed (node channels were skipped).

**Solution**: Modified `ProcessAnimationsForCharacter` to include BOTH bone and node channels:

```csharp
bool isBone = characterBoneInfoMap.ContainsKey(nodeName);
bool isNodeAnimation = _skinnedNodeNames != null && !_skinnedNodeNames.Contains(nodeName);

// Include BOTH bones and nodes
if (!isBone && !isNodeAnimation)
    continue;
```

### Bug #4: Characters Sharing Animations

**Problem**: After fixing bug #3, ALL characters were getting ALL animations because node animation fix removed character-specific filtering.

**Solution**: Restore character-specific filtering while keeping node animation support:

```csharp
// Only include channels that are:
// 1. THIS character's bones (in characterBoneInfoMap), OR
// 2. Non-skinned nodes (global animations)

bool isBone = characterBoneInfoMap.ContainsKey(nodeName);
bool isNodeAnimation = _skinnedNodeNames != null && !_skinnedNodeNames.Contains(nodeName);

if (!isBone && !isNodeAnimation)
    continue; // Skip bones from other characters!

// Result: Character gets its own bone animations + global node animations
```

## Test Cases & Validation

### Test Case 1: Multi-Character Model (DancingGangster.glb)

**Expected**:
- 2 characters created, each with independent animations
- Each character has its own bone map (0-N indexing)
- GUI shows separate animation controls per character
- Characters can play different animations simultaneously

**Verified**: ✅ All characters have independent animation lists

### Test Case 2: Node-Only Animation (CommercialRefrigerator.glb)

**Expected**:
- No skins → No characters created
- NodeAnimation populated with door/shelf animations
- Animations play correctly

**Verified**: ✅ Node animations work without skins

### Test Case 3: Mixed Bone+Node (LittleTokio.glb)

**Expected**:
- 1 character created from skin
- Character animations include BOTH bone and node channels
- Both bone and node animations play simultaneously

**Verified**: ✅ Mixed animations work correctly

### Test Case 4: 100+ Bone Character

**Expected**:
- Character automatically uses texture-based skinning
- `character.SkinningMode` defaults to `SkinningMode.TextureBased`
- Can switch to uniform mode in GUI (if desired)

**Verified**: ✅ Automatic skinning mode selection works

## Performance Characteristics

### Memory Usage

- **Per Character**: `boneCount × 64 bytes` (Matrix4x4) for bone matrices
- **Example**: 2 characters with 80 bones each = 2 × 80 × 64 = 10,240 bytes (~10 KB)
- **Impact**: Negligible for typical multi-character scenes

### CPU Usage

- **Per Frame**: `O(bones₁) + O(bones₂) + ... = O(total bones)`
- **Benefit**: Better cache locality (each animator processes contiguous bone array)
- **Impact**: Neutral or slightly positive vs. single-animator approach

### GPU Usage

- **No Change**: Same total bone matrices uploaded, just organized per-character
- **Benefit**: Can skip characters outside view frustum independently

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        SharpGltfModel                           │
├─────────────────────────────────────────────────────────────────┤
│ + List<AnimatedCharacter> Characters                            │
│ + SharpGltfAnimation? NodeAnimation (for skinless models)       │
│ + SharpGltfAnimator? NodeAnimator                               │
│ + HashSet<string> SkinnedNodeNames (global joint names)         │
│ + List<Mesh> NonSkinnedMeshes                                   │
├─────────────────────────────────────────────────────────────────┤
│ - ProcessAnimationsForCharacter(name, boneMap, root)            │
│ - ProcessNodeAnimations()                                       │
│ - BuildSkinnedNodeNames()                                       │
└─────────────────────────────────────────────────────────────────┘
                            │ contains 0..N
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      AnimatedCharacter                          │
├─────────────────────────────────────────────────────────────────┤
│ + int SkinIndex                                                 │
│ + string Name                                                   │
│ + List<SharpGltfAnimation> Animations (character-specific)      │
│ + int CurrentAnimationIndex                                     │
│ + SharpGltfAnimator Animator (dedicated, 0-N bone indexing)    │
│ + Dictionary<string, BoneInfo> BoneInfoMap (0-N indices)        │
│ + List<Mesh> Meshes                                             │
│ + int BoneCount                                                 │
│ + SkinningMode SkinningMode (Uniform/Texture)                   │
├─────────────────────────────────────────────────────────────────┤
│ + Update(dt)                                                    │
│ + SetAnimation(index)                                           │
│ + GetBoneMatrices() → Matrix4x4[]                               │
└─────────────────────────────────────────────────────────────────┘
                            │ owns
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                     SharpGltfAnimator                           │
├─────────────────────────────────────────────────────────────────┤
│ - Matrix4x4[] _finalBoneMatrices (0-N for this character)      │
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

## Summary

This implementation provides:

1. ✅ **Multi-character support**: Each character has independent animation system with 0-N bone indexing
2. ✅ **Node animation support**: Models without skins can have animated nodes
3. ✅ **Mixed animations**: Models with both bone and node animations work correctly
4. ✅ **Character isolation**: Each character gets only its own animations (no sharing)
5. ✅ **Independent control**: Per-character animation selection and skinning mode
6. ✅ **Global skinned node tracking**: Correctly distinguishes bones from non-skinned nodes across all skins
7. ✅ **Clean architecture**: Self-contained AnimatedCharacter encapsulation
8. ✅ **Automatic skinning mode**: Texture-based for 100+ bones, uniform for fewer

**All animation scenarios work correctly**:
- Single-character models (backward compatible)
- Multi-character models (independent animations per character)
- Node-only animations (models without skins)
- Mixed bone+node animations (single character with both types)

---

*Last Updated: Implementation complete with all critical fixes applied*

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
