// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class SLog
{
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "slog_func", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "slog_func", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void slog_func_native([M(U.LPUTF8Str)] string tag, uint log_level, uint log_item, [M(U.LPUTF8Str)] string message, uint line_nr, [M(U.LPUTF8Str)] string filename, void* user_data);

}
}
