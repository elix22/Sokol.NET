// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class Fontstash
{
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsCreateInternal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsCreateInternal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern IntPtr fonsCreateInternal(IntPtr parameters);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsDeleteInternal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsDeleteInternal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsDeleteInternal(IntPtr s);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsGetAtlasSize", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsGetAtlasSize", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsGetAtlasSize(IntPtr s, ref int width, ref int height);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsExpandAtlas", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsExpandAtlas", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsExpandAtlas(IntPtr s, int width, int height);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsResetAtlas", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsResetAtlas", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsResetAtlas(IntPtr stash, int width, int height);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsGetFontByName", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsGetFontByName", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsGetFontByName(IntPtr s, [M(U.LPUTF8Str)] string name);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsAddFallbackFont", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsAddFallbackFont", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsAddFallbackFont(IntPtr stash, int _base, int fallback);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsPushState", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsPushState", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsPushState(IntPtr s);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsPopState", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsPopState", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsPopState(IntPtr s);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsClearState", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsClearState", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsClearState(IntPtr s);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsSetSize", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsSetSize", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsSetSize(IntPtr s, float size);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsSetColor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsSetColor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsSetColor(IntPtr s, uint color);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsSetSpacing", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsSetSpacing", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsSetSpacing(IntPtr s, float spacing);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsSetBlur", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsSetBlur", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsSetBlur(IntPtr s, float blur);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsSetAlign", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsSetAlign", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsSetAlign(IntPtr s, int align);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsSetFont", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsSetFont", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsSetFont(IntPtr s, int font);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsDrawText", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsDrawText", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float fonsDrawText(IntPtr s, float x, float y, [M(U.LPUTF8Str)] string _string, [M(U.LPUTF8Str)] string end);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsTextBounds", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsTextBounds", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float fonsTextBounds(IntPtr s, float x, float y, [M(U.LPUTF8Str)] string _string, [M(U.LPUTF8Str)] string end, ref float bounds);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsLineBounds", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsLineBounds", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsLineBounds(IntPtr s, float y, ref float miny, ref float maxy);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsVertMetrics", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsVertMetrics", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsVertMetrics(IntPtr s, ref float ascender, ref float descender, ref float lineh);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsTextIterInit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsTextIterInit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsTextIterInit(IntPtr stash, IntPtr iter, float x, float y, [M(U.LPUTF8Str)] string str, [M(U.LPUTF8Str)] string end);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsTextIterNext", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsTextIterNext", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsTextIterNext(IntPtr stash, IntPtr iter, IntPtr quad);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsGetTextureData", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsGetTextureData", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern byte* fonsGetTextureData(IntPtr stash, ref int width, ref int height);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsValidateTexture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsValidateTexture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsValidateTexture(IntPtr s, ref int dirty);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsDrawDebug", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsDrawDebug", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void fonsDrawDebug(IntPtr s, float x, float y);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "fonsAddFontMem", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "fonsAddFontMem", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int fonsAddFontMem(IntPtr stash, [M(U.LPUTF8Str)] string name, byte* data, int dataSize, int freeData);

}
}
