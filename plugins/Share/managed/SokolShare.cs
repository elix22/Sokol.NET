// P/Invoke bindings to the native sokolshare_image_text() function.
// Android, iOS, macOS: native share sheet with image.
// Windows / Linux: managed fallback in SharePlugin.cs.
#if __ANDROID__ || __IOS__ || __MACOS__
using System.Runtime.InteropServices;

internal static class SokolShare
{
#if __IOS__
    const string Lib = "@rpath/sokol_share.framework/sokol_share";
#else
    const string Lib = "sokol_share";
#endif

    // Share an image file and a text string via the platform's native share sheet.
    // imagePath: absolute path to a PNG written by SFilesystem (pass null for text-only).
    // text:      UTF-8 share message.
    [DllImport(Lib, EntryPoint = "sokolshare_image_text",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ShareImageText(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? imagePath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string  text);

#if __ANDROID__ || __IOS__
    // Put text on the system clipboard. Android/iOS ONLY -- the two platforms
    // sapp_set_clipboard_string() leaves unimplemented. macOS is deliberately
    // excluded (sokol handles it), so the macOS dylib needs no such symbol.
    [DllImport(Lib, EntryPoint = "sokolshare_set_clipboard",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetClipboard(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    // Share one existing file of any type, keeping its name. Android/iOS ONLY (the macOS dylib has no such
    // symbol). Returns 1 when the share sheet was asked to open, 0 when the file cannot be shared.
    [DllImport(Lib, EntryPoint = "sokolshare_file",
               CallingConvention = CallingConvention.Cdecl)]
    internal static extern int ShareFile(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string  path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? mime,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? title);
#endif
}
#endif
