using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImGuiNET
{
    using ImFontAtlasRectId = int;

    public struct ImFontAtlasBuilder
    {
    
    }

    public unsafe struct ImFontAtlasBuilderPtr
    {
        public ImFontAtlasBuilder* NativePtr { get; }
        public ImFontAtlasBuilderPtr(ImFontAtlasBuilder* nativePtr) => NativePtr = nativePtr;
        public ImFontAtlasBuilderPtr(IntPtr nativePtr) => NativePtr = (ImFontAtlasBuilder*)nativePtr;
        public static implicit operator ImFontAtlasBuilderPtr(ImFontAtlasBuilder* nativePtr) => new ImFontAtlasBuilderPtr(nativePtr);
        public static implicit operator ImFontAtlasBuilder* (ImFontAtlasBuilderPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImFontAtlasBuilderPtr(IntPtr nativePtr) => new ImFontAtlasBuilderPtr(nativePtr);
    }
    
    public unsafe partial struct ImFontAtlas
    {
        public ImFontAtlasFlags Flags;
        public ImTextureFormat TexDesiredFormat;
        public int TexGlyphPadding;
        public int TexMinWidth;
        public int TexMinHeight;
        public int TexMaxWidth;
        public int TexMaxHeight;
        public void* UserData;
        public ImTextureRef TexRef;
        public ImTextureData* TexData;
        public ImVector TexList;
        public byte Locked;
        public byte RendererHasTextures;
        public byte TexIsBuilt;
        public byte TexPixelsUseColors;
        public Vector2 TexUvScale;
        public Vector2 TexUvWhitePixel;
        public ImVector Fonts;
        public ImVector Sources;
        public Vector4 TexUvLines_0;
        public Vector4 TexUvLines_1;
        public Vector4 TexUvLines_2;
        public Vector4 TexUvLines_3;
        public Vector4 TexUvLines_4;
        public Vector4 TexUvLines_5;
        public Vector4 TexUvLines_6;
        public Vector4 TexUvLines_7;
        public Vector4 TexUvLines_8;
        public Vector4 TexUvLines_9;
        public Vector4 TexUvLines_10;
        public Vector4 TexUvLines_11;
        public Vector4 TexUvLines_12;
        public Vector4 TexUvLines_13;
        public Vector4 TexUvLines_14;
        public Vector4 TexUvLines_15;
        public Vector4 TexUvLines_16;
        public Vector4 TexUvLines_17;
        public Vector4 TexUvLines_18;
        public Vector4 TexUvLines_19;
        public Vector4 TexUvLines_20;
        public Vector4 TexUvLines_21;
        public Vector4 TexUvLines_22;
        public Vector4 TexUvLines_23;
        public Vector4 TexUvLines_24;
        public Vector4 TexUvLines_25;
        public Vector4 TexUvLines_26;
        public Vector4 TexUvLines_27;
        public Vector4 TexUvLines_28;
        public Vector4 TexUvLines_29;
        public Vector4 TexUvLines_30;
        public Vector4 TexUvLines_31;
        public Vector4 TexUvLines_32;
        public int TexNextUniqueID;
        public int FontNextUniqueID;
        public ImVector DrawListSharedDatas;
        public ImFontAtlasBuilder* Builder;
        public ImFontLoader* FontLoader;
        public byte* FontLoaderName;
        public void* FontLoaderData;
        public uint FontLoaderFlags;
        public int RefCount;
        public IntPtr OwnerContext;
    }
    public unsafe partial struct ImFontAtlasPtr
    {
        public ImFontAtlas* NativePtr { get; }
        public ImFontAtlasPtr(ImFontAtlas* nativePtr) => NativePtr = nativePtr;
        public ImFontAtlasPtr(IntPtr nativePtr) => NativePtr = (ImFontAtlas*)nativePtr;
        public static implicit operator ImFontAtlasPtr(ImFontAtlas* nativePtr) => new ImFontAtlasPtr(nativePtr);
        public static implicit operator ImFontAtlas* (ImFontAtlasPtr wrappedPtr) => wrappedPtr.NativePtr;
        public static implicit operator ImFontAtlasPtr(IntPtr nativePtr) => new ImFontAtlasPtr(nativePtr);
        public ref ImFontAtlasFlags Flags => ref Unsafe.AsRef<ImFontAtlasFlags>(&NativePtr->Flags);
        public ref ImTextureFormat TexDesiredFormat => ref Unsafe.AsRef<ImTextureFormat>(&NativePtr->TexDesiredFormat);
        public ref int TexGlyphPadding => ref Unsafe.AsRef<int>(&NativePtr->TexGlyphPadding);
        public ref int TexMinWidth => ref Unsafe.AsRef<int>(&NativePtr->TexMinWidth);
        public ref int TexMinHeight => ref Unsafe.AsRef<int>(&NativePtr->TexMinHeight);
        public ref int TexMaxWidth => ref Unsafe.AsRef<int>(&NativePtr->TexMaxWidth);
        public ref int TexMaxHeight => ref Unsafe.AsRef<int>(&NativePtr->TexMaxHeight);
        public IntPtr UserData { get => (IntPtr)NativePtr->UserData; set => NativePtr->UserData = (void*)value; }
        public ref ImTextureRef TexRef => ref Unsafe.AsRef<ImTextureRef>(&NativePtr->TexRef);
        public ImTextureDataPtr TexData => new ImTextureDataPtr(NativePtr->TexData);
        public ImVector<ImTextureDataPtr> TexList => new ImVector<ImTextureDataPtr>(NativePtr->TexList);
        public ref bool Locked => ref Unsafe.AsRef<bool>(&NativePtr->Locked);
        public ref bool RendererHasTextures => ref Unsafe.AsRef<bool>(&NativePtr->RendererHasTextures);
        public ref bool TexIsBuilt => ref Unsafe.AsRef<bool>(&NativePtr->TexIsBuilt);
        public ref bool TexPixelsUseColors => ref Unsafe.AsRef<bool>(&NativePtr->TexPixelsUseColors);
        public ref Vector2 TexUvScale => ref Unsafe.AsRef<Vector2>(&NativePtr->TexUvScale);
        public ref Vector2 TexUvWhitePixel => ref Unsafe.AsRef<Vector2>(&NativePtr->TexUvWhitePixel);
        public ImVector<ImFontPtr> Fonts => new ImVector<ImFontPtr>(NativePtr->Fonts);
        public ImPtrVector<ImFontConfigPtr> Sources => new ImPtrVector<ImFontConfigPtr>(NativePtr->Sources, Unsafe.SizeOf<ImFontConfig>());
        public RangeAccessor<Vector4> TexUvLines => new RangeAccessor<Vector4>(&NativePtr->TexUvLines_0, 33);
        public ref int TexNextUniqueID => ref Unsafe.AsRef<int>(&NativePtr->TexNextUniqueID);
        public ref int FontNextUniqueID => ref Unsafe.AsRef<int>(&NativePtr->FontNextUniqueID);
        public ImVector<IntPtr> DrawListSharedDatas => new ImVector<IntPtr>(NativePtr->DrawListSharedDatas);
        public ImFontAtlasBuilderPtr Builder => new ImFontAtlasBuilderPtr(NativePtr->Builder);
        public ImFontLoaderPtr FontLoader => new ImFontLoaderPtr(NativePtr->FontLoader);
        public NullTerminatedString FontLoaderName => new NullTerminatedString(NativePtr->FontLoaderName);
        public IntPtr FontLoaderData { get => (IntPtr)NativePtr->FontLoaderData; set => NativePtr->FontLoaderData = (void*)value; }
        public ref uint FontLoaderFlags => ref Unsafe.AsRef<uint>(&NativePtr->FontLoaderFlags);
        public ref int RefCount => ref Unsafe.AsRef<int>(&NativePtr->RefCount);
        public ref IntPtr OwnerContext => ref Unsafe.AsRef<IntPtr>(&NativePtr->OwnerContext);
        public ImFontAtlasRectId AddCustomRect(int width, int height)
        {
            ImFontAtlasRect* out_r = null;
            ImFontAtlasRectId ret = ImGuiNative.ImFontAtlas_AddCustomRect((ImFontAtlas*)(NativePtr), width, height, out_r);
            return ret;
        }
        public ImFontAtlasRectId AddCustomRect(int width, int height, ImFontAtlasRectPtr out_r)
        {
            ImFontAtlasRect* native_out_r = out_r.NativePtr;
            ImFontAtlasRectId ret = ImGuiNative.ImFontAtlas_AddCustomRect((ImFontAtlas*)(NativePtr), width, height, native_out_r);
            return ret;
        }
        public ImFontPtr AddFont(ImFontConfigPtr font_cfg)
        {
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFont((ImFontAtlas*)(NativePtr), native_font_cfg);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontDefault()
        {
            ImFontConfig* font_cfg = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontDefault((ImFontAtlas*)(NativePtr), font_cfg);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontDefault(ImFontConfigPtr font_cfg)
        {
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontDefault((ImFontAtlas*)(NativePtr), native_font_cfg);
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromFileTTF(ReadOnlySpan<char> filename)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            float size_pixels = 0.0f;
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, font_cfg, glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromFileTTF(string filename)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            float size_pixels = 0.0f;
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, font_cfg, glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromFileTTF(ReadOnlySpan<char> filename, float size_pixels)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, font_cfg, glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromFileTTF(string filename, float size_pixels)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, font_cfg, glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromFileTTF(ReadOnlySpan<char> filename, float size_pixels, ImFontConfigPtr font_cfg)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, native_font_cfg, glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromFileTTF(string filename, float size_pixels, ImFontConfigPtr font_cfg)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, native_font_cfg, glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromFileTTF(ReadOnlySpan<char> filename, float size_pixels, ImFontConfigPtr font_cfg, IntPtr glyph_ranges)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* native_glyph_ranges = (ushort*)glyph_ranges.ToPointer();
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, native_font_cfg, native_glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromFileTTF(string filename, float size_pixels, ImFontConfigPtr font_cfg, IntPtr glyph_ranges)
        {
            byte* native_filename;
            int filename_byteCount = 0;
            if (filename != null)
            {
                filename_byteCount = Encoding.UTF8.GetByteCount(filename);
                if (filename_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_filename = Util.Allocate(filename_byteCount + 1);
                }
                else
                {
                    byte* native_filename_stackBytes = stackalloc byte[filename_byteCount + 1];
                    native_filename = native_filename_stackBytes;
                }
                int native_filename_offset = Util.GetUtf8(filename, native_filename, filename_byteCount);
                native_filename[native_filename_offset] = 0;
            }
            else { native_filename = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* native_glyph_ranges = (ushort*)glyph_ranges.ToPointer();
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromFileTTF((ImFontAtlas*)(NativePtr), native_filename, size_pixels, native_font_cfg, native_glyph_ranges);
            if (filename_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_filename);
            }
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(ReadOnlySpan<char> compressed_font_data_base85)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            float size_pixels = 0.0f;
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, font_cfg, glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(string compressed_font_data_base85)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            float size_pixels = 0.0f;
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, font_cfg, glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(ReadOnlySpan<char> compressed_font_data_base85, float size_pixels)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, font_cfg, glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(string compressed_font_data_base85, float size_pixels)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, font_cfg, glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(ReadOnlySpan<char> compressed_font_data_base85, float size_pixels, ImFontConfigPtr font_cfg)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, native_font_cfg, glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(string compressed_font_data_base85, float size_pixels, ImFontConfigPtr font_cfg)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, native_font_cfg, glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(ReadOnlySpan<char> compressed_font_data_base85, float size_pixels, ImFontConfigPtr font_cfg, IntPtr glyph_ranges)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* native_glyph_ranges = (ushort*)glyph_ranges.ToPointer();
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, native_font_cfg, native_glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
#endif
        public ImFontPtr AddFontFromMemoryCompressedBase85TTF(string compressed_font_data_base85, float size_pixels, ImFontConfigPtr font_cfg, IntPtr glyph_ranges)
        {
            byte* native_compressed_font_data_base85;
            int compressed_font_data_base85_byteCount = 0;
            if (compressed_font_data_base85 != null)
            {
                compressed_font_data_base85_byteCount = Encoding.UTF8.GetByteCount(compressed_font_data_base85);
                if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
                {
                    native_compressed_font_data_base85 = Util.Allocate(compressed_font_data_base85_byteCount + 1);
                }
                else
                {
                    byte* native_compressed_font_data_base85_stackBytes = stackalloc byte[compressed_font_data_base85_byteCount + 1];
                    native_compressed_font_data_base85 = native_compressed_font_data_base85_stackBytes;
                }
                int native_compressed_font_data_base85_offset = Util.GetUtf8(compressed_font_data_base85, native_compressed_font_data_base85, compressed_font_data_base85_byteCount);
                native_compressed_font_data_base85[native_compressed_font_data_base85_offset] = 0;
            }
            else { native_compressed_font_data_base85 = null; }
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* native_glyph_ranges = (ushort*)glyph_ranges.ToPointer();
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF((ImFontAtlas*)(NativePtr), native_compressed_font_data_base85, size_pixels, native_font_cfg, native_glyph_ranges);
            if (compressed_font_data_base85_byteCount > Util.StackAllocationSizeLimit)
            {
                Util.Free(native_compressed_font_data_base85);
            }
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryCompressedTTF(IntPtr compressed_font_data, int compressed_font_data_size)
        {
            void* native_compressed_font_data = (void*)compressed_font_data.ToPointer();
            float size_pixels = 0.0f;
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedTTF((ImFontAtlas*)(NativePtr), native_compressed_font_data, compressed_font_data_size, size_pixels, font_cfg, glyph_ranges);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryCompressedTTF(IntPtr compressed_font_data, int compressed_font_data_size, float size_pixels)
        {
            void* native_compressed_font_data = (void*)compressed_font_data.ToPointer();
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedTTF((ImFontAtlas*)(NativePtr), native_compressed_font_data, compressed_font_data_size, size_pixels, font_cfg, glyph_ranges);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryCompressedTTF(IntPtr compressed_font_data, int compressed_font_data_size, float size_pixels, ImFontConfigPtr font_cfg)
        {
            void* native_compressed_font_data = (void*)compressed_font_data.ToPointer();
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedTTF((ImFontAtlas*)(NativePtr), native_compressed_font_data, compressed_font_data_size, size_pixels, native_font_cfg, glyph_ranges);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryCompressedTTF(IntPtr compressed_font_data, int compressed_font_data_size, float size_pixels, ImFontConfigPtr font_cfg, IntPtr glyph_ranges)
        {
            void* native_compressed_font_data = (void*)compressed_font_data.ToPointer();
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* native_glyph_ranges = (ushort*)glyph_ranges.ToPointer();
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryCompressedTTF((ImFontAtlas*)(NativePtr), native_compressed_font_data, compressed_font_data_size, size_pixels, native_font_cfg, native_glyph_ranges);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryTTF(IntPtr font_data, int font_data_size)
        {
            void* native_font_data = (void*)font_data.ToPointer();
            float size_pixels = 0.0f;
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryTTF((ImFontAtlas*)(NativePtr), native_font_data, font_data_size, size_pixels, font_cfg, glyph_ranges);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryTTF(IntPtr font_data, int font_data_size, float size_pixels)
        {
            void* native_font_data = (void*)font_data.ToPointer();
            ImFontConfig* font_cfg = null;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryTTF((ImFontAtlas*)(NativePtr), native_font_data, font_data_size, size_pixels, font_cfg, glyph_ranges);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryTTF(IntPtr font_data, int font_data_size, float size_pixels, ImFontConfigPtr font_cfg)
        {
            void* native_font_data = (void*)font_data.ToPointer();
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* glyph_ranges = null;
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryTTF((ImFontAtlas*)(NativePtr), native_font_data, font_data_size, size_pixels, native_font_cfg, glyph_ranges);
            return new ImFontPtr(ret);
        }
        public ImFontPtr AddFontFromMemoryTTF(IntPtr font_data, int font_data_size, float size_pixels, ImFontConfigPtr font_cfg, IntPtr glyph_ranges)
        {
            void* native_font_data = (void*)font_data.ToPointer();
            ImFontConfig* native_font_cfg = font_cfg.NativePtr;
            ushort* native_glyph_ranges = (ushort*)glyph_ranges.ToPointer();
            ImFont* ret = ImGuiNative.ImFontAtlas_AddFontFromMemoryTTF((ImFontAtlas*)(NativePtr), native_font_data, font_data_size, size_pixels, native_font_cfg, native_glyph_ranges);
            return new ImFontPtr(ret);
        }
        public void Clear()
        {
            ImGuiNative.ImFontAtlas_Clear((ImFontAtlas*)(NativePtr));
        }
        public void ClearFonts()
        {
            ImGuiNative.ImFontAtlas_ClearFonts((ImFontAtlas*)(NativePtr));
        }
        public void ClearInputData()
        {
            ImGuiNative.ImFontAtlas_ClearInputData((ImFontAtlas*)(NativePtr));
        }
        public void ClearTexData()
        {
            ImGuiNative.ImFontAtlas_ClearTexData((ImFontAtlas*)(NativePtr));
        }
        public void CompactCache()
        {
            ImGuiNative.ImFontAtlas_CompactCache((ImFontAtlas*)(NativePtr));
        }
        public void Destroy()
        {
            ImGuiNative.ImFontAtlas_destroy((ImFontAtlas*)(NativePtr));
        }
        public bool GetCustomRect(ImFontAtlasRectId id, ImFontAtlasRectPtr out_r)
        {
            ImFontAtlasRect* native_out_r = out_r.NativePtr;
            byte ret = ImGuiNative.ImFontAtlas_GetCustomRect((ImFontAtlas*)(NativePtr), id, native_out_r);
            return ret != 0;
        }
        public IntPtr GetGlyphRangesDefault()
        {
            ushort* ret = ImGuiNative.ImFontAtlas_GetGlyphRangesDefault((ImFontAtlas*)(NativePtr));
            return (IntPtr)ret;
        }
        public void RemoveCustomRect(ImFontAtlasRectId id)
        {
            ImGuiNative.ImFontAtlas_RemoveCustomRect((ImFontAtlas*)(NativePtr), id);
        }
        public void RemoveFont(ImFontPtr font)
        {
            ImFont* native_font = font.NativePtr;
            ImGuiNative.ImFontAtlas_RemoveFont((ImFontAtlas*)(NativePtr), native_font);
        }
        public void SetFontLoader(ImFontLoaderPtr font_loader)
        {
            ImFontLoader* native_font_loader = (ImFontLoader*)font_loader.NativePtr;
            ImGuiNative.ImFontAtlas_SetFontLoader((ImFontAtlas*)(NativePtr), native_font_loader);
        }
    }
}
