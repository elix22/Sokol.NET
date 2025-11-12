using System;
using System.Diagnostics;
using System.Numerics;
using SharpGLTF.Schema2;
using static Sokol.SG;
using static Sokol.SLog;

namespace Sokol
{

    public class SharpGltfModel
    {
        public List<Mesh> Meshes = new List<Mesh>();
        public List<SharpGltfNode> Nodes = new List<SharpGltfNode>();
        public Dictionary<string, BoneInfo> BoneInfoMap = new Dictionary<string, BoneInfo>();
        public int BoneCounter = 0;
        public List<SharpGltfAnimation> Animations = new List<SharpGltfAnimation>();
        public int CurrentAnimationIndex = 0;
        public SharpGltfAnimation? Animation => Animations.Count > 0 ? Animations[CurrentAnimationIndex] : null;
        public bool HasAnimations => Animations.Count > 0;
        public bool AnimationsReady { get; private set; } = false;
        
        // Material index to mesh mapping for KHR_animation_pointer support
        public Dictionary<int, List<Mesh>> MaterialToMeshMap = new Dictionary<int, List<Mesh>>();
        
        // Track skinned node names (joints in skeletons)
        private HashSet<string> _skinnedNodeNames = new HashSet<string>();

        public int GetAnimationCount() => Animations.Count;
        
        public string GetCurrentAnimationName() => 
            HasAnimations ? Animations[CurrentAnimationIndex].Name : "None";
        
        public void SetCurrentAnimation(int index)
        {
            if (index >= 0 && index < Animations.Count)
            {
                CurrentAnimationIndex = index;
                Info($"Switched to animation '{GetCurrentAnimationName()}' (index {index})", "SharpGLTF");
            }
        }

        public void NextAnimation()
        {
            if (Animations.Count > 0)
            {
                CurrentAnimationIndex = (CurrentAnimationIndex + 1) % Animations.Count;
                Info($"Next animation: '{GetCurrentAnimationName()}'", "SharpGLTF");
            }
        }

        public void PreviousAnimation()
        {
            if (Animations.Count > 0)
            {
                CurrentAnimationIndex = (CurrentAnimationIndex - 1 + Animations.Count) % Animations.Count;
                Info($"Previous animation: '{GetCurrentAnimationName()}'", "SharpGLTF");
            }
        }

        private ModelRoot _model;
        private List<AnimationChannel> _pendingChannels = new List<AnimationChannel>();
        private float _animationDuration;
        private int _currentChannelIndex = 0;
        
        public ModelRoot ModelRoot => _model;  // Expose for animator

        public SharpGltfModel(ModelRoot model, string? filePath = null)
        {
            _model = model;
            ProcessModel();
        }

        private void ProcessModel()
        {
            Info($"Processing model with {_model.LogicalNodes.Count} nodes, {_model.LogicalMeshes.Count} meshes", "SharpGLTF");

            // First pass: Process skinning information
            ProcessSkinning();

            // Second pass: Process all meshes (without nodes yet)
            var meshMap = new Dictionary<SharpGLTF.Schema2.Mesh, int>();
            foreach (var mesh in _model.LogicalMeshes)
            {
                int meshStartIndex = Meshes.Count;
                foreach (var primitive in mesh.Primitives)
                {
                    ProcessMesh(primitive);
                }
                // Store the first mesh index for this logical mesh
                meshMap[mesh] = meshStartIndex;
            }

            // Third pass: Build node index map for morph weight lookups
            Dictionary<Node, int> nodeIndexMap = new Dictionary<Node, int>();
            var logicalNodes = _model.LogicalNodes;
            for (int i = 0; i < logicalNodes.Count; i++)
            {
                nodeIndexMap[logicalNodes[i]] = i;
            }

            // Fourth pass: Process scene nodes with transforms
            var defaultScene = _model.DefaultScene;
            if (defaultScene != null)
            {
                foreach (var node in defaultScene.VisualChildren)
                {
                    ProcessNode(node, Matrix4x4.Identity, meshMap, nodeIndexMap);
                }
            }

            // Fifth pass: Process animations
            ProcessAnimations();

            // Sixth pass: Cache animation info for rendering optimization (must be done after ProcessAnimations)
            CacheAnimationInfo();

            Info($"Model loaded: {Nodes.Count} nodes, {Meshes.Count} meshes, {BoneCounter} bones, {(HasAnimations ? "with" : "without")} animation", "SharpGLTF");
        }

        private void ProcessNode(Node node, Matrix4x4 parentTransform, Dictionary<SharpGLTF.Schema2.Mesh, int> meshMap, Dictionary<Node, int> nodeIndexMap)
        {
            ProcessNodeWithParent(node, null, meshMap, nodeIndexMap);
        }
        
        private void ProcessNodeWithParent(Node node, SharpGltfNode? parentRenderNode, Dictionary<SharpGLTF.Schema2.Mesh, int> meshMap, Dictionary<Node, int> nodeIndexMap)
        {
            // Get node's local transform from glTF node
            var localMatrix = node.LocalMatrix;
            Matrix4x4.Decompose(localMatrix, out var scale, out var rotation, out var position);

            SharpGltfNode? currentRenderNode = null;
            
            // Get node index and morph weights from glTF
            int nodeIndex = nodeIndexMap.ContainsKey(node) ? nodeIndexMap[node] : -1;
            IReadOnlyList<float>? nodeMorphWeights = node.MorphWeights?.Count > 0 ? node.MorphWeights : null;
            IReadOnlyList<float>? meshMorphWeights = node.Mesh?.MorphWeights?.Count > 0 ? node.Mesh.MorphWeights : null;
            
            // Check if this node is part of a skin (joint in skeleton)
            bool isSkinned = !string.IsNullOrEmpty(node.Name) && _skinnedNodeNames.Contains(node.Name);

            // If this node has a mesh, create a SharpGltfNode for rendering
            if (node.Mesh != null && meshMap.ContainsKey(node.Mesh))
            {
                int meshIndex = meshMap[node.Mesh];
                
                // Create a node entry for each primitive in the mesh
                for (int i = 0; i < node.Mesh.Primitives.Count; i++)
                {
                    var renderNode = new SharpGltfNode
                    {
                        Position = position,
                        Rotation = rotation,
                        Scale = scale,
                        MeshIndex = meshIndex + i,
                        NodeName = node.Name,
                        HasAnimation = false,
                        IsSkinned = isSkinned,  // Mark if this is a skinned node
                        Parent = parentRenderNode,  // Set parent relationship
                        NodeIndex = nodeIndex,  // Store node index for morph weight animation lookup
                        NodeMorphWeights = nodeMorphWeights,  // Node-level weights
                        MeshMorphWeights = meshMorphWeights   // Mesh-level weights (fallback)
                    };
                    Nodes.Add(renderNode);
                    
                    // Use first render node as parent for children
                    if (currentRenderNode == null)
                        currentRenderNode = renderNode;
                }
            }
            else
            {
                // Node without mesh - create a transform-only node if it has children OR if it might be animated/used for lights
                // We need to keep leaf nodes (no children, no mesh) if they could be animated or have lights attached
                bool hasChildren = node.VisualChildren.Count() > 0;
                bool mightBeAnimated = !string.IsNullOrEmpty(node.Name); // Animated nodes need names
                
                if (hasChildren || mightBeAnimated)
                {
                    currentRenderNode = new SharpGltfNode
                    {
                        Position = position,
                        Rotation = rotation,
                        Scale = scale,
                        MeshIndex = -1,  // No mesh
                        NodeName = node.Name,
                        HasAnimation = false,
                        IsSkinned = isSkinned,  // Mark if this is a skinned node
                        Parent = parentRenderNode,
                        NodeIndex = nodeIndex  // Store even for transform-only nodes
                    };
                    Nodes.Add(currentRenderNode);
                }
            }

            // Recursively process children with proper parent
            foreach (var child in node.VisualChildren)
            {
                ProcessNodeWithParent(child, currentRenderNode ?? parentRenderNode, meshMap, nodeIndexMap);
            }
        }

        // Cache animation info for rendering optimization (called after ProcessAnimations)
        private void CacheAnimationInfo()
        {
            if (!HasAnimations || Animation == null) return;

            // Get list of animated bone names for faster lookup
            var animatedBoneNames = new HashSet<string>();
            foreach (var bone in Animation.GetBones())
            {
                if (!string.IsNullOrEmpty(bone.Name))
                {
                    animatedBoneNames.Add(bone.Name);
                }
            }

            // Update all render nodes with animation flags (glTF nodes already cached in ProcessNode)
            foreach (var renderNode in Nodes)
            {
                if (!string.IsNullOrEmpty(renderNode.NodeName))
                {
                    // Check if this node has animation
                    bool hasAnimation = animatedBoneNames.Contains(renderNode.NodeName);
                    renderNode.HasAnimation = hasAnimation;
                }
            }

            Info($"Animation cache updated: {animatedBoneNames.Count} animated nodes cached");
        }

        private void ProcessSkinning()
        {
            // Track all nodes that are part of a skin (joints)
            HashSet<string> skinnedNodeNames = new HashSet<string>();
            
            foreach (var skin in _model.LogicalSkins)
            {
                var joints = skin.JointsCount;
                Info($"Processing skin with {joints} joints", "SharpGLTF");

                for (int i = 0; i < joints; i++)
                {
                    var joint = skin.GetJoint(i);
                    
                    // Get inverse bind matrix
                    Matrix4x4 inverseBindMatrix = joint.InverseBindMatrix;

                    string boneName = joint.Joint.Name ?? $"Joint_{i}";
                    
                    // Mark this node as skinned
                    if (!string.IsNullOrEmpty(boneName))
                    {
                        skinnedNodeNames.Add(boneName);
                    }

                    if (!BoneInfoMap.ContainsKey(boneName))
                    {
                        BoneInfo boneInfo = new BoneInfo
                        {
                            Id = BoneCounter,
                            Offset = inverseBindMatrix
                        };
                        BoneInfoMap[boneName] = boneInfo;
                        BoneCounter++;
                    }
                }
            }
            
            // Store skinned node names for later use
            _skinnedNodeNames = skinnedNodeNames;
        }

        private void ProcessMesh(MeshPrimitive primitive)
        {
            List<Vertex> vertices = new List<Vertex>();
            List<Texture?> textures = new List<Texture?>();

            // Check primitive type - we only support TRIANGLES for now
            var drawMode = primitive.DrawPrimitiveType;
            if (drawMode != SharpGLTF.Schema2.PrimitiveType.TRIANGLES)
            {
                Warning($"Primitive type {drawMode} not supported, skipping. Only TRIANGLES are currently supported.", "SharpGLTF");
                return;
            }

            // Get positions
            var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
            var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector4Array();
            var tangents = primitive.GetVertexAccessor("TANGENT")?.AsVector4Array();
            var texCoords0 = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
            var texCoords1 = primitive.GetVertexAccessor("TEXCOORD_1")?.AsVector2Array();
            var colors = primitive.GetVertexAccessor("COLOR_0")?.AsColorArray();
            var joints = primitive.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
            var weights = primitive.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

            bool hasSkinning = joints != null && weights != null;
            bool hasMorphTargets = primitive.MorphTargetsCount > 0;

            if (positions == null)
            {
                Warning("Skipping mesh primitive without positions", "SharpGLTF");
                return;
            }

            // Get material color as fallback if no vertex colors
            // We'll use white as default and let the material processing handle the actual color
            Vector4 materialColor = Vector4.One; // Default white
            
            int vertexCount = positions.Count;
            
            // Determine if we need 32-bit indices based on HIGHEST vertex index that will be used
            // 16-bit indices (ushort) can only reference vertices 0-65535
            // The limit is based on vertex count, not index count, because indices reference vertices
            bool needs32BitIndices = vertexCount > 65535;

            // Log mesh info
            Info($"Processing mesh primitive:", "SharpGLTF");
            Info($"  - Draw mode: {drawMode}", "SharpGLTF");
            Info($"  - Vertices: {vertexCount}", "SharpGLTF");
            Info($"  - Index type: {(needs32BitIndices ? "32-bit" : "16-bit")} (max vertex index: {vertexCount - 1})", "SharpGLTF");
            Info($"  - Has normals: {normals != null}", "SharpGLTF");
            Info($"  - Has tangents: {tangents != null}", "SharpGLTF");
            Info($"  - Has texcoords: {texCoords0 != null}", "SharpGLTF");
            Info($"  - Has vertex colors: {colors != null}", "SharpGLTF");
            Info($"  - Has skinning: {hasSkinning}", "SharpGLTF");
            Info($"  - Has morph targets: {hasMorphTargets} (count: {primitive.MorphTargetsCount})", "SharpGLTF");
            
            // Debug: Check tangent W values
            if (tangents != null && tangents.Count > 0)
            {
                var wValues = tangents.Take(Math.Min(10, tangents.Count)).Select(t => t.W).ToArray();
                Info($"  - Tangent W values (first 10): [{string.Join(", ", wValues.Select(w => w.ToString("F2")))}]", "SharpGLTF");
            }
            
            // Get indices first (needed for tangent calculation)
            var indexAccessor = primitive.IndexAccessor;
            List<uint> indexList = new List<uint>();
            if (indexAccessor != null)
            {
                var indexArray = indexAccessor.AsIndicesArray();
                foreach (var idx in indexArray)
                {
                    indexList.Add(idx);
                }
            }
            else
            {
                // No indices - generate them
                for (int i = 0; i < vertexCount; i++)
                {
                    indexList.Add((uint)i);
                }
            }
            
            // Calculate tangents if missing (using simplified Lengyel method)
            IList<Vector4>? calculatedTangents = null;
            if (tangents == null && texCoords0 != null && normals != null)
            {
                Info($"  - Calculating missing tangents...", "SharpGLTF");
                calculatedTangents = CalculateTangents(positions, normals, texCoords0, indexList);
                Info($"  - Generated {calculatedTangents.Count} tangents", "SharpGLTF");
            }

            // Build vertices
            for (int i = 0; i < vertexCount; i++)
            {
                Vertex vertex = new Vertex();
                vertex.Position = positions[i];
                
                // Normal (convert Vector4 to Vector3)
                if (normals != null && i < normals.Count)
                {
                    var n = normals[i];
                    vertex.Normal = new Vector3(n.X, n.Y, n.Z);
                }
                else
                {
                    vertex.Normal = Vector3.UnitY;
                }
                
                // Tangent (vec4 with w = handedness)
                // Use calculated tangents if we generated them, otherwise use from glTF
                vertex.Tangent = tangents != null && i < tangents.Count ? tangents[i] 
                    : calculatedTangents != null && i < calculatedTangents.Count ? calculatedTangents[i]
                    : new Vector4(1, 0, 0, 1);
                
                // Texture coordinates
                vertex.TexCoord0 = texCoords0 != null && i < texCoords0.Count ? texCoords0[i] : Vector2.Zero;
                vertex.TexCoord1 = texCoords1 != null && i < texCoords1.Count ? texCoords1[i] : Vector2.Zero;
                
                // Use vertex color if available, otherwise use material color
                if (colors != null && i < colors.Count)
                {
                    vertex.Color = colors[i];
                }
                else
                {
                    vertex.Color = materialColor;
                }

                if (hasSkinning)
                {
                    var joint = joints![i];
                    var weight = weights![i];

                    // Set bone IDs directly as vec4 (matching shader vec4 joints_0)
                    vertex.Joints = joint;
                    vertex.BoneWeights = weight;
                }
                else
                {
                    vertex.Joints = Vector4.Zero;
                    vertex.BoneWeights = Vector4.Zero;
                }

                vertices.Add(vertex);
            }

            // Get indices - use 16-bit or 32-bit based on vertex count
            Mesh mesh;
            
            if (needs32BitIndices)
            {
                // Use 32-bit indices for large meshes (already extracted above)
                Info($"  - Indices: {indexList.Count} (32-bit for large mesh)", "SharpGLTF");
                mesh = new Mesh(vertices.ToArray(), indexList.ToArray(), hasSkinning);
            }
            else
            {
                // Use 16-bit indices for smaller meshes (memory efficient)
                // Convert from already-extracted 32-bit indices
                List<ushort> indices16 = new List<ushort>();
                foreach (var idx in indexList)
                {
                    indices16.Add((ushort)idx);
                }
                Info($"  - Indices: {indices16.Count} (16-bit for memory efficiency)", "SharpGLTF");
                mesh = new Mesh(vertices.ToArray(), indices16.ToArray(), hasSkinning);
            }

            // Store morph target information
            if (hasMorphTargets)
            {
                mesh.HasMorphTargets = true;
                mesh.GltfPrimitive = primitive;
                mesh.MorphTargetCount = primitive.MorphTargetsCount;
                Info($"  - Morph targets stored: {mesh.MorphTargetCount} targets", "SharpGLTF");
            }

            // Process material
            var material = primitive.Material;
            if (material != null)
            {
                ProcessMaterial(material, mesh);
            }

            Meshes.Add(mesh);
        }

        private void ProcessMaterial(Material material, Mesh mesh)
        {
            // Extract base color from material
            var baseColorChannel = material.FindChannel("BaseColor");
            if (baseColorChannel.HasValue)
            {
                try
                {
                    mesh.BaseColorFactor = baseColorChannel.Value.Color;
                    Info($"Material has BaseColor: {mesh.BaseColorFactor}", "SharpGLTF");
                }
                catch (Exception ex)
                {
                    Error($"Failed to extract color: {ex.Message}", "SharpGLTF");
                    mesh.BaseColorFactor = Vector4.One; // Fallback to white
                }
            }
            else
            {
                Info("Material has NO BaseColor channel - using white", "SharpGLTF");
                mesh.BaseColorFactor = Vector4.One;
            }
            
            // Extract metallic and roughness values from MetallicRoughness channel
            var metallicRoughnessChannel = material.FindChannel("MetallicRoughness");
            if (metallicRoughnessChannel.HasValue)
            {
                try
                {
                    // GetFactor extracts the scalar value by parameter name
                    mesh.MetallicFactor = metallicRoughnessChannel.Value.GetFactor("MetallicFactor");
                    mesh.RoughnessFactor = metallicRoughnessChannel.Value.GetFactor("RoughnessFactor");
                }
                catch (Exception ex)
                {
                    Error($"Failed to extract metallic/roughness: {ex.Message}", "SharpGLTF");
                    // Default to non-metallic, moderately rough for better shading visibility
                    mesh.MetallicFactor = 0.0f;
                    mesh.RoughnessFactor = 0.5f;
                }
            }
            else
            {
                // No metallic-roughness channel - use sensible defaults for better shading
                mesh.MetallicFactor = 0.0f;  // Non-metallic (better for showing diffuse lighting)
                mesh.RoughnessFactor = 0.5f; // Moderately rough
            }
            
            // Extract emissive factor from material
            var emissiveChannel = material.FindChannel("Emissive");
            if (emissiveChannel.HasValue)
            {
                try
                {
                    // Emissive is RGB color
                    var emissiveColor = emissiveChannel.Value.Color;
                    mesh.EmissiveFactor = new Vector3(emissiveColor.X, emissiveColor.Y, emissiveColor.Z);
                    Info($"Material has Emissive: {mesh.EmissiveFactor}", "SharpGLTF");
                }
                catch (Exception ex)
                {
                    Error($"Failed to extract emissive: {ex.Message}", "SharpGLTF");
                    mesh.EmissiveFactor = Vector3.Zero;
                }
            }
            else
            {
                Info("Material has NO Emissive channel", "SharpGLTF");
                mesh.EmissiveFactor = Vector3.Zero;
            }
            
            // Extract emissive strength from KHR_materials_emissive_strength extension
            // SharpGLTF has built-in support for this extension
            var emissiveStrengthExt = material.GetExtension<SharpGLTF.Schema2.MaterialEmissiveStrength>();
            if (emissiveStrengthExt != null)
            {
                mesh.EmissiveStrength = emissiveStrengthExt.EmissiveStrength;
                Info($"Material {material.LogicalIndex} (with extension): emissiveStrength = {mesh.EmissiveStrength}", "SharpGLTF");
            }
            else
            {
                mesh.EmissiveStrength = 1.0f; // Default value (no extension present)
                Info($"Material {material.LogicalIndex} (no extension): using default emissiveStrength = 1.0", "SharpGLTF");
            }

            // Extract IOR from KHR_materials_ior extension
            // SharpGLTF has built-in support for this extension
            var iorExt = material.GetExtension<SharpGLTF.Schema2.MaterialIOR>();
            if (iorExt != null)
            {
                mesh.IOR = iorExt.IndexOfRefraction;
                Info($"Material {material.LogicalIndex}: IOR = {mesh.IOR} (Air: 1.0, Water: 1.33, Glass: 1.5, Amber: 1.55, Diamond: 2.4)", "SharpGLTF");
            }
            else
            {
                mesh.IOR = 1.5f; // Default value for glass (no extension present)
                Info($"Material {material.LogicalIndex}: using default IOR = 1.5 (glass)", "SharpGLTF");
            }

            // Extract transmission from KHR_materials_transmission extension
            // This enables glass/transparent materials with refraction
            var transmissionExt = material.GetExtension<SharpGLTF.Schema2.MaterialTransmission>();
            if (transmissionExt != null)
            {
                mesh.TransmissionFactor = transmissionExt.TransmissionFactor;
                Info($"Material {material.LogicalIndex}: TransmissionFactor = {mesh.TransmissionFactor} (0.0 = opaque, 1.0 = fully transparent)", "SharpGLTF");
                
                // TODO: Transmission texture support (MaterialChannel "Transmission" from GetChannels)
                // Will be implemented when texture coordinate mapping is needed
            }
            else
            {
                mesh.TransmissionFactor = 0.0f; // Default: opaque (no refraction)
                Info($"Material {material.LogicalIndex}: using default TransmissionFactor = 0.0 (opaque)", "SharpGLTF");
            }

            // Extract volume properties from KHR_materials_volume extension (Beer's Law absorption)
            // This provides the color tint as light passes through transparent materials (e.g., amber, colored glass)
            var volumeExt = material.GetExtension<SharpGLTF.Schema2.MaterialVolume>();
            if (volumeExt != null)
            {
                mesh.ThicknessFactor = volumeExt.ThicknessFactor;
                mesh.AttenuationDistance = volumeExt.AttenuationDistance;
                mesh.AttenuationColor = volumeExt.AttenuationColor;
                
                // Load thickness texture if present
                var volumeThicknessChannel = material.FindChannel("VolumeThickness");
                if (volumeThicknessChannel != null && volumeThicknessChannel.Value.Texture != null)
                {
                    // Mark that we have a thickness texture - will be loaded after standard textures
                    mesh.ThicknessTexCoord = volumeThicknessChannel.Value.TextureCoordinate;
                    // Set to a temp value, will be updated after loading
                    mesh.ThicknessTextureIndex = 999;  // Temp marker that thickness texture exists
                    Info($"Material {material.LogicalIndex}: VolumeThickness texture found, using TEXCOORD_{mesh.ThicknessTexCoord}", "SharpGLTF");
                }
                else
                {
                    mesh.ThicknessTextureIndex = -1;
                    mesh.ThicknessTexCoord = 0;
                }
                
                Info($"Material {material.LogicalIndex}: Volume - Thickness={mesh.ThicknessFactor:F2}, " +
                    $"AttenuationColor=({mesh.AttenuationColor.X:F2}, {mesh.AttenuationColor.Y:F2}, {mesh.AttenuationColor.Z:F2}), " +
                    $"AttenuationDistance={(float.IsPositiveInfinity(mesh.AttenuationDistance) ? "Infinity" : mesh.AttenuationDistance.ToString("F2"))}", "SharpGLTF");
            }
            else if (mesh.TransmissionFactor > 0.0f)
            {
                // Transmission without volume extension: infinitely thin glass (no volume absorption)
                // Per glTF spec, thickness defaults to 0 when KHR_materials_volume is not present
                mesh.ThicknessFactor = 0.0f;  // Infinitely thin - sample at surface, not through volume
                mesh.AttenuationDistance = float.MaxValue;  // No absorption
                mesh.AttenuationColor = new Vector3(1.0f, 1.0f, 1.0f);  // White = no tint
                Info($"Material {material.LogicalIndex}: Transmission without volume - infinitely thin glass (thickness=0)", "SharpGLTF");
            }
            else
            {
                mesh.ThicknessFactor = 0.0f;
                mesh.AttenuationDistance = float.MaxValue;
                mesh.AttenuationColor = new Vector3(1.0f, 1.0f, 1.0f); // White = no tint
                Info($"Material {material.LogicalIndex}: opaque material, no volume needed", "SharpGLTF");
            }

            // Extract clearcoat properties from KHR_materials_clearcoat extension
            var clearcoatExt = material.GetExtension<SharpGLTF.Schema2.MaterialClearCoat>();
            if (clearcoatExt != null)
            {
                mesh.ClearcoatFactor = clearcoatExt.ClearCoatFactor;
                mesh.ClearcoatRoughness = clearcoatExt.RoughnessFactor;
                Info($"Material {material.LogicalIndex}: Clearcoat - Factor={mesh.ClearcoatFactor:F2}, Roughness={mesh.ClearcoatRoughness:F2}", "SharpGLTF");
            }
            else
            {
                mesh.ClearcoatFactor = 0.0f;  // No clearcoat
                mesh.ClearcoatRoughness = 0.0f;
                Info($"Material {material.LogicalIndex}: No clearcoat extension", "SharpGLTF");
            }

            // Extract texture transforms (KHR_texture_transform) for all texture types
            // Base Color
            if (baseColorChannel.HasValue)
            {
                var textureTransform = baseColorChannel.Value.TextureTransform;
                if (textureTransform != null)
                {
                    mesh.BaseColorTexOffset = textureTransform.Offset;
                    mesh.BaseColorTexRotation = textureTransform.Rotation;
                    mesh.BaseColorTexScale = textureTransform.Scale;
                    
                    // Check if this is NOT an identity transform (optimization flag)
                    mesh.HasBaseColorTexTransform = 
                        textureTransform.Offset != Vector2.Zero ||
                        textureTransform.Rotation != 0.0f ||
                        textureTransform.Scale != Vector2.One;
                    
                    if (mesh.HasBaseColorTexTransform)
                    {
                        Info($"Material {material.LogicalIndex}: BaseColor texture transform - " +
                            $"Offset=({mesh.BaseColorTexOffset.X:F2}, {mesh.BaseColorTexOffset.Y:F2}), " +
                            $"Rotation={mesh.BaseColorTexRotation:F2}rad, " +
                            $"Scale=({mesh.BaseColorTexScale.X:F2}, {mesh.BaseColorTexScale.Y:F2})", "SharpGLTF");
                    }
                }
            }
            
            // Metallic-Roughness
            if (metallicRoughnessChannel.HasValue)
            {
                var textureTransform = metallicRoughnessChannel.Value.TextureTransform;
                if (textureTransform != null)
                {
                    mesh.MetallicRoughnessTexOffset = textureTransform.Offset;
                    mesh.MetallicRoughnessTexRotation = textureTransform.Rotation;
                    mesh.MetallicRoughnessTexScale = textureTransform.Scale;
                    
                    // Check if this is NOT an identity transform (optimization flag)
                    mesh.HasMetallicRoughnessTexTransform = 
                        textureTransform.Offset != Vector2.Zero ||
                        textureTransform.Rotation != 0.0f ||
                        textureTransform.Scale != Vector2.One;
                    
                    if (mesh.HasMetallicRoughnessTexTransform)
                    {
                        Info($"Material {material.LogicalIndex}: MetallicRoughness texture transform - " +
                            $"Offset=({mesh.MetallicRoughnessTexOffset.X:F2}, {mesh.MetallicRoughnessTexOffset.Y:F2}), " +
                            $"Rotation={mesh.MetallicRoughnessTexRotation:F2}rad, " +
                            $"Scale=({mesh.MetallicRoughnessTexScale.X:F2}, {mesh.MetallicRoughnessTexScale.Y:F2})", "SharpGLTF");
                    }
                }
            }

            // Extract normal map scale and texture transform
            var normalChannel = material.FindChannel("Normal");
            if (normalChannel.HasValue && normalChannel.Value.Texture != null)
            {
                // Extract normal scale (strength of normal perturbation)
                // According to glTF spec, normalTexture.scale is the first parameter
                if (normalChannel.Value.Parameters.Count > 0)
                {
                    mesh.NormalMapScale = Convert.ToSingle(normalChannel.Value.Parameters[0].Value);
                    Info($"Material {material.LogicalIndex}: Normal scale = {mesh.NormalMapScale:F2}", "SharpGLTF");
                }
                
                // Check if texture has transform extension (KHR_texture_transform)
                var textureTransform = normalChannel.Value.TextureTransform;
                if (textureTransform != null)
                {
                    mesh.NormalTexOffset = textureTransform.Offset;
                    mesh.NormalTexRotation = textureTransform.Rotation;
                    mesh.NormalTexScale = textureTransform.Scale;
                    
                    // Check if this is NOT an identity transform (optimization flag)
                    mesh.HasNormalTexTransform = 
                        textureTransform.Offset != Vector2.Zero ||
                        textureTransform.Rotation != 0.0f ||
                        textureTransform.Scale != Vector2.One;
                    
                    if (mesh.HasNormalTexTransform)
                    {
                        Info($"Material {material.LogicalIndex}: Normal texture transform - " +
                            $"Offset=({mesh.NormalTexOffset.X:F2}, {mesh.NormalTexOffset.Y:F2}), " +
                            $"Rotation={mesh.NormalTexRotation:F2}rad, " +
                            $"Scale=({mesh.NormalTexScale.X:F2}, {mesh.NormalTexScale.Y:F2})", "SharpGLTF");
                    }
                }
                else
                {
                    Info($"Material {material.LogicalIndex}: Normal texture has no transform", "SharpGLTF");
                }
            }
            
            // Occlusion
            var occlusionChannel = material.FindChannel("Occlusion");
            if (occlusionChannel.HasValue)
            {
                // Get occlusion strength from the channel parameters
                var occlusionStrengthParam = occlusionChannel.Value.Parameters.FirstOrDefault(p => p.Name == "OcclusionStrength");
                if (occlusionStrengthParam != null)
                {
                    mesh.OcclusionStrength = Convert.ToSingle(occlusionStrengthParam.Value);
                    Info($"Material {material.LogicalIndex}: Occlusion strength = {mesh.OcclusionStrength:F2}", "SharpGLTF");
                }
                
                var textureTransform = occlusionChannel.Value.TextureTransform;
                if (textureTransform != null)
                {
                    mesh.OcclusionTexOffset = textureTransform.Offset;
                    mesh.OcclusionTexRotation = textureTransform.Rotation;
                    mesh.OcclusionTexScale = textureTransform.Scale;
                    
                    // Check if this is NOT an identity transform (optimization flag)
                    mesh.HasOcclusionTexTransform = 
                        textureTransform.Offset != Vector2.Zero ||
                        textureTransform.Rotation != 0.0f ||
                        textureTransform.Scale != Vector2.One;
                    
                    if (mesh.HasOcclusionTexTransform)
                    {
                        Info($"Material {material.LogicalIndex}: Occlusion texture transform - " +
                            $"Offset=({mesh.OcclusionTexOffset.X:F2}, {mesh.OcclusionTexOffset.Y:F2}), " +
                            $"Rotation={mesh.OcclusionTexRotation:F2}rad, " +
                            $"Scale=({mesh.OcclusionTexScale.X:F2}, {mesh.OcclusionTexScale.Y:F2})", "SharpGLTF");
                    }
                }
            }
            
            // Emissive
            if (emissiveChannel.HasValue)
            {
                var textureTransform = emissiveChannel.Value.TextureTransform;
                if (textureTransform != null)
                {
                    mesh.EmissiveTexOffset = textureTransform.Offset;
                    mesh.EmissiveTexRotation = textureTransform.Rotation;
                    mesh.EmissiveTexScale = textureTransform.Scale;
                    
                    // Check if this is NOT an identity transform (optimization flag)
                    mesh.HasEmissiveTexTransform = 
                        textureTransform.Offset != Vector2.Zero ||
                        textureTransform.Rotation != 0.0f ||
                        textureTransform.Scale != Vector2.One;
                    
                    if (mesh.HasEmissiveTexTransform)
                    {
                        Info($"Material {material.LogicalIndex}: Emissive texture transform - " +
                            $"Offset=({mesh.EmissiveTexOffset.X:F2}, {mesh.EmissiveTexOffset.Y:F2}), " +
                            $"Rotation={mesh.EmissiveTexRotation:F2}rad, " +
                            $"Scale=({mesh.EmissiveTexScale.X:F2}, {mesh.EmissiveTexScale.Y:F2})", "SharpGLTF");
                    }
                }
            }

            // Extract alpha mode and cutoff
            mesh.AlphaMode = material.Alpha;
            mesh.AlphaCutoff = material.AlphaCutoff;
            Info($"Material alpha mode: {mesh.AlphaMode}, cutoff: {mesh.AlphaCutoff}", "SharpGLTF");
            
            // Extract double-sided property
            mesh.DoubleSided = material.DoubleSided;
            Info($"Material double-sided: {mesh.DoubleSided}", "SharpGLTF");

            // Load textures
            LoadTexture(material, "BaseColor", mesh, 0);
            LoadTexture(material, "MetallicRoughness", mesh, 1);
            LoadTexture(material, "Normal", mesh, 2);
            LoadTexture(material, "Occlusion", mesh, 3);
            LoadTexture(material, "Emissive", mesh, 4);
            
            // Load thickness texture if present (KHR_materials_volume)
            // Check for temp marker 999 which means thickness texture was found
            if (mesh.ThicknessTextureIndex == 999)
            {
                mesh.ThicknessTextureIndex = mesh.Textures.Count;  // Set to actual index (should be 5)
                LoadTexture(material, "VolumeThickness", mesh, 5);
                Info($"Material {material.LogicalIndex}: Loaded thickness texture at index {mesh.ThicknessTextureIndex}", "SharpGLTF");
            }
            else
            {
                mesh.ThicknessTextureIndex = -1;  // No thickness texture
            }
            
            // Map material index to mesh for KHR_animation_pointer support
            int materialIndex = material.LogicalIndex;
            if (!MaterialToMeshMap.ContainsKey(materialIndex))
            {
                MaterialToMeshMap[materialIndex] = new List<Mesh>();
            }
            MaterialToMeshMap[materialIndex].Add(mesh);
        }

        private void LoadTexture(Material material, string channelName, Mesh mesh, int index)
        {
            var channel = material.FindChannel(channelName);
            if (channel == null || channel.Value.Texture == null)
            {
                mesh.Textures.Add(null);
                return;
            }

            var gltfTexture = channel.Value.Texture;
            var textureImage = gltfTexture.PrimaryImage;
            if (textureImage?.Content == null)
            {
                mesh.Textures.Add(null);
                return;
            }

            // Extract texture coordinate set index (which UV channel to use)
            int texCoord = channel.Value.TextureCoordinate;
            
            // Store texCoord index for this texture type
            switch (channelName)
            {
                case "BaseColor":
                    mesh.BaseColorTexCoord = texCoord;
                    break;
                case "MetallicRoughness":
                    mesh.MetallicRoughnessTexCoord = texCoord;
                    break;
                case "Normal":
                    mesh.NormalTexCoord = texCoord;
                    break;
                case "Occlusion":
                    mesh.OcclusionTexCoord = texCoord;
                    Info($"Material {material.LogicalIndex}: Occlusion texture uses TEXCOORD_{texCoord}", "SharpGLTF");
                    break;
                case "Emissive":
                    mesh.EmissiveTexCoord = texCoord;
                    break;
                case "VolumeThickness":
                    // ThicknessTexCoord already set during volume extension processing
                    Info($"Material {material.LogicalIndex}: Thickness texture uses TEXCOORD_{texCoord}", "SharpGLTF");
                    break;
            }

            // Extract sampler settings from glTF texture
            var sampler = gltfTexture.Sampler;
            var samplerSettings = ExtractSamplerSettings(sampler);

            // Create texture identifier that includes sampler settings
            // This ensures textures with different samplers are cached separately
            string textureId = $"image_{textureImage.LogicalIndex}_sampler_{samplerSettings.GetHashCode()}";

            // Determine pixel format based on texture type per glTF spec:
            // - BaseColor (index 0), Emissive (index 4): sRGB color space
            // - Others (Metallic-Roughness, Normal, Occlusion, VolumeThickness): Linear non-color data
            sg_pixel_format format = (channelName == "BaseColor" || channelName == "Emissive") 
                ? sg_pixel_format.SG_PIXELFORMAT_SRGB8A8 
                : sg_pixel_format.SG_PIXELFORMAT_RGBA8;

            // Debug: Log texture image index for occlusion
            if (channelName == "Occlusion")
            {
                Info($"Material {material.LogicalIndex}: Occlusion texture uses image index {textureImage.LogicalIndex}", "SharpGLTF");
            }

            // Look up texture in cache or create with proper sampler settings
            var imageData = textureImage.Content.Content.ToArray();
            var texture = TextureCache.Instance.GetOrCreate(textureId, imageData, format, samplerSettings);
            mesh.Textures.Add(texture);
        }

        private SamplerSettings ExtractSamplerSettings(SharpGLTF.Schema2.TextureSampler? sampler)
        {
            var settings = new SamplerSettings();
            
            if (sampler == null)
            {
                // Use glTF defaults: LINEAR filtering with mipmaps
                settings.MinFilter = sg_filter.SG_FILTER_LINEAR;
                settings.MagFilter = sg_filter.SG_FILTER_LINEAR;
                settings.MipmapFilter = sg_filter.SG_FILTER_LINEAR;
                settings.WrapU = sg_wrap.SG_WRAP_REPEAT;
                settings.WrapV = sg_wrap.SG_WRAP_REPEAT;
                return settings;
            }

            // Map glTF magFilter (no mipmap info, just LINEAR or NEAREST)
            settings.MagFilter = sampler.MagFilter switch
            {
                SharpGLTF.Schema2.TextureInterpolationFilter.NEAREST => sg_filter.SG_FILTER_NEAREST,
                SharpGLTF.Schema2.TextureInterpolationFilter.LINEAR => sg_filter.SG_FILTER_LINEAR,
                _ => sg_filter.SG_FILTER_LINEAR
            };

            // Map glTF minFilter (includes mipmap mode)
            // glTF combines base filter + mipmap filter into one enum
            // We need to split it into Sokol's separate min_filter and mipmap_filter
            settings.MinFilter = sampler.MinFilter switch
            {
                // No mipmaps
                SharpGLTF.Schema2.TextureMipMapFilter.NEAREST => sg_filter.SG_FILTER_NEAREST,
                SharpGLTF.Schema2.TextureMipMapFilter.LINEAR => sg_filter.SG_FILTER_LINEAR,
                
                // With mipmaps - base filter
                SharpGLTF.Schema2.TextureMipMapFilter.NEAREST_MIPMAP_NEAREST => sg_filter.SG_FILTER_NEAREST,
                SharpGLTF.Schema2.TextureMipMapFilter.LINEAR_MIPMAP_NEAREST => sg_filter.SG_FILTER_LINEAR,
                SharpGLTF.Schema2.TextureMipMapFilter.NEAREST_MIPMAP_LINEAR => sg_filter.SG_FILTER_NEAREST,
                SharpGLTF.Schema2.TextureMipMapFilter.LINEAR_MIPMAP_LINEAR => sg_filter.SG_FILTER_LINEAR,
                
                _ => sg_filter.SG_FILTER_LINEAR
            };

            // Extract mipmap filter from glTF's combined minFilter enum
            settings.MipmapFilter = sampler.MinFilter switch
            {
                // No mipmaps - set to NEAREST (will be ignored if texture has no mipmaps)
                SharpGLTF.Schema2.TextureMipMapFilter.NEAREST => sg_filter.SG_FILTER_NEAREST,
                SharpGLTF.Schema2.TextureMipMapFilter.LINEAR => sg_filter.SG_FILTER_NEAREST,
                
                // NEAREST mipmap interpolation (sharp transitions between mip levels)
                SharpGLTF.Schema2.TextureMipMapFilter.NEAREST_MIPMAP_NEAREST => sg_filter.SG_FILTER_NEAREST,
                SharpGLTF.Schema2.TextureMipMapFilter.LINEAR_MIPMAP_NEAREST => sg_filter.SG_FILTER_NEAREST,
                
                // LINEAR mipmap interpolation (trilinear - smooth between mip levels)
                SharpGLTF.Schema2.TextureMipMapFilter.NEAREST_MIPMAP_LINEAR => sg_filter.SG_FILTER_LINEAR,
                SharpGLTF.Schema2.TextureMipMapFilter.LINEAR_MIPMAP_LINEAR => sg_filter.SG_FILTER_LINEAR,
                
                _ => sg_filter.SG_FILTER_LINEAR
            };

            settings.WrapU = sampler.WrapS switch
            {
                SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => sg_wrap.SG_WRAP_MIRRORED_REPEAT,
                SharpGLTF.Schema2.TextureWrapMode.REPEAT => sg_wrap.SG_WRAP_REPEAT,
                _ => sg_wrap.SG_WRAP_REPEAT
            };

            settings.WrapV = sampler.WrapT switch
            {
                SharpGLTF.Schema2.TextureWrapMode.CLAMP_TO_EDGE => sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                SharpGLTF.Schema2.TextureWrapMode.MIRRORED_REPEAT => sg_wrap.SG_WRAP_MIRRORED_REPEAT,
                SharpGLTF.Schema2.TextureWrapMode.REPEAT => sg_wrap.SG_WRAP_REPEAT,
                _ => sg_wrap.SG_WRAP_REPEAT
            };

            return settings;
        }

        private void ProcessAnimations()
        {
            var stopwatch = Stopwatch.StartNew();
            
            if (_model.LogicalAnimations.Count == 0)
            {
                Info($"ProcessAnimations: No animations (0.000ms)", "SharpGLTF PROFILE");
                return;
            }

            // Use first animation for now
            var gltfAnim = _model.LogicalAnimations[0];
            Info($"Processing animation: {gltfAnim.Name ?? "Unnamed"}, duration: {gltfAnim.Duration}", "SharpGLTF");

            // Build complete node hierarchy - create a virtual root that contains all scene nodes
            SharpGltfNodeData rootNode = new SharpGltfNodeData
            {
                Name = "SceneRoot",
                Transformation = Matrix4x4.Identity,
                Children = new List<SharpGltfNodeData>(),
                ChildrenCount = 0
            };
            
            foreach (var sceneNode in _model.DefaultScene.VisualChildren)
            {
                rootNode.Children.Add(BuildNodeHierarchy(sceneNode));
                rootNode.ChildrenCount++;
            }
            
            Info($"Built node hierarchy with {rootNode.ChildrenCount} root children", "SharpGLTF");

            // Process ALL animations from the model
            foreach (var gltfAnimation in _model.LogicalAnimations)
            {
                float duration = (float)gltfAnimation.Duration;
                int ticksPerSecond = 1; // SharpGLTF uses seconds directly (no conversion needed)
                var animation = new SharpGltfAnimation(duration, ticksPerSecond, rootNode, BoneInfoMap);
                animation.Name = gltfAnimation.Name ?? $"Animation{Animations.Count}";

                // Process animation channels - store samplers WITHOUT pre-sampling
                foreach (var channel in gltfAnimation.Channels)
                {
                    var targetNode = channel.TargetNode;
                    
                    // Handle non-node targets (e.g., KHR_animation_pointer material properties)
                    if (targetNode == null)
                    {
                        string pointerPath = channel.TargetPointerPath;
                        
                        // Check if this is a material property animation
                        if (pointerPath != null && pointerPath.Contains("/materials/"))
                        {
                            ParseMaterialPropertyAnimation(channel, animation);
                        }
                        continue;
                    }
                    
                    // Check if this is a morph weight animation
                    if (channel.TargetNodePath == SharpGLTF.Schema2.PropertyPath.weights)
                    {
                        ParseMorphWeightAnimation(channel, targetNode, animation);
                        continue;
                    }
                    
                    string boneName = targetNode.Name ?? "Unnamed";

                    // Find or create bone
                    var bone = animation.FindBone(boneName);
                    if (bone == null)
                    {
                        bone = new SharpGltfBone(boneName, BoneInfoMap.ContainsKey(boneName) ? BoneInfoMap[boneName].Id : -1, targetNode);
                        animation.AddBone(bone);
                    }

                    // Store samplers for runtime evaluation (NO extraction)
                    bone.SetSamplers(
                        channel.GetTranslationSampler(),
                        channel.GetRotationSampler(),
                        channel.GetScaleSampler()
                    );
                }

                Animations.Add(animation);
                Info($"Loaded animation '{animation.Name}' with {animation.GetBoneIDMap().Count} bones, duration: {duration:F2}s", "SharpGLTF");
            }

            stopwatch.Stop();
            Info($"Processed {Animations.Count} animation(s)", "SharpGLTF");
            Info($"ProcessAnimations completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)", "SharpGLTF PROFILE");
        }

        private void ParseMaterialPropertyAnimation(AnimationChannel channel, SharpGltfAnimation animation)
        {
            string pointerPath = channel.TargetPointerPath;

            // Parse the pointer path to extract material index and property type
            // Example paths:
            // "/materials/2/normalTexture/extensions/KHR_texture_transform/rotation"
            // "/materials/2/normalTexture/extensions/KHR_texture_transform/offset"
            
            if (!TryParseMaterialPointerPath(pointerPath, out int materialIndex, out MaterialAnimationTarget target))
                return;

            // Create material property animation object
            var matPropAnim = new MaterialPropertyAnimation
            {
                MaterialIndex = materialIndex,
                Target = target,
                PropertyPath = pointerPath
            };

            // Extract keyframe data from the sampler
            var sampler = channel._GetSampler();
            if (sampler == null)
                return;

            // Sample based on target type (float or Vector2)
            if (matPropAnim.IsFloatType)
            {
                // Rotation is a float (radians)
                var floatSampler = sampler as IAnimationSampler<float>;
                if (floatSampler != null)
                {
                    foreach (var (time, value) in floatSampler.GetLinearKeys())
                    {
                        matPropAnim.FloatKeyframes.Add((time, value));
                    }
                }
            }
            else
            {
                // Offset/Scale are Vector2
                var vec2Sampler = sampler as IAnimationSampler<Vector2>;
                if (vec2Sampler != null)
                {
                    foreach (var (time, value) in vec2Sampler.GetLinearKeys())
                    {
                        matPropAnim.Vector2Keyframes.Add((time, value));
                    }
                }
            }

            // Add to animation's material animations list
            animation.MaterialAnimations.Add(matPropAnim);
        }

        private bool TryParseMaterialPointerPath(string pointerPath, out int materialIndex, out MaterialAnimationTarget target)
        {
            materialIndex = -1;
            target = MaterialAnimationTarget.NormalTextureRotation;

            if (string.IsNullOrEmpty(pointerPath))
                return false;

            // Parse material index from path like "/materials/2/..."
            var parts = pointerPath.Split('/');
            int matIdx = -1;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] == "materials" && int.TryParse(parts[i + 1], out matIdx))
                {
                    materialIndex = matIdx;
                    break;
                }
            }

            if (materialIndex < 0)
                return false;

            // Determine the property type from the path
            if (pointerPath.Contains("normalTexture"))
            {
                if (pointerPath.EndsWith("/rotation"))
                    target = MaterialAnimationTarget.NormalTextureRotation;
                else if (pointerPath.EndsWith("/offset"))
                    target = MaterialAnimationTarget.NormalTextureOffset;
                else if (pointerPath.EndsWith("/scale"))
                    target = MaterialAnimationTarget.NormalTextureScale;
                else
                    return false;
            }
            else if (pointerPath.Contains("thicknessTexture"))
            {
                if (pointerPath.EndsWith("/rotation"))
                    target = MaterialAnimationTarget.ThicknessTextureRotation;
                else if (pointerPath.EndsWith("/offset"))
                    target = MaterialAnimationTarget.ThicknessTextureOffset;
                else if (pointerPath.EndsWith("/scale"))
                    target = MaterialAnimationTarget.ThicknessTextureScale;
                else
                    return false;
            }
            else
            {
                return false; // Unsupported texture type
            }

            return true;
        }

        private void ParseMorphWeightAnimation(AnimationChannel channel, Node targetNode, SharpGltfAnimation animation)
        {
            // Get node index - IReadOnlyList doesn't have IndexOf, so find manually
            int nodeIndex = -1;
            var logicalNodes = _model.LogicalNodes;
            for (int i = 0; i < logicalNodes.Count; i++)
            {
                if (logicalNodes[i] == targetNode)
                {
                    nodeIndex = i;
                    break;
                }
            }
            
            if (nodeIndex < 0)
                return;

            // Create morph weight animation object
            var morphAnim = new MorphWeightAnimation
            {
                NodeIndex = nodeIndex,
                NodeName = targetNode.Name ?? "Unnamed"
            };

            // Extract keyframe data from the sampler
            var sampler = channel._GetSampler();
            if (sampler == null)
                return;

            // Morph weights are float arrays (one float per morph target)
            var arraySampler = sampler as IAnimationSampler<float[]>;
            if (arraySampler != null)
            {
                foreach (var (time, weights) in arraySampler.GetLinearKeys())
                {
                    morphAnim.Keyframes.Add(((float)time, weights));
                }
            }

            // Add to animation
            animation.MorphAnimations.Add(morphAnim);
            Info($"  - Added morph weight animation for node '{morphAnim.NodeName}' with {morphAnim.Keyframes.Count} keyframes", "SharpGLTF");
        }

        private SharpGltfNodeData BuildNodeHierarchy(Node node)
        {
            SharpGltfNodeData nodeData = new SharpGltfNodeData
            {
                Name = node.Name ?? "Unnamed",
                Transformation = node.LocalMatrix,
                Children = new List<SharpGltfNodeData>(),
                ChildrenCount = 0
            };

            foreach (var child in node.VisualChildren)
            {
                nodeData.Children.Add(BuildNodeHierarchy(child));
                nodeData.ChildrenCount++;
            }

            return nodeData;
        }
        
        /// <summary>
        /// Calculate tangents using the Lengyel method (simplified version of MikkTSpace)
        /// Reference: https://github.com/KhronosGroup/glTF/tree/master/specification/2.0#meshes
        /// </summary>
        private static IList<Vector4> CalculateTangents(
            IReadOnlyList<Vector3> positions,
            IReadOnlyList<Vector4> normals,
            IReadOnlyList<Vector2> texCoords,
            IList<uint> indices)
        {
            int vertexCount = positions.Count;
            var tangents = new Vector4[vertexCount];
            var tan1 = new Vector3[vertexCount];
            var tan2 = new Vector3[vertexCount];

            // Calculate tangent and bitangent for each triangle
            for (int i = 0; i < indices.Count; i += 3)
            {
                int i1 = (int)indices[i];
                int i2 = (int)indices[i + 1];
                int i3 = (int)indices[i + 2];

                Vector3 v1 = positions[i1];
                Vector3 v2 = positions[i2];
                Vector3 v3 = positions[i3];

                Vector2 w1 = texCoords[i1];
                Vector2 w2 = texCoords[i2];
                Vector2 w3 = texCoords[i3];

                float x1 = v2.X - v1.X;
                float x2 = v3.X - v1.X;
                float y1 = v2.Y - v1.Y;
                float y2 = v3.Y - v1.Y;
                float z1 = v2.Z - v1.Z;
                float z2 = v3.Z - v1.Z;

                float s1 = w2.X - w1.X;
                float s2 = w3.X - w1.X;
                float t1 = w2.Y - w1.Y;
                float t2 = w3.Y - w1.Y;

                float r = s1 * t2 - s2 * t1;
                if (Math.Abs(r) < 0.000001f) r = 1.0f;
                r = 1.0f / r;

                Vector3 sdir = new Vector3(
                    (t2 * x1 - t1 * x2) * r,
                    (t2 * y1 - t1 * y2) * r,
                    (t2 * z1 - t1 * z2) * r
                );

                Vector3 tdir = new Vector3(
                    (s1 * x2 - s2 * x1) * r,
                    (s1 * y2 - s2 * y1) * r,
                    (s1 * z2 - s2 * z1) * r
                );

                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;

                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }

            // Orthogonalize and calculate handedness
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 n = new Vector3(normals[i].X, normals[i].Y, normals[i].Z);
                Vector3 t = tan1[i];

                // Gram-Schmidt orthogonalize
                Vector3 tangent = Vector3.Normalize(t - n * Vector3.Dot(n, t));

                // Calculate handedness
                float w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0.0f) ? -1.0f : 1.0f;

                tangents[i] = new Vector4(tangent.X, tangent.Y, tangent.Z, w);
            }

            return tangents.ToList();
        }

        public void Dispose()
        {
            foreach (var mesh in Meshes)
            {
                mesh.Dispose();
            }
            Meshes.Clear();
        }
    }
}
