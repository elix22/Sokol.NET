using System;
using System.Runtime.InteropServices;

namespace Sokol
{
    public static unsafe partial class SDebugText
    {
#if __IOS__
      const string  sokol_lib = "@rpath/sokol.framework/sokol";
#else
      const string  sokol_lib = "sokol";
#endif
        [DllImport(sokol_lib, EntryPoint = "sdtx_print_wrapper", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sdtx_print_wrapper(string str);

        public static int sdtx_print(string format, params object[] args)
        {
            // Format the string in managed code.
            string message = string.Format(format, args);
            // Pass the formatted string to the native function.
           return sdtx_print_wrapper(message);
        }
    }
}