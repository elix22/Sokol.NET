// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class TinyEXR
{
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRLoadFromMemory", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRLoadFromMemory", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int EXRLoadFromMemory(in byte memory, int size, ref int width, ref int height,  out float * out_rgba, IntPtr err);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRLoad", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRLoad", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int EXRLoad([M(U.LPUTF8Str)] string filename, ref int width, ref int height,  out float * out_rgba, IntPtr err);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRIsFromMemory", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRIsFromMemory", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int EXRIsFromMemory(in byte memory, int size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRFreeImage", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRFreeImage", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void EXRFreeImage(ref float rgba_data);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRFreeErrorMessage", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRFreeErrorMessage", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void EXRFreeErrorMessage([M(U.LPUTF8Str)] string err);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRGetFailureReason", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRGetFailureReason", CallingConvention = CallingConvention.Cdecl)]
#endif
private static extern IntPtr EXRGetFailureReason_native();

public static string EXRGetFailureReason()
{
    IntPtr ptr = EXRGetFailureReason_native();
    if (ptr == IntPtr.Zero)
        return "";

    // Manual UTF-8 to string conversion to avoid marshalling corruption
    try
    {
        return Marshal.PtrToStringUTF8(ptr) ?? "";
    }
    catch
    {
        // Fallback in case of any marshalling issues
        return "";
    }
}

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRConvertPanoramaToDiffuseCubemap", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRConvertPanoramaToDiffuseCubemap", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* EXRConvertPanoramaToDiffuseCubemap(in float panorama_rgba, int pano_width, int pano_height, int cube_size, int sample_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRConvertPanoramaToDiffuseCubemapFace", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRConvertPanoramaToDiffuseCubemapFace", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* EXRConvertPanoramaToDiffuseCubemapFace(in float panorama_rgba, int pano_width, int pano_height, int cube_size, int face_index, int sample_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRConvertPanoramaToSpecularCubemap", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRConvertPanoramaToSpecularCubemap", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* EXRConvertPanoramaToSpecularCubemap(in float panorama_rgba, int pano_width, int pano_height, int cube_size, float roughness, int sample_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRConvertPanoramaToSpecularCubemapFace", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRConvertPanoramaToSpecularCubemapFace", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* EXRConvertPanoramaToSpecularCubemapFace(in float panorama_rgba, int pano_width, int pano_height, int cube_size, int face_index, float roughness, int sample_count);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "EXRFreeCubemapData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "EXRFreeCubemapData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void EXRFreeCubemapData(byte* cubemap_data);

}
}
