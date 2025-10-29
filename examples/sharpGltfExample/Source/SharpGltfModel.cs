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

        public SharpGltfModel(ModelRoot model)
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
            List<ushort> indices = new List<ushort>();
            List<Texture?> textures = new List<Texture?>();

            // Get positions
            var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
            var normals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
            var texCoords = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
            var joints = primitive.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
            var weights = primitive.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();

            bool hasSkinning = joints != null && weights != null;

            if (positions == null)
            {
                Console.WriteLine("[SharpGLTF] Skipping mesh primitive without positions");
                return;
            }

            int vertexCount = positions.Count;

            // Build vertices
            for (int i = 0; i < vertexCount; i++)
            {
                Vertex vertex = new Vertex();
                vertex.Position = positions[i];
                vertex.Normal = normals != null && i < normals.Count ? normals[i] : Vector3.UnitY;
                vertex.TexCoord = texCoords != null && i < texCoords.Count ? texCoords[i] : Vector2.Zero;

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

            // Get indices
            var indexAccessor = primitive.IndexAccessor;
            if (indexAccessor != null)
            {
                var indexArray = indexAccessor.AsIndicesArray();
                foreach (var idx in indexArray)
                {
                    indices.Add((ushort)idx);
                }
            }
            else
            {
                // No indices - generate them
                for (int i = 0; i < vertexCount; i++)
                {
                    indices.Add((ushort)i);
                }
            }

            // Create mesh
            var mesh = new Mesh(vertices.ToArray(), indices.ToArray(), hasSkinning);

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
            // Simple approach: force alpha to 1.0 to fix transparency issues
            // The shader will use textures if available
            mesh.BaseColorFactor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            mesh.MetallicFactor = 1.0f;
            mesh.RoughnessFactor = 1.0f;
            mesh.EmissiveFactor = Vector3.Zero;

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

            // Load texture from memory
            var imageData = textureImage.Content.Content.ToArray();
            var texture = Texture.LoadFromMemory(imageData, $"{channelName}-texture");
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
