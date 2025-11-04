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
    
    // 32-bit index variants (for meshes with >65535 vertices)
    Standard32,
    Skinned32,
    StandardBlend32,
    SkinnedBlend32,
    StandardMask32,
    SkinnedMask32,
}

public static class PipeLineManager
{
    // Pipeline cache with cull mode support (key: (type, cullMode))
    private static Dictionary<(PipelineType, sg_cull_mode), sg_pipeline> _pipelines = new Dictionary<(PipelineType, sg_cull_mode), sg_pipeline>();
    
    // Cache for custom render pass pipelines (key includes format/sample_count/cullMode)
    private static Dictionary<(PipelineType, sg_pixel_format, sg_pixel_format, int, sg_cull_mode), sg_pipeline> _customPassPipelines = 
        new Dictionary<(PipelineType, sg_pixel_format, sg_pixel_format, int, sg_cull_mode), sg_pipeline>();

    /// <summary>
    /// Clear all pipeline caches. Call this when destroying all resources (e.g., on window resize).
    /// This ensures pipelines will be recreated on next use.
    /// </summary>
    public static void ClearCaches()
    {
        _pipelines.Clear();
        _customPassPipelines.Clear();
    }

    public static sg_pipeline GetOrCreatePipeline(PipelineType type, sg_cull_mode cullMode = SG_CULLMODE_BACK)
    {
        var cacheKey = (type, cullMode);
        if (_pipelines.ContainsKey(cacheKey))
        {
            return _pipelines[cacheKey];
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
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;  // Use 32-bit to support large meshes (>65535 vertices)
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.cull_mode = cullMode;  // Use provided cull mode
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
                pipeline_desc.cull_mode = cullMode;  // Use provided cull mode
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
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true; // Keep depth writes for masked objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = swapchain_skinned_mask.sample_count;
                pipeline_desc.colors[0].pixel_format = swapchain_skinned_mask.color_format;
                pipeline_desc.depth.pixel_format = swapchain_skinned_mask.depth_format;
                pipeline_desc.label = "skinned-mask-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
                
            // 32-bit index variants (identical to 16-bit versions except index_type)
            case PipelineType.Standard32:
            case PipelineType.Skinned32:
            case PipelineType.StandardBlend32:
            case PipelineType.SkinnedBlend32:
            case PipelineType.StandardMask32:
            case PipelineType.SkinnedMask32:
                // Create pipeline with same settings as base type, but with 32-bit indices
                var baseType = GetBasePipelineType(type);
                var basePipeline = GetOrCreatePipeline(baseType, cullMode);
                
                // Copy the base pipeline descriptor but change index type
                // Since we can't copy pipeline descriptors, we'll create a new one with 32-bit indices
                pipeline = CreatePipeline32BitVariant(type, cullMode);
                break;
                
            case PipelineType.TransmissionOpaque:
            case PipelineType.TransmissionOpaqueSkinned:
                // These require custom render pass, use CreatePipelineForPass instead
                throw new InvalidOperationException($"Pipeline type {type} requires CreatePipelineForPass with custom render pass");

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        _pipelines[cacheKey] = pipeline;
        return pipeline;
    }
    
    /// <summary>
    /// Create a 32-bit index variant of a pipeline
    /// </summary>
    private static sg_pipeline CreatePipeline32BitVariant(PipelineType type, sg_cull_mode cullMode)
    {
        var baseType = GetBasePipelineType(type);
        var pipeline_desc = default(sg_pipeline_desc);
        var swapchain = sglue_swapchain();
        
        // Determine shader based on base type
        sg_shader shader;
        if (baseType == PipelineType.Skinned || baseType == PipelineType.SkinnedBlend || baseType == PipelineType.SkinnedMask)
        {
            shader = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
        }
        else
        {
            shader = sg_make_shader(cgltf_metallic_shader_desc(sg_query_backend()));
        }
        
        // Common setup for all variants
        pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "color")].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord")].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "boneIds")].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "weights")].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.shader = shader;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT32;  // 32-bit indices for large meshes
        pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.sample_count = swapchain.sample_count;
        pipeline_desc.colors[0].pixel_format = swapchain.color_format;
        pipeline_desc.depth.pixel_format = swapchain.depth_format;
        
        // Type-specific settings
        switch (baseType)
        {
            case PipelineType.Standard:
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.label = "static-32bit-pipeline";
                break;
                
            case PipelineType.Skinned:
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.label = "skinned-32bit-pipeline";
                break;
                
            case PipelineType.StandardBlend:
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.depth.write_enabled = false;
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.label = "static-blend-32bit-pipeline";
                break;
                
            case PipelineType.SkinnedBlend:
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.depth.write_enabled = false;
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.label = "skinned-blend-32bit-pipeline";
                break;
                
            case PipelineType.StandardMask:
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.label = "static-mask-32bit-pipeline";
                break;
                
            case PipelineType.SkinnedMask:
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.label = "skinned-mask-32bit-pipeline";
                break;
        }
        
        return sg_make_pipeline(pipeline_desc);
    }

    /// <summary>
    /// Create a pipeline for a custom render pass (e.g., transmission opaque pass)
    /// Caches pipelines based on type and render pass parameters to avoid recreating them
    /// </summary>
    public static sg_pipeline CreatePipelineForPass(PipelineType type, sg_pixel_format color_format, sg_pixel_format depth_format, int sample_count, sg_cull_mode cullMode = SG_CULLMODE_BACK)
    {
        // Check cache first
        var cache_key = (type, color_format, depth_format, sample_count, cullMode);
        if (_customPassPipelines.ContainsKey(cache_key))
        {
            return _customPassPipelines[cache_key];
        }
        
        // Determine if this is a 32-bit variant
        var baseType = GetBasePipelineType(type);
        bool is32Bit = (baseType != type);
        sg_index_type indexType = is32Bit ? SG_INDEXTYPE_UINT32 : SG_INDEXTYPE_UINT16;
        
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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

    /// <summary>
    /// Get the base pipeline type (maps 32-bit variants to their 16-bit base types for shader lookups)
    /// </summary>
    private static PipelineType GetBasePipelineType(PipelineType type)
    {
        return type switch
        {
            PipelineType.Standard32 => PipelineType.Standard,
            PipelineType.Skinned32 => PipelineType.Skinned,
            PipelineType.StandardBlend32 => PipelineType.StandardBlend,
            PipelineType.SkinnedBlend32 => PipelineType.SkinnedBlend,
            PipelineType.StandardMask32 => PipelineType.StandardMask,
            PipelineType.SkinnedMask32 => PipelineType.SkinnedMask,
            _ => type
        };
    }

    public static int GetAttrSlot(PipelineType type, string attr_name)
    {
        // Map 32-bit variants to base type for shader lookups
        var baseType = GetBasePipelineType(type);
        
        int result = -1;
        switch (baseType)
        {
            case PipelineType.Standard:
            case PipelineType.StandardBlend:
            case PipelineType.StandardMask:
                result = cgltf_metallic_attr_slot(attr_name);
                break;

            case PipelineType.Skinned:
            case PipelineType.SkinnedBlend:
            case PipelineType.SkinnedMask:
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
    /// Get the appropriate pipeline type based on alpha mode, skinning, and index type
    /// </summary>
    public static PipelineType GetPipelineTypeForMaterial(SharpGLTF.Schema2.AlphaMode alphaMode, bool hasSkinning, bool needs32BitIndices = false)
    {
        if (needs32BitIndices)
        {
            // Use 32-bit index pipeline variants for large meshes
            switch (alphaMode)
            {
                case SharpGLTF.Schema2.AlphaMode.OPAQUE:
                    return hasSkinning ? PipelineType.Skinned32 : PipelineType.Standard32;
                    
                case SharpGLTF.Schema2.AlphaMode.BLEND:
                    return hasSkinning ? PipelineType.SkinnedBlend32 : PipelineType.StandardBlend32;
                    
                case SharpGLTF.Schema2.AlphaMode.MASK:
                    return hasSkinning ? PipelineType.SkinnedMask32 : PipelineType.StandardMask32;
                    
                default:
                    return hasSkinning ? PipelineType.Skinned32 : PipelineType.Standard32;
            }
        }
        else
        {
            // Use 16-bit index pipelines for smaller meshes (more memory efficient)
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
    }

    /// <summary>
    /// Create a pipeline for offscreen rendering with custom formats (for bloom scene pass)
    /// Caches pipelines based on type and format parameters to avoid recreating them
    /// </summary>
    public static sg_pipeline CreateOffscreenPipeline(PipelineType type, sg_pixel_format colorFormat, sg_pixel_format depthFormat, sg_cull_mode cullMode = SG_CULLMODE_BACK)
    {
        // Check cache first (sample_count is always 1 for offscreen)
        var cache_key = (type, colorFormat, depthFormat, 1, cullMode);
        if (_customPassPipelines.ContainsKey(cache_key))
        {
            return _customPassPipelines[cache_key];
        }
        
        // Determine if this is a 32-bit variant
        var baseType = GetBasePipelineType(type);
        bool is32Bit = (baseType != type);
        sg_index_type indexType = is32Bit ? SG_INDEXTYPE_UINT32 : SG_INDEXTYPE_UINT16;
        
        sg_pipeline pipeline;
        var pipeline_desc = default(sg_pipeline_desc);
        
        switch (baseType)
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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
                pipeline_desc.index_type = indexType;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
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