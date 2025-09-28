// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

using static Sokol.SG;

using static Sokol.SApp;



namespace Sokol
{
    public readonly struct sgimgui_t
    {
        public IntPtr Value { get; }
        public sgimgui_t(IntPtr value) => Value = value;

        public static implicit operator IntPtr(sgimgui_t t) => t.Value;
    }

    public static unsafe partial class SGImgui
    {
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sgimgui_init_csharp", CallingConvention = CallingConvention.Cdecl)]
#else
        [DllImport("sokol", EntryPoint = "sgimgui_init_csharp", CallingConvention = CallingConvention.Cdecl)]
#endif
        public static extern sgimgui_t sgimgui_init();

    }

}