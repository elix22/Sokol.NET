using System;
using System.Runtime.InteropServices;
using System.Numerics;

namespace Imgui
{
    /// <summary>
    /// Helper class providing simplified ImGui callbacks for cross-platform compatibility (Desktop + WebAssembly).
    /// Use these methods with [UnmanagedCallersOnly] callbacks without needing explicit IntPtr casts.
    /// </summary>
    public static unsafe class ImGuiHelpers
    {
#if __IOS__
		public const string NativeLibraryName = "@rpath/sokol.framework/sokol";
#else
		public const string NativeLibraryName = "sokol";
#endif

        // Internal P/Invoke declarations that accept IntPtr for callbacks
        [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputText")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igInputText_Internal(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
            [MarshalAs(UnmanagedType.I1)] ref byte buf,
            uint buf_size,
            ImGuiInputTextFlags flags,
            IntPtr callback,
            void* user_data);

        [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputTextMultiline")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igInputTextMultiline_Internal(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
            [MarshalAs(UnmanagedType.I1)] ref byte buf,
            uint buf_size,
            Vector2 size,
            ImGuiInputTextFlags flags,
            IntPtr callback,
            void* user_data);

        [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputTextWithHint")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool igInputTextWithHint_Internal(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string label,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string hint,
            [MarshalAs(UnmanagedType.I1)] ref byte buf,
            uint buf_size,
            ImGuiInputTextFlags flags,
            IntPtr callback,
            void* user_data);

        /// <summary>
        /// Simplified igInputText that accepts function pointers directly (like &amp;MyCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// </summary>
        public static bool InputText(
            string label,
            ref byte buf,
            uint buf_size,
            ImGuiInputTextFlags flags,
            delegate* unmanaged<ImGuiInputTextCallbackData*, int> callback,
            void* user_data = null)
        {
            return igInputText_Internal(label, ref buf, buf_size, flags, (IntPtr)callback, user_data);
        }

        /// <summary>
        /// Simplified igInputTextMultiline that accepts function pointers directly (like &amp;MyCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// </summary>
        public static bool InputTextMultiline(
            string label,
            ref byte buf,
            uint buf_size,
            Vector2 size,
            ImGuiInputTextFlags flags,
            delegate* unmanaged<ImGuiInputTextCallbackData*, int> callback,
            void* user_data = null)
        {
            return igInputTextMultiline_Internal(label, ref buf, buf_size, size, flags, (IntPtr)callback, user_data);
        }

        /// <summary>
        /// Simplified igInputTextWithHint that accepts function pointers directly (like &amp;MyCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// </summary>
        public static bool InputTextWithHint(
            string label,
            string hint,
            ref byte buf,
            uint buf_size,
            ImGuiInputTextFlags flags,
            delegate* unmanaged<ImGuiInputTextCallbackData*, int> callback,
            void* user_data = null)
        {
            return igInputTextWithHint_Internal(label, hint, ref buf, buf_size, flags, (IntPtr)callback, user_data);
        }
    }
}
