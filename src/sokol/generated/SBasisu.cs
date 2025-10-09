// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SBasisu
{
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sbasisu_setup();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_shutdown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_shutdown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sbasisu_shutdown();

#if WEB
public static sg_image sbasisu_make_image(sg_range basisu_data)
{
    sg_image result = default;
    sbasisu_make_image_internal(ref result, basisu_data);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_make_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_make_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image sbasisu_make_image(sg_range basisu_data);
#endif

#if WEB
public static sg_image_desc sbasisu_transcode(sg_range basisu_data)
{
    sg_image_desc result = default;
    sbasisu_transcode_internal(ref result, basisu_data);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_transcode", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_transcode", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image_desc sbasisu_transcode(sg_range basisu_data);
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_free", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_free", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sbasisu_free(in sg_image_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_pixelformat", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_pixelformat", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_pixel_format sbasisu_pixelformat(bool has_alpha);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_make_image_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_make_image_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sbasisu_make_image_internal(ref sg_image result, sg_range basisu_data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_transcode_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_transcode_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sbasisu_transcode_internal(ref sg_image_desc result, sg_range basisu_data);

}
}
