using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Sokol;
using static Sokol.SG;
using static Sokol.CGltf;
using static Sokol.SLog;
using static Sokol.SFetch;
using static Sokol.SBasisu;
using static Sokol.StbImage;
using static Sokol.Utils;
using static cgltf_sapp_shader_cs_cgltf.Shaders;
namespace Sokol
{
    /// <summary>
    /// Scene limits - adjust these based on your needs
    /// </summary>
    public static class CGltfSceneLimits
    {
        public const int INVALID_INDEX = -1;
        public const int MAX_BUFFERS = 128;      // Increased from 32 to support more complex models
        public const int MAX_IMAGES = 64;        // Increased from 32
        public const int MAX_MATERIALS = 64;     // Increased from 32
        public const int MAX_PIPELINES = 64;     // Increased from 32
        public const int MAX_PRIMITIVES = 256;   // Increased from 64
        public const int MAX_MESHES = 128;       // Increased from 32
        public const int MAX_NODES = 256;        // Increased from 64
    }

    /// <summary>
    /// Per-material texture indices for PBR metallic workflow
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfMetallicImages
    {
        public int BaseColor;
        public int MetallicRoughness;
        public int Normal;
        public int Occlusion;
        public int Emissive;
    }

    /// <summary>
    /// Fragment shader parameters for metallic material
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfMetallicMaterial
    {
        public cgltf_sapp_shader_cs_cgltf.Shaders.cgltf_metallic_params_t FsParams;
        public CGltfMetallicImages Images;
    }

    /// <summary>
    /// Material definition (supports metallic workflow)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfMaterial
    {
        public bool IsMetallic;
        public CGltfMetallicMaterial Metallic;
    }

    /// <summary>
    /// Vertex buffer mapping for multi-buffer vertex layouts
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfVertexBufferMapping
    {
        public int Num;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SG_MAX_VERTEXBUFFER_BINDSLOTS)]
        public int[] BufferIndices;

        public CGltfVertexBufferMapping()
        {
            Num = 0;
            BufferIndices = new int[SG_MAX_VERTEXBUFFER_BINDSLOTS];
            for (int i = 0; i < SG_MAX_VERTEXBUFFER_BINDSLOTS; i++)
            {
                BufferIndices[i] = CGltfSceneLimits.INVALID_INDEX;
            }
        }
    }

    /// <summary>
    /// Primitive (submesh) definition
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfPrimitive
    {
        public int PipelineIndex;
        public int MaterialIndex;
        public CGltfVertexBufferMapping VertexBuffers;
        public int IndexBuffer;
        public int BaseElement;
        public int NumElements;
    }

    /// <summary>
    /// Mesh definition (collection of primitives)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfMesh
    {
        public int FirstPrimitive;
        public int NumPrimitives;
    }

    /// <summary>
    /// Scene node with transform and mesh
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfNode
    {
        public int MeshIndex;
        public Matrix4x4 Transform;
    }

    /// <summary>
    /// Image with texture view and sampler
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CGltfImage
    {
        public sg_image Image;
        public sg_view TexView;
        public sg_sampler Sampler;
    }

    /// <summary>
    /// Complete parsed GLTF scene
    /// </summary>
    public class CGltfScene
    {
        public int NumBuffers;
        public int NumImages;
        public int NumPipelines;
        public int NumMaterials;
        public int NumPrimitives;
        public int NumMeshes;
        public int NumNodes;

        public sg_buffer[] Buffers;
        public CGltfImage[] Images;
        public sg_pipeline[] Pipelines;
        public CGltfMaterial[] Materials;
        public CGltfPrimitive[] Primitives;
        public CGltfMesh[] Meshes;
        public CGltfNode[] Nodes;

        public CGltfScene()
        {
            Buffers = new sg_buffer[CGltfSceneLimits.MAX_BUFFERS];
            Images = new CGltfImage[CGltfSceneLimits.MAX_IMAGES];
            Pipelines = new sg_pipeline[CGltfSceneLimits.MAX_PIPELINES];
            Materials = new CGltfMaterial[CGltfSceneLimits.MAX_MATERIALS];
            Primitives = new CGltfPrimitive[CGltfSceneLimits.MAX_PRIMITIVES];
            Meshes = new CGltfMesh[CGltfSceneLimits.MAX_MESHES];
            Nodes = new CGltfNode[CGltfSceneLimits.MAX_NODES];

            // Initialize all to invalid/default state
            for (int i = 0; i < CGltfSceneLimits.MAX_BUFFERS; i++)
                Buffers[i] = default;
            for (int i = 0; i < CGltfSceneLimits.MAX_IMAGES; i++)
                Images[i] = default;
            for (int i = 0; i < CGltfSceneLimits.MAX_PIPELINES; i++)
                Pipelines[i] = default;
            for (int i = 0; i < CGltfSceneLimits.MAX_MATERIALS; i++)
                Materials[i] = default;
            for (int i = 0; i < CGltfSceneLimits.MAX_PRIMITIVES; i++)
                Primitives[i] = default;
            for (int i = 0; i < CGltfSceneLimits.MAX_MESHES; i++)
                Meshes[i] = default;
            for (int i = 0; i < CGltfSceneLimits.MAX_NODES; i++)
                Nodes[i] = default;
        }

        /// <summary>
        /// Cleanup all GPU resources
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < NumBuffers; i++)
                sg_destroy_buffer(Buffers[i]);
            for (int i = 0; i < NumImages; i++)
            {
                sg_destroy_sampler(Images[i].Sampler);
                sg_destroy_view(Images[i].TexView);
                sg_destroy_image(Images[i].Image);
            }
            for (int i = 0; i < NumPipelines; i++)
                sg_destroy_pipeline(Pipelines[i]);
        }
    }

    /// <summary>
    /// Internal buffer creation parameters
    /// </summary>
    internal struct BufferCreationParams
    {
        public sg_buffer_usage Usage;
        public int Offset;
        public int Size;
        public int GltfBufferIndex;
    }

    /// <summary>
    /// Internal image/sampler creation parameters
    /// </summary>
    internal struct ImageSamplerCreationParams
    {
        public sg_filter MinFilter;
        public sg_filter MagFilter;
        public sg_filter MipmapFilter;
        public sg_wrap WrapS;
        public sg_wrap WrapT;
        public int GltfImageIndex;
    }

    /// <summary>
    /// Internal pipeline cache parameters
    /// </summary>
    internal struct PipelineCacheParams
    {
        public sg_vertex_layout_state Layout;
        public sg_primitive_type PrimType;
        public sg_index_type IndexType;
        public bool Alpha;
    }

    /// <summary>
    /// CGLTF Parser - loads and parses GLTF/GLB files
    /// </summary>
    public unsafe class CGltfParser
    {
        private string _basePath = "";
        private string _filePath = "";
        private CGltfScene _scene;
        private CGltfScene? _externalScene;  // Reference to external scene (if provided)
        private sg_shader _metallicShader;
        private sg_shader _specularShader;
        
        // Placeholder textures
        private sg_view _whiteTex;
        private sg_view _blackTex;
        private sg_view _normalTex;
        private sg_sampler _placeholderSampler;

        // Internal creation parameters
        private BufferCreationParams[] _bufferParams;
        private ImageSamplerCreationParams[] _imageParams;
        private PipelineCacheParams[] _pipelineCache;

        // Callbacks for async loading
        private Action<CGltfScene>? _onLoadComplete;
        private Action<string>? _onLoadFailed;
        
        // Track pending async loads
        private int _pendingTextureLoads = 0;
        private int _pendingBufferLoads = 0;

        public CGltfScene? Scene => _externalScene ?? _scene;
        
        private CGltfScene ActiveScene => _externalScene ?? _scene;

        public CGltfParser()
        {
            _scene = new CGltfScene();
            _bufferParams = new BufferCreationParams[CGltfSceneLimits.MAX_BUFFERS];
            _imageParams = new ImageSamplerCreationParams[CGltfSceneLimits.MAX_IMAGES];
            _pipelineCache = new PipelineCacheParams[CGltfSceneLimits.MAX_PIPELINES];
        }

        /// <summary>
        /// Initialize the parser with shaders and placeholder textures
        /// </summary>
        public void Init(sg_shader metallicShader, sg_shader specularShader)
        {
            _metallicShader = metallicShader;
            _specularShader = specularShader;

            CreatePlaceholderTextures();
        }

        /// <summary>
        /// Load a GLTF/GLB file synchronously from memory
        /// </summary>
        public bool LoadFromMemory(byte[] data, string basePath = "")
        {
            _basePath = basePath;

            fixed (byte* dataPtr = data)
            {
                cgltf_options options = default;
                cgltf_data* gltfData = null;

                cgltf_result result = cgltf_parse(in options, dataPtr, (nuint)data.Length, out gltfData);
                
                if (result != cgltf_result.cgltf_result_success)
                {
                    Error($"CGltfParser: Failed to parse GLTF data: {result}");
                    return false;
                }

                try
                {
                    Info($"CGltfParser: Successfully parsed GLTF - {gltfData->meshes_count} meshes, {gltfData->materials_count} materials");
                    
                    // For GLB files, the binary buffer data is embedded in the file we just loaded
                    // We need to manually parse the GLB structure and set buffer->data pointer
                    if (gltfData->file_type == cgltf_file_type.cgltf_file_type_glb && gltfData->buffers_count > 0)
                    {
                        cgltf_buffer* firstBuffer = gltfData->buffers;
                        
                        // Check if this buffer needs to be loaded (no URI = embedded in GLB)
                        if (firstBuffer->uri == IntPtr.Zero && firstBuffer->data == null)
                        {
                            // GLB structure (from cgltf.h):
                            // [Header: 12 bytes] [JSON chunk: 8 bytes header + N bytes data]
                            // [BIN chunk: 8 bytes header + M bytes data]
                            
                            byte* glbData = dataPtr;
                            
                            // Read GLB header (12 bytes)
                            uint* header = (uint*)glbData;
                            uint magic = header[0];           // Should be 0x46546C67 ("glTF")
                            uint version = header[1];         // Should be 2
                            uint totalLength = header[2];     // Total file length
                            
                            Info($"CGltfParser: GLB header - magic=0x{magic:X}, version={version}, length={totalLength}");
                            
                            if (magic != 0x46546C67)
                            {
                                Error($"CGltfParser: Invalid GLB magic number: 0x{magic:X}");
                            }
                            else
                            {
                                // Read JSON chunk header (at offset 12)
                                uint jsonChunkLength = header[3];
                                uint jsonChunkType = header[4];   // Should be 0x4E4F534A ("JSON")
                                
                                Info($"CGltfParser: JSON chunk - type=0x{jsonChunkType:X}, length={jsonChunkLength}");
                                
                                // BIN chunk starts after: header(12) + json_chunk_header(8) + json_data(jsonChunkLength)
                                int binChunkOffset = 12 + 8 + (int)jsonChunkLength;
                                
                                if (binChunkOffset + 8 <= data.Length)
                                {
                                    // Read BIN chunk header
                                    uint* binHeader = (uint*)(glbData + binChunkOffset);
                                    uint binChunkLength = binHeader[0];
                                    uint binChunkType = binHeader[1];   // Should be 0x004E4942 ("BIN\0")
                                    
                                    Info($"CGltfParser: BIN chunk - type=0x{binChunkType:X}, length={binChunkLength}, offset={binChunkOffset}");
                                    
                                    if (binChunkType == 0x004E4942)
                                    {
                                        // BIN data starts 8 bytes after the BIN chunk header
                                        byte* binData = glbData + binChunkOffset + 8;
                                        firstBuffer->data = binData;
                                        
                                        Info($"CGltfParser: Set GLB embedded buffer data pointer (ptr=0x{(long)binData:X}, size={binChunkLength})");
                                    }
                                    else
                                    {
                                        Error($"CGltfParser: Invalid BIN chunk type: 0x{binChunkType:X}");
                                    }
                                }
                                else
                                {
                                    Error($"CGltfParser: GLB file too small for BIN chunk at offset {binChunkOffset} (file size: {data.Length})");
                                }
                            }
                        }
                    }
                    
                    // Parse scene structure (allocates buffers but doesn't fill them yet)
                    ParseScene(gltfData);
                    
                    // Start async loading of external .bin buffer files using FileSystem.cs
                    LoadBuffersAsync(gltfData);
                    
                    cgltf_free(gltfData);
                    return true;
                }
                catch (Exception ex)
                {
                    Error($"CGltfParser: Exception during parsing: {ex.Message}");
                    if (gltfData != null)
                        cgltf_free(gltfData);
                    return false;
                }
            }
        }

        /// <summary>
        /// Load a GLTF/GLB file asynchronously using FileSystem
        /// </summary>
        public void LoadFromFileAsync(string filePath, Action<CGltfScene> onComplete, Action<string>? onFailed = null)
        {
            _externalScene = null;  // Use internal scene
            _filePath = filePath;
            _basePath = Path.GetDirectoryName(filePath) ?? "";
            _onLoadComplete = onComplete;
            _onLoadFailed = onFailed;
            _pendingTextureLoads = 0;

            Info($"CGltfParser: Starting async load of '{filePath}'");
            FileSystem.Instance.LoadFile(filePath, OnFileLoaded);
        }

        /// <summary>
        /// Load a GLTF/GLB file asynchronously using FileSystem, filling the provided external scene directly
        /// This avoids the overhead of copying data from internal scene to external scene
        /// </summary>
        public void LoadFromFileAsync(string filePath, CGltfScene externalScene, Action onComplete, Action<string>? onFailed = null)
        {
            _externalScene = externalScene;  // Use external scene
            _filePath = filePath;
            _basePath = Path.GetDirectoryName(filePath) ?? "";
            _onLoadComplete = _ => onComplete();  // Wrap void callback
            _onLoadFailed = onFailed;
            _pendingTextureLoads = 0;

            Info($"CGltfParser: Starting async load of '{filePath}' (direct to external scene)");
            FileSystem.Instance.LoadFile(filePath, OnFileLoaded);
        }

        private void OnFileLoaded(string filePath, byte[]? buffer, FileLoadStatus status)
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                Info($"CGltfParser: File '{filePath}' loaded successfully, size: {buffer.Length} bytes");
                
                if (LoadFromMemory(buffer, _basePath))
                {
                    // If no textures are pending, complete immediately
                    CheckSceneLoadComplete();
                }
                else
                {
                    _onLoadFailed?.Invoke($"Failed to parse GLTF file: {filePath}");
                }
            }
            else
            {
                Error($"CGltfParser: Failed to load file '{filePath}': {status}");
                _onLoadFailed?.Invoke($"File load failed: {status}");
            }
        }
        
        private void CheckSceneLoadComplete()
        {
            if (_pendingBufferLoads == 0 && _pendingTextureLoads == 0)
            {
                Info($"CGltfParser: Scene fully loaded (buffers AND textures complete)");
                
                // Now that all textures are loaded, update material flags
                UpdateMaterialTextureFlags();
                
                if (Scene != null)
                {
                    _onLoadComplete?.Invoke(Scene);
                }
            }
            else
            {
                Info($"CGltfParser: Waiting for {_pendingBufferLoads} buffers, {_pendingTextureLoads} textures...");
            }
        }

        private void LoadBuffersAsync(cgltf_data* gltf)
        {
            if (gltf->buffers_count == 0)
            {
                Info($"CGltfParser: No external buffer files to load");
                CheckSceneLoadComplete();
                return;
            }
            
            for (nuint i = 0; i < gltf->buffers_count; i++)
            {
                cgltf_buffer* gltfBuf = &gltf->buffers[i];
                if (gltfBuf->uri == null)
                {
                    Info($"CGltfParser: Buffer {i} has no URI (embedded data), skipping");
                    continue;
                }
                
                string uri = Marshal.PtrToStringAnsi((IntPtr)gltfBuf->uri) ?? "";
                if (string.IsNullOrEmpty(uri))
                {
                    Info($"CGltfParser: Buffer {i} has empty URI, skipping");
                    continue;
                }
                
                string bufferPath = Path.Combine(_basePath, uri);
                int bufferIndex = (int)i;
                _pendingBufferLoads++;
                
                Info($"CGltfParser: Loading buffer file {i}: {uri} (expected size: {gltfBuf->size} bytes)");
                FileSystem.Instance.LoadFile(bufferPath, (path, buffer, status) => {
                    OnBufferFileLoaded(bufferIndex, buffer, status);
                }, (int)gltfBuf->size);
            }
            
            if (_pendingBufferLoads == 0)
            {
                Info($"CGltfParser: No buffer files needed async loading");
                CheckSceneLoadComplete();
            }
        }

        private void OnBufferFileLoaded(int gltfBufferIndex, byte[]? buffer, FileLoadStatus status)
        {
            if (status == FileLoadStatus.Success && buffer != null)
            {
                Info($"CGltfParser: Buffer {gltfBufferIndex} loaded successfully: {buffer.Length} bytes");
                
                // Initialize all sokol buffers that reference this GLTF buffer
                fixed (byte* dataPtr = buffer)
                {
                    sg_range range = new sg_range
                    {
                        ptr = dataPtr,
                        size = (nuint)buffer.Length
                    };
                    CreateBuffersForGltfBuffer(gltfBufferIndex, range);
                }
            }
            else
            {
                Error($"CGltfParser: Failed to load buffer {gltfBufferIndex}: {status}");
            }
            
            _pendingBufferLoads--;
            CheckSceneLoadComplete();
        }

        private void UpdateMaterialTextureFlags()
        {
            Info($"CGltfParser: Updating material texture flags...");
            
            for (int i = 0; i < ActiveScene.NumMaterials; i++)
            {
                if (ActiveScene.Materials[i].IsMetallic)
                {
                    int baseColorIdx = ActiveScene.Materials[i].Metallic.Images.BaseColor;
                    int metallicRoughnessIdx = ActiveScene.Materials[i].Metallic.Images.MetallicRoughness;
                    int normalIdx = ActiveScene.Materials[i].Metallic.Images.Normal;
                    int occlusionIdx = ActiveScene.Materials[i].Metallic.Images.Occlusion;
                    int emissiveIdx = ActiveScene.Materials[i].Metallic.Images.Emissive;
                    
                    // Set base_color_tex flag
                    ActiveScene.Materials[i].Metallic.FsParams.has_base_color_tex = 
                        (baseColorIdx != CGltfSceneLimits.INVALID_INDEX && ActiveScene.Images[baseColorIdx].Image.id != 0) ? 1.0f : 0.0f;
                    
                    // Set metallic_roughness_tex flag
                    ActiveScene.Materials[i].Metallic.FsParams.has_metallic_roughness_tex = 
                        (metallicRoughnessIdx != CGltfSceneLimits.INVALID_INDEX && ActiveScene.Images[metallicRoughnessIdx].Image.id != 0) ? 1.0f : 0.0f;
                    
                    // Set normal_tex flag
                    ActiveScene.Materials[i].Metallic.FsParams.has_normal_tex = 
                        (normalIdx != CGltfSceneLimits.INVALID_INDEX && ActiveScene.Images[normalIdx].Image.id != 0) ? 1.0f : 0.0f;
                    
                    // Set occlusion_tex flag
                    ActiveScene.Materials[i].Metallic.FsParams.has_occlusion_tex = 
                        (occlusionIdx != CGltfSceneLimits.INVALID_INDEX && ActiveScene.Images[occlusionIdx].Image.id != 0) ? 1.0f : 0.0f;
                    
                    // Set emissive_tex flag
                    ActiveScene.Materials[i].Metallic.FsParams.has_emissive_tex = 
                        (emissiveIdx != CGltfSceneLimits.INVALID_INDEX && ActiveScene.Images[emissiveIdx].Image.id != 0) ? 1.0f : 0.0f;
                    
                    Info($"CGltfParser: Material {i} - BaseColor index:{baseColorIdx}, flag:{ActiveScene.Materials[i].Metallic.FsParams.has_base_color_tex}");
                    Info($"CGltfParser: Material {i} - MetallicRoughness index:{metallicRoughnessIdx}, flag:{ActiveScene.Materials[i].Metallic.FsParams.has_metallic_roughness_tex}");
                    Info($"CGltfParser: Material {i} - Normal index:{normalIdx}, flag:{ActiveScene.Materials[i].Metallic.FsParams.has_normal_tex}");
                    Info($"CGltfParser: Material {i} - Occlusion index:{occlusionIdx}, flag:{ActiveScene.Materials[i].Metallic.FsParams.has_occlusion_tex}");
                    Info($"CGltfParser: Material {i} - Emissive index:{emissiveIdx}, flag:{ActiveScene.Materials[i].Metallic.FsParams.has_emissive_tex}");
                    
                    if (baseColorIdx != CGltfSceneLimits.INVALID_INDEX)
                    {
                        Info($"CGltfParser:   BaseColor image.id = {ActiveScene.Images[baseColorIdx].Image.id}");
                    }
                    if (metallicRoughnessIdx != CGltfSceneLimits.INVALID_INDEX)
                    {
                        Info($"CGltfParser:   MetallicRoughness image.id = {ActiveScene.Images[metallicRoughnessIdx].Image.id}");
                    }
                    if (normalIdx != CGltfSceneLimits.INVALID_INDEX)
                    {
                        Info($"CGltfParser:   Normal image.id = {ActiveScene.Images[normalIdx].Image.id}");
                    }
                    if (occlusionIdx != CGltfSceneLimits.INVALID_INDEX)
                    {
                        Info($"CGltfParser:   Occlusion image.id = {ActiveScene.Images[occlusionIdx].Image.id}");
                    }
                    if (emissiveIdx != CGltfSceneLimits.INVALID_INDEX)
                    {
                        Info($"CGltfParser:   Emissive image.id = {ActiveScene.Images[emissiveIdx].Image.id}");
                    }
                    
                    // TODO: Uncomment when emissive texture flag is added back to shader:
                    // int emissiveIdx = ActiveScene.Materials[i].Metallic.Images.Emissive;
                    // ActiveScene.Materials[i].Metallic.FsParams.has_occlusion_tex = ...
                    // ActiveScene.Materials[i].Metallic.FsParams.has_emissive_tex = ...
                }
            }
        }

        private void ParseScene(cgltf_data* gltf)
        {
            Info($"CGltfParser: Parsing scene...");
            
            ParseBuffers(gltf);
            ParseImages(gltf);
            ParseMaterials(gltf);
            ParseMeshes(gltf);
            ParseNodes(gltf);
            
            Info($"CGltfParser: Scene parsing complete - {ActiveScene.NumNodes} nodes, {ActiveScene.NumMeshes} meshes, {ActiveScene.NumPrimitives} primitives");
            
            // Note: UpdateMaterialTextureFlags() is now called in CheckSceneLoadComplete() 
            // after all async textures have loaded, not here during synchronous parsing
        }

        #region Buffer Parsing

        private void ParseBuffers(cgltf_data* gltf)
        {
            if (gltf->buffer_views_count > CGltfSceneLimits.MAX_BUFFERS)
            {
                throw new Exception($"Too many buffer views: {gltf->buffer_views_count} > {CGltfSceneLimits.MAX_BUFFERS}");
            }

            ActiveScene.NumBuffers = (int)gltf->buffer_views_count;
            
            for (int i = 0; i < ActiveScene.NumBuffers; i++)
            {
                cgltf_buffer_view* bufView = &gltf->buffer_views[i];
                
                _bufferParams[i].GltfBufferIndex = GetBufferIndex(gltf, bufView->buffer);
                _bufferParams[i].Offset = (int)bufView->offset;
                _bufferParams[i].Size = (int)bufView->size;
                
                if (bufView->type == cgltf_buffer_view_type.cgltf_buffer_view_type_indices)
                    _bufferParams[i].Usage.index_buffer = true;
                else
                    _bufferParams[i].Usage.vertex_buffer = true;

                ActiveScene.Buffers[i] = sg_alloc_buffer();
            }

            // Create buffers from GLTF buffer data
            for (nuint i = 0; i < gltf->buffers_count; i++)
            {
                cgltf_buffer* gltfBuf = &gltf->buffers[i];
                
                if (gltfBuf->data != null)
                {
                    CreateBuffersForGltfBuffer((int)i, new sg_range 
                    { 
                        ptr = gltfBuf->data, 
                        size = (nuint)gltfBuf->size 
                    });
                }
            }
        }

        private void CreateBuffersForGltfBuffer(int gltfBufferIndex, sg_range data)
        {
            for (int i = 0; i < ActiveScene.NumBuffers; i++)
            {
                if (_bufferParams[i].GltfBufferIndex == gltfBufferIndex)
                {
                    Info($"CGltfParser: Creating buffer {i}: offset={_bufferParams[i].Offset}, size={_bufferParams[i].Size}, vertex={_bufferParams[i].Usage.vertex_buffer}, index={_bufferParams[i].Usage.index_buffer}");
                    
                    byte* bufferData = (byte*)data.ptr + _bufferParams[i].Offset;
                    
                    // Log first few values for debugging
                    if (_bufferParams[i].Usage.index_buffer)
                    {
                        ushort* indices = (ushort*)bufferData;
                        Info($"  First 6 indices: {indices[0]}, {indices[1]}, {indices[2]}, {indices[3]}, {indices[4]}, {indices[5]}");
                    }
                    else if (_bufferParams[i].Usage.vertex_buffer)
                    {
                        float* vertices = (float*)bufferData;
                        Info($"  First 9 floats: {vertices[0]:F3}, {vertices[1]:F3}, {vertices[2]:F3}, {vertices[3]:F3}, {vertices[4]:F3}, {vertices[5]:F3}, {vertices[6]:F3}, {vertices[7]:F3}, {vertices[8]:F3}");
                    }
                    
                    sg_init_buffer(ActiveScene.Buffers[i], new sg_buffer_desc
                    {
                        usage = _bufferParams[i].Usage,
                        data = new sg_range
                        {
                            ptr = bufferData,
                            size = (nuint)_bufferParams[i].Size,
                        }
                    });
                    Info($"CGltfParser: Buffer {i} created with id={ActiveScene.Buffers[i].id}, state={sg_query_buffer_state(ActiveScene.Buffers[i])}");
                }
            }
        }

        #endregion

        #region Image Parsing

        private void ParseImages(cgltf_data* gltf)
        {
            if (gltf->textures_count > CGltfSceneLimits.MAX_IMAGES)
            {
                throw new Exception($"Too many textures: {gltf->textures_count} > {CGltfSceneLimits.MAX_IMAGES}");
            }

            ActiveScene.NumImages = (int)gltf->textures_count;

            for (int i = 0; i < ActiveScene.NumImages; i++)
            {
                cgltf_texture* gltfTex = &gltf->textures[i];
                
                _imageParams[i].GltfImageIndex = GetImageIndex(gltf, gltfTex->image);
                _imageParams[i].MinFilter = GltfToSgMinFilter(gltfTex->sampler->min_filter);
                _imageParams[i].MagFilter = GltfToSgMagFilter(gltfTex->sampler->mag_filter);
                _imageParams[i].MipmapFilter = GltfToSgMipmapFilter(gltfTex->sampler->min_filter);
                _imageParams[i].WrapS = GltfToSgWrap(gltfTex->sampler->wrap_s);
                _imageParams[i].WrapT = GltfToSgWrap(gltfTex->sampler->wrap_t);

                ActiveScene.Images[i].Image.id = SG_INVALID_ID;
                ActiveScene.Images[i].Sampler.id = SG_INVALID_ID;
                ActiveScene.Images[i].TexView.id = SG_INVALID_ID;
            }

            // Load images from GLTF image data
            for (nuint i = 0; i < gltf->images_count; i++)
            {
                cgltf_image* gltfImg = &gltf->images[i];
                
                if (gltfImg->buffer_view != null)
                {
                    // Embedded image data
                    Info($"CGltfParser: Image {i} is EMBEDDED in buffer view");
                    cgltf_buffer_view* bufView = gltfImg->buffer_view;
                    
                    // Check if buffer data is valid
                    if (bufView->buffer->data == null)
                    {
                        Error($"CGltfParser: Image {i}: buffer->data is NULL! Buffer might not be loaded yet.");
                        continue;
                    }
                    
                    byte* imageData = (byte*)bufView->buffer->data + bufView->offset;
                    Info($"CGltfParser: Image {i}: Creating from embedded data (ptr=0x{(long)imageData:X}, size={bufView->size})");
                    
                    CreateImageSamplersForGltfImage((int)i, new sg_range 
                    { 
                        ptr = imageData, 
                        size = (nuint)bufView->size 
                    });
                }
                else if (gltfImg->uri != IntPtr.Zero)
                {
                    // External image file - load asynchronously
                    string uri = Marshal.PtrToStringUTF8((IntPtr)gltfImg->uri) ?? "";
                    string fullPath = Path.Combine(_basePath, uri);
                    
                    Info($"CGltfParser: Queueing external texture load: {uri}");
                    _pendingTextureLoads++;
                    LoadExternalTexture((int)i, fullPath);
                }
            }
        }
        
        private void LoadExternalTexture(int gltfImageIndex, string filePath)
        {
            // filePath already contains the full path (basePath + uri), don't call util_get_file_path
            Info($"CGltfParser: Loading external texture {gltfImageIndex}: {filePath}");
            
            FileSystem.Instance.LoadFile(filePath, (path, buffer, status) =>
            {
                if (status == FileLoadStatus.Success && buffer != null)
                {
                    Info($"CGltfParser: Texture file loaded ({buffer.Length} bytes), creating image...");
                    
                    // Use the file data directly - CreateImageSamplersForGltfImage will handle decoding
                    fixed (byte* bufferPtr = buffer)
                    {
                        sg_range fileData = new sg_range { ptr = bufferPtr, size = (nuint)buffer.Length };
                        CreateImageSamplersForGltfImage(gltfImageIndex, fileData);
                        
                        Info($"CGltfParser: Created image for texture {gltfImageIndex}");
                        OnTextureLoadComplete();
                    }
                }
                else
                {
                    Error($"CGltfParser: Failed to load texture file '{path}': {status}");
                    OnTextureLoadComplete();
                }
            });
        }
        
        private void OnTextureLoadComplete()
        {
            _pendingTextureLoads--;
            Info($"CGltfParser: Texture loaded, {_pendingTextureLoads} remaining");
            
            if (_pendingTextureLoads == 0)
            {
                CheckSceneLoadComplete();
            }
        }

        private void CreateImageSamplersForGltfImage(int gltfImageIndex, sg_range data)
        {
            // Safety check
            if (data.ptr == null || data.size == 0)
            {
                Error($"CGltfParser: CreateImageSamplersForGltfImage called with invalid data for image {gltfImageIndex} (ptr=0x{(long)data.ptr:X}, size={data.size})");
                return;
            }
            
            Info($"CGltfParser: CreateImageSamplersForGltfImage - gltfImageIndex={gltfImageIndex}, data.size={data.size}");
            
            // Log first few bytes for debugging
            byte* ptr = (byte*)data.ptr;
            if (data.size >= 8)
            {
                Info($"CGltfParser: First 8 bytes: {ptr[0]:X2} {ptr[1]:X2} {ptr[2]:X2} {ptr[3]:X2} {ptr[4]:X2} {ptr[5]:X2} {ptr[6]:X2} {ptr[7]:X2}");
            }
            
            for (int i = 0; i < ActiveScene.NumImages; i++)
            {
                if (_imageParams[i].GltfImageIndex == gltfImageIndex)
                {
                    Info($"CGltfParser: Processing image {i} (matches gltfImageIndex {gltfImageIndex})");
                    sg_image image;
                    
                    // Determine if this texture needs linear color space (not sRGB)
                    // In glTF 2.0: metallic-roughness, normal, and occlusion maps are LINEAR
                    bool needsLinearColorSpace = IsLinearColorSpaceTexture(i);
                    
                    // Detect image format and use appropriate decoder
                    if (IsBasisuFormat(data))
                    {
                        Info($"CGltfParser: Image {i} is Basis Universal format, using sbasisu_make_image");
                        image = sbasisu_make_image(data);
                    }
                    else
                    {
                        Info($"CGltfParser: Image {i} is standard format (PNG/JPG), using stbi_load_csharp, linear={needsLinearColorSpace}");
                        image = LoadImageWithStbi(data, needsLinearColorSpace);
                    }
                    
                    Info($"CGltfParser: Image {i} created with id={image.id}");
                    
                    ActiveScene.Images[i].Image = image;
                    ActiveScene.Images[i].TexView = sg_make_view(new sg_view_desc
                    {
                        texture = new sg_texture_view_desc { image = image },
                        label = $"cgltf-image-{i}-view"
                    });
                    
                    Info($"CGltfParser: TexView {i} created with id={ActiveScene.Images[i].TexView.id}, from image.id={image.id}");
                    
                    ActiveScene.Images[i].Sampler = sg_make_sampler(new sg_sampler_desc
                    {
                        min_filter = _imageParams[i].MinFilter,
                        mag_filter = _imageParams[i].MagFilter,
                        mipmap_filter = _imageParams[i].MipmapFilter,
                        wrap_u = _imageParams[i].WrapS,
                        wrap_v = _imageParams[i].WrapT,
                    });
                }
            }
        }
        
        private bool IsLinearColorSpaceTexture(int textureIndex)
        {
            // Check all materials to see if this texture is used as:
            // - metallic-roughness (LINEAR)
            // - normal map (LINEAR)  
            // - occlusion map (LINEAR)
            // If it's ONLY used for base color or emissive, it should be sRGB
            
            for (int i = 0; i < ActiveScene.NumMaterials; i++)
            {
                if (ActiveScene.Materials[i].IsMetallic)
                {
                    var images = ActiveScene.Materials[i].Metallic.Images;
                    
                    // These textures MUST be linear (not sRGB)
                    if (images.MetallicRoughness == textureIndex) return true;
                    if (images.Normal == textureIndex) return true;
                    if (images.Occlusion == textureIndex) return true;
                }
            }
            
            return false; // Base color and emissive use sRGB
        }
        
        private bool IsBasisuFormat(sg_range data)
        {
            // Safety check
            if (data.ptr == null || data.size == 0)
            {
                Error($"CGltfParser: Invalid data range for image format detection");
                return false;
            }
            
            // Basis Universal files start with specific magic bytes
            // Check for ".basis" or ".ktx2" file signatures
            byte* ptr = (byte*)data.ptr;
            if (data.size < 4) return false;
            
            // Check for KTX 2.0 signature (used by Basis Universal)
            // KTX 2.0: 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB...
            if (ptr[0] == 0xAB && ptr[1] == 0x4B && ptr[2] == 0x54 && ptr[3] == 0x58)
            {
                return true;
            }
            
            // Check for .basis signature
            // Basis files typically start with specific header
            if (data.size >= 12)
            {
                // Simple heuristic: if it's not PNG/JPG/etc, assume it might be basis
                // PNG signature: 0x89, 0x50, 0x4E, 0x47
                if (ptr[0] == 0x89 && ptr[1] == 0x50 && ptr[2] == 0x4E && ptr[3] == 0x47)
                    return false;
                
                // JPEG signature: 0xFF, 0xD8, 0xFF
                if (ptr[0] == 0xFF && ptr[1] == 0xD8 && ptr[2] == 0xFF)
                    return false;
                
                // If we have KTX or unknown format that's not PNG/JPEG, assume basis
                return true;
            }
            
            return false;
        }
        
        private sg_image LoadImageWithStbi(sg_range data, bool linearColorSpace = false)
        {
            // Safety check
            if (data.ptr == null || data.size == 0)
            {
                Error($"CGltfParser: Invalid data range for stbi image loading");
                // Return a placeholder white texture
                return sg_make_image(new sg_image_desc
                {
                    width = 1,
                    height = 1,
                    pixel_format = linearColorSpace ? sg_pixel_format.SG_PIXELFORMAT_RGBA8 : sg_pixel_format.SG_PIXELFORMAT_SRGB8A8,
                });
            }
            
            int width = 0, height = 0, channels = 0;
            int desired_channels = 4; // RGBA
            
            byte* dataPtr = (byte*)data.ptr;
            byte* pixels = stbi_load_csharp(
                in dataPtr[0],
                (int)data.size,
                ref width,
                ref height,
                ref channels,
                desired_channels
            );
            
            if (pixels == null)
            {
                Error($"CGltfParser: Failed to decode image with stbi_load_csharp");
                // Return a placeholder white texture
                return sg_make_image(new sg_image_desc
                {
                    width = 1,
                    height = 1,
                    pixel_format = linearColorSpace ? sg_pixel_format.SG_PIXELFORMAT_RGBA8 : sg_pixel_format.SG_PIXELFORMAT_SRGB8A8,
                });
            }
            
            // CRITICAL: Use linear pixel format (RGBA8) for metallic-roughness, normal, and occlusion maps
            // Use sRGB pixel format (SRGB8A8) for base color and emissive maps
            sg_image_desc desc = new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = linearColorSpace ? sg_pixel_format.SG_PIXELFORMAT_RGBA8 : sg_pixel_format.SG_PIXELFORMAT_SRGB8A8,
            };
            
            desc.data.mip_levels[0] = new sg_range
            {
                ptr = pixels,
                size = (nuint)(width * height * desired_channels)
            };
            
            sg_image image = sg_make_image(desc);
            
            // Free the STB image data
            stbi_image_free_csharp(pixels);
            
            return image;
        }

        #endregion

        #region Material Parsing

        private void ParseMaterials(cgltf_data* gltf)
        {
            if (gltf->materials_count > CGltfSceneLimits.MAX_MATERIALS)
            {
                throw new Exception($"Too many materials: {gltf->materials_count} > {CGltfSceneLimits.MAX_MATERIALS}");
            }

            ActiveScene.NumMaterials = (int)gltf->materials_count;

            for (int i = 0; i < ActiveScene.NumMaterials; i++)
            {
                cgltf_material* gltfMat = &gltf->materials[i];
                
                ActiveScene.Materials[i].IsMetallic = gltfMat->has_pbr_metallic_roughness != 0;

                if (ActiveScene.Materials[i].IsMetallic)
                {
                    cgltf_pbr_metallic_roughness* src = &gltfMat->pbr_metallic_roughness;
                    
                    ActiveScene.Materials[i].Metallic.FsParams.base_color_factor = new Vector4(
                        src->base_color_factor[0],
                        src->base_color_factor[1],
                        src->base_color_factor[2],
                        src->base_color_factor[3]
                    );
                    
                    ActiveScene.Materials[i].Metallic.FsParams.emissive_factor = new Vector3(
                        gltfMat->emissive_factor[0],
                        gltfMat->emissive_factor[1],
                        gltfMat->emissive_factor[2]
                    );
                    
                    ActiveScene.Materials[i].Metallic.FsParams.metallic_factor = src->metallic_factor;
                    ActiveScene.Materials[i].Metallic.FsParams.roughness_factor = src->roughness_factor;

                    ActiveScene.Materials[i].Metallic.Images.BaseColor = GetTextureIndex(gltf, src->base_color_texture.texture);
                    ActiveScene.Materials[i].Metallic.Images.MetallicRoughness = GetTextureIndex(gltf, src->metallic_roughness_texture.texture);
                    ActiveScene.Materials[i].Metallic.Images.Normal = GetTextureIndex(gltf, gltfMat->normal_texture.texture);
                    ActiveScene.Materials[i].Metallic.Images.Occlusion = GetTextureIndex(gltf, gltfMat->occlusion_texture.texture);
                    ActiveScene.Materials[i].Metallic.Images.Emissive = GetTextureIndex(gltf, gltfMat->emissive_texture.texture);
                }
            }
        }

        #endregion

        #region Mesh Parsing

        private void ParseMeshes(cgltf_data* gltf)
        {
            if (gltf->meshes_count > CGltfSceneLimits.MAX_MESHES)
            {
                throw new Exception($"Too many meshes: {gltf->meshes_count} > {CGltfSceneLimits.MAX_MESHES}");
            }

            ActiveScene.NumMeshes = (int)gltf->meshes_count;

            for (int meshIdx = 0; meshIdx < ActiveScene.NumMeshes; meshIdx++)
            {
                cgltf_mesh* gltfMesh = &gltf->meshes[meshIdx];

                if (ActiveScene.NumPrimitives + (int)gltfMesh->primitives_count > CGltfSceneLimits.MAX_PRIMITIVES)
                {
                    throw new Exception($"Too many primitives");
                }

                ActiveScene.Meshes[meshIdx].FirstPrimitive = ActiveScene.NumPrimitives;
                ActiveScene.Meshes[meshIdx].NumPrimitives = (int)gltfMesh->primitives_count;

                for (nuint primIdx = 0; primIdx < gltfMesh->primitives_count; primIdx++)
                {
                    cgltf_primitive* gltfPrim = &gltfMesh->primitives[primIdx];
                    int scenePrivIdx = ActiveScene.NumPrimitives++;

                    ActiveScene.Primitives[scenePrivIdx].MaterialIndex = GetMaterialIndex(gltf, gltfPrim->material);
                    ActiveScene.Primitives[scenePrivIdx].VertexBuffers = CreateVertexBufferMapping(gltf, gltfPrim);
                    ActiveScene.Primitives[scenePrivIdx].PipelineIndex = CreatePipelineForPrimitive(gltf, gltfPrim, ref ActiveScene.Primitives[scenePrivIdx].VertexBuffers);

                    if (gltfPrim->indices != null)
                    {
                        ActiveScene.Primitives[scenePrivIdx].IndexBuffer = GetBufferViewIndex(gltf, gltfPrim->indices->buffer_view);
                        ActiveScene.Primitives[scenePrivIdx].BaseElement = (int)gltfPrim->indices->offset;
                        ActiveScene.Primitives[scenePrivIdx].NumElements = (int)gltfPrim->indices->count;
                    }
                    else
                    {
                        ActiveScene.Primitives[scenePrivIdx].IndexBuffer = CGltfSceneLimits.INVALID_INDEX;
                        ActiveScene.Primitives[scenePrivIdx].BaseElement = 0;
                        ActiveScene.Primitives[scenePrivIdx].NumElements = (int)gltfPrim->attributes[0].data->count;
                    }
                }
            }
        }

        private CGltfVertexBufferMapping CreateVertexBufferMapping(cgltf_data* gltf, cgltf_primitive* prim)
        {
            CGltfVertexBufferMapping map = new CGltfVertexBufferMapping();

            for (nuint attrIdx = 0; attrIdx < prim->attributes_count; attrIdx++)
            {
                cgltf_attribute* attr = &prim->attributes[attrIdx];
                cgltf_accessor* acc = attr->data;
                int bufferViewIndex = GetBufferViewIndex(gltf, acc->buffer_view);

                int i = 0;
                for (; i < map.Num; i++)
                {
                    if (map.BufferIndices[i] == bufferViewIndex)
                        break;
                }

                if (i == map.Num && map.Num < SG_MAX_VERTEXBUFFER_BINDSLOTS)
                {
                    map.BufferIndices[map.Num++] = bufferViewIndex;
                }
            }

            return map;
        }

        private int CreatePipelineForPrimitive(cgltf_data* gltf, cgltf_primitive* prim, ref CGltfVertexBufferMapping vbufMap)
        {
            PipelineCacheParams pipParams = new PipelineCacheParams
            {
                Layout = CreateLayoutForPrimitive(gltf, prim, ref vbufMap),
                PrimType = GltfToPrimType(prim->type),
                IndexType = GltfToIndexType(prim),
                Alpha = prim->material->alpha_mode != cgltf_alpha_mode.cgltf_alpha_mode_opaque
            };

            // Check if pipeline already exists in cache
            for (int i = 0; i < ActiveScene.NumPipelines; i++)
            {
                if (PipelinesEqual(ref _pipelineCache[i], ref pipParams))
                    return i;
            }

            // Create new pipeline
            if (ActiveScene.NumPipelines >= CGltfSceneLimits.MAX_PIPELINES)
            {
                throw new Exception($"Too many pipelines");
            }

            int pipIdx = ActiveScene.NumPipelines++;
            _pipelineCache[pipIdx] = pipParams;

            bool isMetallic = prim->material->has_pbr_metallic_roughness != 0;

            sg_pipeline_desc desc = new sg_pipeline_desc
            {
                layout = pipParams.Layout,
                shader = isMetallic ? _metallicShader : _specularShader,
                primitive_type = pipParams.PrimType,
                index_type = pipParams.IndexType,
                cull_mode = sg_cull_mode.SG_CULLMODE_BACK,
                face_winding = sg_face_winding.SG_FACEWINDING_CCW,
                depth = new sg_depth_state
                {
                    write_enabled = !pipParams.Alpha,
                    compare = sg_compare_func.SG_COMPAREFUNC_LESS_EQUAL,
                }
            };

            desc.colors[0].write_mask = pipParams.Alpha ? sg_color_mask.SG_COLORMASK_RGB : 0;
            desc.colors[0].blend.enabled = pipParams.Alpha;
            desc.colors[0].blend.src_factor_rgb = pipParams.Alpha ? sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA : 0;
            desc.colors[0].blend.dst_factor_rgb = pipParams.Alpha ? sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA : 0;

            ActiveScene.Pipelines[pipIdx] = sg_make_pipeline(desc);
            Info($"CGltfParser: Pipeline created idx={pipIdx}, id={ActiveScene.Pipelines[pipIdx].id}, state={sg_query_pipeline_state(ActiveScene.Pipelines[pipIdx])}, primType={pipParams.PrimType}, indexType={pipParams.IndexType}");

            return pipIdx;
        }

        private sg_vertex_layout_state CreateLayoutForPrimitive(cgltf_data* gltf, cgltf_primitive* prim, ref CGltfVertexBufferMapping vbufMap)
        {
            sg_vertex_layout_state layout = default;

            for (nuint attrIdx = 0; attrIdx < prim->attributes_count; attrIdx++)
            {
                cgltf_attribute* attr = &prim->attributes[attrIdx];
                int attrSlot = GltfAttrTypeToVsInputSlot(attr->type);

                if (attrSlot != CGltfSceneLimits.INVALID_INDEX)
                {
                    layout.attrs[attrSlot].format = GltfToVertexFormat(attr->data);
                    // DO NOT set offset here - it should be 0 for separate buffers (tightly packed)
                    // The accessor->offset is the offset in the file buffer, not the vertex struct offset!
                    
                    // Set buffer index for this attribute
                    int bufferViewIndex = GetBufferViewIndex(gltf, attr->data->buffer_view);
                    for (int vbSlot = 0; vbSlot < vbufMap.Num; vbSlot++)
                    {
                        if (vbufMap.BufferIndices[vbSlot] == bufferViewIndex)
                        {
                            layout.attrs[attrSlot].buffer_index = vbSlot;
                            break;
                        }
                    }
                }
            }

            return layout;
        }

        #endregion

        #region Node Parsing

        private void ParseNodes(cgltf_data* gltf)
        {
            if (gltf->nodes_count > CGltfSceneLimits.MAX_NODES)
            {
                throw new Exception($"Too many nodes: {gltf->nodes_count} > {CGltfSceneLimits.MAX_NODES}");
            }

            for (nuint nodeIdx = 0; nodeIdx < gltf->nodes_count; nodeIdx++)
            {
                cgltf_node* gltfNode = &gltf->nodes[nodeIdx];

                // Only process nodes with meshes
                if (gltfNode->mesh != null)
                {
                    int sceneNodeIdx = ActiveScene.NumNodes++;
                    ActiveScene.Nodes[sceneNodeIdx].MeshIndex = GetMeshIndex(gltf, gltfNode->mesh);
                    ActiveScene.Nodes[sceneNodeIdx].Transform = BuildTransformForNode(gltf, gltfNode);
                }
            }
        }

        private Matrix4x4 BuildTransformForNode(cgltf_data* gltf, cgltf_node* node)
        {
            Matrix4x4 localTransform;

            if (node->has_matrix != 0)
            {
                localTransform = FromGltfMatrix(node->matrix);
            }
            else
            {
                Matrix4x4 translate = Matrix4x4.Identity;
                Matrix4x4 rotate = Matrix4x4.Identity;
                Matrix4x4 scale = Matrix4x4.Identity;

                if (node->has_translation != 0)
                {
                    translate = Matrix4x4.CreateTranslation(new Vector3(
                        node->translation[0],
                        node->translation[1],
                        node->translation[2]
                    ));
                }

                if (node->has_rotation != 0)
                {
                    rotate = Matrix4x4.CreateFromQuaternion(new Quaternion(
                        node->rotation[0],
                        node->rotation[1],
                        node->rotation[2],
                        node->rotation[3]
                    ));
                }

                if (node->has_scale != 0)
                {
                    scale = Matrix4x4.CreateScale(new Vector3(
                        node->scale[0],
                        node->scale[1],
                        node->scale[2]
                    ));
                }

                // Correct order for row-major matrices: scale * rotate * translate
                localTransform = scale * rotate * translate;
            }

            // Apply parent transform: local * parent (row-major order)
            if (node->parent != null)
            {
                Matrix4x4 parentTransform = BuildTransformForNode(gltf, node->parent);
                return localTransform * parentTransform;
            }

            return localTransform;
        }

        #endregion

        #region Helper Methods

        private void CreatePlaceholderTextures()
        {
            uint[] pixels = new uint[64];

            // White texture
            for (int i = 0; i < 64; i++)
                pixels[i] = 0xFFFFFFFF;
            
            sg_image_desc imgDesc = new sg_image_desc
            {
                width = 8,
                height = 8,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            };
            
            fixed (uint* pixelsPtr = pixels)
            {
                imgDesc.data.mip_levels[0] = new sg_range { ptr = pixelsPtr, size = sizeof(uint) * 64 };
                _whiteTex = sg_make_view(new sg_view_desc
                {
                    texture = { image = sg_make_image(imgDesc) }
                });
            }

            // Black texture
            for (int i = 0; i < 64; i++)
                pixels[i] = 0xFF000000;
            
            fixed (uint* pixelsPtr = pixels)
            {
                imgDesc.data.mip_levels[0] = new sg_range { ptr = pixelsPtr, size = sizeof(uint) * 64 };
                _blackTex = sg_make_view(new sg_view_desc
                {
                    texture = { image = sg_make_image(imgDesc) }
                });
            }

            // Normal map texture (flat normal pointing up: RGB = 128,128,255)
            for (int i = 0; i < 64; i++)
                pixels[i] = 0xFF8080FF;
            
            fixed (uint* pixelsPtr = pixels)
            {
                imgDesc.data.mip_levels[0] = new sg_range { ptr = pixelsPtr, size = sizeof(uint) * 64 };
                _normalTex = sg_make_view(new sg_view_desc
                {
                    texture = { image = sg_make_image(imgDesc) }
                });
            }

            _placeholderSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_NEAREST,
                mag_filter = sg_filter.SG_FILTER_NEAREST
            });
        }

        public sg_view GetPlaceholderTexture(string type)
        {
            return type switch
            {
                "white" => _whiteTex,
                "black" => _blackTex,
                "normal" => _normalTex,
                _ => _whiteTex
            };
        }

        public sg_sampler GetPlaceholderSampler() => _placeholderSampler;

        // Index calculation helpers
        private int GetBufferIndex(cgltf_data* gltf, cgltf_buffer* buf) 
            => (int)(buf - gltf->buffers);

        private int GetBufferViewIndex(cgltf_data* gltf, cgltf_buffer_view* bufView) 
            => (int)(bufView - gltf->buffer_views);

        private int GetImageIndex(cgltf_data* gltf, cgltf_image* img) 
            => (int)(img - gltf->images);

        private int GetTextureIndex(cgltf_data* gltf, cgltf_texture* tex)
        {
            if (tex == null) return CGltfSceneLimits.INVALID_INDEX;
            return (int)(tex - gltf->textures);
        }

        private int GetMaterialIndex(cgltf_data* gltf, cgltf_material* mat) 
            => (int)(mat - gltf->materials);

        private int GetMeshIndex(cgltf_data* gltf, cgltf_mesh* mesh) 
            => (int)(mesh - gltf->meshes);

        // Conversion helpers
        private static Matrix4x4 FromGltfMatrix(cgltf_node.matrixCollection mat)
        {
            return new Matrix4x4(
                mat[0], mat[1], mat[2], mat[3],
                mat[4], mat[5], mat[6], mat[7],
                mat[8], mat[9], mat[10], mat[11],
                mat[12], mat[13], mat[14], mat[15]
            );
        }

        private static sg_filter GltfToSgMinFilter(cgltf_filter_type filter)
        {
            return filter switch
            {
                cgltf_filter_type.cgltf_filter_type_nearest => sg_filter.SG_FILTER_NEAREST,
                cgltf_filter_type.cgltf_filter_type_linear => sg_filter.SG_FILTER_LINEAR,
                _ => sg_filter.SG_FILTER_LINEAR
            };
        }

        private static sg_filter GltfToSgMagFilter(cgltf_filter_type filter)
        {
            return filter switch
            {
                cgltf_filter_type.cgltf_filter_type_nearest => sg_filter.SG_FILTER_NEAREST,
                cgltf_filter_type.cgltf_filter_type_linear => sg_filter.SG_FILTER_LINEAR,
                _ => sg_filter.SG_FILTER_LINEAR
            };
        }

        private static sg_filter GltfToSgMipmapFilter(cgltf_filter_type filter)
        {
            return filter switch
            {
                cgltf_filter_type.cgltf_filter_type_nearest or
                cgltf_filter_type.cgltf_filter_type_linear or
                cgltf_filter_type.cgltf_filter_type_nearest_mipmap_nearest or
                cgltf_filter_type.cgltf_filter_type_linear_mipmap_nearest => sg_filter.SG_FILTER_NEAREST,
                cgltf_filter_type.cgltf_filter_type_nearest_mipmap_linear or
                cgltf_filter_type.cgltf_filter_type_linear_mipmap_linear => sg_filter.SG_FILTER_LINEAR,
                _ => sg_filter.SG_FILTER_LINEAR
            };
        }

        private static sg_wrap GltfToSgWrap(cgltf_wrap_mode wrap)
        {
            return wrap switch
            {
                cgltf_wrap_mode.cgltf_wrap_mode_clamp_to_edge => sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                cgltf_wrap_mode.cgltf_wrap_mode_mirrored_repeat => sg_wrap.SG_WRAP_MIRRORED_REPEAT,
                cgltf_wrap_mode.cgltf_wrap_mode_repeat => sg_wrap.SG_WRAP_REPEAT,
                _ => sg_wrap.SG_WRAP_REPEAT
            };
        }

        private static int GltfAttrTypeToVsInputSlot(cgltf_attribute_type attrType)
        {
            // NOTE: These constants should match your shader attribute slots
            return attrType switch
            {
                cgltf_attribute_type.cgltf_attribute_type_position => 0,
                cgltf_attribute_type.cgltf_attribute_type_normal => 1,
                cgltf_attribute_type.cgltf_attribute_type_texcoord => 2,
                _ => CGltfSceneLimits.INVALID_INDEX
            };
        }

        private static sg_primitive_type GltfToPrimType(cgltf_primitive_type primType)
        {
            return primType switch
            {
                cgltf_primitive_type.cgltf_primitive_type_points => sg_primitive_type.SG_PRIMITIVETYPE_POINTS,
                cgltf_primitive_type.cgltf_primitive_type_lines => sg_primitive_type.SG_PRIMITIVETYPE_LINES,
                cgltf_primitive_type.cgltf_primitive_type_line_strip => sg_primitive_type.SG_PRIMITIVETYPE_LINE_STRIP,
                cgltf_primitive_type.cgltf_primitive_type_triangles => sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES,
                cgltf_primitive_type.cgltf_primitive_type_triangle_strip => sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLE_STRIP,
                _ => sg_primitive_type._SG_PRIMITIVETYPE_DEFAULT
            };
        }

        private static sg_index_type GltfToIndexType(cgltf_primitive* prim)
        {
            if (prim->indices != null)
            {
                if (prim->indices->component_type == cgltf_component_type.cgltf_component_type_r_16u)
                    return sg_index_type.SG_INDEXTYPE_UINT16;
                else
                    return sg_index_type.SG_INDEXTYPE_UINT32;
            }
            return sg_index_type.SG_INDEXTYPE_NONE;
        }

        private static sg_vertex_format GltfToVertexFormat(cgltf_accessor* acc)
        {
            switch (acc->component_type)
            {
                case cgltf_component_type.cgltf_component_type_r_8:
                    if (acc->type == cgltf_type.cgltf_type_vec4)
                        return acc->normalized != 0 ? sg_vertex_format.SG_VERTEXFORMAT_BYTE4N : sg_vertex_format.SG_VERTEXFORMAT_BYTE4;
                    break;

                case cgltf_component_type.cgltf_component_type_r_8u:
                    if (acc->type == cgltf_type.cgltf_type_vec4)
                        return acc->normalized != 0 ? sg_vertex_format.SG_VERTEXFORMAT_UBYTE4N : sg_vertex_format.SG_VERTEXFORMAT_UBYTE4;
                    break;

                case cgltf_component_type.cgltf_component_type_r_16:
                    switch (acc->type)
                    {
                        case cgltf_type.cgltf_type_vec2:
                            return acc->normalized != 0 ? sg_vertex_format.SG_VERTEXFORMAT_SHORT2N : sg_vertex_format.SG_VERTEXFORMAT_SHORT2;
                        case cgltf_type.cgltf_type_vec4:
                            return acc->normalized != 0 ? sg_vertex_format.SG_VERTEXFORMAT_SHORT4N : sg_vertex_format.SG_VERTEXFORMAT_SHORT4;
                    }
                    break;

                case cgltf_component_type.cgltf_component_type_r_32f:
                    switch (acc->type)
                    {
                        case cgltf_type.cgltf_type_scalar: return sg_vertex_format.SG_VERTEXFORMAT_FLOAT;
                        case cgltf_type.cgltf_type_vec2: return sg_vertex_format.SG_VERTEXFORMAT_FLOAT2;
                        case cgltf_type.cgltf_type_vec3: return sg_vertex_format.SG_VERTEXFORMAT_FLOAT3;
                        case cgltf_type.cgltf_type_vec4: return sg_vertex_format.SG_VERTEXFORMAT_FLOAT4;
                    }
                    break;
            }

            return sg_vertex_format.SG_VERTEXFORMAT_INVALID;
        }

        private static bool PipelinesEqual(ref PipelineCacheParams p0, ref PipelineCacheParams p1)
        {
            if (p0.PrimType != p1.PrimType) return false;
            if (p0.Alpha != p1.Alpha) return false;
            if (p0.IndexType != p1.IndexType) return false;

            for (int i = 0; i < SG_MAX_VERTEX_ATTRIBUTES; i++)
            {
                if (p0.Layout.attrs[i].buffer_index != p1.Layout.attrs[i].buffer_index ||
                    p0.Layout.attrs[i].offset != p1.Layout.attrs[i].offset ||
                    p0.Layout.attrs[i].format != p1.Layout.attrs[i].format)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
