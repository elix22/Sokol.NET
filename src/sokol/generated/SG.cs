// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class SG
{
[StructLayout(LayoutKind.Sequential)]
public struct sg_buffer
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_image
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_sampler
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_pipeline
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_view
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_range
{
    public void* ptr;
    public nuint size;
}
public const int SG_INVALID_ID = 0;
public const int SG_NUM_INFLIGHT_FRAMES = 2;
public const int SG_MAX_COLOR_ATTACHMENTS = 8;
public const int SG_MAX_UNIFORMBLOCK_MEMBERS = 16;
public const int SG_MAX_VERTEX_ATTRIBUTES = 16;
public const int SG_MAX_MIPMAPS = 16;
public const int SG_MAX_VERTEXBUFFER_BINDSLOTS = 8;
public const int SG_MAX_UNIFORMBLOCK_BINDSLOTS = 8;
public const int SG_MAX_VIEW_BINDSLOTS = 32;
public const int SG_MAX_SAMPLER_BINDSLOTS = 12;
public const int SG_MAX_TEXTURE_SAMPLER_PAIRS = 32;
public const int SG_MAX_PORTABLE_COLOR_ATTACHMENTS = 4;
public const int SG_MAX_PORTABLE_TEXTURE_BINDINGS_PER_STAGE = 16;
public const int SG_MAX_PORTABLE_STORAGEBUFFER_BINDINGS_PER_STAGE = 8;
public const int SG_MAX_PORTABLE_STORAGEIMAGE_BINDINGS_PER_STAGE = 4;
public enum sg_backend
{
    SG_BACKEND_GLCORE,
    SG_BACKEND_GLES3,
    SG_BACKEND_D3D11,
    SG_BACKEND_METAL_IOS,
    SG_BACKEND_METAL_MACOS,
    SG_BACKEND_METAL_SIMULATOR,
    SG_BACKEND_WGPU,
    SG_BACKEND_DUMMY,
}
public enum sg_pixel_format
{
    _SG_PIXELFORMAT_DEFAULT,
    SG_PIXELFORMAT_NONE,
    SG_PIXELFORMAT_R8,
    SG_PIXELFORMAT_R8SN,
    SG_PIXELFORMAT_R8UI,
    SG_PIXELFORMAT_R8SI,
    SG_PIXELFORMAT_R16,
    SG_PIXELFORMAT_R16SN,
    SG_PIXELFORMAT_R16UI,
    SG_PIXELFORMAT_R16SI,
    SG_PIXELFORMAT_R16F,
    SG_PIXELFORMAT_RG8,
    SG_PIXELFORMAT_RG8SN,
    SG_PIXELFORMAT_RG8UI,
    SG_PIXELFORMAT_RG8SI,
    SG_PIXELFORMAT_R32UI,
    SG_PIXELFORMAT_R32SI,
    SG_PIXELFORMAT_R32F,
    SG_PIXELFORMAT_RG16,
    SG_PIXELFORMAT_RG16SN,
    SG_PIXELFORMAT_RG16UI,
    SG_PIXELFORMAT_RG16SI,
    SG_PIXELFORMAT_RG16F,
    SG_PIXELFORMAT_RGBA8,
    SG_PIXELFORMAT_SRGB8A8,
    SG_PIXELFORMAT_RGBA8SN,
    SG_PIXELFORMAT_RGBA8UI,
    SG_PIXELFORMAT_RGBA8SI,
    SG_PIXELFORMAT_BGRA8,
    SG_PIXELFORMAT_RGB10A2,
    SG_PIXELFORMAT_RG11B10F,
    SG_PIXELFORMAT_RGB9E5,
    SG_PIXELFORMAT_RG32UI,
    SG_PIXELFORMAT_RG32SI,
    SG_PIXELFORMAT_RG32F,
    SG_PIXELFORMAT_RGBA16,
    SG_PIXELFORMAT_RGBA16SN,
    SG_PIXELFORMAT_RGBA16UI,
    SG_PIXELFORMAT_RGBA16SI,
    SG_PIXELFORMAT_RGBA16F,
    SG_PIXELFORMAT_RGBA32UI,
    SG_PIXELFORMAT_RGBA32SI,
    SG_PIXELFORMAT_RGBA32F,
    SG_PIXELFORMAT_DEPTH,
    SG_PIXELFORMAT_DEPTH_STENCIL,
    SG_PIXELFORMAT_BC1_RGBA,
    SG_PIXELFORMAT_BC2_RGBA,
    SG_PIXELFORMAT_BC3_RGBA,
    SG_PIXELFORMAT_BC3_SRGBA,
    SG_PIXELFORMAT_BC4_R,
    SG_PIXELFORMAT_BC4_RSN,
    SG_PIXELFORMAT_BC5_RG,
    SG_PIXELFORMAT_BC5_RGSN,
    SG_PIXELFORMAT_BC6H_RGBF,
    SG_PIXELFORMAT_BC6H_RGBUF,
    SG_PIXELFORMAT_BC7_RGBA,
    SG_PIXELFORMAT_BC7_SRGBA,
    SG_PIXELFORMAT_ETC2_RGB8,
    SG_PIXELFORMAT_ETC2_SRGB8,
    SG_PIXELFORMAT_ETC2_RGB8A1,
    SG_PIXELFORMAT_ETC2_RGBA8,
    SG_PIXELFORMAT_ETC2_SRGB8A8,
    SG_PIXELFORMAT_EAC_R11,
    SG_PIXELFORMAT_EAC_R11SN,
    SG_PIXELFORMAT_EAC_RG11,
    SG_PIXELFORMAT_EAC_RG11SN,
    SG_PIXELFORMAT_ASTC_4x4_RGBA,
    SG_PIXELFORMAT_ASTC_4x4_SRGBA,
    _SG_PIXELFORMAT_NUM,
    _SG_PIXELFORMAT_FORCE_U32 = 2147483647,
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_pixelformat_info
{
#if WEB
    private byte _sample;
    public bool sample { get => _sample != 0; set => _sample = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool sample;
#endif
#if WEB
    private byte _filter;
    public bool filter { get => _filter != 0; set => _filter = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool filter;
#endif
#if WEB
    private byte _render;
    public bool render { get => _render != 0; set => _render = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool render;
#endif
#if WEB
    private byte _blend;
    public bool blend { get => _blend != 0; set => _blend = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool blend;
#endif
#if WEB
    private byte _msaa;
    public bool msaa { get => _msaa != 0; set => _msaa = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool msaa;
#endif
#if WEB
    private byte _depth;
    public bool depth { get => _depth != 0; set => _depth = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool depth;
#endif
#if WEB
    private byte _compressed;
    public bool compressed { get => _compressed != 0; set => _compressed = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool compressed;
#endif
#if WEB
    private byte _read;
    public bool read { get => _read != 0; set => _read = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool read;
#endif
#if WEB
    private byte _write;
    public bool write { get => _write != 0; set => _write = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool write;
#endif
    public int bytes_per_pixel;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_features
{
#if WEB
    private byte _origin_top_left;
    public bool origin_top_left { get => _origin_top_left != 0; set => _origin_top_left = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool origin_top_left;
#endif
#if WEB
    private byte _image_clamp_to_border;
    public bool image_clamp_to_border { get => _image_clamp_to_border != 0; set => _image_clamp_to_border = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool image_clamp_to_border;
#endif
#if WEB
    private byte _mrt_independent_blend_state;
    public bool mrt_independent_blend_state { get => _mrt_independent_blend_state != 0; set => _mrt_independent_blend_state = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool mrt_independent_blend_state;
#endif
#if WEB
    private byte _mrt_independent_write_mask;
    public bool mrt_independent_write_mask { get => _mrt_independent_write_mask != 0; set => _mrt_independent_write_mask = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool mrt_independent_write_mask;
#endif
#if WEB
    private byte _compute;
    public bool compute { get => _compute != 0; set => _compute = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool compute;
#endif
#if WEB
    private byte _msaa_texture_bindings;
    public bool msaa_texture_bindings { get => _msaa_texture_bindings != 0; set => _msaa_texture_bindings = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool msaa_texture_bindings;
#endif
#if WEB
    private byte _separate_buffer_types;
    public bool separate_buffer_types { get => _separate_buffer_types != 0; set => _separate_buffer_types = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool separate_buffer_types;
#endif
#if WEB
    private byte _draw_base_vertex;
    public bool draw_base_vertex { get => _draw_base_vertex != 0; set => _draw_base_vertex = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool draw_base_vertex;
#endif
#if WEB
    private byte _draw_base_instance;
    public bool draw_base_instance { get => _draw_base_instance != 0; set => _draw_base_instance = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool draw_base_instance;
#endif
#if WEB
    private byte _gl_texture_views;
    public bool gl_texture_views { get => _gl_texture_views != 0; set => _gl_texture_views = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool gl_texture_views;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_limits
{
    public int max_image_size_2d;
    public int max_image_size_cube;
    public int max_image_size_3d;
    public int max_image_size_array;
    public int max_image_array_layers;
    public int max_vertex_attrs;
    public int max_color_attachments;
    public int max_texture_bindings_per_stage;
    public int max_storage_buffer_bindings_per_stage;
    public int max_storage_image_bindings_per_stage;
    public int gl_max_vertex_uniform_components;
    public int gl_max_combined_texture_image_units;
    public int d3d11_max_unordered_access_views;
}
public enum sg_resource_state
{
    SG_RESOURCESTATE_INITIAL,
    SG_RESOURCESTATE_ALLOC,
    SG_RESOURCESTATE_VALID,
    SG_RESOURCESTATE_FAILED,
    SG_RESOURCESTATE_INVALID,
    _SG_RESOURCESTATE_FORCE_U32 = 2147483647,
}
public enum sg_index_type
{
    _SG_INDEXTYPE_DEFAULT,
    SG_INDEXTYPE_NONE,
    SG_INDEXTYPE_UINT16,
    SG_INDEXTYPE_UINT32,
    _SG_INDEXTYPE_NUM,
    _SG_INDEXTYPE_FORCE_U32 = 2147483647,
}
public enum sg_image_type
{
    _SG_IMAGETYPE_DEFAULT,
    SG_IMAGETYPE_2D,
    SG_IMAGETYPE_CUBE,
    SG_IMAGETYPE_3D,
    SG_IMAGETYPE_ARRAY,
    _SG_IMAGETYPE_NUM,
    _SG_IMAGETYPE_FORCE_U32 = 2147483647,
}
public enum sg_image_sample_type
{
    _SG_IMAGESAMPLETYPE_DEFAULT,
    SG_IMAGESAMPLETYPE_FLOAT,
    SG_IMAGESAMPLETYPE_DEPTH,
    SG_IMAGESAMPLETYPE_SINT,
    SG_IMAGESAMPLETYPE_UINT,
    SG_IMAGESAMPLETYPE_UNFILTERABLE_FLOAT,
    _SG_IMAGESAMPLETYPE_NUM,
    _SG_IMAGESAMPLETYPE_FORCE_U32 = 2147483647,
}
public enum sg_sampler_type
{
    _SG_SAMPLERTYPE_DEFAULT,
    SG_SAMPLERTYPE_FILTERING,
    SG_SAMPLERTYPE_NONFILTERING,
    SG_SAMPLERTYPE_COMPARISON,
    _SG_SAMPLERTYPE_NUM,
    _SG_SAMPLERTYPE_FORCE_U32,
}
public enum sg_primitive_type
{
    _SG_PRIMITIVETYPE_DEFAULT,
    SG_PRIMITIVETYPE_POINTS,
    SG_PRIMITIVETYPE_LINES,
    SG_PRIMITIVETYPE_LINE_STRIP,
    SG_PRIMITIVETYPE_TRIANGLES,
    SG_PRIMITIVETYPE_TRIANGLE_STRIP,
    _SG_PRIMITIVETYPE_NUM,
    _SG_PRIMITIVETYPE_FORCE_U32 = 2147483647,
}
public enum sg_filter
{
    _SG_FILTER_DEFAULT,
    SG_FILTER_NEAREST,
    SG_FILTER_LINEAR,
    _SG_FILTER_NUM,
    _SG_FILTER_FORCE_U32 = 2147483647,
}
public enum sg_wrap
{
    _SG_WRAP_DEFAULT,
    SG_WRAP_REPEAT,
    SG_WRAP_CLAMP_TO_EDGE,
    SG_WRAP_CLAMP_TO_BORDER,
    SG_WRAP_MIRRORED_REPEAT,
    _SG_WRAP_NUM,
    _SG_WRAP_FORCE_U32 = 2147483647,
}
public enum sg_border_color
{
    _SG_BORDERCOLOR_DEFAULT,
    SG_BORDERCOLOR_TRANSPARENT_BLACK,
    SG_BORDERCOLOR_OPAQUE_BLACK,
    SG_BORDERCOLOR_OPAQUE_WHITE,
    _SG_BORDERCOLOR_NUM,
    _SG_BORDERCOLOR_FORCE_U32 = 2147483647,
}
public enum sg_vertex_format
{
    SG_VERTEXFORMAT_INVALID,
    SG_VERTEXFORMAT_FLOAT,
    SG_VERTEXFORMAT_FLOAT2,
    SG_VERTEXFORMAT_FLOAT3,
    SG_VERTEXFORMAT_FLOAT4,
    SG_VERTEXFORMAT_INT,
    SG_VERTEXFORMAT_INT2,
    SG_VERTEXFORMAT_INT3,
    SG_VERTEXFORMAT_INT4,
    SG_VERTEXFORMAT_UINT,
    SG_VERTEXFORMAT_UINT2,
    SG_VERTEXFORMAT_UINT3,
    SG_VERTEXFORMAT_UINT4,
    SG_VERTEXFORMAT_BYTE4,
    SG_VERTEXFORMAT_BYTE4N,
    SG_VERTEXFORMAT_UBYTE4,
    SG_VERTEXFORMAT_UBYTE4N,
    SG_VERTEXFORMAT_SHORT2,
    SG_VERTEXFORMAT_SHORT2N,
    SG_VERTEXFORMAT_USHORT2,
    SG_VERTEXFORMAT_USHORT2N,
    SG_VERTEXFORMAT_SHORT4,
    SG_VERTEXFORMAT_SHORT4N,
    SG_VERTEXFORMAT_USHORT4,
    SG_VERTEXFORMAT_USHORT4N,
    SG_VERTEXFORMAT_UINT10_N2,
    SG_VERTEXFORMAT_HALF2,
    SG_VERTEXFORMAT_HALF4,
    _SG_VERTEXFORMAT_NUM,
    _SG_VERTEXFORMAT_FORCE_U32 = 2147483647,
}
public enum sg_vertex_step
{
    _SG_VERTEXSTEP_DEFAULT,
    SG_VERTEXSTEP_PER_VERTEX,
    SG_VERTEXSTEP_PER_INSTANCE,
    _SG_VERTEXSTEP_NUM,
    _SG_VERTEXSTEP_FORCE_U32 = 2147483647,
}
public enum sg_uniform_type
{
    SG_UNIFORMTYPE_INVALID,
    SG_UNIFORMTYPE_FLOAT,
    SG_UNIFORMTYPE_FLOAT2,
    SG_UNIFORMTYPE_FLOAT3,
    SG_UNIFORMTYPE_FLOAT4,
    SG_UNIFORMTYPE_INT,
    SG_UNIFORMTYPE_INT2,
    SG_UNIFORMTYPE_INT3,
    SG_UNIFORMTYPE_INT4,
    SG_UNIFORMTYPE_MAT4,
    _SG_UNIFORMTYPE_NUM,
    _SG_UNIFORMTYPE_FORCE_U32 = 2147483647,
}
public enum sg_uniform_layout
{
    _SG_UNIFORMLAYOUT_DEFAULT,
    SG_UNIFORMLAYOUT_NATIVE,
    SG_UNIFORMLAYOUT_STD140,
    _SG_UNIFORMLAYOUT_NUM,
    _SG_UNIFORMLAYOUT_FORCE_U32 = 2147483647,
}
public enum sg_cull_mode
{
    _SG_CULLMODE_DEFAULT,
    SG_CULLMODE_NONE,
    SG_CULLMODE_FRONT,
    SG_CULLMODE_BACK,
    _SG_CULLMODE_NUM,
    _SG_CULLMODE_FORCE_U32 = 2147483647,
}
public enum sg_face_winding
{
    _SG_FACEWINDING_DEFAULT,
    SG_FACEWINDING_CCW,
    SG_FACEWINDING_CW,
    _SG_FACEWINDING_NUM,
    _SG_FACEWINDING_FORCE_U32 = 2147483647,
}
public enum sg_compare_func
{
    _SG_COMPAREFUNC_DEFAULT,
    SG_COMPAREFUNC_NEVER,
    SG_COMPAREFUNC_LESS,
    SG_COMPAREFUNC_EQUAL,
    SG_COMPAREFUNC_LESS_EQUAL,
    SG_COMPAREFUNC_GREATER,
    SG_COMPAREFUNC_NOT_EQUAL,
    SG_COMPAREFUNC_GREATER_EQUAL,
    SG_COMPAREFUNC_ALWAYS,
    _SG_COMPAREFUNC_NUM,
    _SG_COMPAREFUNC_FORCE_U32 = 2147483647,
}
public enum sg_stencil_op
{
    _SG_STENCILOP_DEFAULT,
    SG_STENCILOP_KEEP,
    SG_STENCILOP_ZERO,
    SG_STENCILOP_REPLACE,
    SG_STENCILOP_INCR_CLAMP,
    SG_STENCILOP_DECR_CLAMP,
    SG_STENCILOP_INVERT,
    SG_STENCILOP_INCR_WRAP,
    SG_STENCILOP_DECR_WRAP,
    _SG_STENCILOP_NUM,
    _SG_STENCILOP_FORCE_U32 = 2147483647,
}
public enum sg_blend_factor
{
    _SG_BLENDFACTOR_DEFAULT,
    SG_BLENDFACTOR_ZERO,
    SG_BLENDFACTOR_ONE,
    SG_BLENDFACTOR_SRC_COLOR,
    SG_BLENDFACTOR_ONE_MINUS_SRC_COLOR,
    SG_BLENDFACTOR_SRC_ALPHA,
    SG_BLENDFACTOR_ONE_MINUS_SRC_ALPHA,
    SG_BLENDFACTOR_DST_COLOR,
    SG_BLENDFACTOR_ONE_MINUS_DST_COLOR,
    SG_BLENDFACTOR_DST_ALPHA,
    SG_BLENDFACTOR_ONE_MINUS_DST_ALPHA,
    SG_BLENDFACTOR_SRC_ALPHA_SATURATED,
    SG_BLENDFACTOR_BLEND_COLOR,
    SG_BLENDFACTOR_ONE_MINUS_BLEND_COLOR,
    SG_BLENDFACTOR_BLEND_ALPHA,
    SG_BLENDFACTOR_ONE_MINUS_BLEND_ALPHA,
    _SG_BLENDFACTOR_NUM,
    _SG_BLENDFACTOR_FORCE_U32 = 2147483647,
}
public enum sg_blend_op
{
    _SG_BLENDOP_DEFAULT,
    SG_BLENDOP_ADD,
    SG_BLENDOP_SUBTRACT,
    SG_BLENDOP_REVERSE_SUBTRACT,
    SG_BLENDOP_MIN,
    SG_BLENDOP_MAX,
    _SG_BLENDOP_NUM,
    _SG_BLENDOP_FORCE_U32 = 2147483647,
}
public enum sg_color_mask
{
    _SG_COLORMASK_DEFAULT = 0,
    SG_COLORMASK_NONE = 16,
    SG_COLORMASK_R = 1,
    SG_COLORMASK_G = 2,
    SG_COLORMASK_RG = 3,
    SG_COLORMASK_B = 4,
    SG_COLORMASK_RB = 5,
    SG_COLORMASK_GB = 6,
    SG_COLORMASK_RGB = 7,
    SG_COLORMASK_A = 8,
    SG_COLORMASK_RA = 9,
    SG_COLORMASK_GA = 10,
    SG_COLORMASK_RGA = 11,
    SG_COLORMASK_BA = 12,
    SG_COLORMASK_RBA = 13,
    SG_COLORMASK_GBA = 14,
    SG_COLORMASK_RGBA = 15,
    _SG_COLORMASK_FORCE_U32 = 2147483647,
}
public enum sg_load_action
{
    _SG_LOADACTION_DEFAULT,
    SG_LOADACTION_CLEAR,
    SG_LOADACTION_LOAD,
    SG_LOADACTION_DONTCARE,
    _SG_LOADACTION_FORCE_U32 = 2147483647,
}
public enum sg_store_action
{
    _SG_STOREACTION_DEFAULT,
    SG_STOREACTION_STORE,
    SG_STOREACTION_DONTCARE,
    _SG_STOREACTION_FORCE_U32 = 2147483647,
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_color_attachment_action
{
    public sg_load_action load_action;
    public sg_store_action store_action;
    public sg_color clear_value;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_depth_attachment_action
{
    public sg_load_action load_action;
    public sg_store_action store_action;
    public float clear_value;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_stencil_attachment_action
{
    public sg_load_action load_action;
    public sg_store_action store_action;
    public byte clear_value;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_pass_action
{
    #pragma warning disable 169
    public struct colorsCollection
    {
        public ref sg_color_attachment_action this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sg_color_attachment_action _item0;
        private sg_color_attachment_action _item1;
        private sg_color_attachment_action _item2;
        private sg_color_attachment_action _item3;
        private sg_color_attachment_action _item4;
        private sg_color_attachment_action _item5;
        private sg_color_attachment_action _item6;
        private sg_color_attachment_action _item7;
    }
    #pragma warning restore 169
    public colorsCollection colors;
    public sg_depth_attachment_action depth;
    public sg_stencil_attachment_action stencil;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_metal_swapchain
{
    public void* current_drawable;
    public void* depth_stencil_texture;
    public void* msaa_color_texture;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_swapchain
{
    public void* render_view;
    public void* resolve_view;
    public void* depth_stencil_view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_swapchain
{
    public void* render_view;
    public void* resolve_view;
    public void* depth_stencil_view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_gl_swapchain
{
    public uint framebuffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_swapchain
{
    public int width;
    public int height;
    public int sample_count;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public sg_metal_swapchain metal;
    public sg_d3d11_swapchain d3d11;
    public sg_wgpu_swapchain wgpu;
    public sg_gl_swapchain gl;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_attachments
{
    #pragma warning disable 169
    public struct colorsCollection
    {
        public ref sg_view this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sg_view _item0;
        private sg_view _item1;
        private sg_view _item2;
        private sg_view _item3;
        private sg_view _item4;
        private sg_view _item5;
        private sg_view _item6;
        private sg_view _item7;
    }
    #pragma warning restore 169
    public colorsCollection colors;
    #pragma warning disable 169
    public struct resolvesCollection
    {
        public ref sg_view this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sg_view _item0;
        private sg_view _item1;
        private sg_view _item2;
        private sg_view _item3;
        private sg_view _item4;
        private sg_view _item5;
        private sg_view _item6;
        private sg_view _item7;
    }
    #pragma warning restore 169
    public resolvesCollection resolves;
    public sg_view depth_stencil;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_pass
{
    public uint _start_canary;
#if WEB
    private byte _compute;
    public bool compute { get => _compute != 0; set => _compute = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool compute;
#endif
    public sg_pass_action action;
    public sg_attachments attachments;
    public sg_swapchain swapchain;
#if WEB
    private IntPtr _label;
    public string label { get => Marshal.PtrToStringAnsi(_label);  set { if (_label != IntPtr.Zero) { Marshal.FreeHGlobal(_label); _label = IntPtr.Zero; } if (value != null) { _label = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string label;
#endif
    public uint _end_canary;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_bindings
{
    public uint _start_canary;
    #pragma warning disable 169
    public struct vertex_buffersCollection
    {
        public ref sg_buffer this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sg_buffer _item0;
        private sg_buffer _item1;
        private sg_buffer _item2;
        private sg_buffer _item3;
        private sg_buffer _item4;
        private sg_buffer _item5;
        private sg_buffer _item6;
        private sg_buffer _item7;
    }
    #pragma warning restore 169
    public vertex_buffersCollection vertex_buffers;
    #pragma warning disable 169
    public struct vertex_buffer_offsetsCollection
    {
        public ref int this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private int _item0;
        private int _item1;
        private int _item2;
        private int _item3;
        private int _item4;
        private int _item5;
        private int _item6;
        private int _item7;
    }
    #pragma warning restore 169
    public vertex_buffer_offsetsCollection vertex_buffer_offsets;
    public sg_buffer index_buffer;
    public int index_buffer_offset;
    #pragma warning disable 169
    public struct viewsCollection
    {
        public ref sg_view this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 32)[index];
        private sg_view _item0;
        private sg_view _item1;
        private sg_view _item2;
        private sg_view _item3;
        private sg_view _item4;
        private sg_view _item5;
        private sg_view _item6;
        private sg_view _item7;
        private sg_view _item8;
        private sg_view _item9;
        private sg_view _item10;
        private sg_view _item11;
        private sg_view _item12;
        private sg_view _item13;
        private sg_view _item14;
        private sg_view _item15;
        private sg_view _item16;
        private sg_view _item17;
        private sg_view _item18;
        private sg_view _item19;
        private sg_view _item20;
        private sg_view _item21;
        private sg_view _item22;
        private sg_view _item23;
        private sg_view _item24;
        private sg_view _item25;
        private sg_view _item26;
        private sg_view _item27;
        private sg_view _item28;
        private sg_view _item29;
        private sg_view _item30;
        private sg_view _item31;
    }
    #pragma warning restore 169
    public viewsCollection views;
    #pragma warning disable 169
    public struct samplersCollection
    {
        public ref sg_sampler this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 12)[index];
        private sg_sampler _item0;
        private sg_sampler _item1;
        private sg_sampler _item2;
        private sg_sampler _item3;
        private sg_sampler _item4;
        private sg_sampler _item5;
        private sg_sampler _item6;
        private sg_sampler _item7;
        private sg_sampler _item8;
        private sg_sampler _item9;
        private sg_sampler _item10;
        private sg_sampler _item11;
    }
    #pragma warning restore 169
    public samplersCollection samplers;
    public uint _end_canary;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_buffer_usage
{
#if WEB
    private byte _vertex_buffer;
    public bool vertex_buffer { get => _vertex_buffer != 0; set => _vertex_buffer = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool vertex_buffer;
#endif
#if WEB
    private byte _index_buffer;
    public bool index_buffer { get => _index_buffer != 0; set => _index_buffer = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool index_buffer;
#endif
#if WEB
    private byte _storage_buffer;
    public bool storage_buffer { get => _storage_buffer != 0; set => _storage_buffer = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool storage_buffer;
#endif
#if WEB
    private byte _immutable;
    public bool immutable { get => _immutable != 0; set => _immutable = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool immutable;
#endif
#if WEB
    private byte _dynamic_update;
    public bool dynamic_update { get => _dynamic_update != 0; set => _dynamic_update = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool dynamic_update;
#endif
#if WEB
    private byte _stream_update;
    public bool stream_update { get => _stream_update != 0; set => _stream_update = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool stream_update;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_buffer_desc
{
    public uint _start_canary;
    public nuint size;
    public sg_buffer_usage usage;
    public sg_range data;
#if WEB
    private IntPtr _label;
    public string label { get => Marshal.PtrToStringAnsi(_label);  set { if (_label != IntPtr.Zero) { Marshal.FreeHGlobal(_label); _label = IntPtr.Zero; } if (value != null) { _label = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string label;
#endif
    #pragma warning disable 169
    public struct gl_buffersCollection
    {
        public ref uint this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private uint _item0;
        private uint _item1;
    }
    #pragma warning restore 169
    public gl_buffersCollection gl_buffers;
    #pragma warning disable 169
    public struct mtl_buffersCollection
    {
        public ref IntPtr this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private IntPtr _item0;
        private IntPtr _item1;
    }
    #pragma warning restore 169
    public mtl_buffersCollection mtl_buffers;
    public void* d3d11_buffer;
    public void* wgpu_buffer;
    public uint _end_canary;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_image_usage
{
#if WEB
    private byte _storage_image;
    public bool storage_image { get => _storage_image != 0; set => _storage_image = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool storage_image;
#endif
#if WEB
    private byte _color_attachment;
    public bool color_attachment { get => _color_attachment != 0; set => _color_attachment = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool color_attachment;
#endif
#if WEB
    private byte _resolve_attachment;
    public bool resolve_attachment { get => _resolve_attachment != 0; set => _resolve_attachment = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool resolve_attachment;
#endif
#if WEB
    private byte _depth_stencil_attachment;
    public bool depth_stencil_attachment { get => _depth_stencil_attachment != 0; set => _depth_stencil_attachment = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool depth_stencil_attachment;
#endif
#if WEB
    private byte _immutable;
    public bool immutable { get => _immutable != 0; set => _immutable = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool immutable;
#endif
#if WEB
    private byte _dynamic_update;
    public bool dynamic_update { get => _dynamic_update != 0; set => _dynamic_update = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool dynamic_update;
#endif
#if WEB
    private byte _stream_update;
    public bool stream_update { get => _stream_update != 0; set => _stream_update = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool stream_update;
#endif
}
public enum sg_view_type
{
    SG_VIEWTYPE_INVALID,
    SG_VIEWTYPE_STORAGEBUFFER,
    SG_VIEWTYPE_STORAGEIMAGE,
    SG_VIEWTYPE_TEXTURE,
    SG_VIEWTYPE_COLORATTACHMENT,
    SG_VIEWTYPE_RESOLVEATTACHMENT,
    SG_VIEWTYPE_DEPTHSTENCILATTACHMENT,
    _SG_VIEWTYPE_FORCE_U32 = 2147483647,
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_image_data
{
    #pragma warning disable 169
    public struct mip_levelsCollection
    {
        public ref sg_range this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private sg_range _item0;
        private sg_range _item1;
        private sg_range _item2;
        private sg_range _item3;
        private sg_range _item4;
        private sg_range _item5;
        private sg_range _item6;
        private sg_range _item7;
        private sg_range _item8;
        private sg_range _item9;
        private sg_range _item10;
        private sg_range _item11;
        private sg_range _item12;
        private sg_range _item13;
        private sg_range _item14;
        private sg_range _item15;
    }
    #pragma warning restore 169
    public mip_levelsCollection mip_levels;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_image_desc
{
    public uint _start_canary;
    public sg_image_type type;
    public sg_image_usage usage;
    public int width;
    public int height;
    public int num_slices;
    public int num_mipmaps;
    public sg_pixel_format pixel_format;
    public int sample_count;
    public sg_image_data data;
#if WEB
    private IntPtr _label;
    public string label { get => Marshal.PtrToStringAnsi(_label);  set { if (_label != IntPtr.Zero) { Marshal.FreeHGlobal(_label); _label = IntPtr.Zero; } if (value != null) { _label = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string label;
#endif
    #pragma warning disable 169
    public struct gl_texturesCollection
    {
        public ref uint this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private uint _item0;
        private uint _item1;
    }
    #pragma warning restore 169
    public gl_texturesCollection gl_textures;
    public uint gl_texture_target;
    #pragma warning disable 169
    public struct mtl_texturesCollection
    {
        public ref IntPtr this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private IntPtr _item0;
        private IntPtr _item1;
    }
    #pragma warning restore 169
    public mtl_texturesCollection mtl_textures;
    public void* d3d11_texture;
    public void* wgpu_texture;
    public uint _end_canary;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_sampler_desc
{
    public uint _start_canary;
    public sg_filter min_filter;
    public sg_filter mag_filter;
    public sg_filter mipmap_filter;
    public sg_wrap wrap_u;
    public sg_wrap wrap_v;
    public sg_wrap wrap_w;
    public float min_lod;
    public float max_lod;
    public sg_border_color border_color;
    public sg_compare_func compare;
    public uint max_anisotropy;
#if WEB
    private IntPtr _label;
    public string label { get => Marshal.PtrToStringAnsi(_label);  set { if (_label != IntPtr.Zero) { Marshal.FreeHGlobal(_label); _label = IntPtr.Zero; } if (value != null) { _label = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string label;
#endif
    public uint gl_sampler;
    public void* mtl_sampler;
    public void* d3d11_sampler;
    public void* wgpu_sampler;
    public uint _end_canary;
}
public enum sg_shader_stage
{
    SG_SHADERSTAGE_NONE,
    SG_SHADERSTAGE_VERTEX,
    SG_SHADERSTAGE_FRAGMENT,
    SG_SHADERSTAGE_COMPUTE,
    _SG_SHADERSTAGE_FORCE_U32 = 2147483647,
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_function
{
#if WEB
    private IntPtr _source;
    public string source { get => Marshal.PtrToStringAnsi(_source);  set { if (_source != IntPtr.Zero) { Marshal.FreeHGlobal(_source); _source = IntPtr.Zero; } if (value != null) { _source = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string source;
#endif
    public sg_range bytecode;
#if WEB
    private IntPtr _entry;
    public string entry { get => Marshal.PtrToStringAnsi(_entry);  set { if (_entry != IntPtr.Zero) { Marshal.FreeHGlobal(_entry); _entry = IntPtr.Zero; } if (value != null) { _entry = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string entry;
#endif
#if WEB
    private IntPtr _d3d11_target;
    public string d3d11_target { get => Marshal.PtrToStringAnsi(_d3d11_target);  set { if (_d3d11_target != IntPtr.Zero) { Marshal.FreeHGlobal(_d3d11_target); _d3d11_target = IntPtr.Zero; } if (value != null) { _d3d11_target = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string d3d11_target;
#endif
#if WEB
    private IntPtr _d3d11_filepath;
    public string d3d11_filepath { get => Marshal.PtrToStringAnsi(_d3d11_filepath);  set { if (_d3d11_filepath != IntPtr.Zero) { Marshal.FreeHGlobal(_d3d11_filepath); _d3d11_filepath = IntPtr.Zero; } if (value != null) { _d3d11_filepath = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string d3d11_filepath;
#endif
}
public enum sg_shader_attr_base_type
{
    SG_SHADERATTRBASETYPE_UNDEFINED,
    SG_SHADERATTRBASETYPE_FLOAT,
    SG_SHADERATTRBASETYPE_SINT,
    SG_SHADERATTRBASETYPE_UINT,
    _SG_SHADERATTRBASETYPE_FORCE_U32 = 2147483647,
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_vertex_attr
{
    public sg_shader_attr_base_type base_type;
#if WEB
    private IntPtr _glsl_name;
    public string glsl_name { get => Marshal.PtrToStringAnsi(_glsl_name);  set { if (_glsl_name != IntPtr.Zero) { Marshal.FreeHGlobal(_glsl_name); _glsl_name = IntPtr.Zero; } if (value != null) { _glsl_name = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string glsl_name;
#endif
#if WEB
    private IntPtr _hlsl_sem_name;
    public string hlsl_sem_name { get => Marshal.PtrToStringAnsi(_hlsl_sem_name);  set { if (_hlsl_sem_name != IntPtr.Zero) { Marshal.FreeHGlobal(_hlsl_sem_name); _hlsl_sem_name = IntPtr.Zero; } if (value != null) { _hlsl_sem_name = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string hlsl_sem_name;
#endif
    public byte hlsl_sem_index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_glsl_shader_uniform
{
    public sg_uniform_type type;
    public ushort array_count;
#if WEB
    private IntPtr _glsl_name;
    public string glsl_name { get => Marshal.PtrToStringAnsi(_glsl_name);  set { if (_glsl_name != IntPtr.Zero) { Marshal.FreeHGlobal(_glsl_name); _glsl_name = IntPtr.Zero; } if (value != null) { _glsl_name = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string glsl_name;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_uniform_block
{
    public sg_shader_stage stage;
    public uint size;
    public byte hlsl_register_b_n;
    public byte msl_buffer_n;
    public byte wgsl_group0_binding_n;
    public sg_uniform_layout layout;
    #pragma warning disable 169
    public struct glsl_uniformsCollection
    {
        public ref sg_glsl_shader_uniform this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private sg_glsl_shader_uniform _item0;
        private sg_glsl_shader_uniform _item1;
        private sg_glsl_shader_uniform _item2;
        private sg_glsl_shader_uniform _item3;
        private sg_glsl_shader_uniform _item4;
        private sg_glsl_shader_uniform _item5;
        private sg_glsl_shader_uniform _item6;
        private sg_glsl_shader_uniform _item7;
        private sg_glsl_shader_uniform _item8;
        private sg_glsl_shader_uniform _item9;
        private sg_glsl_shader_uniform _item10;
        private sg_glsl_shader_uniform _item11;
        private sg_glsl_shader_uniform _item12;
        private sg_glsl_shader_uniform _item13;
        private sg_glsl_shader_uniform _item14;
        private sg_glsl_shader_uniform _item15;
    }
    #pragma warning restore 169
    public glsl_uniformsCollection glsl_uniforms;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_texture_view
{
    public sg_shader_stage stage;
    public sg_image_type image_type;
    public sg_image_sample_type sample_type;
#if WEB
    private byte _multisampled;
    public bool multisampled { get => _multisampled != 0; set => _multisampled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool multisampled;
#endif
    public byte hlsl_register_t_n;
    public byte msl_texture_n;
    public byte wgsl_group1_binding_n;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_storage_buffer_view
{
    public sg_shader_stage stage;
#if WEB
    private byte __readonly;
    public bool _readonly { get => __readonly != 0; set => __readonly = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool _readonly;
#endif
    public byte hlsl_register_t_n;
    public byte hlsl_register_u_n;
    public byte msl_buffer_n;
    public byte wgsl_group1_binding_n;
    public byte glsl_binding_n;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_storage_image_view
{
    public sg_shader_stage stage;
    public sg_image_type image_type;
    public sg_pixel_format access_format;
#if WEB
    private byte _writeonly;
    public bool writeonly { get => _writeonly != 0; set => _writeonly = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool writeonly;
#endif
    public byte hlsl_register_u_n;
    public byte msl_texture_n;
    public byte wgsl_group1_binding_n;
    public byte glsl_binding_n;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_view
{
    public sg_shader_texture_view texture;
    public sg_shader_storage_buffer_view storage_buffer;
    public sg_shader_storage_image_view storage_image;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_sampler
{
    public sg_shader_stage stage;
    public sg_sampler_type sampler_type;
    public byte hlsl_register_s_n;
    public byte msl_sampler_n;
    public byte wgsl_group1_binding_n;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_texture_sampler_pair
{
    public sg_shader_stage stage;
    public byte view_slot;
    public byte sampler_slot;
#if WEB
    private IntPtr _glsl_name;
    public string glsl_name { get => Marshal.PtrToStringAnsi(_glsl_name);  set { if (_glsl_name != IntPtr.Zero) { Marshal.FreeHGlobal(_glsl_name); _glsl_name = IntPtr.Zero; } if (value != null) { _glsl_name = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string glsl_name;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_mtl_shader_threads_per_threadgroup
{
    public int x;
    public int y;
    public int z;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_desc
{
    public uint _start_canary;
    public sg_shader_function vertex_func;
    public sg_shader_function fragment_func;
    public sg_shader_function compute_func;
    #pragma warning disable 169
    public struct attrsCollection
    {
        public ref sg_shader_vertex_attr this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private sg_shader_vertex_attr _item0;
        private sg_shader_vertex_attr _item1;
        private sg_shader_vertex_attr _item2;
        private sg_shader_vertex_attr _item3;
        private sg_shader_vertex_attr _item4;
        private sg_shader_vertex_attr _item5;
        private sg_shader_vertex_attr _item6;
        private sg_shader_vertex_attr _item7;
        private sg_shader_vertex_attr _item8;
        private sg_shader_vertex_attr _item9;
        private sg_shader_vertex_attr _item10;
        private sg_shader_vertex_attr _item11;
        private sg_shader_vertex_attr _item12;
        private sg_shader_vertex_attr _item13;
        private sg_shader_vertex_attr _item14;
        private sg_shader_vertex_attr _item15;
    }
    #pragma warning restore 169
    public attrsCollection attrs;
    #pragma warning disable 169
    public struct uniform_blocksCollection
    {
        public ref sg_shader_uniform_block this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sg_shader_uniform_block _item0;
        private sg_shader_uniform_block _item1;
        private sg_shader_uniform_block _item2;
        private sg_shader_uniform_block _item3;
        private sg_shader_uniform_block _item4;
        private sg_shader_uniform_block _item5;
        private sg_shader_uniform_block _item6;
        private sg_shader_uniform_block _item7;
    }
    #pragma warning restore 169
    public uniform_blocksCollection uniform_blocks;
    #pragma warning disable 169
    public struct viewsCollection
    {
        public ref sg_shader_view this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 32)[index];
        private sg_shader_view _item0;
        private sg_shader_view _item1;
        private sg_shader_view _item2;
        private sg_shader_view _item3;
        private sg_shader_view _item4;
        private sg_shader_view _item5;
        private sg_shader_view _item6;
        private sg_shader_view _item7;
        private sg_shader_view _item8;
        private sg_shader_view _item9;
        private sg_shader_view _item10;
        private sg_shader_view _item11;
        private sg_shader_view _item12;
        private sg_shader_view _item13;
        private sg_shader_view _item14;
        private sg_shader_view _item15;
        private sg_shader_view _item16;
        private sg_shader_view _item17;
        private sg_shader_view _item18;
        private sg_shader_view _item19;
        private sg_shader_view _item20;
        private sg_shader_view _item21;
        private sg_shader_view _item22;
        private sg_shader_view _item23;
        private sg_shader_view _item24;
        private sg_shader_view _item25;
        private sg_shader_view _item26;
        private sg_shader_view _item27;
        private sg_shader_view _item28;
        private sg_shader_view _item29;
        private sg_shader_view _item30;
        private sg_shader_view _item31;
    }
    #pragma warning restore 169
    public viewsCollection views;
    #pragma warning disable 169
    public struct samplersCollection
    {
        public ref sg_shader_sampler this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 12)[index];
        private sg_shader_sampler _item0;
        private sg_shader_sampler _item1;
        private sg_shader_sampler _item2;
        private sg_shader_sampler _item3;
        private sg_shader_sampler _item4;
        private sg_shader_sampler _item5;
        private sg_shader_sampler _item6;
        private sg_shader_sampler _item7;
        private sg_shader_sampler _item8;
        private sg_shader_sampler _item9;
        private sg_shader_sampler _item10;
        private sg_shader_sampler _item11;
    }
    #pragma warning restore 169
    public samplersCollection samplers;
    #pragma warning disable 169
    public struct texture_sampler_pairsCollection
    {
        public ref sg_shader_texture_sampler_pair this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 32)[index];
        private sg_shader_texture_sampler_pair _item0;
        private sg_shader_texture_sampler_pair _item1;
        private sg_shader_texture_sampler_pair _item2;
        private sg_shader_texture_sampler_pair _item3;
        private sg_shader_texture_sampler_pair _item4;
        private sg_shader_texture_sampler_pair _item5;
        private sg_shader_texture_sampler_pair _item6;
        private sg_shader_texture_sampler_pair _item7;
        private sg_shader_texture_sampler_pair _item8;
        private sg_shader_texture_sampler_pair _item9;
        private sg_shader_texture_sampler_pair _item10;
        private sg_shader_texture_sampler_pair _item11;
        private sg_shader_texture_sampler_pair _item12;
        private sg_shader_texture_sampler_pair _item13;
        private sg_shader_texture_sampler_pair _item14;
        private sg_shader_texture_sampler_pair _item15;
        private sg_shader_texture_sampler_pair _item16;
        private sg_shader_texture_sampler_pair _item17;
        private sg_shader_texture_sampler_pair _item18;
        private sg_shader_texture_sampler_pair _item19;
        private sg_shader_texture_sampler_pair _item20;
        private sg_shader_texture_sampler_pair _item21;
        private sg_shader_texture_sampler_pair _item22;
        private sg_shader_texture_sampler_pair _item23;
        private sg_shader_texture_sampler_pair _item24;
        private sg_shader_texture_sampler_pair _item25;
        private sg_shader_texture_sampler_pair _item26;
        private sg_shader_texture_sampler_pair _item27;
        private sg_shader_texture_sampler_pair _item28;
        private sg_shader_texture_sampler_pair _item29;
        private sg_shader_texture_sampler_pair _item30;
        private sg_shader_texture_sampler_pair _item31;
    }
    #pragma warning restore 169
    public texture_sampler_pairsCollection texture_sampler_pairs;
    public sg_mtl_shader_threads_per_threadgroup mtl_threads_per_threadgroup;
#if WEB
    private IntPtr _label;
    public string label { get => Marshal.PtrToStringAnsi(_label);  set { if (_label != IntPtr.Zero) { Marshal.FreeHGlobal(_label); _label = IntPtr.Zero; } if (value != null) { _label = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string label;
#endif
    public uint _end_canary;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_vertex_buffer_layout_state
{
    public int stride;
    public sg_vertex_step step_func;
    public int step_rate;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_vertex_attr_state
{
    public int buffer_index;
    public int offset;
    public sg_vertex_format format;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_vertex_layout_state
{
    #pragma warning disable 169
    public struct buffersCollection
    {
        public ref sg_vertex_buffer_layout_state this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sg_vertex_buffer_layout_state _item0;
        private sg_vertex_buffer_layout_state _item1;
        private sg_vertex_buffer_layout_state _item2;
        private sg_vertex_buffer_layout_state _item3;
        private sg_vertex_buffer_layout_state _item4;
        private sg_vertex_buffer_layout_state _item5;
        private sg_vertex_buffer_layout_state _item6;
        private sg_vertex_buffer_layout_state _item7;
    }
    #pragma warning restore 169
    public buffersCollection buffers;
    #pragma warning disable 169
    public struct attrsCollection
    {
        public ref sg_vertex_attr_state this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private sg_vertex_attr_state _item0;
        private sg_vertex_attr_state _item1;
        private sg_vertex_attr_state _item2;
        private sg_vertex_attr_state _item3;
        private sg_vertex_attr_state _item4;
        private sg_vertex_attr_state _item5;
        private sg_vertex_attr_state _item6;
        private sg_vertex_attr_state _item7;
        private sg_vertex_attr_state _item8;
        private sg_vertex_attr_state _item9;
        private sg_vertex_attr_state _item10;
        private sg_vertex_attr_state _item11;
        private sg_vertex_attr_state _item12;
        private sg_vertex_attr_state _item13;
        private sg_vertex_attr_state _item14;
        private sg_vertex_attr_state _item15;
    }
    #pragma warning restore 169
    public attrsCollection attrs;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_stencil_face_state
{
    public sg_compare_func compare;
    public sg_stencil_op fail_op;
    public sg_stencil_op depth_fail_op;
    public sg_stencil_op pass_op;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_stencil_state
{
#if WEB
    private byte _enabled;
    public bool enabled { get => _enabled != 0; set => _enabled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enabled;
#endif
    public sg_stencil_face_state front;
    public sg_stencil_face_state back;
    public byte read_mask;
    public byte write_mask;
    public byte _ref;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_depth_state
{
    public sg_pixel_format pixel_format;
    public sg_compare_func compare;
#if WEB
    private byte _write_enabled;
    public bool write_enabled { get => _write_enabled != 0; set => _write_enabled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool write_enabled;
#endif
    public float bias;
    public float bias_slope_scale;
    public float bias_clamp;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_blend_state
{
#if WEB
    private byte _enabled;
    public bool enabled { get => _enabled != 0; set => _enabled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enabled;
#endif
    public sg_blend_factor src_factor_rgb;
    public sg_blend_factor dst_factor_rgb;
    public sg_blend_op op_rgb;
    public sg_blend_factor src_factor_alpha;
    public sg_blend_factor dst_factor_alpha;
    public sg_blend_op op_alpha;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_color_target_state
{
    public sg_pixel_format pixel_format;
    public sg_color_mask write_mask;
    public sg_blend_state blend;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_pipeline_desc
{
    public uint _start_canary;
#if WEB
    private byte _compute;
    public bool compute { get => _compute != 0; set => _compute = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool compute;
#endif
    public sg_shader shader;
    public sg_vertex_layout_state layout;
    public sg_depth_state depth;
    public sg_stencil_state stencil;
    public int color_count;
    #pragma warning disable 169
    public struct colorsCollection
    {
        public ref sg_color_target_state this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sg_color_target_state _item0;
        private sg_color_target_state _item1;
        private sg_color_target_state _item2;
        private sg_color_target_state _item3;
        private sg_color_target_state _item4;
        private sg_color_target_state _item5;
        private sg_color_target_state _item6;
        private sg_color_target_state _item7;
    }
    #pragma warning restore 169
    public colorsCollection colors;
    public sg_primitive_type primitive_type;
    public sg_index_type index_type;
    public sg_cull_mode cull_mode;
    public sg_face_winding face_winding;
    public int sample_count;
    public sg_color blend_color;
#if WEB
    private byte _alpha_to_coverage_enabled;
    public bool alpha_to_coverage_enabled { get => _alpha_to_coverage_enabled != 0; set => _alpha_to_coverage_enabled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool alpha_to_coverage_enabled;
#endif
#if WEB
    private IntPtr _label;
    public string label { get => Marshal.PtrToStringAnsi(_label);  set { if (_label != IntPtr.Zero) { Marshal.FreeHGlobal(_label); _label = IntPtr.Zero; } if (value != null) { _label = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string label;
#endif
    public uint _end_canary;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_buffer_view_desc
{
    public sg_buffer buffer;
    public int offset;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_image_view_desc
{
    public sg_image image;
    public int mip_level;
    public int slice;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_texture_view_range
{
    public int _base;
    public int count;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_texture_view_desc
{
    public sg_image image;
    public sg_texture_view_range mip_levels;
    public sg_texture_view_range slices;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_view_desc
{
    public uint _start_canary;
    public sg_texture_view_desc texture;
    public sg_buffer_view_desc storage_buffer;
    public sg_image_view_desc storage_image;
    public sg_image_view_desc color_attachment;
    public sg_image_view_desc resolve_attachment;
    public sg_image_view_desc depth_stencil_attachment;
#if WEB
    private IntPtr _label;
    public string label { get => Marshal.PtrToStringAnsi(_label);  set { if (_label != IntPtr.Zero) { Marshal.FreeHGlobal(_label); _label = IntPtr.Zero; } if (value != null) { _label = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string label;
#endif
    public uint _end_canary;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_slot_info
{
    public sg_resource_state state;
    public uint res_id;
    public uint uninit_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_buffer_info
{
    public sg_slot_info slot;
    public uint update_frame_index;
    public uint append_frame_index;
    public int append_pos;
#if WEB
    private byte _append_overflow;
    public bool append_overflow { get => _append_overflow != 0; set => _append_overflow = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool append_overflow;
#endif
    public int num_slots;
    public int active_slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_image_info
{
    public sg_slot_info slot;
    public uint upd_frame_index;
    public int num_slots;
    public int active_slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_sampler_info
{
    public sg_slot_info slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_shader_info
{
    public sg_slot_info slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_pipeline_info
{
    public sg_slot_info slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_view_info
{
    public sg_slot_info slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_gl
{
    public uint num_bind_buffer;
    public uint num_active_texture;
    public uint num_bind_texture;
    public uint num_bind_sampler;
    public uint num_bind_image_texture;
    public uint num_use_program;
    public uint num_render_state;
    public uint num_vertex_attrib_pointer;
    public uint num_vertex_attrib_divisor;
    public uint num_enable_vertex_attrib_array;
    public uint num_disable_vertex_attrib_array;
    public uint num_uniform;
    public uint num_memory_barriers;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_d3d11_pass
{
    public uint num_om_set_render_targets;
    public uint num_clear_render_target_view;
    public uint num_clear_depth_stencil_view;
    public uint num_resolve_subresource;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_d3d11_pipeline
{
    public uint num_rs_set_state;
    public uint num_om_set_depth_stencil_state;
    public uint num_om_set_blend_state;
    public uint num_ia_set_primitive_topology;
    public uint num_ia_set_input_layout;
    public uint num_vs_set_shader;
    public uint num_vs_set_constant_buffers;
    public uint num_ps_set_shader;
    public uint num_ps_set_constant_buffers;
    public uint num_cs_set_shader;
    public uint num_cs_set_constant_buffers;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_d3d11_bindings
{
    public uint num_ia_set_vertex_buffers;
    public uint num_ia_set_index_buffer;
    public uint num_vs_set_shader_resources;
    public uint num_vs_set_samplers;
    public uint num_ps_set_shader_resources;
    public uint num_ps_set_samplers;
    public uint num_cs_set_shader_resources;
    public uint num_cs_set_samplers;
    public uint num_cs_set_unordered_access_views;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_d3d11_uniforms
{
    public uint num_update_subresource;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_d3d11_draw
{
    public uint num_draw_indexed_instanced;
    public uint num_draw_indexed;
    public uint num_draw_instanced;
    public uint num_draw;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_d3d11
{
    public sg_frame_stats_d3d11_pass pass;
    public sg_frame_stats_d3d11_pipeline pipeline;
    public sg_frame_stats_d3d11_bindings bindings;
    public sg_frame_stats_d3d11_uniforms uniforms;
    public sg_frame_stats_d3d11_draw draw;
    public uint num_map;
    public uint num_unmap;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_metal_idpool
{
    public uint num_added;
    public uint num_released;
    public uint num_garbage_collected;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_metal_pipeline
{
    public uint num_set_blend_color;
    public uint num_set_cull_mode;
    public uint num_set_front_facing_winding;
    public uint num_set_stencil_reference_value;
    public uint num_set_depth_bias;
    public uint num_set_render_pipeline_state;
    public uint num_set_depth_stencil_state;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_metal_bindings
{
    public uint num_set_vertex_buffer;
    public uint num_set_vertex_buffer_offset;
    public uint num_skip_redundant_vertex_buffer;
    public uint num_set_vertex_texture;
    public uint num_skip_redundant_vertex_texture;
    public uint num_set_vertex_sampler_state;
    public uint num_skip_redundant_vertex_sampler_state;
    public uint num_set_fragment_buffer;
    public uint num_set_fragment_buffer_offset;
    public uint num_skip_redundant_fragment_buffer;
    public uint num_set_fragment_texture;
    public uint num_skip_redundant_fragment_texture;
    public uint num_set_fragment_sampler_state;
    public uint num_skip_redundant_fragment_sampler_state;
    public uint num_set_compute_buffer;
    public uint num_set_compute_buffer_offset;
    public uint num_skip_redundant_compute_buffer;
    public uint num_set_compute_texture;
    public uint num_skip_redundant_compute_texture;
    public uint num_set_compute_sampler_state;
    public uint num_skip_redundant_compute_sampler_state;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_metal_uniforms
{
    public uint num_set_vertex_buffer_offset;
    public uint num_set_fragment_buffer_offset;
    public uint num_set_compute_buffer_offset;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_metal
{
    public sg_frame_stats_metal_idpool idpool;
    public sg_frame_stats_metal_pipeline pipeline;
    public sg_frame_stats_metal_bindings bindings;
    public sg_frame_stats_metal_uniforms uniforms;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_wgpu_uniforms
{
    public uint num_set_bindgroup;
    public uint size_write_buffer;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_wgpu_bindings
{
    public uint num_set_vertex_buffer;
    public uint num_skip_redundant_vertex_buffer;
    public uint num_set_index_buffer;
    public uint num_skip_redundant_index_buffer;
    public uint num_create_bindgroup;
    public uint num_discard_bindgroup;
    public uint num_set_bindgroup;
    public uint num_skip_redundant_bindgroup;
    public uint num_bindgroup_cache_hits;
    public uint num_bindgroup_cache_misses;
    public uint num_bindgroup_cache_collisions;
    public uint num_bindgroup_cache_invalidates;
    public uint num_bindgroup_cache_hash_vs_key_mismatch;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats_wgpu
{
    public sg_frame_stats_wgpu_uniforms uniforms;
    public sg_frame_stats_wgpu_bindings bindings;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_resource_stats
{
    public uint total_alive;
    public uint total_free;
    public uint allocated;
    public uint deallocated;
    public uint inited;
    public uint uninited;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_frame_stats
{
    public uint frame_index;
    public uint num_passes;
    public uint num_apply_viewport;
    public uint num_apply_scissor_rect;
    public uint num_apply_pipeline;
    public uint num_apply_bindings;
    public uint num_apply_uniforms;
    public uint num_draw;
    public uint num_draw_ex;
    public uint num_dispatch;
    public uint num_update_buffer;
    public uint num_append_buffer;
    public uint num_update_image;
    public uint size_apply_uniforms;
    public uint size_update_buffer;
    public uint size_append_buffer;
    public uint size_update_image;
    public sg_resource_stats buffers;
    public sg_resource_stats images;
    public sg_resource_stats samplers;
    public sg_resource_stats views;
    public sg_resource_stats shaders;
    public sg_resource_stats pipelines;
    public sg_frame_stats_gl gl;
    public sg_frame_stats_d3d11 d3d11;
    public sg_frame_stats_metal metal;
    public sg_frame_stats_wgpu wgpu;
}
public enum sg_log_item
{
    SG_LOGITEM_OK,
    SG_LOGITEM_MALLOC_FAILED,
    SG_LOGITEM_GL_TEXTURE_FORMAT_NOT_SUPPORTED,
    SG_LOGITEM_GL_3D_TEXTURES_NOT_SUPPORTED,
    SG_LOGITEM_GL_ARRAY_TEXTURES_NOT_SUPPORTED,
    SG_LOGITEM_GL_STORAGEBUFFER_GLSL_BINDING_OUT_OF_RANGE,
    SG_LOGITEM_GL_STORAGEIMAGE_GLSL_BINDING_OUT_OF_RANGE,
    SG_LOGITEM_GL_SHADER_COMPILATION_FAILED,
    SG_LOGITEM_GL_SHADER_LINKING_FAILED,
    SG_LOGITEM_GL_VERTEX_ATTRIBUTE_NOT_FOUND_IN_SHADER,
    SG_LOGITEM_GL_UNIFORMBLOCK_NAME_NOT_FOUND_IN_SHADER,
    SG_LOGITEM_GL_IMAGE_SAMPLER_NAME_NOT_FOUND_IN_SHADER,
    SG_LOGITEM_GL_FRAMEBUFFER_STATUS_UNDEFINED,
    SG_LOGITEM_GL_FRAMEBUFFER_STATUS_INCOMPLETE_ATTACHMENT,
    SG_LOGITEM_GL_FRAMEBUFFER_STATUS_INCOMPLETE_MISSING_ATTACHMENT,
    SG_LOGITEM_GL_FRAMEBUFFER_STATUS_UNSUPPORTED,
    SG_LOGITEM_GL_FRAMEBUFFER_STATUS_INCOMPLETE_MULTISAMPLE,
    SG_LOGITEM_GL_FRAMEBUFFER_STATUS_UNKNOWN,
    SG_LOGITEM_D3D11_FEATURE_LEVEL_0_DETECTED,
    SG_LOGITEM_D3D11_CREATE_BUFFER_FAILED,
    SG_LOGITEM_D3D11_CREATE_BUFFER_SRV_FAILED,
    SG_LOGITEM_D3D11_CREATE_BUFFER_UAV_FAILED,
    SG_LOGITEM_D3D11_CREATE_DEPTH_TEXTURE_UNSUPPORTED_PIXEL_FORMAT,
    SG_LOGITEM_D3D11_CREATE_DEPTH_TEXTURE_FAILED,
    SG_LOGITEM_D3D11_CREATE_2D_TEXTURE_UNSUPPORTED_PIXEL_FORMAT,
    SG_LOGITEM_D3D11_CREATE_2D_TEXTURE_FAILED,
    SG_LOGITEM_D3D11_CREATE_2D_SRV_FAILED,
    SG_LOGITEM_D3D11_CREATE_3D_TEXTURE_UNSUPPORTED_PIXEL_FORMAT,
    SG_LOGITEM_D3D11_CREATE_3D_TEXTURE_FAILED,
    SG_LOGITEM_D3D11_CREATE_3D_SRV_FAILED,
    SG_LOGITEM_D3D11_CREATE_MSAA_TEXTURE_FAILED,
    SG_LOGITEM_D3D11_CREATE_SAMPLER_STATE_FAILED,
    SG_LOGITEM_D3D11_UNIFORMBLOCK_HLSL_REGISTER_B_OUT_OF_RANGE,
    SG_LOGITEM_D3D11_STORAGEBUFFER_HLSL_REGISTER_T_OUT_OF_RANGE,
    SG_LOGITEM_D3D11_STORAGEBUFFER_HLSL_REGISTER_U_OUT_OF_RANGE,
    SG_LOGITEM_D3D11_IMAGE_HLSL_REGISTER_T_OUT_OF_RANGE,
    SG_LOGITEM_D3D11_STORAGEIMAGE_HLSL_REGISTER_U_OUT_OF_RANGE,
    SG_LOGITEM_D3D11_SAMPLER_HLSL_REGISTER_S_OUT_OF_RANGE,
    SG_LOGITEM_D3D11_LOAD_D3DCOMPILER_47_DLL_FAILED,
    SG_LOGITEM_D3D11_SHADER_COMPILATION_FAILED,
    SG_LOGITEM_D3D11_SHADER_COMPILATION_OUTPUT,
    SG_LOGITEM_D3D11_CREATE_CONSTANT_BUFFER_FAILED,
    SG_LOGITEM_D3D11_CREATE_INPUT_LAYOUT_FAILED,
    SG_LOGITEM_D3D11_CREATE_RASTERIZER_STATE_FAILED,
    SG_LOGITEM_D3D11_CREATE_DEPTH_STENCIL_STATE_FAILED,
    SG_LOGITEM_D3D11_CREATE_BLEND_STATE_FAILED,
    SG_LOGITEM_D3D11_CREATE_RTV_FAILED,
    SG_LOGITEM_D3D11_CREATE_DSV_FAILED,
    SG_LOGITEM_D3D11_CREATE_UAV_FAILED,
    SG_LOGITEM_D3D11_MAP_FOR_UPDATE_BUFFER_FAILED,
    SG_LOGITEM_D3D11_MAP_FOR_APPEND_BUFFER_FAILED,
    SG_LOGITEM_D3D11_MAP_FOR_UPDATE_IMAGE_FAILED,
    SG_LOGITEM_METAL_CREATE_BUFFER_FAILED,
    SG_LOGITEM_METAL_TEXTURE_FORMAT_NOT_SUPPORTED,
    SG_LOGITEM_METAL_CREATE_TEXTURE_FAILED,
    SG_LOGITEM_METAL_CREATE_SAMPLER_FAILED,
    SG_LOGITEM_METAL_SHADER_COMPILATION_FAILED,
    SG_LOGITEM_METAL_SHADER_CREATION_FAILED,
    SG_LOGITEM_METAL_SHADER_COMPILATION_OUTPUT,
    SG_LOGITEM_METAL_SHADER_ENTRY_NOT_FOUND,
    SG_LOGITEM_METAL_UNIFORMBLOCK_MSL_BUFFER_SLOT_OUT_OF_RANGE,
    SG_LOGITEM_METAL_STORAGEBUFFER_MSL_BUFFER_SLOT_OUT_OF_RANGE,
    SG_LOGITEM_METAL_STORAGEIMAGE_MSL_TEXTURE_SLOT_OUT_OF_RANGE,
    SG_LOGITEM_METAL_IMAGE_MSL_TEXTURE_SLOT_OUT_OF_RANGE,
    SG_LOGITEM_METAL_SAMPLER_MSL_SAMPLER_SLOT_OUT_OF_RANGE,
    SG_LOGITEM_METAL_CREATE_CPS_FAILED,
    SG_LOGITEM_METAL_CREATE_CPS_OUTPUT,
    SG_LOGITEM_METAL_CREATE_RPS_FAILED,
    SG_LOGITEM_METAL_CREATE_RPS_OUTPUT,
    SG_LOGITEM_METAL_CREATE_DSS_FAILED,
    SG_LOGITEM_WGPU_BINDGROUPS_POOL_EXHAUSTED,
    SG_LOGITEM_WGPU_BINDGROUPSCACHE_SIZE_GREATER_ONE,
    SG_LOGITEM_WGPU_BINDGROUPSCACHE_SIZE_POW2,
    SG_LOGITEM_WGPU_CREATEBINDGROUP_FAILED,
    SG_LOGITEM_WGPU_CREATE_BUFFER_FAILED,
    SG_LOGITEM_WGPU_CREATE_TEXTURE_FAILED,
    SG_LOGITEM_WGPU_CREATE_TEXTURE_VIEW_FAILED,
    SG_LOGITEM_WGPU_CREATE_SAMPLER_FAILED,
    SG_LOGITEM_WGPU_CREATE_SHADER_MODULE_FAILED,
    SG_LOGITEM_WGPU_SHADER_CREATE_BINDGROUP_LAYOUT_FAILED,
    SG_LOGITEM_WGPU_UNIFORMBLOCK_WGSL_GROUP0_BINDING_OUT_OF_RANGE,
    SG_LOGITEM_WGPU_TEXTURE_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
    SG_LOGITEM_WGPU_STORAGEBUFFER_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
    SG_LOGITEM_WGPU_STORAGEIMAGE_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
    SG_LOGITEM_WGPU_SAMPLER_WGSL_GROUP1_BINDING_OUT_OF_RANGE,
    SG_LOGITEM_WGPU_CREATE_PIPELINE_LAYOUT_FAILED,
    SG_LOGITEM_WGPU_CREATE_RENDER_PIPELINE_FAILED,
    SG_LOGITEM_WGPU_CREATE_COMPUTE_PIPELINE_FAILED,
    SG_LOGITEM_IDENTICAL_COMMIT_LISTENER,
    SG_LOGITEM_COMMIT_LISTENER_ARRAY_FULL,
    SG_LOGITEM_TRACE_HOOKS_NOT_ENABLED,
    SG_LOGITEM_DEALLOC_BUFFER_INVALID_STATE,
    SG_LOGITEM_DEALLOC_IMAGE_INVALID_STATE,
    SG_LOGITEM_DEALLOC_SAMPLER_INVALID_STATE,
    SG_LOGITEM_DEALLOC_SHADER_INVALID_STATE,
    SG_LOGITEM_DEALLOC_PIPELINE_INVALID_STATE,
    SG_LOGITEM_DEALLOC_VIEW_INVALID_STATE,
    SG_LOGITEM_INIT_BUFFER_INVALID_STATE,
    SG_LOGITEM_INIT_IMAGE_INVALID_STATE,
    SG_LOGITEM_INIT_SAMPLER_INVALID_STATE,
    SG_LOGITEM_INIT_SHADER_INVALID_STATE,
    SG_LOGITEM_INIT_PIPELINE_INVALID_STATE,
    SG_LOGITEM_INIT_VIEW_INVALID_STATE,
    SG_LOGITEM_UNINIT_BUFFER_INVALID_STATE,
    SG_LOGITEM_UNINIT_IMAGE_INVALID_STATE,
    SG_LOGITEM_UNINIT_SAMPLER_INVALID_STATE,
    SG_LOGITEM_UNINIT_SHADER_INVALID_STATE,
    SG_LOGITEM_UNINIT_PIPELINE_INVALID_STATE,
    SG_LOGITEM_UNINIT_VIEW_INVALID_STATE,
    SG_LOGITEM_FAIL_BUFFER_INVALID_STATE,
    SG_LOGITEM_FAIL_IMAGE_INVALID_STATE,
    SG_LOGITEM_FAIL_SAMPLER_INVALID_STATE,
    SG_LOGITEM_FAIL_SHADER_INVALID_STATE,
    SG_LOGITEM_FAIL_PIPELINE_INVALID_STATE,
    SG_LOGITEM_FAIL_VIEW_INVALID_STATE,
    SG_LOGITEM_BUFFER_POOL_EXHAUSTED,
    SG_LOGITEM_IMAGE_POOL_EXHAUSTED,
    SG_LOGITEM_SAMPLER_POOL_EXHAUSTED,
    SG_LOGITEM_SHADER_POOL_EXHAUSTED,
    SG_LOGITEM_PIPELINE_POOL_EXHAUSTED,
    SG_LOGITEM_VIEW_POOL_EXHAUSTED,
    SG_LOGITEM_BEGINPASS_TOO_MANY_COLOR_ATTACHMENTS,
    SG_LOGITEM_BEGINPASS_TOO_MANY_RESOLVE_ATTACHMENTS,
    SG_LOGITEM_BEGINPASS_ATTACHMENTS_ALIVE,
    SG_LOGITEM_DRAW_WITHOUT_BINDINGS,
    SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_TEXTURES,
    SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_TEXTURES,
    SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_TEXTURES,
    SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_STORAGEBUFFERS,
    SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_STORAGEBUFFERS,
    SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_STORAGEBUFFERS,
    SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_STORAGEIMAGES,
    SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_STORAGEIMAGES,
    SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_STORAGEIMAGES,
    SG_LOGITEM_SHADERDESC_TOO_MANY_VERTEXSTAGE_TEXTURESAMPLERPAIRS,
    SG_LOGITEM_SHADERDESC_TOO_MANY_FRAGMENTSTAGE_TEXTURESAMPLERPAIRS,
    SG_LOGITEM_SHADERDESC_TOO_MANY_COMPUTESTAGE_TEXTURESAMPLERPAIRS,
    SG_LOGITEM_VALIDATE_BUFFERDESC_CANARY,
    SG_LOGITEM_VALIDATE_BUFFERDESC_IMMUTABLE_DYNAMIC_STREAM,
    SG_LOGITEM_VALIDATE_BUFFERDESC_SEPARATE_BUFFER_TYPES,
    SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_NONZERO_SIZE,
    SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_MATCHING_DATA_SIZE,
    SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_ZERO_DATA_SIZE,
    SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_NO_DATA,
    SG_LOGITEM_VALIDATE_BUFFERDESC_EXPECT_DATA,
    SG_LOGITEM_VALIDATE_BUFFERDESC_STORAGEBUFFER_SUPPORTED,
    SG_LOGITEM_VALIDATE_BUFFERDESC_STORAGEBUFFER_SIZE_MULTIPLE_4,
    SG_LOGITEM_VALIDATE_IMAGEDATA_NODATA,
    SG_LOGITEM_VALIDATE_IMAGEDATA_DATA_SIZE,
    SG_LOGITEM_VALIDATE_IMAGEDESC_CANARY,
    SG_LOGITEM_VALIDATE_IMAGEDESC_IMMUTABLE_DYNAMIC_STREAM,
    SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_2D_NUMSLICES,
    SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_CUBE_NUMSLICES,
    SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_ARRAY_NUMSLICES,
    SG_LOGITEM_VALIDATE_IMAGEDESC_IMAGETYPE_3D_NUMSLICES,
    SG_LOGITEM_VALIDATE_IMAGEDESC_NUMSLICES,
    SG_LOGITEM_VALIDATE_IMAGEDESC_WIDTH,
    SG_LOGITEM_VALIDATE_IMAGEDESC_HEIGHT,
    SG_LOGITEM_VALIDATE_IMAGEDESC_NONRT_PIXELFORMAT,
    SG_LOGITEM_VALIDATE_IMAGEDESC_MSAA_BUT_NO_ATTACHMENT,
    SG_LOGITEM_VALIDATE_IMAGEDESC_DEPTH_3D_IMAGE,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_EXPECT_IMMUTABLE,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_EXPECT_NO_DATA,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_PIXELFORMAT,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_RESOLVE_EXPECT_NO_MSAA,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_NO_MSAA_SUPPORT,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_NUM_MIPMAPS,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_3D_IMAGE,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_CUBE_IMAGE,
    SG_LOGITEM_VALIDATE_IMAGEDESC_ATTACHMENT_MSAA_ARRAY_IMAGE,
    SG_LOGITEM_VALIDATE_IMAGEDESC_STORAGEIMAGE_PIXELFORMAT,
    SG_LOGITEM_VALIDATE_IMAGEDESC_STORAGEIMAGE_EXPECT_NO_MSAA,
    SG_LOGITEM_VALIDATE_IMAGEDESC_INJECTED_NO_DATA,
    SG_LOGITEM_VALIDATE_IMAGEDESC_DYNAMIC_NO_DATA,
    SG_LOGITEM_VALIDATE_IMAGEDESC_COMPRESSED_IMMUTABLE,
    SG_LOGITEM_VALIDATE_SAMPLERDESC_CANARY,
    SG_LOGITEM_VALIDATE_SAMPLERDESC_ANISTROPIC_REQUIRES_LINEAR_FILTERING,
    SG_LOGITEM_VALIDATE_SHADERDESC_CANARY,
    SG_LOGITEM_VALIDATE_SHADERDESC_VERTEX_SOURCE,
    SG_LOGITEM_VALIDATE_SHADERDESC_FRAGMENT_SOURCE,
    SG_LOGITEM_VALIDATE_SHADERDESC_COMPUTE_SOURCE,
    SG_LOGITEM_VALIDATE_SHADERDESC_VERTEX_SOURCE_OR_BYTECODE,
    SG_LOGITEM_VALIDATE_SHADERDESC_FRAGMENT_SOURCE_OR_BYTECODE,
    SG_LOGITEM_VALIDATE_SHADERDESC_COMPUTE_SOURCE_OR_BYTECODE,
    SG_LOGITEM_VALIDATE_SHADERDESC_INVALID_SHADER_COMBO,
    SG_LOGITEM_VALIDATE_SHADERDESC_NO_BYTECODE_SIZE,
    SG_LOGITEM_VALIDATE_SHADERDESC_METAL_THREADS_PER_THREADGROUP_INITIALIZED,
    SG_LOGITEM_VALIDATE_SHADERDESC_METAL_THREADS_PER_THREADGROUP_MULTIPLE_32,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_NO_CONT_MEMBERS,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_SIZE_IS_ZERO,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_METAL_BUFFER_SLOT_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_HLSL_REGISTER_B_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_WGSL_GROUP0_BINDING_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_NO_MEMBERS,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_UNIFORM_GLSL_NAME,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_SIZE_MISMATCH,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_ARRAY_COUNT,
    SG_LOGITEM_VALIDATE_SHADERDESC_UNIFORMBLOCK_STD140_ARRAY_TYPE,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_METAL_BUFFER_SLOT_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_HLSL_REGISTER_T_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_HLSL_REGISTER_U_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_GLSL_BINDING_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEBUFFER_WGSL_GROUP1_BINDING_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_EXPECT_COMPUTE_STAGE,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_METAL_TEXTURE_SLOT_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_HLSL_REGISTER_U_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_GLSL_BINDING_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_STORAGEIMAGE_WGSL_GROUP1_BINDING_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_TEXTURE_METAL_TEXTURE_SLOT_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_TEXTURE_HLSL_REGISTER_T_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_VIEW_TEXTURE_WGSL_GROUP1_BINDING_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_METAL_SAMPLER_SLOT_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_HLSL_REGISTER_S_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_WGSL_GROUP1_BINDING_COLLISION,
    SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_VIEW_SLOT_OUT_OF_RANGE,
    SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_SAMPLER_SLOT_OUT_OF_RANGE,
    SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_TEXTURE_STAGE_MISMATCH,
    SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_EXPECT_TEXTURE_VIEW,
    SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_SAMPLER_STAGE_MISMATCH,
    SG_LOGITEM_VALIDATE_SHADERDESC_TEXTURE_SAMPLER_PAIR_GLSL_NAME,
    SG_LOGITEM_VALIDATE_SHADERDESC_NONFILTERING_SAMPLER_REQUIRED,
    SG_LOGITEM_VALIDATE_SHADERDESC_COMPARISON_SAMPLER_REQUIRED,
    SG_LOGITEM_VALIDATE_SHADERDESC_TEXVIEW_NOT_REFERENCED_BY_TEXTURE_SAMPLER_PAIRS,
    SG_LOGITEM_VALIDATE_SHADERDESC_SAMPLER_NOT_REFERENCED_BY_TEXTURE_SAMPLER_PAIRS,
    SG_LOGITEM_VALIDATE_SHADERDESC_ATTR_STRING_TOO_LONG,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_CANARY,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_SHADER,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_COMPUTE_SHADER_EXPECTED,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_NO_COMPUTE_SHADER_EXPECTED,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_NO_CONT_ATTRS,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_ATTR_BASETYPE_MISMATCH,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_LAYOUT_STRIDE4,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_ATTR_SEMANTICS,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_SHADER_READONLY_STORAGEBUFFERS,
    SG_LOGITEM_VALIDATE_PIPELINEDESC_BLENDOP_MINMAX_REQUIRES_BLENDFACTOR_ONE,
    SG_LOGITEM_VALIDATE_VIEWDESC_CANARY,
    SG_LOGITEM_VALIDATE_VIEWDESC_UNIQUE_VIEWTYPE,
    SG_LOGITEM_VALIDATE_VIEWDESC_ANY_VIEWTYPE,
    SG_LOGITEM_VALIDATE_VIEWDESC_RESOURCE_ALIVE,
    SG_LOGITEM_VALIDATE_VIEWDESC_RESOURCE_FAILED,
    SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEBUFFER_OFFSET_VS_BUFFER_SIZE,
    SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEBUFFER_OFFSET_MULTIPLE_256,
    SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEBUFFER_USAGE,
    SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEIMAGE_USAGE,
    SG_LOGITEM_VALIDATE_VIEWDESC_COLORATTACHMENT_USAGE,
    SG_LOGITEM_VALIDATE_VIEWDESC_RESOLVEATTACHMENT_USAGE,
    SG_LOGITEM_VALIDATE_VIEWDESC_DEPTHSTENCILATTACHMENT_USAGE,
    SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_MIPLEVEL,
    SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_2D_SLICE,
    SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_CUBEMAP_SLICE,
    SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_ARRAY_SLICE,
    SG_LOGITEM_VALIDATE_VIEWDESC_IMAGE_3D_SLICE,
    SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_EXPECT_NO_MSAA,
    SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_MIPLEVELS,
    SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_2D_SLICES,
    SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_CUBEMAP_SLICES,
    SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_ARRAY_SLICES,
    SG_LOGITEM_VALIDATE_VIEWDESC_TEXTURE_3D_SLICES,
    SG_LOGITEM_VALIDATE_VIEWDESC_STORAGEIMAGE_PIXELFORMAT,
    SG_LOGITEM_VALIDATE_VIEWDESC_COLORATTACHMENT_PIXELFORMAT,
    SG_LOGITEM_VALIDATE_VIEWDESC_DEPTHSTENCILATTACHMENT_PIXELFORMAT,
    SG_LOGITEM_VALIDATE_VIEWDESC_RESOLVEATTACHMENT_SAMPLECOUNT,
    SG_LOGITEM_VALIDATE_BEGINPASS_CANARY,
    SG_LOGITEM_VALIDATE_BEGINPASS_COMPUTEPASS_EXPECT_NO_ATTACHMENTS,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_WIDTH,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_WIDTH_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_HEIGHT,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_HEIGHT_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_SAMPLECOUNT,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_SAMPLECOUNT_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_COLORFORMAT,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_COLORFORMAT_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_EXPECT_DEPTHFORMAT_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_CURRENTDRAWABLE,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_CURRENTDRAWABLE_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_DEPTHSTENCILTEXTURE,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_DEPTHSTENCILTEXTURE_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_MSAACOLORTEXTURE,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_METAL_EXPECT_MSAACOLORTEXTURE_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RENDERVIEW,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RENDERVIEW_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RESOLVEVIEW,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_RESOLVEVIEW_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_DEPTHSTENCILVIEW,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_D3D11_EXPECT_DEPTHSTENCILVIEW_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RENDERVIEW,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RENDERVIEW_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RESOLVEVIEW,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_RESOLVEVIEW_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_DEPTHSTENCILVIEW,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_WGPU_EXPECT_DEPTHSTENCILVIEW_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_SWAPCHAIN_GL_EXPECT_FRAMEBUFFER_NOTSET,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEWS_CONTINUOUS,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_ALIVE,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_VALID,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_TYPE,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_IMAGE_ALIVE,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_IMAGE_VALID,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_SIZES,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_SAMPLECOUNT,
    SG_LOGITEM_VALIDATE_BEGINPASS_COLORATTACHMENTVIEW_SAMPLECOUNTS_EQUAL,
    SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_NO_COLORATTACHMENTVIEW,
    SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_ALIVE,
    SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_VALID,
    SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_TYPE,
    SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_IMAGE_ALIVE,
    SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_IMAGE_VALID,
    SG_LOGITEM_VALIDATE_BEGINPASS_RESOLVEATTACHMENTVIEW_SIZES,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEWS_CONTINUOUS,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_ALIVE,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_VALID,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_TYPE,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_IMAGE_ALIVE,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_IMAGE_VALID,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_SIZES,
    SG_LOGITEM_VALIDATE_BEGINPASS_DEPTHSTENCILATTACHMENTVIEW_SAMPLECOUNT,
    SG_LOGITEM_VALIDATE_BEGINPASS_ATTACHMENTS_EXPECTED,
    SG_LOGITEM_VALIDATE_AVP_RENDERPASS_EXPECTED,
    SG_LOGITEM_VALIDATE_ASR_RENDERPASS_EXPECTED,
    SG_LOGITEM_VALIDATE_APIP_PIPELINE_VALID_ID,
    SG_LOGITEM_VALIDATE_APIP_PIPELINE_EXISTS,
    SG_LOGITEM_VALIDATE_APIP_PIPELINE_VALID,
    SG_LOGITEM_VALIDATE_APIP_PASS_EXPECTED,
    SG_LOGITEM_VALIDATE_APIP_PIPELINE_SHADER_ALIVE,
    SG_LOGITEM_VALIDATE_APIP_PIPELINE_SHADER_VALID,
    SG_LOGITEM_VALIDATE_APIP_COMPUTEPASS_EXPECTED,
    SG_LOGITEM_VALIDATE_APIP_RENDERPASS_EXPECTED,
    SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_COLOR_COUNT,
    SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_COLOR_FORMAT,
    SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_DEPTH_FORMAT,
    SG_LOGITEM_VALIDATE_APIP_SWAPCHAIN_SAMPLE_COUNT,
    SG_LOGITEM_VALIDATE_APIP_ATTACHMENTS_ALIVE,
    SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_COUNT,
    SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_VIEW_VALID,
    SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_IMAGE_VALID,
    SG_LOGITEM_VALIDATE_APIP_COLORATTACHMENTS_FORMAT,
    SG_LOGITEM_VALIDATE_APIP_DEPTHSTENCILATTACHMENT_VIEW_VALID,
    SG_LOGITEM_VALIDATE_APIP_DEPTHSTENCILATTACHMENT_IMAGE_VALID,
    SG_LOGITEM_VALIDATE_APIP_DEPTHSTENCILATTACHMENT_FORMAT,
    SG_LOGITEM_VALIDATE_APIP_ATTACHMENT_SAMPLE_COUNT,
    SG_LOGITEM_VALIDATE_ABND_PASS_EXPECTED,
    SG_LOGITEM_VALIDATE_ABND_EMPTY_BINDINGS,
    SG_LOGITEM_VALIDATE_ABND_NO_PIPELINE,
    SG_LOGITEM_VALIDATE_ABND_PIPELINE_ALIVE,
    SG_LOGITEM_VALIDATE_ABND_PIPELINE_VALID,
    SG_LOGITEM_VALIDATE_ABND_PIPELINE_SHADER_ALIVE,
    SG_LOGITEM_VALIDATE_ABND_PIPELINE_SHADER_VALID,
    SG_LOGITEM_VALIDATE_ABND_COMPUTE_EXPECTED_NO_VBUFS,
    SG_LOGITEM_VALIDATE_ABND_COMPUTE_EXPECTED_NO_IBUF,
    SG_LOGITEM_VALIDATE_ABND_EXPECTED_VBUF,
    SG_LOGITEM_VALIDATE_ABND_VBUF_ALIVE,
    SG_LOGITEM_VALIDATE_ABND_VBUF_USAGE,
    SG_LOGITEM_VALIDATE_ABND_VBUF_OVERFLOW,
    SG_LOGITEM_VALIDATE_ABND_EXPECTED_NO_IBUF,
    SG_LOGITEM_VALIDATE_ABND_EXPECTED_IBUF,
    SG_LOGITEM_VALIDATE_ABND_IBUF_ALIVE,
    SG_LOGITEM_VALIDATE_ABND_IBUF_USAGE,
    SG_LOGITEM_VALIDATE_ABND_IBUF_OVERFLOW,
    SG_LOGITEM_VALIDATE_ABND_EXPECTED_VIEW_BINDING,
    SG_LOGITEM_VALIDATE_ABND_VIEW_ALIVE,
    SG_LOGITEM_VALIDATE_ABND_EXPECT_TEXVIEW,
    SG_LOGITEM_VALIDATE_ABND_EXPECT_SBVIEW,
    SG_LOGITEM_VALIDATE_ABND_EXPECT_SIMGVIEW,
    SG_LOGITEM_VALIDATE_ABND_TEXVIEW_IMAGETYPE_MISMATCH,
    SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_MULTISAMPLED_IMAGE,
    SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_NON_MULTISAMPLED_IMAGE,
    SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_FILTERABLE_IMAGE,
    SG_LOGITEM_VALIDATE_ABND_TEXVIEW_EXPECTED_DEPTH_IMAGE,
    SG_LOGITEM_VALIDATE_ABND_SBVIEW_READWRITE_IMMUTABLE,
    SG_LOGITEM_VALIDATE_ABND_SIMGVIEW_COMPUTE_PASS_EXPECTED,
    SG_LOGITEM_VALIDATE_ABND_SIMGVIEW_IMAGETYPE_MISMATCH,
    SG_LOGITEM_VALIDATE_ABND_SIMGVIEW_ACCESSFORMAT,
    SG_LOGITEM_VALIDATE_ABND_EXPECTED_SAMPLER_BINDING,
    SG_LOGITEM_VALIDATE_ABND_UNEXPECTED_SAMPLER_COMPARE_NEVER,
    SG_LOGITEM_VALIDATE_ABND_EXPECTED_SAMPLER_COMPARE_NEVER,
    SG_LOGITEM_VALIDATE_ABND_EXPECTED_NONFILTERING_SAMPLER,
    SG_LOGITEM_VALIDATE_ABND_SAMPLER_ALIVE,
    SG_LOGITEM_VALIDATE_ABND_SAMPLER_VALID,
    SG_LOGITEM_VALIDATE_ABND_TEXTURE_BINDING_VS_DEPTHSTENCIL_ATTACHMENT,
    SG_LOGITEM_VALIDATE_ABND_TEXTURE_BINDING_VS_COLOR_ATTACHMENT,
    SG_LOGITEM_VALIDATE_ABND_TEXTURE_BINDING_VS_RESOLVE_ATTACHMENT,
    SG_LOGITEM_VALIDATE_ABND_TEXTURE_VS_STORAGEIMAGE_BINDING,
    SG_LOGITEM_VALIDATE_AU_PASS_EXPECTED,
    SG_LOGITEM_VALIDATE_AU_NO_PIPELINE,
    SG_LOGITEM_VALIDATE_AU_PIPELINE_ALIVE,
    SG_LOGITEM_VALIDATE_AU_PIPELINE_VALID,
    SG_LOGITEM_VALIDATE_AU_PIPELINE_SHADER_ALIVE,
    SG_LOGITEM_VALIDATE_AU_PIPELINE_SHADER_VALID,
    SG_LOGITEM_VALIDATE_AU_NO_UNIFORMBLOCK_AT_SLOT,
    SG_LOGITEM_VALIDATE_AU_SIZE,
    SG_LOGITEM_VALIDATE_DRAW_RENDERPASS_EXPECTED,
    SG_LOGITEM_VALIDATE_DRAW_BASEELEMENT_GE_ZERO,
    SG_LOGITEM_VALIDATE_DRAW_NUMELEMENTS_GE_ZERO,
    SG_LOGITEM_VALIDATE_DRAW_NUMINSTANCES_GE_ZERO,
    SG_LOGITEM_VALIDATE_DRAW_EX_RENDERPASS_EXPECTED,
    SG_LOGITEM_VALIDATE_DRAW_EX_BASEELEMENT_GE_ZERO,
    SG_LOGITEM_VALIDATE_DRAW_EX_NUMELEMENTS_GE_ZERO,
    SG_LOGITEM_VALIDATE_DRAW_EX_NUMINSTANCES_GE_ZERO,
    SG_LOGITEM_VALIDATE_DRAW_EX_BASEINSTANCE_GE_ZERO,
    SG_LOGITEM_VALIDATE_DRAW_EX_BASEVERTEX_VS_INDEXED,
    SG_LOGITEM_VALIDATE_DRAW_EX_BASEINSTANCE_VS_INSTANCED,
    SG_LOGITEM_VALIDATE_DRAW_EX_BASEVERTEX_NOT_SUPPORTED,
    SG_LOGITEM_VALIDATE_DRAW_EX_BASEINSTANCE_NOT_SUPPORTED,
    SG_LOGITEM_VALIDATE_DRAW_REQUIRED_BINDINGS_OR_UNIFORMS_MISSING,
    SG_LOGITEM_VALIDATE_DISPATCH_COMPUTEPASS_EXPECTED,
    SG_LOGITEM_VALIDATE_DISPATCH_NUMGROUPSX,
    SG_LOGITEM_VALIDATE_DISPATCH_NUMGROUPSY,
    SG_LOGITEM_VALIDATE_DISPATCH_NUMGROUPSZ,
    SG_LOGITEM_VALIDATE_DISPATCH_REQUIRED_BINDINGS_OR_UNIFORMS_MISSING,
    SG_LOGITEM_VALIDATE_UPDATEBUF_USAGE,
    SG_LOGITEM_VALIDATE_UPDATEBUF_SIZE,
    SG_LOGITEM_VALIDATE_UPDATEBUF_ONCE,
    SG_LOGITEM_VALIDATE_UPDATEBUF_APPEND,
    SG_LOGITEM_VALIDATE_APPENDBUF_USAGE,
    SG_LOGITEM_VALIDATE_APPENDBUF_SIZE,
    SG_LOGITEM_VALIDATE_APPENDBUF_UPDATE,
    SG_LOGITEM_VALIDATE_UPDIMG_USAGE,
    SG_LOGITEM_VALIDATE_UPDIMG_ONCE,
    SG_LOGITEM_VALIDATION_FAILED,
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_environment_defaults
{
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_metal_environment
{
    public void* device;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_environment
{
    public void* device;
    public void* device_context;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_environment
{
    public void* device;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_environment
{
    public sg_environment_defaults defaults;
    public sg_metal_environment metal;
    public sg_d3d11_environment d3d11;
    public sg_wgpu_environment wgpu;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_commit_listener
{
    public delegate* unmanaged<void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_allocator
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_logger
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_desc
{
    public uint _start_canary;
    public int buffer_pool_size;
    public int image_pool_size;
    public int sampler_pool_size;
    public int shader_pool_size;
    public int pipeline_pool_size;
    public int view_pool_size;
    public int uniform_buffer_size;
    public int max_commit_listeners;
#if WEB
    private byte _disable_validation;
    public bool disable_validation { get => _disable_validation != 0; set => _disable_validation = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool disable_validation;
#endif
#if WEB
    private byte _enforce_portable_limits;
    public bool enforce_portable_limits { get => _enforce_portable_limits != 0; set => _enforce_portable_limits = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enforce_portable_limits;
#endif
#if WEB
    private byte _d3d11_shader_debugging;
    public bool d3d11_shader_debugging { get => _d3d11_shader_debugging != 0; set => _d3d11_shader_debugging = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool d3d11_shader_debugging;
#endif
#if WEB
    private byte _mtl_force_managed_storage_mode;
    public bool mtl_force_managed_storage_mode { get => _mtl_force_managed_storage_mode != 0; set => _mtl_force_managed_storage_mode = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool mtl_force_managed_storage_mode;
#endif
#if WEB
    private byte _mtl_use_command_buffer_with_retained_references;
    public bool mtl_use_command_buffer_with_retained_references { get => _mtl_use_command_buffer_with_retained_references != 0; set => _mtl_use_command_buffer_with_retained_references = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool mtl_use_command_buffer_with_retained_references;
#endif
#if WEB
    private byte _wgpu_disable_bindgroups_cache;
    public bool wgpu_disable_bindgroups_cache { get => _wgpu_disable_bindgroups_cache != 0; set => _wgpu_disable_bindgroups_cache = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool wgpu_disable_bindgroups_cache;
#endif
    public int wgpu_bindgroups_cache_size;
    public sg_allocator allocator;
    public sg_logger logger;
    public sg_environment environment;
    public uint _end_canary;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_setup(in sg_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_shutdown();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_isvalid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_isvalid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sg_isvalid();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_reset_state_cache", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_reset_state_cache", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_reset_state_cache();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_push_debug_group", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_push_debug_group", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_push_debug_group([M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_pop_debug_group", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_pop_debug_group", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_pop_debug_group();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_add_commit_listener", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_add_commit_listener", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sg_add_commit_listener(sg_commit_listener listener);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_remove_commit_listener", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_remove_commit_listener", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sg_remove_commit_listener(sg_commit_listener listener);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_make_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_make_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_make_buffer_internal(in sg_buffer_desc desc);
public static sg_buffer sg_make_buffer(in sg_buffer_desc desc)
{
    uint _id = sg_make_buffer_internal(desc);
    return new sg_buffer { id = _id };
}
#else
public static extern sg_buffer sg_make_buffer(in sg_buffer_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_make_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_make_image", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_make_image_internal(in sg_image_desc desc);
public static sg_image sg_make_image(in sg_image_desc desc)
{
    uint _id = sg_make_image_internal(desc);
    return new sg_image { id = _id };
}
#else
public static extern sg_image sg_make_image(in sg_image_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_make_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_make_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_make_sampler_internal(in sg_sampler_desc desc);
public static sg_sampler sg_make_sampler(in sg_sampler_desc desc)
{
    uint _id = sg_make_sampler_internal(desc);
    return new sg_sampler { id = _id };
}
#else
public static extern sg_sampler sg_make_sampler(in sg_sampler_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_make_shader", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_make_shader", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_make_shader_internal(in sg_shader_desc desc);
public static sg_shader sg_make_shader(in sg_shader_desc desc)
{
    uint _id = sg_make_shader_internal(desc);
    return new sg_shader { id = _id };
}
#else
public static extern sg_shader sg_make_shader(in sg_shader_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_make_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_make_pipeline_internal(in sg_pipeline_desc desc);
public static sg_pipeline sg_make_pipeline(in sg_pipeline_desc desc)
{
    uint _id = sg_make_pipeline_internal(desc);
    return new sg_pipeline { id = _id };
}
#else
public static extern sg_pipeline sg_make_pipeline(in sg_pipeline_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_make_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_make_view", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_make_view_internal(in sg_view_desc desc);
public static sg_view sg_make_view(in sg_view_desc desc)
{
    uint _id = sg_make_view_internal(desc);
    return new sg_view { id = _id };
}
#else
public static extern sg_view sg_make_view(in sg_view_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_destroy_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_destroy_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_destroy_buffer(sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_destroy_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_destroy_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_destroy_image(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_destroy_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_destroy_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_destroy_sampler(sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_destroy_shader", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_destroy_shader", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_destroy_shader(sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_destroy_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_destroy_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_destroy_pipeline(sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_destroy_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_destroy_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_destroy_view(sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_update_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_update_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_update_buffer(sg_buffer buf, in sg_range data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_update_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_update_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_update_image(sg_image img, in sg_image_data data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_append_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_append_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_append_buffer(sg_buffer buf, in sg_range data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_overflow", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_overflow", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sg_query_buffer_overflow(sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_will_overflow", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_will_overflow", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sg_query_buffer_will_overflow(sg_buffer buf, nuint size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_begin_pass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_begin_pass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_begin_pass(in sg_pass pass);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_apply_viewport", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_apply_viewport", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_apply_viewport(int x, int y, int width, int height, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_apply_viewportf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_apply_viewportf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_apply_viewportf(float x, float y, float width, float height, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_apply_scissor_rect", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_apply_scissor_rect", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_apply_scissor_rect(int x, int y, int width, int height, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_apply_scissor_rectf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_apply_scissor_rectf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_apply_scissor_rectf(float x, float y, float width, float height, bool origin_top_left);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_apply_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_apply_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_apply_pipeline(sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_apply_bindings", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_apply_bindings", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_apply_bindings(in sg_bindings bindings);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_apply_uniforms", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_apply_uniforms", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_apply_uniforms(int ub_slot, in sg_range data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_draw", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_draw", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_draw(uint base_element, uint num_elements, uint num_instances);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_draw_ex", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_draw_ex", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_draw_ex(int base_element, int num_elements, int num_instances, int base_vertex, int base_instance);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_dispatch", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_dispatch", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_dispatch(int num_groups_x, int num_groups_y, int num_groups_z);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_end_pass", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_end_pass", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_end_pass();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_commit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_commit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_commit();

#if WEB
public static sg_desc sg_query_desc()
{
    sg_desc result = default;
    sg_query_desc_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_desc sg_query_desc();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_backend", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_backend", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_backend sg_query_backend();

#if WEB
public static sg_features sg_query_features()
{
    sg_features result = default;
    sg_query_features_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_features", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_features", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_features sg_query_features();
#endif

#if WEB
public static sg_limits sg_query_limits()
{
    sg_limits result = default;
    sg_query_limits_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_limits", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_limits", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_limits sg_query_limits();
#endif

#if WEB
public static sg_pixelformat_info sg_query_pixelformat(sg_pixel_format fmt)
{
    sg_pixelformat_info result = default;
    sg_query_pixelformat_internal(ref result, fmt);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pixelformat", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pixelformat", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_pixelformat_info sg_query_pixelformat(sg_pixel_format fmt);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_row_pitch", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_row_pitch", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_query_row_pitch(sg_pixel_format fmt, int width, int row_align_bytes);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_surface_pitch", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_surface_pitch", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_query_surface_pitch(sg_pixel_format fmt, int width, int height, int row_align_bytes);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_resource_state sg_query_buffer_state(sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_resource_state sg_query_image_state(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_sampler_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_sampler_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_resource_state sg_query_sampler_state(sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_shader_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_shader_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_resource_state sg_query_shader_state(sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pipeline_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pipeline_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_resource_state sg_query_pipeline_state(sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_resource_state sg_query_view_state(sg_view view);

#if WEB
public static sg_buffer_info sg_query_buffer_info(sg_buffer buf)
{
    sg_buffer_info result = default;
    sg_query_buffer_info_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer_info sg_query_buffer_info(sg_buffer buf);
#endif

#if WEB
public static sg_image_info sg_query_image_info(sg_image img)
{
    sg_image_info result = default;
    sg_query_image_info_internal(ref result, img);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image_info sg_query_image_info(sg_image img);
#endif

#if WEB
public static sg_sampler_info sg_query_sampler_info(sg_sampler smp)
{
    sg_sampler_info result = default;
    sg_query_sampler_info_internal(ref result, smp);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_sampler_info sg_query_sampler_info(sg_sampler smp);
#endif

#if WEB
public static sg_shader_info sg_query_shader_info(sg_shader shd)
{
    sg_shader_info result = default;
    sg_query_shader_info_internal(ref result, shd);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_shader_info sg_query_shader_info(sg_shader shd);
#endif

#if WEB
public static sg_pipeline_info sg_query_pipeline_info(sg_pipeline pip)
{
    sg_pipeline_info result = default;
    sg_query_pipeline_info_internal(ref result, pip);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_pipeline_info sg_query_pipeline_info(sg_pipeline pip);
#endif

#if WEB
public static sg_view_info sg_query_view_info(sg_view view)
{
    sg_view_info result = default;
    sg_query_view_info_internal(ref result, view);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_view_info sg_query_view_info(sg_view view);
#endif

#if WEB
public static sg_buffer_desc sg_query_buffer_desc(sg_buffer buf)
{
    sg_buffer_desc result = default;
    sg_query_buffer_desc_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer_desc sg_query_buffer_desc(sg_buffer buf);
#endif

#if WEB
public static sg_image_desc sg_query_image_desc(sg_image img)
{
    sg_image_desc result = default;
    sg_query_image_desc_internal(ref result, img);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image_desc sg_query_image_desc(sg_image img);
#endif

#if WEB
public static sg_sampler_desc sg_query_sampler_desc(sg_sampler smp)
{
    sg_sampler_desc result = default;
    sg_query_sampler_desc_internal(ref result, smp);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_sampler_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_sampler_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_sampler_desc sg_query_sampler_desc(sg_sampler smp);
#endif

#if WEB
public static sg_shader_desc sg_query_shader_desc(sg_shader shd)
{
    sg_shader_desc result = default;
    sg_query_shader_desc_internal(ref result, shd);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_shader_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_shader_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_shader_desc sg_query_shader_desc(sg_shader shd);
#endif

#if WEB
public static sg_pipeline_desc sg_query_pipeline_desc(sg_pipeline pip)
{
    sg_pipeline_desc result = default;
    sg_query_pipeline_desc_internal(ref result, pip);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pipeline_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pipeline_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_pipeline_desc sg_query_pipeline_desc(sg_pipeline pip);
#endif

#if WEB
public static sg_view_desc sg_query_view_desc(sg_view view)
{
    sg_view_desc result = default;
    sg_query_view_desc_internal(ref result, view);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_view_desc sg_query_view_desc(sg_view view);
#endif

#if WEB
public static sg_buffer_desc sg_query_buffer_defaults(in sg_buffer_desc desc)
{
    sg_buffer_desc result = default;
    sg_query_buffer_defaults_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_defaults", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_defaults", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer_desc sg_query_buffer_defaults(in sg_buffer_desc desc);
#endif

#if WEB
public static sg_image_desc sg_query_image_defaults(in sg_image_desc desc)
{
    sg_image_desc result = default;
    sg_query_image_defaults_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_defaults", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_defaults", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image_desc sg_query_image_defaults(in sg_image_desc desc);
#endif

#if WEB
public static sg_sampler_desc sg_query_sampler_defaults(in sg_sampler_desc desc)
{
    sg_sampler_desc result = default;
    sg_query_sampler_defaults_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_sampler_defaults", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_sampler_defaults", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_sampler_desc sg_query_sampler_defaults(in sg_sampler_desc desc);
#endif

#if WEB
public static sg_shader_desc sg_query_shader_defaults(in sg_shader_desc desc)
{
    sg_shader_desc result = default;
    sg_query_shader_defaults_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_shader_defaults", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_shader_defaults", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_shader_desc sg_query_shader_defaults(in sg_shader_desc desc);
#endif

#if WEB
public static sg_pipeline_desc sg_query_pipeline_defaults(in sg_pipeline_desc desc)
{
    sg_pipeline_desc result = default;
    sg_query_pipeline_defaults_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pipeline_defaults", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pipeline_defaults", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_pipeline_desc sg_query_pipeline_defaults(in sg_pipeline_desc desc);
#endif

#if WEB
public static sg_view_desc sg_query_view_defaults(in sg_view_desc desc)
{
    sg_view_desc result = default;
    sg_query_view_defaults_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_defaults", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_defaults", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_view_desc sg_query_view_defaults(in sg_view_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_size", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_size", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern nuint sg_query_buffer_size(sg_buffer buf);

#if WEB
public static sg_buffer_usage sg_query_buffer_usage(sg_buffer buf)
{
    sg_buffer_usage result = default;
    sg_query_buffer_usage_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_usage", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_usage", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer_usage sg_query_buffer_usage(sg_buffer buf);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_type", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_type", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image_type sg_query_image_type(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_width", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_width", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_query_image_width(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_height", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_height", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_query_image_height(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_num_slices", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_num_slices", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_query_image_num_slices(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_num_mipmaps", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_num_mipmaps", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_query_image_num_mipmaps(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_pixelformat", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_pixelformat", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_pixel_format sg_query_image_pixelformat(sg_image img);

#if WEB
public static sg_image_usage sg_query_image_usage(sg_image img)
{
    sg_image_usage result = default;
    sg_query_image_usage_internal(ref result, img);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_usage", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_usage", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image_usage sg_query_image_usage(sg_image img);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_sample_count", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_sample_count", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sg_query_image_sample_count(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_type", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_type", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_view_type sg_query_view_type(sg_view view);

#if WEB
public static sg_image sg_query_view_image(sg_view view)
{
    sg_image result = default;
    sg_query_view_image_internal(ref result, view);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image sg_query_view_image(sg_view view);
#endif

#if WEB
public static sg_buffer sg_query_view_buffer(sg_view view)
{
    sg_buffer result = default;
    sg_query_view_buffer_internal(ref result, view);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_buffer sg_query_view_buffer(sg_view view);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_alloc_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_alloc_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_alloc_buffer_internal();
public static sg_buffer sg_alloc_buffer()
{
    uint _id = sg_alloc_buffer_internal();
    return new sg_buffer { id = _id };
}
#else
public static extern sg_buffer sg_alloc_buffer();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_alloc_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_alloc_image", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_alloc_image_internal();
public static sg_image sg_alloc_image()
{
    uint _id = sg_alloc_image_internal();
    return new sg_image { id = _id };
}
#else
public static extern sg_image sg_alloc_image();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_alloc_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_alloc_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_alloc_sampler_internal();
public static sg_sampler sg_alloc_sampler()
{
    uint _id = sg_alloc_sampler_internal();
    return new sg_sampler { id = _id };
}
#else
public static extern sg_sampler sg_alloc_sampler();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_alloc_shader", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_alloc_shader", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_alloc_shader_internal();
public static sg_shader sg_alloc_shader()
{
    uint _id = sg_alloc_shader_internal();
    return new sg_shader { id = _id };
}
#else
public static extern sg_shader sg_alloc_shader();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_alloc_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_alloc_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_alloc_pipeline_internal();
public static sg_pipeline sg_alloc_pipeline()
{
    uint _id = sg_alloc_pipeline_internal();
    return new sg_pipeline { id = _id };
}
#else
public static extern sg_pipeline sg_alloc_pipeline();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_alloc_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_alloc_view", CallingConvention = CallingConvention.Cdecl)]
#endif
#if WEB
static extern uint sg_alloc_view_internal();
public static sg_view sg_alloc_view()
{
    uint _id = sg_alloc_view_internal();
    return new sg_view { id = _id };
}
#else
public static extern sg_view sg_alloc_view();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_dealloc_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_dealloc_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_dealloc_buffer(sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_dealloc_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_dealloc_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_dealloc_image(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_dealloc_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_dealloc_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_dealloc_sampler(sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_dealloc_shader", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_dealloc_shader", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_dealloc_shader(sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_dealloc_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_dealloc_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_dealloc_pipeline(sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_dealloc_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_dealloc_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_dealloc_view(sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_init_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_init_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_init_buffer(sg_buffer buf, in sg_buffer_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_init_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_init_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_init_image(sg_image img, in sg_image_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_init_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_init_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_init_sampler(sg_sampler smg, in sg_sampler_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_init_shader", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_init_shader", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_init_shader(sg_shader shd, in sg_shader_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_init_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_init_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_init_pipeline(sg_pipeline pip, in sg_pipeline_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_init_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_init_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_init_view(sg_view view, in sg_view_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_uninit_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_uninit_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_uninit_buffer(sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_uninit_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_uninit_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_uninit_image(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_uninit_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_uninit_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_uninit_sampler(sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_uninit_shader", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_uninit_shader", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_uninit_shader(sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_uninit_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_uninit_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_uninit_pipeline(sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_uninit_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_uninit_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_uninit_view(sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_fail_buffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_fail_buffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_fail_buffer(sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_fail_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_fail_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_fail_image(sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_fail_sampler", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_fail_sampler", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_fail_sampler(sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_fail_shader", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_fail_shader", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_fail_shader(sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_fail_pipeline", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_fail_pipeline", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_fail_pipeline(sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_fail_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_fail_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_fail_view(sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_enable_frame_stats", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_enable_frame_stats", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_enable_frame_stats();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_disable_frame_stats", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_disable_frame_stats", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_disable_frame_stats();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_frame_stats_enabled", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_frame_stats_enabled", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sg_frame_stats_enabled();

#if WEB
public static sg_frame_stats sg_query_frame_stats()
{
    sg_frame_stats result = default;
    sg_query_frame_stats_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_frame_stats", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_frame_stats", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_frame_stats sg_query_frame_stats();
#endif

[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_buffer_info
{
    public void* buf;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_image_info
{
    public void* tex2d;
    public void* tex3d;
    public void* res;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_sampler_info
{
    public void* smp;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_shader_info
{
    #pragma warning disable 169
    public struct cbufsCollection
    {
        public ref IntPtr this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private IntPtr _item0;
        private IntPtr _item1;
        private IntPtr _item2;
        private IntPtr _item3;
        private IntPtr _item4;
        private IntPtr _item5;
        private IntPtr _item6;
        private IntPtr _item7;
    }
    #pragma warning restore 169
    public cbufsCollection cbufs;
    public void* vs;
    public void* fs;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_pipeline_info
{
    public void* il;
    public void* rs;
    public void* dss;
    public void* bs;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_d3d11_view_info
{
    public void* srv;
    public void* uav;
    public void* rtv;
    public void* dsv;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_mtl_buffer_info
{
    #pragma warning disable 169
    public struct bufCollection
    {
        public ref IntPtr this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private IntPtr _item0;
        private IntPtr _item1;
    }
    #pragma warning restore 169
    public bufCollection buf;
    public int active_slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_mtl_image_info
{
    #pragma warning disable 169
    public struct texCollection
    {
        public ref IntPtr this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private IntPtr _item0;
        private IntPtr _item1;
    }
    #pragma warning restore 169
    public texCollection tex;
    public int active_slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_mtl_sampler_info
{
    public void* smp;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_mtl_shader_info
{
    public void* vertex_lib;
    public void* fragment_lib;
    public void* vertex_func;
    public void* fragment_func;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_mtl_pipeline_info
{
    public void* rps;
    public void* dss;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_buffer_info
{
    public void* buf;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_image_info
{
    public void* tex;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_sampler_info
{
    public void* smp;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_shader_info
{
    public void* vs_mod;
    public void* fs_mod;
    public void* bgl;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_pipeline_info
{
    public void* render_pipeline;
    public void* compute_pipeline;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_wgpu_view_info
{
    public void* view;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_gl_buffer_info
{
    #pragma warning disable 169
    public struct bufCollection
    {
        public ref uint this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private uint _item0;
        private uint _item1;
    }
    #pragma warning restore 169
    public bufCollection buf;
    public int active_slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_gl_image_info
{
    #pragma warning disable 169
    public struct texCollection
    {
        public ref uint this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private uint _item0;
        private uint _item1;
    }
    #pragma warning restore 169
    public texCollection tex;
    public uint tex_target;
    public int active_slot;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_gl_sampler_info
{
    public uint smp;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_gl_shader_info
{
    public uint prog;
}
[StructLayout(LayoutKind.Sequential)]
public struct sg_gl_view_info
{
    #pragma warning disable 169
    public struct tex_viewCollection
    {
        public ref uint this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 2)[index];
        private uint _item0;
        private uint _item1;
    }
    #pragma warning restore 169
    public tex_viewCollection tex_view;
    public uint msaa_render_buffer;
    public uint msaa_resolve_frame_buffer;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_device", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_device", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_d3d11_device();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_device_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_device_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_d3d11_device_context();

#if WEB
public static sg_d3d11_buffer_info sg_d3d11_query_buffer_info(sg_buffer buf)
{
    sg_d3d11_buffer_info result = default;
    sg_d3d11_query_buffer_info_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_d3d11_buffer_info sg_d3d11_query_buffer_info(sg_buffer buf);
#endif

#if WEB
public static sg_d3d11_image_info sg_d3d11_query_image_info(sg_image img)
{
    sg_d3d11_image_info result = default;
    sg_d3d11_query_image_info_internal(ref result, img);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_d3d11_image_info sg_d3d11_query_image_info(sg_image img);
#endif

#if WEB
public static sg_d3d11_sampler_info sg_d3d11_query_sampler_info(sg_sampler smp)
{
    sg_d3d11_sampler_info result = default;
    sg_d3d11_query_sampler_info_internal(ref result, smp);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_d3d11_sampler_info sg_d3d11_query_sampler_info(sg_sampler smp);
#endif

#if WEB
public static sg_d3d11_shader_info sg_d3d11_query_shader_info(sg_shader shd)
{
    sg_d3d11_shader_info result = default;
    sg_d3d11_query_shader_info_internal(ref result, shd);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_d3d11_shader_info sg_d3d11_query_shader_info(sg_shader shd);
#endif

#if WEB
public static sg_d3d11_pipeline_info sg_d3d11_query_pipeline_info(sg_pipeline pip)
{
    sg_d3d11_pipeline_info result = default;
    sg_d3d11_query_pipeline_info_internal(ref result, pip);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_d3d11_pipeline_info sg_d3d11_query_pipeline_info(sg_pipeline pip);
#endif

#if WEB
public static sg_d3d11_view_info sg_d3d11_query_view_info(sg_view view)
{
    sg_d3d11_view_info result = default;
    sg_d3d11_query_view_info_internal(ref result, view);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_d3d11_view_info sg_d3d11_query_view_info(sg_view view);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_device", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_device", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_mtl_device();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_render_command_encoder", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_render_command_encoder", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_mtl_render_command_encoder();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_compute_command_encoder", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_compute_command_encoder", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_mtl_compute_command_encoder();

#if WEB
public static sg_mtl_buffer_info sg_mtl_query_buffer_info(sg_buffer buf)
{
    sg_mtl_buffer_info result = default;
    sg_mtl_query_buffer_info_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_mtl_buffer_info sg_mtl_query_buffer_info(sg_buffer buf);
#endif

#if WEB
public static sg_mtl_image_info sg_mtl_query_image_info(sg_image img)
{
    sg_mtl_image_info result = default;
    sg_mtl_query_image_info_internal(ref result, img);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_mtl_image_info sg_mtl_query_image_info(sg_image img);
#endif

#if WEB
public static sg_mtl_sampler_info sg_mtl_query_sampler_info(sg_sampler smp)
{
    sg_mtl_sampler_info result = default;
    sg_mtl_query_sampler_info_internal(ref result, smp);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_mtl_sampler_info sg_mtl_query_sampler_info(sg_sampler smp);
#endif

#if WEB
public static sg_mtl_shader_info sg_mtl_query_shader_info(sg_shader shd)
{
    sg_mtl_shader_info result = default;
    sg_mtl_query_shader_info_internal(ref result, shd);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_mtl_shader_info sg_mtl_query_shader_info(sg_shader shd);
#endif

#if WEB
public static sg_mtl_pipeline_info sg_mtl_query_pipeline_info(sg_pipeline pip)
{
    sg_mtl_pipeline_info result = default;
    sg_mtl_query_pipeline_info_internal(ref result, pip);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_mtl_pipeline_info sg_mtl_query_pipeline_info(sg_pipeline pip);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_device", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_device", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_wgpu_device();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_queue", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_queue", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_wgpu_queue();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_command_encoder", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_command_encoder", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_wgpu_command_encoder();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_render_pass_encoder", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_render_pass_encoder", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_wgpu_render_pass_encoder();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_compute_pass_encoder", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_compute_pass_encoder", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sg_wgpu_compute_pass_encoder();

#if WEB
public static sg_wgpu_buffer_info sg_wgpu_query_buffer_info(sg_buffer buf)
{
    sg_wgpu_buffer_info result = default;
    sg_wgpu_query_buffer_info_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_wgpu_buffer_info sg_wgpu_query_buffer_info(sg_buffer buf);
#endif

#if WEB
public static sg_wgpu_image_info sg_wgpu_query_image_info(sg_image img)
{
    sg_wgpu_image_info result = default;
    sg_wgpu_query_image_info_internal(ref result, img);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_wgpu_image_info sg_wgpu_query_image_info(sg_image img);
#endif

#if WEB
public static sg_wgpu_sampler_info sg_wgpu_query_sampler_info(sg_sampler smp)
{
    sg_wgpu_sampler_info result = default;
    sg_wgpu_query_sampler_info_internal(ref result, smp);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_wgpu_sampler_info sg_wgpu_query_sampler_info(sg_sampler smp);
#endif

#if WEB
public static sg_wgpu_shader_info sg_wgpu_query_shader_info(sg_shader shd)
{
    sg_wgpu_shader_info result = default;
    sg_wgpu_query_shader_info_internal(ref result, shd);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_wgpu_shader_info sg_wgpu_query_shader_info(sg_shader shd);
#endif

#if WEB
public static sg_wgpu_pipeline_info sg_wgpu_query_pipeline_info(sg_pipeline pip)
{
    sg_wgpu_pipeline_info result = default;
    sg_wgpu_query_pipeline_info_internal(ref result, pip);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_pipeline_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_wgpu_pipeline_info sg_wgpu_query_pipeline_info(sg_pipeline pip);
#endif

#if WEB
public static sg_wgpu_view_info sg_wgpu_query_view_info(sg_view view)
{
    sg_wgpu_view_info result = default;
    sg_wgpu_query_view_info_internal(ref result, view);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_wgpu_view_info sg_wgpu_query_view_info(sg_view view);
#endif

#if WEB
public static sg_gl_buffer_info sg_gl_query_buffer_info(sg_buffer buf)
{
    sg_gl_buffer_info result = default;
    sg_gl_query_buffer_info_internal(ref result, buf);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_buffer_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_gl_buffer_info sg_gl_query_buffer_info(sg_buffer buf);
#endif

#if WEB
public static sg_gl_image_info sg_gl_query_image_info(sg_image img)
{
    sg_gl_image_info result = default;
    sg_gl_query_image_info_internal(ref result, img);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_image_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_gl_image_info sg_gl_query_image_info(sg_image img);
#endif

#if WEB
public static sg_gl_sampler_info sg_gl_query_sampler_info(sg_sampler smp)
{
    sg_gl_sampler_info result = default;
    sg_gl_query_sampler_info_internal(ref result, smp);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_sampler_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_gl_sampler_info sg_gl_query_sampler_info(sg_sampler smp);
#endif

#if WEB
public static sg_gl_shader_info sg_gl_query_shader_info(sg_shader shd)
{
    sg_gl_shader_info result = default;
    sg_gl_query_shader_info_internal(ref result, shd);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_shader_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_gl_shader_info sg_gl_query_shader_info(sg_shader shd);
#endif

#if WEB
public static sg_gl_view_info sg_gl_query_view_info(sg_view view)
{
    sg_gl_view_info result = default;
    sg_gl_query_view_info_internal(ref result, view);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_view_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_gl_view_info sg_gl_query_view_info(sg_view view);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_desc_internal(ref sg_desc result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_features_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_features_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_features_internal(ref sg_features result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_limits_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_limits_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_limits_internal(ref sg_limits result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pixelformat_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pixelformat_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_pixelformat_internal(ref sg_pixelformat_info result, sg_pixel_format fmt);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_buffer_info_internal(ref sg_buffer_info result, sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_image_info_internal(ref sg_image_info result, sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_sampler_info_internal(ref sg_sampler_info result, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_shader_info_internal(ref sg_shader_info result, sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_pipeline_info_internal(ref sg_pipeline_info result, sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_view_info_internal(ref sg_view_info result, sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_buffer_desc_internal(ref sg_buffer_desc result, sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_image_desc_internal(ref sg_image_desc result, sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_sampler_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_sampler_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_sampler_desc_internal(ref sg_sampler_desc result, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_shader_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_shader_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_shader_desc_internal(ref sg_shader_desc result, sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pipeline_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pipeline_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_pipeline_desc_internal(ref sg_pipeline_desc result, sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_view_desc_internal(ref sg_view_desc result, sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_buffer_defaults_internal(ref sg_buffer_desc result, in sg_buffer_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_image_defaults_internal(ref sg_image_desc result, in sg_image_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_sampler_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_sampler_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_sampler_defaults_internal(ref sg_sampler_desc result, in sg_sampler_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_shader_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_shader_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_shader_defaults_internal(ref sg_shader_desc result, in sg_shader_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_pipeline_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_pipeline_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_pipeline_defaults_internal(ref sg_pipeline_desc result, in sg_pipeline_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_defaults_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_view_defaults_internal(ref sg_view_desc result, in sg_view_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_buffer_usage_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_buffer_usage_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_buffer_usage_internal(ref sg_buffer_usage result, sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_image_usage_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_image_usage_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_image_usage_internal(ref sg_image_usage result, sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_image_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_image_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_view_image_internal(ref sg_image result, sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_view_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_view_buffer_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_view_buffer_internal(ref sg_buffer result, sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_query_frame_stats_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_query_frame_stats_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_query_frame_stats_internal(ref sg_frame_stats result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_d3d11_query_buffer_info_internal(ref sg_d3d11_buffer_info result, sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_d3d11_query_image_info_internal(ref sg_d3d11_image_info result, sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_d3d11_query_sampler_info_internal(ref sg_d3d11_sampler_info result, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_d3d11_query_shader_info_internal(ref sg_d3d11_shader_info result, sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_d3d11_query_pipeline_info_internal(ref sg_d3d11_pipeline_info result, sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_d3d11_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_d3d11_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_d3d11_query_view_info_internal(ref sg_d3d11_view_info result, sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_mtl_query_buffer_info_internal(ref sg_mtl_buffer_info result, sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_mtl_query_image_info_internal(ref sg_mtl_image_info result, sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_mtl_query_sampler_info_internal(ref sg_mtl_sampler_info result, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_mtl_query_shader_info_internal(ref sg_mtl_shader_info result, sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_mtl_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_mtl_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_mtl_query_pipeline_info_internal(ref sg_mtl_pipeline_info result, sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_wgpu_query_buffer_info_internal(ref sg_wgpu_buffer_info result, sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_wgpu_query_image_info_internal(ref sg_wgpu_image_info result, sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_wgpu_query_sampler_info_internal(ref sg_wgpu_sampler_info result, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_wgpu_query_shader_info_internal(ref sg_wgpu_shader_info result, sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_pipeline_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_wgpu_query_pipeline_info_internal(ref sg_wgpu_pipeline_info result, sg_pipeline pip);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_wgpu_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_wgpu_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_wgpu_query_view_info_internal(ref sg_wgpu_view_info result, sg_view view);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_buffer_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_gl_query_buffer_info_internal(ref sg_gl_buffer_info result, sg_buffer buf);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_gl_query_image_info_internal(ref sg_gl_image_info result, sg_image img);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_sampler_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_gl_query_sampler_info_internal(ref sg_gl_sampler_info result, sg_sampler smp);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_shader_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_gl_query_shader_info_internal(ref sg_gl_shader_info result, sg_shader shd);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sg_gl_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sg_gl_query_view_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sg_gl_query_view_info_internal(ref sg_gl_view_info result, sg_view view);

}
}
