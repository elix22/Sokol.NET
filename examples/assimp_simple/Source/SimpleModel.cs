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


public class SimpleModel
{
    static readonly Random random = new Random();
    public static float NextRandom(float min, float max) { return (float)((random.NextDouble() * (max - min)) + min); }

    public string FilePath { get; private set; } = "";

    public List<SimpleMesh>? SimpleMeshes;
    public SimpleModel(string filePath)
    {
        FilePath = filePath;
        SimpleMeshes = new List<SimpleMesh>();
        FileSystem.Instance.LoadFile(filePath, OnFileLoaded);
    }


    void OnFileLoaded(string filePath, byte[]? buffer, FileLoadStatus status)
    {
        if (status == FileLoadStatus.Success && buffer != null)
        {
            Console.WriteLine($"Assimp: File '{filePath}' loaded successfully, size: {buffer.Length} bytes");

            // Check first few bytes to verify data is XML (not binary plist)
            int previewLength = Math.Min(10, buffer.Length);
            Console.WriteLine($"Assimp: First {previewLength} bytes: {BitConverter.ToString(buffer, 0, previewLength)}");

            // File successfully loaded, now parse with Assimp
            var stream = new MemoryStream(buffer);
            PostProcessSteps ppSteps = PostProcessPreset.TargetRealTimeQuality | PostProcessSteps.FlipWindingOrder;
            AssimpContext importer = new AssimpContext();
            importer.SetConfig(new NormalSmoothingAngleConfig(66f));


            // Extract file extension for other files
            string formatHint = Path.GetExtension(filePath).TrimStart('.');
            if (string.IsNullOrEmpty(formatHint))
            {
                formatHint = "gltf2"; // fallback default
            }
            Console.WriteLine($"Assimp: Using format hint: '{formatHint}' for non-binary file");


            Scene? scene = importer.ImportFileFromStream(stream, ppSteps, formatHint);
            if (scene != null)
            {
                Console.WriteLine($"Assimp: Successfully loaded model (format: {formatHint}).");
                ProcesScene(scene);
            }
            else
            {
                Console.WriteLine($"Assimp: Failed to load model (format: {formatHint}).");
            }
        }
        else
        {
            Console.WriteLine($"Assimp: Failed to load file: {status}");
        }
    }

    unsafe private void ProcesScene(Scene scene)
    {
        var m_sceneMin = new Vector3(1e10f, 1e10f, 1e10f);
        var m_sceneMax = new Vector3(-1e10f, -1e10f, -1e10f);

        foreach (Mesh m in scene.Meshes)
        {
            List<Vector3> verts = m.Vertices;
            List<Vector3>? norms = (m.HasNormals) ? m.Normals : null;
            List<Vector3>? uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;

            int floats_per_vertex = 9; // 3 pos + 3 random color + 2 uv + 1 padding
            float[] float_vertices = new float[verts.Count * floats_per_vertex]; // 9 floats per vertex
            UInt16[] int_indices = new UInt16[m.FaceCount * 3];

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 pos = verts[i];
                Vector3 norm = (norms != null) ? norms[i] : new Vector3(0, 0, 0);
                Vector3 uv = (uvs != null) ? uvs[i] : new Vector3(0, 0, 0);

                float_vertices[i * floats_per_vertex + 0] = pos.X;
                float_vertices[i * floats_per_vertex + 1] = pos.Y;
                float_vertices[i * floats_per_vertex + 2] = pos.Z;
                float_vertices[i * floats_per_vertex + 3] = NextRandom(0.0f, 1.0f);
                float_vertices[i * floats_per_vertex + 4] = NextRandom(0.0f, 1.0f);
                float_vertices[i * floats_per_vertex + 5] = NextRandom(0.0f, 1.0f);
                float_vertices[i * floats_per_vertex + 6] = 1.0f;
                float_vertices[i * floats_per_vertex + 7] = uv.X;
                float_vertices[i * floats_per_vertex + 8] = uv.Y;

                m_sceneMin.X = Math.Min(m_sceneMin.X, pos.X);
                m_sceneMin.Y = Math.Min(m_sceneMin.Y, pos.Y);
                m_sceneMin.Z = Math.Min(m_sceneMin.Z, pos.Z);

                m_sceneMax.X = Math.Max(m_sceneMax.X, pos.X);
                m_sceneMax.Y = Math.Max(m_sceneMax.Y, pos.Y);
                m_sceneMax.Z = Math.Max(m_sceneMax.Z, pos.Z);
            }

            List<Face> faces = m.Faces;
            for (int i = 0; i < faces.Count; i++)
            {
                Face f = faces[i];

                //Ignore non-triangle faces
                if (f.IndexCount != 3)
                {
                    continue;
                }

                int_indices[i * 3 + 0] = (UInt16)(f.Indices[0]);
                int_indices[i * 3 + 1] = (UInt16)(f.Indices[1]);
                int_indices[i * 3 + 2] = (UInt16)(f.Indices[2]);
            }

            var material = scene.Materials[m.MaterialIndex];

            List<Texture> textures = new List<Texture>();
            int texture_diffuse_count = material.GetMaterialTextureCount(TextureType.Diffuse);
            if (texture_diffuse_count > 0)
            {
                TextureSlot texSlot;
                material.GetMaterialTexture(TextureType.Diffuse, 0, out texSlot);
                Console.WriteLine($"Assimp: Mesh uses diffuse texture: {texSlot.FilePath}");

                if (texSlot.FilePath[0] == '*')
                {
                    // this is an embedded texture
                    texSlot.FilePath = texSlot.FilePath.Substring(1);
                    if (!int.TryParse(texSlot.FilePath, out int textureIndex))
                    {
                        Console.WriteLine($"Assimp: Failed to parse embedded texture index from '{texSlot.FilePath}'");
                        textureIndex = -1;
                    }
                    else
                    {
                        Console.WriteLine($"Assimp: Found embedded texture index: {textureIndex}");
                        if (textureIndex < scene.TextureCount)
                        {
                            EmbeddedTexture embeddedTexture = scene.Textures[textureIndex];
                            if (embeddedTexture.IsCompressed)
                            {
                                Console.WriteLine($"Assimp: Embedded texture is compressed, size: {embeddedTexture.CompressedData.Length} bytes");
                                int png_width = 0, png_height = 0, channels = 0, desired_channels = 4;

                                byte* pixels = stbi_load_flipped_csharp(
                                    embeddedTexture.CompressedData[0],
                                    embeddedTexture.CompressedData.Length,
                                    ref png_width,
                                    ref png_height,
                                    ref channels,
                                    desired_channels
                                );

                                if (pixels == null)
                                {
                                    Console.WriteLine($"Assimp: Failed to decode embedded texture index: {textureIndex}");
                                }
                                else
                                {

                                    textures.Add(new Texture(pixels, png_width, png_height));

                                    // Free the native STB image data
                                    stbi_image_free_csharp(pixels);
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Assimp: Embedded texture is uncompressed, size: {embeddedTexture.NonCompressedData.Length} texels");

                                // Convert Texel[] to byte[] (each Texel has 4 bytes: B, G, R, A)
                                var texelData = embeddedTexture.NonCompressedData;
                                byte[] imageBytes = new byte[texelData.Length * 4];

                                for (int i = 0; i < texelData.Length; i++)
                                {
                                    var texel = texelData[i];
                                    imageBytes[i * 4 + 0] = texel.R;  // Red
                                    imageBytes[i * 4 + 1] = texel.G;  // Green
                                    imageBytes[i * 4 + 2] = texel.B;  // Blue
                                    imageBytes[i * 4 + 3] = texel.A;  // Alpha
                                }

                                fixed (byte* ptr = imageBytes)
                                {
                                    textures.Add(new Texture(ptr, embeddedTexture.Width, embeddedTexture.Height));
                                }
                            }

                        }
                        else
                        {
                            Console.WriteLine($"Embedded texture index out of range: {textureIndex}");

                        }
                    }
                }
                else
                {
                    string filePath = util_get_file_path(texSlot.FilePath);
                    string modelDirectory = Path.GetDirectoryName(FilePath) ?? "";
                    string fullTexturePath = Path.Combine(modelDirectory, filePath);
                    textures.Add(new Texture(fullTexturePath));
                }

            }

            SimpleMeshes.Add(new SimpleMesh(this, float_vertices, int_indices, textures));
        }

    }

}



