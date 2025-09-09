using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImTextureData
    {
        public int UniqueID;
        public ImTextureStatus Status;
        public void* BackendUserData;
        public IntPtr TexID;
        public ImTextureFormat Format;
        public int Width;
        public int Height;
        public int BytesPerPixel;
        public byte* Pixels;
        public ImTextureRect UsedRect;
        public ImTextureRect UpdateRect;
        public ImVector Updates;
        public int UnusedFrames;
        public ushort RefCount;
        public byte UseColors;
        public byte WantDestroyNextFrame;
    }
    public unsafe partial struct ImTextureDataPtr
    {
        public ImTextureData* NativePtr { get; }
        public ImTextureDataPtr(ImTextureData* nativePtr) => NativePtr = nativePtr;
        public ImTextureDataPtr(IntPtr nativePtr) => NativePtr = (ImTextureData*)nativePtr;
        public static implicit operator ImTextureDataPtr(ImTextureData* nativePtr) => new ImTextureDataPtr(nativePtr);
        public static implicit operator ImTextureData* (ImTextureDataPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImTextureDataPtr(IntPtr nativePtr) => new ImTextureDataPtr(nativePtr);
        public ref int UniqueID => ref Unsafe.AsRef<int>(&NativePtr->UniqueID);
        public ref ImTextureStatus Status => ref Unsafe.AsRef<ImTextureStatus>(&NativePtr->Status);
        public IntPtr BackendUserData { get => (IntPtr)NativePtr->BackendUserData; set => NativePtr->BackendUserData = (void*)value; }
        public ref IntPtr TexID => ref Unsafe.AsRef<IntPtr>(&NativePtr->TexID);
        public ref ImTextureFormat Format => ref Unsafe.AsRef<ImTextureFormat>(&NativePtr->Format);
        public ref int Width => ref Unsafe.AsRef<int>(&NativePtr->Width);
        public ref int Height => ref Unsafe.AsRef<int>(&NativePtr->Height);
        public ref int BytesPerPixel => ref Unsafe.AsRef<int>(&NativePtr->BytesPerPixel);
        public IntPtr Pixels { get => (IntPtr)NativePtr->Pixels; set => NativePtr->Pixels = (byte*)value; }
        public ref ImTextureRect UsedRect => ref Unsafe.AsRef<ImTextureRect>(&NativePtr->UsedRect);
        public ref ImTextureRect UpdateRect => ref Unsafe.AsRef<ImTextureRect>(&NativePtr->UpdateRect);
        public ImPtrVector<ImTextureRectPtr> Updates => new ImPtrVector<ImTextureRectPtr>(NativePtr->Updates, Unsafe.SizeOf<ImTextureRect>());
        public ref int UnusedFrames => ref Unsafe.AsRef<int>(&NativePtr->UnusedFrames);
        public ref ushort RefCount => ref Unsafe.AsRef<ushort>(&NativePtr->RefCount);
        public ref bool UseColors => ref Unsafe.AsRef<bool>(&NativePtr->UseColors);
        public ref bool WantDestroyNextFrame => ref Unsafe.AsRef<bool>(&NativePtr->WantDestroyNextFrame);
        public void Create(ImTextureFormat format, int w, int h)
        {
            ImGuiNative.ImTextureData_Create((ImTextureData*)(NativePtr), format, w, h);
        }
        public void Destroy()
        {
            ImGuiNative.ImTextureData_destroy((ImTextureData*)(NativePtr));
        }
        public void DestroyPixels()
        {
            ImGuiNative.ImTextureData_DestroyPixels((ImTextureData*)(NativePtr));
        }
        public int GetPitch()
        {
            int ret = ImGuiNative.ImTextureData_GetPitch((ImTextureData*)(NativePtr));
            return ret;
        }
        public IntPtr GetPixels()
        {
            void* ret = ImGuiNative.ImTextureData_GetPixels((ImTextureData*)(NativePtr));
            return (IntPtr)ret;
        }
        public IntPtr GetPixelsAt(int x, int y)
        {
            void* ret = ImGuiNative.ImTextureData_GetPixelsAt((ImTextureData*)(NativePtr), x, y);
            return (IntPtr)ret;
        }
        public int GetSizeInBytes()
        {
            int ret = ImGuiNative.ImTextureData_GetSizeInBytes((ImTextureData*)(NativePtr));
            return ret;
        }
        public IntPtr GetTexID()
        {
            IntPtr ret = ImGuiNative.ImTextureData_GetTexID((ImTextureData*)(NativePtr));
            return ret;
        }
        public ImTextureRef GetTexRef()
        {
            ImTextureRef __retval;
            ImGuiNative.ImTextureData_GetTexRef(&__retval, (ImTextureData*)(NativePtr));
            return __retval;
        }
        public void SetStatus(ImTextureStatus status)
        {
            ImGuiNative.ImTextureData_SetStatus((ImTextureData*)(NativePtr), status);
        }
        public void SetTexID(IntPtr tex_id)
        {
            ImGuiNative.ImTextureData_SetTexID((ImTextureData*)(NativePtr), tex_id);
        }
    }
}
