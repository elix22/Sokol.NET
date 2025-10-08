// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

using static Sokol.SApp;

namespace Sokol
{
public static unsafe partial class SGImgui
{
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_str_t
{
    #pragma warning disable 169
    public struct bufCollection
    {
        public ref byte this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 96)[index];
        private byte _item0;
        private byte _item1;
        private byte _item2;
        private byte _item3;
        private byte _item4;
        private byte _item5;
        private byte _item6;
        private byte _item7;
        private byte _item8;
        private byte _item9;
        private byte _item10;
        private byte _item11;
        private byte _item12;
        private byte _item13;
        private byte _item14;
        private byte _item15;
        private byte _item16;
        private byte _item17;
        private byte _item18;
        private byte _item19;
        private byte _item20;
        private byte _item21;
        private byte _item22;
        private byte _item23;
        private byte _item24;
        private byte _item25;
        private byte _item26;
        private byte _item27;
        private byte _item28;
        private byte _item29;
        private byte _item30;
        private byte _item31;
        private byte _item32;
        private byte _item33;
        private byte _item34;
        private byte _item35;
        private byte _item36;
        private byte _item37;
        private byte _item38;
        private byte _item39;
        private byte _item40;
        private byte _item41;
        private byte _item42;
        private byte _item43;
        private byte _item44;
        private byte _item45;
        private byte _item46;
        private byte _item47;
        private byte _item48;
        private byte _item49;
        private byte _item50;
        private byte _item51;
        private byte _item52;
        private byte _item53;
        private byte _item54;
        private byte _item55;
        private byte _item56;
        private byte _item57;
        private byte _item58;
        private byte _item59;
        private byte _item60;
        private byte _item61;
        private byte _item62;
        private byte _item63;
        private byte _item64;
        private byte _item65;
        private byte _item66;
        private byte _item67;
        private byte _item68;
        private byte _item69;
        private byte _item70;
        private byte _item71;
        private byte _item72;
        private byte _item73;
        private byte _item74;
        private byte _item75;
        private byte _item76;
        private byte _item77;
        private byte _item78;
        private byte _item79;
        private byte _item80;
        private byte _item81;
        private byte _item82;
        private byte _item83;
        private byte _item84;
        private byte _item85;
        private byte _item86;
        private byte _item87;
        private byte _item88;
        private byte _item89;
        private byte _item90;
        private byte _item91;
        private byte _item92;
        private byte _item93;
        private byte _item94;
        private byte _item95;
    }
    #pragma warning restore 169
    public bufCollection buf;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_buffer_t
{
    public sg_buffer res_id;
    public sgimgui_str_t label;
    public sg_buffer_desc desc;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_image_t
{
    public sg_image res_id;
    public float ui_scale;
    public sgimgui_str_t label;
    public sg_image_desc desc;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_sampler_t
{
    public sg_sampler res_id;
    public sgimgui_str_t label;
    public sg_sampler_desc desc;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_shader_t
{
    public sg_shader res_id;
    public sgimgui_str_t label;
    public sgimgui_str_t vs_entry;
    public sgimgui_str_t vs_d3d11_target;
    public sgimgui_str_t fs_entry;
    public sgimgui_str_t fs_d3d11_target;
    #pragma warning disable 169
    public struct glsl_texture_sampler_nameCollection
    {
        public ref sgimgui_str_t this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 32)[index];
        private sgimgui_str_t _item0;
        private sgimgui_str_t _item1;
        private sgimgui_str_t _item2;
        private sgimgui_str_t _item3;
        private sgimgui_str_t _item4;
        private sgimgui_str_t _item5;
        private sgimgui_str_t _item6;
        private sgimgui_str_t _item7;
        private sgimgui_str_t _item8;
        private sgimgui_str_t _item9;
        private sgimgui_str_t _item10;
        private sgimgui_str_t _item11;
        private sgimgui_str_t _item12;
        private sgimgui_str_t _item13;
        private sgimgui_str_t _item14;
        private sgimgui_str_t _item15;
        private sgimgui_str_t _item16;
        private sgimgui_str_t _item17;
        private sgimgui_str_t _item18;
        private sgimgui_str_t _item19;
        private sgimgui_str_t _item20;
        private sgimgui_str_t _item21;
        private sgimgui_str_t _item22;
        private sgimgui_str_t _item23;
        private sgimgui_str_t _item24;
        private sgimgui_str_t _item25;
        private sgimgui_str_t _item26;
        private sgimgui_str_t _item27;
        private sgimgui_str_t _item28;
        private sgimgui_str_t _item29;
        private sgimgui_str_t _item30;
        private sgimgui_str_t _item31;
    }
    #pragma warning restore 169
    public glsl_texture_sampler_nameCollection glsl_texture_sampler_name;
    #pragma warning disable 169
    public struct glsl_uniform_nameCollection
    {
        public ref sgimgui_str_t this[int x, int y] { get { fixed (sgimgui_str_t* pTP = &_item0) return ref *(pTP + x + (y * 8)); } }
        private sgimgui_str_t _item0;
        private sgimgui_str_t _item1;
        private sgimgui_str_t _item2;
        private sgimgui_str_t _item3;
        private sgimgui_str_t _item4;
        private sgimgui_str_t _item5;
        private sgimgui_str_t _item6;
        private sgimgui_str_t _item7;
        private sgimgui_str_t _item8;
        private sgimgui_str_t _item9;
        private sgimgui_str_t _item10;
        private sgimgui_str_t _item11;
        private sgimgui_str_t _item12;
        private sgimgui_str_t _item13;
        private sgimgui_str_t _item14;
        private sgimgui_str_t _item15;
        private sgimgui_str_t _item16;
        private sgimgui_str_t _item17;
        private sgimgui_str_t _item18;
        private sgimgui_str_t _item19;
        private sgimgui_str_t _item20;
        private sgimgui_str_t _item21;
        private sgimgui_str_t _item22;
        private sgimgui_str_t _item23;
        private sgimgui_str_t _item24;
        private sgimgui_str_t _item25;
        private sgimgui_str_t _item26;
        private sgimgui_str_t _item27;
        private sgimgui_str_t _item28;
        private sgimgui_str_t _item29;
        private sgimgui_str_t _item30;
        private sgimgui_str_t _item31;
        private sgimgui_str_t _item32;
        private sgimgui_str_t _item33;
        private sgimgui_str_t _item34;
        private sgimgui_str_t _item35;
        private sgimgui_str_t _item36;
        private sgimgui_str_t _item37;
        private sgimgui_str_t _item38;
        private sgimgui_str_t _item39;
        private sgimgui_str_t _item40;
        private sgimgui_str_t _item41;
        private sgimgui_str_t _item42;
        private sgimgui_str_t _item43;
        private sgimgui_str_t _item44;
        private sgimgui_str_t _item45;
        private sgimgui_str_t _item46;
        private sgimgui_str_t _item47;
        private sgimgui_str_t _item48;
        private sgimgui_str_t _item49;
        private sgimgui_str_t _item50;
        private sgimgui_str_t _item51;
        private sgimgui_str_t _item52;
        private sgimgui_str_t _item53;
        private sgimgui_str_t _item54;
        private sgimgui_str_t _item55;
        private sgimgui_str_t _item56;
        private sgimgui_str_t _item57;
        private sgimgui_str_t _item58;
        private sgimgui_str_t _item59;
        private sgimgui_str_t _item60;
        private sgimgui_str_t _item61;
        private sgimgui_str_t _item62;
        private sgimgui_str_t _item63;
        private sgimgui_str_t _item64;
        private sgimgui_str_t _item65;
        private sgimgui_str_t _item66;
        private sgimgui_str_t _item67;
        private sgimgui_str_t _item68;
        private sgimgui_str_t _item69;
        private sgimgui_str_t _item70;
        private sgimgui_str_t _item71;
        private sgimgui_str_t _item72;
        private sgimgui_str_t _item73;
        private sgimgui_str_t _item74;
        private sgimgui_str_t _item75;
        private sgimgui_str_t _item76;
        private sgimgui_str_t _item77;
        private sgimgui_str_t _item78;
        private sgimgui_str_t _item79;
        private sgimgui_str_t _item80;
        private sgimgui_str_t _item81;
        private sgimgui_str_t _item82;
        private sgimgui_str_t _item83;
        private sgimgui_str_t _item84;
        private sgimgui_str_t _item85;
        private sgimgui_str_t _item86;
        private sgimgui_str_t _item87;
        private sgimgui_str_t _item88;
        private sgimgui_str_t _item89;
        private sgimgui_str_t _item90;
        private sgimgui_str_t _item91;
        private sgimgui_str_t _item92;
        private sgimgui_str_t _item93;
        private sgimgui_str_t _item94;
        private sgimgui_str_t _item95;
        private sgimgui_str_t _item96;
        private sgimgui_str_t _item97;
        private sgimgui_str_t _item98;
        private sgimgui_str_t _item99;
        private sgimgui_str_t _item100;
        private sgimgui_str_t _item101;
        private sgimgui_str_t _item102;
        private sgimgui_str_t _item103;
        private sgimgui_str_t _item104;
        private sgimgui_str_t _item105;
        private sgimgui_str_t _item106;
        private sgimgui_str_t _item107;
        private sgimgui_str_t _item108;
        private sgimgui_str_t _item109;
        private sgimgui_str_t _item110;
        private sgimgui_str_t _item111;
        private sgimgui_str_t _item112;
        private sgimgui_str_t _item113;
        private sgimgui_str_t _item114;
        private sgimgui_str_t _item115;
        private sgimgui_str_t _item116;
        private sgimgui_str_t _item117;
        private sgimgui_str_t _item118;
        private sgimgui_str_t _item119;
        private sgimgui_str_t _item120;
        private sgimgui_str_t _item121;
        private sgimgui_str_t _item122;
        private sgimgui_str_t _item123;
        private sgimgui_str_t _item124;
        private sgimgui_str_t _item125;
        private sgimgui_str_t _item126;
        private sgimgui_str_t _item127;
    }
    #pragma warning restore 169
    public glsl_uniform_nameCollection glsl_uniform_name;
    #pragma warning disable 169
    public struct attr_glsl_nameCollection
    {
        public ref sgimgui_str_t this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private sgimgui_str_t _item0;
        private sgimgui_str_t _item1;
        private sgimgui_str_t _item2;
        private sgimgui_str_t _item3;
        private sgimgui_str_t _item4;
        private sgimgui_str_t _item5;
        private sgimgui_str_t _item6;
        private sgimgui_str_t _item7;
        private sgimgui_str_t _item8;
        private sgimgui_str_t _item9;
        private sgimgui_str_t _item10;
        private sgimgui_str_t _item11;
        private sgimgui_str_t _item12;
        private sgimgui_str_t _item13;
        private sgimgui_str_t _item14;
        private sgimgui_str_t _item15;
    }
    #pragma warning restore 169
    public attr_glsl_nameCollection attr_glsl_name;
    #pragma warning disable 169
    public struct attr_hlsl_sem_nameCollection
    {
        public ref sgimgui_str_t this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private sgimgui_str_t _item0;
        private sgimgui_str_t _item1;
        private sgimgui_str_t _item2;
        private sgimgui_str_t _item3;
        private sgimgui_str_t _item4;
        private sgimgui_str_t _item5;
        private sgimgui_str_t _item6;
        private sgimgui_str_t _item7;
        private sgimgui_str_t _item8;
        private sgimgui_str_t _item9;
        private sgimgui_str_t _item10;
        private sgimgui_str_t _item11;
        private sgimgui_str_t _item12;
        private sgimgui_str_t _item13;
        private sgimgui_str_t _item14;
        private sgimgui_str_t _item15;
    }
    #pragma warning restore 169
    public attr_hlsl_sem_nameCollection attr_hlsl_sem_name;
    public sg_shader_desc desc;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_pipeline_t
{
    public sg_pipeline res_id;
    public sgimgui_str_t label;
    public sg_pipeline_desc desc;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_view_t
{
    public sg_view res_id;
    public float ui_scale;
    public sgimgui_str_t label;
    public sg_view_desc desc;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_buffer_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
    public sg_buffer sel_buf;
    public int num_slots;
    public sgimgui_buffer_t* slots;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_image_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
    public sg_image sel_img;
    public int num_slots;
    public sgimgui_image_t* slots;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_sampler_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
    public sg_sampler sel_smp;
    public int num_slots;
    public sgimgui_sampler_t* slots;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_shader_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
    public sg_shader sel_shd;
    public int num_slots;
    public sgimgui_shader_t* slots;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_pipeline_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
    public sg_pipeline sel_pip;
    public int num_slots;
    public sgimgui_pipeline_t* slots;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_view_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
    public sg_view sel_view;
    public int num_slots;
    public sgimgui_view_t* slots;
}
public enum sgimgui_cmd_t
{
    SGIMGUI_CMD_INVALID,
    SGIMGUI_CMD_RESET_STATE_CACHE,
    SGIMGUI_CMD_MAKE_BUFFER,
    SGIMGUI_CMD_MAKE_IMAGE,
    SGIMGUI_CMD_MAKE_SAMPLER,
    SGIMGUI_CMD_MAKE_SHADER,
    SGIMGUI_CMD_MAKE_PIPELINE,
    SGIMGUI_CMD_MAKE_VIEW,
    SGIMGUI_CMD_DESTROY_BUFFER,
    SGIMGUI_CMD_DESTROY_IMAGE,
    SGIMGUI_CMD_DESTROY_SAMPLER,
    SGIMGUI_CMD_DESTROY_SHADER,
    SGIMGUI_CMD_DESTROY_PIPELINE,
    SGIMGUI_CMD_DESTROY_VIEW,
    SGIMGUI_CMD_UPDATE_BUFFER,
    SGIMGUI_CMD_UPDATE_IMAGE,
    SGIMGUI_CMD_APPEND_BUFFER,
    SGIMGUI_CMD_BEGIN_PASS,
    SGIMGUI_CMD_APPLY_VIEWPORT,
    SGIMGUI_CMD_APPLY_SCISSOR_RECT,
    SGIMGUI_CMD_APPLY_PIPELINE,
    SGIMGUI_CMD_APPLY_BINDINGS,
    SGIMGUI_CMD_APPLY_UNIFORMS,
    SGIMGUI_CMD_DRAW,
    SGIMGUI_CMD_DRAW_EX,
    SGIMGUI_CMD_DISPATCH,
    SGIMGUI_CMD_END_PASS,
    SGIMGUI_CMD_COMMIT,
    SGIMGUI_CMD_ALLOC_BUFFER,
    SGIMGUI_CMD_ALLOC_IMAGE,
    SGIMGUI_CMD_ALLOC_SAMPLER,
    SGIMGUI_CMD_ALLOC_SHADER,
    SGIMGUI_CMD_ALLOC_PIPELINE,
    SGIMGUI_CMD_ALLOC_VIEW,
    SGIMGUI_CMD_DEALLOC_BUFFER,
    SGIMGUI_CMD_DEALLOC_IMAGE,
    SGIMGUI_CMD_DEALLOC_SAMPLER,
    SGIMGUI_CMD_DEALLOC_SHADER,
    SGIMGUI_CMD_DEALLOC_PIPELINE,
    SGIMGUI_CMD_DEALLOC_VIEW,
    SGIMGUI_CMD_INIT_BUFFER,
    SGIMGUI_CMD_INIT_IMAGE,
    SGIMGUI_CMD_INIT_SAMPLER,
    SGIMGUI_CMD_INIT_SHADER,
    SGIMGUI_CMD_INIT_PIPELINE,
    SGIMGUI_CMD_INIT_VIEW,
    SGIMGUI_CMD_UNINIT_BUFFER,
    SGIMGUI_CMD_UNINIT_IMAGE,
    SGIMGUI_CMD_UNINIT_SAMPLER,
    SGIMGUI_CMD_UNINIT_SHADER,
    SGIMGUI_CMD_UNINIT_PIPELINE,
    SGIMGUI_CMD_UNINIT_VIEW,
    SGIMGUI_CMD_FAIL_BUFFER,
    SGIMGUI_CMD_FAIL_IMAGE,
    SGIMGUI_CMD_FAIL_SAMPLER,
    SGIMGUI_CMD_FAIL_SHADER,
    SGIMGUI_CMD_FAIL_PIPELINE,
    SGIMGUI_CMD_FAIL_VIEW,
    SGIMGUI_CMD_PUSH_DEBUG_GROUP,
    SGIMGUI_CMD_POP_DEBUG_GROUP,
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_make_buffer_t
{
    public sg_buffer result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_make_image_t
{
    public sg_image result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_make_sampler_t
{
    public sg_sampler result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_make_shader_t
{
    public sg_shader result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_make_pipeline_t
{
    public sg_pipeline result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_make_view_t
{
    public sg_view result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_destroy_buffer_t
{
    public sg_buffer buffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_destroy_image_t
{
    public sg_image image;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_destroy_sampler_t
{
    public sg_sampler sampler;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_destroy_shader_t
{
    public sg_shader shader;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_destroy_pipeline_t
{
    public sg_pipeline pipeline;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_destroy_view_t
{
    public sg_view view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_update_buffer_t
{
    public sg_buffer buffer;
    public nuint data_size;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_update_image_t
{
    public sg_image image;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_append_buffer_t
{
    public sg_buffer buffer;
    public nuint data_size;
    public int result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_begin_pass_t
{
    public sg_pass pass;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_apply_viewport_t
{
    public int x;
    public int y;
    public int width;
    public int height;
#if WEB
    private byte _origin_top_left;
    public bool origin_top_left { get => _origin_top_left != 0; set => _origin_top_left = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool origin_top_left;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_apply_scissor_rect_t
{
    public int x;
    public int y;
    public int width;
    public int height;
#if WEB
    private byte _origin_top_left;
    public bool origin_top_left { get => _origin_top_left != 0; set => _origin_top_left = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool origin_top_left;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_apply_pipeline_t
{
    public sg_pipeline pipeline;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_apply_bindings_t
{
    public sg_bindings bindings;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_apply_uniforms_t
{
    public int ub_slot;
    public nuint data_size;
    public sg_pipeline pipeline;
    public nuint ubuf_pos;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_draw_t
{
    public int base_element;
    public int num_elements;
    public int num_instances;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_draw_ex_t
{
    public int base_element;
    public int num_elements;
    public int num_instances;
    public int base_vertex;
    public int base_instance;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_dispatch_t
{
    public int num_groups_x;
    public int num_groups_y;
    public int num_groups_z;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_alloc_buffer_t
{
    public sg_buffer result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_alloc_image_t
{
    public sg_image result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_alloc_sampler_t
{
    public sg_sampler result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_alloc_shader_t
{
    public sg_shader result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_alloc_pipeline_t
{
    public sg_pipeline result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_alloc_view_t
{
    public sg_view result;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_dealloc_buffer_t
{
    public sg_buffer buffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_dealloc_image_t
{
    public sg_image image;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_dealloc_sampler_t
{
    public sg_sampler sampler;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_dealloc_shader_t
{
    public sg_shader shader;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_dealloc_pipeline_t
{
    public sg_pipeline pipeline;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_dealloc_view_t
{
    public sg_view view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_init_buffer_t
{
    public sg_buffer buffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_init_image_t
{
    public sg_image image;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_init_sampler_t
{
    public sg_sampler sampler;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_init_shader_t
{
    public sg_shader shader;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_init_pipeline_t
{
    public sg_pipeline pipeline;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_init_view_t
{
    public sg_view view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_uninit_buffer_t
{
    public sg_buffer buffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_uninit_image_t
{
    public sg_image image;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_uninit_sampler_t
{
    public sg_sampler sampler;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_uninit_shader_t
{
    public sg_shader shader;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_uninit_pipeline_t
{
    public sg_pipeline pipeline;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_uninit_view_t
{
    public sg_view view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_fail_buffer_t
{
    public sg_buffer buffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_fail_image_t
{
    public sg_image image;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_fail_sampler_t
{
    public sg_sampler sampler;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_fail_shader_t
{
    public sg_shader shader;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_fail_pipeline_t
{
    public sg_pipeline pipeline;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_fail_view_t
{
    public sg_view view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_push_debug_group_t
{
    public sgimgui_str_t name;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_args_t
{
    public sgimgui_args_make_buffer_t make_buffer;
    public sgimgui_args_make_image_t make_image;
    public sgimgui_args_make_sampler_t make_sampler;
    public sgimgui_args_make_shader_t make_shader;
    public sgimgui_args_make_pipeline_t make_pipeline;
    public sgimgui_args_make_view_t make_view;
    public sgimgui_args_destroy_buffer_t destroy_buffer;
    public sgimgui_args_destroy_image_t destroy_image;
    public sgimgui_args_destroy_sampler_t destroy_sampler;
    public sgimgui_args_destroy_shader_t destroy_shader;
    public sgimgui_args_destroy_pipeline_t destroy_pipeline;
    public sgimgui_args_destroy_view_t destroy_view;
    public sgimgui_args_update_buffer_t update_buffer;
    public sgimgui_args_update_image_t update_image;
    public sgimgui_args_append_buffer_t append_buffer;
    public sgimgui_args_begin_pass_t begin_pass;
    public sgimgui_args_apply_viewport_t apply_viewport;
    public sgimgui_args_apply_scissor_rect_t apply_scissor_rect;
    public sgimgui_args_apply_pipeline_t apply_pipeline;
    public sgimgui_args_apply_bindings_t apply_bindings;
    public sgimgui_args_apply_uniforms_t apply_uniforms;
    public sgimgui_args_draw_t draw;
    public sgimgui_args_draw_ex_t draw_ex;
    public sgimgui_args_dispatch_t dispatch;
    public sgimgui_args_alloc_buffer_t alloc_buffer;
    public sgimgui_args_alloc_image_t alloc_image;
    public sgimgui_args_alloc_sampler_t alloc_sampler;
    public sgimgui_args_alloc_shader_t alloc_shader;
    public sgimgui_args_alloc_pipeline_t alloc_pipeline;
    public sgimgui_args_alloc_view_t alloc_view;
    public sgimgui_args_dealloc_buffer_t dealloc_buffer;
    public sgimgui_args_dealloc_image_t dealloc_image;
    public sgimgui_args_dealloc_sampler_t dealloc_sampler;
    public sgimgui_args_dealloc_shader_t dealloc_shader;
    public sgimgui_args_dealloc_pipeline_t dealloc_pipeline;
    public sgimgui_args_dealloc_view_t dealloc_view;
    public sgimgui_args_init_buffer_t init_buffer;
    public sgimgui_args_init_image_t init_image;
    public sgimgui_args_init_sampler_t init_sampler;
    public sgimgui_args_init_shader_t init_shader;
    public sgimgui_args_init_pipeline_t init_pipeline;
    public sgimgui_args_init_view_t init_view;
    public sgimgui_args_uninit_buffer_t uninit_buffer;
    public sgimgui_args_uninit_image_t uninit_image;
    public sgimgui_args_uninit_sampler_t uninit_sampler;
    public sgimgui_args_uninit_shader_t uninit_shader;
    public sgimgui_args_uninit_pipeline_t uninit_pipeline;
    public sgimgui_args_uninit_view_t uninit_view;
    public sgimgui_args_fail_buffer_t fail_buffer;
    public sgimgui_args_fail_image_t fail_image;
    public sgimgui_args_fail_sampler_t fail_sampler;
    public sgimgui_args_fail_shader_t fail_shader;
    public sgimgui_args_fail_pipeline_t fail_pipeline;
    public sgimgui_args_fail_view_t fail_view;
    public sgimgui_args_push_debug_group_t push_debug_group;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_capture_item_t
{
    public sgimgui_cmd_t cmd;
    public uint color;
    public sgimgui_args_t args;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_capture_bucket_t
{
    public nuint ubuf_size;
    public nuint ubuf_pos;
    public byte* ubuf;
    public int num_items;
    #pragma warning disable 169
    public struct itemsCollection
    {
        public ref sgimgui_capture_item_t this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 4096)[index];
        private sgimgui_capture_item_t _item0;
        private sgimgui_capture_item_t _item1;
        private sgimgui_capture_item_t _item2;
        private sgimgui_capture_item_t _item3;
        private sgimgui_capture_item_t _item4;
        private sgimgui_capture_item_t _item5;
        private sgimgui_capture_item_t _item6;
        private sgimgui_capture_item_t _item7;
        private sgimgui_capture_item_t _item8;
        private sgimgui_capture_item_t _item9;
        private sgimgui_capture_item_t _item10;
        private sgimgui_capture_item_t _item11;
        private sgimgui_capture_item_t _item12;
        private sgimgui_capture_item_t _item13;
        private sgimgui_capture_item_t _item14;
        private sgimgui_capture_item_t _item15;
        private sgimgui_capture_item_t _item16;
        private sgimgui_capture_item_t _item17;
        private sgimgui_capture_item_t _item18;
        private sgimgui_capture_item_t _item19;
        private sgimgui_capture_item_t _item20;
        private sgimgui_capture_item_t _item21;
        private sgimgui_capture_item_t _item22;
        private sgimgui_capture_item_t _item23;
        private sgimgui_capture_item_t _item24;
        private sgimgui_capture_item_t _item25;
        private sgimgui_capture_item_t _item26;
        private sgimgui_capture_item_t _item27;
        private sgimgui_capture_item_t _item28;
        private sgimgui_capture_item_t _item29;
        private sgimgui_capture_item_t _item30;
        private sgimgui_capture_item_t _item31;
        private sgimgui_capture_item_t _item32;
        private sgimgui_capture_item_t _item33;
        private sgimgui_capture_item_t _item34;
        private sgimgui_capture_item_t _item35;
        private sgimgui_capture_item_t _item36;
        private sgimgui_capture_item_t _item37;
        private sgimgui_capture_item_t _item38;
        private sgimgui_capture_item_t _item39;
        private sgimgui_capture_item_t _item40;
        private sgimgui_capture_item_t _item41;
        private sgimgui_capture_item_t _item42;
        private sgimgui_capture_item_t _item43;
        private sgimgui_capture_item_t _item44;
        private sgimgui_capture_item_t _item45;
        private sgimgui_capture_item_t _item46;
        private sgimgui_capture_item_t _item47;
        private sgimgui_capture_item_t _item48;
        private sgimgui_capture_item_t _item49;
        private sgimgui_capture_item_t _item50;
        private sgimgui_capture_item_t _item51;
        private sgimgui_capture_item_t _item52;
        private sgimgui_capture_item_t _item53;
        private sgimgui_capture_item_t _item54;
        private sgimgui_capture_item_t _item55;
        private sgimgui_capture_item_t _item56;
        private sgimgui_capture_item_t _item57;
        private sgimgui_capture_item_t _item58;
        private sgimgui_capture_item_t _item59;
        private sgimgui_capture_item_t _item60;
        private sgimgui_capture_item_t _item61;
        private sgimgui_capture_item_t _item62;
        private sgimgui_capture_item_t _item63;
        private sgimgui_capture_item_t _item64;
        private sgimgui_capture_item_t _item65;
        private sgimgui_capture_item_t _item66;
        private sgimgui_capture_item_t _item67;
        private sgimgui_capture_item_t _item68;
        private sgimgui_capture_item_t _item69;
        private sgimgui_capture_item_t _item70;
        private sgimgui_capture_item_t _item71;
        private sgimgui_capture_item_t _item72;
        private sgimgui_capture_item_t _item73;
        private sgimgui_capture_item_t _item74;
        private sgimgui_capture_item_t _item75;
        private sgimgui_capture_item_t _item76;
        private sgimgui_capture_item_t _item77;
        private sgimgui_capture_item_t _item78;
        private sgimgui_capture_item_t _item79;
        private sgimgui_capture_item_t _item80;
        private sgimgui_capture_item_t _item81;
        private sgimgui_capture_item_t _item82;
        private sgimgui_capture_item_t _item83;
        private sgimgui_capture_item_t _item84;
        private sgimgui_capture_item_t _item85;
        private sgimgui_capture_item_t _item86;
        private sgimgui_capture_item_t _item87;
        private sgimgui_capture_item_t _item88;
        private sgimgui_capture_item_t _item89;
        private sgimgui_capture_item_t _item90;
        private sgimgui_capture_item_t _item91;
        private sgimgui_capture_item_t _item92;
        private sgimgui_capture_item_t _item93;
        private sgimgui_capture_item_t _item94;
        private sgimgui_capture_item_t _item95;
        private sgimgui_capture_item_t _item96;
        private sgimgui_capture_item_t _item97;
        private sgimgui_capture_item_t _item98;
        private sgimgui_capture_item_t _item99;
        private sgimgui_capture_item_t _item100;
        private sgimgui_capture_item_t _item101;
        private sgimgui_capture_item_t _item102;
        private sgimgui_capture_item_t _item103;
        private sgimgui_capture_item_t _item104;
        private sgimgui_capture_item_t _item105;
        private sgimgui_capture_item_t _item106;
        private sgimgui_capture_item_t _item107;
        private sgimgui_capture_item_t _item108;
        private sgimgui_capture_item_t _item109;
        private sgimgui_capture_item_t _item110;
        private sgimgui_capture_item_t _item111;
        private sgimgui_capture_item_t _item112;
        private sgimgui_capture_item_t _item113;
        private sgimgui_capture_item_t _item114;
        private sgimgui_capture_item_t _item115;
        private sgimgui_capture_item_t _item116;
        private sgimgui_capture_item_t _item117;
        private sgimgui_capture_item_t _item118;
        private sgimgui_capture_item_t _item119;
        private sgimgui_capture_item_t _item120;
        private sgimgui_capture_item_t _item121;
        private sgimgui_capture_item_t _item122;
        private sgimgui_capture_item_t _item123;
        private sgimgui_capture_item_t _item124;
        private sgimgui_capture_item_t _item125;
        private sgimgui_capture_item_t _item126;
        private sgimgui_capture_item_t _item127;
        private sgimgui_capture_item_t _item128;
        private sgimgui_capture_item_t _item129;
        private sgimgui_capture_item_t _item130;
        private sgimgui_capture_item_t _item131;
        private sgimgui_capture_item_t _item132;
        private sgimgui_capture_item_t _item133;
        private sgimgui_capture_item_t _item134;
        private sgimgui_capture_item_t _item135;
        private sgimgui_capture_item_t _item136;
        private sgimgui_capture_item_t _item137;
        private sgimgui_capture_item_t _item138;
        private sgimgui_capture_item_t _item139;
        private sgimgui_capture_item_t _item140;
        private sgimgui_capture_item_t _item141;
        private sgimgui_capture_item_t _item142;
        private sgimgui_capture_item_t _item143;
        private sgimgui_capture_item_t _item144;
        private sgimgui_capture_item_t _item145;
        private sgimgui_capture_item_t _item146;
        private sgimgui_capture_item_t _item147;
        private sgimgui_capture_item_t _item148;
        private sgimgui_capture_item_t _item149;
        private sgimgui_capture_item_t _item150;
        private sgimgui_capture_item_t _item151;
        private sgimgui_capture_item_t _item152;
        private sgimgui_capture_item_t _item153;
        private sgimgui_capture_item_t _item154;
        private sgimgui_capture_item_t _item155;
        private sgimgui_capture_item_t _item156;
        private sgimgui_capture_item_t _item157;
        private sgimgui_capture_item_t _item158;
        private sgimgui_capture_item_t _item159;
        private sgimgui_capture_item_t _item160;
        private sgimgui_capture_item_t _item161;
        private sgimgui_capture_item_t _item162;
        private sgimgui_capture_item_t _item163;
        private sgimgui_capture_item_t _item164;
        private sgimgui_capture_item_t _item165;
        private sgimgui_capture_item_t _item166;
        private sgimgui_capture_item_t _item167;
        private sgimgui_capture_item_t _item168;
        private sgimgui_capture_item_t _item169;
        private sgimgui_capture_item_t _item170;
        private sgimgui_capture_item_t _item171;
        private sgimgui_capture_item_t _item172;
        private sgimgui_capture_item_t _item173;
        private sgimgui_capture_item_t _item174;
        private sgimgui_capture_item_t _item175;
        private sgimgui_capture_item_t _item176;
        private sgimgui_capture_item_t _item177;
        private sgimgui_capture_item_t _item178;
        private sgimgui_capture_item_t _item179;
        private sgimgui_capture_item_t _item180;
        private sgimgui_capture_item_t _item181;
        private sgimgui_capture_item_t _item182;
        private sgimgui_capture_item_t _item183;
        private sgimgui_capture_item_t _item184;
        private sgimgui_capture_item_t _item185;
        private sgimgui_capture_item_t _item186;
        private sgimgui_capture_item_t _item187;
        private sgimgui_capture_item_t _item188;
        private sgimgui_capture_item_t _item189;
        private sgimgui_capture_item_t _item190;
        private sgimgui_capture_item_t _item191;
        private sgimgui_capture_item_t _item192;
        private sgimgui_capture_item_t _item193;
        private sgimgui_capture_item_t _item194;
        private sgimgui_capture_item_t _item195;
        private sgimgui_capture_item_t _item196;
        private sgimgui_capture_item_t _item197;
        private sgimgui_capture_item_t _item198;
        private sgimgui_capture_item_t _item199;
        private sgimgui_capture_item_t _item200;
        private sgimgui_capture_item_t _item201;
        private sgimgui_capture_item_t _item202;
        private sgimgui_capture_item_t _item203;
        private sgimgui_capture_item_t _item204;
        private sgimgui_capture_item_t _item205;
        private sgimgui_capture_item_t _item206;
        private sgimgui_capture_item_t _item207;
        private sgimgui_capture_item_t _item208;
        private sgimgui_capture_item_t _item209;
        private sgimgui_capture_item_t _item210;
        private sgimgui_capture_item_t _item211;
        private sgimgui_capture_item_t _item212;
        private sgimgui_capture_item_t _item213;
        private sgimgui_capture_item_t _item214;
        private sgimgui_capture_item_t _item215;
        private sgimgui_capture_item_t _item216;
        private sgimgui_capture_item_t _item217;
        private sgimgui_capture_item_t _item218;
        private sgimgui_capture_item_t _item219;
        private sgimgui_capture_item_t _item220;
        private sgimgui_capture_item_t _item221;
        private sgimgui_capture_item_t _item222;
        private sgimgui_capture_item_t _item223;
        private sgimgui_capture_item_t _item224;
        private sgimgui_capture_item_t _item225;
        private sgimgui_capture_item_t _item226;
        private sgimgui_capture_item_t _item227;
        private sgimgui_capture_item_t _item228;
        private sgimgui_capture_item_t _item229;
        private sgimgui_capture_item_t _item230;
        private sgimgui_capture_item_t _item231;
        private sgimgui_capture_item_t _item232;
        private sgimgui_capture_item_t _item233;
        private sgimgui_capture_item_t _item234;
        private sgimgui_capture_item_t _item235;
        private sgimgui_capture_item_t _item236;
        private sgimgui_capture_item_t _item237;
        private sgimgui_capture_item_t _item238;
        private sgimgui_capture_item_t _item239;
        private sgimgui_capture_item_t _item240;
        private sgimgui_capture_item_t _item241;
        private sgimgui_capture_item_t _item242;
        private sgimgui_capture_item_t _item243;
        private sgimgui_capture_item_t _item244;
        private sgimgui_capture_item_t _item245;
        private sgimgui_capture_item_t _item246;
        private sgimgui_capture_item_t _item247;
        private sgimgui_capture_item_t _item248;
        private sgimgui_capture_item_t _item249;
        private sgimgui_capture_item_t _item250;
        private sgimgui_capture_item_t _item251;
        private sgimgui_capture_item_t _item252;
        private sgimgui_capture_item_t _item253;
        private sgimgui_capture_item_t _item254;
        private sgimgui_capture_item_t _item255;
        private sgimgui_capture_item_t _item256;
        private sgimgui_capture_item_t _item257;
        private sgimgui_capture_item_t _item258;
        private sgimgui_capture_item_t _item259;
        private sgimgui_capture_item_t _item260;
        private sgimgui_capture_item_t _item261;
        private sgimgui_capture_item_t _item262;
        private sgimgui_capture_item_t _item263;
        private sgimgui_capture_item_t _item264;
        private sgimgui_capture_item_t _item265;
        private sgimgui_capture_item_t _item266;
        private sgimgui_capture_item_t _item267;
        private sgimgui_capture_item_t _item268;
        private sgimgui_capture_item_t _item269;
        private sgimgui_capture_item_t _item270;
        private sgimgui_capture_item_t _item271;
        private sgimgui_capture_item_t _item272;
        private sgimgui_capture_item_t _item273;
        private sgimgui_capture_item_t _item274;
        private sgimgui_capture_item_t _item275;
        private sgimgui_capture_item_t _item276;
        private sgimgui_capture_item_t _item277;
        private sgimgui_capture_item_t _item278;
        private sgimgui_capture_item_t _item279;
        private sgimgui_capture_item_t _item280;
        private sgimgui_capture_item_t _item281;
        private sgimgui_capture_item_t _item282;
        private sgimgui_capture_item_t _item283;
        private sgimgui_capture_item_t _item284;
        private sgimgui_capture_item_t _item285;
        private sgimgui_capture_item_t _item286;
        private sgimgui_capture_item_t _item287;
        private sgimgui_capture_item_t _item288;
        private sgimgui_capture_item_t _item289;
        private sgimgui_capture_item_t _item290;
        private sgimgui_capture_item_t _item291;
        private sgimgui_capture_item_t _item292;
        private sgimgui_capture_item_t _item293;
        private sgimgui_capture_item_t _item294;
        private sgimgui_capture_item_t _item295;
        private sgimgui_capture_item_t _item296;
        private sgimgui_capture_item_t _item297;
        private sgimgui_capture_item_t _item298;
        private sgimgui_capture_item_t _item299;
        private sgimgui_capture_item_t _item300;
        private sgimgui_capture_item_t _item301;
        private sgimgui_capture_item_t _item302;
        private sgimgui_capture_item_t _item303;
        private sgimgui_capture_item_t _item304;
        private sgimgui_capture_item_t _item305;
        private sgimgui_capture_item_t _item306;
        private sgimgui_capture_item_t _item307;
        private sgimgui_capture_item_t _item308;
        private sgimgui_capture_item_t _item309;
        private sgimgui_capture_item_t _item310;
        private sgimgui_capture_item_t _item311;
        private sgimgui_capture_item_t _item312;
        private sgimgui_capture_item_t _item313;
        private sgimgui_capture_item_t _item314;
        private sgimgui_capture_item_t _item315;
        private sgimgui_capture_item_t _item316;
        private sgimgui_capture_item_t _item317;
        private sgimgui_capture_item_t _item318;
        private sgimgui_capture_item_t _item319;
        private sgimgui_capture_item_t _item320;
        private sgimgui_capture_item_t _item321;
        private sgimgui_capture_item_t _item322;
        private sgimgui_capture_item_t _item323;
        private sgimgui_capture_item_t _item324;
        private sgimgui_capture_item_t _item325;
        private sgimgui_capture_item_t _item326;
        private sgimgui_capture_item_t _item327;
        private sgimgui_capture_item_t _item328;
        private sgimgui_capture_item_t _item329;
        private sgimgui_capture_item_t _item330;
        private sgimgui_capture_item_t _item331;
        private sgimgui_capture_item_t _item332;
        private sgimgui_capture_item_t _item333;
        private sgimgui_capture_item_t _item334;
        private sgimgui_capture_item_t _item335;
        private sgimgui_capture_item_t _item336;
        private sgimgui_capture_item_t _item337;
        private sgimgui_capture_item_t _item338;
        private sgimgui_capture_item_t _item339;
        private sgimgui_capture_item_t _item340;
        private sgimgui_capture_item_t _item341;
        private sgimgui_capture_item_t _item342;
        private sgimgui_capture_item_t _item343;
        private sgimgui_capture_item_t _item344;
        private sgimgui_capture_item_t _item345;
        private sgimgui_capture_item_t _item346;
        private sgimgui_capture_item_t _item347;
        private sgimgui_capture_item_t _item348;
        private sgimgui_capture_item_t _item349;
        private sgimgui_capture_item_t _item350;
        private sgimgui_capture_item_t _item351;
        private sgimgui_capture_item_t _item352;
        private sgimgui_capture_item_t _item353;
        private sgimgui_capture_item_t _item354;
        private sgimgui_capture_item_t _item355;
        private sgimgui_capture_item_t _item356;
        private sgimgui_capture_item_t _item357;
        private sgimgui_capture_item_t _item358;
        private sgimgui_capture_item_t _item359;
        private sgimgui_capture_item_t _item360;
        private sgimgui_capture_item_t _item361;
        private sgimgui_capture_item_t _item362;
        private sgimgui_capture_item_t _item363;
        private sgimgui_capture_item_t _item364;
        private sgimgui_capture_item_t _item365;
        private sgimgui_capture_item_t _item366;
        private sgimgui_capture_item_t _item367;
        private sgimgui_capture_item_t _item368;
        private sgimgui_capture_item_t _item369;
        private sgimgui_capture_item_t _item370;
        private sgimgui_capture_item_t _item371;
        private sgimgui_capture_item_t _item372;
        private sgimgui_capture_item_t _item373;
        private sgimgui_capture_item_t _item374;
        private sgimgui_capture_item_t _item375;
        private sgimgui_capture_item_t _item376;
        private sgimgui_capture_item_t _item377;
        private sgimgui_capture_item_t _item378;
        private sgimgui_capture_item_t _item379;
        private sgimgui_capture_item_t _item380;
        private sgimgui_capture_item_t _item381;
        private sgimgui_capture_item_t _item382;
        private sgimgui_capture_item_t _item383;
        private sgimgui_capture_item_t _item384;
        private sgimgui_capture_item_t _item385;
        private sgimgui_capture_item_t _item386;
        private sgimgui_capture_item_t _item387;
        private sgimgui_capture_item_t _item388;
        private sgimgui_capture_item_t _item389;
        private sgimgui_capture_item_t _item390;
        private sgimgui_capture_item_t _item391;
        private sgimgui_capture_item_t _item392;
        private sgimgui_capture_item_t _item393;
        private sgimgui_capture_item_t _item394;
        private sgimgui_capture_item_t _item395;
        private sgimgui_capture_item_t _item396;
        private sgimgui_capture_item_t _item397;
        private sgimgui_capture_item_t _item398;
        private sgimgui_capture_item_t _item399;
        private sgimgui_capture_item_t _item400;
        private sgimgui_capture_item_t _item401;
        private sgimgui_capture_item_t _item402;
        private sgimgui_capture_item_t _item403;
        private sgimgui_capture_item_t _item404;
        private sgimgui_capture_item_t _item405;
        private sgimgui_capture_item_t _item406;
        private sgimgui_capture_item_t _item407;
        private sgimgui_capture_item_t _item408;
        private sgimgui_capture_item_t _item409;
        private sgimgui_capture_item_t _item410;
        private sgimgui_capture_item_t _item411;
        private sgimgui_capture_item_t _item412;
        private sgimgui_capture_item_t _item413;
        private sgimgui_capture_item_t _item414;
        private sgimgui_capture_item_t _item415;
        private sgimgui_capture_item_t _item416;
        private sgimgui_capture_item_t _item417;
        private sgimgui_capture_item_t _item418;
        private sgimgui_capture_item_t _item419;
        private sgimgui_capture_item_t _item420;
        private sgimgui_capture_item_t _item421;
        private sgimgui_capture_item_t _item422;
        private sgimgui_capture_item_t _item423;
        private sgimgui_capture_item_t _item424;
        private sgimgui_capture_item_t _item425;
        private sgimgui_capture_item_t _item426;
        private sgimgui_capture_item_t _item427;
        private sgimgui_capture_item_t _item428;
        private sgimgui_capture_item_t _item429;
        private sgimgui_capture_item_t _item430;
        private sgimgui_capture_item_t _item431;
        private sgimgui_capture_item_t _item432;
        private sgimgui_capture_item_t _item433;
        private sgimgui_capture_item_t _item434;
        private sgimgui_capture_item_t _item435;
        private sgimgui_capture_item_t _item436;
        private sgimgui_capture_item_t _item437;
        private sgimgui_capture_item_t _item438;
        private sgimgui_capture_item_t _item439;
        private sgimgui_capture_item_t _item440;
        private sgimgui_capture_item_t _item441;
        private sgimgui_capture_item_t _item442;
        private sgimgui_capture_item_t _item443;
        private sgimgui_capture_item_t _item444;
        private sgimgui_capture_item_t _item445;
        private sgimgui_capture_item_t _item446;
        private sgimgui_capture_item_t _item447;
        private sgimgui_capture_item_t _item448;
        private sgimgui_capture_item_t _item449;
        private sgimgui_capture_item_t _item450;
        private sgimgui_capture_item_t _item451;
        private sgimgui_capture_item_t _item452;
        private sgimgui_capture_item_t _item453;
        private sgimgui_capture_item_t _item454;
        private sgimgui_capture_item_t _item455;
        private sgimgui_capture_item_t _item456;
        private sgimgui_capture_item_t _item457;
        private sgimgui_capture_item_t _item458;
        private sgimgui_capture_item_t _item459;
        private sgimgui_capture_item_t _item460;
        private sgimgui_capture_item_t _item461;
        private sgimgui_capture_item_t _item462;
        private sgimgui_capture_item_t _item463;
        private sgimgui_capture_item_t _item464;
        private sgimgui_capture_item_t _item465;
        private sgimgui_capture_item_t _item466;
        private sgimgui_capture_item_t _item467;
        private sgimgui_capture_item_t _item468;
        private sgimgui_capture_item_t _item469;
        private sgimgui_capture_item_t _item470;
        private sgimgui_capture_item_t _item471;
        private sgimgui_capture_item_t _item472;
        private sgimgui_capture_item_t _item473;
        private sgimgui_capture_item_t _item474;
        private sgimgui_capture_item_t _item475;
        private sgimgui_capture_item_t _item476;
        private sgimgui_capture_item_t _item477;
        private sgimgui_capture_item_t _item478;
        private sgimgui_capture_item_t _item479;
        private sgimgui_capture_item_t _item480;
        private sgimgui_capture_item_t _item481;
        private sgimgui_capture_item_t _item482;
        private sgimgui_capture_item_t _item483;
        private sgimgui_capture_item_t _item484;
        private sgimgui_capture_item_t _item485;
        private sgimgui_capture_item_t _item486;
        private sgimgui_capture_item_t _item487;
        private sgimgui_capture_item_t _item488;
        private sgimgui_capture_item_t _item489;
        private sgimgui_capture_item_t _item490;
        private sgimgui_capture_item_t _item491;
        private sgimgui_capture_item_t _item492;
        private sgimgui_capture_item_t _item493;
        private sgimgui_capture_item_t _item494;
        private sgimgui_capture_item_t _item495;
        private sgimgui_capture_item_t _item496;
        private sgimgui_capture_item_t _item497;
        private sgimgui_capture_item_t _item498;
        private sgimgui_capture_item_t _item499;
        private sgimgui_capture_item_t _item500;
        private sgimgui_capture_item_t _item501;
        private sgimgui_capture_item_t _item502;
        private sgimgui_capture_item_t _item503;
        private sgimgui_capture_item_t _item504;
        private sgimgui_capture_item_t _item505;
        private sgimgui_capture_item_t _item506;
        private sgimgui_capture_item_t _item507;
        private sgimgui_capture_item_t _item508;
        private sgimgui_capture_item_t _item509;
        private sgimgui_capture_item_t _item510;
        private sgimgui_capture_item_t _item511;
        private sgimgui_capture_item_t _item512;
        private sgimgui_capture_item_t _item513;
        private sgimgui_capture_item_t _item514;
        private sgimgui_capture_item_t _item515;
        private sgimgui_capture_item_t _item516;
        private sgimgui_capture_item_t _item517;
        private sgimgui_capture_item_t _item518;
        private sgimgui_capture_item_t _item519;
        private sgimgui_capture_item_t _item520;
        private sgimgui_capture_item_t _item521;
        private sgimgui_capture_item_t _item522;
        private sgimgui_capture_item_t _item523;
        private sgimgui_capture_item_t _item524;
        private sgimgui_capture_item_t _item525;
        private sgimgui_capture_item_t _item526;
        private sgimgui_capture_item_t _item527;
        private sgimgui_capture_item_t _item528;
        private sgimgui_capture_item_t _item529;
        private sgimgui_capture_item_t _item530;
        private sgimgui_capture_item_t _item531;
        private sgimgui_capture_item_t _item532;
        private sgimgui_capture_item_t _item533;
        private sgimgui_capture_item_t _item534;
        private sgimgui_capture_item_t _item535;
        private sgimgui_capture_item_t _item536;
        private sgimgui_capture_item_t _item537;
        private sgimgui_capture_item_t _item538;
        private sgimgui_capture_item_t _item539;
        private sgimgui_capture_item_t _item540;
        private sgimgui_capture_item_t _item541;
        private sgimgui_capture_item_t _item542;
        private sgimgui_capture_item_t _item543;
        private sgimgui_capture_item_t _item544;
        private sgimgui_capture_item_t _item545;
        private sgimgui_capture_item_t _item546;
        private sgimgui_capture_item_t _item547;
        private sgimgui_capture_item_t _item548;
        private sgimgui_capture_item_t _item549;
        private sgimgui_capture_item_t _item550;
        private sgimgui_capture_item_t _item551;
        private sgimgui_capture_item_t _item552;
        private sgimgui_capture_item_t _item553;
        private sgimgui_capture_item_t _item554;
        private sgimgui_capture_item_t _item555;
        private sgimgui_capture_item_t _item556;
        private sgimgui_capture_item_t _item557;
        private sgimgui_capture_item_t _item558;
        private sgimgui_capture_item_t _item559;
        private sgimgui_capture_item_t _item560;
        private sgimgui_capture_item_t _item561;
        private sgimgui_capture_item_t _item562;
        private sgimgui_capture_item_t _item563;
        private sgimgui_capture_item_t _item564;
        private sgimgui_capture_item_t _item565;
        private sgimgui_capture_item_t _item566;
        private sgimgui_capture_item_t _item567;
        private sgimgui_capture_item_t _item568;
        private sgimgui_capture_item_t _item569;
        private sgimgui_capture_item_t _item570;
        private sgimgui_capture_item_t _item571;
        private sgimgui_capture_item_t _item572;
        private sgimgui_capture_item_t _item573;
        private sgimgui_capture_item_t _item574;
        private sgimgui_capture_item_t _item575;
        private sgimgui_capture_item_t _item576;
        private sgimgui_capture_item_t _item577;
        private sgimgui_capture_item_t _item578;
        private sgimgui_capture_item_t _item579;
        private sgimgui_capture_item_t _item580;
        private sgimgui_capture_item_t _item581;
        private sgimgui_capture_item_t _item582;
        private sgimgui_capture_item_t _item583;
        private sgimgui_capture_item_t _item584;
        private sgimgui_capture_item_t _item585;
        private sgimgui_capture_item_t _item586;
        private sgimgui_capture_item_t _item587;
        private sgimgui_capture_item_t _item588;
        private sgimgui_capture_item_t _item589;
        private sgimgui_capture_item_t _item590;
        private sgimgui_capture_item_t _item591;
        private sgimgui_capture_item_t _item592;
        private sgimgui_capture_item_t _item593;
        private sgimgui_capture_item_t _item594;
        private sgimgui_capture_item_t _item595;
        private sgimgui_capture_item_t _item596;
        private sgimgui_capture_item_t _item597;
        private sgimgui_capture_item_t _item598;
        private sgimgui_capture_item_t _item599;
        private sgimgui_capture_item_t _item600;
        private sgimgui_capture_item_t _item601;
        private sgimgui_capture_item_t _item602;
        private sgimgui_capture_item_t _item603;
        private sgimgui_capture_item_t _item604;
        private sgimgui_capture_item_t _item605;
        private sgimgui_capture_item_t _item606;
        private sgimgui_capture_item_t _item607;
        private sgimgui_capture_item_t _item608;
        private sgimgui_capture_item_t _item609;
        private sgimgui_capture_item_t _item610;
        private sgimgui_capture_item_t _item611;
        private sgimgui_capture_item_t _item612;
        private sgimgui_capture_item_t _item613;
        private sgimgui_capture_item_t _item614;
        private sgimgui_capture_item_t _item615;
        private sgimgui_capture_item_t _item616;
        private sgimgui_capture_item_t _item617;
        private sgimgui_capture_item_t _item618;
        private sgimgui_capture_item_t _item619;
        private sgimgui_capture_item_t _item620;
        private sgimgui_capture_item_t _item621;
        private sgimgui_capture_item_t _item622;
        private sgimgui_capture_item_t _item623;
        private sgimgui_capture_item_t _item624;
        private sgimgui_capture_item_t _item625;
        private sgimgui_capture_item_t _item626;
        private sgimgui_capture_item_t _item627;
        private sgimgui_capture_item_t _item628;
        private sgimgui_capture_item_t _item629;
        private sgimgui_capture_item_t _item630;
        private sgimgui_capture_item_t _item631;
        private sgimgui_capture_item_t _item632;
        private sgimgui_capture_item_t _item633;
        private sgimgui_capture_item_t _item634;
        private sgimgui_capture_item_t _item635;
        private sgimgui_capture_item_t _item636;
        private sgimgui_capture_item_t _item637;
        private sgimgui_capture_item_t _item638;
        private sgimgui_capture_item_t _item639;
        private sgimgui_capture_item_t _item640;
        private sgimgui_capture_item_t _item641;
        private sgimgui_capture_item_t _item642;
        private sgimgui_capture_item_t _item643;
        private sgimgui_capture_item_t _item644;
        private sgimgui_capture_item_t _item645;
        private sgimgui_capture_item_t _item646;
        private sgimgui_capture_item_t _item647;
        private sgimgui_capture_item_t _item648;
        private sgimgui_capture_item_t _item649;
        private sgimgui_capture_item_t _item650;
        private sgimgui_capture_item_t _item651;
        private sgimgui_capture_item_t _item652;
        private sgimgui_capture_item_t _item653;
        private sgimgui_capture_item_t _item654;
        private sgimgui_capture_item_t _item655;
        private sgimgui_capture_item_t _item656;
        private sgimgui_capture_item_t _item657;
        private sgimgui_capture_item_t _item658;
        private sgimgui_capture_item_t _item659;
        private sgimgui_capture_item_t _item660;
        private sgimgui_capture_item_t _item661;
        private sgimgui_capture_item_t _item662;
        private sgimgui_capture_item_t _item663;
        private sgimgui_capture_item_t _item664;
        private sgimgui_capture_item_t _item665;
        private sgimgui_capture_item_t _item666;
        private sgimgui_capture_item_t _item667;
        private sgimgui_capture_item_t _item668;
        private sgimgui_capture_item_t _item669;
        private sgimgui_capture_item_t _item670;
        private sgimgui_capture_item_t _item671;
        private sgimgui_capture_item_t _item672;
        private sgimgui_capture_item_t _item673;
        private sgimgui_capture_item_t _item674;
        private sgimgui_capture_item_t _item675;
        private sgimgui_capture_item_t _item676;
        private sgimgui_capture_item_t _item677;
        private sgimgui_capture_item_t _item678;
        private sgimgui_capture_item_t _item679;
        private sgimgui_capture_item_t _item680;
        private sgimgui_capture_item_t _item681;
        private sgimgui_capture_item_t _item682;
        private sgimgui_capture_item_t _item683;
        private sgimgui_capture_item_t _item684;
        private sgimgui_capture_item_t _item685;
        private sgimgui_capture_item_t _item686;
        private sgimgui_capture_item_t _item687;
        private sgimgui_capture_item_t _item688;
        private sgimgui_capture_item_t _item689;
        private sgimgui_capture_item_t _item690;
        private sgimgui_capture_item_t _item691;
        private sgimgui_capture_item_t _item692;
        private sgimgui_capture_item_t _item693;
        private sgimgui_capture_item_t _item694;
        private sgimgui_capture_item_t _item695;
        private sgimgui_capture_item_t _item696;
        private sgimgui_capture_item_t _item697;
        private sgimgui_capture_item_t _item698;
        private sgimgui_capture_item_t _item699;
        private sgimgui_capture_item_t _item700;
        private sgimgui_capture_item_t _item701;
        private sgimgui_capture_item_t _item702;
        private sgimgui_capture_item_t _item703;
        private sgimgui_capture_item_t _item704;
        private sgimgui_capture_item_t _item705;
        private sgimgui_capture_item_t _item706;
        private sgimgui_capture_item_t _item707;
        private sgimgui_capture_item_t _item708;
        private sgimgui_capture_item_t _item709;
        private sgimgui_capture_item_t _item710;
        private sgimgui_capture_item_t _item711;
        private sgimgui_capture_item_t _item712;
        private sgimgui_capture_item_t _item713;
        private sgimgui_capture_item_t _item714;
        private sgimgui_capture_item_t _item715;
        private sgimgui_capture_item_t _item716;
        private sgimgui_capture_item_t _item717;
        private sgimgui_capture_item_t _item718;
        private sgimgui_capture_item_t _item719;
        private sgimgui_capture_item_t _item720;
        private sgimgui_capture_item_t _item721;
        private sgimgui_capture_item_t _item722;
        private sgimgui_capture_item_t _item723;
        private sgimgui_capture_item_t _item724;
        private sgimgui_capture_item_t _item725;
        private sgimgui_capture_item_t _item726;
        private sgimgui_capture_item_t _item727;
        private sgimgui_capture_item_t _item728;
        private sgimgui_capture_item_t _item729;
        private sgimgui_capture_item_t _item730;
        private sgimgui_capture_item_t _item731;
        private sgimgui_capture_item_t _item732;
        private sgimgui_capture_item_t _item733;
        private sgimgui_capture_item_t _item734;
        private sgimgui_capture_item_t _item735;
        private sgimgui_capture_item_t _item736;
        private sgimgui_capture_item_t _item737;
        private sgimgui_capture_item_t _item738;
        private sgimgui_capture_item_t _item739;
        private sgimgui_capture_item_t _item740;
        private sgimgui_capture_item_t _item741;
        private sgimgui_capture_item_t _item742;
        private sgimgui_capture_item_t _item743;
        private sgimgui_capture_item_t _item744;
        private sgimgui_capture_item_t _item745;
        private sgimgui_capture_item_t _item746;
        private sgimgui_capture_item_t _item747;
        private sgimgui_capture_item_t _item748;
        private sgimgui_capture_item_t _item749;
        private sgimgui_capture_item_t _item750;
        private sgimgui_capture_item_t _item751;
        private sgimgui_capture_item_t _item752;
        private sgimgui_capture_item_t _item753;
        private sgimgui_capture_item_t _item754;
        private sgimgui_capture_item_t _item755;
        private sgimgui_capture_item_t _item756;
        private sgimgui_capture_item_t _item757;
        private sgimgui_capture_item_t _item758;
        private sgimgui_capture_item_t _item759;
        private sgimgui_capture_item_t _item760;
        private sgimgui_capture_item_t _item761;
        private sgimgui_capture_item_t _item762;
        private sgimgui_capture_item_t _item763;
        private sgimgui_capture_item_t _item764;
        private sgimgui_capture_item_t _item765;
        private sgimgui_capture_item_t _item766;
        private sgimgui_capture_item_t _item767;
        private sgimgui_capture_item_t _item768;
        private sgimgui_capture_item_t _item769;
        private sgimgui_capture_item_t _item770;
        private sgimgui_capture_item_t _item771;
        private sgimgui_capture_item_t _item772;
        private sgimgui_capture_item_t _item773;
        private sgimgui_capture_item_t _item774;
        private sgimgui_capture_item_t _item775;
        private sgimgui_capture_item_t _item776;
        private sgimgui_capture_item_t _item777;
        private sgimgui_capture_item_t _item778;
        private sgimgui_capture_item_t _item779;
        private sgimgui_capture_item_t _item780;
        private sgimgui_capture_item_t _item781;
        private sgimgui_capture_item_t _item782;
        private sgimgui_capture_item_t _item783;
        private sgimgui_capture_item_t _item784;
        private sgimgui_capture_item_t _item785;
        private sgimgui_capture_item_t _item786;
        private sgimgui_capture_item_t _item787;
        private sgimgui_capture_item_t _item788;
        private sgimgui_capture_item_t _item789;
        private sgimgui_capture_item_t _item790;
        private sgimgui_capture_item_t _item791;
        private sgimgui_capture_item_t _item792;
        private sgimgui_capture_item_t _item793;
        private sgimgui_capture_item_t _item794;
        private sgimgui_capture_item_t _item795;
        private sgimgui_capture_item_t _item796;
        private sgimgui_capture_item_t _item797;
        private sgimgui_capture_item_t _item798;
        private sgimgui_capture_item_t _item799;
        private sgimgui_capture_item_t _item800;
        private sgimgui_capture_item_t _item801;
        private sgimgui_capture_item_t _item802;
        private sgimgui_capture_item_t _item803;
        private sgimgui_capture_item_t _item804;
        private sgimgui_capture_item_t _item805;
        private sgimgui_capture_item_t _item806;
        private sgimgui_capture_item_t _item807;
        private sgimgui_capture_item_t _item808;
        private sgimgui_capture_item_t _item809;
        private sgimgui_capture_item_t _item810;
        private sgimgui_capture_item_t _item811;
        private sgimgui_capture_item_t _item812;
        private sgimgui_capture_item_t _item813;
        private sgimgui_capture_item_t _item814;
        private sgimgui_capture_item_t _item815;
        private sgimgui_capture_item_t _item816;
        private sgimgui_capture_item_t _item817;
        private sgimgui_capture_item_t _item818;
        private sgimgui_capture_item_t _item819;
        private sgimgui_capture_item_t _item820;
        private sgimgui_capture_item_t _item821;
        private sgimgui_capture_item_t _item822;
        private sgimgui_capture_item_t _item823;
        private sgimgui_capture_item_t _item824;
        private sgimgui_capture_item_t _item825;
        private sgimgui_capture_item_t _item826;
        private sgimgui_capture_item_t _item827;
        private sgimgui_capture_item_t _item828;
        private sgimgui_capture_item_t _item829;
        private sgimgui_capture_item_t _item830;
        private sgimgui_capture_item_t _item831;
        private sgimgui_capture_item_t _item832;
        private sgimgui_capture_item_t _item833;
        private sgimgui_capture_item_t _item834;
        private sgimgui_capture_item_t _item835;
        private sgimgui_capture_item_t _item836;
        private sgimgui_capture_item_t _item837;
        private sgimgui_capture_item_t _item838;
        private sgimgui_capture_item_t _item839;
        private sgimgui_capture_item_t _item840;
        private sgimgui_capture_item_t _item841;
        private sgimgui_capture_item_t _item842;
        private sgimgui_capture_item_t _item843;
        private sgimgui_capture_item_t _item844;
        private sgimgui_capture_item_t _item845;
        private sgimgui_capture_item_t _item846;
        private sgimgui_capture_item_t _item847;
        private sgimgui_capture_item_t _item848;
        private sgimgui_capture_item_t _item849;
        private sgimgui_capture_item_t _item850;
        private sgimgui_capture_item_t _item851;
        private sgimgui_capture_item_t _item852;
        private sgimgui_capture_item_t _item853;
        private sgimgui_capture_item_t _item854;
        private sgimgui_capture_item_t _item855;
        private sgimgui_capture_item_t _item856;
        private sgimgui_capture_item_t _item857;
        private sgimgui_capture_item_t _item858;
        private sgimgui_capture_item_t _item859;
        private sgimgui_capture_item_t _item860;
        private sgimgui_capture_item_t _item861;
        private sgimgui_capture_item_t _item862;
        private sgimgui_capture_item_t _item863;
        private sgimgui_capture_item_t _item864;
        private sgimgui_capture_item_t _item865;
        private sgimgui_capture_item_t _item866;
        private sgimgui_capture_item_t _item867;
        private sgimgui_capture_item_t _item868;
        private sgimgui_capture_item_t _item869;
        private sgimgui_capture_item_t _item870;
        private sgimgui_capture_item_t _item871;
        private sgimgui_capture_item_t _item872;
        private sgimgui_capture_item_t _item873;
        private sgimgui_capture_item_t _item874;
        private sgimgui_capture_item_t _item875;
        private sgimgui_capture_item_t _item876;
        private sgimgui_capture_item_t _item877;
        private sgimgui_capture_item_t _item878;
        private sgimgui_capture_item_t _item879;
        private sgimgui_capture_item_t _item880;
        private sgimgui_capture_item_t _item881;
        private sgimgui_capture_item_t _item882;
        private sgimgui_capture_item_t _item883;
        private sgimgui_capture_item_t _item884;
        private sgimgui_capture_item_t _item885;
        private sgimgui_capture_item_t _item886;
        private sgimgui_capture_item_t _item887;
        private sgimgui_capture_item_t _item888;
        private sgimgui_capture_item_t _item889;
        private sgimgui_capture_item_t _item890;
        private sgimgui_capture_item_t _item891;
        private sgimgui_capture_item_t _item892;
        private sgimgui_capture_item_t _item893;
        private sgimgui_capture_item_t _item894;
        private sgimgui_capture_item_t _item895;
        private sgimgui_capture_item_t _item896;
        private sgimgui_capture_item_t _item897;
        private sgimgui_capture_item_t _item898;
        private sgimgui_capture_item_t _item899;
        private sgimgui_capture_item_t _item900;
        private sgimgui_capture_item_t _item901;
        private sgimgui_capture_item_t _item902;
        private sgimgui_capture_item_t _item903;
        private sgimgui_capture_item_t _item904;
        private sgimgui_capture_item_t _item905;
        private sgimgui_capture_item_t _item906;
        private sgimgui_capture_item_t _item907;
        private sgimgui_capture_item_t _item908;
        private sgimgui_capture_item_t _item909;
        private sgimgui_capture_item_t _item910;
        private sgimgui_capture_item_t _item911;
        private sgimgui_capture_item_t _item912;
        private sgimgui_capture_item_t _item913;
        private sgimgui_capture_item_t _item914;
        private sgimgui_capture_item_t _item915;
        private sgimgui_capture_item_t _item916;
        private sgimgui_capture_item_t _item917;
        private sgimgui_capture_item_t _item918;
        private sgimgui_capture_item_t _item919;
        private sgimgui_capture_item_t _item920;
        private sgimgui_capture_item_t _item921;
        private sgimgui_capture_item_t _item922;
        private sgimgui_capture_item_t _item923;
        private sgimgui_capture_item_t _item924;
        private sgimgui_capture_item_t _item925;
        private sgimgui_capture_item_t _item926;
        private sgimgui_capture_item_t _item927;
        private sgimgui_capture_item_t _item928;
        private sgimgui_capture_item_t _item929;
        private sgimgui_capture_item_t _item930;
        private sgimgui_capture_item_t _item931;
        private sgimgui_capture_item_t _item932;
        private sgimgui_capture_item_t _item933;
        private sgimgui_capture_item_t _item934;
        private sgimgui_capture_item_t _item935;
        private sgimgui_capture_item_t _item936;
        private sgimgui_capture_item_t _item937;
        private sgimgui_capture_item_t _item938;
        private sgimgui_capture_item_t _item939;
        private sgimgui_capture_item_t _item940;
        private sgimgui_capture_item_t _item941;
        private sgimgui_capture_item_t _item942;
        private sgimgui_capture_item_t _item943;
        private sgimgui_capture_item_t _item944;
        private sgimgui_capture_item_t _item945;
        private sgimgui_capture_item_t _item946;
        private sgimgui_capture_item_t _item947;
        private sgimgui_capture_item_t _item948;
        private sgimgui_capture_item_t _item949;
        private sgimgui_capture_item_t _item950;
        private sgimgui_capture_item_t _item951;
        private sgimgui_capture_item_t _item952;
        private sgimgui_capture_item_t _item953;
        private sgimgui_capture_item_t _item954;
        private sgimgui_capture_item_t _item955;
        private sgimgui_capture_item_t _item956;
        private sgimgui_capture_item_t _item957;
        private sgimgui_capture_item_t _item958;
        private sgimgui_capture_item_t _item959;
        private sgimgui_capture_item_t _item960;
        private sgimgui_capture_item_t _item961;
        private sgimgui_capture_item_t _item962;
        private sgimgui_capture_item_t _item963;
        private sgimgui_capture_item_t _item964;
        private sgimgui_capture_item_t _item965;
        private sgimgui_capture_item_t _item966;
        private sgimgui_capture_item_t _item967;
        private sgimgui_capture_item_t _item968;
        private sgimgui_capture_item_t _item969;
        private sgimgui_capture_item_t _item970;
        private sgimgui_capture_item_t _item971;
        private sgimgui_capture_item_t _item972;
        private sgimgui_capture_item_t _item973;
        private sgimgui_capture_item_t _item974;
        private sgimgui_capture_item_t _item975;
        private sgimgui_capture_item_t _item976;
        private sgimgui_capture_item_t _item977;
        private sgimgui_capture_item_t _item978;
        private sgimgui_capture_item_t _item979;
        private sgimgui_capture_item_t _item980;
        private sgimgui_capture_item_t _item981;
        private sgimgui_capture_item_t _item982;
        private sgimgui_capture_item_t _item983;
        private sgimgui_capture_item_t _item984;
        private sgimgui_capture_item_t _item985;
        private sgimgui_capture_item_t _item986;
        private sgimgui_capture_item_t _item987;
        private sgimgui_capture_item_t _item988;
        private sgimgui_capture_item_t _item989;
        private sgimgui_capture_item_t _item990;
        private sgimgui_capture_item_t _item991;
        private sgimgui_capture_item_t _item992;
        private sgimgui_capture_item_t _item993;
        private sgimgui_capture_item_t _item994;
        private sgimgui_capture_item_t _item995;
        private sgimgui_capture_item_t _item996;
        private sgimgui_capture_item_t _item997;
        private sgimgui_capture_item_t _item998;
        private sgimgui_capture_item_t _item999;
        private sgimgui_capture_item_t _item1000;
        private sgimgui_capture_item_t _item1001;
        private sgimgui_capture_item_t _item1002;
        private sgimgui_capture_item_t _item1003;
        private sgimgui_capture_item_t _item1004;
        private sgimgui_capture_item_t _item1005;
        private sgimgui_capture_item_t _item1006;
        private sgimgui_capture_item_t _item1007;
        private sgimgui_capture_item_t _item1008;
        private sgimgui_capture_item_t _item1009;
        private sgimgui_capture_item_t _item1010;
        private sgimgui_capture_item_t _item1011;
        private sgimgui_capture_item_t _item1012;
        private sgimgui_capture_item_t _item1013;
        private sgimgui_capture_item_t _item1014;
        private sgimgui_capture_item_t _item1015;
        private sgimgui_capture_item_t _item1016;
        private sgimgui_capture_item_t _item1017;
        private sgimgui_capture_item_t _item1018;
        private sgimgui_capture_item_t _item1019;
        private sgimgui_capture_item_t _item1020;
        private sgimgui_capture_item_t _item1021;
        private sgimgui_capture_item_t _item1022;
        private sgimgui_capture_item_t _item1023;
        private sgimgui_capture_item_t _item1024;
        private sgimgui_capture_item_t _item1025;
        private sgimgui_capture_item_t _item1026;
        private sgimgui_capture_item_t _item1027;
        private sgimgui_capture_item_t _item1028;
        private sgimgui_capture_item_t _item1029;
        private sgimgui_capture_item_t _item1030;
        private sgimgui_capture_item_t _item1031;
        private sgimgui_capture_item_t _item1032;
        private sgimgui_capture_item_t _item1033;
        private sgimgui_capture_item_t _item1034;
        private sgimgui_capture_item_t _item1035;
        private sgimgui_capture_item_t _item1036;
        private sgimgui_capture_item_t _item1037;
        private sgimgui_capture_item_t _item1038;
        private sgimgui_capture_item_t _item1039;
        private sgimgui_capture_item_t _item1040;
        private sgimgui_capture_item_t _item1041;
        private sgimgui_capture_item_t _item1042;
        private sgimgui_capture_item_t _item1043;
        private sgimgui_capture_item_t _item1044;
        private sgimgui_capture_item_t _item1045;
        private sgimgui_capture_item_t _item1046;
        private sgimgui_capture_item_t _item1047;
        private sgimgui_capture_item_t _item1048;
        private sgimgui_capture_item_t _item1049;
        private sgimgui_capture_item_t _item1050;
        private sgimgui_capture_item_t _item1051;
        private sgimgui_capture_item_t _item1052;
        private sgimgui_capture_item_t _item1053;
        private sgimgui_capture_item_t _item1054;
        private sgimgui_capture_item_t _item1055;
        private sgimgui_capture_item_t _item1056;
        private sgimgui_capture_item_t _item1057;
        private sgimgui_capture_item_t _item1058;
        private sgimgui_capture_item_t _item1059;
        private sgimgui_capture_item_t _item1060;
        private sgimgui_capture_item_t _item1061;
        private sgimgui_capture_item_t _item1062;
        private sgimgui_capture_item_t _item1063;
        private sgimgui_capture_item_t _item1064;
        private sgimgui_capture_item_t _item1065;
        private sgimgui_capture_item_t _item1066;
        private sgimgui_capture_item_t _item1067;
        private sgimgui_capture_item_t _item1068;
        private sgimgui_capture_item_t _item1069;
        private sgimgui_capture_item_t _item1070;
        private sgimgui_capture_item_t _item1071;
        private sgimgui_capture_item_t _item1072;
        private sgimgui_capture_item_t _item1073;
        private sgimgui_capture_item_t _item1074;
        private sgimgui_capture_item_t _item1075;
        private sgimgui_capture_item_t _item1076;
        private sgimgui_capture_item_t _item1077;
        private sgimgui_capture_item_t _item1078;
        private sgimgui_capture_item_t _item1079;
        private sgimgui_capture_item_t _item1080;
        private sgimgui_capture_item_t _item1081;
        private sgimgui_capture_item_t _item1082;
        private sgimgui_capture_item_t _item1083;
        private sgimgui_capture_item_t _item1084;
        private sgimgui_capture_item_t _item1085;
        private sgimgui_capture_item_t _item1086;
        private sgimgui_capture_item_t _item1087;
        private sgimgui_capture_item_t _item1088;
        private sgimgui_capture_item_t _item1089;
        private sgimgui_capture_item_t _item1090;
        private sgimgui_capture_item_t _item1091;
        private sgimgui_capture_item_t _item1092;
        private sgimgui_capture_item_t _item1093;
        private sgimgui_capture_item_t _item1094;
        private sgimgui_capture_item_t _item1095;
        private sgimgui_capture_item_t _item1096;
        private sgimgui_capture_item_t _item1097;
        private sgimgui_capture_item_t _item1098;
        private sgimgui_capture_item_t _item1099;
        private sgimgui_capture_item_t _item1100;
        private sgimgui_capture_item_t _item1101;
        private sgimgui_capture_item_t _item1102;
        private sgimgui_capture_item_t _item1103;
        private sgimgui_capture_item_t _item1104;
        private sgimgui_capture_item_t _item1105;
        private sgimgui_capture_item_t _item1106;
        private sgimgui_capture_item_t _item1107;
        private sgimgui_capture_item_t _item1108;
        private sgimgui_capture_item_t _item1109;
        private sgimgui_capture_item_t _item1110;
        private sgimgui_capture_item_t _item1111;
        private sgimgui_capture_item_t _item1112;
        private sgimgui_capture_item_t _item1113;
        private sgimgui_capture_item_t _item1114;
        private sgimgui_capture_item_t _item1115;
        private sgimgui_capture_item_t _item1116;
        private sgimgui_capture_item_t _item1117;
        private sgimgui_capture_item_t _item1118;
        private sgimgui_capture_item_t _item1119;
        private sgimgui_capture_item_t _item1120;
        private sgimgui_capture_item_t _item1121;
        private sgimgui_capture_item_t _item1122;
        private sgimgui_capture_item_t _item1123;
        private sgimgui_capture_item_t _item1124;
        private sgimgui_capture_item_t _item1125;
        private sgimgui_capture_item_t _item1126;
        private sgimgui_capture_item_t _item1127;
        private sgimgui_capture_item_t _item1128;
        private sgimgui_capture_item_t _item1129;
        private sgimgui_capture_item_t _item1130;
        private sgimgui_capture_item_t _item1131;
        private sgimgui_capture_item_t _item1132;
        private sgimgui_capture_item_t _item1133;
        private sgimgui_capture_item_t _item1134;
        private sgimgui_capture_item_t _item1135;
        private sgimgui_capture_item_t _item1136;
        private sgimgui_capture_item_t _item1137;
        private sgimgui_capture_item_t _item1138;
        private sgimgui_capture_item_t _item1139;
        private sgimgui_capture_item_t _item1140;
        private sgimgui_capture_item_t _item1141;
        private sgimgui_capture_item_t _item1142;
        private sgimgui_capture_item_t _item1143;
        private sgimgui_capture_item_t _item1144;
        private sgimgui_capture_item_t _item1145;
        private sgimgui_capture_item_t _item1146;
        private sgimgui_capture_item_t _item1147;
        private sgimgui_capture_item_t _item1148;
        private sgimgui_capture_item_t _item1149;
        private sgimgui_capture_item_t _item1150;
        private sgimgui_capture_item_t _item1151;
        private sgimgui_capture_item_t _item1152;
        private sgimgui_capture_item_t _item1153;
        private sgimgui_capture_item_t _item1154;
        private sgimgui_capture_item_t _item1155;
        private sgimgui_capture_item_t _item1156;
        private sgimgui_capture_item_t _item1157;
        private sgimgui_capture_item_t _item1158;
        private sgimgui_capture_item_t _item1159;
        private sgimgui_capture_item_t _item1160;
        private sgimgui_capture_item_t _item1161;
        private sgimgui_capture_item_t _item1162;
        private sgimgui_capture_item_t _item1163;
        private sgimgui_capture_item_t _item1164;
        private sgimgui_capture_item_t _item1165;
        private sgimgui_capture_item_t _item1166;
        private sgimgui_capture_item_t _item1167;
        private sgimgui_capture_item_t _item1168;
        private sgimgui_capture_item_t _item1169;
        private sgimgui_capture_item_t _item1170;
        private sgimgui_capture_item_t _item1171;
        private sgimgui_capture_item_t _item1172;
        private sgimgui_capture_item_t _item1173;
        private sgimgui_capture_item_t _item1174;
        private sgimgui_capture_item_t _item1175;
        private sgimgui_capture_item_t _item1176;
        private sgimgui_capture_item_t _item1177;
        private sgimgui_capture_item_t _item1178;
        private sgimgui_capture_item_t _item1179;
        private sgimgui_capture_item_t _item1180;
        private sgimgui_capture_item_t _item1181;
        private sgimgui_capture_item_t _item1182;
        private sgimgui_capture_item_t _item1183;
        private sgimgui_capture_item_t _item1184;
        private sgimgui_capture_item_t _item1185;
        private sgimgui_capture_item_t _item1186;
        private sgimgui_capture_item_t _item1187;
        private sgimgui_capture_item_t _item1188;
        private sgimgui_capture_item_t _item1189;
        private sgimgui_capture_item_t _item1190;
        private sgimgui_capture_item_t _item1191;
        private sgimgui_capture_item_t _item1192;
        private sgimgui_capture_item_t _item1193;
        private sgimgui_capture_item_t _item1194;
        private sgimgui_capture_item_t _item1195;
        private sgimgui_capture_item_t _item1196;
        private sgimgui_capture_item_t _item1197;
        private sgimgui_capture_item_t _item1198;
        private sgimgui_capture_item_t _item1199;
        private sgimgui_capture_item_t _item1200;
        private sgimgui_capture_item_t _item1201;
        private sgimgui_capture_item_t _item1202;
        private sgimgui_capture_item_t _item1203;
        private sgimgui_capture_item_t _item1204;
        private sgimgui_capture_item_t _item1205;
        private sgimgui_capture_item_t _item1206;
        private sgimgui_capture_item_t _item1207;
        private sgimgui_capture_item_t _item1208;
        private sgimgui_capture_item_t _item1209;
        private sgimgui_capture_item_t _item1210;
        private sgimgui_capture_item_t _item1211;
        private sgimgui_capture_item_t _item1212;
        private sgimgui_capture_item_t _item1213;
        private sgimgui_capture_item_t _item1214;
        private sgimgui_capture_item_t _item1215;
        private sgimgui_capture_item_t _item1216;
        private sgimgui_capture_item_t _item1217;
        private sgimgui_capture_item_t _item1218;
        private sgimgui_capture_item_t _item1219;
        private sgimgui_capture_item_t _item1220;
        private sgimgui_capture_item_t _item1221;
        private sgimgui_capture_item_t _item1222;
        private sgimgui_capture_item_t _item1223;
        private sgimgui_capture_item_t _item1224;
        private sgimgui_capture_item_t _item1225;
        private sgimgui_capture_item_t _item1226;
        private sgimgui_capture_item_t _item1227;
        private sgimgui_capture_item_t _item1228;
        private sgimgui_capture_item_t _item1229;
        private sgimgui_capture_item_t _item1230;
        private sgimgui_capture_item_t _item1231;
        private sgimgui_capture_item_t _item1232;
        private sgimgui_capture_item_t _item1233;
        private sgimgui_capture_item_t _item1234;
        private sgimgui_capture_item_t _item1235;
        private sgimgui_capture_item_t _item1236;
        private sgimgui_capture_item_t _item1237;
        private sgimgui_capture_item_t _item1238;
        private sgimgui_capture_item_t _item1239;
        private sgimgui_capture_item_t _item1240;
        private sgimgui_capture_item_t _item1241;
        private sgimgui_capture_item_t _item1242;
        private sgimgui_capture_item_t _item1243;
        private sgimgui_capture_item_t _item1244;
        private sgimgui_capture_item_t _item1245;
        private sgimgui_capture_item_t _item1246;
        private sgimgui_capture_item_t _item1247;
        private sgimgui_capture_item_t _item1248;
        private sgimgui_capture_item_t _item1249;
        private sgimgui_capture_item_t _item1250;
        private sgimgui_capture_item_t _item1251;
        private sgimgui_capture_item_t _item1252;
        private sgimgui_capture_item_t _item1253;
        private sgimgui_capture_item_t _item1254;
        private sgimgui_capture_item_t _item1255;
        private sgimgui_capture_item_t _item1256;
        private sgimgui_capture_item_t _item1257;
        private sgimgui_capture_item_t _item1258;
        private sgimgui_capture_item_t _item1259;
        private sgimgui_capture_item_t _item1260;
        private sgimgui_capture_item_t _item1261;
        private sgimgui_capture_item_t _item1262;
        private sgimgui_capture_item_t _item1263;
        private sgimgui_capture_item_t _item1264;
        private sgimgui_capture_item_t _item1265;
        private sgimgui_capture_item_t _item1266;
        private sgimgui_capture_item_t _item1267;
        private sgimgui_capture_item_t _item1268;
        private sgimgui_capture_item_t _item1269;
        private sgimgui_capture_item_t _item1270;
        private sgimgui_capture_item_t _item1271;
        private sgimgui_capture_item_t _item1272;
        private sgimgui_capture_item_t _item1273;
        private sgimgui_capture_item_t _item1274;
        private sgimgui_capture_item_t _item1275;
        private sgimgui_capture_item_t _item1276;
        private sgimgui_capture_item_t _item1277;
        private sgimgui_capture_item_t _item1278;
        private sgimgui_capture_item_t _item1279;
        private sgimgui_capture_item_t _item1280;
        private sgimgui_capture_item_t _item1281;
        private sgimgui_capture_item_t _item1282;
        private sgimgui_capture_item_t _item1283;
        private sgimgui_capture_item_t _item1284;
        private sgimgui_capture_item_t _item1285;
        private sgimgui_capture_item_t _item1286;
        private sgimgui_capture_item_t _item1287;
        private sgimgui_capture_item_t _item1288;
        private sgimgui_capture_item_t _item1289;
        private sgimgui_capture_item_t _item1290;
        private sgimgui_capture_item_t _item1291;
        private sgimgui_capture_item_t _item1292;
        private sgimgui_capture_item_t _item1293;
        private sgimgui_capture_item_t _item1294;
        private sgimgui_capture_item_t _item1295;
        private sgimgui_capture_item_t _item1296;
        private sgimgui_capture_item_t _item1297;
        private sgimgui_capture_item_t _item1298;
        private sgimgui_capture_item_t _item1299;
        private sgimgui_capture_item_t _item1300;
        private sgimgui_capture_item_t _item1301;
        private sgimgui_capture_item_t _item1302;
        private sgimgui_capture_item_t _item1303;
        private sgimgui_capture_item_t _item1304;
        private sgimgui_capture_item_t _item1305;
        private sgimgui_capture_item_t _item1306;
        private sgimgui_capture_item_t _item1307;
        private sgimgui_capture_item_t _item1308;
        private sgimgui_capture_item_t _item1309;
        private sgimgui_capture_item_t _item1310;
        private sgimgui_capture_item_t _item1311;
        private sgimgui_capture_item_t _item1312;
        private sgimgui_capture_item_t _item1313;
        private sgimgui_capture_item_t _item1314;
        private sgimgui_capture_item_t _item1315;
        private sgimgui_capture_item_t _item1316;
        private sgimgui_capture_item_t _item1317;
        private sgimgui_capture_item_t _item1318;
        private sgimgui_capture_item_t _item1319;
        private sgimgui_capture_item_t _item1320;
        private sgimgui_capture_item_t _item1321;
        private sgimgui_capture_item_t _item1322;
        private sgimgui_capture_item_t _item1323;
        private sgimgui_capture_item_t _item1324;
        private sgimgui_capture_item_t _item1325;
        private sgimgui_capture_item_t _item1326;
        private sgimgui_capture_item_t _item1327;
        private sgimgui_capture_item_t _item1328;
        private sgimgui_capture_item_t _item1329;
        private sgimgui_capture_item_t _item1330;
        private sgimgui_capture_item_t _item1331;
        private sgimgui_capture_item_t _item1332;
        private sgimgui_capture_item_t _item1333;
        private sgimgui_capture_item_t _item1334;
        private sgimgui_capture_item_t _item1335;
        private sgimgui_capture_item_t _item1336;
        private sgimgui_capture_item_t _item1337;
        private sgimgui_capture_item_t _item1338;
        private sgimgui_capture_item_t _item1339;
        private sgimgui_capture_item_t _item1340;
        private sgimgui_capture_item_t _item1341;
        private sgimgui_capture_item_t _item1342;
        private sgimgui_capture_item_t _item1343;
        private sgimgui_capture_item_t _item1344;
        private sgimgui_capture_item_t _item1345;
        private sgimgui_capture_item_t _item1346;
        private sgimgui_capture_item_t _item1347;
        private sgimgui_capture_item_t _item1348;
        private sgimgui_capture_item_t _item1349;
        private sgimgui_capture_item_t _item1350;
        private sgimgui_capture_item_t _item1351;
        private sgimgui_capture_item_t _item1352;
        private sgimgui_capture_item_t _item1353;
        private sgimgui_capture_item_t _item1354;
        private sgimgui_capture_item_t _item1355;
        private sgimgui_capture_item_t _item1356;
        private sgimgui_capture_item_t _item1357;
        private sgimgui_capture_item_t _item1358;
        private sgimgui_capture_item_t _item1359;
        private sgimgui_capture_item_t _item1360;
        private sgimgui_capture_item_t _item1361;
        private sgimgui_capture_item_t _item1362;
        private sgimgui_capture_item_t _item1363;
        private sgimgui_capture_item_t _item1364;
        private sgimgui_capture_item_t _item1365;
        private sgimgui_capture_item_t _item1366;
        private sgimgui_capture_item_t _item1367;
        private sgimgui_capture_item_t _item1368;
        private sgimgui_capture_item_t _item1369;
        private sgimgui_capture_item_t _item1370;
        private sgimgui_capture_item_t _item1371;
        private sgimgui_capture_item_t _item1372;
        private sgimgui_capture_item_t _item1373;
        private sgimgui_capture_item_t _item1374;
        private sgimgui_capture_item_t _item1375;
        private sgimgui_capture_item_t _item1376;
        private sgimgui_capture_item_t _item1377;
        private sgimgui_capture_item_t _item1378;
        private sgimgui_capture_item_t _item1379;
        private sgimgui_capture_item_t _item1380;
        private sgimgui_capture_item_t _item1381;
        private sgimgui_capture_item_t _item1382;
        private sgimgui_capture_item_t _item1383;
        private sgimgui_capture_item_t _item1384;
        private sgimgui_capture_item_t _item1385;
        private sgimgui_capture_item_t _item1386;
        private sgimgui_capture_item_t _item1387;
        private sgimgui_capture_item_t _item1388;
        private sgimgui_capture_item_t _item1389;
        private sgimgui_capture_item_t _item1390;
        private sgimgui_capture_item_t _item1391;
        private sgimgui_capture_item_t _item1392;
        private sgimgui_capture_item_t _item1393;
        private sgimgui_capture_item_t _item1394;
        private sgimgui_capture_item_t _item1395;
        private sgimgui_capture_item_t _item1396;
        private sgimgui_capture_item_t _item1397;
        private sgimgui_capture_item_t _item1398;
        private sgimgui_capture_item_t _item1399;
        private sgimgui_capture_item_t _item1400;
        private sgimgui_capture_item_t _item1401;
        private sgimgui_capture_item_t _item1402;
        private sgimgui_capture_item_t _item1403;
        private sgimgui_capture_item_t _item1404;
        private sgimgui_capture_item_t _item1405;
        private sgimgui_capture_item_t _item1406;
        private sgimgui_capture_item_t _item1407;
        private sgimgui_capture_item_t _item1408;
        private sgimgui_capture_item_t _item1409;
        private sgimgui_capture_item_t _item1410;
        private sgimgui_capture_item_t _item1411;
        private sgimgui_capture_item_t _item1412;
        private sgimgui_capture_item_t _item1413;
        private sgimgui_capture_item_t _item1414;
        private sgimgui_capture_item_t _item1415;
        private sgimgui_capture_item_t _item1416;
        private sgimgui_capture_item_t _item1417;
        private sgimgui_capture_item_t _item1418;
        private sgimgui_capture_item_t _item1419;
        private sgimgui_capture_item_t _item1420;
        private sgimgui_capture_item_t _item1421;
        private sgimgui_capture_item_t _item1422;
        private sgimgui_capture_item_t _item1423;
        private sgimgui_capture_item_t _item1424;
        private sgimgui_capture_item_t _item1425;
        private sgimgui_capture_item_t _item1426;
        private sgimgui_capture_item_t _item1427;
        private sgimgui_capture_item_t _item1428;
        private sgimgui_capture_item_t _item1429;
        private sgimgui_capture_item_t _item1430;
        private sgimgui_capture_item_t _item1431;
        private sgimgui_capture_item_t _item1432;
        private sgimgui_capture_item_t _item1433;
        private sgimgui_capture_item_t _item1434;
        private sgimgui_capture_item_t _item1435;
        private sgimgui_capture_item_t _item1436;
        private sgimgui_capture_item_t _item1437;
        private sgimgui_capture_item_t _item1438;
        private sgimgui_capture_item_t _item1439;
        private sgimgui_capture_item_t _item1440;
        private sgimgui_capture_item_t _item1441;
        private sgimgui_capture_item_t _item1442;
        private sgimgui_capture_item_t _item1443;
        private sgimgui_capture_item_t _item1444;
        private sgimgui_capture_item_t _item1445;
        private sgimgui_capture_item_t _item1446;
        private sgimgui_capture_item_t _item1447;
        private sgimgui_capture_item_t _item1448;
        private sgimgui_capture_item_t _item1449;
        private sgimgui_capture_item_t _item1450;
        private sgimgui_capture_item_t _item1451;
        private sgimgui_capture_item_t _item1452;
        private sgimgui_capture_item_t _item1453;
        private sgimgui_capture_item_t _item1454;
        private sgimgui_capture_item_t _item1455;
        private sgimgui_capture_item_t _item1456;
        private sgimgui_capture_item_t _item1457;
        private sgimgui_capture_item_t _item1458;
        private sgimgui_capture_item_t _item1459;
        private sgimgui_capture_item_t _item1460;
        private sgimgui_capture_item_t _item1461;
        private sgimgui_capture_item_t _item1462;
        private sgimgui_capture_item_t _item1463;
        private sgimgui_capture_item_t _item1464;
        private sgimgui_capture_item_t _item1465;
        private sgimgui_capture_item_t _item1466;
        private sgimgui_capture_item_t _item1467;
        private sgimgui_capture_item_t _item1468;
        private sgimgui_capture_item_t _item1469;
        private sgimgui_capture_item_t _item1470;
        private sgimgui_capture_item_t _item1471;
        private sgimgui_capture_item_t _item1472;
        private sgimgui_capture_item_t _item1473;
        private sgimgui_capture_item_t _item1474;
        private sgimgui_capture_item_t _item1475;
        private sgimgui_capture_item_t _item1476;
        private sgimgui_capture_item_t _item1477;
        private sgimgui_capture_item_t _item1478;
        private sgimgui_capture_item_t _item1479;
        private sgimgui_capture_item_t _item1480;
        private sgimgui_capture_item_t _item1481;
        private sgimgui_capture_item_t _item1482;
        private sgimgui_capture_item_t _item1483;
        private sgimgui_capture_item_t _item1484;
        private sgimgui_capture_item_t _item1485;
        private sgimgui_capture_item_t _item1486;
        private sgimgui_capture_item_t _item1487;
        private sgimgui_capture_item_t _item1488;
        private sgimgui_capture_item_t _item1489;
        private sgimgui_capture_item_t _item1490;
        private sgimgui_capture_item_t _item1491;
        private sgimgui_capture_item_t _item1492;
        private sgimgui_capture_item_t _item1493;
        private sgimgui_capture_item_t _item1494;
        private sgimgui_capture_item_t _item1495;
        private sgimgui_capture_item_t _item1496;
        private sgimgui_capture_item_t _item1497;
        private sgimgui_capture_item_t _item1498;
        private sgimgui_capture_item_t _item1499;
        private sgimgui_capture_item_t _item1500;
        private sgimgui_capture_item_t _item1501;
        private sgimgui_capture_item_t _item1502;
        private sgimgui_capture_item_t _item1503;
        private sgimgui_capture_item_t _item1504;
        private sgimgui_capture_item_t _item1505;
        private sgimgui_capture_item_t _item1506;
        private sgimgui_capture_item_t _item1507;
        private sgimgui_capture_item_t _item1508;
        private sgimgui_capture_item_t _item1509;
        private sgimgui_capture_item_t _item1510;
        private sgimgui_capture_item_t _item1511;
        private sgimgui_capture_item_t _item1512;
        private sgimgui_capture_item_t _item1513;
        private sgimgui_capture_item_t _item1514;
        private sgimgui_capture_item_t _item1515;
        private sgimgui_capture_item_t _item1516;
        private sgimgui_capture_item_t _item1517;
        private sgimgui_capture_item_t _item1518;
        private sgimgui_capture_item_t _item1519;
        private sgimgui_capture_item_t _item1520;
        private sgimgui_capture_item_t _item1521;
        private sgimgui_capture_item_t _item1522;
        private sgimgui_capture_item_t _item1523;
        private sgimgui_capture_item_t _item1524;
        private sgimgui_capture_item_t _item1525;
        private sgimgui_capture_item_t _item1526;
        private sgimgui_capture_item_t _item1527;
        private sgimgui_capture_item_t _item1528;
        private sgimgui_capture_item_t _item1529;
        private sgimgui_capture_item_t _item1530;
        private sgimgui_capture_item_t _item1531;
        private sgimgui_capture_item_t _item1532;
        private sgimgui_capture_item_t _item1533;
        private sgimgui_capture_item_t _item1534;
        private sgimgui_capture_item_t _item1535;
        private sgimgui_capture_item_t _item1536;
        private sgimgui_capture_item_t _item1537;
        private sgimgui_capture_item_t _item1538;
        private sgimgui_capture_item_t _item1539;
        private sgimgui_capture_item_t _item1540;
        private sgimgui_capture_item_t _item1541;
        private sgimgui_capture_item_t _item1542;
        private sgimgui_capture_item_t _item1543;
        private sgimgui_capture_item_t _item1544;
        private sgimgui_capture_item_t _item1545;
        private sgimgui_capture_item_t _item1546;
        private sgimgui_capture_item_t _item1547;
        private sgimgui_capture_item_t _item1548;
        private sgimgui_capture_item_t _item1549;
        private sgimgui_capture_item_t _item1550;
        private sgimgui_capture_item_t _item1551;
        private sgimgui_capture_item_t _item1552;
        private sgimgui_capture_item_t _item1553;
        private sgimgui_capture_item_t _item1554;
        private sgimgui_capture_item_t _item1555;
        private sgimgui_capture_item_t _item1556;
        private sgimgui_capture_item_t _item1557;
        private sgimgui_capture_item_t _item1558;
        private sgimgui_capture_item_t _item1559;
        private sgimgui_capture_item_t _item1560;
        private sgimgui_capture_item_t _item1561;
        private sgimgui_capture_item_t _item1562;
        private sgimgui_capture_item_t _item1563;
        private sgimgui_capture_item_t _item1564;
        private sgimgui_capture_item_t _item1565;
        private sgimgui_capture_item_t _item1566;
        private sgimgui_capture_item_t _item1567;
        private sgimgui_capture_item_t _item1568;
        private sgimgui_capture_item_t _item1569;
        private sgimgui_capture_item_t _item1570;
        private sgimgui_capture_item_t _item1571;
        private sgimgui_capture_item_t _item1572;
        private sgimgui_capture_item_t _item1573;
        private sgimgui_capture_item_t _item1574;
        private sgimgui_capture_item_t _item1575;
        private sgimgui_capture_item_t _item1576;
        private sgimgui_capture_item_t _item1577;
        private sgimgui_capture_item_t _item1578;
        private sgimgui_capture_item_t _item1579;
        private sgimgui_capture_item_t _item1580;
        private sgimgui_capture_item_t _item1581;
        private sgimgui_capture_item_t _item1582;
        private sgimgui_capture_item_t _item1583;
        private sgimgui_capture_item_t _item1584;
        private sgimgui_capture_item_t _item1585;
        private sgimgui_capture_item_t _item1586;
        private sgimgui_capture_item_t _item1587;
        private sgimgui_capture_item_t _item1588;
        private sgimgui_capture_item_t _item1589;
        private sgimgui_capture_item_t _item1590;
        private sgimgui_capture_item_t _item1591;
        private sgimgui_capture_item_t _item1592;
        private sgimgui_capture_item_t _item1593;
        private sgimgui_capture_item_t _item1594;
        private sgimgui_capture_item_t _item1595;
        private sgimgui_capture_item_t _item1596;
        private sgimgui_capture_item_t _item1597;
        private sgimgui_capture_item_t _item1598;
        private sgimgui_capture_item_t _item1599;
        private sgimgui_capture_item_t _item1600;
        private sgimgui_capture_item_t _item1601;
        private sgimgui_capture_item_t _item1602;
        private sgimgui_capture_item_t _item1603;
        private sgimgui_capture_item_t _item1604;
        private sgimgui_capture_item_t _item1605;
        private sgimgui_capture_item_t _item1606;
        private sgimgui_capture_item_t _item1607;
        private sgimgui_capture_item_t _item1608;
        private sgimgui_capture_item_t _item1609;
        private sgimgui_capture_item_t _item1610;
        private sgimgui_capture_item_t _item1611;
        private sgimgui_capture_item_t _item1612;
        private sgimgui_capture_item_t _item1613;
        private sgimgui_capture_item_t _item1614;
        private sgimgui_capture_item_t _item1615;
        private sgimgui_capture_item_t _item1616;
        private sgimgui_capture_item_t _item1617;
        private sgimgui_capture_item_t _item1618;
        private sgimgui_capture_item_t _item1619;
        private sgimgui_capture_item_t _item1620;
        private sgimgui_capture_item_t _item1621;
        private sgimgui_capture_item_t _item1622;
        private sgimgui_capture_item_t _item1623;
        private sgimgui_capture_item_t _item1624;
        private sgimgui_capture_item_t _item1625;
        private sgimgui_capture_item_t _item1626;
        private sgimgui_capture_item_t _item1627;
        private sgimgui_capture_item_t _item1628;
        private sgimgui_capture_item_t _item1629;
        private sgimgui_capture_item_t _item1630;
        private sgimgui_capture_item_t _item1631;
        private sgimgui_capture_item_t _item1632;
        private sgimgui_capture_item_t _item1633;
        private sgimgui_capture_item_t _item1634;
        private sgimgui_capture_item_t _item1635;
        private sgimgui_capture_item_t _item1636;
        private sgimgui_capture_item_t _item1637;
        private sgimgui_capture_item_t _item1638;
        private sgimgui_capture_item_t _item1639;
        private sgimgui_capture_item_t _item1640;
        private sgimgui_capture_item_t _item1641;
        private sgimgui_capture_item_t _item1642;
        private sgimgui_capture_item_t _item1643;
        private sgimgui_capture_item_t _item1644;
        private sgimgui_capture_item_t _item1645;
        private sgimgui_capture_item_t _item1646;
        private sgimgui_capture_item_t _item1647;
        private sgimgui_capture_item_t _item1648;
        private sgimgui_capture_item_t _item1649;
        private sgimgui_capture_item_t _item1650;
        private sgimgui_capture_item_t _item1651;
        private sgimgui_capture_item_t _item1652;
        private sgimgui_capture_item_t _item1653;
        private sgimgui_capture_item_t _item1654;
        private sgimgui_capture_item_t _item1655;
        private sgimgui_capture_item_t _item1656;
        private sgimgui_capture_item_t _item1657;
        private sgimgui_capture_item_t _item1658;
        private sgimgui_capture_item_t _item1659;
        private sgimgui_capture_item_t _item1660;
        private sgimgui_capture_item_t _item1661;
        private sgimgui_capture_item_t _item1662;
        private sgimgui_capture_item_t _item1663;
        private sgimgui_capture_item_t _item1664;
        private sgimgui_capture_item_t _item1665;
        private sgimgui_capture_item_t _item1666;
        private sgimgui_capture_item_t _item1667;
        private sgimgui_capture_item_t _item1668;
        private sgimgui_capture_item_t _item1669;
        private sgimgui_capture_item_t _item1670;
        private sgimgui_capture_item_t _item1671;
        private sgimgui_capture_item_t _item1672;
        private sgimgui_capture_item_t _item1673;
        private sgimgui_capture_item_t _item1674;
        private sgimgui_capture_item_t _item1675;
        private sgimgui_capture_item_t _item1676;
        private sgimgui_capture_item_t _item1677;
        private sgimgui_capture_item_t _item1678;
        private sgimgui_capture_item_t _item1679;
        private sgimgui_capture_item_t _item1680;
        private sgimgui_capture_item_t _item1681;
        private sgimgui_capture_item_t _item1682;
        private sgimgui_capture_item_t _item1683;
        private sgimgui_capture_item_t _item1684;
        private sgimgui_capture_item_t _item1685;
        private sgimgui_capture_item_t _item1686;
        private sgimgui_capture_item_t _item1687;
        private sgimgui_capture_item_t _item1688;
        private sgimgui_capture_item_t _item1689;
        private sgimgui_capture_item_t _item1690;
        private sgimgui_capture_item_t _item1691;
        private sgimgui_capture_item_t _item1692;
        private sgimgui_capture_item_t _item1693;
        private sgimgui_capture_item_t _item1694;
        private sgimgui_capture_item_t _item1695;
        private sgimgui_capture_item_t _item1696;
        private sgimgui_capture_item_t _item1697;
        private sgimgui_capture_item_t _item1698;
        private sgimgui_capture_item_t _item1699;
        private sgimgui_capture_item_t _item1700;
        private sgimgui_capture_item_t _item1701;
        private sgimgui_capture_item_t _item1702;
        private sgimgui_capture_item_t _item1703;
        private sgimgui_capture_item_t _item1704;
        private sgimgui_capture_item_t _item1705;
        private sgimgui_capture_item_t _item1706;
        private sgimgui_capture_item_t _item1707;
        private sgimgui_capture_item_t _item1708;
        private sgimgui_capture_item_t _item1709;
        private sgimgui_capture_item_t _item1710;
        private sgimgui_capture_item_t _item1711;
        private sgimgui_capture_item_t _item1712;
        private sgimgui_capture_item_t _item1713;
        private sgimgui_capture_item_t _item1714;
        private sgimgui_capture_item_t _item1715;
        private sgimgui_capture_item_t _item1716;
        private sgimgui_capture_item_t _item1717;
        private sgimgui_capture_item_t _item1718;
        private sgimgui_capture_item_t _item1719;
        private sgimgui_capture_item_t _item1720;
        private sgimgui_capture_item_t _item1721;
        private sgimgui_capture_item_t _item1722;
        private sgimgui_capture_item_t _item1723;
        private sgimgui_capture_item_t _item1724;
        private sgimgui_capture_item_t _item1725;
        private sgimgui_capture_item_t _item1726;
        private sgimgui_capture_item_t _item1727;
        private sgimgui_capture_item_t _item1728;
        private sgimgui_capture_item_t _item1729;
        private sgimgui_capture_item_t _item1730;
        private sgimgui_capture_item_t _item1731;
        private sgimgui_capture_item_t _item1732;
        private sgimgui_capture_item_t _item1733;
        private sgimgui_capture_item_t _item1734;
        private sgimgui_capture_item_t _item1735;
        private sgimgui_capture_item_t _item1736;
        private sgimgui_capture_item_t _item1737;
        private sgimgui_capture_item_t _item1738;
        private sgimgui_capture_item_t _item1739;
        private sgimgui_capture_item_t _item1740;
        private sgimgui_capture_item_t _item1741;
        private sgimgui_capture_item_t _item1742;
        private sgimgui_capture_item_t _item1743;
        private sgimgui_capture_item_t _item1744;
        private sgimgui_capture_item_t _item1745;
        private sgimgui_capture_item_t _item1746;
        private sgimgui_capture_item_t _item1747;
        private sgimgui_capture_item_t _item1748;
        private sgimgui_capture_item_t _item1749;
        private sgimgui_capture_item_t _item1750;
        private sgimgui_capture_item_t _item1751;
        private sgimgui_capture_item_t _item1752;
        private sgimgui_capture_item_t _item1753;
        private sgimgui_capture_item_t _item1754;
        private sgimgui_capture_item_t _item1755;
        private sgimgui_capture_item_t _item1756;
        private sgimgui_capture_item_t _item1757;
        private sgimgui_capture_item_t _item1758;
        private sgimgui_capture_item_t _item1759;
        private sgimgui_capture_item_t _item1760;
        private sgimgui_capture_item_t _item1761;
        private sgimgui_capture_item_t _item1762;
        private sgimgui_capture_item_t _item1763;
        private sgimgui_capture_item_t _item1764;
        private sgimgui_capture_item_t _item1765;
        private sgimgui_capture_item_t _item1766;
        private sgimgui_capture_item_t _item1767;
        private sgimgui_capture_item_t _item1768;
        private sgimgui_capture_item_t _item1769;
        private sgimgui_capture_item_t _item1770;
        private sgimgui_capture_item_t _item1771;
        private sgimgui_capture_item_t _item1772;
        private sgimgui_capture_item_t _item1773;
        private sgimgui_capture_item_t _item1774;
        private sgimgui_capture_item_t _item1775;
        private sgimgui_capture_item_t _item1776;
        private sgimgui_capture_item_t _item1777;
        private sgimgui_capture_item_t _item1778;
        private sgimgui_capture_item_t _item1779;
        private sgimgui_capture_item_t _item1780;
        private sgimgui_capture_item_t _item1781;
        private sgimgui_capture_item_t _item1782;
        private sgimgui_capture_item_t _item1783;
        private sgimgui_capture_item_t _item1784;
        private sgimgui_capture_item_t _item1785;
        private sgimgui_capture_item_t _item1786;
        private sgimgui_capture_item_t _item1787;
        private sgimgui_capture_item_t _item1788;
        private sgimgui_capture_item_t _item1789;
        private sgimgui_capture_item_t _item1790;
        private sgimgui_capture_item_t _item1791;
        private sgimgui_capture_item_t _item1792;
        private sgimgui_capture_item_t _item1793;
        private sgimgui_capture_item_t _item1794;
        private sgimgui_capture_item_t _item1795;
        private sgimgui_capture_item_t _item1796;
        private sgimgui_capture_item_t _item1797;
        private sgimgui_capture_item_t _item1798;
        private sgimgui_capture_item_t _item1799;
        private sgimgui_capture_item_t _item1800;
        private sgimgui_capture_item_t _item1801;
        private sgimgui_capture_item_t _item1802;
        private sgimgui_capture_item_t _item1803;
        private sgimgui_capture_item_t _item1804;
        private sgimgui_capture_item_t _item1805;
        private sgimgui_capture_item_t _item1806;
        private sgimgui_capture_item_t _item1807;
        private sgimgui_capture_item_t _item1808;
        private sgimgui_capture_item_t _item1809;
        private sgimgui_capture_item_t _item1810;
        private sgimgui_capture_item_t _item1811;
        private sgimgui_capture_item_t _item1812;
        private sgimgui_capture_item_t _item1813;
        private sgimgui_capture_item_t _item1814;
        private sgimgui_capture_item_t _item1815;
        private sgimgui_capture_item_t _item1816;
        private sgimgui_capture_item_t _item1817;
        private sgimgui_capture_item_t _item1818;
        private sgimgui_capture_item_t _item1819;
        private sgimgui_capture_item_t _item1820;
        private sgimgui_capture_item_t _item1821;
        private sgimgui_capture_item_t _item1822;
        private sgimgui_capture_item_t _item1823;
        private sgimgui_capture_item_t _item1824;
        private sgimgui_capture_item_t _item1825;
        private sgimgui_capture_item_t _item1826;
        private sgimgui_capture_item_t _item1827;
        private sgimgui_capture_item_t _item1828;
        private sgimgui_capture_item_t _item1829;
        private sgimgui_capture_item_t _item1830;
        private sgimgui_capture_item_t _item1831;
        private sgimgui_capture_item_t _item1832;
        private sgimgui_capture_item_t _item1833;
        private sgimgui_capture_item_t _item1834;
        private sgimgui_capture_item_t _item1835;
        private sgimgui_capture_item_t _item1836;
        private sgimgui_capture_item_t _item1837;
        private sgimgui_capture_item_t _item1838;
        private sgimgui_capture_item_t _item1839;
        private sgimgui_capture_item_t _item1840;
        private sgimgui_capture_item_t _item1841;
        private sgimgui_capture_item_t _item1842;
        private sgimgui_capture_item_t _item1843;
        private sgimgui_capture_item_t _item1844;
        private sgimgui_capture_item_t _item1845;
        private sgimgui_capture_item_t _item1846;
        private sgimgui_capture_item_t _item1847;
        private sgimgui_capture_item_t _item1848;
        private sgimgui_capture_item_t _item1849;
        private sgimgui_capture_item_t _item1850;
        private sgimgui_capture_item_t _item1851;
        private sgimgui_capture_item_t _item1852;
        private sgimgui_capture_item_t _item1853;
        private sgimgui_capture_item_t _item1854;
        private sgimgui_capture_item_t _item1855;
        private sgimgui_capture_item_t _item1856;
        private sgimgui_capture_item_t _item1857;
        private sgimgui_capture_item_t _item1858;
        private sgimgui_capture_item_t _item1859;
        private sgimgui_capture_item_t _item1860;
        private sgimgui_capture_item_t _item1861;
        private sgimgui_capture_item_t _item1862;
        private sgimgui_capture_item_t _item1863;
        private sgimgui_capture_item_t _item1864;
        private sgimgui_capture_item_t _item1865;
        private sgimgui_capture_item_t _item1866;
        private sgimgui_capture_item_t _item1867;
        private sgimgui_capture_item_t _item1868;
        private sgimgui_capture_item_t _item1869;
        private sgimgui_capture_item_t _item1870;
        private sgimgui_capture_item_t _item1871;
        private sgimgui_capture_item_t _item1872;
        private sgimgui_capture_item_t _item1873;
        private sgimgui_capture_item_t _item1874;
        private sgimgui_capture_item_t _item1875;
        private sgimgui_capture_item_t _item1876;
        private sgimgui_capture_item_t _item1877;
        private sgimgui_capture_item_t _item1878;
        private sgimgui_capture_item_t _item1879;
        private sgimgui_capture_item_t _item1880;
        private sgimgui_capture_item_t _item1881;
        private sgimgui_capture_item_t _item1882;
        private sgimgui_capture_item_t _item1883;
        private sgimgui_capture_item_t _item1884;
        private sgimgui_capture_item_t _item1885;
        private sgimgui_capture_item_t _item1886;
        private sgimgui_capture_item_t _item1887;
        private sgimgui_capture_item_t _item1888;
        private sgimgui_capture_item_t _item1889;
        private sgimgui_capture_item_t _item1890;
        private sgimgui_capture_item_t _item1891;
        private sgimgui_capture_item_t _item1892;
        private sgimgui_capture_item_t _item1893;
        private sgimgui_capture_item_t _item1894;
        private sgimgui_capture_item_t _item1895;
        private sgimgui_capture_item_t _item1896;
        private sgimgui_capture_item_t _item1897;
        private sgimgui_capture_item_t _item1898;
        private sgimgui_capture_item_t _item1899;
        private sgimgui_capture_item_t _item1900;
        private sgimgui_capture_item_t _item1901;
        private sgimgui_capture_item_t _item1902;
        private sgimgui_capture_item_t _item1903;
        private sgimgui_capture_item_t _item1904;
        private sgimgui_capture_item_t _item1905;
        private sgimgui_capture_item_t _item1906;
        private sgimgui_capture_item_t _item1907;
        private sgimgui_capture_item_t _item1908;
        private sgimgui_capture_item_t _item1909;
        private sgimgui_capture_item_t _item1910;
        private sgimgui_capture_item_t _item1911;
        private sgimgui_capture_item_t _item1912;
        private sgimgui_capture_item_t _item1913;
        private sgimgui_capture_item_t _item1914;
        private sgimgui_capture_item_t _item1915;
        private sgimgui_capture_item_t _item1916;
        private sgimgui_capture_item_t _item1917;
        private sgimgui_capture_item_t _item1918;
        private sgimgui_capture_item_t _item1919;
        private sgimgui_capture_item_t _item1920;
        private sgimgui_capture_item_t _item1921;
        private sgimgui_capture_item_t _item1922;
        private sgimgui_capture_item_t _item1923;
        private sgimgui_capture_item_t _item1924;
        private sgimgui_capture_item_t _item1925;
        private sgimgui_capture_item_t _item1926;
        private sgimgui_capture_item_t _item1927;
        private sgimgui_capture_item_t _item1928;
        private sgimgui_capture_item_t _item1929;
        private sgimgui_capture_item_t _item1930;
        private sgimgui_capture_item_t _item1931;
        private sgimgui_capture_item_t _item1932;
        private sgimgui_capture_item_t _item1933;
        private sgimgui_capture_item_t _item1934;
        private sgimgui_capture_item_t _item1935;
        private sgimgui_capture_item_t _item1936;
        private sgimgui_capture_item_t _item1937;
        private sgimgui_capture_item_t _item1938;
        private sgimgui_capture_item_t _item1939;
        private sgimgui_capture_item_t _item1940;
        private sgimgui_capture_item_t _item1941;
        private sgimgui_capture_item_t _item1942;
        private sgimgui_capture_item_t _item1943;
        private sgimgui_capture_item_t _item1944;
        private sgimgui_capture_item_t _item1945;
        private sgimgui_capture_item_t _item1946;
        private sgimgui_capture_item_t _item1947;
        private sgimgui_capture_item_t _item1948;
        private sgimgui_capture_item_t _item1949;
        private sgimgui_capture_item_t _item1950;
        private sgimgui_capture_item_t _item1951;
        private sgimgui_capture_item_t _item1952;
        private sgimgui_capture_item_t _item1953;
        private sgimgui_capture_item_t _item1954;
        private sgimgui_capture_item_t _item1955;
        private sgimgui_capture_item_t _item1956;
        private sgimgui_capture_item_t _item1957;
        private sgimgui_capture_item_t _item1958;
        private sgimgui_capture_item_t _item1959;
        private sgimgui_capture_item_t _item1960;
        private sgimgui_capture_item_t _item1961;
        private sgimgui_capture_item_t _item1962;
        private sgimgui_capture_item_t _item1963;
        private sgimgui_capture_item_t _item1964;
        private sgimgui_capture_item_t _item1965;
        private sgimgui_capture_item_t _item1966;
        private sgimgui_capture_item_t _item1967;
        private sgimgui_capture_item_t _item1968;
        private sgimgui_capture_item_t _item1969;
        private sgimgui_capture_item_t _item1970;
        private sgimgui_capture_item_t _item1971;
        private sgimgui_capture_item_t _item1972;
        private sgimgui_capture_item_t _item1973;
        private sgimgui_capture_item_t _item1974;
        private sgimgui_capture_item_t _item1975;
        private sgimgui_capture_item_t _item1976;
        private sgimgui_capture_item_t _item1977;
        private sgimgui_capture_item_t _item1978;
        private sgimgui_capture_item_t _item1979;
        private sgimgui_capture_item_t _item1980;
        private sgimgui_capture_item_t _item1981;
        private sgimgui_capture_item_t _item1982;
        private sgimgui_capture_item_t _item1983;
        private sgimgui_capture_item_t _item1984;
        private sgimgui_capture_item_t _item1985;
        private sgimgui_capture_item_t _item1986;
        private sgimgui_capture_item_t _item1987;
        private sgimgui_capture_item_t _item1988;
        private sgimgui_capture_item_t _item1989;
        private sgimgui_capture_item_t _item1990;
        private sgimgui_capture_item_t _item1991;
        private sgimgui_capture_item_t _item1992;
        private sgimgui_capture_item_t _item1993;
        private sgimgui_capture_item_t _item1994;
        private sgimgui_capture_item_t _item1995;
        private sgimgui_capture_item_t _item1996;
        private sgimgui_capture_item_t _item1997;
        private sgimgui_capture_item_t _item1998;
        private sgimgui_capture_item_t _item1999;
        private sgimgui_capture_item_t _item2000;
        private sgimgui_capture_item_t _item2001;
        private sgimgui_capture_item_t _item2002;
        private sgimgui_capture_item_t _item2003;
        private sgimgui_capture_item_t _item2004;
        private sgimgui_capture_item_t _item2005;
        private sgimgui_capture_item_t _item2006;
        private sgimgui_capture_item_t _item2007;
        private sgimgui_capture_item_t _item2008;
        private sgimgui_capture_item_t _item2009;
        private sgimgui_capture_item_t _item2010;
        private sgimgui_capture_item_t _item2011;
        private sgimgui_capture_item_t _item2012;
        private sgimgui_capture_item_t _item2013;
        private sgimgui_capture_item_t _item2014;
        private sgimgui_capture_item_t _item2015;
        private sgimgui_capture_item_t _item2016;
        private sgimgui_capture_item_t _item2017;
        private sgimgui_capture_item_t _item2018;
        private sgimgui_capture_item_t _item2019;
        private sgimgui_capture_item_t _item2020;
        private sgimgui_capture_item_t _item2021;
        private sgimgui_capture_item_t _item2022;
        private sgimgui_capture_item_t _item2023;
        private sgimgui_capture_item_t _item2024;
        private sgimgui_capture_item_t _item2025;
        private sgimgui_capture_item_t _item2026;
        private sgimgui_capture_item_t _item2027;
        private sgimgui_capture_item_t _item2028;
        private sgimgui_capture_item_t _item2029;
        private sgimgui_capture_item_t _item2030;
        private sgimgui_capture_item_t _item2031;
        private sgimgui_capture_item_t _item2032;
        private sgimgui_capture_item_t _item2033;
        private sgimgui_capture_item_t _item2034;
        private sgimgui_capture_item_t _item2035;
        private sgimgui_capture_item_t _item2036;
        private sgimgui_capture_item_t _item2037;
        private sgimgui_capture_item_t _item2038;
        private sgimgui_capture_item_t _item2039;
        private sgimgui_capture_item_t _item2040;
        private sgimgui_capture_item_t _item2041;
        private sgimgui_capture_item_t _item2042;
        private sgimgui_capture_item_t _item2043;
        private sgimgui_capture_item_t _item2044;
        private sgimgui_capture_item_t _item2045;
        private sgimgui_capture_item_t _item2046;
        private sgimgui_capture_item_t _item2047;
        private sgimgui_capture_item_t _item2048;
        private sgimgui_capture_item_t _item2049;
        private sgimgui_capture_item_t _item2050;
        private sgimgui_capture_item_t _item2051;
        private sgimgui_capture_item_t _item2052;
        private sgimgui_capture_item_t _item2053;
        private sgimgui_capture_item_t _item2054;
        private sgimgui_capture_item_t _item2055;
        private sgimgui_capture_item_t _item2056;
        private sgimgui_capture_item_t _item2057;
        private sgimgui_capture_item_t _item2058;
        private sgimgui_capture_item_t _item2059;
        private sgimgui_capture_item_t _item2060;
        private sgimgui_capture_item_t _item2061;
        private sgimgui_capture_item_t _item2062;
        private sgimgui_capture_item_t _item2063;
        private sgimgui_capture_item_t _item2064;
        private sgimgui_capture_item_t _item2065;
        private sgimgui_capture_item_t _item2066;
        private sgimgui_capture_item_t _item2067;
        private sgimgui_capture_item_t _item2068;
        private sgimgui_capture_item_t _item2069;
        private sgimgui_capture_item_t _item2070;
        private sgimgui_capture_item_t _item2071;
        private sgimgui_capture_item_t _item2072;
        private sgimgui_capture_item_t _item2073;
        private sgimgui_capture_item_t _item2074;
        private sgimgui_capture_item_t _item2075;
        private sgimgui_capture_item_t _item2076;
        private sgimgui_capture_item_t _item2077;
        private sgimgui_capture_item_t _item2078;
        private sgimgui_capture_item_t _item2079;
        private sgimgui_capture_item_t _item2080;
        private sgimgui_capture_item_t _item2081;
        private sgimgui_capture_item_t _item2082;
        private sgimgui_capture_item_t _item2083;
        private sgimgui_capture_item_t _item2084;
        private sgimgui_capture_item_t _item2085;
        private sgimgui_capture_item_t _item2086;
        private sgimgui_capture_item_t _item2087;
        private sgimgui_capture_item_t _item2088;
        private sgimgui_capture_item_t _item2089;
        private sgimgui_capture_item_t _item2090;
        private sgimgui_capture_item_t _item2091;
        private sgimgui_capture_item_t _item2092;
        private sgimgui_capture_item_t _item2093;
        private sgimgui_capture_item_t _item2094;
        private sgimgui_capture_item_t _item2095;
        private sgimgui_capture_item_t _item2096;
        private sgimgui_capture_item_t _item2097;
        private sgimgui_capture_item_t _item2098;
        private sgimgui_capture_item_t _item2099;
        private sgimgui_capture_item_t _item2100;
        private sgimgui_capture_item_t _item2101;
        private sgimgui_capture_item_t _item2102;
        private sgimgui_capture_item_t _item2103;
        private sgimgui_capture_item_t _item2104;
        private sgimgui_capture_item_t _item2105;
        private sgimgui_capture_item_t _item2106;
        private sgimgui_capture_item_t _item2107;
        private sgimgui_capture_item_t _item2108;
        private sgimgui_capture_item_t _item2109;
        private sgimgui_capture_item_t _item2110;
        private sgimgui_capture_item_t _item2111;
        private sgimgui_capture_item_t _item2112;
        private sgimgui_capture_item_t _item2113;
        private sgimgui_capture_item_t _item2114;
        private sgimgui_capture_item_t _item2115;
        private sgimgui_capture_item_t _item2116;
        private sgimgui_capture_item_t _item2117;
        private sgimgui_capture_item_t _item2118;
        private sgimgui_capture_item_t _item2119;
        private sgimgui_capture_item_t _item2120;
        private sgimgui_capture_item_t _item2121;
        private sgimgui_capture_item_t _item2122;
        private sgimgui_capture_item_t _item2123;
        private sgimgui_capture_item_t _item2124;
        private sgimgui_capture_item_t _item2125;
        private sgimgui_capture_item_t _item2126;
        private sgimgui_capture_item_t _item2127;
        private sgimgui_capture_item_t _item2128;
        private sgimgui_capture_item_t _item2129;
        private sgimgui_capture_item_t _item2130;
        private sgimgui_capture_item_t _item2131;
        private sgimgui_capture_item_t _item2132;
        private sgimgui_capture_item_t _item2133;
        private sgimgui_capture_item_t _item2134;
        private sgimgui_capture_item_t _item2135;
        private sgimgui_capture_item_t _item2136;
        private sgimgui_capture_item_t _item2137;
        private sgimgui_capture_item_t _item2138;
        private sgimgui_capture_item_t _item2139;
        private sgimgui_capture_item_t _item2140;
        private sgimgui_capture_item_t _item2141;
        private sgimgui_capture_item_t _item2142;
        private sgimgui_capture_item_t _item2143;
        private sgimgui_capture_item_t _item2144;
        private sgimgui_capture_item_t _item2145;
        private sgimgui_capture_item_t _item2146;
        private sgimgui_capture_item_t _item2147;
        private sgimgui_capture_item_t _item2148;
        private sgimgui_capture_item_t _item2149;
        private sgimgui_capture_item_t _item2150;
        private sgimgui_capture_item_t _item2151;
        private sgimgui_capture_item_t _item2152;
        private sgimgui_capture_item_t _item2153;
        private sgimgui_capture_item_t _item2154;
        private sgimgui_capture_item_t _item2155;
        private sgimgui_capture_item_t _item2156;
        private sgimgui_capture_item_t _item2157;
        private sgimgui_capture_item_t _item2158;
        private sgimgui_capture_item_t _item2159;
        private sgimgui_capture_item_t _item2160;
        private sgimgui_capture_item_t _item2161;
        private sgimgui_capture_item_t _item2162;
        private sgimgui_capture_item_t _item2163;
        private sgimgui_capture_item_t _item2164;
        private sgimgui_capture_item_t _item2165;
        private sgimgui_capture_item_t _item2166;
        private sgimgui_capture_item_t _item2167;
        private sgimgui_capture_item_t _item2168;
        private sgimgui_capture_item_t _item2169;
        private sgimgui_capture_item_t _item2170;
        private sgimgui_capture_item_t _item2171;
        private sgimgui_capture_item_t _item2172;
        private sgimgui_capture_item_t _item2173;
        private sgimgui_capture_item_t _item2174;
        private sgimgui_capture_item_t _item2175;
        private sgimgui_capture_item_t _item2176;
        private sgimgui_capture_item_t _item2177;
        private sgimgui_capture_item_t _item2178;
        private sgimgui_capture_item_t _item2179;
        private sgimgui_capture_item_t _item2180;
        private sgimgui_capture_item_t _item2181;
        private sgimgui_capture_item_t _item2182;
        private sgimgui_capture_item_t _item2183;
        private sgimgui_capture_item_t _item2184;
        private sgimgui_capture_item_t _item2185;
        private sgimgui_capture_item_t _item2186;
        private sgimgui_capture_item_t _item2187;
        private sgimgui_capture_item_t _item2188;
        private sgimgui_capture_item_t _item2189;
        private sgimgui_capture_item_t _item2190;
        private sgimgui_capture_item_t _item2191;
        private sgimgui_capture_item_t _item2192;
        private sgimgui_capture_item_t _item2193;
        private sgimgui_capture_item_t _item2194;
        private sgimgui_capture_item_t _item2195;
        private sgimgui_capture_item_t _item2196;
        private sgimgui_capture_item_t _item2197;
        private sgimgui_capture_item_t _item2198;
        private sgimgui_capture_item_t _item2199;
        private sgimgui_capture_item_t _item2200;
        private sgimgui_capture_item_t _item2201;
        private sgimgui_capture_item_t _item2202;
        private sgimgui_capture_item_t _item2203;
        private sgimgui_capture_item_t _item2204;
        private sgimgui_capture_item_t _item2205;
        private sgimgui_capture_item_t _item2206;
        private sgimgui_capture_item_t _item2207;
        private sgimgui_capture_item_t _item2208;
        private sgimgui_capture_item_t _item2209;
        private sgimgui_capture_item_t _item2210;
        private sgimgui_capture_item_t _item2211;
        private sgimgui_capture_item_t _item2212;
        private sgimgui_capture_item_t _item2213;
        private sgimgui_capture_item_t _item2214;
        private sgimgui_capture_item_t _item2215;
        private sgimgui_capture_item_t _item2216;
        private sgimgui_capture_item_t _item2217;
        private sgimgui_capture_item_t _item2218;
        private sgimgui_capture_item_t _item2219;
        private sgimgui_capture_item_t _item2220;
        private sgimgui_capture_item_t _item2221;
        private sgimgui_capture_item_t _item2222;
        private sgimgui_capture_item_t _item2223;
        private sgimgui_capture_item_t _item2224;
        private sgimgui_capture_item_t _item2225;
        private sgimgui_capture_item_t _item2226;
        private sgimgui_capture_item_t _item2227;
        private sgimgui_capture_item_t _item2228;
        private sgimgui_capture_item_t _item2229;
        private sgimgui_capture_item_t _item2230;
        private sgimgui_capture_item_t _item2231;
        private sgimgui_capture_item_t _item2232;
        private sgimgui_capture_item_t _item2233;
        private sgimgui_capture_item_t _item2234;
        private sgimgui_capture_item_t _item2235;
        private sgimgui_capture_item_t _item2236;
        private sgimgui_capture_item_t _item2237;
        private sgimgui_capture_item_t _item2238;
        private sgimgui_capture_item_t _item2239;
        private sgimgui_capture_item_t _item2240;
        private sgimgui_capture_item_t _item2241;
        private sgimgui_capture_item_t _item2242;
        private sgimgui_capture_item_t _item2243;
        private sgimgui_capture_item_t _item2244;
        private sgimgui_capture_item_t _item2245;
        private sgimgui_capture_item_t _item2246;
        private sgimgui_capture_item_t _item2247;
        private sgimgui_capture_item_t _item2248;
        private sgimgui_capture_item_t _item2249;
        private sgimgui_capture_item_t _item2250;
        private sgimgui_capture_item_t _item2251;
        private sgimgui_capture_item_t _item2252;
        private sgimgui_capture_item_t _item2253;
        private sgimgui_capture_item_t _item2254;
        private sgimgui_capture_item_t _item2255;
        private sgimgui_capture_item_t _item2256;
        private sgimgui_capture_item_t _item2257;
        private sgimgui_capture_item_t _item2258;
        private sgimgui_capture_item_t _item2259;
        private sgimgui_capture_item_t _item2260;
        private sgimgui_capture_item_t _item2261;
        private sgimgui_capture_item_t _item2262;
        private sgimgui_capture_item_t _item2263;
        private sgimgui_capture_item_t _item2264;
        private sgimgui_capture_item_t _item2265;
        private sgimgui_capture_item_t _item2266;
        private sgimgui_capture_item_t _item2267;
        private sgimgui_capture_item_t _item2268;
        private sgimgui_capture_item_t _item2269;
        private sgimgui_capture_item_t _item2270;
        private sgimgui_capture_item_t _item2271;
        private sgimgui_capture_item_t _item2272;
        private sgimgui_capture_item_t _item2273;
        private sgimgui_capture_item_t _item2274;
        private sgimgui_capture_item_t _item2275;
        private sgimgui_capture_item_t _item2276;
        private sgimgui_capture_item_t _item2277;
        private sgimgui_capture_item_t _item2278;
        private sgimgui_capture_item_t _item2279;
        private sgimgui_capture_item_t _item2280;
        private sgimgui_capture_item_t _item2281;
        private sgimgui_capture_item_t _item2282;
        private sgimgui_capture_item_t _item2283;
        private sgimgui_capture_item_t _item2284;
        private sgimgui_capture_item_t _item2285;
        private sgimgui_capture_item_t _item2286;
        private sgimgui_capture_item_t _item2287;
        private sgimgui_capture_item_t _item2288;
        private sgimgui_capture_item_t _item2289;
        private sgimgui_capture_item_t _item2290;
        private sgimgui_capture_item_t _item2291;
        private sgimgui_capture_item_t _item2292;
        private sgimgui_capture_item_t _item2293;
        private sgimgui_capture_item_t _item2294;
        private sgimgui_capture_item_t _item2295;
        private sgimgui_capture_item_t _item2296;
        private sgimgui_capture_item_t _item2297;
        private sgimgui_capture_item_t _item2298;
        private sgimgui_capture_item_t _item2299;
        private sgimgui_capture_item_t _item2300;
        private sgimgui_capture_item_t _item2301;
        private sgimgui_capture_item_t _item2302;
        private sgimgui_capture_item_t _item2303;
        private sgimgui_capture_item_t _item2304;
        private sgimgui_capture_item_t _item2305;
        private sgimgui_capture_item_t _item2306;
        private sgimgui_capture_item_t _item2307;
        private sgimgui_capture_item_t _item2308;
        private sgimgui_capture_item_t _item2309;
        private sgimgui_capture_item_t _item2310;
        private sgimgui_capture_item_t _item2311;
        private sgimgui_capture_item_t _item2312;
        private sgimgui_capture_item_t _item2313;
        private sgimgui_capture_item_t _item2314;
        private sgimgui_capture_item_t _item2315;
        private sgimgui_capture_item_t _item2316;
        private sgimgui_capture_item_t _item2317;
        private sgimgui_capture_item_t _item2318;
        private sgimgui_capture_item_t _item2319;
        private sgimgui_capture_item_t _item2320;
        private sgimgui_capture_item_t _item2321;
        private sgimgui_capture_item_t _item2322;
        private sgimgui_capture_item_t _item2323;
        private sgimgui_capture_item_t _item2324;
        private sgimgui_capture_item_t _item2325;
        private sgimgui_capture_item_t _item2326;
        private sgimgui_capture_item_t _item2327;
        private sgimgui_capture_item_t _item2328;
        private sgimgui_capture_item_t _item2329;
        private sgimgui_capture_item_t _item2330;
        private sgimgui_capture_item_t _item2331;
        private sgimgui_capture_item_t _item2332;
        private sgimgui_capture_item_t _item2333;
        private sgimgui_capture_item_t _item2334;
        private sgimgui_capture_item_t _item2335;
        private sgimgui_capture_item_t _item2336;
        private sgimgui_capture_item_t _item2337;
        private sgimgui_capture_item_t _item2338;
        private sgimgui_capture_item_t _item2339;
        private sgimgui_capture_item_t _item2340;
        private sgimgui_capture_item_t _item2341;
        private sgimgui_capture_item_t _item2342;
        private sgimgui_capture_item_t _item2343;
        private sgimgui_capture_item_t _item2344;
        private sgimgui_capture_item_t _item2345;
        private sgimgui_capture_item_t _item2346;
        private sgimgui_capture_item_t _item2347;
        private sgimgui_capture_item_t _item2348;
        private sgimgui_capture_item_t _item2349;
        private sgimgui_capture_item_t _item2350;
        private sgimgui_capture_item_t _item2351;
        private sgimgui_capture_item_t _item2352;
        private sgimgui_capture_item_t _item2353;
        private sgimgui_capture_item_t _item2354;
        private sgimgui_capture_item_t _item2355;
        private sgimgui_capture_item_t _item2356;
        private sgimgui_capture_item_t _item2357;
        private sgimgui_capture_item_t _item2358;
        private sgimgui_capture_item_t _item2359;
        private sgimgui_capture_item_t _item2360;
        private sgimgui_capture_item_t _item2361;
        private sgimgui_capture_item_t _item2362;
        private sgimgui_capture_item_t _item2363;
        private sgimgui_capture_item_t _item2364;
        private sgimgui_capture_item_t _item2365;
        private sgimgui_capture_item_t _item2366;
        private sgimgui_capture_item_t _item2367;
        private sgimgui_capture_item_t _item2368;
        private sgimgui_capture_item_t _item2369;
        private sgimgui_capture_item_t _item2370;
        private sgimgui_capture_item_t _item2371;
        private sgimgui_capture_item_t _item2372;
        private sgimgui_capture_item_t _item2373;
        private sgimgui_capture_item_t _item2374;
        private sgimgui_capture_item_t _item2375;
        private sgimgui_capture_item_t _item2376;
        private sgimgui_capture_item_t _item2377;
        private sgimgui_capture_item_t _item2378;
        private sgimgui_capture_item_t _item2379;
        private sgimgui_capture_item_t _item2380;
        private sgimgui_capture_item_t _item2381;
        private sgimgui_capture_item_t _item2382;
        private sgimgui_capture_item_t _item2383;
        private sgimgui_capture_item_t _item2384;
        private sgimgui_capture_item_t _item2385;
        private sgimgui_capture_item_t _item2386;
        private sgimgui_capture_item_t _item2387;
        private sgimgui_capture_item_t _item2388;
        private sgimgui_capture_item_t _item2389;
        private sgimgui_capture_item_t _item2390;
        private sgimgui_capture_item_t _item2391;
        private sgimgui_capture_item_t _item2392;
        private sgimgui_capture_item_t _item2393;
        private sgimgui_capture_item_t _item2394;
        private sgimgui_capture_item_t _item2395;
        private sgimgui_capture_item_t _item2396;
        private sgimgui_capture_item_t _item2397;
        private sgimgui_capture_item_t _item2398;
        private sgimgui_capture_item_t _item2399;
        private sgimgui_capture_item_t _item2400;
        private sgimgui_capture_item_t _item2401;
        private sgimgui_capture_item_t _item2402;
        private sgimgui_capture_item_t _item2403;
        private sgimgui_capture_item_t _item2404;
        private sgimgui_capture_item_t _item2405;
        private sgimgui_capture_item_t _item2406;
        private sgimgui_capture_item_t _item2407;
        private sgimgui_capture_item_t _item2408;
        private sgimgui_capture_item_t _item2409;
        private sgimgui_capture_item_t _item2410;
        private sgimgui_capture_item_t _item2411;
        private sgimgui_capture_item_t _item2412;
        private sgimgui_capture_item_t _item2413;
        private sgimgui_capture_item_t _item2414;
        private sgimgui_capture_item_t _item2415;
        private sgimgui_capture_item_t _item2416;
        private sgimgui_capture_item_t _item2417;
        private sgimgui_capture_item_t _item2418;
        private sgimgui_capture_item_t _item2419;
        private sgimgui_capture_item_t _item2420;
        private sgimgui_capture_item_t _item2421;
        private sgimgui_capture_item_t _item2422;
        private sgimgui_capture_item_t _item2423;
        private sgimgui_capture_item_t _item2424;
        private sgimgui_capture_item_t _item2425;
        private sgimgui_capture_item_t _item2426;
        private sgimgui_capture_item_t _item2427;
        private sgimgui_capture_item_t _item2428;
        private sgimgui_capture_item_t _item2429;
        private sgimgui_capture_item_t _item2430;
        private sgimgui_capture_item_t _item2431;
        private sgimgui_capture_item_t _item2432;
        private sgimgui_capture_item_t _item2433;
        private sgimgui_capture_item_t _item2434;
        private sgimgui_capture_item_t _item2435;
        private sgimgui_capture_item_t _item2436;
        private sgimgui_capture_item_t _item2437;
        private sgimgui_capture_item_t _item2438;
        private sgimgui_capture_item_t _item2439;
        private sgimgui_capture_item_t _item2440;
        private sgimgui_capture_item_t _item2441;
        private sgimgui_capture_item_t _item2442;
        private sgimgui_capture_item_t _item2443;
        private sgimgui_capture_item_t _item2444;
        private sgimgui_capture_item_t _item2445;
        private sgimgui_capture_item_t _item2446;
        private sgimgui_capture_item_t _item2447;
        private sgimgui_capture_item_t _item2448;
        private sgimgui_capture_item_t _item2449;
        private sgimgui_capture_item_t _item2450;
        private sgimgui_capture_item_t _item2451;
        private sgimgui_capture_item_t _item2452;
        private sgimgui_capture_item_t _item2453;
        private sgimgui_capture_item_t _item2454;
        private sgimgui_capture_item_t _item2455;
        private sgimgui_capture_item_t _item2456;
        private sgimgui_capture_item_t _item2457;
        private sgimgui_capture_item_t _item2458;
        private sgimgui_capture_item_t _item2459;
        private sgimgui_capture_item_t _item2460;
        private sgimgui_capture_item_t _item2461;
        private sgimgui_capture_item_t _item2462;
        private sgimgui_capture_item_t _item2463;
        private sgimgui_capture_item_t _item2464;
        private sgimgui_capture_item_t _item2465;
        private sgimgui_capture_item_t _item2466;
        private sgimgui_capture_item_t _item2467;
        private sgimgui_capture_item_t _item2468;
        private sgimgui_capture_item_t _item2469;
        private sgimgui_capture_item_t _item2470;
        private sgimgui_capture_item_t _item2471;
        private sgimgui_capture_item_t _item2472;
        private sgimgui_capture_item_t _item2473;
        private sgimgui_capture_item_t _item2474;
        private sgimgui_capture_item_t _item2475;
        private sgimgui_capture_item_t _item2476;
        private sgimgui_capture_item_t _item2477;
        private sgimgui_capture_item_t _item2478;
        private sgimgui_capture_item_t _item2479;
        private sgimgui_capture_item_t _item2480;
        private sgimgui_capture_item_t _item2481;
        private sgimgui_capture_item_t _item2482;
        private sgimgui_capture_item_t _item2483;
        private sgimgui_capture_item_t _item2484;
        private sgimgui_capture_item_t _item2485;
        private sgimgui_capture_item_t _item2486;
        private sgimgui_capture_item_t _item2487;
        private sgimgui_capture_item_t _item2488;
        private sgimgui_capture_item_t _item2489;
        private sgimgui_capture_item_t _item2490;
        private sgimgui_capture_item_t _item2491;
        private sgimgui_capture_item_t _item2492;
        private sgimgui_capture_item_t _item2493;
        private sgimgui_capture_item_t _item2494;
        private sgimgui_capture_item_t _item2495;
        private sgimgui_capture_item_t _item2496;
        private sgimgui_capture_item_t _item2497;
        private sgimgui_capture_item_t _item2498;
        private sgimgui_capture_item_t _item2499;
        private sgimgui_capture_item_t _item2500;
        private sgimgui_capture_item_t _item2501;
        private sgimgui_capture_item_t _item2502;
        private sgimgui_capture_item_t _item2503;
        private sgimgui_capture_item_t _item2504;
        private sgimgui_capture_item_t _item2505;
        private sgimgui_capture_item_t _item2506;
        private sgimgui_capture_item_t _item2507;
        private sgimgui_capture_item_t _item2508;
        private sgimgui_capture_item_t _item2509;
        private sgimgui_capture_item_t _item2510;
        private sgimgui_capture_item_t _item2511;
        private sgimgui_capture_item_t _item2512;
        private sgimgui_capture_item_t _item2513;
        private sgimgui_capture_item_t _item2514;
        private sgimgui_capture_item_t _item2515;
        private sgimgui_capture_item_t _item2516;
        private sgimgui_capture_item_t _item2517;
        private sgimgui_capture_item_t _item2518;
        private sgimgui_capture_item_t _item2519;
        private sgimgui_capture_item_t _item2520;
        private sgimgui_capture_item_t _item2521;
        private sgimgui_capture_item_t _item2522;
        private sgimgui_capture_item_t _item2523;
        private sgimgui_capture_item_t _item2524;
        private sgimgui_capture_item_t _item2525;
        private sgimgui_capture_item_t _item2526;
        private sgimgui_capture_item_t _item2527;
        private sgimgui_capture_item_t _item2528;
        private sgimgui_capture_item_t _item2529;
        private sgimgui_capture_item_t _item2530;
        private sgimgui_capture_item_t _item2531;
        private sgimgui_capture_item_t _item2532;
        private sgimgui_capture_item_t _item2533;
        private sgimgui_capture_item_t _item2534;
        private sgimgui_capture_item_t _item2535;
        private sgimgui_capture_item_t _item2536;
        private sgimgui_capture_item_t _item2537;
        private sgimgui_capture_item_t _item2538;
        private sgimgui_capture_item_t _item2539;
        private sgimgui_capture_item_t _item2540;
        private sgimgui_capture_item_t _item2541;
        private sgimgui_capture_item_t _item2542;
        private sgimgui_capture_item_t _item2543;
        private sgimgui_capture_item_t _item2544;
        private sgimgui_capture_item_t _item2545;
        private sgimgui_capture_item_t _item2546;
        private sgimgui_capture_item_t _item2547;
        private sgimgui_capture_item_t _item2548;
        private sgimgui_capture_item_t _item2549;
        private sgimgui_capture_item_t _item2550;
        private sgimgui_capture_item_t _item2551;
        private sgimgui_capture_item_t _item2552;
        private sgimgui_capture_item_t _item2553;
        private sgimgui_capture_item_t _item2554;
        private sgimgui_capture_item_t _item2555;
        private sgimgui_capture_item_t _item2556;
        private sgimgui_capture_item_t _item2557;
        private sgimgui_capture_item_t _item2558;
        private sgimgui_capture_item_t _item2559;
        private sgimgui_capture_item_t _item2560;
        private sgimgui_capture_item_t _item2561;
        private sgimgui_capture_item_t _item2562;
        private sgimgui_capture_item_t _item2563;
        private sgimgui_capture_item_t _item2564;
        private sgimgui_capture_item_t _item2565;
        private sgimgui_capture_item_t _item2566;
        private sgimgui_capture_item_t _item2567;
        private sgimgui_capture_item_t _item2568;
        private sgimgui_capture_item_t _item2569;
        private sgimgui_capture_item_t _item2570;
        private sgimgui_capture_item_t _item2571;
        private sgimgui_capture_item_t _item2572;
        private sgimgui_capture_item_t _item2573;
        private sgimgui_capture_item_t _item2574;
        private sgimgui_capture_item_t _item2575;
        private sgimgui_capture_item_t _item2576;
        private sgimgui_capture_item_t _item2577;
        private sgimgui_capture_item_t _item2578;
        private sgimgui_capture_item_t _item2579;
        private sgimgui_capture_item_t _item2580;
        private sgimgui_capture_item_t _item2581;
        private sgimgui_capture_item_t _item2582;
        private sgimgui_capture_item_t _item2583;
        private sgimgui_capture_item_t _item2584;
        private sgimgui_capture_item_t _item2585;
        private sgimgui_capture_item_t _item2586;
        private sgimgui_capture_item_t _item2587;
        private sgimgui_capture_item_t _item2588;
        private sgimgui_capture_item_t _item2589;
        private sgimgui_capture_item_t _item2590;
        private sgimgui_capture_item_t _item2591;
        private sgimgui_capture_item_t _item2592;
        private sgimgui_capture_item_t _item2593;
        private sgimgui_capture_item_t _item2594;
        private sgimgui_capture_item_t _item2595;
        private sgimgui_capture_item_t _item2596;
        private sgimgui_capture_item_t _item2597;
        private sgimgui_capture_item_t _item2598;
        private sgimgui_capture_item_t _item2599;
        private sgimgui_capture_item_t _item2600;
        private sgimgui_capture_item_t _item2601;
        private sgimgui_capture_item_t _item2602;
        private sgimgui_capture_item_t _item2603;
        private sgimgui_capture_item_t _item2604;
        private sgimgui_capture_item_t _item2605;
        private sgimgui_capture_item_t _item2606;
        private sgimgui_capture_item_t _item2607;
        private sgimgui_capture_item_t _item2608;
        private sgimgui_capture_item_t _item2609;
        private sgimgui_capture_item_t _item2610;
        private sgimgui_capture_item_t _item2611;
        private sgimgui_capture_item_t _item2612;
        private sgimgui_capture_item_t _item2613;
        private sgimgui_capture_item_t _item2614;
        private sgimgui_capture_item_t _item2615;
        private sgimgui_capture_item_t _item2616;
        private sgimgui_capture_item_t _item2617;
        private sgimgui_capture_item_t _item2618;
        private sgimgui_capture_item_t _item2619;
        private sgimgui_capture_item_t _item2620;
        private sgimgui_capture_item_t _item2621;
        private sgimgui_capture_item_t _item2622;
        private sgimgui_capture_item_t _item2623;
        private sgimgui_capture_item_t _item2624;
        private sgimgui_capture_item_t _item2625;
        private sgimgui_capture_item_t _item2626;
        private sgimgui_capture_item_t _item2627;
        private sgimgui_capture_item_t _item2628;
        private sgimgui_capture_item_t _item2629;
        private sgimgui_capture_item_t _item2630;
        private sgimgui_capture_item_t _item2631;
        private sgimgui_capture_item_t _item2632;
        private sgimgui_capture_item_t _item2633;
        private sgimgui_capture_item_t _item2634;
        private sgimgui_capture_item_t _item2635;
        private sgimgui_capture_item_t _item2636;
        private sgimgui_capture_item_t _item2637;
        private sgimgui_capture_item_t _item2638;
        private sgimgui_capture_item_t _item2639;
        private sgimgui_capture_item_t _item2640;
        private sgimgui_capture_item_t _item2641;
        private sgimgui_capture_item_t _item2642;
        private sgimgui_capture_item_t _item2643;
        private sgimgui_capture_item_t _item2644;
        private sgimgui_capture_item_t _item2645;
        private sgimgui_capture_item_t _item2646;
        private sgimgui_capture_item_t _item2647;
        private sgimgui_capture_item_t _item2648;
        private sgimgui_capture_item_t _item2649;
        private sgimgui_capture_item_t _item2650;
        private sgimgui_capture_item_t _item2651;
        private sgimgui_capture_item_t _item2652;
        private sgimgui_capture_item_t _item2653;
        private sgimgui_capture_item_t _item2654;
        private sgimgui_capture_item_t _item2655;
        private sgimgui_capture_item_t _item2656;
        private sgimgui_capture_item_t _item2657;
        private sgimgui_capture_item_t _item2658;
        private sgimgui_capture_item_t _item2659;
        private sgimgui_capture_item_t _item2660;
        private sgimgui_capture_item_t _item2661;
        private sgimgui_capture_item_t _item2662;
        private sgimgui_capture_item_t _item2663;
        private sgimgui_capture_item_t _item2664;
        private sgimgui_capture_item_t _item2665;
        private sgimgui_capture_item_t _item2666;
        private sgimgui_capture_item_t _item2667;
        private sgimgui_capture_item_t _item2668;
        private sgimgui_capture_item_t _item2669;
        private sgimgui_capture_item_t _item2670;
        private sgimgui_capture_item_t _item2671;
        private sgimgui_capture_item_t _item2672;
        private sgimgui_capture_item_t _item2673;
        private sgimgui_capture_item_t _item2674;
        private sgimgui_capture_item_t _item2675;
        private sgimgui_capture_item_t _item2676;
        private sgimgui_capture_item_t _item2677;
        private sgimgui_capture_item_t _item2678;
        private sgimgui_capture_item_t _item2679;
        private sgimgui_capture_item_t _item2680;
        private sgimgui_capture_item_t _item2681;
        private sgimgui_capture_item_t _item2682;
        private sgimgui_capture_item_t _item2683;
        private sgimgui_capture_item_t _item2684;
        private sgimgui_capture_item_t _item2685;
        private sgimgui_capture_item_t _item2686;
        private sgimgui_capture_item_t _item2687;
        private sgimgui_capture_item_t _item2688;
        private sgimgui_capture_item_t _item2689;
        private sgimgui_capture_item_t _item2690;
        private sgimgui_capture_item_t _item2691;
        private sgimgui_capture_item_t _item2692;
        private sgimgui_capture_item_t _item2693;
        private sgimgui_capture_item_t _item2694;
        private sgimgui_capture_item_t _item2695;
        private sgimgui_capture_item_t _item2696;
        private sgimgui_capture_item_t _item2697;
        private sgimgui_capture_item_t _item2698;
        private sgimgui_capture_item_t _item2699;
        private sgimgui_capture_item_t _item2700;
        private sgimgui_capture_item_t _item2701;
        private sgimgui_capture_item_t _item2702;
        private sgimgui_capture_item_t _item2703;
        private sgimgui_capture_item_t _item2704;
        private sgimgui_capture_item_t _item2705;
        private sgimgui_capture_item_t _item2706;
        private sgimgui_capture_item_t _item2707;
        private sgimgui_capture_item_t _item2708;
        private sgimgui_capture_item_t _item2709;
        private sgimgui_capture_item_t _item2710;
        private sgimgui_capture_item_t _item2711;
        private sgimgui_capture_item_t _item2712;
        private sgimgui_capture_item_t _item2713;
        private sgimgui_capture_item_t _item2714;
        private sgimgui_capture_item_t _item2715;
        private sgimgui_capture_item_t _item2716;
        private sgimgui_capture_item_t _item2717;
        private sgimgui_capture_item_t _item2718;
        private sgimgui_capture_item_t _item2719;
        private sgimgui_capture_item_t _item2720;
        private sgimgui_capture_item_t _item2721;
        private sgimgui_capture_item_t _item2722;
        private sgimgui_capture_item_t _item2723;
        private sgimgui_capture_item_t _item2724;
        private sgimgui_capture_item_t _item2725;
        private sgimgui_capture_item_t _item2726;
        private sgimgui_capture_item_t _item2727;
        private sgimgui_capture_item_t _item2728;
        private sgimgui_capture_item_t _item2729;
        private sgimgui_capture_item_t _item2730;
        private sgimgui_capture_item_t _item2731;
        private sgimgui_capture_item_t _item2732;
        private sgimgui_capture_item_t _item2733;
        private sgimgui_capture_item_t _item2734;
        private sgimgui_capture_item_t _item2735;
        private sgimgui_capture_item_t _item2736;
        private sgimgui_capture_item_t _item2737;
        private sgimgui_capture_item_t _item2738;
        private sgimgui_capture_item_t _item2739;
        private sgimgui_capture_item_t _item2740;
        private sgimgui_capture_item_t _item2741;
        private sgimgui_capture_item_t _item2742;
        private sgimgui_capture_item_t _item2743;
        private sgimgui_capture_item_t _item2744;
        private sgimgui_capture_item_t _item2745;
        private sgimgui_capture_item_t _item2746;
        private sgimgui_capture_item_t _item2747;
        private sgimgui_capture_item_t _item2748;
        private sgimgui_capture_item_t _item2749;
        private sgimgui_capture_item_t _item2750;
        private sgimgui_capture_item_t _item2751;
        private sgimgui_capture_item_t _item2752;
        private sgimgui_capture_item_t _item2753;
        private sgimgui_capture_item_t _item2754;
        private sgimgui_capture_item_t _item2755;
        private sgimgui_capture_item_t _item2756;
        private sgimgui_capture_item_t _item2757;
        private sgimgui_capture_item_t _item2758;
        private sgimgui_capture_item_t _item2759;
        private sgimgui_capture_item_t _item2760;
        private sgimgui_capture_item_t _item2761;
        private sgimgui_capture_item_t _item2762;
        private sgimgui_capture_item_t _item2763;
        private sgimgui_capture_item_t _item2764;
        private sgimgui_capture_item_t _item2765;
        private sgimgui_capture_item_t _item2766;
        private sgimgui_capture_item_t _item2767;
        private sgimgui_capture_item_t _item2768;
        private sgimgui_capture_item_t _item2769;
        private sgimgui_capture_item_t _item2770;
        private sgimgui_capture_item_t _item2771;
        private sgimgui_capture_item_t _item2772;
        private sgimgui_capture_item_t _item2773;
        private sgimgui_capture_item_t _item2774;
        private sgimgui_capture_item_t _item2775;
        private sgimgui_capture_item_t _item2776;
        private sgimgui_capture_item_t _item2777;
        private sgimgui_capture_item_t _item2778;
        private sgimgui_capture_item_t _item2779;
        private sgimgui_capture_item_t _item2780;
        private sgimgui_capture_item_t _item2781;
        private sgimgui_capture_item_t _item2782;
        private sgimgui_capture_item_t _item2783;
        private sgimgui_capture_item_t _item2784;
        private sgimgui_capture_item_t _item2785;
        private sgimgui_capture_item_t _item2786;
        private sgimgui_capture_item_t _item2787;
        private sgimgui_capture_item_t _item2788;
        private sgimgui_capture_item_t _item2789;
        private sgimgui_capture_item_t _item2790;
        private sgimgui_capture_item_t _item2791;
        private sgimgui_capture_item_t _item2792;
        private sgimgui_capture_item_t _item2793;
        private sgimgui_capture_item_t _item2794;
        private sgimgui_capture_item_t _item2795;
        private sgimgui_capture_item_t _item2796;
        private sgimgui_capture_item_t _item2797;
        private sgimgui_capture_item_t _item2798;
        private sgimgui_capture_item_t _item2799;
        private sgimgui_capture_item_t _item2800;
        private sgimgui_capture_item_t _item2801;
        private sgimgui_capture_item_t _item2802;
        private sgimgui_capture_item_t _item2803;
        private sgimgui_capture_item_t _item2804;
        private sgimgui_capture_item_t _item2805;
        private sgimgui_capture_item_t _item2806;
        private sgimgui_capture_item_t _item2807;
        private sgimgui_capture_item_t _item2808;
        private sgimgui_capture_item_t _item2809;
        private sgimgui_capture_item_t _item2810;
        private sgimgui_capture_item_t _item2811;
        private sgimgui_capture_item_t _item2812;
        private sgimgui_capture_item_t _item2813;
        private sgimgui_capture_item_t _item2814;
        private sgimgui_capture_item_t _item2815;
        private sgimgui_capture_item_t _item2816;
        private sgimgui_capture_item_t _item2817;
        private sgimgui_capture_item_t _item2818;
        private sgimgui_capture_item_t _item2819;
        private sgimgui_capture_item_t _item2820;
        private sgimgui_capture_item_t _item2821;
        private sgimgui_capture_item_t _item2822;
        private sgimgui_capture_item_t _item2823;
        private sgimgui_capture_item_t _item2824;
        private sgimgui_capture_item_t _item2825;
        private sgimgui_capture_item_t _item2826;
        private sgimgui_capture_item_t _item2827;
        private sgimgui_capture_item_t _item2828;
        private sgimgui_capture_item_t _item2829;
        private sgimgui_capture_item_t _item2830;
        private sgimgui_capture_item_t _item2831;
        private sgimgui_capture_item_t _item2832;
        private sgimgui_capture_item_t _item2833;
        private sgimgui_capture_item_t _item2834;
        private sgimgui_capture_item_t _item2835;
        private sgimgui_capture_item_t _item2836;
        private sgimgui_capture_item_t _item2837;
        private sgimgui_capture_item_t _item2838;
        private sgimgui_capture_item_t _item2839;
        private sgimgui_capture_item_t _item2840;
        private sgimgui_capture_item_t _item2841;
        private sgimgui_capture_item_t _item2842;
        private sgimgui_capture_item_t _item2843;
        private sgimgui_capture_item_t _item2844;
        private sgimgui_capture_item_t _item2845;
        private sgimgui_capture_item_t _item2846;
        private sgimgui_capture_item_t _item2847;
        private sgimgui_capture_item_t _item2848;
        private sgimgui_capture_item_t _item2849;
        private sgimgui_capture_item_t _item2850;
        private sgimgui_capture_item_t _item2851;
        private sgimgui_capture_item_t _item2852;
        private sgimgui_capture_item_t _item2853;
        private sgimgui_capture_item_t _item2854;
        private sgimgui_capture_item_t _item2855;
        private sgimgui_capture_item_t _item2856;
        private sgimgui_capture_item_t _item2857;
        private sgimgui_capture_item_t _item2858;
        private sgimgui_capture_item_t _item2859;
        private sgimgui_capture_item_t _item2860;
        private sgimgui_capture_item_t _item2861;
        private sgimgui_capture_item_t _item2862;
        private sgimgui_capture_item_t _item2863;
        private sgimgui_capture_item_t _item2864;
        private sgimgui_capture_item_t _item2865;
        private sgimgui_capture_item_t _item2866;
        private sgimgui_capture_item_t _item2867;
        private sgimgui_capture_item_t _item2868;
        private sgimgui_capture_item_t _item2869;
        private sgimgui_capture_item_t _item2870;
        private sgimgui_capture_item_t _item2871;
        private sgimgui_capture_item_t _item2872;
        private sgimgui_capture_item_t _item2873;
        private sgimgui_capture_item_t _item2874;
        private sgimgui_capture_item_t _item2875;
        private sgimgui_capture_item_t _item2876;
        private sgimgui_capture_item_t _item2877;
        private sgimgui_capture_item_t _item2878;
        private sgimgui_capture_item_t _item2879;
        private sgimgui_capture_item_t _item2880;
        private sgimgui_capture_item_t _item2881;
        private sgimgui_capture_item_t _item2882;
        private sgimgui_capture_item_t _item2883;
        private sgimgui_capture_item_t _item2884;
        private sgimgui_capture_item_t _item2885;
        private sgimgui_capture_item_t _item2886;
        private sgimgui_capture_item_t _item2887;
        private sgimgui_capture_item_t _item2888;
        private sgimgui_capture_item_t _item2889;
        private sgimgui_capture_item_t _item2890;
        private sgimgui_capture_item_t _item2891;
        private sgimgui_capture_item_t _item2892;
        private sgimgui_capture_item_t _item2893;
        private sgimgui_capture_item_t _item2894;
        private sgimgui_capture_item_t _item2895;
        private sgimgui_capture_item_t _item2896;
        private sgimgui_capture_item_t _item2897;
        private sgimgui_capture_item_t _item2898;
        private sgimgui_capture_item_t _item2899;
        private sgimgui_capture_item_t _item2900;
        private sgimgui_capture_item_t _item2901;
        private sgimgui_capture_item_t _item2902;
        private sgimgui_capture_item_t _item2903;
        private sgimgui_capture_item_t _item2904;
        private sgimgui_capture_item_t _item2905;
        private sgimgui_capture_item_t _item2906;
        private sgimgui_capture_item_t _item2907;
        private sgimgui_capture_item_t _item2908;
        private sgimgui_capture_item_t _item2909;
        private sgimgui_capture_item_t _item2910;
        private sgimgui_capture_item_t _item2911;
        private sgimgui_capture_item_t _item2912;
        private sgimgui_capture_item_t _item2913;
        private sgimgui_capture_item_t _item2914;
        private sgimgui_capture_item_t _item2915;
        private sgimgui_capture_item_t _item2916;
        private sgimgui_capture_item_t _item2917;
        private sgimgui_capture_item_t _item2918;
        private sgimgui_capture_item_t _item2919;
        private sgimgui_capture_item_t _item2920;
        private sgimgui_capture_item_t _item2921;
        private sgimgui_capture_item_t _item2922;
        private sgimgui_capture_item_t _item2923;
        private sgimgui_capture_item_t _item2924;
        private sgimgui_capture_item_t _item2925;
        private sgimgui_capture_item_t _item2926;
        private sgimgui_capture_item_t _item2927;
        private sgimgui_capture_item_t _item2928;
        private sgimgui_capture_item_t _item2929;
        private sgimgui_capture_item_t _item2930;
        private sgimgui_capture_item_t _item2931;
        private sgimgui_capture_item_t _item2932;
        private sgimgui_capture_item_t _item2933;
        private sgimgui_capture_item_t _item2934;
        private sgimgui_capture_item_t _item2935;
        private sgimgui_capture_item_t _item2936;
        private sgimgui_capture_item_t _item2937;
        private sgimgui_capture_item_t _item2938;
        private sgimgui_capture_item_t _item2939;
        private sgimgui_capture_item_t _item2940;
        private sgimgui_capture_item_t _item2941;
        private sgimgui_capture_item_t _item2942;
        private sgimgui_capture_item_t _item2943;
        private sgimgui_capture_item_t _item2944;
        private sgimgui_capture_item_t _item2945;
        private sgimgui_capture_item_t _item2946;
        private sgimgui_capture_item_t _item2947;
        private sgimgui_capture_item_t _item2948;
        private sgimgui_capture_item_t _item2949;
        private sgimgui_capture_item_t _item2950;
        private sgimgui_capture_item_t _item2951;
        private sgimgui_capture_item_t _item2952;
        private sgimgui_capture_item_t _item2953;
        private sgimgui_capture_item_t _item2954;
        private sgimgui_capture_item_t _item2955;
        private sgimgui_capture_item_t _item2956;
        private sgimgui_capture_item_t _item2957;
        private sgimgui_capture_item_t _item2958;
        private sgimgui_capture_item_t _item2959;
        private sgimgui_capture_item_t _item2960;
        private sgimgui_capture_item_t _item2961;
        private sgimgui_capture_item_t _item2962;
        private sgimgui_capture_item_t _item2963;
        private sgimgui_capture_item_t _item2964;
        private sgimgui_capture_item_t _item2965;
        private sgimgui_capture_item_t _item2966;
        private sgimgui_capture_item_t _item2967;
        private sgimgui_capture_item_t _item2968;
        private sgimgui_capture_item_t _item2969;
        private sgimgui_capture_item_t _item2970;
        private sgimgui_capture_item_t _item2971;
        private sgimgui_capture_item_t _item2972;
        private sgimgui_capture_item_t _item2973;
        private sgimgui_capture_item_t _item2974;
        private sgimgui_capture_item_t _item2975;
        private sgimgui_capture_item_t _item2976;
        private sgimgui_capture_item_t _item2977;
        private sgimgui_capture_item_t _item2978;
        private sgimgui_capture_item_t _item2979;
        private sgimgui_capture_item_t _item2980;
        private sgimgui_capture_item_t _item2981;
        private sgimgui_capture_item_t _item2982;
        private sgimgui_capture_item_t _item2983;
        private sgimgui_capture_item_t _item2984;
        private sgimgui_capture_item_t _item2985;
        private sgimgui_capture_item_t _item2986;
        private sgimgui_capture_item_t _item2987;
        private sgimgui_capture_item_t _item2988;
        private sgimgui_capture_item_t _item2989;
        private sgimgui_capture_item_t _item2990;
        private sgimgui_capture_item_t _item2991;
        private sgimgui_capture_item_t _item2992;
        private sgimgui_capture_item_t _item2993;
        private sgimgui_capture_item_t _item2994;
        private sgimgui_capture_item_t _item2995;
        private sgimgui_capture_item_t _item2996;
        private sgimgui_capture_item_t _item2997;
        private sgimgui_capture_item_t _item2998;
        private sgimgui_capture_item_t _item2999;
        private sgimgui_capture_item_t _item3000;
        private sgimgui_capture_item_t _item3001;
        private sgimgui_capture_item_t _item3002;
        private sgimgui_capture_item_t _item3003;
        private sgimgui_capture_item_t _item3004;
        private sgimgui_capture_item_t _item3005;
        private sgimgui_capture_item_t _item3006;
        private sgimgui_capture_item_t _item3007;
        private sgimgui_capture_item_t _item3008;
        private sgimgui_capture_item_t _item3009;
        private sgimgui_capture_item_t _item3010;
        private sgimgui_capture_item_t _item3011;
        private sgimgui_capture_item_t _item3012;
        private sgimgui_capture_item_t _item3013;
        private sgimgui_capture_item_t _item3014;
        private sgimgui_capture_item_t _item3015;
        private sgimgui_capture_item_t _item3016;
        private sgimgui_capture_item_t _item3017;
        private sgimgui_capture_item_t _item3018;
        private sgimgui_capture_item_t _item3019;
        private sgimgui_capture_item_t _item3020;
        private sgimgui_capture_item_t _item3021;
        private sgimgui_capture_item_t _item3022;
        private sgimgui_capture_item_t _item3023;
        private sgimgui_capture_item_t _item3024;
        private sgimgui_capture_item_t _item3025;
        private sgimgui_capture_item_t _item3026;
        private sgimgui_capture_item_t _item3027;
        private sgimgui_capture_item_t _item3028;
        private sgimgui_capture_item_t _item3029;
        private sgimgui_capture_item_t _item3030;
        private sgimgui_capture_item_t _item3031;
        private sgimgui_capture_item_t _item3032;
        private sgimgui_capture_item_t _item3033;
        private sgimgui_capture_item_t _item3034;
        private sgimgui_capture_item_t _item3035;
        private sgimgui_capture_item_t _item3036;
        private sgimgui_capture_item_t _item3037;
        private sgimgui_capture_item_t _item3038;
        private sgimgui_capture_item_t _item3039;
        private sgimgui_capture_item_t _item3040;
        private sgimgui_capture_item_t _item3041;
        private sgimgui_capture_item_t _item3042;
        private sgimgui_capture_item_t _item3043;
        private sgimgui_capture_item_t _item3044;
        private sgimgui_capture_item_t _item3045;
        private sgimgui_capture_item_t _item3046;
        private sgimgui_capture_item_t _item3047;
        private sgimgui_capture_item_t _item3048;
        private sgimgui_capture_item_t _item3049;
        private sgimgui_capture_item_t _item3050;
        private sgimgui_capture_item_t _item3051;
        private sgimgui_capture_item_t _item3052;
        private sgimgui_capture_item_t _item3053;
        private sgimgui_capture_item_t _item3054;
        private sgimgui_capture_item_t _item3055;
        private sgimgui_capture_item_t _item3056;
        private sgimgui_capture_item_t _item3057;
        private sgimgui_capture_item_t _item3058;
        private sgimgui_capture_item_t _item3059;
        private sgimgui_capture_item_t _item3060;
        private sgimgui_capture_item_t _item3061;
        private sgimgui_capture_item_t _item3062;
        private sgimgui_capture_item_t _item3063;
        private sgimgui_capture_item_t _item3064;
        private sgimgui_capture_item_t _item3065;
        private sgimgui_capture_item_t _item3066;
        private sgimgui_capture_item_t _item3067;
        private sgimgui_capture_item_t _item3068;
        private sgimgui_capture_item_t _item3069;
        private sgimgui_capture_item_t _item3070;
        private sgimgui_capture_item_t _item3071;
        private sgimgui_capture_item_t _item3072;
        private sgimgui_capture_item_t _item3073;
        private sgimgui_capture_item_t _item3074;
        private sgimgui_capture_item_t _item3075;
        private sgimgui_capture_item_t _item3076;
        private sgimgui_capture_item_t _item3077;
        private sgimgui_capture_item_t _item3078;
        private sgimgui_capture_item_t _item3079;
        private sgimgui_capture_item_t _item3080;
        private sgimgui_capture_item_t _item3081;
        private sgimgui_capture_item_t _item3082;
        private sgimgui_capture_item_t _item3083;
        private sgimgui_capture_item_t _item3084;
        private sgimgui_capture_item_t _item3085;
        private sgimgui_capture_item_t _item3086;
        private sgimgui_capture_item_t _item3087;
        private sgimgui_capture_item_t _item3088;
        private sgimgui_capture_item_t _item3089;
        private sgimgui_capture_item_t _item3090;
        private sgimgui_capture_item_t _item3091;
        private sgimgui_capture_item_t _item3092;
        private sgimgui_capture_item_t _item3093;
        private sgimgui_capture_item_t _item3094;
        private sgimgui_capture_item_t _item3095;
        private sgimgui_capture_item_t _item3096;
        private sgimgui_capture_item_t _item3097;
        private sgimgui_capture_item_t _item3098;
        private sgimgui_capture_item_t _item3099;
        private sgimgui_capture_item_t _item3100;
        private sgimgui_capture_item_t _item3101;
        private sgimgui_capture_item_t _item3102;
        private sgimgui_capture_item_t _item3103;
        private sgimgui_capture_item_t _item3104;
        private sgimgui_capture_item_t _item3105;
        private sgimgui_capture_item_t _item3106;
        private sgimgui_capture_item_t _item3107;
        private sgimgui_capture_item_t _item3108;
        private sgimgui_capture_item_t _item3109;
        private sgimgui_capture_item_t _item3110;
        private sgimgui_capture_item_t _item3111;
        private sgimgui_capture_item_t _item3112;
        private sgimgui_capture_item_t _item3113;
        private sgimgui_capture_item_t _item3114;
        private sgimgui_capture_item_t _item3115;
        private sgimgui_capture_item_t _item3116;
        private sgimgui_capture_item_t _item3117;
        private sgimgui_capture_item_t _item3118;
        private sgimgui_capture_item_t _item3119;
        private sgimgui_capture_item_t _item3120;
        private sgimgui_capture_item_t _item3121;
        private sgimgui_capture_item_t _item3122;
        private sgimgui_capture_item_t _item3123;
        private sgimgui_capture_item_t _item3124;
        private sgimgui_capture_item_t _item3125;
        private sgimgui_capture_item_t _item3126;
        private sgimgui_capture_item_t _item3127;
        private sgimgui_capture_item_t _item3128;
        private sgimgui_capture_item_t _item3129;
        private sgimgui_capture_item_t _item3130;
        private sgimgui_capture_item_t _item3131;
        private sgimgui_capture_item_t _item3132;
        private sgimgui_capture_item_t _item3133;
        private sgimgui_capture_item_t _item3134;
        private sgimgui_capture_item_t _item3135;
        private sgimgui_capture_item_t _item3136;
        private sgimgui_capture_item_t _item3137;
        private sgimgui_capture_item_t _item3138;
        private sgimgui_capture_item_t _item3139;
        private sgimgui_capture_item_t _item3140;
        private sgimgui_capture_item_t _item3141;
        private sgimgui_capture_item_t _item3142;
        private sgimgui_capture_item_t _item3143;
        private sgimgui_capture_item_t _item3144;
        private sgimgui_capture_item_t _item3145;
        private sgimgui_capture_item_t _item3146;
        private sgimgui_capture_item_t _item3147;
        private sgimgui_capture_item_t _item3148;
        private sgimgui_capture_item_t _item3149;
        private sgimgui_capture_item_t _item3150;
        private sgimgui_capture_item_t _item3151;
        private sgimgui_capture_item_t _item3152;
        private sgimgui_capture_item_t _item3153;
        private sgimgui_capture_item_t _item3154;
        private sgimgui_capture_item_t _item3155;
        private sgimgui_capture_item_t _item3156;
        private sgimgui_capture_item_t _item3157;
        private sgimgui_capture_item_t _item3158;
        private sgimgui_capture_item_t _item3159;
        private sgimgui_capture_item_t _item3160;
        private sgimgui_capture_item_t _item3161;
        private sgimgui_capture_item_t _item3162;
        private sgimgui_capture_item_t _item3163;
        private sgimgui_capture_item_t _item3164;
        private sgimgui_capture_item_t _item3165;
        private sgimgui_capture_item_t _item3166;
        private sgimgui_capture_item_t _item3167;
        private sgimgui_capture_item_t _item3168;
        private sgimgui_capture_item_t _item3169;
        private sgimgui_capture_item_t _item3170;
        private sgimgui_capture_item_t _item3171;
        private sgimgui_capture_item_t _item3172;
        private sgimgui_capture_item_t _item3173;
        private sgimgui_capture_item_t _item3174;
        private sgimgui_capture_item_t _item3175;
        private sgimgui_capture_item_t _item3176;
        private sgimgui_capture_item_t _item3177;
        private sgimgui_capture_item_t _item3178;
        private sgimgui_capture_item_t _item3179;
        private sgimgui_capture_item_t _item3180;
        private sgimgui_capture_item_t _item3181;
        private sgimgui_capture_item_t _item3182;
        private sgimgui_capture_item_t _item3183;
        private sgimgui_capture_item_t _item3184;
        private sgimgui_capture_item_t _item3185;
        private sgimgui_capture_item_t _item3186;
        private sgimgui_capture_item_t _item3187;
        private sgimgui_capture_item_t _item3188;
        private sgimgui_capture_item_t _item3189;
        private sgimgui_capture_item_t _item3190;
        private sgimgui_capture_item_t _item3191;
        private sgimgui_capture_item_t _item3192;
        private sgimgui_capture_item_t _item3193;
        private sgimgui_capture_item_t _item3194;
        private sgimgui_capture_item_t _item3195;
        private sgimgui_capture_item_t _item3196;
        private sgimgui_capture_item_t _item3197;
        private sgimgui_capture_item_t _item3198;
        private sgimgui_capture_item_t _item3199;
        private sgimgui_capture_item_t _item3200;
        private sgimgui_capture_item_t _item3201;
        private sgimgui_capture_item_t _item3202;
        private sgimgui_capture_item_t _item3203;
        private sgimgui_capture_item_t _item3204;
        private sgimgui_capture_item_t _item3205;
        private sgimgui_capture_item_t _item3206;
        private sgimgui_capture_item_t _item3207;
        private sgimgui_capture_item_t _item3208;
        private sgimgui_capture_item_t _item3209;
        private sgimgui_capture_item_t _item3210;
        private sgimgui_capture_item_t _item3211;
        private sgimgui_capture_item_t _item3212;
        private sgimgui_capture_item_t _item3213;
        private sgimgui_capture_item_t _item3214;
        private sgimgui_capture_item_t _item3215;
        private sgimgui_capture_item_t _item3216;
        private sgimgui_capture_item_t _item3217;
        private sgimgui_capture_item_t _item3218;
        private sgimgui_capture_item_t _item3219;
        private sgimgui_capture_item_t _item3220;
        private sgimgui_capture_item_t _item3221;
        private sgimgui_capture_item_t _item3222;
        private sgimgui_capture_item_t _item3223;
        private sgimgui_capture_item_t _item3224;
        private sgimgui_capture_item_t _item3225;
        private sgimgui_capture_item_t _item3226;
        private sgimgui_capture_item_t _item3227;
        private sgimgui_capture_item_t _item3228;
        private sgimgui_capture_item_t _item3229;
        private sgimgui_capture_item_t _item3230;
        private sgimgui_capture_item_t _item3231;
        private sgimgui_capture_item_t _item3232;
        private sgimgui_capture_item_t _item3233;
        private sgimgui_capture_item_t _item3234;
        private sgimgui_capture_item_t _item3235;
        private sgimgui_capture_item_t _item3236;
        private sgimgui_capture_item_t _item3237;
        private sgimgui_capture_item_t _item3238;
        private sgimgui_capture_item_t _item3239;
        private sgimgui_capture_item_t _item3240;
        private sgimgui_capture_item_t _item3241;
        private sgimgui_capture_item_t _item3242;
        private sgimgui_capture_item_t _item3243;
        private sgimgui_capture_item_t _item3244;
        private sgimgui_capture_item_t _item3245;
        private sgimgui_capture_item_t _item3246;
        private sgimgui_capture_item_t _item3247;
        private sgimgui_capture_item_t _item3248;
        private sgimgui_capture_item_t _item3249;
        private sgimgui_capture_item_t _item3250;
        private sgimgui_capture_item_t _item3251;
        private sgimgui_capture_item_t _item3252;
        private sgimgui_capture_item_t _item3253;
        private sgimgui_capture_item_t _item3254;
        private sgimgui_capture_item_t _item3255;
        private sgimgui_capture_item_t _item3256;
        private sgimgui_capture_item_t _item3257;
        private sgimgui_capture_item_t _item3258;
        private sgimgui_capture_item_t _item3259;
        private sgimgui_capture_item_t _item3260;
        private sgimgui_capture_item_t _item3261;
        private sgimgui_capture_item_t _item3262;
        private sgimgui_capture_item_t _item3263;
        private sgimgui_capture_item_t _item3264;
        private sgimgui_capture_item_t _item3265;
        private sgimgui_capture_item_t _item3266;
        private sgimgui_capture_item_t _item3267;
        private sgimgui_capture_item_t _item3268;
        private sgimgui_capture_item_t _item3269;
        private sgimgui_capture_item_t _item3270;
        private sgimgui_capture_item_t _item3271;
        private sgimgui_capture_item_t _item3272;
        private sgimgui_capture_item_t _item3273;
        private sgimgui_capture_item_t _item3274;
        private sgimgui_capture_item_t _item3275;
        private sgimgui_capture_item_t _item3276;
        private sgimgui_capture_item_t _item3277;
        private sgimgui_capture_item_t _item3278;
        private sgimgui_capture_item_t _item3279;
        private sgimgui_capture_item_t _item3280;
        private sgimgui_capture_item_t _item3281;
        private sgimgui_capture_item_t _item3282;
        private sgimgui_capture_item_t _item3283;
        private sgimgui_capture_item_t _item3284;
        private sgimgui_capture_item_t _item3285;
        private sgimgui_capture_item_t _item3286;
        private sgimgui_capture_item_t _item3287;
        private sgimgui_capture_item_t _item3288;
        private sgimgui_capture_item_t _item3289;
        private sgimgui_capture_item_t _item3290;
        private sgimgui_capture_item_t _item3291;
        private sgimgui_capture_item_t _item3292;
        private sgimgui_capture_item_t _item3293;
        private sgimgui_capture_item_t _item3294;
        private sgimgui_capture_item_t _item3295;
        private sgimgui_capture_item_t _item3296;
        private sgimgui_capture_item_t _item3297;
        private sgimgui_capture_item_t _item3298;
        private sgimgui_capture_item_t _item3299;
        private sgimgui_capture_item_t _item3300;
        private sgimgui_capture_item_t _item3301;
        private sgimgui_capture_item_t _item3302;
        private sgimgui_capture_item_t _item3303;
        private sgimgui_capture_item_t _item3304;
        private sgimgui_capture_item_t _item3305;
        private sgimgui_capture_item_t _item3306;
        private sgimgui_capture_item_t _item3307;
        private sgimgui_capture_item_t _item3308;
        private sgimgui_capture_item_t _item3309;
        private sgimgui_capture_item_t _item3310;
        private sgimgui_capture_item_t _item3311;
        private sgimgui_capture_item_t _item3312;
        private sgimgui_capture_item_t _item3313;
        private sgimgui_capture_item_t _item3314;
        private sgimgui_capture_item_t _item3315;
        private sgimgui_capture_item_t _item3316;
        private sgimgui_capture_item_t _item3317;
        private sgimgui_capture_item_t _item3318;
        private sgimgui_capture_item_t _item3319;
        private sgimgui_capture_item_t _item3320;
        private sgimgui_capture_item_t _item3321;
        private sgimgui_capture_item_t _item3322;
        private sgimgui_capture_item_t _item3323;
        private sgimgui_capture_item_t _item3324;
        private sgimgui_capture_item_t _item3325;
        private sgimgui_capture_item_t _item3326;
        private sgimgui_capture_item_t _item3327;
        private sgimgui_capture_item_t _item3328;
        private sgimgui_capture_item_t _item3329;
        private sgimgui_capture_item_t _item3330;
        private sgimgui_capture_item_t _item3331;
        private sgimgui_capture_item_t _item3332;
        private sgimgui_capture_item_t _item3333;
        private sgimgui_capture_item_t _item3334;
        private sgimgui_capture_item_t _item3335;
        private sgimgui_capture_item_t _item3336;
        private sgimgui_capture_item_t _item3337;
        private sgimgui_capture_item_t _item3338;
        private sgimgui_capture_item_t _item3339;
        private sgimgui_capture_item_t _item3340;
        private sgimgui_capture_item_t _item3341;
        private sgimgui_capture_item_t _item3342;
        private sgimgui_capture_item_t _item3343;
        private sgimgui_capture_item_t _item3344;
        private sgimgui_capture_item_t _item3345;
        private sgimgui_capture_item_t _item3346;
        private sgimgui_capture_item_t _item3347;
        private sgimgui_capture_item_t _item3348;
        private sgimgui_capture_item_t _item3349;
        private sgimgui_capture_item_t _item3350;
        private sgimgui_capture_item_t _item3351;
        private sgimgui_capture_item_t _item3352;
        private sgimgui_capture_item_t _item3353;
        private sgimgui_capture_item_t _item3354;
        private sgimgui_capture_item_t _item3355;
        private sgimgui_capture_item_t _item3356;
        private sgimgui_capture_item_t _item3357;
        private sgimgui_capture_item_t _item3358;
        private sgimgui_capture_item_t _item3359;
        private sgimgui_capture_item_t _item3360;
        private sgimgui_capture_item_t _item3361;
        private sgimgui_capture_item_t _item3362;
        private sgimgui_capture_item_t _item3363;
        private sgimgui_capture_item_t _item3364;
        private sgimgui_capture_item_t _item3365;
        private sgimgui_capture_item_t _item3366;
        private sgimgui_capture_item_t _item3367;
        private sgimgui_capture_item_t _item3368;
        private sgimgui_capture_item_t _item3369;
        private sgimgui_capture_item_t _item3370;
        private sgimgui_capture_item_t _item3371;
        private sgimgui_capture_item_t _item3372;
        private sgimgui_capture_item_t _item3373;
        private sgimgui_capture_item_t _item3374;
        private sgimgui_capture_item_t _item3375;
        private sgimgui_capture_item_t _item3376;
        private sgimgui_capture_item_t _item3377;
        private sgimgui_capture_item_t _item3378;
        private sgimgui_capture_item_t _item3379;
        private sgimgui_capture_item_t _item3380;
        private sgimgui_capture_item_t _item3381;
        private sgimgui_capture_item_t _item3382;
        private sgimgui_capture_item_t _item3383;
        private sgimgui_capture_item_t _item3384;
        private sgimgui_capture_item_t _item3385;
        private sgimgui_capture_item_t _item3386;
        private sgimgui_capture_item_t _item3387;
        private sgimgui_capture_item_t _item3388;
        private sgimgui_capture_item_t _item3389;
        private sgimgui_capture_item_t _item3390;
        private sgimgui_capture_item_t _item3391;
        private sgimgui_capture_item_t _item3392;
        private sgimgui_capture_item_t _item3393;
        private sgimgui_capture_item_t _item3394;
        private sgimgui_capture_item_t _item3395;
        private sgimgui_capture_item_t _item3396;
        private sgimgui_capture_item_t _item3397;
        private sgimgui_capture_item_t _item3398;
        private sgimgui_capture_item_t _item3399;
        private sgimgui_capture_item_t _item3400;
        private sgimgui_capture_item_t _item3401;
        private sgimgui_capture_item_t _item3402;
        private sgimgui_capture_item_t _item3403;
        private sgimgui_capture_item_t _item3404;
        private sgimgui_capture_item_t _item3405;
        private sgimgui_capture_item_t _item3406;
        private sgimgui_capture_item_t _item3407;
        private sgimgui_capture_item_t _item3408;
        private sgimgui_capture_item_t _item3409;
        private sgimgui_capture_item_t _item3410;
        private sgimgui_capture_item_t _item3411;
        private sgimgui_capture_item_t _item3412;
        private sgimgui_capture_item_t _item3413;
        private sgimgui_capture_item_t _item3414;
        private sgimgui_capture_item_t _item3415;
        private sgimgui_capture_item_t _item3416;
        private sgimgui_capture_item_t _item3417;
        private sgimgui_capture_item_t _item3418;
        private sgimgui_capture_item_t _item3419;
        private sgimgui_capture_item_t _item3420;
        private sgimgui_capture_item_t _item3421;
        private sgimgui_capture_item_t _item3422;
        private sgimgui_capture_item_t _item3423;
        private sgimgui_capture_item_t _item3424;
        private sgimgui_capture_item_t _item3425;
        private sgimgui_capture_item_t _item3426;
        private sgimgui_capture_item_t _item3427;
        private sgimgui_capture_item_t _item3428;
        private sgimgui_capture_item_t _item3429;
        private sgimgui_capture_item_t _item3430;
        private sgimgui_capture_item_t _item3431;
        private sgimgui_capture_item_t _item3432;
        private sgimgui_capture_item_t _item3433;
        private sgimgui_capture_item_t _item3434;
        private sgimgui_capture_item_t _item3435;
        private sgimgui_capture_item_t _item3436;
        private sgimgui_capture_item_t _item3437;
        private sgimgui_capture_item_t _item3438;
        private sgimgui_capture_item_t _item3439;
        private sgimgui_capture_item_t _item3440;
        private sgimgui_capture_item_t _item3441;
        private sgimgui_capture_item_t _item3442;
        private sgimgui_capture_item_t _item3443;
        private sgimgui_capture_item_t _item3444;
        private sgimgui_capture_item_t _item3445;
        private sgimgui_capture_item_t _item3446;
        private sgimgui_capture_item_t _item3447;
        private sgimgui_capture_item_t _item3448;
        private sgimgui_capture_item_t _item3449;
        private sgimgui_capture_item_t _item3450;
        private sgimgui_capture_item_t _item3451;
        private sgimgui_capture_item_t _item3452;
        private sgimgui_capture_item_t _item3453;
        private sgimgui_capture_item_t _item3454;
        private sgimgui_capture_item_t _item3455;
        private sgimgui_capture_item_t _item3456;
        private sgimgui_capture_item_t _item3457;
        private sgimgui_capture_item_t _item3458;
        private sgimgui_capture_item_t _item3459;
        private sgimgui_capture_item_t _item3460;
        private sgimgui_capture_item_t _item3461;
        private sgimgui_capture_item_t _item3462;
        private sgimgui_capture_item_t _item3463;
        private sgimgui_capture_item_t _item3464;
        private sgimgui_capture_item_t _item3465;
        private sgimgui_capture_item_t _item3466;
        private sgimgui_capture_item_t _item3467;
        private sgimgui_capture_item_t _item3468;
        private sgimgui_capture_item_t _item3469;
        private sgimgui_capture_item_t _item3470;
        private sgimgui_capture_item_t _item3471;
        private sgimgui_capture_item_t _item3472;
        private sgimgui_capture_item_t _item3473;
        private sgimgui_capture_item_t _item3474;
        private sgimgui_capture_item_t _item3475;
        private sgimgui_capture_item_t _item3476;
        private sgimgui_capture_item_t _item3477;
        private sgimgui_capture_item_t _item3478;
        private sgimgui_capture_item_t _item3479;
        private sgimgui_capture_item_t _item3480;
        private sgimgui_capture_item_t _item3481;
        private sgimgui_capture_item_t _item3482;
        private sgimgui_capture_item_t _item3483;
        private sgimgui_capture_item_t _item3484;
        private sgimgui_capture_item_t _item3485;
        private sgimgui_capture_item_t _item3486;
        private sgimgui_capture_item_t _item3487;
        private sgimgui_capture_item_t _item3488;
        private sgimgui_capture_item_t _item3489;
        private sgimgui_capture_item_t _item3490;
        private sgimgui_capture_item_t _item3491;
        private sgimgui_capture_item_t _item3492;
        private sgimgui_capture_item_t _item3493;
        private sgimgui_capture_item_t _item3494;
        private sgimgui_capture_item_t _item3495;
        private sgimgui_capture_item_t _item3496;
        private sgimgui_capture_item_t _item3497;
        private sgimgui_capture_item_t _item3498;
        private sgimgui_capture_item_t _item3499;
        private sgimgui_capture_item_t _item3500;
        private sgimgui_capture_item_t _item3501;
        private sgimgui_capture_item_t _item3502;
        private sgimgui_capture_item_t _item3503;
        private sgimgui_capture_item_t _item3504;
        private sgimgui_capture_item_t _item3505;
        private sgimgui_capture_item_t _item3506;
        private sgimgui_capture_item_t _item3507;
        private sgimgui_capture_item_t _item3508;
        private sgimgui_capture_item_t _item3509;
        private sgimgui_capture_item_t _item3510;
        private sgimgui_capture_item_t _item3511;
        private sgimgui_capture_item_t _item3512;
        private sgimgui_capture_item_t _item3513;
        private sgimgui_capture_item_t _item3514;
        private sgimgui_capture_item_t _item3515;
        private sgimgui_capture_item_t _item3516;
        private sgimgui_capture_item_t _item3517;
        private sgimgui_capture_item_t _item3518;
        private sgimgui_capture_item_t _item3519;
        private sgimgui_capture_item_t _item3520;
        private sgimgui_capture_item_t _item3521;
        private sgimgui_capture_item_t _item3522;
        private sgimgui_capture_item_t _item3523;
        private sgimgui_capture_item_t _item3524;
        private sgimgui_capture_item_t _item3525;
        private sgimgui_capture_item_t _item3526;
        private sgimgui_capture_item_t _item3527;
        private sgimgui_capture_item_t _item3528;
        private sgimgui_capture_item_t _item3529;
        private sgimgui_capture_item_t _item3530;
        private sgimgui_capture_item_t _item3531;
        private sgimgui_capture_item_t _item3532;
        private sgimgui_capture_item_t _item3533;
        private sgimgui_capture_item_t _item3534;
        private sgimgui_capture_item_t _item3535;
        private sgimgui_capture_item_t _item3536;
        private sgimgui_capture_item_t _item3537;
        private sgimgui_capture_item_t _item3538;
        private sgimgui_capture_item_t _item3539;
        private sgimgui_capture_item_t _item3540;
        private sgimgui_capture_item_t _item3541;
        private sgimgui_capture_item_t _item3542;
        private sgimgui_capture_item_t _item3543;
        private sgimgui_capture_item_t _item3544;
        private sgimgui_capture_item_t _item3545;
        private sgimgui_capture_item_t _item3546;
        private sgimgui_capture_item_t _item3547;
        private sgimgui_capture_item_t _item3548;
        private sgimgui_capture_item_t _item3549;
        private sgimgui_capture_item_t _item3550;
        private sgimgui_capture_item_t _item3551;
        private sgimgui_capture_item_t _item3552;
        private sgimgui_capture_item_t _item3553;
        private sgimgui_capture_item_t _item3554;
        private sgimgui_capture_item_t _item3555;
        private sgimgui_capture_item_t _item3556;
        private sgimgui_capture_item_t _item3557;
        private sgimgui_capture_item_t _item3558;
        private sgimgui_capture_item_t _item3559;
        private sgimgui_capture_item_t _item3560;
        private sgimgui_capture_item_t _item3561;
        private sgimgui_capture_item_t _item3562;
        private sgimgui_capture_item_t _item3563;
        private sgimgui_capture_item_t _item3564;
        private sgimgui_capture_item_t _item3565;
        private sgimgui_capture_item_t _item3566;
        private sgimgui_capture_item_t _item3567;
        private sgimgui_capture_item_t _item3568;
        private sgimgui_capture_item_t _item3569;
        private sgimgui_capture_item_t _item3570;
        private sgimgui_capture_item_t _item3571;
        private sgimgui_capture_item_t _item3572;
        private sgimgui_capture_item_t _item3573;
        private sgimgui_capture_item_t _item3574;
        private sgimgui_capture_item_t _item3575;
        private sgimgui_capture_item_t _item3576;
        private sgimgui_capture_item_t _item3577;
        private sgimgui_capture_item_t _item3578;
        private sgimgui_capture_item_t _item3579;
        private sgimgui_capture_item_t _item3580;
        private sgimgui_capture_item_t _item3581;
        private sgimgui_capture_item_t _item3582;
        private sgimgui_capture_item_t _item3583;
        private sgimgui_capture_item_t _item3584;
        private sgimgui_capture_item_t _item3585;
        private sgimgui_capture_item_t _item3586;
        private sgimgui_capture_item_t _item3587;
        private sgimgui_capture_item_t _item3588;
        private sgimgui_capture_item_t _item3589;
        private sgimgui_capture_item_t _item3590;
        private sgimgui_capture_item_t _item3591;
        private sgimgui_capture_item_t _item3592;
        private sgimgui_capture_item_t _item3593;
        private sgimgui_capture_item_t _item3594;
        private sgimgui_capture_item_t _item3595;
        private sgimgui_capture_item_t _item3596;
        private sgimgui_capture_item_t _item3597;
        private sgimgui_capture_item_t _item3598;
        private sgimgui_capture_item_t _item3599;
        private sgimgui_capture_item_t _item3600;
        private sgimgui_capture_item_t _item3601;
        private sgimgui_capture_item_t _item3602;
        private sgimgui_capture_item_t _item3603;
        private sgimgui_capture_item_t _item3604;
        private sgimgui_capture_item_t _item3605;
        private sgimgui_capture_item_t _item3606;
        private sgimgui_capture_item_t _item3607;
        private sgimgui_capture_item_t _item3608;
        private sgimgui_capture_item_t _item3609;
        private sgimgui_capture_item_t _item3610;
        private sgimgui_capture_item_t _item3611;
        private sgimgui_capture_item_t _item3612;
        private sgimgui_capture_item_t _item3613;
        private sgimgui_capture_item_t _item3614;
        private sgimgui_capture_item_t _item3615;
        private sgimgui_capture_item_t _item3616;
        private sgimgui_capture_item_t _item3617;
        private sgimgui_capture_item_t _item3618;
        private sgimgui_capture_item_t _item3619;
        private sgimgui_capture_item_t _item3620;
        private sgimgui_capture_item_t _item3621;
        private sgimgui_capture_item_t _item3622;
        private sgimgui_capture_item_t _item3623;
        private sgimgui_capture_item_t _item3624;
        private sgimgui_capture_item_t _item3625;
        private sgimgui_capture_item_t _item3626;
        private sgimgui_capture_item_t _item3627;
        private sgimgui_capture_item_t _item3628;
        private sgimgui_capture_item_t _item3629;
        private sgimgui_capture_item_t _item3630;
        private sgimgui_capture_item_t _item3631;
        private sgimgui_capture_item_t _item3632;
        private sgimgui_capture_item_t _item3633;
        private sgimgui_capture_item_t _item3634;
        private sgimgui_capture_item_t _item3635;
        private sgimgui_capture_item_t _item3636;
        private sgimgui_capture_item_t _item3637;
        private sgimgui_capture_item_t _item3638;
        private sgimgui_capture_item_t _item3639;
        private sgimgui_capture_item_t _item3640;
        private sgimgui_capture_item_t _item3641;
        private sgimgui_capture_item_t _item3642;
        private sgimgui_capture_item_t _item3643;
        private sgimgui_capture_item_t _item3644;
        private sgimgui_capture_item_t _item3645;
        private sgimgui_capture_item_t _item3646;
        private sgimgui_capture_item_t _item3647;
        private sgimgui_capture_item_t _item3648;
        private sgimgui_capture_item_t _item3649;
        private sgimgui_capture_item_t _item3650;
        private sgimgui_capture_item_t _item3651;
        private sgimgui_capture_item_t _item3652;
        private sgimgui_capture_item_t _item3653;
        private sgimgui_capture_item_t _item3654;
        private sgimgui_capture_item_t _item3655;
        private sgimgui_capture_item_t _item3656;
        private sgimgui_capture_item_t _item3657;
        private sgimgui_capture_item_t _item3658;
        private sgimgui_capture_item_t _item3659;
        private sgimgui_capture_item_t _item3660;
        private sgimgui_capture_item_t _item3661;
        private sgimgui_capture_item_t _item3662;
        private sgimgui_capture_item_t _item3663;
        private sgimgui_capture_item_t _item3664;
        private sgimgui_capture_item_t _item3665;
        private sgimgui_capture_item_t _item3666;
        private sgimgui_capture_item_t _item3667;
        private sgimgui_capture_item_t _item3668;
        private sgimgui_capture_item_t _item3669;
        private sgimgui_capture_item_t _item3670;
        private sgimgui_capture_item_t _item3671;
        private sgimgui_capture_item_t _item3672;
        private sgimgui_capture_item_t _item3673;
        private sgimgui_capture_item_t _item3674;
        private sgimgui_capture_item_t _item3675;
        private sgimgui_capture_item_t _item3676;
        private sgimgui_capture_item_t _item3677;
        private sgimgui_capture_item_t _item3678;
        private sgimgui_capture_item_t _item3679;
        private sgimgui_capture_item_t _item3680;
        private sgimgui_capture_item_t _item3681;
        private sgimgui_capture_item_t _item3682;
        private sgimgui_capture_item_t _item3683;
        private sgimgui_capture_item_t _item3684;
        private sgimgui_capture_item_t _item3685;
        private sgimgui_capture_item_t _item3686;
        private sgimgui_capture_item_t _item3687;
        private sgimgui_capture_item_t _item3688;
        private sgimgui_capture_item_t _item3689;
        private sgimgui_capture_item_t _item3690;
        private sgimgui_capture_item_t _item3691;
        private sgimgui_capture_item_t _item3692;
        private sgimgui_capture_item_t _item3693;
        private sgimgui_capture_item_t _item3694;
        private sgimgui_capture_item_t _item3695;
        private sgimgui_capture_item_t _item3696;
        private sgimgui_capture_item_t _item3697;
        private sgimgui_capture_item_t _item3698;
        private sgimgui_capture_item_t _item3699;
        private sgimgui_capture_item_t _item3700;
        private sgimgui_capture_item_t _item3701;
        private sgimgui_capture_item_t _item3702;
        private sgimgui_capture_item_t _item3703;
        private sgimgui_capture_item_t _item3704;
        private sgimgui_capture_item_t _item3705;
        private sgimgui_capture_item_t _item3706;
        private sgimgui_capture_item_t _item3707;
        private sgimgui_capture_item_t _item3708;
        private sgimgui_capture_item_t _item3709;
        private sgimgui_capture_item_t _item3710;
        private sgimgui_capture_item_t _item3711;
        private sgimgui_capture_item_t _item3712;
        private sgimgui_capture_item_t _item3713;
        private sgimgui_capture_item_t _item3714;
        private sgimgui_capture_item_t _item3715;
        private sgimgui_capture_item_t _item3716;
        private sgimgui_capture_item_t _item3717;
        private sgimgui_capture_item_t _item3718;
        private sgimgui_capture_item_t _item3719;
        private sgimgui_capture_item_t _item3720;
        private sgimgui_capture_item_t _item3721;
        private sgimgui_capture_item_t _item3722;
        private sgimgui_capture_item_t _item3723;
        private sgimgui_capture_item_t _item3724;
        private sgimgui_capture_item_t _item3725;
        private sgimgui_capture_item_t _item3726;
        private sgimgui_capture_item_t _item3727;
        private sgimgui_capture_item_t _item3728;
        private sgimgui_capture_item_t _item3729;
        private sgimgui_capture_item_t _item3730;
        private sgimgui_capture_item_t _item3731;
        private sgimgui_capture_item_t _item3732;
        private sgimgui_capture_item_t _item3733;
        private sgimgui_capture_item_t _item3734;
        private sgimgui_capture_item_t _item3735;
        private sgimgui_capture_item_t _item3736;
        private sgimgui_capture_item_t _item3737;
        private sgimgui_capture_item_t _item3738;
        private sgimgui_capture_item_t _item3739;
        private sgimgui_capture_item_t _item3740;
        private sgimgui_capture_item_t _item3741;
        private sgimgui_capture_item_t _item3742;
        private sgimgui_capture_item_t _item3743;
        private sgimgui_capture_item_t _item3744;
        private sgimgui_capture_item_t _item3745;
        private sgimgui_capture_item_t _item3746;
        private sgimgui_capture_item_t _item3747;
        private sgimgui_capture_item_t _item3748;
        private sgimgui_capture_item_t _item3749;
        private sgimgui_capture_item_t _item3750;
        private sgimgui_capture_item_t _item3751;
        private sgimgui_capture_item_t _item3752;
        private sgimgui_capture_item_t _item3753;
        private sgimgui_capture_item_t _item3754;
        private sgimgui_capture_item_t _item3755;
        private sgimgui_capture_item_t _item3756;
        private sgimgui_capture_item_t _item3757;
        private sgimgui_capture_item_t _item3758;
        private sgimgui_capture_item_t _item3759;
        private sgimgui_capture_item_t _item3760;
        private sgimgui_capture_item_t _item3761;
        private sgimgui_capture_item_t _item3762;
        private sgimgui_capture_item_t _item3763;
        private sgimgui_capture_item_t _item3764;
        private sgimgui_capture_item_t _item3765;
        private sgimgui_capture_item_t _item3766;
        private sgimgui_capture_item_t _item3767;
        private sgimgui_capture_item_t _item3768;
        private sgimgui_capture_item_t _item3769;
        private sgimgui_capture_item_t _item3770;
        private sgimgui_capture_item_t _item3771;
        private sgimgui_capture_item_t _item3772;
        private sgimgui_capture_item_t _item3773;
        private sgimgui_capture_item_t _item3774;
        private sgimgui_capture_item_t _item3775;
        private sgimgui_capture_item_t _item3776;
        private sgimgui_capture_item_t _item3777;
        private sgimgui_capture_item_t _item3778;
        private sgimgui_capture_item_t _item3779;
        private sgimgui_capture_item_t _item3780;
        private sgimgui_capture_item_t _item3781;
        private sgimgui_capture_item_t _item3782;
        private sgimgui_capture_item_t _item3783;
        private sgimgui_capture_item_t _item3784;
        private sgimgui_capture_item_t _item3785;
        private sgimgui_capture_item_t _item3786;
        private sgimgui_capture_item_t _item3787;
        private sgimgui_capture_item_t _item3788;
        private sgimgui_capture_item_t _item3789;
        private sgimgui_capture_item_t _item3790;
        private sgimgui_capture_item_t _item3791;
        private sgimgui_capture_item_t _item3792;
        private sgimgui_capture_item_t _item3793;
        private sgimgui_capture_item_t _item3794;
        private sgimgui_capture_item_t _item3795;
        private sgimgui_capture_item_t _item3796;
        private sgimgui_capture_item_t _item3797;
        private sgimgui_capture_item_t _item3798;
        private sgimgui_capture_item_t _item3799;
        private sgimgui_capture_item_t _item3800;
        private sgimgui_capture_item_t _item3801;
        private sgimgui_capture_item_t _item3802;
        private sgimgui_capture_item_t _item3803;
        private sgimgui_capture_item_t _item3804;
        private sgimgui_capture_item_t _item3805;
        private sgimgui_capture_item_t _item3806;
        private sgimgui_capture_item_t _item3807;
        private sgimgui_capture_item_t _item3808;
        private sgimgui_capture_item_t _item3809;
        private sgimgui_capture_item_t _item3810;
        private sgimgui_capture_item_t _item3811;
        private sgimgui_capture_item_t _item3812;
        private sgimgui_capture_item_t _item3813;
        private sgimgui_capture_item_t _item3814;
        private sgimgui_capture_item_t _item3815;
        private sgimgui_capture_item_t _item3816;
        private sgimgui_capture_item_t _item3817;
        private sgimgui_capture_item_t _item3818;
        private sgimgui_capture_item_t _item3819;
        private sgimgui_capture_item_t _item3820;
        private sgimgui_capture_item_t _item3821;
        private sgimgui_capture_item_t _item3822;
        private sgimgui_capture_item_t _item3823;
        private sgimgui_capture_item_t _item3824;
        private sgimgui_capture_item_t _item3825;
        private sgimgui_capture_item_t _item3826;
        private sgimgui_capture_item_t _item3827;
        private sgimgui_capture_item_t _item3828;
        private sgimgui_capture_item_t _item3829;
        private sgimgui_capture_item_t _item3830;
        private sgimgui_capture_item_t _item3831;
        private sgimgui_capture_item_t _item3832;
        private sgimgui_capture_item_t _item3833;
        private sgimgui_capture_item_t _item3834;
        private sgimgui_capture_item_t _item3835;
        private sgimgui_capture_item_t _item3836;
        private sgimgui_capture_item_t _item3837;
        private sgimgui_capture_item_t _item3838;
        private sgimgui_capture_item_t _item3839;
        private sgimgui_capture_item_t _item3840;
        private sgimgui_capture_item_t _item3841;
        private sgimgui_capture_item_t _item3842;
        private sgimgui_capture_item_t _item3843;
        private sgimgui_capture_item_t _item3844;
        private sgimgui_capture_item_t _item3845;
        private sgimgui_capture_item_t _item3846;
        private sgimgui_capture_item_t _item3847;
        private sgimgui_capture_item_t _item3848;
        private sgimgui_capture_item_t _item3849;
        private sgimgui_capture_item_t _item3850;
        private sgimgui_capture_item_t _item3851;
        private sgimgui_capture_item_t _item3852;
        private sgimgui_capture_item_t _item3853;
        private sgimgui_capture_item_t _item3854;
        private sgimgui_capture_item_t _item3855;
        private sgimgui_capture_item_t _item3856;
        private sgimgui_capture_item_t _item3857;
        private sgimgui_capture_item_t _item3858;
        private sgimgui_capture_item_t _item3859;
        private sgimgui_capture_item_t _item3860;
        private sgimgui_capture_item_t _item3861;
        private sgimgui_capture_item_t _item3862;
        private sgimgui_capture_item_t _item3863;
        private sgimgui_capture_item_t _item3864;
        private sgimgui_capture_item_t _item3865;
        private sgimgui_capture_item_t _item3866;
        private sgimgui_capture_item_t _item3867;
        private sgimgui_capture_item_t _item3868;
        private sgimgui_capture_item_t _item3869;
        private sgimgui_capture_item_t _item3870;
        private sgimgui_capture_item_t _item3871;
        private sgimgui_capture_item_t _item3872;
        private sgimgui_capture_item_t _item3873;
        private sgimgui_capture_item_t _item3874;
        private sgimgui_capture_item_t _item3875;
        private sgimgui_capture_item_t _item3876;
        private sgimgui_capture_item_t _item3877;
        private sgimgui_capture_item_t _item3878;
        private sgimgui_capture_item_t _item3879;
        private sgimgui_capture_item_t _item3880;
        private sgimgui_capture_item_t _item3881;
        private sgimgui_capture_item_t _item3882;
        private sgimgui_capture_item_t _item3883;
        private sgimgui_capture_item_t _item3884;
        private sgimgui_capture_item_t _item3885;
        private sgimgui_capture_item_t _item3886;
        private sgimgui_capture_item_t _item3887;
        private sgimgui_capture_item_t _item3888;
        private sgimgui_capture_item_t _item3889;
        private sgimgui_capture_item_t _item3890;
        private sgimgui_capture_item_t _item3891;
        private sgimgui_capture_item_t _item3892;
        private sgimgui_capture_item_t _item3893;
        private sgimgui_capture_item_t _item3894;
        private sgimgui_capture_item_t _item3895;
        private sgimgui_capture_item_t _item3896;
        private sgimgui_capture_item_t _item3897;
        private sgimgui_capture_item_t _item3898;
        private sgimgui_capture_item_t _item3899;
        private sgimgui_capture_item_t _item3900;
        private sgimgui_capture_item_t _item3901;
        private sgimgui_capture_item_t _item3902;
        private sgimgui_capture_item_t _item3903;
        private sgimgui_capture_item_t _item3904;
        private sgimgui_capture_item_t _item3905;
        private sgimgui_capture_item_t _item3906;
        private sgimgui_capture_item_t _item3907;
        private sgimgui_capture_item_t _item3908;
        private sgimgui_capture_item_t _item3909;
        private sgimgui_capture_item_t _item3910;
        private sgimgui_capture_item_t _item3911;
        private sgimgui_capture_item_t _item3912;
        private sgimgui_capture_item_t _item3913;
        private sgimgui_capture_item_t _item3914;
        private sgimgui_capture_item_t _item3915;
        private sgimgui_capture_item_t _item3916;
        private sgimgui_capture_item_t _item3917;
        private sgimgui_capture_item_t _item3918;
        private sgimgui_capture_item_t _item3919;
        private sgimgui_capture_item_t _item3920;
        private sgimgui_capture_item_t _item3921;
        private sgimgui_capture_item_t _item3922;
        private sgimgui_capture_item_t _item3923;
        private sgimgui_capture_item_t _item3924;
        private sgimgui_capture_item_t _item3925;
        private sgimgui_capture_item_t _item3926;
        private sgimgui_capture_item_t _item3927;
        private sgimgui_capture_item_t _item3928;
        private sgimgui_capture_item_t _item3929;
        private sgimgui_capture_item_t _item3930;
        private sgimgui_capture_item_t _item3931;
        private sgimgui_capture_item_t _item3932;
        private sgimgui_capture_item_t _item3933;
        private sgimgui_capture_item_t _item3934;
        private sgimgui_capture_item_t _item3935;
        private sgimgui_capture_item_t _item3936;
        private sgimgui_capture_item_t _item3937;
        private sgimgui_capture_item_t _item3938;
        private sgimgui_capture_item_t _item3939;
        private sgimgui_capture_item_t _item3940;
        private sgimgui_capture_item_t _item3941;
        private sgimgui_capture_item_t _item3942;
        private sgimgui_capture_item_t _item3943;
        private sgimgui_capture_item_t _item3944;
        private sgimgui_capture_item_t _item3945;
        private sgimgui_capture_item_t _item3946;
        private sgimgui_capture_item_t _item3947;
        private sgimgui_capture_item_t _item3948;
        private sgimgui_capture_item_t _item3949;
        private sgimgui_capture_item_t _item3950;
        private sgimgui_capture_item_t _item3951;
        private sgimgui_capture_item_t _item3952;
        private sgimgui_capture_item_t _item3953;
        private sgimgui_capture_item_t _item3954;
        private sgimgui_capture_item_t _item3955;
        private sgimgui_capture_item_t _item3956;
        private sgimgui_capture_item_t _item3957;
        private sgimgui_capture_item_t _item3958;
        private sgimgui_capture_item_t _item3959;
        private sgimgui_capture_item_t _item3960;
        private sgimgui_capture_item_t _item3961;
        private sgimgui_capture_item_t _item3962;
        private sgimgui_capture_item_t _item3963;
        private sgimgui_capture_item_t _item3964;
        private sgimgui_capture_item_t _item3965;
        private sgimgui_capture_item_t _item3966;
        private sgimgui_capture_item_t _item3967;
        private sgimgui_capture_item_t _item3968;
        private sgimgui_capture_item_t _item3969;
        private sgimgui_capture_item_t _item3970;
        private sgimgui_capture_item_t _item3971;
        private sgimgui_capture_item_t _item3972;
        private sgimgui_capture_item_t _item3973;
        private sgimgui_capture_item_t _item3974;
        private sgimgui_capture_item_t _item3975;
        private sgimgui_capture_item_t _item3976;
        private sgimgui_capture_item_t _item3977;
        private sgimgui_capture_item_t _item3978;
        private sgimgui_capture_item_t _item3979;
        private sgimgui_capture_item_t _item3980;
        private sgimgui_capture_item_t _item3981;
        private sgimgui_capture_item_t _item3982;
        private sgimgui_capture_item_t _item3983;
        private sgimgui_capture_item_t _item3984;
        private sgimgui_capture_item_t _item3985;
        private sgimgui_capture_item_t _item3986;
        private sgimgui_capture_item_t _item3987;
        private sgimgui_capture_item_t _item3988;
        private sgimgui_capture_item_t _item3989;
        private sgimgui_capture_item_t _item3990;
        private sgimgui_capture_item_t _item3991;
        private sgimgui_capture_item_t _item3992;
        private sgimgui_capture_item_t _item3993;
        private sgimgui_capture_item_t _item3994;
        private sgimgui_capture_item_t _item3995;
        private sgimgui_capture_item_t _item3996;
        private sgimgui_capture_item_t _item3997;
        private sgimgui_capture_item_t _item3998;
        private sgimgui_capture_item_t _item3999;
        private sgimgui_capture_item_t _item4000;
        private sgimgui_capture_item_t _item4001;
        private sgimgui_capture_item_t _item4002;
        private sgimgui_capture_item_t _item4003;
        private sgimgui_capture_item_t _item4004;
        private sgimgui_capture_item_t _item4005;
        private sgimgui_capture_item_t _item4006;
        private sgimgui_capture_item_t _item4007;
        private sgimgui_capture_item_t _item4008;
        private sgimgui_capture_item_t _item4009;
        private sgimgui_capture_item_t _item4010;
        private sgimgui_capture_item_t _item4011;
        private sgimgui_capture_item_t _item4012;
        private sgimgui_capture_item_t _item4013;
        private sgimgui_capture_item_t _item4014;
        private sgimgui_capture_item_t _item4015;
        private sgimgui_capture_item_t _item4016;
        private sgimgui_capture_item_t _item4017;
        private sgimgui_capture_item_t _item4018;
        private sgimgui_capture_item_t _item4019;
        private sgimgui_capture_item_t _item4020;
        private sgimgui_capture_item_t _item4021;
        private sgimgui_capture_item_t _item4022;
        private sgimgui_capture_item_t _item4023;
        private sgimgui_capture_item_t _item4024;
        private sgimgui_capture_item_t _item4025;
        private sgimgui_capture_item_t _item4026;
        private sgimgui_capture_item_t _item4027;
        private sgimgui_capture_item_t _item4028;
        private sgimgui_capture_item_t _item4029;
        private sgimgui_capture_item_t _item4030;
        private sgimgui_capture_item_t _item4031;
        private sgimgui_capture_item_t _item4032;
        private sgimgui_capture_item_t _item4033;
        private sgimgui_capture_item_t _item4034;
        private sgimgui_capture_item_t _item4035;
        private sgimgui_capture_item_t _item4036;
        private sgimgui_capture_item_t _item4037;
        private sgimgui_capture_item_t _item4038;
        private sgimgui_capture_item_t _item4039;
        private sgimgui_capture_item_t _item4040;
        private sgimgui_capture_item_t _item4041;
        private sgimgui_capture_item_t _item4042;
        private sgimgui_capture_item_t _item4043;
        private sgimgui_capture_item_t _item4044;
        private sgimgui_capture_item_t _item4045;
        private sgimgui_capture_item_t _item4046;
        private sgimgui_capture_item_t _item4047;
        private sgimgui_capture_item_t _item4048;
        private sgimgui_capture_item_t _item4049;
        private sgimgui_capture_item_t _item4050;
        private sgimgui_capture_item_t _item4051;
        private sgimgui_capture_item_t _item4052;
        private sgimgui_capture_item_t _item4053;
        private sgimgui_capture_item_t _item4054;
        private sgimgui_capture_item_t _item4055;
        private sgimgui_capture_item_t _item4056;
        private sgimgui_capture_item_t _item4057;
        private sgimgui_capture_item_t _item4058;
        private sgimgui_capture_item_t _item4059;
        private sgimgui_capture_item_t _item4060;
        private sgimgui_capture_item_t _item4061;
        private sgimgui_capture_item_t _item4062;
        private sgimgui_capture_item_t _item4063;
        private sgimgui_capture_item_t _item4064;
        private sgimgui_capture_item_t _item4065;
        private sgimgui_capture_item_t _item4066;
        private sgimgui_capture_item_t _item4067;
        private sgimgui_capture_item_t _item4068;
        private sgimgui_capture_item_t _item4069;
        private sgimgui_capture_item_t _item4070;
        private sgimgui_capture_item_t _item4071;
        private sgimgui_capture_item_t _item4072;
        private sgimgui_capture_item_t _item4073;
        private sgimgui_capture_item_t _item4074;
        private sgimgui_capture_item_t _item4075;
        private sgimgui_capture_item_t _item4076;
        private sgimgui_capture_item_t _item4077;
        private sgimgui_capture_item_t _item4078;
        private sgimgui_capture_item_t _item4079;
        private sgimgui_capture_item_t _item4080;
        private sgimgui_capture_item_t _item4081;
        private sgimgui_capture_item_t _item4082;
        private sgimgui_capture_item_t _item4083;
        private sgimgui_capture_item_t _item4084;
        private sgimgui_capture_item_t _item4085;
        private sgimgui_capture_item_t _item4086;
        private sgimgui_capture_item_t _item4087;
        private sgimgui_capture_item_t _item4088;
        private sgimgui_capture_item_t _item4089;
        private sgimgui_capture_item_t _item4090;
        private sgimgui_capture_item_t _item4091;
        private sgimgui_capture_item_t _item4092;
        private sgimgui_capture_item_t _item4093;
        private sgimgui_capture_item_t _item4094;
        private sgimgui_capture_item_t _item4095;
    }
    #pragma warning restore 169
    public itemsCollection items;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_capture_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
    public int bucket_index;
    public int sel_item;
    #pragma warning disable 169
    public struct bucketCollection
    {
        public ref sgimgui_capture_bucket_t this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private sgimgui_capture_bucket_t _item0;
        private sgimgui_capture_bucket_t _item1;
    }
    #pragma warning restore 169
    public bucketCollection bucket;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_caps_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_frame_stats_window_t
{
#if WEB
    private byte _open;
    public bool open { get => _open != 0; set => _open = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool open;
#endif
#if WEB
    private byte _disable_sokol_imgui_stats;
    public bool disable_sokol_imgui_stats { get => _disable_sokol_imgui_stats != 0; set => _disable_sokol_imgui_stats = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool disable_sokol_imgui_stats;
#endif
#if WEB
    private byte _in_sokol_imgui;
    public bool in_sokol_imgui { get => _in_sokol_imgui != 0; set => _in_sokol_imgui = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool in_sokol_imgui;
#endif
    public sg_frame_stats stats;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_allocator_t
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sgimgui_desc_t
{
    public sgimgui_allocator_t allocator;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_discard", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_discard", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_discard(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_menu", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_menu", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_menu(IntPtr ctx, [M(U.LPUTF8Str)] string title);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_buffer_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_buffer_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_buffer_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_image_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_image_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_image_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_sampler_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_sampler_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_sampler_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_shader_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_shader_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_shader_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_pipeline_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_pipeline_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_pipeline_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_view_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_view_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_view_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_capture_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_capture_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_capture_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_capabilities_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_capabilities_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_capabilities_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_frame_stats_window_content", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_frame_stats_window_content", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_frame_stats_window_content(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_buffer_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_buffer_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_buffer_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_image_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_image_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_image_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_sampler_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_sampler_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_sampler_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_shader_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_shader_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_shader_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_pipeline_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_pipeline_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_pipeline_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_view_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_view_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_view_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_capture_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_capture_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_capture_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_capabilities_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_capabilities_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_capabilities_window(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_draw_frame_stats_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sgimgui_draw_frame_stats_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sgimgui_draw_frame_stats_window(IntPtr ctx);

}
}
