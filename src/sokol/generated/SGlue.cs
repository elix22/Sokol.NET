// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

namespace Sokol
{
public static unsafe partial class SGlue
{
#if WEB
public static sg_environment sglue_environment()
{
    sg_environment result = default;
    sglue_environment_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sglue_environment", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sglue_environment", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_environment sglue_environment();
#endif

#if WEB
public static sg_swapchain sglue_swapchain()
{
    sg_swapchain result = default;
    sglue_swapchain_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sglue_swapchain", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sglue_swapchain", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_swapchain sglue_swapchain();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sglue_environment_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sglue_environment_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sglue_environment_internal(ref sg_environment result);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sglue_swapchain_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sglue_swapchain_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sglue_swapchain_internal(ref sg_swapchain result);

}
}
