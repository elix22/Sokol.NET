using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImGuiPlatformImeData
    {
        public byte WantVisible;
        public byte WantTextInput;
        public Vector2 InputPos;
        public float InputLineHeight;
        public uint ViewportId;
    }
    public unsafe partial struct ImGuiPlatformImeDataPtr
    {
        public ImGuiPlatformImeData* NativePtr { get; }
        public ImGuiPlatformImeDataPtr(ImGuiPlatformImeData* nativePtr) => NativePtr = nativePtr;
        public ImGuiPlatformImeDataPtr(IntPtr nativePtr) => NativePtr = (ImGuiPlatformImeData*)nativePtr;
        public static implicit operator ImGuiPlatformImeDataPtr(ImGuiPlatformImeData* nativePtr) => new ImGuiPlatformImeDataPtr(nativePtr);
        public static implicit operator ImGuiPlatformImeData* (ImGuiPlatformImeDataPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImGuiPlatformImeDataPtr(IntPtr nativePtr) => new ImGuiPlatformImeDataPtr(nativePtr);
        public ref bool WantVisible => ref Unsafe.AsRef<bool>(&NativePtr->WantVisible);
        public ref bool WantTextInput => ref Unsafe.AsRef<bool>(&NativePtr->WantTextInput);
        public ref Vector2 InputPos => ref Unsafe.AsRef<Vector2>(&NativePtr->InputPos);
        public ref float InputLineHeight => ref Unsafe.AsRef<float>(&NativePtr->InputLineHeight);
        public ref uint ViewportId => ref Unsafe.AsRef<uint>(&NativePtr->ViewportId);
        public void Destroy()
        {
            ImGuiNative.ImGuiPlatformImeData_destroy((ImGuiPlatformImeData*)(NativePtr));
        }
    }
}
