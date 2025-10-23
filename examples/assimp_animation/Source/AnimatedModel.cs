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
    public struct AnimatedVertex
    {
        public Vector3 Position;
        public Vector4 Color;
        public Vector2 TexCoord;
        public Vector4 BoneIDs;    // Up to 4 bone influences
        public Vector4 BoneWeights; // Corresponding weights
    }

    public class AnimatedModel
    {
        private const int MAX_BONE_INFLUENCE = 4;

        public string FilePath { get; private set; } = "";
        public List<SimpleMesh>? SimpleMeshes;
        private Dictionary<string, BoneInfo> m_BoneInfoMap = new Dictionary<string, BoneInfo>();
        private int m_BoneCounter = 0;

        public AnimatedModel(string filePath)
        {
            FilePath = filePath;
            SimpleMeshes = new List<SimpleMesh>();
            FileSystem.Instance.LoadFile(filePath, OnFileLoaded);
        }

        public Dictionary<string, BoneInfo> GetBoneInfoMap() => m_BoneInfoMap;
        public int GetBoneCount() => m_BoneCounter;
        public void SetBoneCount(int count) => m_BoneCounter = count;

        void OnFileLoaded(string filePath, byte[]? buffer, FileLoadStatus status)
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                Console.WriteLine($"AnimatedModel: File '{filePath}' loaded successfully, size: {buffer.Length} bytes");

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
                                          PostProcessSteps.CalculateTangentSpace |
                                          PostProcessSteps.FlipWindingOrder;

                AssimpContext importer = new AssimpContext();
                importer.SetConfig(new NormalSmoothingAngleConfig(66f));

                string formatHint = Path.GetExtension(filePath).TrimStart('.');
                if (string.IsNullOrEmpty(formatHint))
                {
                    formatHint = "gltf2";
                }

                Scene? scene = importer.ImportFileFromStream(stream, ppSteps, formatHint);
                if (scene != null)
                {
                    Console.WriteLine($"AnimatedModel: Successfully loaded model (format: {formatHint}).");
                    ProcessScene(scene);
                }
                else
                {
                    Console.WriteLine($"AnimatedModel: Failed to load model (format: {formatHint}).");
                }
            }
            else
            {
                Console.WriteLine($"AnimatedModel: Failed to load file: {status}");
            }
        }

        unsafe private void ProcessScene(Scene scene)
        {
            foreach (Mesh m in scene.Meshes)
            {
                ProcessMesh(m, scene);
            }
        }

        unsafe private void ProcessMesh(Mesh mesh, Scene scene)
        {
            List<AnimatedVertex> vertices = new List<AnimatedVertex>();
            
            // Initialize vertices with default bone data
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                AnimatedVertex vertex = new AnimatedVertex();
                vertex.Position = mesh.Vertices[i];  // Already Vector3
                vertex.Color = new Vector4(
                    SimpleModel.NextRandom(0.0f, 1.0f),
                    SimpleModel.NextRandom(0.0f, 1.0f),
                    SimpleModel.NextRandom(0.0f, 1.0f),
                    1.0f
                );
                
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

                vertices.Add(vertex);
            }

            // Extract bone weights
            ExtractBoneWeights(vertices, mesh, scene);

            // Build index buffer
            List<UInt16> indices = new List<UInt16>();
            foreach (var face in mesh.Faces)
            {
                if (face.IndexCount != 3) continue;
                
                indices.Add((UInt16)face.Indices[0]);
                indices.Add((UInt16)face.Indices[1]);
                indices.Add((UInt16)face.Indices[2]);
            }

            // Convert vertices to float array (17 floats per vertex)
            // 3 pos + 4 color + 2 uv + 4 bone IDs + 4 bone weights = 17 floats
            int floatsPerVertex = 17;
            float[] vertexData = new float[vertices.Count * floatsPerVertex];
            
            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                int offset = i * floatsPerVertex;
                
                vertexData[offset + 0] = v.Position.X;
                vertexData[offset + 1] = v.Position.Y;
                vertexData[offset + 2] = v.Position.Z;
                vertexData[offset + 3] = v.Color.X;
                vertexData[offset + 4] = v.Color.Y;
                vertexData[offset + 5] = v.Color.Z;
                vertexData[offset + 6] = v.Color.W;
                vertexData[offset + 7] = v.TexCoord.X;
                vertexData[offset + 8] = v.TexCoord.Y;
                vertexData[offset + 9] = v.BoneIDs.X;
                vertexData[offset + 10] = v.BoneIDs.Y;
                vertexData[offset + 11] = v.BoneIDs.Z;
                vertexData[offset + 12] = v.BoneIDs.W;
                vertexData[offset + 13] = v.BoneWeights.X;
                vertexData[offset + 14] = v.BoneWeights.Y;
                vertexData[offset + 15] = v.BoneWeights.Z;
                vertexData[offset + 16] = v.BoneWeights.W;
            }

            // Load textures
            List<Texture> textures = LoadTextures(mesh, scene);

            SimpleMeshes?.Add(new SimpleMesh(null!, vertexData, indices.ToArray(), textures));
        }

        private void SetVertexBoneData(ref AnimatedVertex vertex, int boneID, float weight)
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

        private void ExtractBoneWeights(List<AnimatedVertex> vertices, Mesh mesh, Scene scene)
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
                    
                    Debug.Assert(vertexId < vertices.Count);
                    
                    if (weight != 0)
                    {
                        var vertex = vertices[vertexId];
                        SetVertexBoneData(ref vertex, boneID, weight);
                        vertices[vertexId] = vertex;
                    }
                }
            }
        }

        unsafe private List<Texture> LoadTextures(Mesh mesh, Scene scene)
        {
            List<Texture> textures = new List<Texture>();
            var material = scene.Materials[mesh.MaterialIndex];

            int textureDiffuseCount = material.GetMaterialTextureCount(TextureType.Diffuse);
            if (textureDiffuseCount > 0)
            {
                TextureSlot texSlot;
                material.GetMaterialTexture(TextureType.Diffuse, 0, out texSlot);

                if (texSlot.FilePath[0] == '*')
                {
                    // Embedded texture
                    string indexStr = texSlot.FilePath.Substring(1);
                    if (int.TryParse(indexStr, out int textureIndex) && textureIndex < scene.TextureCount)
                    {
                        EmbeddedTexture embeddedTexture = scene.Textures[textureIndex];
                        if (embeddedTexture.IsCompressed)
                        {
                            int pngWidth = 0, pngHeight = 0, channels = 0, desiredChannels = 4;
                            byte* pixels = stbi_load_csharp(
                                embeddedTexture.CompressedData[0],
                                embeddedTexture.CompressedData.Length,
                                ref pngWidth,
                                ref pngHeight,
                                ref channels,
                                desiredChannels
                            );

                            if (pixels != null)
                            {
                                textures.Add(new Texture(pixels, pngWidth, pngHeight));
                                stbi_image_free_csharp(pixels);
                            }
                        }
                    }
                }
            }

            return textures;
        }
    }
}
