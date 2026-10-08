#if __ANDROID__
using System;
using System.Runtime.InteropServices;

internal static class SokolIncomingLinks
{
    const string Lib = "sokol_inlinks";

    [DllImport(Lib, EntryPoint = "sokolinlinks_take", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr Take();
}
#endif
