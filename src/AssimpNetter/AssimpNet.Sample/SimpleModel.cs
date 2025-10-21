/*
* Copyright (c) 2012-2020 AssimpNet - Nicholas Woodfield
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/

using Assimp.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Veldrid;
using SN = System.Numerics;

namespace Assimp.Sample
{
    //Extremely simple example of how to load an Assimp scene into GPU objects. Only static meshes supported and extremely simple materials (single diffuse color + diffuse texture). All of the meshes
    //are packed into a single Vertex/Index buffer and each Assimp scene node that has meshes will produce a single draw call with the appropiate World transform.
    //
    //Once loaded, the model is scaled down based on its bounding volume so we can render it without any precision issue. Presumably you would find an appropiate scale for your 3D models.
    public sealed class SimpleModel : IDisposable
    {
        private List<MeshPart> m_meshesData;
        private List<SimpleMaterial> m_materials;
        private List<MeshDrawCall> m_meshesToDraw;

        private DeviceBuffer m_vertexBuffer;
        private DeviceBuffer m_indexBuffer;

        private Pipeline m_pipelineState;
        private ShaderSetDescription m_shaderSet;

        private DeviceBuffer m_worldParam;
        private DeviceBuffer m_wvpParam;
        private DeviceBuffer m_lightPosParam;
        private DeviceBuffer m_camPosParam;

        private ResourceLayout m_worldLightCBLayout;
        private ResourceSet m_worldLightCBSet;

        private ResourceLayout m_materialCBLayout;

        private SN.Vector3 m_sceneCenter, m_sceneMin, m_sceneMax;

        public SN.Matrix4x4 WorldMatrix { get; set; }
        public SN.Vector3 LightPosition { get; set; }

        public SN.Vector3 SceneCenter => m_sceneCenter;
        public SN.Vector3 SceneMin => m_sceneMin;
        public SN.Vector3 SceneMax => m_sceneMax;

        public static SimpleModel LoadFromFile(string filePath, GraphicsDevice gd, PostProcessSteps ppSteps, params PropertyConfig[] configs)
        {
            if(!File.Exists(filePath) || gd == null)
                return null;

            AssimpContext importer = new AssimpContext();
            if(configs != null)
            {
                foreach(PropertyConfig config in configs)
                    importer.SetConfig(config);
            }

            Scene scene = importer.ImportFile(filePath, ppSteps);
            if(scene == null)
                return null;

            SimpleModel model = new SimpleModel();
            model.WorldMatrix = SN.Matrix4x4.Identity;
            if(!model.CreateVertexBuffer(scene, gd, Path.GetDirectoryName(filePath)))
                return null;

            model.ComputeBoundingBox(scene);
            model.AdjustModelScale();

            return model;
        }

        public SimpleModel()
        {
            m_meshesData = new List<MeshPart>();
            m_materials = new List<SimpleMaterial>();
            m_meshesToDraw = new List<MeshDrawCall>();
        }

        public void Draw(CommandList cmdList, Camera cam)
        {
            cmdList.SetVertexBuffer(0, m_vertexBuffer);
            cmdList.SetIndexBuffer(m_indexBuffer, IndexFormat.UInt32);
            cmdList.SetPipeline(m_pipelineState);

            SN.Matrix4x4 transform = WorldMatrix;

            for(int i = 0; i < m_meshesToDraw.Count; i++)
            {
                MeshDrawCall drawParams = m_meshesToDraw[i];
                MeshPart partData = m_meshesData[drawParams.MeshPartIndex];
                SimpleMaterial material = m_materials[partData.MaterialIndex];

                SN.Matrix4x4 w = drawParams.World * transform;
                SN.Matrix4x4 wvp = w * cam.ViewProjection;

                cmdList.UpdateBuffer(m_wvpParam, 0, ref wvp);
                cmdList.UpdateBuffer(m_worldParam, 0, ref w);
                cmdList.UpdateBuffer(m_lightPosParam, 0, LightPosition);
                cmdList.UpdateBuffer(m_camPosParam, 0, cam.Position);

                cmdList.SetGraphicsResourceSet(0, m_worldLightCBSet);
                cmdList.SetGraphicsResourceSet(1, material.ColorAndTexture);

                cmdList.DrawIndexed((uint) partData.IndexCount, 1, (uint) partData.IndexOffset, 0, 0);
            }
        }

        private void GatherVertexCounts(Scene scene, out int vertexCount, out int indexCount)
        {
            vertexCount = 0;
            indexCount = 0;

            foreach(Mesh m in scene.Meshes)
            {
                vertexCount += m.VertexCount;
                indexCount += 3 * m.FaceCount;
            }
        }

        private bool CreateVertexBuffer(Scene scene, GraphicsDevice gd, string baseDir)
        {
            int vCount, iCount;
            GatherVertexCounts(scene, out vCount, out iCount);

            if(vCount == 0 || iCount == 0)
                return false;

            //Load mesh data and all the mesh instances to draw. We put all the geometry into a single vertex buffer/index buffer. Each draw call has exactly one material
            //and will draw potentially a subset of the geometry
            VertexPositionNormalTexture[] vertices = new VertexPositionNormalTexture[vCount];
            uint[] indices = new uint[iCount];

            int vIndex = 0;
            int iIndex = 0;
            int vertexOffset = 0;
            int indexOffset = 0;
            foreach(Mesh m in scene.Meshes)
            {
                List<SN.Vector3> verts = m.Vertices;
                List<SN.Vector3> norms = (m.HasNormals) ? m.Normals : null;
                List<SN.Vector3> uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;
                for(int i = 0; i < verts.Count; i++)
                {
                    SN.Vector3 pos = verts[i];
                    SN.Vector3 norm = (norms != null) ? norms[i] : new SN.Vector3(0, 0, 0);
                    SN.Vector3 uv = (uvs != null) ? uvs[i] : new SN.Vector3(0, 0, 0);

                    vertices[vIndex++] = new VertexPositionNormalTexture(pos, norm, new SN.Vector2(uv.X, 1 - uv.Y)); //Invert Y coordinate!
                }

                List<Face> faces = m.Faces;
                for(int i = 0; i < faces.Count; i++)
                {
                    Face f = faces[i];

                    //Ignore non-triangle faces
                    if(f.IndexCount != 3)
                    {
                        indices[iIndex++] = 0;
                        indices[iIndex++] = 0;
                        indices[iIndex++] = 0;
                        continue;
                    }

                    indices[iIndex++] = (uint) (f.Indices[0] + vertexOffset);
                    indices[iIndex++] = (uint) (f.Indices[1] + vertexOffset);
                    indices[iIndex++] = (uint) (f.Indices[2] + vertexOffset);
                }

                int indexCountForMesh = faces.Count * 3;
                m_meshesData.Add(new MeshPart(m.MaterialIndex, indexOffset, indexCountForMesh));

                vertexOffset += verts.Count;
                indexOffset += indexCountForMesh;
            }

            //Gather up all the nodes (and their final world transform) that have meshes
            FindAllMeshInstances(scene.RootNode, SN.Matrix4x4.Identity);

            if(m_meshesData.Count == 0 || m_meshesToDraw.Count == 0)
                return false;

            //Create and load materials - this is rather dumb, as textures might be loaded multiple times if multiple materials use the same file.
            m_materialCBLayout = gd.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("DiffuseColor", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("DiffuseTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("DiffuseSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            foreach(Material m in scene.Materials)
            {
                SN.Vector4 diffuseColor = new SN.Vector4(1, 1, 1, 1);
                if(m.HasColorDiffuse)
                    diffuseColor = m.ColorDiffuse;
              
                string filePath = string.Empty;
                if(m.HasTextureDiffuse)
                    filePath = Path.Combine(baseDir, m.TextureDiffuse.FilePath);

                m_materials.Add(new SimpleMaterial(gd, diffuseColor, filePath, m_materialCBLayout));
            }

            //Start creating all the needed GPU resources

            m_vertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint) vCount * VertexPositionNormalTexture.SizeInBytes, BufferUsage.VertexBuffer));
            gd.UpdateBuffer<VertexPositionNormalTexture>(m_vertexBuffer, 0, vertices);

            m_indexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint) iCount * sizeof(uint), BufferUsage.IndexBuffer));
            gd.UpdateBuffer<uint>(m_indexBuffer, 0, indices);

            m_shaderSet = new ShaderSetDescription(
                new[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
                        new VertexElementDescription("Normal", VertexElementSemantic.Normal, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                },
                new[]
                {
                    Helper.LoadShader(gd.ResourceFactory, "SimpleTextured", ShaderStages.Vertex, "VS"),
                    Helper.LoadShader(gd.ResourceFactory, "SimpleTextured", ShaderStages.Fragment, "FS")
                });

            m_wvpParam = gd.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            m_worldParam = gd.ResourceFactory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            m_lightPosParam = gd.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            m_camPosParam = gd.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            m_worldLightCBLayout = gd.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("WorldViewProjection", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("World", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("LightPosition", ResourceKind.UniformBuffer, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("CameraPosition", ResourceKind.UniformBuffer, ShaderStages.Fragment)
                ));

            m_worldLightCBSet = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                m_worldLightCBLayout, m_wvpParam, m_worldParam, m_lightPosParam, m_camPosParam
                ));

            m_pipelineState = gd.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
                //new RasterizerStateDescription(FaceCullMode.Back, PolygonFillMode.Wireframe, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                m_shaderSet, new[] { m_worldLightCBLayout, m_materialCBLayout },
                gd.MainSwapchain.Framebuffer.OutputDescription
                ));

            return true;
        }

        private void FindAllMeshInstances(Node parent, in SN.Matrix4x4 rootTransform)
        {
            SN.Matrix4x4 trafo;
            Helper.ToNumerics(parent.Transform, out trafo);

            SN.Matrix4x4 world = trafo * rootTransform;

            foreach(int meshIndex in parent.MeshIndices)
                m_meshesToDraw.Add(new MeshDrawCall(meshIndex, world));

            foreach(Node child in parent.Children)
                FindAllMeshInstances(child, world);
        }

        public void Dispose()
        {
            m_vertexBuffer.Dispose();
            m_indexBuffer.Dispose();

            m_worldLightCBLayout.Dispose();
            m_worldLightCBSet.Dispose();
            m_wvpParam.Dispose();
            m_worldParam.Dispose();
            m_lightPosParam.Dispose();
            m_camPosParam.Dispose();
            m_materialCBLayout.Dispose();
            m_pipelineState.Dispose();

            foreach(Shader sh in m_shaderSet.Shaders)
                sh.Dispose();

            foreach(SimpleMaterial mat in m_materials)
                mat.Dispose();
        }

        private void ComputeBoundingBox(Scene scene)
        {
            m_sceneMin = new SN.Vector3(1e10f, 1e10f, 1e10f);
            m_sceneMax = new SN.Vector3(-1e10f, -1e10f, -1e10f);
            SN.Matrix4x4 identity = SN.Matrix4x4.Identity;

            ComputeBoundingBox(scene, scene.RootNode, ref m_sceneMin, ref m_sceneMax, ref identity);

            m_sceneCenter.X = (m_sceneMin.X + m_sceneMax.X) / 2.0f;
            m_sceneCenter.Y = (m_sceneMin.Y + m_sceneMax.Y) / 2.0f;
            m_sceneCenter.Z = (m_sceneMin.Z + m_sceneMax.Z) / 2.0f;
        }

        private void ComputeBoundingBox(Scene scene, Node node, ref SN.Vector3 min, ref SN.Vector3 max, ref SN.Matrix4x4 trafo)
        {
            SN.Matrix4x4 prev = trafo;
            SN.Matrix4x4 curr;
            Helper.ToNumerics(node.Transform, out curr);
            trafo = prev * curr;

            if(node.HasMeshes)
            {
                foreach(int index in node.MeshIndices)
                {
                    Mesh mesh = scene.Meshes[index];
                    for(int i = 0; i < mesh.VertexCount; i++)
                    {
                        SN.Vector3 tmp;
                        Helper.ToNumerics(mesh.Vertices[i], out tmp);
                        tmp = SN.Vector3.Transform(tmp, trafo);

                        min.X = Math.Min(min.X, tmp.X);
                        min.Y = Math.Min(min.Y, tmp.Y);
                        min.Z = Math.Min(min.Z, tmp.Z);

                        max.X = Math.Max(max.X, tmp.X);
                        max.Y = Math.Max(max.Y, tmp.Y);
                        max.Z = Math.Max(max.Z, tmp.Z);
                    }
                }
            }

            for(int i = 0; i < node.ChildCount; i++)
                ComputeBoundingBox(scene, node.Children[i], ref min, ref max, ref trafo);

            trafo = prev;
        }

        private void AdjustModelScale()
        {
            //Make sure the model isn't GIGANTIC and blows out our depth precision because the camera and clip planes adjust to the bounding volume
            float scale = m_sceneMax.X - m_sceneMin.X;
            scale = Math.Max(m_sceneMax.Y - m_sceneMin.Y, scale);
            scale = Math.Max(m_sceneMax.Z - m_sceneMin.Z, scale);
            scale = (1.0f / scale) * 2.0f;

            SN.Matrix4x4 scaleMatrix = SN.Matrix4x4.CreateScale(scale);

            for(int i = 0; i < m_meshesToDraw.Count; i++)
            {
                MeshDrawCall mesh = m_meshesToDraw[i];
                mesh.World = mesh.World * scaleMatrix;
                m_meshesToDraw[i] = mesh;
            }

            m_sceneCenter = SN.Vector3.Transform(m_sceneCenter, scaleMatrix);
            m_sceneMin = SN.Vector3.Transform(m_sceneMin, scaleMatrix);
            m_sceneMax = SN.Vector3.Transform(m_sceneMax, scaleMatrix);
        }
    }

    public struct MeshDrawCall
    {
        public int MeshPartIndex;
        public SN.Matrix4x4 World;

        public MeshDrawCall(int meshIndex, in SN.Matrix4x4 world)
        {
            MeshPartIndex = meshIndex;
            World = world;
        }
    }

    public struct MeshPart
    {
        public int MaterialIndex;

        public int IndexOffset;
        public int IndexCount;

        public MeshPart(int matIndex, int indexOffset, int indexCount)
        {
            MaterialIndex = matIndex;

            IndexOffset = indexOffset;
            IndexCount = indexCount;
        }
    }

    public sealed class SimpleMaterial : IDisposable
    {
        public ResourceSet ColorAndTexture;

        private Texture m_tex;
        private TextureView m_texView;
        private DeviceBuffer m_materialConstantBuffer;

        public SimpleMaterial(GraphicsDevice gd, SN.Vector4 diffuseColor, string texPath, ResourceLayout layout)
        {
            m_materialConstantBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
            gd.UpdateBuffer<SN.Vector4>(m_materialConstantBuffer, 0, ref diffuseColor);

            if(File.Exists(texPath))
            {
                try
                {
                    m_tex = Helper.LoadTextureFromFile(texPath, gd, gd.ResourceFactory);
                }
                catch(Exception) { }
            }


            if(m_tex == null)
            {
                m_tex = gd.ResourceFactory.CreateTexture(new TextureDescription(1, 1, 1, 1, 1, PixelFormat.B8_G8_R8_A8_UNorm, TextureUsage.Sampled, Veldrid.TextureType.Texture2D));
                Color[] color = new Color[1] { Color.White };
                gd.UpdateTexture<Color>(m_tex, color, 0, 0, 0, 1, 1, 1, 0, 0);
            }

            m_texView = gd.ResourceFactory.CreateTextureView(m_tex);

            ColorAndTexture = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(layout, m_materialConstantBuffer, m_texView, gd.Aniso4xSampler));
        }

        public void Dispose()
        {
            if(m_materialConstantBuffer != null)
            {
                m_materialConstantBuffer.Dispose();
                m_materialConstantBuffer = null;
            }

            if(ColorAndTexture != null)
            {
                ColorAndTexture.Dispose();
                ColorAndTexture = null;
            }

            if(m_texView != null)
            {
                m_texView.Dispose();
                m_texView = null;
            }

            if(m_tex != null)
            {
                m_tex.Dispose();
                m_tex = null;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionNormalTexture
    {
        public static readonly uint SizeInBytes = (uint) MemoryHelper.SizeOf<VertexPositionNormalTexture>();

        public float PosX;
        public float PosY;
        public float PosZ;

        public float NormX;
        public float NormY;
        public float NormZ;

        public float TexU;
        public float TexV;

        public VertexPositionNormalTexture(in SN.Vector3 pos, in SN.Vector3 norm, in SN.Vector2 uv)
        {
            PosX = pos.X;
            PosY = pos.Y;
            PosZ = pos.Z;

            NormX = norm.X;
            NormY = norm.Y;
            NormZ = norm.Z;

            TexU = uv.X;
            TexV = uv.Y;
        }
    }
}
