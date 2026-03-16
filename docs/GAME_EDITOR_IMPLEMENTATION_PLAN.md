# Game Editor — Detailed Implementation Plan

> **Based on**: Sokol.NET with .NET 10 AOT, Dear ImGui, cgltf, JoltPhysics, Box2D, and the CGltfViewer/JoltPhysics/box2dPhysics reference examples.
>
> **Current state of GameEditor**: Empty Sokol template — a single `GameEditor-app.cs` with a gradient background and no imgui or game logic.
>
> **Framework folder** (`src/Framework/`): Currently empty — all engine code shared between the editor and deployed games goes here.

---

## Implementation Status

> Last updated reflecting session work. ✅ = fully implemented, 🔧 = partially implemented, ⬜ = not started.

| Phase | Status | Notes |
|---|---|---|
| 0 — Project Skeleton | ✅ | Framework compile includes wired; ImGui docking + DockBuilder layout; main menu bar |
| 1a — AppLoop, Time, EventBus | ✅ | `AppLoop.cs`, `Time.cs`, `EventBus.cs` in `src/Framework/Core/` |
| 1b — Logger → Console Window | 🔧 | `Logger.cs` exists; Console Window ring buffer display pending |
| 2a — OffscreenTarget, RenderView | ✅ | Scene viewport renders into offscreen target displayed as `igImage` |
| 2b — PBR pipeline extract | 🔧 | Basic renderer; full PBR pipeline extract pending |
| 2c — Editor grid shader | ⬜ | Not started |
| 3 — ECS (EntityId, stores, query) | ✅ | `ECSWorld.cs` with `CreateEntity`, `AddComponent<T>`, `GetComponent<T>`, `Query<T>`, `DestroyEntity` |
| 4a — SceneGraph, parenting | ✅ | `Scene.cs`, `SceneManager.cs`; `Transform.Parent` hierarchy; recursive dirty propagation |
| 4b — Play/Pause/Stop state machine | ✅ | `SceneManager.PlayModeState`; snapshot/restore on play/stop via JSON serialisation |
| 5 — Physics: IPhysicsWorld | ✅ | `IPhysicsWorld` interface + `PhysicsBodyHandle`, `BodyDesc`, `RaycastHit` in `src/Framework/Physics/` |
| 5 — Jolt & Box2D impls | ⬜ | Concrete `JoltPhysicsWorld` / `Box2DPhysicsWorld` not yet connected |
| 6a — cgltf_write bindings | ⬜ | Not started |
| 6b/6c — Scene GLB serialisation | ⬜ | Currently uses JSON; GLB pending cgltf_write |
| 7 — GameBehaviour scripting | ✅ | `GameBehaviour.cs` base class; `OnStart/OnUpdate/OnDestroy/OnCollisionEnter` |
| 8a — Docking layout | ✅ | Full DockBuilder layout (Hierarchy / Scene / Inspector / Console / Project) |
| 8b — Scene Window | ✅ | Offscreen viewport; orbit/pan/zoom editor camera |
| 8c — Hierarchy Panel | ✅ | Recursive tree with `Leaf`-flag fix, drag-to-reparent, right-click context menu |
| 8d — Game Window | ✅ | Offscreen game view; Play/Pause/Stop toolbar |
| 8e — Inspector components | ✅ | All component drawers with undo support; `ActiveFlag` checkbox; `Remove Component` (✕) button; `ScriptComponent` display |
| 9 — Project management | ✅ | `ProjectConfig`/`ConfigManager`; New/Open Project dialogs; `BuildDeployPanel` with `Process`-based build streaming |
| 10a — ImGuizmo / picking | ✅ | `ext/ImGuizmo` submodule; `ext/imguizmo_wrapper.cpp` C wrapper; `src/imgui/ImGuizmo.cs` P/Invoke bindings; gizmo overlay in `SceneWindow` with undo; W/E/R shortcuts; T/R/S + Local/World toolbar buttons. **Requires native lib rebuild** (`scripts/build-xcode-macos.sh`). |
| 10b — Undo/Redo | ✅ | `IEditorCommand`, `DelegateCommand`, `UndoStack` (100-step); Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z |
| 10c — Prefabs | ⬜ | Not started |

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Phase 0 — Prerequisites & Project Skeleton](#phase-0--prerequisites--project-skeleton)
3. [Phase 1 — Core Layer](#phase-1--core-layer)
4. [Phase 2 — Renderer Layer](#phase-2--renderer-layer)
5. [Phase 3 — ECS Layer](#phase-3--ecs-layer)
6. [Phase 4 — Scene Layer](#phase-4--scene-layer)
7. [Phase 5 — Physics Layer](#phase-5--physics-layer)
8. [Phase 6 — Asset Layer (cgltf + cgltf_write)](#phase-6--asset-layer)
9. [Phase 7 — Scripting Layer](#phase-7--scripting-layer)
10. [Phase 8 — Editor UI (Dear ImGui Docking)](#phase-8--editor-ui)
11. [Phase 9 — Project Management & Deployment](#phase-9--project-management--deployment)
12. [Phase 10 — Editor-Specific Features](#phase-10--editor-specific-features)
13. [Implementation Sequence Summary](#implementation-sequence-summary)
14. [Cross-Cutting Concerns](#cross-cutting-concerns)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     GameEditor (examples/GameEditor)            │
│  ┌──────────┐ ┌────────────┐ ┌────────────┐ ┌──────────────┐  │
│  │  Scene   │ │  Hierarchy │ │  Inspector │ │   Console    │  │
│  │  Window  │ │   Panel    │ │   Panel    │ │   Window     │  │
│  └──────────┘ └────────────┘ └────────────┘ └──────────────┘  │
│  ┌──────────┐ ┌────────────┐ ┌────────────┐                    │
│  │  Game    │ │  Project   │ │  Editor    │                    │
│  │  Window  │ │  Settings  │ │  Settings  │                    │
│  └──────────┘ └────────────┘ └────────────┘                    │
│              Dear ImGui (Docking + Tabs)                        │
├─────────────────────────────────────────────────────────────────┤
│                   src/Framework  (shared)                       │
│  Core | Renderer | ECS | Scene | Physics | Assets | Scripting  │
├─────────────────────────────────────────────────────────────────┤
│          Sokol.NET bindings (src/sokol, src/imgui)              │
│     cgltf · JoltSharp · Box2D · fontstash · sokol-gp            │
└─────────────────────────────────────────────────────────────────┘
```

### Key design decisions

| Concern | Decision |
|---|---|
| Shared engine code | Lives in `src/Framework/` — compiled into both the editor and every deployed game |
| Rendering API | Sokol (`src/sokol/SG.cs`) — already cross-platform (Metal/HLSL/GL/GLES/WebGPU) |
| UI toolkit | Dear ImGui via existing `src/imgui/` bindings — docking already demonstrated in `examples/cimgui` |
| Physics | Jolt (3D) + Box2D (2D) — abstracted behind an `IPhysicsWorld` interface |
| GLTF I/O | cgltf (load) + cgltf_write (save, bindings to be generated) |
| Scripting | C# via Roslyn or external IDE (VS Code / Rider / Visual Studio) |
| Project creation | `SokolApplicationBuilder` via `CliWrap` |

---

## Phase 0 — Prerequisites & Project Skeleton

### 0.1 Framework Project Setup

**Task**: Create `src/Framework/Framework.csproj` — a class library compiled into every consumer (editor + deployed projects).

**Options**:
| Option | Description | Verdict |
|---|---|---|
| A) Single .csproj library | All engine code in one project, included by reference or `Compile Include` | ✅ Recommended — consistent with how `src/sokol` and `src/imgui` are included via `Compile Include` in `Directory.Build.props` |
| B) Multiple sub-projects (Core, Renderer, etc.) | Better separation but more MSBuild complexity | Too early — premature complexity |

**Recommended approach**: Mirror how `src/sokol/` is consumed. Add `Compile Include` entries to `examples/GameEditor/Directory.Build.props` for each `src/Framework/**/*.cs` subfolder.

**Action items**:
- Create folder structure in `src/Framework/`:
  ```
  src/Framework/
    Core/
    Renderer/
    ECS/
    Scene/
    Physics/
    Assets/
    Scripting/
  ```
- Extend `examples/GameEditor/Directory.Build.props` with:
  ```xml
  <ItemGroup>
    <Compile Include="../../src/Framework/**/*.cs">
      <Link>Framework/%(RecursiveDir)%(Filename)%(Extension)</Link>
    </Compile>
  </ItemGroup>
  ```

### 0.2 Enable ImGui Docking in GameEditor

**Task**: Wire up sokol-imgui and enable the docking flag, matching the pattern in `examples/cimgui/Source/cimgui-app.cs`.

```csharp
// In Init()
simgui_setup(new simgui_desc_t { logger = { func = &slog_func } });
ImGuiIO* io = igGetIO_Nil();
io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;
io->ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; // optional: detach panels to OS windows
```

Also wire up `simgui_new_frame`, `simgui_render` and `simgui_handle_event` in Frame and Event callbacks — exactly as in the CGltfViewer `Frame.cs` and `Event.cs`.

### 0.3 ShaderSlang Configuration

GameEditor already has `Directory.Build.props` with `ShaderSlang` and `CompileShaders` target. Add any custom shaders (editor grid, gizmo outlines, selection highlight) to `shaders/` as `.glsl` files. Run `dotnet build GameEditor.csproj -t:CompileShaders` to compile them.

---

## Phase 1 — Core Layer

**Location**: `src/Framework/Core/`

### 1.1 Application Loop

The Sokol `sapp_desc` callback model (`init_cb`, `frame_cb`, `event_cb`, `cleanup_cb`) already provides the application loop. The Core layer wraps this cleanly.

**Recommended structure**:
```
Application.cs        — static entry point, owns the sokol_main() sapp_desc
AppLoop.cs            — calls Core, Renderer, ECS, Scene update in correct order
Time.cs               — DeltaTime, TotalTime, FrameCount (read SApp.sapp_frame_duration)
```

**AppLoop update order** (per frame):
1. `simgui_new_frame()` — start ImGui frame
2. `EventBus.Flush()` — dispatch queued events
3. `PhysicsWorld.Step(dt)` — fixed/variable timestep physics
4. `ECSWorld.Update(dt)` — system ticks (scripts, transforms)
5. `SceneGraph.LateUpdate()` — propagate dirty transforms
6. `Renderer.Render(scene, camera)` — sokol render passes
7. `EditorUI.Draw()` — ImGui panels
8. `simgui_render()` — flush ImGui draw to sokol

### 1.2 Event Bus

**Options**:
| Option | Description | Verdict |
|---|---|---|
| A) Simple C# delegates/events | Easy, no allocation concern with AOT | ✅ Recommended for v1 |
| B) Ring-buffer message queue | Better for decoupling async events | Add later if needed |

**Minimum events needed**:
- `EntitySelected(EntityId)`
- `EntityCreated(EntityId)`, `EntityDestroyed(EntityId)`
- `ComponentChanged(EntityId, Type)`
- `SceneLoaded(ScenePath)`, `SceneUnloaded()`
- `PlayModeChanged(PlayModeState)`

### 1.3 Logging

**Options**:
| Option | Description | Verdict |
|---|---|---|
| A) Wrap existing `SLog` (`slog_func` already in `src/sokol/SLog.cs`) | Zero overhead, captures sokol messages | ✅ Recommended |
| B) Microsoft.Extensions.Logging | Full-featured but heavy for AOT | Overkill |

Extend `SLog` with `Warning()` / `Info()` / `Error()` helpers (already partially present in `CGltfViewer/Source/Init.cs`) into a shared `Logger.cs`. Route log output to both the system console and the editor's Console Panel.

### 1.4 Memory Management

.NET AOT GC handles most allocations. Explicit management is only required for:
- `cgltf_data*` (IDisposable pattern — already implemented in `CGltfModel.cs`)
- `GCHandle` pins for native buffers (already in `CGltfModel.ParsedGltf`)
- Sokol resources (`sg_buffer`, `sg_image`, etc.) — tracked in a `ResourceRegistry` that calls `sg_destroy_*` on scene unload

---

## Phase 2 — Renderer Layer

**Location**: `src/Framework/Renderer/`

This is the most complex layer. The CGltfViewer already has a production-grade PBR renderer. The strategy is to **extract and generalize** it rather than rewrite.

### 2.1 Render Pipeline Architecture

**Options**:
| Option | Description | Verdict |
|---|---|---|
| A) Copy CGltfViewer renderer as-is | Fast start, works immediately | First pass ✅ |
| B) Generalized RenderPipeline + RenderPass abstraction | Cleaner, supports multiple cameras (scene + game view) | Target architecture |
| C) Fully data-driven render graph | Maximum flexibility, high complexity | Too early |

**Recommended path**: Start with Option A (copy), then refactor toward B as the scene and game windows each require their own offscreen render pass.

### 2.2 Offscreen Rendering (Scene Window + Game Window)

Both the Scene Window and Game Window render into an offscreen texture, which is then displayed as an ImGui `igImage`. This is the crucial architectural difference from the standalone CGltfViewer.

**Implementation**:
```
OffscreenTarget.cs  — wraps sg_image (color + depth) + sg_pass
RenderView.cs       — camera + OffscreenTarget + viewport rect
```

Each panel (Scene/Game) owns a `RenderView`. The frame loop renders each active view into its offscreen target, then ImGui displays the resulting texture via `igImage`.

Reference: `examples/imgui_usercallback/Source/imgui-usercallback-sapp.cs` shows rendering into sokol passes and compositing with ImGui.

### 2.3 Shaders

Copy shaders from `examples/CGltfViewer/shaders/`:
- `pbr.glsl` — PBR surface shader (standard + skinned)
- `ibl.glsl` — Image-Based Lighting
- `brdf.glsl` — BRDF pre-integration
- `bloom.glsl` — Bloom post-process
- `tonemapping.glsl` — Tone mapping
- `cubemap.glsl` — Skybox / environment

Additional editor-only shaders to create:
- `grid.glsl` — Infinite editor grid (infinite plane grid via fragment shader distance)
- `outline.glsl` — Selection outline / stencil highlight
- `gizmo.glsl` — ImGuizmo custom depth (if not using ImGuizmo's built-in rendering)
- `wireframe.glsl` — Optional wireframe overlay

Add all to `examples/GameEditor/Directory.Build.props` `<ShaderFiles>` ItemGroup.

### 2.4 Batched 2D Rendering

For 2D game support, integrate the existing `sokol_gp` extension (already in `ext/sokol_gp/`, referenced in `examples/sokol_gp_*`).

**Options**:
| Option | Description | Verdict |
|---|---|---|
| A) sokol_gp | 2D primitive & sprite batcher, already integrated in Sokol.NET | ✅ Recommended |
| B) Custom sprite batcher | Full control | Only if sokol_gp is insufficient |

### 2.5 Text Rendering

Fontstash is available (`ext/fontstash/`, `examples/fontstash/`). Use it for in-world text and HUD labels.

### 2.6 Lights

Generalise the `Light.cs` from CGltfViewer. Support:
- Directional, Point, Spot (matching the Inspector component list)
- Maximum `RenderingConstants.MAX_LIGHTS` per scene
- Pass light array into PBR uniform buffer

---

## Phase 3 — ECS Layer

**Location**: `src/Framework/ECS/`

### 3.1 ECS Options

| Option | Description | Verdict |
|---|---|---|
| A) **Custom minimal ECS** — archetype-based | Full control, AOT-friendly, no external dependency | ✅ Recommended |
| B) Arch (NuGet `Arch`) | Production .NET ECS, archetype-based, AOT support in recent versions | Good choice if custom feels heavy |
| C) DefaultEcs | Simple, fast, AOT-friendly | Simpler but less powerful |
| D) Unity-style `MonoBehaviour` simulation (OOP, not ECS) | Familiar for Unity devs | Not cache-friendly, not recommended |

**Recommended for v1**: A custom minimal ECS with a flat `EntityId` (int handle) and component stores as typed arrays. This is AOT-safe, has zero NuGet dependencies, and can be incrementally made more sophisticated.

### 3.2 Minimum API

```csharp
// World
EntityId entity = ECSWorld.CreateEntity();
ECSWorld.AddComponent<Transform>(entity, new Transform(...));
ref Transform t = ref ECSWorld.GetComponent<Transform>(entity);
ECSWorld.DestroyEntity(entity);

// Component iteration
foreach (var (id, t, mesh) in ECSWorld.Query<Transform, MeshRenderer>())
    Renderer.Submit(id, t, mesh);
```

### 3.3 Built-in Components

Implement as plain `struct` or `class` records:

| Component | Key fields |
|---|---|
| `Transform` | `Position (Vector3)`, `Rotation (Quaternion)`, `Scale (Vector3)`, `Parent (EntityId?)`, `Children (List<EntityId>)` |
| `MeshRenderer` | `MeshAssetRef`, `MaterialSlots[]`, `CastShadows`, `ReceiveShadows` |
| `Camera` | `FOV`, `NearClip`, `FarClip`, `ProjectionType (Perspective/Ortho)`, `ClearFlags`, `CullingMask` |
| `Light` | `Type`, `Color`, `Intensity`, `Range`, `SpotAngle` |
| `Rigidbody` | See requirements doc — full property list |
| `Collider` | `Box/Sphere/Capsule/Mesh`, `IsTrigger`, `Material` |
| `ScriptComponent` | `ScriptTypeName`, `Fields (Dictionary<string,object>)` |
| `NameTag` | `Name (string)` |
| `ActiveFlag` | `IsActive (bool)` |

---

## Phase 4 — Scene Layer

**Location**: `src/Framework/Scene/`

### 4.1 Scene Graph

A scene contains a flat list of entities (ECS IDs) with a parenting hierarchy expressed via `Transform.Parent`.

```
Scene.cs           — entity registry, name lookup, dirty tracking
SceneManager.cs    — load/unload/save scenes, play-mode state machine
Prefab.cs          — snapshot of an entity subtree; instantiate/serialize
```

### 4.2 Scene Serialization — GLTF Extension Approach

The requirement mandates GLTF/GLB as the scene format with custom extensions. This is the most nuanced part of the asset layer.

**Options**:
| Option | Description | Verdict |
|---|---|---|
| A) **Custom GLTF extensions** via cgltf_write | Standard GLTF + vendor extensions encode all engine components | ✅ Recommended as per requirements |
| B) JSON (System.Text.Json) | Simple, no external format | Breaks GLTF ecosystem compatibility |
| C) Binary custom format | Fast but opaque | No third-party tooling |

**Extension schema (examples)**:

```json
// Node-level extension (on each GLTF node)
"extensions": {
  "SOKOLNET_entity": {
    "entityId": 42,
    "active": true,
    "components": {
      "Rigidbody": { "mass": 1.0, "useGravity": true, ... },
      "ScriptComponent": { "typeName": "PlayerController", "fields": {...} }
    }
  }
}

// Root-level extension (on the GLTF asset)
"extensions": {
  "SOKOLNET_scene": {
    "version": "1.0",
    "defaultCamera": "MainCamera",
    "physicsGravity": [0, -9.81, 0],
    "layers": [...]
  }
}
```

**cgltf_write binding generation** (required step — see Phase 6.2).

### 4.3 Play-Mode State Machine

```
Stopped → Playing → Paused → Playing → Stopped
```

- **Play**: Serialize current scene to memory → begin scene, physics, scripts
- **Pause**: Freeze physics/scripts, allow Inspector edits
- **Stop**: Deserialize + restore pre-play scene state (discard all runtime changes)

Managed by `SceneManager.PlayModeState` enum + events on `EventBus`.

---

## Phase 5 — Physics Layer

**Location**: `src/Framework/Physics/`

### 5.1 Physics Abstraction ✅

> **Implemented**: `src/Framework/Physics/IPhysicsWorld.cs` — interface + `PhysicsBodyHandle(int Value)`, `BodyDesc`, `RaycastHit` structs. Concrete `JoltPhysicsWorld` and `Box2DPhysicsWorld` remain to be wired up.

The requirement explicitly asks for a physics-engine-agnostic interface.

```csharp
public interface IPhysicsWorld
{
    void Initialize(PhysicsConfig config);
    void Step(float deltaTime);
    PhysicsBodyHandle CreateBody(BodyDesc desc);
    void DestroyBody(PhysicsBodyHandle handle);
    void SetBodyLinearVelocity(PhysicsBodyHandle handle, Vector3 velocity);
    Vector3 GetBodyPosition(PhysicsBodyHandle handle);
    Quaternion GetBodyRotation(PhysicsBodyHandle handle);
    // Raycasts, overlap queries, etc.
    bool Raycast(Ray ray, out RaycastHit hit, float maxDistance);
}
```

### 5.2 3D Physics — Jolt

**Reference**: `examples/JoltPhysics/Source/JoltPhysics-app.cs` — already uses `JoltPhysicsSharp` (NuGet), demonstrates `PhysicsSystem`, `BodyInterface`, `JobSystemThreadPool`.

**Recommended implementation**:
```
JoltPhysicsWorld.cs   — implements IPhysicsWorld using JoltPhysicsSharp
JoltBodyHandle.cs     — wraps BodyID
```

The JoltPhysics example is fully working on desktop. WebAssembly support is tracked in `examples/JoltPhysics/WEBASSEMBLY_STATUS.md`.

### 5.3 2D Physics — Box2D

**Reference**: `examples/box2dPhysics/Source/box2d-app.cs` — uses `static Sokol.Box2D`, demonstrating `b2WorldId`, `b2BodyId`.

```
Box2DPhysicsWorld.cs  — implements IPhysicsWorld for 2D
Box2DBodyHandle.cs
```

### 5.4 Selecting Physics at Runtime

```csharp
// In project config.json
{ "physics": { "engine3D": "jolt", "engine2D": "box2d" } }

// In SceneManager
IPhysicsWorld physics3D = config.Physics3D == "jolt"
    ? new JoltPhysicsWorld()
    : throw new NotSupportedException();
```

---

## Phase 6 — Asset Layer

**Location**: `src/Framework/Assets/`

### 6.1 Asset Manager

```
AssetManager.cs       — load/cache/unload assets by path
AssetHandle<T>        — typed handle with ref-counting
TextureCache.cs       — already implemented in CGltfViewer, extract to Framework
MeshCache.cs          — deduplicate meshes by source path + submesh index
```

**Supported asset types (v1)**:
- GLTF/GLB (models + scenes) — via cgltf
- PNG / JPEG textures — via stb (already in `ext/stb/`)
- Fonts — via fontstash
- Audio — deferred (not in scope for v1)

### 6.2 cgltf_write Binding Generation

This is required for saving scenes as GLB files.

**Step-by-step**:

1. Inspect the existing `bindgen/` Python scripts:
   - `gen.py` — entry point
   - `gen_ir.py` — parses C headers to IR
   - `gen_csharp.py` — emits C# from IR

2. Run the binding generator against `ext/cgltf/cgltf_write.h`:
   ```bash
   cd bindgen
   python3 gen.py --input ../ext/cgltf/cgltf_write.h --output ../src/sokol/generated/cgltf_write.cs
   ```
   The existing `src/sokol/CGltf.cs` shows the expected output pattern.

3. Key functions to expose:
   - `cgltf_write_file(options*, path*, data*)` — write GLB/GLTF to disk
   - `cgltf_write(options*, buffer, size, data*)` — write to memory buffer

4. After generation, validate that `cgltf_write.cs` compiles without errors in the GameEditor project.

### 6.3 Custom GLTF Extensions for Scene Data

Implement `SceneGltfSerializer.cs` and `SceneGltfDeserializer.cs`:

- **Save**: Iterate ECS entities → emit GLTF nodes → attach `SOKOLNET_entity` extension JSON to each node
- **Load**: Parse GLTF via cgltf → for each node reconstruct ECS entities → call `CGltfExtensionRegistry` hooks to process `SOKOLNET_entity`

This reuses the existing extension hook system from `CGltfExtensionRegistry.cs` (already present in CGltfViewer).

---

## Phase 7 — Scripting Layer

**Location**: `src/Framework/Scripting/`

### 7.1 C# Script Model

Scripts are C# classes that derive from `GameBehaviour` (analogous to Unity's MonoBehaviour):

```csharp
public class PlayerController : GameBehaviour
{
    public float Speed = 5.0f;

    public override void OnStart() { }
    public override void OnUpdate(float dt) { }
    public override void OnDestroy() { }
    public override void OnCollisionEnter(PhysicsBodyHandle other) { }
}
```

### 7.2 Script Compilation Options

| Option | Description | Verdict |
|---|---|---|
| A) **External IDE** (VS Code, Rider, Visual Studio) with standard `dotnet build` | No in-editor compilation. Player/editor restarts to pick up script changes | ✅ Recommended for v1 — this is exactly what requirements specify |
| B) Roslyn in-process compilation | Hot-reload without restart | Complex, AOT limitations |
| C) Roslyn + AssemblyLoadContext hot-swap | True hot-reload | Significant complexity, runtime-only (not AOT) |

**Recommended for v1**: Scripts live in the deployed project alongside game code. The editor invokes the project build via `CliWrap → SokolApplicationBuilder`. When a script changes, the developer rebuilds in their IDE and restarts (or the editor detects file changes and triggers a rebuild).

### 7.3 Script Discovery

At editor startup, scan the project directory for classes inheriting `GameBehaviour`. Use reflection (compatible with AOT via source generators or `[DynamicDependency]` attributes) or maintain a manifest file.

### 7.4 Inspector Integration

Public fields of `GameBehaviour` subclasses are displayed in the Inspector Panel as editable fields. Use reflection-based drawer or a code-generated property bag.

---

## Phase 8 — Editor UI

**Location**: `examples/GameEditor/Source/` (editor-only code, not in Framework)

### 8.1 ImGui Docking Layout

Dear ImGui docking (already demonstrated in `examples/cimgui`) allows a Unity-like panel layout.

**Initial layout bootstrap** (called once on first launch):

```csharp
// Setup docking layout with DockBuilder API
uint dockspaceId = igDockSpaceOverViewport(/* ... */);
igDockBuilderAddNode(dockspaceId, ImGuiDockNodeFlags.DockSpace);

// Split into regions
uint leftId, centerId, rightId, bottomId;
igDockBuilderSplitNode(dockspaceId, ImGuiDir.Left, 0.2f, out leftId, out centerId);
igDockBuilderSplitNode(centerId, ImGuiDir.Right, 0.25f, out rightId, out centerId);
igDockBuilderSplitNode(centerId, ImGuiDir.Down, 0.3f, out bottomId, out centerId);

igDockBuilderDockWindow("Hierarchy", leftId);
igDockBuilderDockWindow("Scene", centerId);
igDockBuilderDockWindow("Game", centerId);     // second tab in same area
igDockBuilderDockWindow("Inspector", rightId);
igDockBuilderDockWindow("Console", bottomId);
igDockBuilderDockWindow("Project", bottomId);  // second tab
igDockBuilderFinish(dockspaceId);
```

Save layout to `editor-layout.ini` via `igSaveIniSettingsToDisk`.

### 8.2 Scene Window

**Responsibilities**:
- Render scene from editor viewport camera into `OffscreenTarget`
- Display as `igImage` inside an ImGui window
- Overlay ImGuizmo gizmos for transform manipulation

**Editor Viewport Camera**: Extract and generalize the `Camera.cs` orbit/pan/zoom from CGltfViewer. It already handles mouse drag (orbit), scroll (zoom), middle-mouse (pan), WASD movement, and touch input.

**ImGuizmo Integration** ✅:
- `ext/ImGuizmo` submodule added (https://github.com/elix22/ImGuizmo.git)
- `ext/imguizmo_wrapper.cpp` — thin `extern "C"` shim exposing: `imguizmo_begin_frame`, `imguizmo_set_rect`, `imguizmo_manipulate`, `imguizmo_is_over`, `imguizmo_is_using`, `imguizmo_decompose_matrix`, `imguizmo_recompose_matrix`
- `ext/CMakeLists.txt` updated — `ImGuizmo/ImGuizmo.cpp` + wrapper compiled into `libsokol`; `cimgui/imgui` added as private include so `imgui.h` resolves
- `src/imgui/ImGuizmo.cs` — AOT-safe P/Invoke bindings with `ImGuizmo.Operation` and `ImGuizmo.Mode` enums
- **Matrix convention**: System.Numerics is row-major; ImGuizmo expects column-major. `Matrix4x4.Transpose()` applied to View/Proj/entity matrices before Manipulate; `DecomposeMatrix` called on the output (still column-major) to extract position/EulerAngles/scale
- `SceneWindow.DrawGizmo()` — called after `igImage`; records undo entry on drag-end via `UndoStack.RecordAlreadyExecuted`
- **Gizmo shortcuts**: W = Translate, E = Rotate, R = Scale (wired in `GameEditor-app.cs` Event handler)
- **Toolbar**: T/R/S radio buttons + Local/World toggle on right side of menu bar
- ⚠️ **Requires native rebuild**: run `scripts/build-xcode-macos.sh` to recompile `libsokol.dylib` with ImGuizmo included

**Toolbar**: Play / Pause / Stop buttons, gizmo mode toggles (Translate W, Rotate E, Scale R), local/world space toggle.

**Picking**: On left-click in Scene viewport, render scene with solid entity-ID colors into a 1x1 offscreen buffer at the cursor position (CPU readback). Map pixel color → EntityId → select in Hierarchy + Inspector.

**Multiple Scene Windows**: Each open scene gets a `SceneWindowState` with its own `OffscreenTarget` and editor camera. Windows share the ECS world but operate on different active scenes.

### 8.3 Game Window

- Renders from the scene's active Camera component into a dedicated `OffscreenTarget`
- Displays as `igImage`
- Controlled by Play/Pause/Stop state machine (see Phase 4.3)
- Resolution selector dropdown (16:9, 4:3, custom)

### 8.4 Hierarchy Panel ✅

> **Implemented** in `examples/GameEditor/Source/UI/HierarchyPanel.cs`. Recursive child rendering with correct `Leaf` flag (set only when `hasChildren == false`). Drag-to-reparent working. Right-click context menu (Create Entity, Delete, Rename, Duplicate).

```csharp
// Recursive tree render
void DrawEntityNode(EntityId id)
{
    var name = ECSWorld.GetComponent<NameTag>(id).Name;
    var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
    if (selectedEntity == id) flags |= ImGuiTreeNodeFlags.Selected;

    bool open = igTreeNodeEx_Str(name, flags);
    
    // Drag source/target for reparenting
    if (igBeginDragDropSource(...)) { igSetDragDropPayload("ENTITY_ID", ...); }
    if (igBeginDragDropTarget(...)) { /* reparent on drop */ }
    
    // Right-click context menu
    if (igBeginPopupContextItem(...)) { DrawHierarchyContextMenu(id); }
    
    if (open) {
        foreach (var child in GetChildren(id)) DrawEntityNode(child);
        igTreePop();
    }
}
```

Icons: Use Dear ImGui's font atlas with icon glyphs (e.g., Font Awesome via `fontstash` or `ImGui_PushFont` with icon font).

### 8.5 Inspector Panel ✅

> **Implemented** in `examples/GameEditor/Source/UI/InspectorPanel.cs` + `ComponentDrawers.cs`.
> - `ActiveFlag` checkbox at the top of the Inspector (with undo)
> - Each component header has a `✕` **Remove Component** button (calls `world.RemoveComponent<T>` + `UndoStack.Clear()`)
> - **Add Component** popup: all component types listed; each add is undoable via `UndoStack.Record`
> - `ScriptComponent` shows `TypeName` read-only label via `igTextDisabled`
> - All drag-float fields use `igIsItemDeactivatedAfterEdit()` → `UndoStack.RecordAlreadyExecuted` pattern
> - Instant-toggle checkboxes and combos use `UndoStack.Record()` directly

Draw all ECS components of the selected entity as collapsible `igCollapsingHeader` sections. Each component type has a dedicated **drawer**:

```
ComponentDrawers.cs
  DrawTransform()         ✅ — drag floats for position/rotation/scale with undo
  DrawMeshRenderer()      ✅ — mesh/material display (asset picker pending)
  DrawCamera()            ✅ — FOV, clip, projection type with undo
  DrawRigidbody()         ✅ — all Rigidbody properties with undo
  DrawLight()             ✅ — LightType combo, color, intensity, range with undo
  DrawScriptComponent()   ✅ — TypeName read-only label
  GenericDrawer           ⬜ — fallback reflection-based drawer (pending)
```

**Add Component button**: `igButton("Add Component")` opens a searchable popup listing all registered component types.

### 8.6 Console Window

```
ConsoleWindow.cs   — ring buffer of LogEntry(level, message, timestamp, stacktrace)
```

- Filter buttons: Info / Warning / Error
- Clear button
- Auto-scroll toggle
- Double-click entry → open file in IDE

Route `Logger.cs` output here.

### 8.7 Project Settings Window

Editable fields for `config.json`:
- Default scene
- Default main camera
- Screen size
- Physics settings
- Platform-specific overrides (Android/iOS/Web)

### 8.8 Editor Settings Window

- Theme (Dark / Light — already in CGltfViewer `GUI.cs`)
- IDE path for script editing
- Build output directory

---

## Phase 9 — Project Management & Deployment

### 9.1 config.json Schema

Every project contains `config.json`:

```json
{
  "version": "1.0",
  "projectName": "MyGame",
  "defaultScene": "Scenes/Main.glb",
  "defaultCamera": "MainCamera",
  "screenWidth": 1280,
  "screenHeight": 720,
  "physics": {
    "engine3D": "jolt",
    "engine2D": "box2d",
    "gravity": [0.0, -9.81, 0.0]
  },
  "platforms": {
    "android": { "packagePrefix": "com.yourcompany", "minSdk": 26 },
    "ios": { "bundleId": "com.yourcompany.mygame" },
    "web": { "canvasWidth": 960, "canvasHeight": 540 }
  }
}
```

### 9.2 Creating a New Project

The editor calls `SokolApplicationBuilder` via `CliWrap`:

```csharp
using CliWrap;

await Cli.Wrap("dotnet")
    .WithArguments(new[] {
        "run", "--project", sokolAppBuilderPath, "--",
        "--task", "create",
        "--project", projectName,
        "--destination", destinationPath
    })
    .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Logger.Info(line)))
    .ExecuteAsync();
```

The `CreateProjectTask.cs` in SokolApplicationBuilder already validates project names, checks that destination is outside the Sokol.NET repository, and copies from `templates/template_app`.

### 9.3 Deployment Targets

| Target | SokolApplicationBuilder task | Architecture flag |
|---|---|---|
| Desktop (macOS/Win/Linux) | `prepare` | `--architecture desktop` |
| Android APK | `AndroidBuild` | `--architecture android` |
| Android AAB (Play Store) | `AndroidBuildRelease` | `--architecture android` |
| iOS | `iOSBuild` | `--architecture ios` |
| Web (WASM) | `prepare` | `--architecture web` |

The editor UI presents a **Build & Deploy** panel that fills in `SokolApplicationBuilder` CLI arguments and streams output to the Console Window.

---

## Phase 10 — Editor-Specific Features

### 10.1 Asset Browser Panel (future)

- File tree rooted at project `Assets/` folder
- Drag-and-drop to Hierarchy or Inspector fields
- Thumbnail preview for textures (render via sokol → `igImage`)

### 10.2 Undo/Redo System ✅

> **Implemented**:
> - `src/Framework/Core/IEditorCommand.cs` — `IEditorCommand` interface (`Description`, `Execute()`, `Undo()`) + `DelegateCommand` AOT-safe closure implementation
> - `src/Framework/Core/UndoStack.cs` — `Record(cmd)` (execute + push), `RecordAlreadyExecuted(cmd)` (push-only for live ImGui drags), `Undo()`, `Redo()`, `Clear()`. 100-item cap. Static singleton.
> - Keyboard shortcuts: **Ctrl+Z** (undo), **Ctrl+Y** / **Ctrl+Shift+Z** (redo) wired in `GameEditor-app.cs` `Event()` handler
> - `UndoStack.Clear()` called on `NewScene()` and `LoadScene()` in `SceneManager.cs`

**Options**:
| Option | Description | Verdict |
|---|---|---|
| A) Command pattern — `IEditorCommand` with `Execute` / `Undo` | Standard, decoupled | ✅ Implemented |
| B) Full scene snapshot diff | Simple but memory heavy | Too slow for large scenes |

Commands include: `SetComponentValueCommand` (via `DelegateCommand` closures), `CreateEntityCommand`, `DestroyEntityCommand`, `ReparentEntityCommand`.

### 10.3 Prefab System

- Serialize an entity subtree to a `.prefab.glb` file (using cgltf_write + `SOKOLNET_entity` extension)
- Highlight prefab roots in blue in the Hierarchy Panel
- **Override system**: Track divergences from the prefab definition

### 10.4 Multiple Scenes

Each open scene gets its own tab in the Scene Window area (via ImGui tab bar). Switch between scenes with `SceneManager.SetActiveScene(sceneId)`.

---

## Implementation Sequence Summary

| Phase | Deliverable | Depends on | Status |
|---|---|---|---|
| 0 | Project skeleton, ImGui wired up | — | ✅ Done |
| 1a | Core: AppLoop, Time, EventBus | Phase 0 | ✅ Done |
| 1b | Core: Logger → Console Window (basic) | Phase 1a | 🔧 Logger done; Console Window rings pending |
| 2a | Renderer: OffscreenTarget, RenderView | Phase 1a | ✅ Done |
| 2b | Renderer: Extract CGltfViewer PBR pipeline | Phase 2a | 🔧 Basic renderer; full PBR pending |
| 2c | Renderer: Editor grid shader | Phase 2b | ⬜ Not started |
| 8a | UI: Docking layout, main menu bar | Phase 0 | ✅ Done |
| 8b | UI: Scene Window (viewport camera + offscreen render) | Phases 2b, 8a | ✅ Done |
| 3 | ECS: EntityId, component stores, query | Phase 1a | ✅ Done |
| 8c | UI: Hierarchy Panel (recursive tree, reparent) | Phase 3 | ✅ Done |
| 4a | Scene: SceneGraph, entity parenting | Phase 3 | ✅ Done |
| 4b | Scene: Play/Pause/Stop state machine | Phase 4a | ✅ Done |
| 8d | UI: Game Window | Phases 2b, 4b | ✅ Done |
| 8e | UI: Inspector — all component drawers + undo + active flag + remove button | Phases 3, 7, 10b | ✅ Done |
| 5 | Physics: IPhysicsWorld interface | Phase 3 | ✅ Done |
| 5 | Physics: Jolt + Box2D concrete impls | Phase 5 interface | ⬜ Not started |
| 6a | Assets: cgltf_write bindings generation | — | ⬜ Not started |
| 6b | Assets: Scene serialization to GLB | Phases 4, 6a | ⬜ Not started |
| 6c | Assets: Scene deserialization from GLB | Phase 6b | ⬜ Not started |
| 7 | Scripting: GameBehaviour, script discovery | Phase 3 | ✅ Done |
| 9 | Project management: config.json, CliWrap build | Phase 4 | ⬜ Not started |
| 10a | Editor features: ImGuizmo, picking | Phase 8b | ✅ Done — native rebuild needed |
| 10b | Editor features: Undo/Redo | Phase 8c | ✅ Done |
| 10c | Editor features: Prefabs | Phase 6b | ⬜ Not started |

**Recommended starting point**: Phases 0 → 1a → 2a → 8a → 8b in sequence. This gives a visible editor shell with a working Scene viewport in the shortest time, proving the offscreen render + imgui composition works before adding game logic layers.

---

## Cross-Cutting Concerns

### AOT Compatibility

All Framework code must be AOT-compatible (the project uses `<PublishAot>true</PublishAot>`). Key constraints:
- Avoid `dynamic`, `Expression`, `Assembly.GetTypes()` without `[DynamicDependency]`
- Reflection-based component inspectors need `[RequiresUnreferencedCode]` or source-generated alternatives
- Roslyn in-process compilation is incompatible with AOT — stay with external IDE approach

### Thread Safety

- Physics step runs on Jolt's `JobSystemThreadPool` — only access physics results on the main thread
- File I/O uses `FileSystem.Instance.LoadFile` callback pattern (already async in CGltfViewer)

### Platform-Specific Notes

| Platform | Notes |
|---|---|
| macOS | Metal shaders (`metal_macos`), `.app` bundle via `macOS: Build App Bundle` task |
| Windows | HLSL5 shaders |
| Linux | GLSL430 shaders |
| Android | GLSL300ES, minimum SDK 26, `AndroidPackagePrefix` in Directory.Build.props |
| iOS | Metal iOS shaders, requires `development-team` option in SokolApplicationBuilder |
| Web (WASM) | GLSL WebGL shaders, JoltPhysics WASM status tracked separately |

### Shader Compilation

Run `dotnet build GameEditor.csproj -t:CompileShaders` whenever any `.glsl` file in `shaders/` is modified. The MSBuild target in `Directory.Build.props` handles incremental compilation via input/output timestamps.

### cgltf_write.h Binding Generation — Detailed Steps

1. Read `ext/cgltf/cgltf_write.h` to understand the API surface
2. Examine existing `bindgen/gen.py` and `bindgen/c/` to understand the annotation conventions
3. Annotate `cgltf_write.h` as needed (or write a thin wrapper header)
4. Run: `bash scripts/generate-bindings.sh` (or invoke `python3 bindgen/gen.py` directly)
5. Output goes to `src/sokol/generated/` — make sure it is included by `Directory.Build.props`
6. Verify all write functions (`cgltf_write_file`, etc.) are callable from C# with correct marshal attributes

---

*Document version: 1.1 — Updated to reflect Phases 0, 1a, 2a, 3, 4a, 4b, 5 (interface), 7, 8a–8e, 10b as fully implemented.*
