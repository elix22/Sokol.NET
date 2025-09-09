using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImTextureRect
    {
        public ushort x;
        public ushort y;
        public ushort w;
        public ushort h;
    }
    public unsafe partial struct ImTextureRectPtr
    {
        public ImTextureRect* NativePtr { get; }
        public ImTextureRectPtr(ImTextureRect* nativePtr) => NativePtr = nativePtr;
        public ImTextureRectPtr(IntPtr nativePtr) => NativePtr = (ImTextureRect*)nativePtr;
        public static implicit operator ImTextureRectPtr(ImTextureRect* nativePtr) => new ImTextureRectPtr(nativePtr);
        public static implicit operator ImTextureRect* (ImTextureRectPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImTextureRectPtr(IntPtr nativePtr) => new ImTextureRectPtr(nativePtr);
        public ref ushort x => ref Unsafe.AsRef<ushort>(&NativePtr->x);
        public ref ushort y => ref Unsafe.AsRef<ushort>(&NativePtr->y);
        public ref ushort w => ref Unsafe.AsRef<ushort>(&NativePtr->w);
        public ref ushort h => ref Unsafe.AsRef<ushort>(&NativePtr->h);
    }
}
