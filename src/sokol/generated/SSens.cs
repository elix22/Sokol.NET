// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SApp;

namespace Sokol
{
public static unsafe partial class SSens
{
#if WEB
[DllImport("sokol", EntryPoint = "ssens_start", CallingConvention = CallingConvention.Cdecl)]
private static extern int ssens_start_native();
public static bool ssens_start() => ssens_start_native() != 0;
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "ssens_start", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "ssens_start", CallingConvention = CallingConvention.Cdecl)]
#endif
[return: M(U.I1)]
public static extern bool ssens_start();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "ssens_stop", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "ssens_stop", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void ssens_stop();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "ssens_gravity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "ssens_gravity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int ssens_gravity(ref float out_xyz);

}
}
