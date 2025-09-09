using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImFontBaked
    {
        public ImVector IndexAdvanceX;
        public float FallbackAdvanceX;
        public float Size;
        public float RasterizerDensity;
        public ImVector IndexLookup;
        public ImVector Glyphs;
        public int FallbackGlyphIndex;
        public float Ascent;
        public float Descent;
        public uint MetricsTotalSurface;
        public uint WantDestroy;
        public uint LoadNoFallback;
        public uint LoadNoRenderOnLayout;
        public int LastUsedFrame;
        public uint BakedId;
        public ImFont* ContainerFont;
        public void* FontLoaderDatas;
    }
    public unsafe partial struct ImFontBakedPtr
    {
        public ImFontBaked* NativePtr { get; }
        public ImFontBakedPtr(ImFontBaked* nativePtr) => NativePtr = nativePtr;
        public ImFontBakedPtr(IntPtr nativePtr) => NativePtr = (ImFontBaked*)nativePtr;
        public static implicit operator ImFontBakedPtr(ImFontBaked* nativePtr) => new ImFontBakedPtr(nativePtr);
        public static implicit operator ImFontBaked* (ImFontBakedPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImFontBakedPtr(IntPtr nativePtr) => new ImFontBakedPtr(nativePtr);
        public ImVector<float> IndexAdvanceX => new ImVector<float>(NativePtr->IndexAdvanceX);
        public ref float FallbackAdvanceX => ref Unsafe.AsRef<float>(&NativePtr->FallbackAdvanceX);
        public ref float Size => ref Unsafe.AsRef<float>(&NativePtr->Size);
        public ref float RasterizerDensity => ref Unsafe.AsRef<float>(&NativePtr->RasterizerDensity);
        public ImVector<ushort> IndexLookup => new ImVector<ushort>(NativePtr->IndexLookup);
        public ImPtrVector<ImFontGlyphPtr> Glyphs => new ImPtrVector<ImFontGlyphPtr>(NativePtr->Glyphs, Unsafe.SizeOf<ImFontGlyph>());
        public ref int FallbackGlyphIndex => ref Unsafe.AsRef<int>(&NativePtr->FallbackGlyphIndex);
        public ref float Ascent => ref Unsafe.AsRef<float>(&NativePtr->Ascent);
        public ref float Descent => ref Unsafe.AsRef<float>(&NativePtr->Descent);
        public ref uint MetricsTotalSurface => ref Unsafe.AsRef<uint>(&NativePtr->MetricsTotalSurface);
        public ref uint WantDestroy => ref Unsafe.AsRef<uint>(&NativePtr->WantDestroy);
        public ref uint LoadNoFallback => ref Unsafe.AsRef<uint>(&NativePtr->LoadNoFallback);
        public ref uint LoadNoRenderOnLayout => ref Unsafe.AsRef<uint>(&NativePtr->LoadNoRenderOnLayout);
        public ref int LastUsedFrame => ref Unsafe.AsRef<int>(&NativePtr->LastUsedFrame);
        public ref uint BakedId => ref Unsafe.AsRef<uint>(&NativePtr->BakedId);
        public ImFontPtr ContainerFont => new ImFontPtr(NativePtr->ContainerFont);
        public IntPtr FontLoaderDatas { get => (IntPtr)NativePtr->FontLoaderDatas; set => NativePtr->FontLoaderDatas = (void*)value; }
        public void ClearOutputData()
        {
            ImGuiNative.ImFontBaked_ClearOutputData((ImFontBaked*)(NativePtr));
        }
        public void Destroy()
        {
            ImGuiNative.ImFontBaked_destroy((ImFontBaked*)(NativePtr));
        }
        public ImFontGlyphPtr FindGlyph(ushort c)
        {
            ImFontGlyph* ret = ImGuiNative.ImFontBaked_FindGlyph((ImFontBaked*)(NativePtr), c);
            return new ImFontGlyphPtr(ret);
        }
        public ImFontGlyphPtr FindGlyphNoFallback(ushort c)
        {
            ImFontGlyph* ret = ImGuiNative.ImFontBaked_FindGlyphNoFallback((ImFontBaked*)(NativePtr), c);
            return new ImFontGlyphPtr(ret);
        }
        public float GetCharAdvance(ushort c)
        {
            float ret = ImGuiNative.ImFontBaked_GetCharAdvance((ImFontBaked*)(NativePtr), c);
            return ret;
        }
        public bool IsGlyphLoaded(ushort c)
        {
            byte ret = ImGuiNative.ImFontBaked_IsGlyphLoaded((ImFontBaked*)(NativePtr), c);
            return ret != 0;
        }
    }
}
