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
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sglue_environment", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sglue_environment", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_environment sglue_environment();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sglue_swapchain", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sglue_swapchain", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sg_swapchain sglue_swapchain();

}
}
