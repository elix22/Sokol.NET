// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class StbImage
{
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stbi_load_csharp", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stbi_load_csharp", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* stbi_load_csharp(in byte buffer, int len, ref int x, ref int y, ref int channels_in_file, int desired_channels);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stbi_load_flipped_csharp", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stbi_load_flipped_csharp", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* stbi_load_flipped_csharp(in byte buffer, int len, ref int x, ref int y, ref int channels_in_file, int desired_channels);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stbi_loadf_csharp", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stbi_loadf_csharp", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float* stbi_loadf_csharp(in byte buffer, int len, ref int x, ref int y, ref int channels_in_file, int desired_channels);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stbi_loadf_flipped_csharp", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stbi_loadf_flipped_csharp", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float* stbi_loadf_flipped_csharp(in byte buffer, int len, ref int x, ref int y, ref int channels_in_file, int desired_channels);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stbi_image_free_csharp", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stbi_image_free_csharp", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void stbi_image_free_csharp(void* retval_from_stbi_load);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stbi_failure_reason_csharp", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stbi_failure_reason_csharp", CallingConvention = CallingConvention.Cdecl)]
#endif
private static extern IntPtr stbi_failure_reason_csharp_native();

public static string stbi_failure_reason_csharp()
{
    IntPtr ptr = stbi_failure_reason_csharp_native();
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

}
}
