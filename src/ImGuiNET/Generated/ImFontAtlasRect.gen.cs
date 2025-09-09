using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImFontAtlasRect
    {
        public ushort x;
        public ushort y;
        public ushort w;
        public ushort h;
        public Vector2 uv0;
        public Vector2 uv1;
    }
    public unsafe partial struct ImFontAtlasRectPtr
    {
        public ImFontAtlasRect* NativePtr { get; }
        public ImFontAtlasRectPtr(ImFontAtlasRect* nativePtr) => NativePtr = nativePtr;
        public ImFontAtlasRectPtr(IntPtr nativePtr) => NativePtr = (ImFontAtlasRect*)nativePtr;
        public static implicit operator ImFontAtlasRectPtr(ImFontAtlasRect* nativePtr) => new ImFontAtlasRectPtr(nativePtr);
        public static implicit operator ImFontAtlasRect* (ImFontAtlasRectPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImFontAtlasRectPtr(IntPtr nativePtr) => new ImFontAtlasRectPtr(nativePtr);
        public ref ushort x => ref Unsafe.AsRef<ushort>(&NativePtr->x);
        public ref ushort y => ref Unsafe.AsRef<ushort>(&NativePtr->y);
        public ref ushort w => ref Unsafe.AsRef<ushort>(&NativePtr->w);
        public ref ushort h => ref Unsafe.AsRef<ushort>(&NativePtr->h);
        public ref Vector2 uv0 => ref Unsafe.AsRef<Vector2>(&NativePtr->uv0);
        public ref Vector2 uv1 => ref Unsafe.AsRef<Vector2>(&NativePtr->uv1);
        public void Destroy()
        {
            ImGuiNative.ImFontAtlasRect_destroy((ImFontAtlasRect*)(NativePtr));
        }
    }
}
