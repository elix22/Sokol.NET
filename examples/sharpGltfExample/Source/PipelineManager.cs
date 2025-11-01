using System;
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
using static Sokol.SG.sg_pixel_format;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;
using static Sokol.SG.sg_load_action;
using static Sokol.Utils;
using System.Diagnostics;
using static Sokol.SLog;
using static Sokol.SDebugUI;
using static Sokol.SImgui;
using Imgui;
using static cgltf_sapp_shader_cs_cgltf.Shaders;
using static cgltf_sapp_shader_skinning_cs_skinning.Shaders;

public enum PipelineType
{
    Standard,
    Skinned,
    StandardBlend,        // For alpha blending
    SkinnedBlend,         // For alpha blending with skinning
    StandardMask,         // For alpha masking
    SkinnedMask,          // For alpha masking with skinning
    
    // Transmission (glass materials) pipelines - render opaque objects to screen texture
    TransmissionOpaque,        // Opaque pass for standard meshes
    TransmissionOpaqueSkinned, // Opaque pass for skinned meshes
}

public static class PipeLineManager
{
    private static Dictionary<PipelineType, sg_pipeline> _pipelines = new Dictionary<PipelineType, sg_pipeline>();
    
    // Cache for custom render pass pipelines (key includes format/sample_count)
    private static Dictionary<(PipelineType, sg_pixel_format, sg_pixel_format, int), sg_pipeline> _customPassPipelines = 
        new Dictionary<(PipelineType, sg_pixel_format, sg_pixel_format, int), sg_pipeline>();

    /// <summary>
    /// Clear all pipeline caches. Call this when destroying all resources (e.g., on window resize).
    /// This ensures pipelines will be recreated on next use.
    /// </summary>
    public static void ClearCaches()
    {
        _pipelines.Clear();
        _customPassPipelines.Clear();
    }

    public static sg_pipeline GetOrCreatePipeline(PipelineType type)
    {
        if (_pipelines.ContainsKey(type))
        {
            return _pipelines[type];
        }

        sg_pipeline pipeline;
        var pipeline_desc = default(sg_pipeline_desc);
        switch (type)
        {
            case PipelineType.Standard:
                sg_shader shader_static = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
                var swapchain = sglue_swapchain();
                // Create pipeline for static meshes
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = swapchain.sample_count;
                pipeline_desc.colors[0].pixel_format = swapchain.color_format;
                pipeline_desc.depth.pixel_format = swapchain.depth_format;
                pipeline_desc.label = "static-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
            case PipelineType.Skinned:
                sg_shader shader_skinned = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
                var swapchain_skinned = sglue_swapchain();
                // Create pipeline for skinned meshes
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = swapchain_skinned.sample_count;
                pipeline_desc.colors[0].pixel_format = swapchain_skinned.color_format;
                pipeline_desc.depth.pixel_format = swapchain_skinned.depth_format;
                pipeline_desc.label = "skinned-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
                
            case PipelineType.StandardBlend:
                sg_shader shader_static_blend = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
                var swapchain_blend = sglue_swapchain();
                // Create pipeline for static meshes with alpha blending
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_NONE; // Disable culling for transparent objects
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false; // Disable depth writes for transparent objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = swapchain_blend.sample_count;
                pipeline_desc.colors[0].pixel_format = swapchain_blend.color_format;
                pipeline_desc.depth.pixel_format = swapchain_blend.depth_format;
                // Enable alpha blending
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.label = "static-blend-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
                
            case PipelineType.SkinnedBlend:
                sg_shader shader_skinned_blend = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
                var swapchain_skinned_blend = sglue_swapchain();
                // Create pipeline for skinned meshes with alpha blending
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_NONE; // Disable culling for transparent objects
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false; // Disable depth writes for transparent objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = swapchain_skinned_blend.sample_count;
                pipeline_desc.colors[0].pixel_format = swapchain_skinned_blend.color_format;
                pipeline_desc.depth.pixel_format = swapchain_skinned_blend.depth_format;
                // Enable alpha blending
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.label = "skinned-blend-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
                
            case PipelineType.StandardMask:
                sg_shader shader_static_mask = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
                var swapchain_mask = sglue_swapchain();
                // Create pipeline for static meshes with alpha masking (cutout)
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true; // Keep depth writes for masked objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = swapchain_mask.sample_count;
                pipeline_desc.colors[0].pixel_format = swapchain_mask.color_format;
                pipeline_desc.depth.pixel_format = swapchain_mask.depth_format;
                pipeline_desc.label = "static-mask-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
                
            case PipelineType.SkinnedMask:
                sg_shader shader_skinned_mask = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
                var swapchain_skinned_mask = sglue_swapchain();
                // Create pipeline for skinned meshes with alpha masking (cutout)
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true; // Keep depth writes for masked objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = swapchain_skinned_mask.sample_count;
                pipeline_desc.colors[0].pixel_format = swapchain_skinned_mask.color_format;
                pipeline_desc.depth.pixel_format = swapchain_skinned_mask.depth_format;
                pipeline_desc.label = "skinned-mask-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
                
            case PipelineType.TransmissionOpaque:
            case PipelineType.TransmissionOpaqueSkinned:
                // These require custom render pass, use CreatePipelineForPass instead
                throw new InvalidOperationException($"Pipeline type {type} requires CreatePipelineForPass with custom render pass");

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        _pipelines[type] = pipeline;
        return pipeline;
    }

    /// <summary>
    /// Create a pipeline for a custom render pass (e.g., transmission opaque pass)
    /// Caches pipelines based on type and render pass parameters to avoid recreating them
    /// </summary>
    public static sg_pipeline CreatePipelineForPass(PipelineType type, sg_pixel_format color_format, sg_pixel_format depth_format, int sample_count)
    {
        // Check cache first
        var cache_key = (type, color_format, depth_format, sample_count);
        if (_customPassPipelines.ContainsKey(cache_key))
        {
            return _customPassPipelines[cache_key];
        }
        
        var pipeline_desc = default(sg_pipeline_desc);
        
        switch (type)
        {
            case PipelineType.TransmissionOpaque:
                // Standard mesh pipeline rendering to transmission opaque pass
                sg_shader shader_static = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = sample_count;
                pipeline_desc.colors[0].pixel_format = color_format;
                pipeline_desc.depth.pixel_format = depth_format;
                pipeline_desc.label = "transmission-opaque-static-pipeline";
                break;
                
            case PipelineType.TransmissionOpaqueSkinned:
                // Skinned mesh pipeline rendering to transmission opaque pass
                sg_shader shader_skinned = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = sample_count;
                pipeline_desc.colors[0].pixel_format = color_format;
                pipeline_desc.depth.pixel_format = depth_format;
                pipeline_desc.label = "transmission-opaque-skinned-pipeline";
                break;
                
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Pipeline type not supported for custom render pass");
        }
        
        var pipeline = sg_make_pipeline(pipeline_desc);
        _customPassPipelines[cache_key] = pipeline;
        return pipeline;
    }

    public static int GetAttrSlot(PipelineType type, string attr_name)
    {
        int result = -1;
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_attr_slot(attr_name);
                break;

            case PipelineType.Skinned:
                result = skinning_metallic_attr_slot(attr_name);
                break;

        }

        if (result == -1)
            throw new ArgumentOutOfRangeException(attr_name);
        return result;
    }

    public static int GetTextureSlot(PipelineType type, string tex_name)
    {
        int result = -1;
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_texture_slot(tex_name);
                break;

            case PipelineType.Skinned:
                result = skinning_metallic_texture_slot(tex_name);
                break;


        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(tex_name);
        return result;
    }

    public static int GetSamplerSlot(PipelineType type, string smp_name)
    {
        int result = -1;
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_sampler_slot(smp_name);
                break;


            case PipelineType.Skinned:
                result = skinning_metallic_sampler_slot(smp_name);
                break;


        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(smp_name);
        return result;
    }

    public static int GetUniformBlockSlot(PipelineType type, string ub_name)
    {
        int result = -1;
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_uniformblock_slot(ub_name);
                break;


            case PipelineType.Skinned:
                result = skinning_metallic_uniformblock_slot(ub_name);
                break;


        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(ub_name);

        return result;
    }

    public static int GetUniformBlockSize(PipelineType type, string ub_name)
    {
        int result = -1;
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_uniformblock_size(ub_name);
                break;


            case PipelineType.Skinned:
                result = skinning_metallic_uniformblock_size(ub_name);
                break;

        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(ub_name);

        return result;
    }

    public static int GetUniformOffset(PipelineType type, string ub_name, string u_name)
    {
        int result = -1;
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_uniform_offset(ub_name, u_name);
                break;


            case PipelineType.Skinned:
                result = skinning_metallic_uniform_offset(ub_name, u_name);
                break;

        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(u_name);

        return result;
    }

    public static sg_glsl_shader_uniform GetUniformDesc(PipelineType type, string ub_name, string u_name)
    {
        sg_glsl_shader_uniform result = default;
        bool found = false;
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_uniform_desc(ub_name, u_name);
                found = true;
                break;


            case PipelineType.Skinned:
                result = skinning_metallic_uniform_desc(ub_name, u_name);
                found = true;
                break;


        }
        if (!found)
            throw new ArgumentOutOfRangeException(u_name);

        return result;
    }

    public static int GetStorageBufferSlot(PipelineType type,string sbuf_name) {
        int result = -1;        
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_storagebuffer_slot(sbuf_name);
                break;


            case PipelineType.Skinned:
                result = skinning_metallic_storagebuffer_slot(sbuf_name);
                break;

        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(sbuf_name);

        return result;
    }
    public static int GetMetallicStorageImageSlot(PipelineType type,string simg_name) {
        int result = -1;        
        switch (type)
        {
            case PipelineType.Standard:
                result = cgltf_metallic_storageimage_slot(simg_name);
                break;      
            case PipelineType.Skinned:
                result = skinning_metallic_storageimage_slot(simg_name);
                break;
        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(simg_name);
        return result;
    }

    /// <summary>
    /// Get the appropriate pipeline type based on alpha mode and skinning
    /// </summary>
    public static PipelineType GetPipelineTypeForMaterial(SharpGLTF.Schema2.AlphaMode alphaMode, bool hasSkinning)
    {
        switch (alphaMode)
        {
            case SharpGLTF.Schema2.AlphaMode.OPAQUE:
                return hasSkinning ? PipelineType.Skinned : PipelineType.Standard;
                
            case SharpGLTF.Schema2.AlphaMode.BLEND:
                return hasSkinning ? PipelineType.SkinnedBlend : PipelineType.StandardBlend;
                
            case SharpGLTF.Schema2.AlphaMode.MASK:
                return hasSkinning ? PipelineType.SkinnedMask : PipelineType.StandardMask;
                
            default:
                return hasSkinning ? PipelineType.Skinned : PipelineType.Standard;
        }
    }

    /// <summary>
    /// Create a pipeline for offscreen rendering with custom formats (for bloom scene pass)
    /// Caches pipelines based on type and format parameters to avoid recreating them
    /// </summary>
    public static sg_pipeline CreateOffscreenPipeline(PipelineType type, sg_pixel_format colorFormat, sg_pixel_format depthFormat)
    {
        // Check cache first (sample_count is always 1 for offscreen)
        var cache_key = (type, colorFormat, depthFormat, 1);
        if (_customPassPipelines.ContainsKey(cache_key))
        {
            return _customPassPipelines[cache_key];
        }
        
        sg_pipeline pipeline;
        var pipeline_desc = default(sg_pipeline_desc);
        
        switch (type)
        {
            case PipelineType.Standard:
                sg_shader shader_static = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = 1;  // Offscreen rendering uses sample_count = 1
                pipeline_desc.colors[0].pixel_format = colorFormat;
                pipeline_desc.depth.pixel_format = depthFormat;
                pipeline_desc.label = "bloom-scene-static-pipeline";
                break;
                
            case PipelineType.Skinned:
                sg_shader shader_skinned = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = 1;  // Offscreen rendering uses sample_count = 1
                pipeline_desc.colors[0].pixel_format = colorFormat;
                pipeline_desc.depth.pixel_format = depthFormat;
                pipeline_desc.label = "bloom-scene-skinned-pipeline";
                break;
                
            case PipelineType.StandardBlend:
                sg_shader shader_static_blend = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.sample_count = 1;  // Offscreen rendering uses sample_count = 1
                pipeline_desc.colors[0].pixel_format = colorFormat;
                pipeline_desc.depth.pixel_format = depthFormat;
                pipeline_desc.label = "bloom-scene-static-blend-pipeline";
                break;
                
            case PipelineType.SkinnedBlend:
                sg_shader shader_skinned_blend = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.sample_count = 1;  // Offscreen rendering uses sample_count = 1
                pipeline_desc.colors[0].pixel_format = colorFormat;
                pipeline_desc.depth.pixel_format = depthFormat;
                pipeline_desc.label = "bloom-scene-skinned-blend-pipeline";
                break;
                
            case PipelineType.StandardMask:
                sg_shader shader_static_mask = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = 1;  // Offscreen rendering uses sample_count = 1
                pipeline_desc.colors[0].pixel_format = colorFormat;
                pipeline_desc.depth.pixel_format = depthFormat;
                pipeline_desc.label = "bloom-scene-static-mask-pipeline";
                break;
                
            case PipelineType.SkinnedMask:
                sg_shader shader_skinned_mask = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = SG_CULLMODE_BACK;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = 1;  // Offscreen rendering uses sample_count = 1
                pipeline_desc.colors[0].pixel_format = colorFormat;
                pipeline_desc.depth.pixel_format = depthFormat;
                pipeline_desc.label = "bloom-scene-skinned-mask-pipeline";
                break;
                
            default:
                throw new ArgumentException($"Unsupported pipeline type: {type}");
        }
        
        pipeline = sg_make_pipeline(pipeline_desc);
        _customPassPipelines[cache_key] = pipeline;
        return pipeline;
    }

}