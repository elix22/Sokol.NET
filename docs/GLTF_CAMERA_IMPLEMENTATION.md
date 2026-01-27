# glTF Camera Implementation Guide

## Executive Summary

This document describes the implementation of a new `GltfCamera` class to support glTF camera definitions in the GltfViewer application. The current orbit camera system is incompatible with glTF's free-look camera paradigm, requiring a separate camera implementation.

## Problem Statement

### Current Camera System (Orbit Camera)
The existing `Camera` class in `examples/GltfViewer/Source/Camera.cs` implements an **orbit camera** with these characteristics:

- **Paradigm**: Always orbits around a fixed center point
- **Parameters**: 
  - `Center` (Vector3): The point the camera orbits around
  - `Distance` (float): Distance from center
  - `Latitude` (degrees): Vertical angle (-85° to +85°)
  - `Longitude` (degrees): Horizontal angle (0° to 360°)
- **Movement**: WASD moves the orbit center, mouse drag rotates around center
- **View Calculation**: Camera position is calculated from center + spherical coordinates

### glTF Camera System
glTF cameras use a **free-look camera** paradigm:

- **Paradigm**: Camera has explicit position and rotation, can look in any direction
- **Parameters**:
  - `translation` (Vector3): World-space position
  - `rotation` (Quaternion): Orientation as quaternion [x, y, z, w]
  - `perspective.yfov` (radians): Vertical field of view
  - `perspective.znear` (float): Near clipping plane
  - `perspective.zfar` (float): Far clipping plane
- **Direction**: glTF cameras look down the **-Z axis** in their local space
- **Movement**: Free 6DOF movement and rotation

### Example glTF Camera Data
From `Scene_TrainWayRunner.gltf`:
```json
{
  "cameras": [{
    "perspective": {
      "aspectRatio": 1.4402332305908203,
      "yfov": 1.0471975803375244,  // 60 degrees in radians
      "zfar": 1000.1340942382812,
      "znear": 0.30000001192092896
    },
    "type": "perspective",
    "name": "Camera"
  }],
  "nodes": [{
    "camera": 0,
    "rotation": [0.0, 1.0, 0.0, 0.0],  // 180° rotation around Y-axis
    "translation": [0.0, 2.58, -612.6247],
    "name": "Camera"
  }]
}
```

### Why Conversion Failed
Previous attempts to convert glTF camera parameters to orbit camera parameters failed because:

1. **Fundamental incompatibility**: An orbit camera cannot represent all possible camera orientations that a free-look camera can
2. **Matrix interpretation errors**: Initially used column-major matrix extraction when C# Matrix4x4 is row-major
3. **Arbitrary center point**: Computing a meaningful orbit center from a free direction is ambiguous
4. **Limited rotation**: Orbit camera latitude is clamped to ±85°, glTF cameras can look straight up/down

## Solution: Dedicated GltfCamera Class

Create a new `GltfCamera` class that maintains the glTF camera paradigm natively, without trying to convert to orbit camera parameters.

---

## GltfCamera Class Design

### File Location
`examples/GltfViewer/Source/GltfCamera.cs`

### Class Structure

```csharp
namespace GltfViewer;

/// <summary>
/// A free-look camera that maintains explicit position and rotation,
/// suitable for glTF camera definitions. Unlike the orbit camera,
/// this camera can move and rotate freely without being constrained
/// to orbit around a center point.
/// </summary>
public class GltfCamera
{
    // === Core Properties ===
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    
    // === Camera Parameters ===
    public float Fov { get; set; }        // Vertical FOV in degrees
    public float NearZ { get; set; }
    public float FarZ { get; set; }
    public float AspectRatio { get; set; }
    
    // === Movement Configuration ===
    public float MoveSpeed { get; set; }   // Units per second
    public float RotateSpeed { get; set; } // Rotation sensitivity
    
    // === Cached Direction Vectors ===
    private Vector3 _forward;
    private Vector3 _right;
    private Vector3 _up;
    
    public Vector3 Forward => _forward;
    public Vector3 Right => _right;
    public Vector3 Up => _up;
}
```

### Key Methods

#### 1. **Initialization**
```csharp
public void Init(Vector3 position, Quaternion rotation, float fov, float nearZ, float farZ)
```
- Initialize camera from glTF camera node data
- Sets position, rotation, and camera parameters
- Calls `UpdateVectors()` to cache direction vectors

#### 2. **Vector Updates**
```csharp
private void UpdateVectors()
```
- Transforms basis vectors by rotation quaternion
- glTF cameras look down -Z: `forward = Transform(-UnitZ, rotation)`
- Caches right, up, forward vectors for efficient access

#### 3. **Movement Methods**
```csharp
public void MoveForward(float delta)   // Move along forward direction
public void MoveRight(float delta)     // Move perpendicular to forward
public void MoveUp(float delta)        // Move along world Y-axis
```
- `delta` is normalized input (-1 to +1)
- Movement scaled by `MoveSpeed`
- Position updated directly without constraints

#### 4. **Rotation Method**
```csharp
public void Rotate(float deltaYaw, float deltaPitch)
```
- `deltaYaw`: Horizontal rotation (degrees)
- `deltaPitch`: Vertical rotation (degrees)
- Creates quaternion rotations for yaw (around world Y) and pitch (around local right)
- Applies rotations and updates cached vectors
- **No clamping** - can look straight up/down unlike orbit camera

#### 5. **Matrix Generation**
```csharp
public Matrix4x4 GetViewMatrix()
public Matrix4x4 GetProjectionMatrix()
```
- `GetViewMatrix()`: Creates look-at matrix from position, forward, up
- `GetProjectionMatrix()`: Creates perspective matrix from FOV, aspect, near/far

---

## Implementation Steps

### Step 1: Add GltfCamera to State
**File**: `examples/GltfViewer/Source/Main.cs` or wherever `State` is defined

**Current State Structure**:
```csharp
static State state = new()
{
    camera = new Camera(),  // Orbit camera
    // ... other fields
};
```

**Add New Fields**:
```csharp
static State state = new()
{
    camera = new Camera(),           // Keep for non-glTF scenes
    gltfCamera = null,               // GltfCamera? - used when glTF has camera
    usingGltfCamera = false,         // bool - flag to switch between cameras
    // ... other fields
};
```

### Step 2: Initialize GltfCamera from glTF Data
**File**: `examples/GltfViewer/Source/Frame.cs`

**Location**: Inside the camera initialization block (around line 730-840)

**Current Code Pattern**:
```csharp
if (state.model?.ModelRoot?.LogicalCameras?.Count > 0)
{
    var gltfCamera = state.model.ModelRoot.LogicalCameras[0];
    // ... existing orbit camera conversion code
}
```

**Replace With**:
```csharp
if (state.model?.ModelRoot?.LogicalCameras?.Count > 0)
{
    var gltfCameraDefinition = state.model.ModelRoot.LogicalCameras[0];
    
    Info($"[Camera] Using glTF camera: {gltfCameraDefinition.Name ?? "Unnamed"}");
    
    // Find the node that contains this camera
    SharpGLTF.Schema2.Node? cameraNode = null;
    foreach (var node in state.model.ModelRoot.LogicalNodes)
    {
        if (node.Camera == gltfCameraDefinition)
        {
            cameraNode = node;
            break;
        }
    }
    
    if (cameraNode != null)
    {
        // Extract world transform from camera node
        var worldMatrix = cameraNode.WorldMatrix;
        Matrix4x4.Decompose(worldMatrix, out var scale, out var rotation, out var position);
        
        // Get camera parameters
        float fov = 60.0f;  // Default
        float nearZ = 0.01f;
        float farZ = 1000.0f;
        
        var camSettings = gltfCameraDefinition.Settings;
        if (camSettings is SharpGLTF.Schema2.CameraPerspective perspective)
        {
            // Convert vertical FOV from radians to degrees
            fov = perspective.VerticalFOV * (180.0f / MathF.PI);
            nearZ = perspective.ZNear;
            farZ = float.IsPositiveInfinity(perspective.ZFar) ? farZ : perspective.ZFar;
        }
        
        // Create and initialize GltfCamera
        state.gltfCamera = new GltfCamera();
        state.gltfCamera.Init(position, rotation, fov, nearZ, farZ);
        state.gltfCamera.AspectRatio = (float)width / (float)height;
        state.usingGltfCamera = true;
        
        Info($"[Camera] Initialized glTF camera at position: {position}");
        Info($"[Camera] FOV: {fov:F2}°, Near: {nearZ}, Far: {farZ}");
        Info($"[Camera] Forward: {state.gltfCamera.Forward}");
    }
}
else
{
    // No glTF camera - use orbit camera as before
    state.camera.Init(new CameraDesc()
    {
        Aspect = 60.0f,
        NearZ = nearZ,
        FarZ = farZ,
        Center = state.modelBounds.Center,
        Distance = distance,
        Latitude = 0.0f,
        Longitude = 0.0f,
    });
    state.usingGltfCamera = false;
}
```

### Step 3: Update Input Handling
**File**: `examples/GltfViewer/Source/Frame.cs` in `Event()` function

**Current Input Code**:
```csharp
// Mouse drag - orbit
if (app_event.type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE)
{
    if (app_event.mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
    {
        state.camera.Orbit(app_event.mouse_dx, app_event.mouse_dy);
    }
}

// WASD - move orbit center
if (key == 'W') state.camera.Center += ...;
```

**Add Camera-Specific Input**:
```csharp
if (state.usingGltfCamera && state.gltfCamera != null)
{
    // === GltfCamera Input ===
    
    // Mouse drag - free-look rotation
    if (app_event.type == sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE)
    {
        if (app_event.mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
        {
            state.gltfCamera.Rotate(app_event.mouse_dx, -app_event.mouse_dy);
        }
    }
    
    // Keyboard - free movement
    float dt = (float)sapp_frame_duration();
    
    if (key == 'W') state.gltfCamera.MoveForward(dt);
    if (key == 'S') state.gltfCamera.MoveForward(-dt);
    if (key == 'A') state.gltfCamera.MoveRight(-dt);
    if (key == 'D') state.gltfCamera.MoveRight(dt);
    if (key == 'Q') state.gltfCamera.MoveUp(-dt);
    if (key == 'E') state.gltfCamera.MoveUp(dt);
}
else
{
    // === Orbit Camera Input (existing code) ===
    // ... current orbit camera input code
}
```

### Step 4: Update Camera Matrix Updates
**File**: `examples/GltfViewer/Source/Frame.cs` in rendering code

**Find Current Camera Update**:
```csharp
state.camera.Update(width, height);
```

**Replace With Conditional**:
```csharp
if (state.usingGltfCamera && state.gltfCamera != null)
{
    // Update aspect ratio
    state.gltfCamera.AspectRatio = (float)width / (float)height;
    
    // Get matrices directly
    state.viewMatrix = state.gltfCamera.GetViewMatrix();
    state.projMatrix = state.gltfCamera.GetProjectionMatrix();
    state.viewProjMatrix = state.viewMatrix * state.projMatrix;
    state.eyePos = state.gltfCamera.Position;
}
else
{
    // Use orbit camera
    state.camera.Update(width, height);
    state.viewMatrix = state.camera.View;
    state.projMatrix = state.camera.Proj;
    state.viewProjMatrix = state.camera.ViewProj;
    state.eyePos = state.camera.EyePos;
}
```

### Step 5: Update Shader Uniforms
**File**: Wherever uniforms are set (probably in `Frame.cs` rendering code)

**Ensure Eye Position is Set**:
```csharp
vs_params.eye_pos = state.eyePos;  // Works for both camera types now
```

### Step 6: Update ImGui Camera Info Display
**File**: `examples/GltfViewer/Source/Frame.cs` in ImGui rendering

**Current Display**:
```csharp
ImGui.Text($"Distance: {state.camera.Distance:F2}");
ImGui.Text($"Latitude: {state.camera.Latitude:F2}");
ImGui.Text($"Longitude: {state.camera.Longitude:F2}");
```

**Add Conditional Display**:
```csharp
if (state.usingGltfCamera && state.gltfCamera != null)
{
    ImGui.Text("Camera Type: glTF Free-Look");
    ImGui.Text($"Position: {state.gltfCamera.Position}");
    ImGui.Text($"Forward: {state.gltfCamera.Forward}");
    ImGui.Text($"FOV: {state.gltfCamera.Fov:F1}°");
    
    if (ImGui.Button("Switch to Orbit Camera"))
    {
        state.usingGltfCamera = false;
        // Optionally: position orbit camera to match current view
    }
}
else
{
    ImGui.Text("Camera Type: Orbit");
    ImGui.Text($"Distance: {state.camera.Distance:F2}");
    ImGui.Text($"Latitude: {state.camera.Latitude:F2}");
    ImGui.Text($"Longitude: {state.camera.Longitude:F2}");
    ImGui.Text($"Center: {state.camera.Center}");
    
    if (ImGui.Button("Switch to Free-Look"))
    {
        if (state.gltfCamera == null)
        {
            state.gltfCamera = new GltfCamera();
            state.gltfCamera.Init(
                state.camera.EyePos,
                Quaternion.Identity,
                state.camera.Aspect,
                state.camera.NearZ,
                state.camera.FarZ
            );
        }
        state.usingGltfCamera = true;
    }
}
```

---

## Technical Details

### Matrix4x4 in C# is Row-Major
**Critical**: C# System.Numerics.Matrix4x4 is **row-major**, not column-major like OpenGL.

To extract basis vectors from a Matrix4x4:
```csharp
// CORRECT for C# Matrix4x4 (row-major)
Vector3 right = new Vector3(matrix.M11, matrix.M12, matrix.M13);  // First row
Vector3 up    = new Vector3(matrix.M21, matrix.M22, matrix.M23);  // Second row
Vector3 back  = new Vector3(matrix.M31, matrix.M32, matrix.M33);  // Third row

// WRONG (this is column-major extraction)
Vector3 right = new Vector3(matrix.M11, matrix.M21, matrix.M31);  // Don't do this!
```

### glTF Camera Coordinate System
- **Right-handed coordinate system**
- **Camera looks down -Z axis** in local space
- To get forward direction from back: `forward = -back`
- World matrix M13, M23, M33 contain the back direction (Z-axis)
- Forward direction: `forward = -new Vector3(M31, M32, M33)` or `forward = Transform(-Vector3.UnitZ, rotation)`

### Quaternion Rotation [x, y, z, w]
Example: Camera rotation `[0, 1, 0, 0]` means:
- 180° rotation around Y-axis
- Converts forward from (0, 0, -1) to (0, 0, 1)
- Camera at (0, 2.58, -612) looking forward (+Z direction) toward origin

### Converting Radians to Degrees
glTF uses radians for angles, C# code typically uses degrees:
```csharp
float degrees = radians * (180.0f / MathF.PI);
float radians = degrees * (MathF.PI / 180.0f);
```

---

## Testing Plan

### Test Case 1: Scene_TrainWayRunner.gltf
- **Expected**: Camera positioned at (0, 2.58, -612.6247)
- **Expected**: Looking forward down the train track (+Z direction)
- **Expected**: FOV = 60°, Near = 0.3, Far = 1000.13
- **Expected**: Jack character visible ahead on the tracks
- **Expected**: WASD moves camera freely, mouse rotates view

### Test Case 2: physics_ball_pit.gltf
Check if this scene has a camera defined. If so:
- **Expected**: Camera matches position from glTF file
- **Expected**: Looking at the scene with correct angle

### Test Case 3: LittleTokio.gltf
- **Expected**: Falls back to orbit camera (no glTF camera defined)
- **Expected**: Auto-positioning works as before
- **Expected**: WASD moves orbit center, mouse orbits

### Test Case 4: Switch Between Camera Types
- **Action**: Load glTF with camera, switch to orbit mode in UI
- **Expected**: Smooth transition, orbit camera initialized from current view
- **Action**: Switch back to glTF camera
- **Expected**: Returns to original glTF camera position/rotation

---

## State Variables Summary

### New State Fields Needed
```csharp
// In State class/struct definition:
GltfCamera? gltfCamera;           // The glTF camera instance (null if not used)
bool usingGltfCamera;             // Flag: true = use gltfCamera, false = use camera

// Unified matrix fields (may already exist):
Matrix4x4 viewMatrix;             // View matrix from active camera
Matrix4x4 projMatrix;             // Projection matrix from active camera  
Matrix4x4 viewProjMatrix;         // Combined view-projection matrix
Vector3 eyePos;                   // Eye/camera position for shaders
```

### Existing State Fields (Keep)
```csharp
Camera camera;                    // Original orbit camera (still used for non-glTF scenes)
bool cameraInitialized;           // Whether camera has been set up
// ... other existing fields
```

---

## Migration Path

### Phase 1: Add GltfCamera class (file creation only)
- Create `GltfCamera.cs` with full implementation
- No changes to existing code
- Build to verify no compilation errors

### Phase 2: Add state fields
- Add `gltfCamera` and `usingGltfCamera` to State
- Initialize to null/false
- Application still uses orbit camera exclusively

### Phase 3: Add glTF camera detection
- Modify Frame.cs camera initialization
- Detect glTF camera and create GltfCamera instance
- Set `usingGltfCamera = true` when glTF camera found
- Application creates GltfCamera but doesn't use it yet

### Phase 4: Add camera switching logic
- Add conditional in Update/Render to call correct camera
- Add conditional matrix generation
- Application now uses GltfCamera when available

### Phase 5: Add input handling
- Add GltfCamera-specific input in Event()
- Test movement and rotation

### Phase 6: Add UI elements
- Update ImGui to show camera type
- Add camera switching button
- Polish and test

---

## Common Issues and Solutions

### Issue: Camera appears at origin instead of glTF position
**Cause**: WorldMatrix not being extracted correctly
**Solution**: Verify `Matrix4x4.Decompose()` is getting position from node's world matrix

### Issue: Camera is rotated 90° off
**Cause**: Using column-major extraction on row-major matrix
**Solution**: Use correct row extraction: `new Vector3(M11, M12, M13)` for first row

### Issue: Camera upside-down or mirrored
**Cause**: Not negating Z for forward direction
**Solution**: glTF cameras look down -Z, use `forward = -back`

### Issue: Movement too fast/slow
**Solution**: Adjust `GltfCamera.MoveSpeed` (default 5.0 units/second)

### Issue: Rotation too sensitive/sluggish  
**Solution**: Adjust `GltfCamera.RotateSpeed` (default 0.15)

### Issue: Can't see anything
**Cause**: Near/far planes wrong, or camera inside geometry
**Solution**: Check near/far values from glTF, ensure camera position makes sense

---

## Files Modified Summary

### New Files
1. `examples/GltfViewer/Source/GltfCamera.cs` - New camera class

### Modified Files
1. `examples/GltfViewer/Source/Main.cs` - Add state fields
2. `examples/GltfViewer/Source/Frame.cs` - Multiple changes:
   - Camera initialization (add glTF camera detection)
   - Input handling (add GltfCamera controls)
   - Update/Render (add camera switching logic)
   - ImGui UI (add camera info display)

### Files Verified (No Changes Expected)
1. `examples/GltfViewer/Source/Camera.cs` - Orbit camera unchanged
2. Shader files - No changes needed
3. Build files - No changes needed

---

## Additional Notes

### Performance
- GltfCamera caches direction vectors to avoid repeated quaternion transformations
- `UpdateVectors()` only called after rotation changes
- Matrix generation is cheap (one look-at, one perspective)

### Extensibility
Future enhancements could include:
- Camera animation support (interpolating between keyframes)
- Multiple camera switching (if glTF has multiple cameras)
- Camera smoothing/damping for movement
- Collision detection to prevent walking through geometry

### Compatibility
- Backward compatible: existing orbit camera still works for non-glTF scenes
- Can add manual toggle between camera modes
- No breaking changes to existing API

---

## Quick Reference: Key Code Locations

### Where to find State definition
Search for: `static State state = new()`
Or: `class State` / `struct State`

### Where to find camera initialization
File: `Frame.cs`
Search for: `state.model?.ModelRoot?.LogicalCameras`
Or: `AUTO-POSITIONING CAMERA`

### Where to find input handling  
File: `Frame.cs`
Function: `Event()`
Search for: `SAPP_EVENTTYPE_MOUSE_MOVE` or `state.camera.Orbit`

### Where to find rendering
File: `Frame.cs`
Function: `Frame()`
Search for: `state.camera.Update` or `sg_apply_uniforms`

---

## Conclusion

This implementation provides a clean separation between orbit camera (for general use) and glTF camera (for glTF-defined cameras), allowing the GltfViewer to correctly display scenes with camera definitions while maintaining backward compatibility with scenes that don't define cameras.

The key insight is that attempting to convert between these two camera paradigms is fundamentally flawed - instead, we support both paradigms natively and switch between them as needed.
