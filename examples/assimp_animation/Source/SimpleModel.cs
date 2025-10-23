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

            foreach (Mesh m in scene.Meshes)
            {
                List<Vector3> verts = m.Vertices;
                List<Vector3>? norms = (m.HasNormals) ? m.Normals : null;
                List<Vector3>? uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;

                Vertex[] vertices = new Vertex[verts.Count];
                UInt16[] indices = new UInt16[m.FaceCount * 3];

                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 pos = verts[i];
                    Vector3 norm = (norms != null) ? norms[i] : new Vector3(0, 0, 0);
                    Vector3 uv = (uvs != null) ? uvs[i] : new Vector3(0, 0, 0);

                    vertices[i] = new Vertex(pos, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector2(uv.X, uv.Y));
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

                    indices[i * 3 + 0] = (UInt16)(f.Indices[0]);
                    indices[i * 3 + 1] = (UInt16)(f.Indices[1]);
                    indices[i * 3 + 2] = (UInt16)(f.Indices[2]);
                }

                List<Texture> textures = Texture.LoadTextures(scene, m,FilePath);

                SimpleMeshes.Add(new SimpleMesh(this, vertices, indices, textures));
            }
        }

    }
}

