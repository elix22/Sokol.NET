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

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_make_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_make_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image sbasisu_make_image(sg_range basisu_data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sbasisu_transcode", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sbasisu_transcode", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_image_desc sbasisu_transcode(sg_range basisu_data);

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

}
}
