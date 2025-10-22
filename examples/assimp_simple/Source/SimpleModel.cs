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
using Assimp.Configs;
using static Sokol.SFetch;
using static Sokol.SDebugText;
using static assimp_simple_app_shader_cs.Shaders;
using Assimp;


public class SimpleModel
{
    static readonly Random random = new Random();
    public static float NextRandom(float min, float max) { return (float)((random.NextDouble() * (max - min)) + min); }

    public List<SimpleMesh>? SimpleMeshes;
    public SimpleModel(string filePath)
    {
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

            // Extract file extension from the file path to use as format hint
            string formatHint = Path.GetExtension(filePath).TrimStart('.');
            if (string.IsNullOrEmpty(formatHint))
            {
                formatHint = "collada"; // fallback default
            }

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

    private void ProcesScene(Scene scene)
    {
        var m_sceneMin = new Vector3(1e10f, 1e10f, 1e10f);
        var m_sceneMax = new Vector3(-1e10f, -1e10f, -1e10f);

        foreach (Mesh m in scene.Meshes)
        {
            List<Vector3> verts = m.Vertices;
            List<Vector3>? norms = (m.HasNormals) ? m.Normals : null;
            List<Vector3>? uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;

            float[] float_vertices = new float[verts.Count * 7]; // 8 floats per vertex
            UInt16[] int_indices = new UInt16[m.FaceCount * 3];

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 pos = verts[i];
                Vector3 norm = (norms != null) ? norms[i] : new Vector3(0, 0, 0);
                Vector3 uv = (uvs != null) ? uvs[i] : new Vector3(0, 0, 0);

                float_vertices[i * 7 + 0] = pos.X;
                float_vertices[i * 7 + 1] = pos.Y;
                float_vertices[i * 7 + 2] = pos.Z;
                float_vertices[i * 7 + 3] = NextRandom(0.0f, 1.0f);
                float_vertices[i * 7 + 4] = NextRandom(0.0f, 1.0f);
                float_vertices[i * 7 + 5] = NextRandom(0.0f, 1.0f);
                float_vertices[i * 7 + 6] = 1.0f;

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

            SimpleMeshes.Add(new SimpleMesh(float_vertices, int_indices));
        }

    }
}
