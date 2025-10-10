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

        [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetNextWindowSizeConstraints")]
        private static extern void igSetNextWindowSizeConstraints_Internal(
            Vector2 size_min,
            Vector2 size_max,
            IntPtr custom_callback,
            void* custom_callback_data);

        [DllImport(NativeLibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ImDrawList_AddCallback")]
        private static extern void ImDrawList_AddCallback_Internal(
            ImDrawList* self,
            IntPtr callback,
            void* userdata,
            uint userdata_size);

        /// <summary>
        /// Simplified igInputText that accepts function pointers directly (like &amp;MyCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// Pass null for callback to use no callback (flags will be set to None automatically).
        /// </summary>
        public static bool InputText(
            string label,
            ref byte buf,
            uint buf_size,
            ImGuiInputTextFlags flags,
            delegate* unmanaged<ImGuiInputTextCallbackData*, int> callback,
            void* user_data = null)
        {
            // If no callback, clear all callback-related flags
            if (callback == null)
            {
                flags = ImGuiInputTextFlags.None;
            }
            return igInputText_Internal(label, ref buf, buf_size, flags, callback != null ? (IntPtr)callback : IntPtr.Zero, user_data);
        }

        /// <summary>
        /// Simplified igInputTextMultiline that accepts function pointers directly (like &amp;MyCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// Pass null for callback to use no callback (flags will be set to None automatically).
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
            // If no callback, clear all callback-related flags
            if (callback == null)
            {
                flags = ImGuiInputTextFlags.None;
            }
            return igInputTextMultiline_Internal(label, ref buf, buf_size, size, flags, callback != null ? (IntPtr)callback : IntPtr.Zero, user_data);
        }

        /// <summary>
        /// Simplified igInputTextWithHint that accepts function pointers directly (like &amp;MyCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// Pass null for callback to use no callback (flags will be set to None automatically).
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
            // If no callback, clear all callback-related flags
            if (callback == null)
            {
                flags = ImGuiInputTextFlags.None;
            }
            return igInputTextWithHint_Internal(label, hint, ref buf, buf_size, flags, callback != null ? (IntPtr)callback : IntPtr.Zero, user_data);
        }

        /// <summary>
        /// Simplified igSetNextWindowSizeConstraints that accepts function pointers directly (like &amp;MySizeCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// Pass null for custom_callback to use no callback.
        /// </summary>
        public static void SetNextWindowSizeConstraints(
            Vector2 size_min,
            Vector2 size_max,
            delegate* unmanaged<ImGuiSizeCallbackData*, void> custom_callback,
            void* custom_callback_data = null)
        {
            igSetNextWindowSizeConstraints_Internal(size_min, size_max, custom_callback != null ? (IntPtr)custom_callback : IntPtr.Zero, custom_callback_data);
        }

        /// <summary>
        /// Simplified ImDrawList_AddCallback that accepts function pointers directly (like &amp;MyDrawCallback).
        /// Use with [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] callbacks.
        /// The callback signature should be: void MyCallback(ImDrawList* parent_list, ImDrawCmd* cmd)
        /// Pass null for callback to use no callback.
        /// </summary>
        public static void DrawList_AddCallback(
            ImDrawList* drawList,
            delegate* unmanaged<ImDrawList*, ImDrawCmd*, void> callback,
            void* userdata = null,
            uint userdata_size = 0)
        {
            ImDrawList_AddCallback_Internal(drawList, callback != null ? (IntPtr)callback : IntPtr.Zero, userdata, userdata_size);
        }
    }
}
