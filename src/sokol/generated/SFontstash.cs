// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

using static Sokol.SApp;

namespace Sokol
{
public static unsafe partial class SFontstash
{
[StructLayout(LayoutKind.Sequential)]
public struct sfons_allocator_t
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sfons_desc_t
{
    public int width;
    public int height;
    public sfons_allocator_t allocator;
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfons_create", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfons_create", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern IntPtr sfons_create(in sfons_desc_t desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfons_destroy", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfons_destroy", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfons_destroy(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfons_flush", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfons_flush", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sfons_flush(IntPtr ctx);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sfons_rgba", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sfons_rgba", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint sfons_rgba(byte r, byte g, byte b, byte a);

}
}
