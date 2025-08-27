// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class STM
{
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_setup", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_setup", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void stm_setup();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_now", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_now", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong stm_now();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_diff", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_diff", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong stm_diff(ulong new_ticks, ulong old_ticks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_since", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_since", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong stm_since(ulong start_ticks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_laptime", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_laptime", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong stm_laptime(ref ulong last_time);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_round_to_common_refresh_rate", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_round_to_common_refresh_rate", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong stm_round_to_common_refresh_rate(ulong frame_ticks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_sec", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_sec", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern double stm_sec(ulong ticks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_ms", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_ms", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern double stm_ms(ulong ticks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_us", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_us", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern double stm_us(ulong ticks);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "stm_ns", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "stm_ns", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern double stm_ns(ulong ticks);

}
}
