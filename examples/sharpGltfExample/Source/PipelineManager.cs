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
using static pbr_shader_cs.Shaders;
using static pbr_shader_skinning_cs_skinning.Shaders;
using static pbr_shader_morphing_cs_morphing.Shaders;
using static pbr_shader_skinning_morphing_cs_skinning_morphing.Shaders;
using static bloom_shader_cs.Shaders;

public enum PipelineType
{
    Standard,
    Skinned,
    Morphing,                 // Morphing only
    SkinnedMorphing,          // Both skinning and morphing
    StandardBlend,            // For alpha blending
    SkinnedBlend,             // For alpha blending with skinning
    MorphingBlend,            // For alpha blending with morphing
    SkinnedMorphingBlend,     // For alpha blending with skinning + morphing
    StandardMask,             // For alpha masking
    SkinnedMask,              // For alpha masking with skinning
    MorphingMask,             // For alpha masking with morphing
    SkinnedMorphingMask,      // For alpha masking with skinning + morphing
    
    // Transmission (glass materials) pipelines - render opaque objects to screen texture
    TransmissionOpaque,        // Opaque pass for standard meshes
    TransmissionOpaqueSkinned, // Opaque pass for skinned meshes
    TransmissionOpaqueMorphing,        // Opaque pass for morphing meshes
    TransmissionOpaqueSkinnedMorphing, // Opaque pass for skinned + morphing meshes
    
    // 32-bit index variants (for meshes with >65535 vertices)
    Standard32,
    Skinned32,
    Morphing32,
    SkinnedMorphing32,
    StandardBlend32,
    SkinnedBlend32,
    MorphingBlend32,
    SkinnedMorphingBlend32,
    StandardMask32,
    SkinnedMask32,
    MorphingMask32,
    SkinnedMorphingMask32,
    
    // Post-processing pipelines
    BloomBright,           // Bright pass for bloom effect
    BloomBlurHorizontal,   // Horizontal blur for bloom effect
    BloomBlurVertical,     // Vertical blur for bloom effect
    BloomComposite,        // Composite pass for bloom effect
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


//, sg_pixel_format colorFormat, sg_pixel_format depthFormat, sg_cull_mode cullMode = SG_CULLMODE_BACK
    public static sg_pipeline GetOrCreatePipeline(PipelineType type, sg_cull_mode cullMode = SG_CULLMODE_BACK, sg_pixel_format? colorFormat = null, sg_pixel_format? depthFormat = null, int? sampleCount = null)
    {
        // Determine if this is a custom format pipeline (for render passes or offscreen)
        bool isCustomFormat = colorFormat.HasValue && depthFormat.HasValue;
        
        // Validate that colorFormat and depthFormat are provided together
        if (colorFormat.HasValue != depthFormat.HasValue)
        {
            throw new ArgumentException("colorFormat and depthFormat must be provided together");
        }
        
        if (colorFormat.HasValue && depthFormat.HasValue)
        {
            // Use custom pass pipeline cache
            var customColorFormat = colorFormat.Value;
            var customDepthFormat = depthFormat.Value;
            var customCacheKey = (type, customColorFormat, customDepthFormat, sampleCount ?? 1, cullMode);
            if (_customPassPipelines.ContainsKey(customCacheKey))
            {
                return _customPassPipelines[customCacheKey];
            }
        }
        else
        {
            // Use main pipeline cache
            var mainCacheKey = (type, cullMode);
            if (_pipelines.ContainsKey(mainCacheKey))
            {
                return _pipelines[mainCacheKey];
            }
        }

        sg_pipeline pipeline;
        var pipeline_desc = default(sg_pipeline_desc);
        
        // Get formats and sample count
        sg_pixel_format finalColorFormat;
        sg_pixel_format finalDepthFormat;
        int finalSampleCount;
        
        if (colorFormat.HasValue && depthFormat.HasValue)
        {
            finalColorFormat = colorFormat.Value;
            finalDepthFormat = depthFormat.Value;
            finalSampleCount = sampleCount ?? 1; // Default to 1 for offscreen/custom passes
        }
        else
        {
            var swapchain = sglue_swapchain();
            finalColorFormat = swapchain.color_format;
            finalDepthFormat = swapchain.depth_format;
            finalSampleCount = swapchain.sample_count;
        }
        switch (type)
        {
            case PipelineType.Standard:
                sg_shader shader_static = sg_make_shader(pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for static meshes
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;  // Use 32-bit to support large meshes (>65535 vertices)
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "static-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
            case PipelineType.Skinned:
                sg_shader shader_skinned = sg_make_shader(skinning_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for skinned meshes
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "skinned-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.Morphing:
                sg_shader shader_morphing = sg_make_shader(morphing_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for morphing meshes
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_morphing;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "morphing-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.SkinnedMorphing:
                sg_shader shader_skinned_morphing = sg_make_shader(skinning_morphing_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for skinned + morphing meshes
                pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_morphing;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "skinned-morphing-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.StandardBlend:
                sg_shader shader_static_blend = sg_make_shader(pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for static meshes with alpha blending
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;  // Use provided cull mode
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false; // Disable depth writes for transparent objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
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
                sg_shader shader_skinned_blend = sg_make_shader(skinning_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for skinned meshes with alpha blending
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;  // Use provided cull mode
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false; // Disable depth writes for transparent objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                // Enable alpha blending
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.label = "skinned-blend-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.MorphingBlend:
                sg_shader shader_morphing_blend = sg_make_shader(morphing_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for morphing meshes with alpha blending
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_morphing_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false; // Disable depth writes for transparent objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                // Enable alpha blending
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.label = "morphing-blend-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.SkinnedMorphingBlend:
                sg_shader shader_skinned_morphing_blend = sg_make_shader(skinning_morphing_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for skinned + morphing meshes with alpha blending
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_morphing_blend;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false; // Disable depth writes for transparent objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                // Enable alpha blending
                pipeline_desc.colors[0].blend.enabled = true;
                pipeline_desc.colors[0].blend.src_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_SRC_ALPHA;
                pipeline_desc.colors[0].blend.dst_factor_rgb = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.colors[0].blend.src_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE;
                pipeline_desc.colors[0].blend.dst_factor_alpha = sg_blend_factor.SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA;
                pipeline_desc.label = "skinned-morphing-blend-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.StandardMask:
                sg_shader shader_static_mask = sg_make_shader(pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for static meshes with alpha masking (cutout)
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_static_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true; // Keep depth writes for masked objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "static-mask-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.SkinnedMask:
                sg_shader shader_skinned_mask = sg_make_shader(skinning_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for skinned meshes with alpha masking (cutout)
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true; // Keep depth writes for masked objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "skinned-mask-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.MorphingMask:
                sg_shader shader_morphing_mask = sg_make_shader(morphing_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for morphing meshes with alpha masking (cutout)
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Morphing, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_morphing_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true; // Keep depth writes for masked objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "morphing-mask-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.SkinnedMorphingMask:
                sg_shader shader_skinned_morphing_mask = sg_make_shader(skinning_morphing_pbr_program_shader_desc(sg_query_backend()));
                // Create pipeline for skinned + morphing meshes with alpha masking (cutout)
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.SkinnedMorphing, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_skinned_morphing_mask;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true; // Keep depth writes for masked objects
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "skinned-morphing-mask-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            // 32-bit index variants (identical to 16-bit versions except index_type)
            case PipelineType.Standard32:
            case PipelineType.Skinned32:
            case PipelineType.Morphing32:
            case PipelineType.SkinnedMorphing32:
            case PipelineType.StandardBlend32:
            case PipelineType.SkinnedBlend32:
            case PipelineType.MorphingBlend32:
            case PipelineType.SkinnedMorphingBlend32:
            case PipelineType.StandardMask32:
            case PipelineType.SkinnedMask32:
            case PipelineType.MorphingMask32:
            case PipelineType.SkinnedMorphingMask32:
                // Create pipeline with same settings as base type, but with 32-bit indices
                var baseType = GetBasePipelineType(type);
                pipeline = CreatePipeline32BitVariant(type, cullMode, finalColorFormat, finalDepthFormat, finalSampleCount);
                break;

            case PipelineType.TransmissionOpaque:
                // Standard mesh pipeline rendering to transmission opaque pass
                sg_shader shader_transmission_static = sg_make_shader(pbr_program_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Standard, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_transmission_static;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "transmission-opaque-static-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.TransmissionOpaqueSkinned:
                // Skinned mesh pipeline rendering to transmission opaque pass
                sg_shader shader_transmission_skinned = sg_make_shader(skinning_pbr_program_shader_desc(sg_query_backend()));
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "position")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.layout.attrs[GetAttrSlot(PipelineType.Skinned, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
                pipeline_desc.shader = shader_transmission_skinned;
                pipeline_desc.index_type = SG_INDEXTYPE_UINT16;  // Use appropriate index type (16-bit or 32-bit)
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = true;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "transmission-opaque-skinned-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.BloomBright:
                // Bright pass pipeline for bloom effect (fullscreen quad)
                pipeline_desc.layout.attrs[ATTR_bright_pass_position].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.shader = sg_make_shader(bright_pass_shader_desc(sg_query_backend()));
                pipeline_desc.primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES;
                pipeline_desc.index_type = sg_index_type.SG_INDEXTYPE_NONE;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_ALWAYS;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "bloom-bright-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.BloomBlurHorizontal:
                // Horizontal blur pipeline for bloom effect (fullscreen quad)
                pipeline_desc.layout.attrs[ATTR_blur_horizontal_position].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.shader = sg_make_shader(blur_horizontal_shader_desc(sg_query_backend()));
                pipeline_desc.primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES;
                pipeline_desc.index_type = sg_index_type.SG_INDEXTYPE_NONE;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_ALWAYS;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "bloom-blur-h-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.BloomBlurVertical:
                // Vertical blur pipeline for bloom effect (fullscreen quad)
                pipeline_desc.layout.attrs[ATTR_blur_vertical_position].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.shader = sg_make_shader(blur_vertical_shader_desc(sg_query_backend()));
                pipeline_desc.primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES;
                pipeline_desc.index_type = sg_index_type.SG_INDEXTYPE_NONE;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false;
                pipeline_desc.depth.compare = SG_COMPAREFUNC_ALWAYS;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                pipeline_desc.depth.pixel_format = finalDepthFormat;
                pipeline_desc.label = "bloom-blur-v-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            case PipelineType.BloomComposite:
                // Composite pipeline for bloom effect (renders to swapchain)
                pipeline_desc.layout.attrs[ATTR_bloom_composite_position].format = SG_VERTEXFORMAT_FLOAT2;
                pipeline_desc.shader = sg_make_shader(bloom_shader_cs.Shaders.bloom_composite_shader_desc(sg_query_backend()));
                pipeline_desc.primitive_type = sg_primitive_type.SG_PRIMITIVETYPE_TRIANGLES;
                pipeline_desc.index_type = sg_index_type.SG_INDEXTYPE_NONE;
                pipeline_desc.cull_mode = cullMode;
                pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
                pipeline_desc.depth.write_enabled = false;
                pipeline_desc.depth.compare = sg_compare_func.SG_COMPAREFUNC_ALWAYS;
                pipeline_desc.sample_count = finalSampleCount;
                pipeline_desc.colors[0].pixel_format = finalColorFormat;
                // Note: depth.pixel_format is intentionally not set for composite pipeline (renders to swapchain)
                pipeline_desc.label = "bloom-composite-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        if (colorFormat.HasValue && depthFormat.HasValue)
        {
            var customCacheKey = (type, colorFormat.Value, depthFormat.Value, sampleCount ?? 1, cullMode);
            _customPassPipelines[customCacheKey] = pipeline;
        }
        else
        {
            var mainCacheKey = (type, cullMode);
            _pipelines[mainCacheKey] = pipeline;
        }
        return pipeline;
    }
    
    /// <summary>
    /// Create a 32-bit index variant of a pipeline
    /// </summary>
    private static sg_pipeline CreatePipeline32BitVariant(PipelineType type, sg_cull_mode cullMode, sg_pixel_format colorFormat, sg_pixel_format depthFormat, int sampleCount)
    {
        var baseType = GetBasePipelineType(type);
        var pipeline_desc = default(sg_pipeline_desc);
        
        // Determine shader based on base type
        sg_shader shader;
        if (baseType == PipelineType.Skinned || baseType == PipelineType.SkinnedBlend || baseType == PipelineType.SkinnedMask)
        {
            shader = sg_make_shader(skinning_pbr_program_shader_desc(sg_query_backend()));
        }
        else
        {
            shader = sg_make_shader(pbr_program_shader_desc(sg_query_backend()));
        }
        
        // Common setup for all variants
        pipeline_desc.layout.attrs[GetAttrSlot(type, "position")].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "normal")].format = SG_VERTEXFORMAT_FLOAT3;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "tangent")].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "color_0")].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_0")].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "texcoord_1")].format = SG_VERTEXFORMAT_FLOAT2;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "joints_0")].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.layout.attrs[GetAttrSlot(type, "weights_0")].format = SG_VERTEXFORMAT_FLOAT4;
        pipeline_desc.shader = shader;
        pipeline_desc.index_type = SG_INDEXTYPE_UINT32;  // 32-bit indices for large meshes
        pipeline_desc.face_winding = sg_face_winding.SG_FACEWINDING_CCW;
        pipeline_desc.depth.compare = SG_COMPAREFUNC_LESS_EQUAL;
        pipeline_desc.sample_count = sampleCount;
        pipeline_desc.colors[0].pixel_format = colorFormat;
        pipeline_desc.depth.pixel_format = depthFormat;
        
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




    /// <summary>
    /// Get the base pipeline type (maps 32-bit variants to their 16-bit base types for shader lookups)
    /// </summary>
    private static PipelineType GetBasePipelineType(PipelineType type)
    {
        return type switch
        {
            PipelineType.Standard32 => PipelineType.Standard,
            PipelineType.Skinned32 => PipelineType.Skinned,
            PipelineType.Morphing32 => PipelineType.Morphing,
            PipelineType.SkinnedMorphing32 => PipelineType.SkinnedMorphing,
            PipelineType.StandardBlend32 => PipelineType.StandardBlend,
            PipelineType.SkinnedBlend32 => PipelineType.SkinnedBlend,
            PipelineType.MorphingBlend32 => PipelineType.MorphingBlend,
            PipelineType.SkinnedMorphingBlend32 => PipelineType.SkinnedMorphingBlend,
            PipelineType.StandardMask32 => PipelineType.StandardMask,
            PipelineType.SkinnedMask32 => PipelineType.SkinnedMask,
            PipelineType.MorphingMask32 => PipelineType.MorphingMask,
            PipelineType.SkinnedMorphingMask32 => PipelineType.SkinnedMorphingMask,
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
            case PipelineType.TransmissionOpaque:
                result = pbr_program_attr_slot(attr_name);
                break;

            case PipelineType.Skinned:
            case PipelineType.SkinnedBlend:
            case PipelineType.SkinnedMask:
            case PipelineType.TransmissionOpaqueSkinned:
                result = skinning_pbr_program_attr_slot(attr_name);
                break;

            case PipelineType.Morphing:
            case PipelineType.MorphingBlend:
            case PipelineType.MorphingMask:
            case PipelineType.TransmissionOpaqueMorphing:
                result = morphing_pbr_program_attr_slot(attr_name);
                break;

            case PipelineType.SkinnedMorphing:
            case PipelineType.SkinnedMorphingBlend:
            case PipelineType.SkinnedMorphingMask:
            case PipelineType.TransmissionOpaqueSkinnedMorphing:
                result = skinning_morphing_pbr_program_attr_slot(attr_name);
                break;
                
            case PipelineType.BloomBright:
            case PipelineType.BloomBlurHorizontal:
            case PipelineType.BloomBlurVertical:
            case PipelineType.BloomComposite:
                // Bloom pipelines use different shaders with different attributes
                // These are handled in GetOrCreatePipeline and don't use this method
                throw new InvalidOperationException($"Bloom pipeline types should not call GetAttrSlot. Use shader-specific attribute indices instead.");
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
                result = pbr_program_texture_slot(tex_name);
                break;

            case PipelineType.Skinned:
                result = skinning_pbr_program_texture_slot(tex_name);
                break;

            case PipelineType.Morphing:
                result = morphing_pbr_program_texture_slot(tex_name);
                break;

            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_texture_slot(tex_name);
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
                result = pbr_program_sampler_slot(smp_name);
                break;

            case PipelineType.Skinned:
                result = skinning_pbr_program_sampler_slot(smp_name);
                break;

            case PipelineType.Morphing:
                result = morphing_pbr_program_sampler_slot(smp_name);
                break;

            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_sampler_slot(smp_name);
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
                result = pbr_program_uniformblock_slot(ub_name);
                break;

            case PipelineType.Skinned:
                result = skinning_pbr_program_uniformblock_slot(ub_name);
                break;

            case PipelineType.Morphing:
                result = morphing_pbr_program_uniformblock_slot(ub_name);
                break;

            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_uniformblock_slot(ub_name);
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
                result = pbr_program_uniformblock_size(ub_name);
                break;

            case PipelineType.Skinned:
                result = skinning_pbr_program_uniformblock_size(ub_name);
                break;

            case PipelineType.Morphing:
                result = morphing_pbr_program_uniformblock_size(ub_name);
                break;

            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_uniformblock_size(ub_name);
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
                result = pbr_program_uniform_offset(ub_name, u_name);
                break;

            case PipelineType.Skinned:
                result = skinning_pbr_program_uniform_offset(ub_name, u_name);
                break;

            case PipelineType.Morphing:
                result = morphing_pbr_program_uniform_offset(ub_name, u_name);
                break;

            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_uniform_offset(ub_name, u_name);
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
                result = pbr_program_uniform_desc(ub_name, u_name);
                found = true;
                break;

            case PipelineType.Skinned:
                result = skinning_pbr_program_uniform_desc(ub_name, u_name);
                found = true;
                break;

            case PipelineType.Morphing:
                result = morphing_pbr_program_uniform_desc(ub_name, u_name);
                found = true;
                break;

            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_uniform_desc(ub_name, u_name);
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
                result = pbr_program_storagebuffer_slot(sbuf_name);
                break;

            case PipelineType.Skinned:
                result = skinning_pbr_program_storagebuffer_slot(sbuf_name);
                break;

            case PipelineType.Morphing:
                result = morphing_pbr_program_storagebuffer_slot(sbuf_name);
                break;

            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_storagebuffer_slot(sbuf_name);
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
                result = pbr_program_storageimage_slot(simg_name);
                break;      
            case PipelineType.Skinned:
                result = skinning_pbr_program_storageimage_slot(simg_name);
                break;
            case PipelineType.Morphing:
                result = morphing_pbr_program_storageimage_slot(simg_name);
                break;
            case PipelineType.SkinnedMorphing:
                result = skinning_morphing_pbr_program_storageimage_slot(simg_name);
                break;
        }
        if (result == -1)
            throw new ArgumentOutOfRangeException(simg_name);
        return result;
    }

    /// <summary>
    /// Get the appropriate pipeline type based on alpha mode, skinning, morphing, and index type
    /// </summary>
    public static PipelineType GetPipelineTypeForMaterial(SharpGLTF.Schema2.AlphaMode alphaMode, bool hasSkinning, bool hasMorphing, bool needs32BitIndices = false)
    {
        if (needs32BitIndices)
        {
            // Use 32-bit index pipeline variants for large meshes
            switch (alphaMode)
            {
                case SharpGLTF.Schema2.AlphaMode.OPAQUE:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphing32;
                    if (hasSkinning) return PipelineType.Skinned32;
                    if (hasMorphing) return PipelineType.Morphing32;
                    return PipelineType.Standard32;
                    
                case SharpGLTF.Schema2.AlphaMode.BLEND:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphingBlend32;
                    if (hasSkinning) return PipelineType.SkinnedBlend32;
                    if (hasMorphing) return PipelineType.MorphingBlend32;
                    return PipelineType.StandardBlend32;
                    
                case SharpGLTF.Schema2.AlphaMode.MASK:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphingMask32;
                    if (hasSkinning) return PipelineType.SkinnedMask32;
                    if (hasMorphing) return PipelineType.MorphingMask32;
                    return PipelineType.StandardMask32;
                    
                default:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphing32;
                    if (hasSkinning) return PipelineType.Skinned32;
                    if (hasMorphing) return PipelineType.Morphing32;
                    return PipelineType.Standard32;
            }
        }
        else
        {
            // Use 16-bit index pipelines for smaller meshes (more memory efficient)
            switch (alphaMode)
            {
                case SharpGLTF.Schema2.AlphaMode.OPAQUE:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphing;
                    if (hasSkinning) return PipelineType.Skinned;
                    if (hasMorphing) return PipelineType.Morphing;
                    return PipelineType.Standard;
                    
                case SharpGLTF.Schema2.AlphaMode.BLEND:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphingBlend;
                    if (hasSkinning) return PipelineType.SkinnedBlend;
                    if (hasMorphing) return PipelineType.MorphingBlend;
                    return PipelineType.StandardBlend;
                    
                case SharpGLTF.Schema2.AlphaMode.MASK:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphingMask;
                    if (hasSkinning) return PipelineType.SkinnedMask;
                    if (hasMorphing) return PipelineType.MorphingMask;
                    return PipelineType.StandardMask;
                    
                default:
                    if (hasSkinning && hasMorphing) return PipelineType.SkinnedMorphing;
                    if (hasSkinning) return PipelineType.Skinned;
                    if (hasMorphing) return PipelineType.Morphing;
                    return PipelineType.Standard;
            }
        }
    }

    

}