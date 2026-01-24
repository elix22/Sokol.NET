using System;
using System.Collections.Generic;
using System.Numerics;
using JoltPhysicsSharp;
using SharpGLTF.Schema2;
using Sokol;

using static Sokol.SLog;

public static unsafe partial class GltfViewer
{
    // Jolt Physics layer definitions
    public static class Layers
    {
        public const byte NON_MOVING = 0;
        public const byte MOVING = 1;
        public const byte USER_LAYER_START = 2;  // User-defined layers start here
        public const byte NUM_LAYERS = 32;  // Support up to 32 layers for collision filtering
    }

    public class PhysicsSystem : IDisposable
    {
        private JoltPhysicsSharp.PhysicsSystem? _physicsSystem;
        private BodyInterface? _bodyInterface;
        private JobSystemThreadPool? _jobSystem;
        private ObjectLayerPairFilterTable? _objectLayerPairFilter;
        private BroadPhaseLayerInterfaceTable? _broadPhaseLayerInterface;
        private ObjectVsBroadPhaseLayerFilterTable? _objectVsBroadPhaseLayerFilter;
        private bool _isInitialized;

        // Track bodies created from glTF nodes (map SharpGltfNode wrapper to physics body)
        private readonly Dictionary<SharpGltfNode, BodyID> _nodeBodies = new();
        
        // Track sensor (trigger) bodies to identify them in collision callbacks
        private readonly HashSet<BodyID> _sensorBodies = new();
        
        // Track body names for logging
        private readonly Dictionary<BodyID, string> _bodyNames = new();
        
        // Store physics shape definitions from glTF
        private readonly List<OMI_physics_shape.PhysicsShape> _physicsShapes = new();
        
        // Store physics materials from glTF  
        private readonly List<OMI_physics_body.PhysicsMaterial> _physicsMaterials = new();
        
        // Store collision filters from glTF
        private readonly List<OMI_physics_body.CollisionFilter> _collisionFilters = new();
        
        // Map layer names to Jolt layer indices
        private readonly Dictionary<string, byte> _layerNameToIndex = new();
        private byte _nextAvailableLayer = Layers.USER_LAYER_START;
        
        // Map BodyID to collision filter (for collision checks)
        private readonly Dictionary<BodyID, OMI_physics_body.CollisionFilter> _bodyCollisionFilters = new();
        
        // Track trigger events for UI display
        private readonly List<TriggerEvent> _triggerEvents = new();
        private double _simulationTime = 0.0;

        public bool IsInitialized => _isInitialized;
        
        // Trigger event structure for UI
        public struct TriggerEvent
        {
            public string eventType;      // "ENTER" or "EXIT"
            public string triggerName;
            public string otherName;
            public double timestamp;
        }
        
        // Body statistics for UI
        public struct BodyStatistics
        {
            public int totalBodies;
            public int staticBodies;
            public int dynamicBodies;
            public int kinematicBodies;
            public int triggerBodies;
        }

        public void Initialize()
        {
            // Initialize Jolt Physics foundation
            if (!Foundation.Init())
            {
                Console.WriteLine("[Physics] Failed to initialize Jolt Physics foundation");
                return;
            }

            // Create layer filters
            _objectLayerPairFilter = new ObjectLayerPairFilterTable(Layers.NUM_LAYERS);
            
            // Enable default collision pairs
            _objectLayerPairFilter.EnableCollision(Layers.NON_MOVING, Layers.MOVING);
            _objectLayerPairFilter.EnableCollision(Layers.MOVING, Layers.MOVING);
            
            // Enable collisions between all user-defined layers by default
            for (byte i = Layers.USER_LAYER_START; i < Layers.NUM_LAYERS; i++)
            {
                _objectLayerPairFilter.EnableCollision(Layers.NON_MOVING, i);
                _objectLayerPairFilter.EnableCollision(Layers.MOVING, i);
                for (byte j = i; j < Layers.NUM_LAYERS; j++)
                {
                    _objectLayerPairFilter.EnableCollision(i, j);
                }
            }

            // Create broad phase layer interface
            _broadPhaseLayerInterface = new BroadPhaseLayerInterfaceTable(Layers.NUM_LAYERS, Layers.NUM_LAYERS);
            _broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.NON_MOVING, 0);
            _broadPhaseLayerInterface.MapObjectToBroadPhaseLayer(Layers.MOVING, 1);

            // Create object vs broad phase layer filter
            _objectVsBroadPhaseLayerFilter = new ObjectVsBroadPhaseLayerFilterTable(
                _broadPhaseLayerInterface, Layers.NUM_LAYERS,
                _objectLayerPairFilter, Layers.NUM_LAYERS);

            // Create physics system settings
            var physicsSystemSettings = new PhysicsSystemSettings
            {
                MaxBodies = 10240,
                MaxBodyPairs = 65536,
                MaxContactConstraints = 10240,
                NumBodyMutexes = 0,
                ObjectLayerPairFilter = _objectLayerPairFilter,
                BroadPhaseLayerInterface = _broadPhaseLayerInterface,
                ObjectVsBroadPhaseLayerFilter = _objectVsBroadPhaseLayerFilter
            };

            // Create physics system
            _physicsSystem = new JoltPhysicsSharp.PhysicsSystem(physicsSystemSettings);
            _bodyInterface = _physicsSystem.BodyInterface;
            
            // Register contact listener for trigger events
            _physicsSystem.OnContactAdded += OnContactAdded;
            _physicsSystem.OnContactRemoved += OnContactRemoved;

            // Set gravity
            _physicsSystem.Gravity = new Vector3(0, -9.81f, 0);

            // Create job system for multi-threading
            int numThreads = Math.Max(1, Environment.ProcessorCount - 1);
            var jobSystemConfig = new JobSystemThreadPoolConfig
            {
                maxJobs = 2048,
                maxBarriers = 8,
                numThreads = numThreads
            };
            _jobSystem = new JobSystemThreadPool(jobSystemConfig);

            _isInitialized = true;
            Console.WriteLine($"[Physics] Initialized Jolt Physics with {numThreads} worker threads");
        }

        public void LoadPhysicsShapes(OMI_physics_shape.PhysicsShape[] shapes)
        {
            _physicsShapes.Clear();
            _physicsShapes.AddRange(shapes);
        }
        
        public void LoadPhysicsMaterials(OMI_physics_body.PhysicsMaterial[]? materials)
        {
            _physicsMaterials.Clear();
            if (materials != null)
                _physicsMaterials.AddRange(materials);
        }
        
        public void LoadCollisionFilters(OMI_physics_body.CollisionFilter[]? filters)
        {
            _collisionFilters.Clear();
            if (filters != null)
                _collisionFilters.AddRange(filters);
        }

        public bool CreatePhysicsBody(Node gltfNode, SharpGltfNode modelNode, OMI_physics_body body, SharpGltfModel model, ModelRoot modelRoot)
        {
            if (_physicsSystem == null || _bodyInterface == null)
                return false;

            // Handle triggers (sensor bodies)
            if (body.Trigger != null)
            {
                return CreateTriggerBody(gltfNode, modelNode, body, model, modelRoot);
            }

            ShapeSettings? shape = null;

            // If no collider is defined, try to create one from the mesh bounds
            if (body.Collider == null)
            {
                // Try to auto-generate collision from mesh bounds
                if (modelNode.MeshIndex >= 0 && modelNode.MeshIndex < model.Meshes.Count)
                {
                    var mesh = model.Meshes[modelNode.MeshIndex];
                    var bounds = mesh.Bounds;
                    var size = bounds.Size;
                    var halfExtents = size * 0.5f;
                    
                    shape = new BoxShapeSettings(halfExtents);
                    Info($"[Physics] Auto-generated box collider for '{gltfNode.Name}' from mesh bounds: {size}");
                }
                else
                {
                    Warning($"[Physics] Node '{gltfNode.Name ?? "unnamed"}' has physics body but no collider and no mesh, skipping");
                    return false;
                }
            }
            else
            {
                // Use explicit collider shape
                if (body.Collider.Shape >= _physicsShapes.Count)
                {
                    Console.WriteLine($"[Physics] Warning: Node {gltfNode.Name ?? "unnamed"} references invalid shape index {body.Collider.Shape}, skipping");
                    return false;
                }

                var shapeData = _physicsShapes[body.Collider.Shape];
                shape = CreateShape(shapeData, modelRoot);
                if (shape == null)
                {
                    Console.WriteLine($"[Physics] Warning: Failed to create shape for node {gltfNode.Name ?? "unnamed"}");
                    return false;
                }
            }

            // Get world transform for the node
            var worldTransform = gltfNode.WorldMatrix;
            var position = new Vector3(worldTransform.M41, worldTransform.M42, worldTransform.M43);
            var rotation = Quaternion.CreateFromRotationMatrix(worldTransform);
            
            // Extract scale from world transform
            var scale = new Vector3(
                new Vector3(worldTransform.M11, worldTransform.M12, worldTransform.M13).Length(),
                new Vector3(worldTransform.M21, worldTransform.M22, worldTransform.M23).Length(),
                new Vector3(worldTransform.M31, worldTransform.M32, worldTransform.M33).Length());
            
            // Apply scale to shape
            shape = new ScaledShapeSettings(shape, scale);

            // Determine motion type
            // Per OMI spec: 
            // - If motion property exists, default type is "dynamic"
            // - If motion property is missing, body with collider defaults to "static"
            string motionTypeStr;
            if (body.Motion != null)
            {
                // Motion exists, default is "dynamic" if type not specified
                motionTypeStr = body.Motion.Type ?? "dynamic";
            }
            else
            {
                // No motion property, default is "static"
                motionTypeStr = "static";
            }
            
            Info($"[Physics] Node '{gltfNode.Name}' motion type string: '{motionTypeStr}' (motion is {(body.Motion == null ? "null" : "defined")})");
            
            MotionType motionType = motionTypeStr.ToLower() switch
            {
                "static" => MotionType.Static,
                "kinematic" => MotionType.Kinematic,
                _ => MotionType.Dynamic
            };
            
            Info($"[Physics] Node '{gltfNode.Name}' resolved to motion type: {motionType}");

            // Determine layer from collision filter or use default based on motion type
            byte layer = GetLayerForBody(body.Collider, motionType);
            Info($"[Physics] Node '{gltfNode.Name}' assigned to layer: {layer}");

            // Create body settings
            var bodySettings = new BodyCreationSettings(
                shape,
                position,
                rotation,
                motionType,
                layer);

            // Apply motion properties if this is a dynamic or kinematic body
            if (body.Motion != null && motionType != MotionType.Static)
            {
                // Check if we need to override mass properties
                bool needsMassOverride = body.Motion.Mass.HasValue || 
                                        (body.Motion.CenterOfMass != null && body.Motion.CenterOfMass.Length >= 3) ||
                                        (body.Motion.InertiaDiagonal != null && body.Motion.InertiaDiagonal.Length >= 3);
                
                if (needsMassOverride)
                {
                    var massProps = new MassProperties();
                    
                    // Set mass (default: 1.0 kg)
                    if (body.Motion.Mass.HasValue)
                    {
                        massProps.Mass = body.Motion.Mass.Value;
                        Info($"[Physics] Node '{gltfNode.Name}' mass: {massProps.Mass} kg");
                    }
                    else
                    {
                        massProps.Mass = 1.0f;  // Default mass
                    }
                    
                    // Apply center of mass offset if specified
                    if (body.Motion.CenterOfMass != null && body.Motion.CenterOfMass.Length >= 3)
                    {
                        var centerOfMass = new Vector3(
                            body.Motion.CenterOfMass[0],
                            body.Motion.CenterOfMass[1],
                            body.Motion.CenterOfMass[2]);
                        
                        // Note: Jolt uses MassProperties.Inertia which is a 3x3 matrix
                        // The center of mass offset affects the inertia tensor
                        // For now, we'll let Jolt calculate it with the offset
                        Info($"[Physics] Node '{gltfNode.Name}' center of mass offset: {centerOfMass}");
                        
                        // Store for later application (Jolt doesn't have direct COM offset in MassProperties)
                        // We'll need to translate the shape instead
                    }
                    
                    // Apply custom inertia tensor if specified
                    if (body.Motion.InertiaDiagonal != null && body.Motion.InertiaDiagonal.Length >= 3)
                    {
                        // Create inertia matrix from diagonal values
                        // Jolt uses a 3x3 matrix, OMI provides diagonal (Ixx, Iyy, Izz)
                        var inertiaDiag = new Vector3(
                            body.Motion.InertiaDiagonal[0],
                            body.Motion.InertiaDiagonal[1],
                            body.Motion.InertiaDiagonal[2]);
                        
                        // Set diagonal inertia (off-diagonal terms are 0 for principal axes)
                        massProps.Inertia = new Matrix4x4(
                            inertiaDiag.X, 0, 0, 0,
                            0, inertiaDiag.Y, 0, 0,
                            0, 0, inertiaDiag.Z, 0,
                            0, 0, 0, 1);
                        
                        Info($"[Physics] Node '{gltfNode.Name}' custom inertia diagonal: [{inertiaDiag.X}, {inertiaDiag.Y}, {inertiaDiag.Z}]");
                        
                        // If inertia orientation is specified, apply rotation
                        if (body.Motion.InertiaOrientation != null && body.Motion.InertiaOrientation.Length >= 4)
                        {
                            var inertiaRot = new Quaternion(
                                body.Motion.InertiaOrientation[0],
                                body.Motion.InertiaOrientation[1],
                                body.Motion.InertiaOrientation[2],
                                body.Motion.InertiaOrientation[3]);
                            
                            // Rotate the inertia tensor: I' = R * I * R^T
                            var rotMatrix = Matrix4x4.CreateFromQuaternion(inertiaRot);
                            massProps.Inertia = rotMatrix * massProps.Inertia * Matrix4x4.Transpose(rotMatrix);
                            
                            Info($"[Physics] Node '{gltfNode.Name}' inertia orientation applied");
                        }
                        
                        bodySettings.OverrideMassProperties = JoltPhysicsSharp.OverrideMassProperties.MassAndInertiaProvided;
                    }
                    else
                    {
                        // Let Jolt calculate inertia from shape and mass
                        bodySettings.OverrideMassProperties = JoltPhysicsSharp.OverrideMassProperties.CalculateInertia;
                    }
                    
                    bodySettings.MassPropertiesOverride = massProps;
                }
                
                // Legacy logging for unimplemented features
                if (body.Motion.InertiaDiagonal != null && body.Motion.InertiaDiagonal.Length >= 3)
                {
                    Info($"[Physics] Node '{gltfNode.Name}' inertia diagonal (not yet applied): [{body.Motion.InertiaDiagonal[0]}, {body.Motion.InertiaDiagonal[1]}, {body.Motion.InertiaDiagonal[2]}]");
                }
            }
            
            // Apply physics material properties if specified
            if (body.Collider?.PhysicsMaterial != null)
            {
                int materialIndex = body.Collider.PhysicsMaterial.Value;
                if (materialIndex >= 0 && materialIndex < _physicsMaterials.Count)
                {
                    var material = _physicsMaterials[materialIndex];
                    
                    // Apply friction (Jolt uses dynamic friction, with static as optional override)
                    float friction = material.DynamicFriction ?? material.StaticFriction ?? 0.6f;
                    bodySettings.Friction = friction;
                    
                    // Apply restitution (bounce)
                    float restitution = material.Restitution ?? 0.0f;
                    bodySettings.Restitution = restitution;
                    
                    // Log combine modes (Jolt handles these internally during contact resolution)
                    // The combine modes are considered during OnContactAdded callback
                    string frictionCombine = material.FrictionCombine ?? "average";
                    string restitutionCombine = material.RestitutionCombine ?? "average";
                    
                    Info($"[Physics] Node '{gltfNode.Name}' material: friction={friction} (combine={frictionCombine}), restitution={restitution} (combine={restitutionCombine})");
                    
                    // Store material for runtime combine mode application
                    // Note: Jolt's default behavior is to average, which matches OMI default
                }
            }

            // Create and add body
            if (_bodyInterface != null)
            {
                var bodyInterface = _bodyInterface.Value;
                var joltBody = bodyInterface.CreateBody(bodySettings);
                
                // Apply initial velocities for dynamic/kinematic bodies
                if (body.Motion != null && motionType != MotionType.Static)
                {
                    // Apply linear velocity
                    if (body.Motion.LinearVelocity != null && body.Motion.LinearVelocity.Length >= 3)
                    {
                        var linearVel = new Vector3(
                            body.Motion.LinearVelocity[0],
                            body.Motion.LinearVelocity[1],
                            body.Motion.LinearVelocity[2]);
                        bodyInterface.SetLinearVelocity(joltBody.ID, linearVel);
                        Info($"[Physics] Node '{gltfNode.Name}' linear velocity: {linearVel}");
                    }
                    
                    // Apply angular velocity
                    if (body.Motion.AngularVelocity != null && body.Motion.AngularVelocity.Length >= 3)
                    {
                        var angularVel = new Vector3(
                            body.Motion.AngularVelocity[0],
                            body.Motion.AngularVelocity[1],
                            body.Motion.AngularVelocity[2]);
                        bodyInterface.SetAngularVelocity(joltBody.ID, angularVel);
                        Info($"[Physics] Node '{gltfNode.Name}' angular velocity: {angularVel}");
                    }
                    
                    // Apply gravity factor
                    if (body.Motion.GravityFactor.HasValue)
                    {
                        bodyInterface.SetGravityFactor(joltBody.ID, body.Motion.GravityFactor.Value);
                        Info($"[Physics] Node '{gltfNode.Name}' gravity factor: {body.Motion.GravityFactor.Value}");
                    }
                }
                
                var activation = motionType == MotionType.Static ? Activation.DontActivate : Activation.Activate;
                bodyInterface.AddBody(joltBody.ID, activation);

                // IMPORTANT: Track the parent node (the one with the mesh as child) instead of the collision shape node
                // This ensures the entire visual hierarchy moves with physics
                var nodeToTrack = modelNode.Parent ?? modelNode;
                _nodeBodies[nodeToTrack] = joltBody.ID;
                _bodyNames[joltBody.ID] = gltfNode.Name ?? "unnamed";
                
                // Store collision filter if specified
                if (body.Collider?.CollisionFilter.HasValue == true)
                {
                    int filterIndex = body.Collider.CollisionFilter.Value;
                    if (filterIndex >= 0 && filterIndex < _collisionFilters.Count)
                    {
                        _bodyCollisionFilters[joltBody.ID] = _collisionFilters[filterIndex];
                        Info($"[Physics] Body '{gltfNode.Name}' using collision filter {filterIndex}");
                    }
                }
                
                Console.WriteLine($"[Physics] Created {motionType} body for node {gltfNode.Name ?? "unnamed"}");
                Info($"[Physics] Tracking node: '{nodeToTrack.NodeName}' (MeshIndex={nodeToTrack.MeshIndex}) [original: '{modelNode.NodeName}']");

                return true;
            }
            
            return false;
        }

        private bool CreateTriggerBody(Node gltfNode, SharpGltfNode modelNode, OMI_physics_body body, SharpGltfModel model, ModelRoot modelRoot)
        {
            if (_physicsSystem == null || _bodyInterface == null || body.Trigger == null)
                return false;

            Info($"[Physics] Creating trigger (sensor) for node '{gltfNode.Name}'");

            ShapeSettings? shape = null;

            // Check if trigger has a direct shape reference
            if (body.Trigger.Shape.HasValue)
            {
                if (body.Trigger.Shape.Value >= _physicsShapes.Count)
                {
                    Warning($"[Physics] Trigger '{gltfNode.Name}' references invalid shape index {body.Trigger.Shape.Value}");
                    return false;
                }

                var shapeData = _physicsShapes[body.Trigger.Shape.Value];
                shape = CreateShape(shapeData, modelRoot);
                if (shape == null)
                {
                    Warning($"[Physics] Failed to create shape for trigger '{gltfNode.Name}'");
                    return false;
                }
            }
            // Check if trigger references child nodes (compound trigger)
            else if (body.Trigger.Nodes != null && body.Trigger.Nodes.Length > 0)
            {
                Info($"[Physics] Compound trigger '{gltfNode.Name}' references {body.Trigger.Nodes.Length} child nodes");
                
                // Create compound shape from multiple child shapes
                var childShapes = new List<ShapeSettings>();
                var childPositions = new List<Vector3>();
                var childRotations = new List<Quaternion>();
                
                foreach (var childIndex in body.Trigger.Nodes)
                {
                    if (childIndex >= modelRoot.LogicalNodes.Count)
                    {
                        Warning($"[Physics] Trigger '{gltfNode.Name}' references invalid node index {childIndex}");
                        continue;
                    }
                    
                    var childNode = modelRoot.LogicalNodes[childIndex];
                    var childPhysicsExt = PhysicsExtensionParser.ParsePhysicsBodyExtension(childNode);
                    
                    Info($"[Physics] Checking child node '{childNode.Name}' for trigger: Trigger={childPhysicsExt?.Trigger != null}, Collider={childPhysicsExt?.Collider != null}, HasMesh={childNode.Mesh != null}");
                    
                    if (childPhysicsExt?.Collider != null)
                    {
                        Info($"[Physics] Child node '{childNode.Name}' collider: Shape={childPhysicsExt.Collider.Shape}");
                    }
                    
                    ShapeSettings? childShape = null;
                    
                    // Try trigger shape first, then collider shape (nodes can reference either)
                    int? childShapeIndex = null;
                    if (childPhysicsExt?.Trigger?.Shape != null)
                    {
                        childShapeIndex = childPhysicsExt.Trigger.Shape.Value;
                        Info($"[Physics] Child node '{childNode.Name}' has trigger shape {childShapeIndex}");
                    }
                    else if (childPhysicsExt?.Collider != null)
                    {
                        childShapeIndex = childPhysicsExt.Collider.Shape;
                        Info($"[Physics] Child node '{childNode.Name}' has collider shape {childShapeIndex}");
                    }
                    
                    // If child has a shape reference, use it
                    if (childShapeIndex.HasValue && childShapeIndex.Value < _physicsShapes.Count)
                    {
                        var childShapeData = _physicsShapes[childShapeIndex.Value];
                        childShape = CreateShape(childShapeData, modelRoot);
                    }
                    // Otherwise, if child has a mesh, create convex hull from mesh
                    else if (childNode.Mesh != null)
                    {
                        Info($"[Physics] Child node '{childNode.Name}' has no shape reference, creating convex hull from mesh");
                        
                        // Find first mesh node child or use this node's mesh
                        var meshNode = childNode;
                        var gltfMesh = meshNode.Mesh;
                        
                        if (gltfMesh != null && gltfMesh.Primitives.Count > 0)
                        {
                            var primitive = gltfMesh.Primitives[0];
                            var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                            
                            if (positions != null && positions.Count > 0)
                            {
                                Info($"[Physics] Creating convex hull from {positions.Count} vertices for child '{childNode.Name}'");
                                childShape = new ConvexHullShapeSettings(positions.ToArray());
                            }
                        }
                    }
                    // Check if child has mesh children nodes
                    else
                    {
                        Info($"[Physics] Child node '{childNode.Name}' has {childNode.VisualChildren.Count()} visual children");
                        foreach (var visualChild in childNode.VisualChildren)
                        {
                            Info($"[Physics]   - Visual child: '{visualChild.Name}', HasMesh={visualChild.Mesh != null}");
                        }
                        
                        // Try VisualChildren first
                        foreach (var meshChildNode in childNode.VisualChildren)
                        {
                            if (meshChildNode.Mesh != null && meshChildNode.Mesh.Primitives.Count > 0)
                            {
                                Info($"[Physics] Child node '{childNode.Name}' has visual mesh child '{meshChildNode.Name}', creating convex hull");
                                
                                var primitive = meshChildNode.Mesh.Primitives[0];
                                var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                                
                                if (positions != null && positions.Count > 0)
                                {
                                    Info($"[Physics] Creating convex hull from {positions.Count} vertices for child '{meshChildNode.Name}'");
                                    childShape = new ConvexHullShapeSettings(positions.ToArray());
                                    break; // Use first mesh child
                                }
                            }
                        }
                        
                        // Final fallback: Search for sibling mesh nodes by name pattern (e.g., "StandaloneShape" -> "StandaloneMesh")
                        if (childShape == null && childNode.VisualChildren.Count() == 0)
                        {
                            Info($"[Physics] No visual children found, searching for mesh nodes by name pattern");
                            
                            // Extract base name by removing common suffixes like "Shape"
                            string baseName = childNode.Name;
                            if (baseName.EndsWith("Shape"))
                            {
                                baseName = baseName.Substring(0, baseName.Length - 5); // Remove "Shape"
                            }
                            
                            Info($"[Physics] Base name for pattern matching: '{baseName}' (from '{childNode.Name}')");
                            
                            // Look for nodes named like "{baseName}Mesh"
                            foreach (var potentialMeshNode in modelRoot.LogicalNodes)
                            {
                                if (potentialMeshNode.Mesh != null && 
                                    (potentialMeshNode.Name == baseName + "Mesh" || 
                                     potentialMeshNode.Name == childNode.Name + "Mesh"))
                                {
                                    Info($"[Physics] Found mesh node '{potentialMeshNode.Name}' matching pattern for '{childNode.Name}'");
                                    
                                    var primitive = potentialMeshNode.Mesh.Primitives[0];
                                    var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                                    
                                    if (positions != null && positions.Count > 0)
                                    {
                                        Info($"[Physics] Creating convex hull from {positions.Count} vertices for '{potentialMeshNode.Name}'");
                                        childShape = new ConvexHullShapeSettings(positions.ToArray());
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    
                    if (childShape != null)
                    {
                        childShapes.Add(childShape);
                        
                        // Get relative transform of child to parent
                        var childTransform = childNode.LocalMatrix;
                        var childPos = new Vector3(childTransform.M41, childTransform.M42, childTransform.M43);
                        var childRot = Quaternion.CreateFromRotationMatrix(childTransform);
                        
                        childPositions.Add(childPos);
                        childRotations.Add(childRot);
                        
                        Info($"[Physics] Added child trigger shape from node '{childNode.Name}' at position {childPos}");
                    }
                    else
                    {
                        Warning($"[Physics] Child node '{childNode.Name}' has no valid shape reference or mesh");
                    }
                }
                
                if (childShapes.Count == 0)
                {
                    Warning($"[Physics] Compound trigger '{gltfNode.Name}' has no valid child shapes");
                    return false;
                }
                
                // For compound triggers with MULTIPLE children, create individual triggers for each child
                // (matching .gltf behavior where each child is both a standalone trigger AND part of compound)
                // BUT: Skip children that already have physics bodies (they may have their own trigger extension)
                if (childShapes.Count > 1)
                {
                    Info($"[Physics] Creating individual triggers for {childShapes.Count} children of compound trigger '{gltfNode.Name}'");
                    
                    for (int i = 0; i < body.Trigger.Nodes.Length; i++)
                    {
                        int childNodeIndex = body.Trigger.Nodes[i];
                        if (childNodeIndex >= modelRoot.LogicalNodes.Count || i >= childShapes.Count)
                            continue;
                        
                        var childModelNode = modelRoot.LogicalNodes[childNodeIndex];
                        var childSharpGltfNode = model.Nodes.FirstOrDefault(n => n.NodeIndex == childNodeIndex);
                        if (childSharpGltfNode == null)
                            continue;
                        
                        // Skip if this child already has a physics body (e.g., it has its own trigger extension)
                        // Check both the node itself and its parent (since CreateTriggerBody stores parent if available)
                        var parentNode = childSharpGltfNode.Parent;
                        if (_nodeBodies.ContainsKey(childSharpGltfNode) || (parentNode != null && _nodeBodies.ContainsKey(parentNode)))
                        {
                            Info($"[Physics] Child '{childModelNode.Name}' already has physics body, skipping duplicate trigger creation");
                            continue;
                        }
                        
                        var childShape = childShapes[i];
                        var childWorldTransform = childModelNode.WorldMatrix;
                        var childPosition = new Vector3(childWorldTransform.M41, childWorldTransform.M42, childWorldTransform.M43);
                        var childRotation = Quaternion.CreateFromRotationMatrix(childWorldTransform);
                        var childScale = new Vector3(
                            new Vector3(childWorldTransform.M11, childWorldTransform.M12, childWorldTransform.M13).Length(),
                            new Vector3(childWorldTransform.M21, childWorldTransform.M22, childWorldTransform.M23).Length(),
                            new Vector3(childWorldTransform.M31, childWorldTransform.M32, childWorldTransform.M33).Length());
                        
                        var scaledChildShape = new ScaledShapeSettings(childShape, childScale);
                        
                        using (var childSettings = new BodyCreationSettings(scaledChildShape, childPosition, childRotation, MotionType.Static, Layers.NON_MOVING))
                        {
                            childSettings.IsSensor = true;
                            var bodyInterface = _bodyInterface.Value;
                            var childBody = bodyInterface.CreateBody(childSettings);
                            if (childBody != null)
                            {
                                bodyInterface.AddBody(childBody.ID, Activation.DontActivate);
                                _nodeBodies[childSharpGltfNode] = childBody.ID;
                                _sensorBodies.Add(childBody.ID);
                                _bodyNames[childBody.ID] = childModelNode.Name ?? "unnamed_child_trigger";
                                Info($"[Physics] Created individual trigger for child '{childModelNode.Name}'");
                            }
                        }
                    }
                }
                
                // Create compound shape if multiple children, otherwise use single shape
                if (childShapes.Count == 1)
                {
                    shape = childShapes[0];
                }
                else
                {
                    // TODO: Implement compound shape creation for multiple trigger volumes
                    // For now, use the first shape as a fallback
                    Warning($"[Physics] Compound triggers with multiple shapes not yet fully implemented, using first shape only");
                    shape = childShapes[0];
                }
            }
            else
            {
                Warning($"[Physics] Trigger '{gltfNode.Name}' has no shape or nodes reference");
                return false;
            }

            // Get world transform for the node
            var worldTransform = gltfNode.WorldMatrix;
            var position = new Vector3(worldTransform.M41, worldTransform.M42, worldTransform.M43);
            var rotation = Quaternion.CreateFromRotationMatrix(worldTransform);
            
            // Extract scale from world transform
            var scale = new Vector3(
                new Vector3(worldTransform.M11, worldTransform.M12, worldTransform.M13).Length(),
                new Vector3(worldTransform.M21, worldTransform.M22, worldTransform.M23).Length(),
                new Vector3(worldTransform.M31, worldTransform.M32, worldTransform.M33).Length());
            
            // Apply scale to shape
            shape = new ScaledShapeSettings(shape, scale);

            // Triggers are always static sensors (they don't move and don't have physics interactions)
            var layer = Layers.NON_MOVING;
            var motionType = MotionType.Static;

            using (var settings = new BodyCreationSettings(shape, position, rotation, motionType, layer))
            {
                // CRITICAL: Set IsSensor to true for triggers
                settings.IsSensor = true;
                
                var bodyInterface = _bodyInterface.Value;
                var joltBody = bodyInterface.CreateBody(settings);
                if (joltBody == null)
                {
                    Console.WriteLine($"[Physics] Failed to create trigger body for {gltfNode.Name ?? "unnamed"}");
                    return false;
                }
                
                // Verify IsSensor was set correctly after creation
                Console.WriteLine($"[Physics] Created trigger '{gltfNode.Name}': IsSensor={joltBody.IsSensor}, Position={position.Y:F3}");

                bodyInterface.AddBody(joltBody.ID, Activation.DontActivate);

                // Track the node (parent node if available)
                var nodeToTrack = modelNode.Parent ?? modelNode;
                _nodeBodies[nodeToTrack] = joltBody.ID;
                
                // Track this as a sensor body for collision detection
                _sensorBodies.Add(joltBody.ID);
                _bodyNames[joltBody.ID] = gltfNode.Name ?? "unnamed_trigger";
                
                // Store collision filter if specified
                if (body.Trigger.CollisionFilter.HasValue)
                {
                    int filterIndex = body.Trigger.CollisionFilter.Value;
                    if (filterIndex >= 0 && filterIndex < _collisionFilters.Count)
                    {
                        _bodyCollisionFilters[joltBody.ID] = _collisionFilters[filterIndex];
                        Info($"[Physics] Trigger '{gltfNode.Name}' using collision filter {filterIndex}");
                    }
                }

                Info($"[Physics] Created sensor trigger for node '{gltfNode.Name}' (IsSensor=true)");

                return true;
            }
        }

        private ShapeSettings? CreateShape(OMI_physics_shape.PhysicsShape shapeData, ModelRoot modelRoot)
        {
            if (shapeData == null)
                return null;

            if (shapeData.Sphere != null)
            {
                float radius = shapeData.Sphere.Radius ?? 0.5f;
                return new SphereShapeSettings(radius);
            }
            else if (shapeData.Box != null)
            {
                var size = shapeData.Box.Size ?? new[] { 1.0f, 1.0f, 1.0f };
                var halfExtents = new Vector3(size[0] / 2, size[1] / 2, size[2] / 2);
                return new BoxShapeSettings(halfExtents);
            }
            else if (shapeData.Capsule != null)
            {
                // Use radiusTop/radiusBottom with fallback to old radius property
                float radiusTop = shapeData.Capsule.RadiusTop ?? 0.5f;
                float radiusBottom = shapeData.Capsule.RadiusBottom ?? radiusTop;  // Default to same radius
                float height = shapeData.Capsule.Height ?? 1.0f;  // Mid-height
                
                // Check if tapered capsule (different radii) - Jolt doesn't support this natively
                if (Math.Abs(radiusTop - radiusBottom) > 0.001f)
                {
                    Warning($"[Physics] Tapered capsule detected (radiusTop={radiusTop}, radiusBottom={radiusBottom}). Using average radius.");
                    float avgRadius = (radiusTop + radiusBottom) / 2.0f;
                    return new CapsuleShapeSettings(height / 2, avgRadius);
                }
                
                return new CapsuleShapeSettings(height / 2, radiusTop);
            }
            else if (shapeData.Cylinder != null)
            {
                // Use radiusTop/radiusBottom with fallback
                float radiusTop = shapeData.Cylinder.RadiusTop ?? 0.5f;
                float radiusBottom = shapeData.Cylinder.RadiusBottom ?? radiusTop;  // Default to same radius
                float height = shapeData.Cylinder.Height ?? 2.0f;  // Total height
                
                // Check if tapered cylinder (different radii) - Jolt doesn't support this natively
                if (Math.Abs(radiusTop - radiusBottom) > 0.001f)
                {
                    Warning($"[Physics] Tapered cylinder detected (radiusTop={radiusTop}, radiusBottom={radiusBottom}). Using average radius.");
                    float avgRadius = (radiusTop + radiusBottom) / 2.0f;
                    return new CylinderShapeSettings(height / 2, avgRadius);
                }
                
                return new CylinderShapeSettings(height / 2, radiusTop);
            }
            else if (shapeData.Convex != null)
            {
                // Create convex hull from mesh vertices
                int meshIndex = shapeData.Convex.Mesh;
                if (meshIndex < 0 || meshIndex >= modelRoot.LogicalMeshes.Count)
                {
                    Warning($"[Physics] Convex shape references invalid mesh index {meshIndex}");
                    return null;
                }
                
                var gltfMesh = modelRoot.LogicalMeshes[meshIndex];
                if (gltfMesh.Primitives.Count == 0)
                {
                    Warning($"[Physics] Convex shape mesh has no primitives");
                    return null;
                }
                
                // Get vertices from first primitive
                var primitive = gltfMesh.Primitives[0];
                var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                
                if (positions == null || positions.Count == 0)
                {
                    Warning($"[Physics] Convex shape mesh has no position data");
                    return null;
                }
                
                Info($"[Physics] Creating convex hull from {positions.Count} vertices");
                
                // Create convex hull shape from vertices
                return new ConvexHullShapeSettings(positions.ToArray());
            }
            else if (shapeData.Trimesh != null)
            {
                // Create triangle mesh from mesh indices and vertices
                int meshIndex = shapeData.Trimesh.Mesh;
                if (meshIndex < 0 || meshIndex >= modelRoot.LogicalMeshes.Count)
                {
                    Warning($"[Physics] Trimesh shape references invalid mesh index {meshIndex}");
                    return null;
                }
                
                var gltfMesh = modelRoot.LogicalMeshes[meshIndex];
                if (gltfMesh.Primitives.Count == 0)
                {
                    Warning($"[Physics] Trimesh shape mesh has no primitives");
                    return null;
                }
                
                // Get vertices and indices from first primitive
                var primitive = gltfMesh.Primitives[0];
                var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                var indexAccessor = primitive.IndexAccessor;
                
                if (positions == null || positions.Count == 0)
                {
                    Warning($"[Physics] Trimesh shape mesh has no position data");
                    return null;
                }
                
                if (indexAccessor == null)
                {
                    Warning($"[Physics] Trimesh shape mesh has no index data");
                    return null;
                }
                
                var indices = indexAccessor.AsIndicesArray();
                
                Info($"[Physics] Creating trimesh from {positions.Count} vertices and {indices.Count / 3} triangles");
                
                // Create mesh shape from triangles
                var triangles = new Triangle[indices.Count / 3];
                for (int i = 0; i < triangles.Length; i++)
                {
                    uint idx0 = indices[i * 3 + 0];
                    uint idx1 = indices[i * 3 + 1];
                    uint idx2 = indices[i * 3 + 2];
                    
                    triangles[i] = new Triangle(
                        positions[(int)idx0],
                        positions[(int)idx1],
                        positions[(int)idx2],
                        0 // material index
                    );
                }
                
                return new MeshShapeSettings(triangles);
            }

            return null;
        }

        public void Update(float deltaTime)
        {
            if (_physicsSystem == null || _jobSystem == null)
                return;

            // Track simulation time for trigger events
            _simulationTime += deltaTime;

            // Step the physics simulation
            _physicsSystem.Update(deltaTime, 1, _jobSystem);

            // Sync transforms back to nodes
            SyncTransforms();
        }

        private void SyncTransforms()
        {
            if (_bodyInterface == null)
                return;

            var bodyInterface = _bodyInterface.Value;

            foreach (var (node, bodyId) in _nodeBodies)
            {
                // Skip static bodies - they don't move
                var motionType = bodyInterface.GetMotionType(bodyId);
                if (motionType == MotionType.Static)
                    continue;

                var position = bodyInterface.GetPosition(bodyId);
                var rotation = bodyInterface.GetRotation(bodyId);

                // CRITICAL: Preserve the node's original scale
                // Physics only controls position and rotation, not scale
                var originalScale = node.Scale;
                
                // Create transform matrix from physics body state WITH original scale
                var matrix = Matrix4x4.CreateScale(originalScale) *
                             Matrix4x4.CreateFromQuaternion(rotation) * 
                             Matrix4x4.CreateTranslation(position);
                
                // Update the node's world transform
                node.SetWorldTransform(matrix);
            }
        }

        private void OnContactAdded(JoltPhysicsSharp.PhysicsSystem system, in JoltPhysicsSharp.Body body1, in JoltPhysicsSharp.Body body2, in ContactManifold manifold, ref ContactSettings settings)
        {
            var body1ID = body1.ID;
            var body2ID = body2.ID;
            
            // Check collision filters - disable collision if filters prevent it
            if (!ShouldBodiesCollide(body1ID, body2ID))
            {
                // Disable the collision by setting solver iterations to 0
                settings.CombinedRestitution = 0;
                settings.CombinedFriction = 0;
                return;
            }
            
            // Check if either body is a sensor (trigger)
            bool body1IsSensor = _sensorBodies.Contains(body1ID);
            bool body2IsSensor = _sensorBodies.Contains(body2ID);
            
            if (body1IsSensor || body2IsSensor)
            {
                string triggerName = body1IsSensor ? GetBodyName(body1ID) : GetBodyName(body2ID);
                string otherName = body1IsSensor ? GetBodyName(body2ID) : GetBodyName(body1ID);
                
                // Add to trigger events list
                _triggerEvents.Add(new TriggerEvent
                {
                    eventType = "ENTER",
                    triggerName = triggerName,
                    otherName = otherName,
                    timestamp = _simulationTime
                });
                
                // Keep only last 100 events to prevent memory growth
                if (_triggerEvents.Count > 100)
                {
                    _triggerEvents.RemoveAt(0);
                }
                
                Console.WriteLine($"[Physics] 🟢 TRIGGER ENTER: '{otherName}' entered trigger '{triggerName}'");
            }
        }
        
        private void OnContactRemoved(JoltPhysicsSharp.PhysicsSystem system, ref SubShapeIDPair subShapePair)
        {
            var body1ID = subShapePair.Body1ID;
            var body2ID = subShapePair.Body2ID;
            
            // Check if either body is a sensor (trigger)
            bool body1IsSensor = _sensorBodies.Contains(body1ID);
            bool body2IsSensor = _sensorBodies.Contains(body2ID);
            
            if (body1IsSensor || body2IsSensor)
            {
                string triggerName = body1IsSensor ? GetBodyName(body1ID) : GetBodyName(body2ID);
                string otherName = body1IsSensor ? GetBodyName(body2ID) : GetBodyName(body1ID);
                
                // Add to trigger events list
                _triggerEvents.Add(new TriggerEvent
                {
                    eventType = "EXIT",
                    triggerName = triggerName,
                    otherName = otherName,
                    timestamp = _simulationTime
                });
                
                // Keep only last 100 events to prevent memory growth
                if (_triggerEvents.Count > 100)
                {
                    _triggerEvents.RemoveAt(0);
                }
                
                Console.WriteLine($"[Physics] 🔴 TRIGGER EXIT: '{otherName}' exited trigger '{triggerName}'");
            }
        }
        
        private string GetBodyName(BodyID bodyId)
        {
            return _bodyNames.TryGetValue(bodyId, out var name) ? name : $"Body_{bodyId.ID}";
        }
        
        // Helper to check if two bodies should collide based on collision filters
        private bool ShouldBodiesCollide(BodyID body1ID, BodyID body2ID)
        {
            // Get collision filters for both bodies (if any)
            _bodyCollisionFilters.TryGetValue(body1ID, out var filter1);
            _bodyCollisionFilters.TryGetValue(body2ID, out var filter2);
            
            // If neither has a filter, allow collision
            if (filter1 == null && filter2 == null)
                return true;
            
            // Get layer names for both bodies
            var body1Layers = filter1?.CollisionSystems ?? Array.Empty<string>();
            var body2Layers = filter2?.CollisionSystems ?? Array.Empty<string>();
            
            // Check body1's collision rules against body2's layers
            if (filter1 != null)
            {
                // If body1 has collideWithSystems (whitelist), body2 must be in one of those systems
                if (filter1.CollideWithSystems != null && filter1.CollideWithSystems.Length > 0)
                {
                    bool canCollide = false;
                    foreach (var allowedSystem in filter1.CollideWithSystems)
                    {
                        if (body2Layers.Contains(allowedSystem))
                        {
                            canCollide = true;
                            break;
                        }
                    }
                    
                    if (!canCollide)
                    {
                        Info($"[Physics] Collision blocked: Body1 whitelist excludes Body2 layers");
                        return false;
                    }
                }
                
                // If body1 has notCollideWithSystems (blacklist), body2 must NOT be in those systems
                if (filter1.NotCollideWithSystems != null && filter1.NotCollideWithSystems.Length > 0)
                {
                    foreach (var blockedSystem in filter1.NotCollideWithSystems)
                    {
                        if (body2Layers.Contains(blockedSystem))
                        {
                            Info($"[Physics] Collision blocked: Body1 blacklist excludes Body2 layer '{blockedSystem}'");
                            return false;
                        }
                    }
                }
            }
            
            // Check body2's collision rules against body1's layers
            if (filter2 != null)
            {
                // If body2 has collideWithSystems (whitelist), body1 must be in one of those systems
                if (filter2.CollideWithSystems != null && filter2.CollideWithSystems.Length > 0)
                {
                    bool canCollide = false;
                    foreach (var allowedSystem in filter2.CollideWithSystems)
                    {
                        if (body1Layers.Contains(allowedSystem))
                        {
                            canCollide = true;
                            break;
                        }
                    }
                    
                    if (!canCollide)
                    {
                        Info($"[Physics] Collision blocked: Body2 whitelist excludes Body1 layers");
                        return false;
                    }
                }
                
                // If body2 has notCollideWithSystems (blacklist), body1 must NOT be in those systems
                if (filter2.NotCollideWithSystems != null && filter2.NotCollideWithSystems.Length > 0)
                {
                    foreach (var blockedSystem in filter2.NotCollideWithSystems)
                    {
                        if (body1Layers.Contains(blockedSystem))
                        {
                            Info($"[Physics] Collision blocked: Body2 blacklist excludes Body1 layer '{blockedSystem}'");
                            return false;
                        }
                    }
                }
            }
            
            // Both filters allow collision
            return true;
        }
        
        private byte GetLayerForBody(OMI_physics_body.ColliderData? collider, MotionType motionType)
        {
            // Check if body has a collision filter assigned
            if (collider?.CollisionFilter != null)
            {
                int filterIndex = collider.CollisionFilter.Value;
                if (filterIndex >= 0 && filterIndex < _collisionFilters.Count)
                {
                    var filter = _collisionFilters[filterIndex];
                    
                    // Use the first collision system name as the primary layer
                    if (filter.CollisionSystems != null && filter.CollisionSystems.Length > 0)
                    {
                        string primaryLayerName = filter.CollisionSystems[0];
                        
                        // Map layer name to Jolt layer index (create if doesn't exist)
                        if (!_layerNameToIndex.TryGetValue(primaryLayerName, out byte layer))
                        {
                            // Assign a new layer index for this name
                            if (_nextAvailableLayer < Layers.NUM_LAYERS)
                            {
                                layer = _nextAvailableLayer++;
                                _layerNameToIndex[primaryLayerName] = layer;
                                Info($"[Physics] Registered new collision layer '{primaryLayerName}' as Jolt layer {layer}");
                            }
                            else
                            {
                                Warning($"[Physics] Ran out of collision layers! Using default layer for '{primaryLayerName}'");
                                layer = Layers.MOVING;
                            }
                        }
                        
                        Info($"[Physics] Using collision filter {filterIndex}: layer={layer} ('{primaryLayerName}')");
                        return layer;
                    }
                }
            }
            
            // Default: use motion type to determine layer
            return motionType == MotionType.Static ? Layers.NON_MOVING : Layers.MOVING;
        }
        
        // Public API methods for GUI
        
        public BodyStatistics GetBodyStatistics()
        {
            var stats = new BodyStatistics();
            
            if (_physicsSystem == null || _bodyInterface == null)
                return stats;
            
            var bodyInterface = _bodyInterface.Value;
            
            // Use Jolt's API to get total body count (authoritative)
            stats.totalBodies = (int)_physicsSystem.BodiesCount;
            
            // Get all body IDs from Jolt
            var bodyCount = _physicsSystem.BodiesCount;
            if (bodyCount > 0)
            {
                var bodyIds = stackalloc BodyID[(int)bodyCount];
                JoltPhysicsSharp.JoltApi.JPH_PhysicsSystem_GetBodies(_physicsSystem.Handle, bodyIds, bodyCount);
                
                // Count by motion type
                for (int i = 0; i < bodyCount; i++)
                {
                    var motionType = bodyInterface.GetMotionType(bodyIds[i]);
                    switch (motionType)
                    {
                        case MotionType.Static:
                            stats.staticBodies++;
                            break;
                        case MotionType.Dynamic:
                            stats.dynamicBodies++;
                            break;
                        case MotionType.Kinematic:
                            stats.kinematicBodies++;
                            break;
                    }
                }
            }
            
            // Count triggers directly from _sensorBodies (authoritative source)
            stats.triggerBodies = _sensorBodies.Count;
            
            return stats;
        }
        
        public Vector3 GetGravity()
        {
            return _physicsSystem?.Gravity ?? Vector3.Zero;
        }
        
        public List<TriggerEvent> GetTriggerEvents()
        {
            return _triggerEvents;
        }
        
        public void ClearTriggerEvents()
        {
            _triggerEvents.Clear();
        }

        public struct BodyInfo
        {
            public string name;
            public BodyID id;
            public MotionType motionType;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 linearVelocity;
            public Vector3 angularVelocity;
            public bool isSensor;
            public bool isActive;
        }

        public List<BodyInfo> GetDetailedBodyInfo()
        {
            var bodyInfos = new List<BodyInfo>();
            
            if (_physicsSystem == null || _bodyInterface == null)
                return bodyInfos;
            
            var bodyInterface = _bodyInterface.Value;
            var bodyCount = _physicsSystem.BodiesCount;
            
            if (bodyCount > 0)
            {
                var bodyIds = stackalloc BodyID[(int)bodyCount];
                JoltPhysicsSharp.JoltApi.JPH_PhysicsSystem_GetBodies(_physicsSystem.Handle, bodyIds, bodyCount);
                
                for (int i = 0; i < bodyCount; i++)
                {
                    var bodyId = bodyIds[i];
                    var info = new BodyInfo
                    {
                        id = bodyId,
                        name = _bodyNames.TryGetValue(bodyId, out var name) ? name : $"Body_{bodyId.ID}",
                        motionType = bodyInterface.GetMotionType(bodyId),
                        position = bodyInterface.GetPosition(bodyId),
                        rotation = bodyInterface.GetRotation(bodyId),
                        linearVelocity = bodyInterface.GetLinearVelocity(bodyId),
                        angularVelocity = bodyInterface.GetAngularVelocity(bodyId),
                        isSensor = _sensorBodies.Contains(bodyId),
                        isActive = bodyInterface.IsActive(bodyId)
                    };
                    bodyInfos.Add(info);
                }
            }
            
            return bodyInfos;
        }

        public void Dispose()
        {
            if (!_isInitialized)
                return;

            Console.WriteLine("[Physics] Starting cleanup...");

            // Remove all bodies
            if (_bodyInterface != null)
            {
                var bodyInterface = _bodyInterface.Value;
                foreach (var bodyId in _nodeBodies.Values)
                {
                    bodyInterface.RemoveBody(bodyId);
                    bodyInterface.DestroyBody(bodyId);
                }
            }
            
            // Clear all tracking dictionaries
            _nodeBodies.Clear();
            _sensorBodies.Clear();
            _bodyNames.Clear();
            _bodyCollisionFilters.Clear();
            _layerNameToIndex.Clear();
            _triggerEvents.Clear();
            
            // Clear stored data
            _physicsShapes.Clear();
            _physicsMaterials.Clear();
            _collisionFilters.Clear();
            
            // Reset layer allocation
            _nextAvailableLayer = Layers.USER_LAYER_START;

            // Cleanup Jolt resources
            _jobSystem?.Dispose();
            _jobSystem = null;
            
            _physicsSystem?.Dispose();
            _physicsSystem = null;
            
            _bodyInterface = null;
            _objectLayerPairFilter = null;
            _broadPhaseLayerInterface = null;
            _objectVsBroadPhaseLayerFilter = null;
            
            Foundation.Shutdown();

            _isInitialized = false;
            Console.WriteLine("[Physics] Cleanup complete");
        }
    }
}
