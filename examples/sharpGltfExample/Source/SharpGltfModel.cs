using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using SharpGLTF.Schema2;
using static Sokol.SG;
using static Sokol.SLog;

namespace Sokol
{
    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;
        
        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }
        
        // Transform bounding box by a matrix
        public BoundingBox Transform(Matrix4x4 matrix)
        {
            // Transform all 8 corners of the box
            Vector3[] corners = new Vector3[8]
            {
                new Vector3(Min.X, Min.Y, Min.Z),
                new Vector3(Max.X, Min.Y, Min.Z),
                new Vector3(Min.X, Max.Y, Min.Z),
                new Vector3(Max.X, Max.Y, Min.Z),
                new Vector3(Min.X, Min.Y, Max.Z),
                new Vector3(Max.X, Min.Y, Max.Z),
                new Vector3(Min.X, Max.Y, Max.Z),
                new Vector3(Max.X, Max.Y, Max.Z)
            };
            
            Vector3 newMin = new Vector3(float.MaxValue);
            Vector3 newMax = new Vector3(float.MinValue);
            
            foreach (var corner in corners)
            {
                Vector3 transformed = Vector3.Transform(corner, matrix);
                newMin = Vector3.Min(newMin, transformed);
                newMax = Vector3.Max(newMax, transformed);
            }
            
            return new BoundingBox(newMin, newMax);
        }
    }

    public class SharpGltfNode
    {
        public Matrix4x4 Transform;
        public int MeshIndex = -1;  // Index into SharpGltfModel.Meshes
    }

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

        public int GetAnimationCount() => Animations.Count;
        
        public string GetCurrentAnimationName() => 
            HasAnimations ? Animations[CurrentAnimationIndex].Name : "None";
        
        public void SetCurrentAnimation(int index)
        {
            if (index >= 0 && index < Animations.Count)
            {
                CurrentAnimationIndex = index;
                Console.WriteLine($"[SharpGLTF] Switched to animation '{GetCurrentAnimationName()}' (index {index})");
            }
        }

        public void NextAnimation()
        {
            if (Animations.Count > 0)
            {
                CurrentAnimationIndex = (CurrentAnimationIndex + 1) % Animations.Count;
                Console.WriteLine($"[SharpGLTF] Next animation: '{GetCurrentAnimationName()}'");
            }
        }

        public void PreviousAnimation()
        {
            if (Animations.Count > 0)
            {
                CurrentAnimationIndex = (CurrentAnimationIndex - 1 + Animations.Count) % Animations.Count;
                Console.WriteLine($"[SharpGLTF] Previous animation: '{GetCurrentAnimationName()}'");
            }
        }

        private ModelRoot _model;
        private List<AnimationChannel> _pendingChannels = new List<AnimationChannel>();
        private float _animationDuration;
        private int _currentChannelIndex = 0;

        public SharpGltfModel(ModelRoot model, string? filePath = null)
        {
            _model = model;
            ProcessModel();
        }

        private void ProcessModel()
        {
            Console.WriteLine($"[SharpGLTF] Processing model with {_model.LogicalNodes.Count} nodes, {_model.LogicalMeshes.Count} meshes");

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

            // Third pass: Process scene nodes with transforms
            var defaultScene = _model.DefaultScene;
            if (defaultScene != null)
            {
                foreach (var node in defaultScene.VisualChildren)
                {
                    ProcessNode(node, Matrix4x4.Identity, meshMap);
                }
            }

            // Fourth pass: Process animations
            ProcessAnimations();

            Console.WriteLine($"[SharpGLTF] Model loaded: {Nodes.Count} nodes, {Meshes.Count} meshes, {BoneCounter} bones, {(HasAnimations ? "with" : "without")} animation");
        }

        private void ProcessNode(Node node, Matrix4x4 parentTransform, Dictionary<SharpGLTF.Schema2.Mesh, int> meshMap)
        {
            // Get node's local transform
            var localMatrix = node.LocalMatrix;
            
            // Combine with parent transform
            Matrix4x4 worldTransform = localMatrix * parentTransform;

            // If this node has a mesh, create a SharpGltfNode for rendering
            if (node.Mesh != null && meshMap.ContainsKey(node.Mesh))
            {
                int meshIndex = meshMap[node.Mesh];
                
                // Create a node entry for each primitive in the mesh
                for (int i = 0; i < node.Mesh.Primitives.Count; i++)
                {
                    var renderNode = new SharpGltfNode
                    {
                        Transform = worldTransform,
                        MeshIndex = meshIndex + i
                    };
                    Nodes.Add(renderNode);
                }
            }

            // Recursively process children
            foreach (var child in node.VisualChildren)
            {
                ProcessNode(child, worldTransform, meshMap);
            }
        }

        private void ProcessSkinning()
        {
            foreach (var skin in _model.LogicalSkins)
            {
                var joints = skin.JointsCount;
                Console.WriteLine($"[SharpGLTF] Processing skin with {joints} joints");

                for (int i = 0; i < joints; i++)
                {
                    var joint = skin.GetJoint(i);
                    
                    // Get inverse bind matrix
                    Matrix4x4 inverseBindMatrix = joint.InverseBindMatrix;

                    string boneName = joint.Joint.Name ?? $"Joint_{i}";

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
        }

        private void ProcessMesh(MeshPrimitive primitive)
        {
            List<Vertex> vertices = new List<Vertex>();
            List<Texture?> textures = new List<Texture?>();

            // Check primitive type - we only support TRIANGLES for now
            var drawMode = primitive.DrawPrimitiveType;
            if (drawMode != SharpGLTF.Schema2.PrimitiveType.TRIANGLES)
            {
                Console.WriteLine($"[SharpGLTF] Warning: Primitive type {drawMode} not supported, skipping. Only TRIANGLES are currently supported.");
                return;
            }

            // Get positions
            var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
            var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
            var texCoords = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
            var colors = primitive.GetVertexAccessor("COLOR_0")?.AsColorArray();
            var joints = primitive.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
            var weights = primitive.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

            bool hasSkinning = joints != null && weights != null;

            if (positions == null)
            {
                Console.WriteLine("[SharpGLTF] Skipping mesh primitive without positions");
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
            Console.WriteLine($"[SharpGLTF] Processing mesh primitive:");
            Console.WriteLine($"  - Draw mode: {drawMode}");
            Console.WriteLine($"  - Vertices: {vertexCount}");
            Console.WriteLine($"  - Index type: {(needs32BitIndices ? "32-bit" : "16-bit")} (max vertex index: {vertexCount - 1})");
            Console.WriteLine($"  - Has normals: {normals != null}");
            Console.WriteLine($"  - Has texcoords: {texCoords != null}");
            Console.WriteLine($"  - Has vertex colors: {colors != null}");
            Console.WriteLine($"  - Has skinning: {hasSkinning}");

            // Build vertices
            for (int i = 0; i < vertexCount; i++)
            {
                Vertex vertex = new Vertex();
                vertex.Position = positions[i];
                vertex.Normal = normals != null && i < normals.Count ? normals[i] : Vector3.UnitY;
                vertex.TexCoord = texCoords != null && i < texCoords.Count ? texCoords[i] : Vector2.Zero;
                
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

                    // Set bone IDs as unsigned integers (matching shader uvec4)
                    vertex.SetBoneIDs(new int[] { (int)joint.X, (int)joint.Y, (int)joint.Z, (int)joint.W });
                    vertex.BoneWeights = weight;
                }
                else
                {
                    vertex.SetBoneIDs(new int[] { 0, 0, 0, 0 });
                    vertex.BoneWeights = Vector4.Zero;
                }

                vertices.Add(vertex);
            }

            // Get indices - use 16-bit or 32-bit based on vertex count
            Mesh mesh;
            var indexAccessor = primitive.IndexAccessor;
            
            if (needs32BitIndices)
            {
                // Use 32-bit indices for large meshes
                List<uint> indices32 = new List<uint>();
                if (indexAccessor != null)
                {
                    var indexArray = indexAccessor.AsIndicesArray();
                    foreach (var idx in indexArray)
                    {
                        indices32.Add(idx);
                    }
                }
                else
                {
                    // No indices - generate them
                    for (int i = 0; i < vertexCount; i++)
                    {
                        indices32.Add((uint)i);
                    }
                }
                Console.WriteLine($"  - Indices: {indices32.Count} (32-bit for large mesh)");
                mesh = new Mesh(vertices.ToArray(), indices32.ToArray(), hasSkinning);
            }
            else
            {
                // Use 16-bit indices for smaller meshes (memory efficient)
                List<ushort> indices16 = new List<ushort>();
                if (indexAccessor != null)
                {
                    var indexArray = indexAccessor.AsIndicesArray();
                    foreach (var idx in indexArray)
                    {
                        indices16.Add((ushort)idx);
                    }
                }
                else
                {
                    // No indices - generate them
                    for (int i = 0; i < vertexCount; i++)
                    {
                        indices16.Add((ushort)i);
                    }
                }
                Console.WriteLine($"  - Indices: {indices16.Count} (16-bit for memory efficiency)");
                mesh = new Mesh(vertices.ToArray(), indices16.ToArray(), hasSkinning);
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
                    Console.WriteLine($"[SharpGLTF] Material has BaseColor: {mesh.BaseColorFactor}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SharpGLTF] Failed to extract color: {ex.Message}");
                    mesh.BaseColorFactor = Vector4.One; // Fallback to white
                }
            }
            else
            {
                Console.WriteLine("[SharpGLTF] Material has NO BaseColor channel - using white");
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
                    Console.WriteLine($"[SharpGLTF] Failed to extract metallic/roughness: {ex.Message}");
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
                    Console.WriteLine($"[SharpGLTF] Material has Emissive: {mesh.EmissiveFactor}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SharpGLTF] Failed to extract emissive: {ex.Message}");
                    mesh.EmissiveFactor = Vector3.Zero;
                }
            }
            else
            {
                Console.WriteLine("[SharpGLTF] Material has NO Emissive channel");
                mesh.EmissiveFactor = Vector3.Zero;
            }
            
            // Extract emissive strength from KHR_materials_emissive_strength extension
            // SharpGLTF has built-in support for this extension
            var emissiveStrengthExt = material.GetExtension<SharpGLTF.Schema2.MaterialEmissiveStrength>();
            if (emissiveStrengthExt != null)
            {
                mesh.EmissiveStrength = emissiveStrengthExt.EmissiveStrength;
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex} (with extension): emissiveStrength = {mesh.EmissiveStrength}");
            }
            else
            {
                mesh.EmissiveStrength = 1.0f; // Default value (no extension present)
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex} (no extension): using default emissiveStrength = 1.0");
            }

            // Extract IOR from KHR_materials_ior extension
            // SharpGLTF has built-in support for this extension
            var iorExt = material.GetExtension<SharpGLTF.Schema2.MaterialIOR>();
            if (iorExt != null)
            {
                mesh.IOR = iorExt.IndexOfRefraction;
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex}: IOR = {mesh.IOR} (Air: 1.0, Water: 1.33, Glass: 1.5, Amber: 1.55, Diamond: 2.4)");
            }
            else
            {
                mesh.IOR = 1.5f; // Default value for glass (no extension present)
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex}: using default IOR = 1.5 (glass)");
            }

            // Extract transmission from KHR_materials_transmission extension
            // This enables glass/transparent materials with refraction
            var transmissionExt = material.GetExtension<SharpGLTF.Schema2.MaterialTransmission>();
            if (transmissionExt != null)
            {
                mesh.TransmissionFactor = transmissionExt.TransmissionFactor;
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex}: TransmissionFactor = {mesh.TransmissionFactor} (0.0 = opaque, 1.0 = fully transparent)");
                
                // TODO: Transmission texture support (MaterialChannel "Transmission" from GetChannels)
                // Will be implemented when texture coordinate mapping is needed
            }
            else
            {
                mesh.TransmissionFactor = 0.0f; // Default: opaque (no refraction)
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex}: using default TransmissionFactor = 0.0 (opaque)");
            }

            // Extract volume properties from KHR_materials_volume extension (Beer's Law absorption)
            // This provides the color tint as light passes through transparent materials (e.g., amber, colored glass)
            var volumeExt = material.GetExtension<SharpGLTF.Schema2.MaterialVolume>();
            if (volumeExt != null)
            {
                mesh.ThicknessFactor = volumeExt.ThicknessFactor;
                mesh.AttenuationDistance = volumeExt.AttenuationDistance;
                mesh.AttenuationColor = volumeExt.AttenuationColor;
                
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex}: Volume - Thickness={mesh.ThicknessFactor:F2}, " +
                    $"AttenuationColor=({mesh.AttenuationColor.X:F2}, {mesh.AttenuationColor.Y:F2}, {mesh.AttenuationColor.Z:F2}), " +
                    $"AttenuationDistance={(float.IsPositiveInfinity(mesh.AttenuationDistance) ? "Infinity" : mesh.AttenuationDistance.ToString("F2"))}");
            }
            else if (mesh.TransmissionFactor > 0.0f)
            {
                // Fallback for transmission without volume: use base color as attenuation color
                // This provides approximate colored glass effect (e.g., amber tint)
                mesh.ThicknessFactor = 1.0f;  // Use unit thickness as default
                mesh.AttenuationDistance = 1.0f;  // Moderate absorption
                // Use base color's RGB as attenuation color
                mesh.AttenuationColor = new Vector3(
                    mesh.BaseColorFactor.X,
                    mesh.BaseColorFactor.Y,
                    mesh.BaseColorFactor.Z
                );
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex}: No volume extension, using base color as attenuation fallback - " +
                    $"Color=({mesh.AttenuationColor.X:F2}, {mesh.AttenuationColor.Y:F2}, {mesh.AttenuationColor.Z:F2})");
            }
            else
            {
                mesh.ThicknessFactor = 0.0f;
                mesh.AttenuationDistance = float.MaxValue;
                mesh.AttenuationColor = new Vector3(1.0f, 1.0f, 1.0f); // White = no tint
                Console.WriteLine($"[SharpGLTF] Material {material.LogicalIndex}: opaque material, no volume needed");
            }

            // Extract alpha mode and cutoff
            mesh.AlphaMode = material.Alpha;
            mesh.AlphaCutoff = material.AlphaCutoff;
            Console.WriteLine($"[SharpGLTF] Material alpha mode: {mesh.AlphaMode}, cutoff: {mesh.AlphaCutoff}");

            // Load textures
            LoadTexture(material, "BaseColor", mesh, 0);
            LoadTexture(material, "MetallicRoughness", mesh, 1);
            LoadTexture(material, "Normal", mesh, 2);
            LoadTexture(material, "Occlusion", mesh, 3);
            LoadTexture(material, "Emissive", mesh, 4);
        }

        private void LoadTexture(Material material, string channelName, Mesh mesh, int index)
        {
            var channel = material.FindChannel(channelName);
            if (channel == null || channel.Value.Texture == null)
            {
                mesh.Textures.Add(null);
                return;
            }

            var textureImage = channel.Value.Texture.PrimaryImage;
            if (textureImage?.Content == null)
            {
                mesh.Textures.Add(null);
                return;
            }

            // Create unique identifier for texture to enable caching
            // Using texture's logical index as identifier (unique within a model)
            string textureId = $"texture_{textureImage.LogicalIndex}_{channelName}";

            // All textures use RGBA8 format - manual srgb_to_linear() conversion in shader
            sg_pixel_format format = sg_pixel_format.SG_PIXELFORMAT_RGBA8;

            // Load texture from memory using cache
            var imageData = textureImage.Content.Content.ToArray();
            var texture = TextureCache.Instance.GetOrCreate(textureId, imageData, format);
            mesh.Textures.Add(texture);
        }

        private void ProcessAnimations()
        {
            var stopwatch = Stopwatch.StartNew();
            
            if (_model.LogicalAnimations.Count == 0)
            {
                Console.WriteLine($"[SharpGLTF PROFILE] ProcessAnimations: No animations (0.000ms)");
                return;
            }

            // Use first animation for now
            var gltfAnim = _model.LogicalAnimations[0];
            Console.WriteLine($"[SharpGLTF] Processing animation: {gltfAnim.Name ?? "Unnamed"}, duration: {gltfAnim.Duration}");

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
            
            Console.WriteLine($"[SharpGLTF] Built node hierarchy with {rootNode.ChildrenCount} root children");

            // Process ALL animations from the model
            foreach (var gltfAnimation in _model.LogicalAnimations)
            {
                float duration = (float)gltfAnimation.Duration;
                int ticksPerSecond = 1; // SharpGLTF uses seconds, we'll convert
                var animation = new SharpGltfAnimation(duration, ticksPerSecond, rootNode, BoneInfoMap);
                animation.Name = gltfAnimation.Name ?? $"Animation{Animations.Count}";

                // Process animation channels - store samplers WITHOUT pre-sampling
                foreach (var channel in gltfAnimation.Channels)
                {
                    var targetNode = channel.TargetNode;
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
                Console.WriteLine($"[SharpGLTF] Loaded animation '{animation.Name}' with {animation.GetBoneIDMap().Count} bones, duration: {duration:F2}s");
            }

            stopwatch.Stop();
            Console.WriteLine($"[SharpGLTF] Processed {Animations.Count} animation(s)");
            Console.WriteLine($"[SharpGLTF PROFILE] ProcessAnimations completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed.TotalSeconds:F3}s)");
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
