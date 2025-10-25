// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
    public static unsafe partial class SLog
    {
        [UnmanagedCallersOnly]
        public static void slog_func(byte* tag, uint log_level, uint log_item, byte* message, uint line_nr, byte* filename, void* user_data)
        {
            SLog.slog_func_native(
                Marshal.PtrToStringAnsi((IntPtr)tag) ?? string.Empty,
                log_level,
                log_item,
                Marshal.PtrToStringAnsi((IntPtr)message) ?? string.Empty,
                line_nr,
                Marshal.PtrToStringAnsi((IntPtr)filename) ?? string.Empty,
                user_data);
        }

        // Helper function for easy logging from C# code
        // Log levels: 1=error, 2=warning, 3=info
        // Automatically captures caller file path and line number
        // Only active in DEBUG builds
        public static void Log(
            string message, 
            string tag = "Sokol App", 
            uint log_level = 3,
            [CallerFilePath] string filename = "",
            [CallerLineNumber] uint line_nr = 0)
        {
#if DEBUG
            if (log_level <= 0 || log_level > 3)
            {
                log_level = 3; // default to info
            }

            // Extract just the filename (find last / or \)
            int lastSep = filename.LastIndexOf('/');
            if (lastSep < 0) lastSep = filename.LastIndexOf('\\');
            string shortName = lastSep >= 0 ? filename.Substring(lastSep + 1) : filename;

            slog_func_native(tag, log_level, 0, message, line_nr, shortName, null);
#endif
        }
        
        public static void Info(
            string message, 
            string tag = "Sokol App",
            [CallerFilePath] string filename = "",
            [CallerLineNumber] uint line_nr = 0)
        {
#if DEBUG
            // Extract just the filename (find last / or \)
            int lastSep = filename.LastIndexOf('/');
            if (lastSep < 0) lastSep = filename.LastIndexOf('\\');
            string shortName = lastSep >= 0 ? filename.Substring(lastSep + 1) : filename;

            slog_func_native(tag, 3, 0, message, line_nr, shortName, null);
#endif
        }

        public static void Warning(
            string message, 
            string tag = "Sokol App",
            [CallerFilePath] string filename = "",
            [CallerLineNumber] uint line_nr = 0)
        {
#if DEBUG
            // Extract just the filename (find last / or \)
            int lastSep = filename.LastIndexOf('/');
            if (lastSep < 0) lastSep = filename.LastIndexOf('\\');
            string shortName = lastSep >= 0 ? filename.Substring(lastSep + 1) : filename;

            slog_func_native(tag, 2, 0, message, line_nr, shortName, null);
#endif
        }

        public static void Error(
            string message, 
            string tag = "Sokol App",
            [CallerFilePath] string filename = "",
            [CallerLineNumber] uint line_nr = 0)
        {
#if DEBUG
            // Extract just the filename (find last / or \)
            int lastSep = filename.LastIndexOf('/');
            if (lastSep < 0) lastSep = filename.LastIndexOf('\\');
            string shortName = lastSep >= 0 ? filename.Substring(lastSep + 1) : filename;

            slog_func_native(tag, 1, 0, message, line_nr, shortName, null);
#endif
        }

    }
}
