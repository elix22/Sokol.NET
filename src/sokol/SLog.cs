// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class SLog
{
    [UnmanagedCallersOnly]
    public static void slog_func(byte* tag, uint log_level, uint log_item, byte* message, uint line_nr, byte* filename, void* user_data)
    {
        // logger.func(tag, log_level, log_item, message, line_nr, filename, user_data);
        SLog.slog_func_native(
            Marshal.PtrToStringAnsi((IntPtr)tag) ?? string.Empty, 
            log_level, 
            log_item, 
            Marshal.PtrToStringAnsi((IntPtr)message) ?? string.Empty, 
            line_nr, 
            Marshal.PtrToStringAnsi((IntPtr)filename) ?? string.Empty, 
            user_data);
    }
}
}
