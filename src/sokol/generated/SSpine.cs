// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SSpine
{
public const int SSPINE_INVALID_ID = 0;
public const int SSPINE_MAX_SKINSET_SKINS = 32;
public const int SSPINE_MAX_STRING_SIZE = 61;
[StructLayout(LayoutKind.Sequential)]
public struct sspine_context
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_atlas
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_skeleton
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_instance
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_skinset
{
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_image
{
    public uint atlas_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_atlas_page
{
    public uint atlas_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_anim
{
    public uint skeleton_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_bone
{
    public uint skeleton_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_slot
{
    public uint skeleton_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_event
{
    public uint skeleton_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_iktarget
{
    public uint skeleton_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_skin
{
    public uint skeleton_id;
    public int index;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_range
{
    public void* ptr;
    public nuint size;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_vec2
{
    public float x;
    public float y;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_mat4
{
    #pragma warning disable 169
    public struct mCollection
    {
        public ref float this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 16)[index];
        private float _item0;
        private float _item1;
        private float _item2;
        private float _item3;
        private float _item4;
        private float _item5;
        private float _item6;
        private float _item7;
        private float _item8;
        private float _item9;
        private float _item10;
        private float _item11;
        private float _item12;
        private float _item13;
        private float _item14;
        private float _item15;
    }
    #pragma warning restore 169
    public mCollection m;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_string
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
#if WEB
    private byte _truncated;
    public bool truncated { get => _truncated != 0; set => _truncated = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool truncated;
#endif
    public byte len;
    #pragma warning disable 169
    public struct cstrCollection
    {
        public ref byte this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 61)[index];
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
    }
    #pragma warning restore 169
    public cstrCollection cstr;
}
public enum sspine_resource_state
{
    SSPINE_RESOURCESTATE_INITIAL,
    SSPINE_RESOURCESTATE_ALLOC,
    SSPINE_RESOURCESTATE_VALID,
    SSPINE_RESOURCESTATE_FAILED,
    SSPINE_RESOURCESTATE_INVALID,
    _SSPINE_RESOURCESTATE_FORCE_U32 = 2147483647,
}
public enum sspine_log_item
{
    SSPINE_LOGITEM_OK,
    SSPINE_LOGITEM_MALLOC_FAILED,
    SSPINE_LOGITEM_CONTEXT_POOL_EXHAUSTED,
    SSPINE_LOGITEM_ATLAS_POOL_EXHAUSTED,
    SSPINE_LOGITEM_SKELETON_POOL_EXHAUSTED,
    SSPINE_LOGITEM_SKINSET_POOL_EXHAUSTED,
    SSPINE_LOGITEM_INSTANCE_POOL_EXHAUSTED,
    SSPINE_LOGITEM_CANNOT_DESTROY_DEFAULT_CONTEXT,
    SSPINE_LOGITEM_ATLAS_DESC_NO_DATA,
    SSPINE_LOGITEM_SPINE_ATLAS_CREATION_FAILED,
    SSPINE_LOGITEM_SG_ALLOC_IMAGE_FAILED,
    SSPINE_LOGITEM_SG_ALLOC_VIEW_FAILED,
    SSPINE_LOGITEM_SG_ALLOC_SAMPLER_FAILED,
    SSPINE_LOGITEM_SKELETON_DESC_NO_DATA,
    SSPINE_LOGITEM_SKELETON_DESC_NO_ATLAS,
    SSPINE_LOGITEM_SKELETON_ATLAS_NOT_VALID,
    SSPINE_LOGITEM_CREATE_SKELETON_DATA_FROM_JSON_FAILED,
    SSPINE_LOGITEM_CREATE_SKELETON_DATA_FROM_BINARY_FAILED,
    SSPINE_LOGITEM_SKINSET_DESC_NO_SKELETON,
    SSPINE_LOGITEM_SKINSET_SKELETON_NOT_VALID,
    SSPINE_LOGITEM_SKINSET_INVALID_SKIN_HANDLE,
    SSPINE_LOGITEM_INSTANCE_DESC_NO_SKELETON,
    SSPINE_LOGITEM_INSTANCE_SKELETON_NOT_VALID,
    SSPINE_LOGITEM_INSTANCE_ATLAS_NOT_VALID,
    SSPINE_LOGITEM_SPINE_SKELETON_CREATION_FAILED,
    SSPINE_LOGITEM_SPINE_ANIMATIONSTATE_CREATION_FAILED,
    SSPINE_LOGITEM_SPINE_SKELETONCLIPPING_CREATION_FAILED,
    SSPINE_LOGITEM_COMMAND_BUFFER_FULL,
    SSPINE_LOGITEM_VERTEX_BUFFER_FULL,
    SSPINE_LOGITEM_INDEX_BUFFER_FULL,
    SSPINE_LOGITEM_STRING_TRUNCATED,
    SSPINE_LOGITEM_ADD_COMMIT_LISTENER_FAILED,
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_layer_transform
{
    public sspine_vec2 size;
    public sspine_vec2 origin;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_bone_transform
{
    public sspine_vec2 position;
    public float rotation;
    public sspine_vec2 scale;
    public sspine_vec2 shear;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_context_desc
{
    public int max_vertices;
    public int max_commands;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
    public sg_color_mask color_write_mask;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_context_info
{
    public int num_vertices;
    public int num_indices;
    public int num_commands;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_image_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public sg_image sgimage;
    public sg_view sgview;
    public sg_sampler sgsampler;
    public sg_filter min_filter;
    public sg_filter mag_filter;
    public sg_filter mipmap_filter;
    public sg_wrap wrap_u;
    public sg_wrap wrap_v;
    public int width;
    public int height;
#if WEB
    private byte _premul_alpha;
    public bool premul_alpha { get => _premul_alpha != 0; set => _premul_alpha = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool premul_alpha;
#endif
    public sspine_string filename;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_atlas_overrides
{
    public sg_filter min_filter;
    public sg_filter mag_filter;
    public sg_filter mipmap_filter;
    public sg_wrap wrap_u;
    public sg_wrap wrap_v;
#if WEB
    private byte _premul_alpha_enabled;
    public bool premul_alpha_enabled { get => _premul_alpha_enabled != 0; set => _premul_alpha_enabled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool premul_alpha_enabled;
#endif
#if WEB
    private byte _premul_alpha_disabled;
    public bool premul_alpha_disabled { get => _premul_alpha_disabled != 0; set => _premul_alpha_disabled = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool premul_alpha_disabled;
#endif
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_atlas_desc
{
    public sspine_range data;
    public sspine_atlas_overrides _override;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_atlas_page_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public sspine_atlas atlas;
    public sspine_image_info image;
    public sspine_atlas_overrides overrides;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_skeleton_desc
{
    public sspine_atlas atlas;
    public float prescale;
    public float anim_default_mix;
#if WEB
    private IntPtr _json_data;
    public string json_data { get => Marshal.PtrToStringAnsi(_json_data);  set { if (_json_data != IntPtr.Zero) { Marshal.FreeHGlobal(_json_data); _json_data = IntPtr.Zero; } if (value != null) { _json_data = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string json_data;
#endif
    public sspine_range binary_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_skinset_desc
{
    public sspine_skeleton skeleton;
    #pragma warning disable 169
    public struct skinsCollection
    {
        public ref sspine_skin this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 32)[index];
        private sspine_skin _item0;
        private sspine_skin _item1;
        private sspine_skin _item2;
        private sspine_skin _item3;
        private sspine_skin _item4;
        private sspine_skin _item5;
        private sspine_skin _item6;
        private sspine_skin _item7;
        private sspine_skin _item8;
        private sspine_skin _item9;
        private sspine_skin _item10;
        private sspine_skin _item11;
        private sspine_skin _item12;
        private sspine_skin _item13;
        private sspine_skin _item14;
        private sspine_skin _item15;
        private sspine_skin _item16;
        private sspine_skin _item17;
        private sspine_skin _item18;
        private sspine_skin _item19;
        private sspine_skin _item20;
        private sspine_skin _item21;
        private sspine_skin _item22;
        private sspine_skin _item23;
        private sspine_skin _item24;
        private sspine_skin _item25;
        private sspine_skin _item26;
        private sspine_skin _item27;
        private sspine_skin _item28;
        private sspine_skin _item29;
        private sspine_skin _item30;
        private sspine_skin _item31;
    }
    #pragma warning restore 169
    public skinsCollection skins;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_anim_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public int index;
    public float duration;
    public sspine_string name;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_bone_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public int index;
    public sspine_bone parent_bone;
    public float length;
    public sspine_bone_transform pose;
    public sg_color color;
    public sspine_string name;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_slot_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public int index;
    public sspine_bone bone;
    public sg_color color;
    public sspine_string attachment_name;
    public sspine_string name;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_iktarget_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public int index;
    public sspine_bone target_bone;
    public sspine_string name;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_skin_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public int index;
    public sspine_string name;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_event_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public int index;
    public int int_value;
    public float float_value;
    public float volume;
    public float balance;
    public sspine_string name;
    public sspine_string string_value;
    public sspine_string audio_path;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_triggered_event_info
{
#if WEB
    private byte _valid;
    public bool valid { get => _valid != 0; set => _valid = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool valid;
#endif
    public sspine_event _event;
    public float time;
    public int int_value;
    public float float_value;
    public float volume;
    public float balance;
    public sspine_string string_value;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_instance_desc
{
    public sspine_skeleton skeleton;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_allocator
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_logger
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sspine_desc
{
    public int max_vertices;
    public int max_commands;
    public int context_pool_size;
    public int atlas_pool_size;
    public int skeleton_pool_size;
    public int skinset_pool_size;
    public int instance_pool_size;
    public sg_pixel_format color_format;
    public sg_pixel_format depth_format;
    public int sample_count;
    public sg_color_mask color_write_mask;
    public sspine_allocator allocator;
    public sspine_logger logger;
}
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_setup(in sspine_desc desc);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_shutdown();

#if WEB
public static sspine_context sspine_make_context(in sspine_context_desc desc)
{
    sspine_context result = default;
    sspine_make_context_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_context sspine_make_context(in sspine_context_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_destroy_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_destroy_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_destroy_context(sspine_context ctx);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_context(sspine_context ctx);

#if WEB
public static sspine_context sspine_get_context()
{
    sspine_context result = default;
    sspine_get_context_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_context sspine_get_context();
#endif

#if WEB
public static sspine_context sspine_default_context()
{
    sspine_context result = default;
    sspine_default_context_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_default_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_default_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_context sspine_default_context();
#endif

#if WEB
public static sspine_context_info sspine_get_context_info(sspine_context ctx)
{
    sspine_context_info result = default;
    sspine_get_context_info_internal(ref result, ctx);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_context_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_context_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_context_info sspine_get_context_info(sspine_context ctx);
#endif

#if WEB
public static sspine_atlas sspine_make_atlas(in sspine_atlas_desc desc)
{
    sspine_atlas result = default;
    sspine_make_atlas_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_atlas", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_atlas", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_atlas sspine_make_atlas(in sspine_atlas_desc desc);
#endif

#if WEB
public static sspine_skeleton sspine_make_skeleton(in sspine_skeleton_desc desc)
{
    sspine_skeleton result = default;
    sspine_make_skeleton_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_skeleton", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_skeleton", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_skeleton sspine_make_skeleton(in sspine_skeleton_desc desc);
#endif

#if WEB
public static sspine_skinset sspine_make_skinset(in sspine_skinset_desc desc)
{
    sspine_skinset result = default;
    sspine_make_skinset_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_skinset", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_skinset", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_skinset sspine_make_skinset(in sspine_skinset_desc desc);
#endif

#if WEB
public static sspine_instance sspine_make_instance(in sspine_instance_desc desc)
{
    sspine_instance result = default;
    sspine_make_instance_internal(ref result, desc);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_instance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_instance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_instance sspine_make_instance(in sspine_instance_desc desc);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_destroy_atlas", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_destroy_atlas", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_destroy_atlas(sspine_atlas atlas);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_destroy_skeleton", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_destroy_skeleton", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_destroy_skeleton(sspine_skeleton skeleton);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_destroy_skinset", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_destroy_skinset", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_destroy_skinset(sspine_skinset skinset);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_destroy_instance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_destroy_instance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_destroy_instance(sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_skinset", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_skinset", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_skinset(sspine_instance instance, sspine_skinset skinset);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_update_instance", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_update_instance", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_update_instance(sspine_instance instance, float delta_time);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_triggered_events", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_triggered_events", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_triggered_events(sspine_instance instance);

#if WEB
public static sspine_triggered_event_info sspine_get_triggered_event_info(sspine_instance instance, int triggered_event_index)
{
    sspine_triggered_event_info result = default;
    sspine_get_triggered_event_info_internal(ref result, instance, triggered_event_index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_triggered_event_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_triggered_event_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_triggered_event_info sspine_get_triggered_event_info(sspine_instance instance, int triggered_event_index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_draw_instance_in_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_draw_instance_in_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_draw_instance_in_layer(sspine_instance instance, int layer);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_context_draw_instance_in_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_context_draw_instance_in_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_context_draw_instance_in_layer(sspine_context ctx, sspine_instance instance, int layer);

#if WEB
public static sspine_mat4 sspine_layer_transform_to_mat4(in sspine_layer_transform tform)
{
    sspine_mat4 result = default;
    sspine_layer_transform_to_mat4_internal(ref result, tform);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_layer_transform_to_mat4", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_layer_transform_to_mat4", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_mat4 sspine_layer_transform_to_mat4(in sspine_layer_transform tform);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_draw_layer(int layer, in sspine_layer_transform tform);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_context_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_context_draw_layer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_context_draw_layer(sspine_context ctx, int layer, in sspine_layer_transform tform);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_context_resource_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_context_resource_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_resource_state sspine_get_context_resource_state(sspine_context context);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_atlas_resource_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_atlas_resource_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_resource_state sspine_get_atlas_resource_state(sspine_atlas atlas);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_skeleton_resource_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_skeleton_resource_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_resource_state sspine_get_skeleton_resource_state(sspine_skeleton skeleton);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_skinset_resource_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_skinset_resource_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_resource_state sspine_get_skinset_resource_state(sspine_skinset skinset);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_instance_resource_state", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_instance_resource_state", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_resource_state sspine_get_instance_resource_state(sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_context_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_context_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_context_valid(sspine_context context);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_atlas_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_atlas_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_atlas_valid(sspine_atlas atlas);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skeleton_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skeleton_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_skeleton_valid(sspine_skeleton skeleton);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_instance_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_instance_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_instance_valid(sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skinset_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skinset_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_skinset_valid(sspine_skinset skinset);

#if WEB
public static sspine_atlas sspine_get_skeleton_atlas(sspine_skeleton skeleton)
{
    sspine_atlas result = default;
    sspine_get_skeleton_atlas_internal(ref result, skeleton);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_skeleton_atlas", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_skeleton_atlas", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_atlas sspine_get_skeleton_atlas(sspine_skeleton skeleton);
#endif

#if WEB
public static sspine_skeleton sspine_get_instance_skeleton(sspine_instance instance)
{
    sspine_skeleton result = default;
    sspine_get_instance_skeleton_internal(ref result, instance);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_instance_skeleton", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_instance_skeleton", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_skeleton sspine_get_instance_skeleton(sspine_instance instance);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_images", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_images", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_images(sspine_atlas atlas);

#if WEB
public static sspine_image sspine_image_by_index(sspine_atlas atlas, int index)
{
    sspine_image result = default;
    sspine_image_by_index_internal(ref result, atlas, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_image_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_image_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_image sspine_image_by_index(sspine_atlas atlas, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_image_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_image_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_image_valid(sspine_image image);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_image_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_image_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_image_equal(sspine_image first, sspine_image second);

#if WEB
public static sspine_image_info sspine_get_image_info(sspine_image image)
{
    sspine_image_info result = default;
    sspine_get_image_info_internal(ref result, image);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_image_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_image_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_image_info sspine_get_image_info(sspine_image image);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_atlas_pages", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_atlas_pages", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_atlas_pages(sspine_atlas atlas);

#if WEB
public static sspine_atlas_page sspine_atlas_page_by_index(sspine_atlas atlas, int index)
{
    sspine_atlas_page result = default;
    sspine_atlas_page_by_index_internal(ref result, atlas, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_atlas_page_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_atlas_page_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_atlas_page sspine_atlas_page_by_index(sspine_atlas atlas, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_atlas_page_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_atlas_page_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_atlas_page_valid(sspine_atlas_page page);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_atlas_page_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_atlas_page_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_atlas_page_equal(sspine_atlas_page first, sspine_atlas_page second);

#if WEB
public static sspine_atlas_page_info sspine_get_atlas_page_info(sspine_atlas_page page)
{
    sspine_atlas_page_info result = default;
    sspine_get_atlas_page_info_internal(ref result, page);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_atlas_page_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_atlas_page_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_atlas_page_info sspine_get_atlas_page_info(sspine_atlas_page page);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_position", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_position", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_position(sspine_instance instance, sspine_vec2 position);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_scale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_scale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_scale(sspine_instance instance, sspine_vec2 scale);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_color", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_color", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_color(sspine_instance instance, sg_color color);

#if WEB
public static sspine_vec2 sspine_get_position(sspine_instance instance)
{
    sspine_vec2 result = default;
    sspine_get_position_internal(ref result, instance);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_position", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_position", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_get_position(sspine_instance instance);
#endif

#if WEB
public static sspine_vec2 sspine_get_scale(sspine_instance instance)
{
    sspine_vec2 result = default;
    sspine_get_scale_internal(ref result, instance);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_scale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_scale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_get_scale(sspine_instance instance);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_color", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_color", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_color sspine_get_color(sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_anims", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_anims", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_anims(sspine_skeleton skeleton);

#if WEB
public static sspine_anim sspine_anim_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name)
{
    sspine_anim result = default;
    sspine_anim_by_name_internal(ref result, skeleton, name);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_anim_by_name", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_anim_by_name", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_anim sspine_anim_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);
#endif

#if WEB
public static sspine_anim sspine_anim_by_index(sspine_skeleton skeleton, int index)
{
    sspine_anim result = default;
    sspine_anim_by_index_internal(ref result, skeleton, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_anim_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_anim_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_anim sspine_anim_by_index(sspine_skeleton skeleton, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_anim_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_anim_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_anim_valid(sspine_anim anim);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_anim_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_anim_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_anim_equal(sspine_anim first, sspine_anim second);

#if WEB
public static sspine_anim_info sspine_get_anim_info(sspine_anim anim)
{
    sspine_anim_info result = default;
    sspine_get_anim_info_internal(ref result, anim);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_anim_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_anim_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_anim_info sspine_get_anim_info(sspine_anim anim);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_clear_animation_tracks", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_clear_animation_tracks", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_clear_animation_tracks(sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_clear_animation_track", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_clear_animation_track", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_clear_animation_track(sspine_instance instance, int track_index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_animation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_animation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_animation(sspine_instance instance, sspine_anim anim, int track_index, bool loop);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_add_animation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_add_animation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_add_animation(sspine_instance instance, sspine_anim anim, int track_index, bool loop, float delay);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_empty_animation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_empty_animation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_empty_animation(sspine_instance instance, int track_index, float mix_duration);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_add_empty_animation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_add_empty_animation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_add_empty_animation(sspine_instance instance, int track_index, float mix_duration, float delay);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_bones", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_bones", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_bones(sspine_skeleton skeleton);

#if WEB
public static sspine_bone sspine_bone_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name)
{
    sspine_bone result = default;
    sspine_bone_by_name_internal(ref result, skeleton, name);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_by_name", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_by_name", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_bone sspine_bone_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);
#endif

#if WEB
public static sspine_bone sspine_bone_by_index(sspine_skeleton skeleton, int index)
{
    sspine_bone result = default;
    sspine_bone_by_index_internal(ref result, skeleton, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_bone sspine_bone_by_index(sspine_skeleton skeleton, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_bone_valid(sspine_bone bone);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_bone_equal(sspine_bone first, sspine_bone second);

#if WEB
public static sspine_bone_info sspine_get_bone_info(sspine_bone bone)
{
    sspine_bone_info result = default;
    sspine_get_bone_info_internal(ref result, bone);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_bone_info sspine_get_bone_info(sspine_bone bone);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_bone_transform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_bone_transform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_bone_transform(sspine_instance instance, sspine_bone bone, in sspine_bone_transform transform);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_bone_position", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_bone_position", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_bone_position(sspine_instance instance, sspine_bone bone, sspine_vec2 position);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_bone_rotation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_bone_rotation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_bone_rotation(sspine_instance instance, sspine_bone bone, float rotation);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_bone_scale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_bone_scale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_bone_scale(sspine_instance instance, sspine_bone bone, sspine_vec2 scale);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_bone_shear", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_bone_shear", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_bone_shear(sspine_instance instance, sspine_bone bone, sspine_vec2 shear);

#if WEB
public static sspine_bone_transform sspine_get_bone_transform(sspine_instance instance, sspine_bone bone)
{
    sspine_bone_transform result = default;
    sspine_get_bone_transform_internal(ref result, instance, bone);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_transform", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_transform", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_bone_transform sspine_get_bone_transform(sspine_instance instance, sspine_bone bone);
#endif

#if WEB
public static sspine_vec2 sspine_get_bone_position(sspine_instance instance, sspine_bone bone)
{
    sspine_vec2 result = default;
    sspine_get_bone_position_internal(ref result, instance, bone);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_position", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_position", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_get_bone_position(sspine_instance instance, sspine_bone bone);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_rotation", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_rotation", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float sspine_get_bone_rotation(sspine_instance instance, sspine_bone bone);

#if WEB
public static sspine_vec2 sspine_get_bone_scale(sspine_instance instance, sspine_bone bone)
{
    sspine_vec2 result = default;
    sspine_get_bone_scale_internal(ref result, instance, bone);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_scale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_scale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_get_bone_scale(sspine_instance instance, sspine_bone bone);
#endif

#if WEB
public static sspine_vec2 sspine_get_bone_shear(sspine_instance instance, sspine_bone bone)
{
    sspine_vec2 result = default;
    sspine_get_bone_shear_internal(ref result, instance, bone);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_shear", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_shear", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_get_bone_shear(sspine_instance instance, sspine_bone bone);
#endif

#if WEB
public static sspine_vec2 sspine_get_bone_world_position(sspine_instance instance, sspine_bone bone)
{
    sspine_vec2 result = default;
    sspine_get_bone_world_position_internal(ref result, instance, bone);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_world_position", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_world_position", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_get_bone_world_position(sspine_instance instance, sspine_bone bone);
#endif

#if WEB
public static sspine_vec2 sspine_bone_local_to_world(sspine_instance instance, sspine_bone bone, sspine_vec2 local_pos)
{
    sspine_vec2 result = default;
    sspine_bone_local_to_world_internal(ref result, instance, bone, local_pos);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_local_to_world", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_local_to_world", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_bone_local_to_world(sspine_instance instance, sspine_bone bone, sspine_vec2 local_pos);
#endif

#if WEB
public static sspine_vec2 sspine_bone_world_to_local(sspine_instance instance, sspine_bone bone, sspine_vec2 world_pos)
{
    sspine_vec2 result = default;
    sspine_bone_world_to_local_internal(ref result, instance, bone, world_pos);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_world_to_local", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_world_to_local", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_vec2 sspine_bone_world_to_local(sspine_instance instance, sspine_bone bone, sspine_vec2 world_pos);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_slots", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_slots", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_slots(sspine_skeleton skeleton);

#if WEB
public static sspine_slot sspine_slot_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name)
{
    sspine_slot result = default;
    sspine_slot_by_name_internal(ref result, skeleton, name);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_slot_by_name", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_slot_by_name", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_slot sspine_slot_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);
#endif

#if WEB
public static sspine_slot sspine_slot_by_index(sspine_skeleton skeleton, int index)
{
    sspine_slot result = default;
    sspine_slot_by_index_internal(ref result, skeleton, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_slot_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_slot_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_slot sspine_slot_by_index(sspine_skeleton skeleton, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_slot_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_slot_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_slot_valid(sspine_slot slot);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_slot_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_slot_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_slot_equal(sspine_slot first, sspine_slot second);

#if WEB
public static sspine_slot_info sspine_get_slot_info(sspine_slot slot)
{
    sspine_slot_info result = default;
    sspine_get_slot_info_internal(ref result, slot);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_slot_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_slot_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_slot_info sspine_get_slot_info(sspine_slot slot);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_slot_color", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_slot_color", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_slot_color(sspine_instance instance, sspine_slot slot, sg_color color);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_slot_color", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_slot_color", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_color sspine_get_slot_color(sspine_instance instance, sspine_slot slot);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_events", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_events", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_events(sspine_skeleton skeleton);

#if WEB
public static sspine_event sspine_event_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name)
{
    sspine_event result = default;
    sspine_event_by_name_internal(ref result, skeleton, name);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_event_by_name", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_event_by_name", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_event sspine_event_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);
#endif

#if WEB
public static sspine_event sspine_event_by_index(sspine_skeleton skeleton, int index)
{
    sspine_event result = default;
    sspine_event_by_index_internal(ref result, skeleton, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_event_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_event_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_event sspine_event_by_index(sspine_skeleton skeleton, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_event_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_event_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_event_valid(sspine_event _event);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_event_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_event_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_event_equal(sspine_event first, sspine_event second);

#if WEB
public static sspine_event_info sspine_get_event_info(sspine_event _event)
{
    sspine_event_info result = default;
    sspine_get_event_info_internal(ref result, _event);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_event_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_event_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_event_info sspine_get_event_info(sspine_event _event);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_iktargets", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_iktargets", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_iktargets(sspine_skeleton skeleton);

#if WEB
public static sspine_iktarget sspine_iktarget_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name)
{
    sspine_iktarget result = default;
    sspine_iktarget_by_name_internal(ref result, skeleton, name);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_iktarget_by_name", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_iktarget_by_name", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_iktarget sspine_iktarget_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);
#endif

#if WEB
public static sspine_iktarget sspine_iktarget_by_index(sspine_skeleton skeleton, int index)
{
    sspine_iktarget result = default;
    sspine_iktarget_by_index_internal(ref result, skeleton, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_iktarget_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_iktarget_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_iktarget sspine_iktarget_by_index(sspine_skeleton skeleton, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_iktarget_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_iktarget_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_iktarget_valid(sspine_iktarget iktarget);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_iktarget_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_iktarget_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_iktarget_equal(sspine_iktarget first, sspine_iktarget second);

#if WEB
public static sspine_iktarget_info sspine_get_iktarget_info(sspine_iktarget iktarget)
{
    sspine_iktarget_info result = default;
    sspine_get_iktarget_info_internal(ref result, iktarget);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_iktarget_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_iktarget_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_iktarget_info sspine_get_iktarget_info(sspine_iktarget iktarget);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_iktarget_world_pos", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_iktarget_world_pos", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_iktarget_world_pos(sspine_instance instance, sspine_iktarget iktarget, sspine_vec2 world_pos);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_num_skins", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_num_skins", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sspine_num_skins(sspine_skeleton skeleton);

#if WEB
public static sspine_skin sspine_skin_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name)
{
    sspine_skin result = default;
    sspine_skin_by_name_internal(ref result, skeleton, name);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skin_by_name", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skin_by_name", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_skin sspine_skin_by_name(sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);
#endif

#if WEB
public static sspine_skin sspine_skin_by_index(sspine_skeleton skeleton, int index)
{
    sspine_skin result = default;
    sspine_skin_by_index_internal(ref result, skeleton, index);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skin_by_index", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skin_by_index", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_skin sspine_skin_by_index(sspine_skeleton skeleton, int index);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skin_valid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skin_valid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_skin_valid(sspine_skin skin);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skin_equal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skin_equal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sspine_skin_equal(sspine_skin first, sspine_skin second);

#if WEB
public static sspine_skin_info sspine_get_skin_info(sspine_skin skin)
{
    sspine_skin_info result = default;
    sspine_get_skin_info_internal(ref result, skin);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_skin_info", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_skin_info", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sspine_skin_info sspine_get_skin_info(sspine_skin skin);
#endif

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_set_skin", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_set_skin", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_set_skin(sspine_instance instance, sspine_skin skin);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_context_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_context_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_make_context_internal(ref sspine_context result, in sspine_context_desc desc);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_context_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_context_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_context_internal(ref sspine_context result);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_default_context_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_default_context_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_default_context_internal(ref sspine_context result);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_context_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_context_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_context_info_internal(ref sspine_context_info result, sspine_context ctx);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_atlas_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_atlas_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_make_atlas_internal(ref sspine_atlas result, in sspine_atlas_desc desc);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_skeleton_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_skeleton_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_make_skeleton_internal(ref sspine_skeleton result, in sspine_skeleton_desc desc);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_skinset_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_skinset_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_make_skinset_internal(ref sspine_skinset result, in sspine_skinset_desc desc);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_make_instance_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_make_instance_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_make_instance_internal(ref sspine_instance result, in sspine_instance_desc desc);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_triggered_event_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_triggered_event_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_triggered_event_info_internal(ref sspine_triggered_event_info result, sspine_instance instance, int triggered_event_index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_layer_transform_to_mat4_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_layer_transform_to_mat4_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_layer_transform_to_mat4_internal(ref sspine_mat4 result, in sspine_layer_transform tform);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_skeleton_atlas_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_skeleton_atlas_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_skeleton_atlas_internal(ref sspine_atlas result, sspine_skeleton skeleton);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_instance_skeleton_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_instance_skeleton_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_instance_skeleton_internal(ref sspine_skeleton result, sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_image_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_image_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_image_by_index_internal(ref sspine_image result, sspine_atlas atlas, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_image_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_image_info_internal(ref sspine_image_info result, sspine_image image);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_atlas_page_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_atlas_page_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_atlas_page_by_index_internal(ref sspine_atlas_page result, sspine_atlas atlas, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_atlas_page_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_atlas_page_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_atlas_page_info_internal(ref sspine_atlas_page_info result, sspine_atlas_page page);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_position_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_position_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_position_internal(ref sspine_vec2 result, sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_scale_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_scale_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_scale_internal(ref sspine_vec2 result, sspine_instance instance);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_anim_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_anim_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_anim_by_name_internal(ref sspine_anim result, sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_anim_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_anim_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_anim_by_index_internal(ref sspine_anim result, sspine_skeleton skeleton, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_anim_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_anim_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_anim_info_internal(ref sspine_anim_info result, sspine_anim anim);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_bone_by_name_internal(ref sspine_bone result, sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_bone_by_index_internal(ref sspine_bone result, sspine_skeleton skeleton, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_bone_info_internal(ref sspine_bone_info result, sspine_bone bone);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_transform_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_transform_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_bone_transform_internal(ref sspine_bone_transform result, sspine_instance instance, sspine_bone bone);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_position_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_position_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_bone_position_internal(ref sspine_vec2 result, sspine_instance instance, sspine_bone bone);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_scale_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_scale_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_bone_scale_internal(ref sspine_vec2 result, sspine_instance instance, sspine_bone bone);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_shear_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_shear_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_bone_shear_internal(ref sspine_vec2 result, sspine_instance instance, sspine_bone bone);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_bone_world_position_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_bone_world_position_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_bone_world_position_internal(ref sspine_vec2 result, sspine_instance instance, sspine_bone bone);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_local_to_world_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_local_to_world_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_bone_local_to_world_internal(ref sspine_vec2 result, sspine_instance instance, sspine_bone bone, sspine_vec2 local_pos);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_bone_world_to_local_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_bone_world_to_local_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_bone_world_to_local_internal(ref sspine_vec2 result, sspine_instance instance, sspine_bone bone, sspine_vec2 world_pos);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_slot_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_slot_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_slot_by_name_internal(ref sspine_slot result, sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_slot_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_slot_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_slot_by_index_internal(ref sspine_slot result, sspine_skeleton skeleton, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_slot_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_slot_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_slot_info_internal(ref sspine_slot_info result, sspine_slot slot);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_event_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_event_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_event_by_name_internal(ref sspine_event result, sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_event_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_event_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_event_by_index_internal(ref sspine_event result, sspine_skeleton skeleton, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_event_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_event_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_event_info_internal(ref sspine_event_info result, sspine_event _event);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_iktarget_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_iktarget_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_iktarget_by_name_internal(ref sspine_iktarget result, sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_iktarget_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_iktarget_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_iktarget_by_index_internal(ref sspine_iktarget result, sspine_skeleton skeleton, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_iktarget_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_iktarget_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_iktarget_info_internal(ref sspine_iktarget_info result, sspine_iktarget iktarget);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skin_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skin_by_name_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_skin_by_name_internal(ref sspine_skin result, sspine_skeleton skeleton, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_skin_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_skin_by_index_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_skin_by_index_internal(ref sspine_skin result, sspine_skeleton skeleton, int index);

#if __IOS__
[DllImport("@rpath/spine-c.framework/spine-c", EntryPoint = "sspine_get_skin_info_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("spine-c", EntryPoint = "sspine_get_skin_info_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sspine_get_skin_info_internal(ref sspine_skin_info result, sspine_skin skin);

}
}
