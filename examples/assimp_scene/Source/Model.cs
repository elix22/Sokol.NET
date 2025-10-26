using System;
using System.IO;
using System.Diagnostics;
using Sokol;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SG.sg_vertex_format;
using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using static Sokol.StbImage;
using Assimp.Configs;
using Assimp;

namespace Sokol
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector4 Color;
        public Vector2 TexCoord;
        public Vector4 BoneIDs;    // Up to 4 bone influences
        public Vector4 BoneWeights; // Corresponding weights
    }

    public class Model
    {
        private const int MAX_BONE_INFLUENCE = 4;

        public string FilePath { get; private set; } = "";
        public List<Mesh>? SimpleMeshes = new List<Mesh>();
        public Sokol.Scene? SceneGraph { get; private set; }
        
        private Dictionary<string, BoneInfo> m_BoneInfoMap = new Dictionary<string, BoneInfo>();
        private int m_BoneCounter = 0;
        private Dictionary<int, Mesh> m_MeshIndexToMesh = new Dictionary<int, Mesh>();

        public Model(string filePath)
        {
            FilePath = filePath;
            SimpleMeshes = new List<Mesh>();
            FileSystem.Instance.LoadFile(filePath, OnFileLoaded);
        }

        public Dictionary<string, BoneInfo> GetBoneInfoMap() => m_BoneInfoMap;
        public int GetBoneCount() => m_BoneCounter;
        public void SetBoneCount(int count) => m_BoneCounter = count;

        void OnFileLoaded(string filePath, byte[]? buffer, FileLoadStatus status)
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                Info($"AnimatedModel: File '{filePath}' loaded successfully, size: {buffer.Length} bytes");

                var stream = new MemoryStream(buffer);
                PostProcessSteps ppSteps = PostProcessSteps.JoinIdenticalVertices |
                                          PostProcessSteps.ValidateDataStructure |
                                          PostProcessSteps.ImproveCacheLocality |
                                          PostProcessSteps.FindDegenerates |
                                          PostProcessSteps.FindInvalidData |
                                          PostProcessSteps.GenerateUVCoords |
                                          PostProcessSteps.TransformUVCoords |
                                          PostProcessSteps.FindInstances |
                                          PostProcessSteps.LimitBoneWeights |
                                          PostProcessSteps.OptimizeMeshes |
                                          PostProcessSteps.SplitByBoneCount |
                                          PostProcessSteps.Triangulate |
                                          PostProcessSteps.GenerateSmoothNormals |
                                          PostProcessSteps.FlipWindingOrder |  // Fix winding order for proper back-face culling
                                          PostProcessSteps.CalculateTangentSpace;

                AssimpContext importer = new AssimpContext();
                importer.SetConfig(new NormalSmoothingAngleConfig(66f));

                string formatHint = Path.GetExtension(filePath).TrimStart('.');
                if (string.IsNullOrEmpty(formatHint))
                {
                    formatHint = "gltf2";
                }

                Assimp.Scene? assimpScene = importer.ImportFileFromStream(stream, ppSteps, formatHint);
                if (assimpScene != null)
                {
                    Info($"AnimatedModel: Successfully loaded model (format: {formatHint}).");
                    ProcessScene(assimpScene);
                }
                else
                {
                    Info($"AnimatedModel: Failed to load model (format: {formatHint}).");
                }
            }
            else
            {
                Info($"AnimatedModel: Failed to load file: {status}");
            }
        }

        unsafe private void ProcessScene(Assimp.Scene assimpScene)
        {
            // First, process all meshes and store them
            for (int i = 0; i < assimpScene.MeshCount; i++)
            {
                var assimpMesh = assimpScene.Meshes[i];
                var mesh = ProcessMesh(assimpMesh, assimpScene);
                m_MeshIndexToMesh[i] = mesh;
                SimpleMeshes?.Add(mesh);
            }

            // Build the scene graph from Assimp's node hierarchy
            if (assimpScene.RootNode != null)
            {
                Node rootNode = ProcessNode(assimpScene.RootNode, assimpScene);
                SceneGraph = new Sokol.Scene(rootNode);
                SceneGraph.SetBoneInfoMap(m_BoneInfoMap, m_BoneCounter);
                
                // Update all transforms initially
                SceneGraph.UpdateTransforms();
                
                Info($"Scene graph built with root node: {rootNode.Name}");
                Info($"=== Scene Processing Complete ===");
            }
        }

        private Node ProcessNode(Assimp.Node assimpNode, Assimp.Scene assimpScene)
        {
            // Convert Assimp's row-major matrix to System.Numerics column-major
            Matrix4x4 localTransform = AssimpHelpers.ToNumerics(assimpNode.Transform);
            
            Node node = new Node(assimpNode.Name, localTransform);

            // Attach meshes to this node
            if (assimpNode.HasMeshes)
            {
                foreach (int meshIndex in assimpNode.MeshIndices)
                {
                    if (m_MeshIndexToMesh.ContainsKey(meshIndex))
                    {
                        var mesh = m_MeshIndexToMesh[meshIndex];
                        node.AddMesh(mesh);
                    }
                }
            }

            // Recursively process children
            foreach (Assimp.Node childNode in assimpNode.Children)
            {
                Node childSceneNode = ProcessNode(childNode, assimpScene);
                node.AddChild(childSceneNode);
            }

            return node;
        }

        unsafe private Mesh ProcessMesh(Assimp.Mesh mesh, Assimp.Scene assimpScene)
        {
            // Get material diffuse color for meshes without vertex colors
            Vector4 materialColor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f); // default white
            
            if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < assimpScene.MaterialCount)
            {
                var material = assimpScene.Materials[mesh.MaterialIndex];
                if (material.HasColorDiffuse)
                {
                    // ColorDiffuse returns Vector4 with X=R, Y=G, Z=B, W=A
                    materialColor = material.ColorDiffuse;
                }
            }
            
            Vertex[] vertices = new Vertex[mesh.VertexCount];
            
            // Initialize vertices with default bone data
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                Vertex vertex = new Vertex();
                vertex.Position = mesh.Vertices[i];  // Already Vector3
                
                // Extract normals
                if (mesh.HasNormals)
                {
                    vertex.Normal = mesh.Normals[i];
                }
                else
                {
                    vertex.Normal = Vector3.UnitY; // Default up normal
                }
                
                // Use vertex colors if available, otherwise use material diffuse color
                if (mesh.HasVertexColors(0))
                {
                    var color = mesh.VertexColorChannels[0][i];
                    vertex.Color = new Vector4(color.X, color.Y, color.Z, color.W);
                }
                else
                {
                    vertex.Color = materialColor; // Use material color instead of white
                }
                
                if (mesh.HasTextureCoords(0))
                {
                    var uv = mesh.TextureCoordinateChannels[0][i];
                    vertex.TexCoord = new Vector2(uv.X, uv.Y);
                }
                else
                {
                    vertex.TexCoord = Vector2.Zero;
                }

                // Initialize bone IDs and weights
                vertex.BoneIDs = new Vector4(-1, -1, -1, -1);
                vertex.BoneWeights = Vector4.Zero;

                vertices[i] = vertex;
            }

            // Extract bone weights
            ExtractBoneWeights(ref vertices, mesh, assimpScene);

            // Build index buffer
            List<UInt16> indices = new List<UInt16>();
            foreach (var face in mesh.Faces)
            {
                if (face.IndexCount != 3)
                {
                    Info($"Warning: Skipping non-triangular face with {face.IndexCount} indices.");
                    continue;
                }
                
                indices.Add((UInt16)face.Indices[0]);
                indices.Add((UInt16)face.Indices[1]);
                indices.Add((UInt16)face.Indices[2]);
            }
            // Load textures
            List<Texture> textures = Texture.LoadTextures(assimpScene, mesh, FilePath);

            return new Mesh(vertices, indices.ToArray(), textures);
        }

        private void SetVertexBoneData(ref Vertex vertex, int boneID, float weight)
        {
            if (weight == 0) return;

            for (int i = 0; i < MAX_BONE_INFLUENCE; i++)
            {
                float currentBoneID = i == 0 ? vertex.BoneIDs.X :
                                     i == 1 ? vertex.BoneIDs.Y :
                                     i == 2 ? vertex.BoneIDs.Z :
                                     vertex.BoneIDs.W;

                if (currentBoneID < 0)
                {
                    // Set bone ID
                    if (i == 0) vertex.BoneIDs.X = boneID;
                    else if (i == 1) vertex.BoneIDs.Y = boneID;
                    else if (i == 2) vertex.BoneIDs.Z = boneID;
                    else vertex.BoneIDs.W = boneID;

                    // Set weight
                    if (i == 0) vertex.BoneWeights.X = weight;
                    else if (i == 1) vertex.BoneWeights.Y = weight;
                    else if (i == 2) vertex.BoneWeights.Z = weight;
                    else vertex.BoneWeights.W = weight;

                    break;
                }
            }
        }

        private void ExtractBoneWeights(ref Vertex[] vertices, Assimp.Mesh mesh, Assimp.Scene assimpScene)
        {
            for (int boneIndex = 0; boneIndex < mesh.BoneCount; ++boneIndex)
            {
                int boneID = -1;
                string boneName = mesh.Bones[boneIndex].Name;
                
                if (!m_BoneInfoMap.ContainsKey(boneName))
                {
                    BoneInfo newBoneInfo = new BoneInfo
                    {
                        Id = m_BoneCounter,
                        Offset = AssimpHelpers.ToNumerics(mesh.Bones[boneIndex].OffsetMatrix)  // Transpose to row-major
                    };
                    m_BoneInfoMap[boneName] = newBoneInfo;
                    boneID = m_BoneCounter;
                    m_BoneCounter++;
                }
                else
                {
                    boneID = m_BoneInfoMap[boneName].Id;
                }

                Debug.Assert(boneID != -1);
                
                var weights = mesh.Bones[boneIndex].VertexWeights;
                for (int weightIndex = 0; weightIndex < weights.Count; ++weightIndex)
                {
                    int vertexId = weights[weightIndex].VertexID;
                    float weight = weights[weightIndex].Weight;
                    
                    Debug.Assert(vertexId < vertices.Length);
                    
                    if (weight != 0)
                    {
                        var vertex = vertices[vertexId];
                        SetVertexBoneData(ref vertex, boneID, weight);
                        vertices[vertexId] = vertex;
                    }
                }
            }
        }

    }
}
