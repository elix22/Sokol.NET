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
}

public static class PipeLineManager
{
    private static Dictionary<PipelineType, sg_pipeline> _pipelines = new Dictionary<PipelineType, sg_pipeline>();

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
                pipeline_desc.label = "static-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;
            case PipelineType.Skinned:
                sg_shader shader_skinned = sg_make_shader(skinning_metallic_shader_desc(sg_query_backend()));
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
                pipeline_desc.label = "skinned-pipeline";
                pipeline = sg_make_pipeline(pipeline_desc);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }

        _pipelines[type] = pipeline;
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



}