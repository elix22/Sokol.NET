using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImGuiPlatformIO
    {
        public IntPtr Platform_GetClipboardTextFn;
        public IntPtr Platform_SetClipboardTextFn;
        public void* Platform_ClipboardUserData;
        public IntPtr Platform_OpenInShellFn;
        public void* Platform_OpenInShellUserData;
        public IntPtr Platform_SetImeDataFn;
        public void* Platform_ImeUserData;
        public ushort Platform_LocaleDecimalPoint;
        public int Renderer_TextureMaxWidth;
        public int Renderer_TextureMaxHeight;
        public void* Renderer_RenderState;
        public ImVector Textures;
    }
    public unsafe partial struct ImGuiPlatformIOPtr
    {
        public ImGuiPlatformIO* NativePtr { get; }
        public ImGuiPlatformIOPtr(ImGuiPlatformIO* nativePtr) => NativePtr = nativePtr;
        public ImGuiPlatformIOPtr(IntPtr nativePtr) => NativePtr = (ImGuiPlatformIO*)nativePtr;
        public static implicit operator ImGuiPlatformIOPtr(ImGuiPlatformIO* nativePtr) => new ImGuiPlatformIOPtr(nativePtr);
        public static implicit operator ImGuiPlatformIO* (ImGuiPlatformIOPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImGuiPlatformIOPtr(IntPtr nativePtr) => new ImGuiPlatformIOPtr(nativePtr);
        public ref IntPtr Platform_GetClipboardTextFn => ref Unsafe.AsRef<IntPtr>(&NativePtr->Platform_GetClipboardTextFn);
        public ref IntPtr Platform_SetClipboardTextFn => ref Unsafe.AsRef<IntPtr>(&NativePtr->Platform_SetClipboardTextFn);
        public IntPtr Platform_ClipboardUserData { get => (IntPtr)NativePtr->Platform_ClipboardUserData; set => NativePtr->Platform_ClipboardUserData = (void*)value; }
        public ref IntPtr Platform_OpenInShellFn => ref Unsafe.AsRef<IntPtr>(&NativePtr->Platform_OpenInShellFn);
        public IntPtr Platform_OpenInShellUserData { get => (IntPtr)NativePtr->Platform_OpenInShellUserData; set => NativePtr->Platform_OpenInShellUserData = (void*)value; }
        public ref IntPtr Platform_SetImeDataFn => ref Unsafe.AsRef<IntPtr>(&NativePtr->Platform_SetImeDataFn);
        public IntPtr Platform_ImeUserData { get => (IntPtr)NativePtr->Platform_ImeUserData; set => NativePtr->Platform_ImeUserData = (void*)value; }
        public ref ushort Platform_LocaleDecimalPoint => ref Unsafe.AsRef<ushort>(&NativePtr->Platform_LocaleDecimalPoint);
        public ref int Renderer_TextureMaxWidth => ref Unsafe.AsRef<int>(&NativePtr->Renderer_TextureMaxWidth);
        public ref int Renderer_TextureMaxHeight => ref Unsafe.AsRef<int>(&NativePtr->Renderer_TextureMaxHeight);
        public IntPtr Renderer_RenderState { get => (IntPtr)NativePtr->Renderer_RenderState; set => NativePtr->Renderer_RenderState = (void*)value; }
        public ImVector<ImTextureDataPtr> Textures => new ImVector<ImTextureDataPtr>(NativePtr->Textures);
        public void Destroy()
        {
            ImGuiNative.ImGuiPlatformIO_destroy((ImGuiPlatformIO*)(NativePtr));
        }
    }
}
