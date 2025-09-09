using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    public unsafe partial struct ImFont
    {
        public ImFontBaked* LastBaked;
        public ImFontAtlas* ContainerAtlas;
        public ImFontFlags Flags;
        public float CurrentRasterizerDensity;
        public uint FontId;
        public float LegacySize;
        public ImVector Sources;
        public ushort EllipsisChar;
        public ushort FallbackChar;
        public fixed byte Used8kPagesMap[1];
        public byte EllipsisAutoBake;
        public ImGuiStorage RemapPairs;
    }
    public unsafe partial struct ImFontPtr
    {
        public ImFont* NativePtr { get; }
        public ImFontPtr(ImFont* nativePtr) => NativePtr = nativePtr;
        public ImFontPtr(IntPtr nativePtr) => NativePtr = (ImFont*)nativePtr;
        public static implicit operator ImFontPtr(ImFont* nativePtr) => new ImFontPtr(nativePtr);
        public static implicit operator ImFont* (ImFontPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImFontPtr(IntPtr nativePtr) => new ImFontPtr(nativePtr);
        public ImFontBakedPtr LastBaked => new ImFontBakedPtr(NativePtr->LastBaked);
        public ImFontAtlasPtr ContainerAtlas => new ImFontAtlasPtr(NativePtr->ContainerAtlas);
        public ref ImFontFlags Flags => ref Unsafe.AsRef<ImFontFlags>(&NativePtr->Flags);
        public ref float CurrentRasterizerDensity => ref Unsafe.AsRef<float>(&NativePtr->CurrentRasterizerDensity);
        public ref uint FontId => ref Unsafe.AsRef<uint>(&NativePtr->FontId);
        public ref float LegacySize => ref Unsafe.AsRef<float>(&NativePtr->LegacySize);
        public ImVector<ImFontConfigPtr> Sources => new ImVector<ImFontConfigPtr>(NativePtr->Sources);
        public ref ushort EllipsisChar => ref Unsafe.AsRef<ushort>(&NativePtr->EllipsisChar);
        public ref ushort FallbackChar => ref Unsafe.AsRef<ushort>(&NativePtr->FallbackChar);
        public RangeAccessor<byte> Used8kPagesMap => new RangeAccessor<byte>(NativePtr->Used8kPagesMap, 1);
        public ref bool EllipsisAutoBake => ref Unsafe.AsRef<bool>(&NativePtr->EllipsisAutoBake);
        public ref ImGuiStorage RemapPairs => ref Unsafe.AsRef<ImGuiStorage>(&NativePtr->RemapPairs);
        public void AddRemapChar(ushort from_codepoint, ushort to_codepoint)
        {
            ImGuiNative.ImFont_AddRemapChar((ImFont*)(NativePtr), from_codepoint, to_codepoint);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public Vector2 CalcTextSizeA(float size, float max_width, float wrap_width, ReadOnlySpan<char> text_begin)
        {
            Vector2 __retval;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            byte** remaining = null;
            ImGuiNative.ImFont_CalcTextSizeA(&__retval, (ImFont*)(NativePtr), size, max_width, wrap_width, native_text_begin, native_text_begin+text_begin_byteCount, remaining);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
            return __retval;
        }
#endif
        public Vector2 CalcTextSizeA(float size, float max_width, float wrap_width, string text_begin)
        {
            Vector2 __retval;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            byte** remaining = null;
            ImGuiNative.ImFont_CalcTextSizeA(&__retval, (ImFont*)(NativePtr), size, max_width, wrap_width, native_text_begin, native_text_begin+text_begin_byteCount, remaining);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
            return __retval;
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public Vector2 CalcTextSizeA(float size, float max_width, float wrap_width, ReadOnlySpan<char> text_begin, ref byte* remaining)
        {
            Vector2 __retval;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            fixed (byte** native_remaining = &remaining)
            {
                ImGuiNative.ImFont_CalcTextSizeA(&__retval, (ImFont*)(NativePtr), size, max_width, wrap_width, native_text_begin, native_text_begin+text_begin_byteCount, native_remaining);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    Util.Free(native_text_begin);
                }
                return __retval;
            }
        }
#endif
        public Vector2 CalcTextSizeA(float size, float max_width, float wrap_width, string text_begin, ref byte* remaining)
        {
            Vector2 __retval;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            fixed (byte** native_remaining = &remaining)
            {
                ImGuiNative.ImFont_CalcTextSizeA(&__retval, (ImFont*)(NativePtr), size, max_width, wrap_width, native_text_begin, native_text_begin+text_begin_byteCount, native_remaining);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    Util.Free(native_text_begin);
                }
                return __retval;
            }
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public string CalcWordWrapPosition(float size, ReadOnlySpan<char> text, float wrap_width)
        {
            byte* native_text;
            int text_byteCount = 0;
            if (text != null)
            {
                text_byteCount = Encoding.UTF8.GetByteCount(text);
                if (text_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text = Util.Allocate(text_byteCount + 1);
                }
                else
                {
                    byte* native_text_stackBytes = stackalloc byte[text_byteCount + 1];
                    native_text = native_text_stackBytes;
                }
                int native_text_offset = Util.GetUtf8(text, native_text, text_byteCount);
                native_text[native_text_offset] = 0;
            }
            else { native_text = null; }
            byte* ret = ImGuiNative.ImFont_CalcWordWrapPosition((ImFont*)(NativePtr), size, native_text, native_text+text_byteCount, wrap_width);
            if (text_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text);
            }
            return Util.StringFromPtr(ret);
        }
#endif
        public string CalcWordWrapPosition(float size, string text, float wrap_width)
        {
            byte* native_text;
            int text_byteCount = 0;
            if (text != null)
            {
                text_byteCount = Encoding.UTF8.GetByteCount(text);
                if (text_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text = Util.Allocate(text_byteCount + 1);
                }
                else
                {
                    byte* native_text_stackBytes = stackalloc byte[text_byteCount + 1];
                    native_text = native_text_stackBytes;
                }
                int native_text_offset = Util.GetUtf8(text, native_text, text_byteCount);
                native_text[native_text_offset] = 0;
            }
            else { native_text = null; }
            byte* ret = ImGuiNative.ImFont_CalcWordWrapPosition((ImFont*)(NativePtr), size, native_text, native_text+text_byteCount, wrap_width);
            if (text_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text);
            }
            return Util.StringFromPtr(ret);
        }
        public void ClearOutputData()
        {
            ImGuiNative.ImFont_ClearOutputData((ImFont*)(NativePtr));
        }
        public void Destroy()
        {
            ImGuiNative.ImFont_destroy((ImFont*)(NativePtr));
        }
        public string GetDebugName()
        {
            byte* ret = ImGuiNative.ImFont_GetDebugName((ImFont*)(NativePtr));
            return Util.StringFromPtr(ret);
        }
        public ImFontBakedPtr GetFontBaked(float font_size)
        {
            float density = -1.0f;
            ImFontBaked* ret = ImGuiNative.ImFont_GetFontBaked((ImFont*)(NativePtr), font_size, density);
            return new ImFontBakedPtr(ret);
        }
        public ImFontBakedPtr GetFontBaked(float font_size, float density)
        {
            ImFontBaked* ret = ImGuiNative.ImFont_GetFontBaked((ImFont*)(NativePtr), font_size, density);
            return new ImFontBakedPtr(ret);
        }
        public bool IsGlyphInFont(ushort c)
        {
            byte ret = ImGuiNative.ImFont_IsGlyphInFont((ImFont*)(NativePtr), c);
            return ret != 0;
        }
        public bool IsLoaded()
        {
            byte ret = ImGuiNative.ImFont_IsLoaded((ImFont*)(NativePtr));
            return ret != 0;
        }
        public void RenderChar(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, ushort c)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            Vector4* cpu_fine_clip = null;
            ImGuiNative.ImFont_RenderChar((ImFont*)(NativePtr), native_draw_list, size, pos, col, c, cpu_fine_clip);
        }
        public void RenderChar(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, ushort c, ref Vector4 cpu_fine_clip)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            fixed (Vector4* native_cpu_fine_clip = &cpu_fine_clip)
            {
                ImGuiNative.ImFont_RenderChar((ImFont*)(NativePtr), native_draw_list, size, pos, col, c, native_cpu_fine_clip);
            }
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public void RenderText(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, ReadOnlySpan<char> text_begin)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            float wrap_width = 0.0f;
            byte cpu_fine_clip = 0;
            ImGuiNative.ImFont_RenderText((ImFont*)(NativePtr), native_draw_list, size, pos, col, clip_rect, native_text_begin, native_text_begin+text_begin_byteCount, wrap_width, cpu_fine_clip);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
        }
#endif
        public void RenderText(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, string text_begin)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            float wrap_width = 0.0f;
            byte cpu_fine_clip = 0;
            ImGuiNative.ImFont_RenderText((ImFont*)(NativePtr), native_draw_list, size, pos, col, clip_rect, native_text_begin, native_text_begin+text_begin_byteCount, wrap_width, cpu_fine_clip);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public void RenderText(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, ReadOnlySpan<char> text_begin, float wrap_width)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            byte cpu_fine_clip = 0;
            ImGuiNative.ImFont_RenderText((ImFont*)(NativePtr), native_draw_list, size, pos, col, clip_rect, native_text_begin, native_text_begin+text_begin_byteCount, wrap_width, cpu_fine_clip);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
        }
#endif
        public void RenderText(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, string text_begin, float wrap_width)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            byte cpu_fine_clip = 0;
            ImGuiNative.ImFont_RenderText((ImFont*)(NativePtr), native_draw_list, size, pos, col, clip_rect, native_text_begin, native_text_begin+text_begin_byteCount, wrap_width, cpu_fine_clip);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public void RenderText(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, ReadOnlySpan<char> text_begin, float wrap_width, bool cpu_fine_clip)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            byte native_cpu_fine_clip = cpu_fine_clip ? (byte)1 : (byte)0;
            ImGuiNative.ImFont_RenderText((ImFont*)(NativePtr), native_draw_list, size, pos, col, clip_rect, native_text_begin, native_text_begin+text_begin_byteCount, wrap_width, native_cpu_fine_clip);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
        }
#endif
        public void RenderText(ImDrawListPtr draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, string text_begin, float wrap_width, bool cpu_fine_clip)
        {
            ImDrawList* native_draw_list = draw_list.NativePtr;
            byte* native_text_begin;
            int text_begin_byteCount = 0;
                text_begin_byteCount = Encoding.UTF8.GetByteCount(text_begin);
                if (text_begin_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_text_begin = Util.Allocate(text_begin_byteCount + 1);
                }
                else
                {
                    byte* native_text_begin_stackBytes = stackalloc byte[text_begin_byteCount + 1];
                    native_text_begin = native_text_begin_stackBytes;
                }
                int native_text_begin_offset = Util.GetUtf8(text_begin, native_text_begin, text_begin_byteCount);
                native_text_begin[native_text_begin_offset] = 0;
            byte native_cpu_fine_clip = cpu_fine_clip ? (byte)1 : (byte)0;
            ImGuiNative.ImFont_RenderText((ImFont*)(NativePtr), native_draw_list, size, pos, col, clip_rect, native_text_begin, native_text_begin+text_begin_byteCount, wrap_width, native_cpu_fine_clip);
            if (text_begin_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_text_begin);
            }
        }
    }
}
