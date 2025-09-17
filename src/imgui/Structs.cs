using System.Numerics;
using Imgui;
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

//TBD ELI
using ImVector_ImTextureDataPtr = System.IntPtr;
using ImVector_ImFontConfigPtr = System.IntPtr;
using ImVector_ImTextureRef = System.IntPtr;
using ImVector_ImDrawListSharedDataPtr = System.IntPtr;
using ImFontAtlasBuilder = System.IntPtr;
using ImFontAtlasRectId = int;

public struct ImVector_ImTextureRect
{
	
}

namespace Imgui
{
	public unsafe partial struct ImColor
	{
		public Vector4 Value;

		public ImColor HSV(float h, float s, float v, float a = 1.0f)
		{
			ImColor pOut;
			ImguiNative.ImColor_HSV(&pOut, h, s, v, a);

			return pOut;
		}

		public void SetHSV(float h, float s, float v, float a = 1.0f)
		{
			ImguiNative.ImColor_SetHSV(self, h, s, v, a);
		}

		public ImColor* self => (ImColor*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImDrawChannel
	{
		public ImVector _CmdBuffer;
		public ImVector _IdxBuffer;
	}

	public unsafe partial struct ImDrawCmd
	{
		public Vector4 ClipRect;
		public ImTextureRef TexRef;
		public uint VtxOffset;
		public uint IdxOffset;
		public uint ElemCount;
		public IntPtr UserCallback;
		public void* UserCallbackData;
		public int UserCallbackDataSize;
		public int UserCallbackDataOffset;

		public ulong GetTexID()
		{
			return ImguiNative.ImDrawCmd_GetTexID(self);
		}

		public ImDrawCmd* self => (ImDrawCmd*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImDrawCmdHeader
	{
		public Vector4 ClipRect;
		public ImTextureRef TexRef;
		public uint VtxOffset;
	}

	public unsafe partial struct ImDrawData
	{
		public byte Valid;
		public int CmdListsCount;
		public int TotalIdxCount;
		public int TotalVtxCount;
		public ImVector CmdLists;
		public Vector2 DisplayPos;
		public Vector2 DisplaySize;
		public Vector2 FramebufferScale;
		public ImGuiViewport* OwnerViewport;
		public ImVector_ImTextureDataPtr* Textures;

		public void AddDrawList(ImDrawList* draw_list)
		{
			ImguiNative.ImDrawData_AddDrawList(self, draw_list);
		}

		public void Clear()
		{
			ImguiNative.ImDrawData_Clear(self);
		}

		public void DeIndexAllBuffers()
		{
			ImguiNative.ImDrawData_DeIndexAllBuffers(self);
		}

		public void ScaleClipRects(Vector2 fb_scale)
		{
			ImguiNative.ImDrawData_ScaleClipRects(self, fb_scale);
		}

		public ImDrawData* self => (ImDrawData*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImDrawList
	{
		public ImVector CmdBuffer;
		public ImVector IdxBuffer;
		public ImVector VtxBuffer;
		public ImDrawListFlags Flags;
		public uint _VtxCurrentIdx;
		public IntPtr _Data;
		public ImDrawVert* _VtxWritePtr;
		public ushort* _IdxWritePtr;
		public ImVector _Path;
		public ImDrawCmdHeader _CmdHeader;
		public ImDrawListSplitter _Splitter;
		public ImVector _ClipRectStack;
		public ImVector_ImTextureRef _TextureStack;
		public ImVector _CallbacksDataBuf;
		public float _FringeScale;
		public byte* _OwnerName;

		public void AddBezierCubic(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, uint col, float thickness, int num_segments = 0)
		{
			ImguiNative.ImDrawList_AddBezierCubic(self, p1, p2, p3, p4, col, thickness, num_segments);
		}

		public void AddBezierQuadratic(Vector2 p1, Vector2 p2, Vector2 p3, uint col, float thickness, int num_segments = 0)
		{
			ImguiNative.ImDrawList_AddBezierQuadratic(self, p1, p2, p3, col, thickness, num_segments);
		}

		public void AddCallback(IntPtr callback, void* userdata, uint userdata_size = 0)
		{
			ImguiNative.ImDrawList_AddCallback(self, callback, userdata, userdata_size);
		}

		public void AddCircle(Vector2 center, float radius, uint col, int num_segments = 0, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_AddCircle(self, center, radius, col, num_segments, thickness);
		}

		public void AddCircleFilled(Vector2 center, float radius, uint col, int num_segments = 0)
		{
			ImguiNative.ImDrawList_AddCircleFilled(self, center, radius, col, num_segments);
		}

		public void AddConcavePolyFilled(Vector2* points, int num_points, uint col)
		{
			ImguiNative.ImDrawList_AddConcavePolyFilled(self, points, num_points, col);
		}

		public void AddConvexPolyFilled(Vector2* points, int num_points, uint col)
		{
			ImguiNative.ImDrawList_AddConvexPolyFilled(self, points, num_points, col);
		}

		public void AddDrawCmd()
		{
			ImguiNative.ImDrawList_AddDrawCmd(self);
		}

		public void AddEllipse(Vector2 center, Vector2 radius, uint col, float rot = 0.0f, int num_segments = 0, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_AddEllipse(self, center, radius, col, rot, num_segments, thickness);
		}

		public void AddEllipseFilled(Vector2 center, Vector2 radius, uint col, float rot = 0.0f, int num_segments = 0)
		{
			ImguiNative.ImDrawList_AddEllipseFilled(self, center, radius, col, rot, num_segments);
		}

		public void AddImage(ImTextureRef tex_ref, Vector2 p_min, Vector2 p_max, Vector2 uv_min, Vector2 uv_max, uint col = 4294967295)
		{
			ImguiNative.ImDrawList_AddImage(self, tex_ref, p_min, p_max, uv_min, uv_max, col);
		}

		public void AddImageQuad(ImTextureRef tex_ref, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4, uint col = 4294967295)
		{
			ImguiNative.ImDrawList_AddImageQuad(self, tex_ref, p1, p2, p3, p4, uv1, uv2, uv3, uv4, col);
		}

		public void AddImageRounded(ImTextureRef tex_ref, Vector2 p_min, Vector2 p_max, Vector2 uv_min, Vector2 uv_max, uint col, float rounding, ImDrawFlags flags = 0)
		{
			ImguiNative.ImDrawList_AddImageRounded(self, tex_ref, p_min, p_max, uv_min, uv_max, col, rounding, flags);
		}

		public void AddLine(Vector2 p1, Vector2 p2, uint col, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_AddLine(self, p1, p2, col, thickness);
		}

		public void AddNgon(Vector2 center, float radius, uint col, int num_segments, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_AddNgon(self, center, radius, col, num_segments, thickness);
		}

		public void AddNgonFilled(Vector2 center, float radius, uint col, int num_segments)
		{
			ImguiNative.ImDrawList_AddNgonFilled(self, center, radius, col, num_segments);
		}

		public void AddPolyline(Vector2* points, int num_points, uint col, ImDrawFlags flags, float thickness)
		{
			ImguiNative.ImDrawList_AddPolyline(self, points, num_points, col, flags, thickness);
		}

		public void AddQuad(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, uint col, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_AddQuad(self, p1, p2, p3, p4, col, thickness);
		}

		public void AddQuadFilled(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, uint col)
		{
			ImguiNative.ImDrawList_AddQuadFilled(self, p1, p2, p3, p4, col);
		}

		public void AddRect(Vector2 p_min, Vector2 p_max, uint col, float rounding = 0.0f, ImDrawFlags flags = 0, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_AddRect(self, p_min, p_max, col, rounding, flags, thickness);
		}

		public void AddRectFilled(Vector2 p_min, Vector2 p_max, uint col, float rounding = 0.0f, ImDrawFlags flags = 0)
		{
			ImguiNative.ImDrawList_AddRectFilled(self, p_min, p_max, col, rounding, flags);
		}

		public void AddRectFilledMultiColor(Vector2 p_min, Vector2 p_max, uint col_upr_left, uint col_upr_right, uint col_bot_right, uint col_bot_left)
		{
			ImguiNative.ImDrawList_AddRectFilledMultiColor(self, p_min, p_max, col_upr_left, col_upr_right, col_bot_right, col_bot_left);
		}

		public void AddText_Vec2(Vector2 pos, uint col, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end = null)
		{
			ImguiNative.ImDrawList_AddText_Vec2(self, pos, col, text_begin, text_end);
		}

		public void AddText_FontPtr(ImFont* font, float font_size, Vector2 pos, uint col, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end = null, float wrap_width = 0.0f, Vector4* cpu_fine_clip_rect = null)
		{
			ImguiNative.ImDrawList_AddText_FontPtr(self, font, font_size, pos, col, text_begin, text_end, wrap_width, cpu_fine_clip_rect);
		}

		public void AddTriangle(Vector2 p1, Vector2 p2, Vector2 p3, uint col, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_AddTriangle(self, p1, p2, p3, col, thickness);
		}

		public void AddTriangleFilled(Vector2 p1, Vector2 p2, Vector2 p3, uint col)
		{
			ImguiNative.ImDrawList_AddTriangleFilled(self, p1, p2, p3, col);
		}

		public void ChannelsMerge()
		{
			ImguiNative.ImDrawList_ChannelsMerge(self);
		}

		public void ChannelsSetCurrent(int n)
		{
			ImguiNative.ImDrawList_ChannelsSetCurrent(self, n);
		}

		public void ChannelsSplit(int count)
		{
			ImguiNative.ImDrawList_ChannelsSplit(self, count);
		}

		public ImDrawList* CloneOutput()
		{
			return ImguiNative.ImDrawList_CloneOutput(self);
		}

		public Vector2 GetClipRectMax()
		{
			Vector2 pOut;
			ImguiNative.ImDrawList_GetClipRectMax(&pOut, self);

			return pOut;
		}

		public Vector2 GetClipRectMin()
		{
			Vector2 pOut;
			ImguiNative.ImDrawList_GetClipRectMin(&pOut, self);

			return pOut;
		}

		public void PathArcTo(Vector2 center, float radius, float a_min, float a_max, int num_segments = 0)
		{
			ImguiNative.ImDrawList_PathArcTo(self, center, radius, a_min, a_max, num_segments);
		}

		public void PathArcToFast(Vector2 center, float radius, int a_min_of_12, int a_max_of_12)
		{
			ImguiNative.ImDrawList_PathArcToFast(self, center, radius, a_min_of_12, a_max_of_12);
		}

		public void PathBezierCubicCurveTo(Vector2 p2, Vector2 p3, Vector2 p4, int num_segments = 0)
		{
			ImguiNative.ImDrawList_PathBezierCubicCurveTo(self, p2, p3, p4, num_segments);
		}

		public void PathBezierQuadraticCurveTo(Vector2 p2, Vector2 p3, int num_segments = 0)
		{
			ImguiNative.ImDrawList_PathBezierQuadraticCurveTo(self, p2, p3, num_segments);
		}

		public void PathClear()
		{
			ImguiNative.ImDrawList_PathClear(self);
		}

		public void PathEllipticalArcTo(Vector2 center, Vector2 radius, float rot, float a_min, float a_max, int num_segments = 0)
		{
			ImguiNative.ImDrawList_PathEllipticalArcTo(self, center, radius, rot, a_min, a_max, num_segments);
		}

		public void PathFillConcave(uint col)
		{
			ImguiNative.ImDrawList_PathFillConcave(self, col);
		}

		public void PathFillConvex(uint col)
		{
			ImguiNative.ImDrawList_PathFillConvex(self, col);
		}

		public void PathLineTo(Vector2 pos)
		{
			ImguiNative.ImDrawList_PathLineTo(self, pos);
		}

		public void PathLineToMergeDuplicate(Vector2 pos)
		{
			ImguiNative.ImDrawList_PathLineToMergeDuplicate(self, pos);
		}

		public void PathRect(Vector2 rect_min, Vector2 rect_max, float rounding = 0.0f, ImDrawFlags flags = 0)
		{
			ImguiNative.ImDrawList_PathRect(self, rect_min, rect_max, rounding, flags);
		}

		public void PathStroke(uint col, ImDrawFlags flags = 0, float thickness = 1.0f)
		{
			ImguiNative.ImDrawList_PathStroke(self, col, flags, thickness);
		}

		public void PopClipRect()
		{
			ImguiNative.ImDrawList_PopClipRect(self);
		}

		public void PopTexture()
		{
			ImguiNative.ImDrawList_PopTexture(self);
		}

		public void PrimQuadUV(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 uv_a, Vector2 uv_b, Vector2 uv_c, Vector2 uv_d, uint col)
		{
			ImguiNative.ImDrawList_PrimQuadUV(self, a, b, c, d, uv_a, uv_b, uv_c, uv_d, col);
		}

		public void PrimRect(Vector2 a, Vector2 b, uint col)
		{
			ImguiNative.ImDrawList_PrimRect(self, a, b, col);
		}

		public void PrimRectUV(Vector2 a, Vector2 b, Vector2 uv_a, Vector2 uv_b, uint col)
		{
			ImguiNative.ImDrawList_PrimRectUV(self, a, b, uv_a, uv_b, col);
		}

		public void PrimReserve(int idx_count, int vtx_count)
		{
			ImguiNative.ImDrawList_PrimReserve(self, idx_count, vtx_count);
		}

		public void PrimUnreserve(int idx_count, int vtx_count)
		{
			ImguiNative.ImDrawList_PrimUnreserve(self, idx_count, vtx_count);
		}

		public void PrimVtx(Vector2 pos, Vector2 uv, uint col)
		{
			ImguiNative.ImDrawList_PrimVtx(self, pos, uv, col);
		}

		public void PrimWriteIdx(ushort idx)
		{
			ImguiNative.ImDrawList_PrimWriteIdx(self, idx);
		}

		public void PrimWriteVtx(Vector2 pos, Vector2 uv, uint col)
		{
			ImguiNative.ImDrawList_PrimWriteVtx(self, pos, uv, col);
		}

		public void PushClipRect(Vector2 clip_rect_min, Vector2 clip_rect_max, [MarshalAs(UnmanagedType.I1)] bool intersect_with_current_clip_rect = true)
		{
			ImguiNative.ImDrawList_PushClipRect(self, clip_rect_min, clip_rect_max, intersect_with_current_clip_rect);
		}

		public void PushClipRectFullScreen()
		{
			ImguiNative.ImDrawList_PushClipRectFullScreen(self);
		}

		public void PushTexture(ImTextureRef tex_ref)
		{
			ImguiNative.ImDrawList_PushTexture(self, tex_ref);
		}

		public int _CalcCircleAutoSegmentCount(float radius)
		{
			return ImguiNative.ImDrawList__CalcCircleAutoSegmentCount(self, radius);
		}

		public void _ClearFreeMemory()
		{
			ImguiNative.ImDrawList__ClearFreeMemory(self);
		}

		public void _OnChangedClipRect()
		{
			ImguiNative.ImDrawList__OnChangedClipRect(self);
		}

		public void _OnChangedTexture()
		{
			ImguiNative.ImDrawList__OnChangedTexture(self);
		}

		public void _OnChangedVtxOffset()
		{
			ImguiNative.ImDrawList__OnChangedVtxOffset(self);
		}

		public void _PathArcToFastEx(Vector2 center, float radius, int a_min_sample, int a_max_sample, int a_step)
		{
			ImguiNative.ImDrawList__PathArcToFastEx(self, center, radius, a_min_sample, a_max_sample, a_step);
		}

		public void _PathArcToN(Vector2 center, float radius, float a_min, float a_max, int num_segments)
		{
			ImguiNative.ImDrawList__PathArcToN(self, center, radius, a_min, a_max, num_segments);
		}

		public void _PopUnusedDrawCmd()
		{
			ImguiNative.ImDrawList__PopUnusedDrawCmd(self);
		}

		public void _ResetForNewFrame()
		{
			ImguiNative.ImDrawList__ResetForNewFrame(self);
		}

		public void _SetDrawListSharedData(IntPtr data)
		{
			ImguiNative.ImDrawList__SetDrawListSharedData(self, data);
		}

		public void _SetTexture(ImTextureRef tex_ref)
		{
			ImguiNative.ImDrawList__SetTexture(self, tex_ref);
		}

		public void _TryMergeDrawCmds()
		{
			ImguiNative.ImDrawList__TryMergeDrawCmds(self);
		}

		public ImDrawList* self => (ImDrawList*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImDrawListSplitter
	{
		public int _Current;
		public int _Count;
		public ImVector _Channels;

		public void Clear()
		{
			ImguiNative.ImDrawListSplitter_Clear(self);
		}

		public void ClearFreeMemory()
		{
			ImguiNative.ImDrawListSplitter_ClearFreeMemory(self);
		}

		public void Merge(ImDrawList* draw_list)
		{
			ImguiNative.ImDrawListSplitter_Merge(self, draw_list);
		}

		public void SetCurrentChannel(ImDrawList* draw_list, int channel_idx)
		{
			ImguiNative.ImDrawListSplitter_SetCurrentChannel(self, draw_list, channel_idx);
		}

		public void Split(ImDrawList* draw_list, int count)
		{
			ImguiNative.ImDrawListSplitter_Split(self, draw_list, count);
		}

		public ImDrawListSplitter* self => (ImDrawListSplitter*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImDrawVert
	{
		public Vector2 pos;
		public Vector2 uv;
		public uint col;
	}

	public unsafe partial struct ImFont
	{
		public ImFontBaked* LastBaked;
		public ImFontAtlas* ContainerAtlas;
		public ImFontFlags Flags;
		public float CurrentRasterizerDensity;
		public uint FontId;
		public float LegacySize;
		public ImVector_ImFontConfigPtr Sources;
		public ushort EllipsisChar;
		public ushort FallbackChar;
		public fixed byte Used8kPagesMap[1];
		public byte EllipsisAutoBake;
		public ImGuiStorage RemapPairs;

		public void AddRemapChar(ushort from_codepoint, ushort to_codepoint)
		{
			ImguiNative.ImFont_AddRemapChar(self, from_codepoint, to_codepoint);
		}

		public Vector2 CalcTextSizeA(float size, float max_width, float wrap_width, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end = null, char** remaining = null)
		{
			Vector2 pOut;
			ImguiNative.ImFont_CalcTextSizeA(&pOut, self, size, max_width, wrap_width, text_begin, text_end, remaining);

			return pOut;
		}

		public string CalcWordWrapPosition(float size, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end, float wrap_width)
		{
			return ImguiNative.ImFont_CalcWordWrapPosition(self, size, text, text_end, wrap_width);
		}

		public void ClearOutputData()
		{
			ImguiNative.ImFont_ClearOutputData(self);
		}

		public string GetDebugName()
		{
			return ImguiNative.ImFont_GetDebugName(self);
		}

		public ImFontBaked* GetFontBaked(float font_size, float density = -1.0f)
		{
			return ImguiNative.ImFont_GetFontBaked(self, font_size, density);
		}

		public bool IsGlyphInFont(ushort c)
		{
			return ImguiNative.ImFont_IsGlyphInFont(self, c);
		}

		public bool IsGlyphRangeUnused(uint c_begin, uint c_last)
		{
			return ImguiNative.ImFont_IsGlyphRangeUnused(self, c_begin, c_last);
		}

		public bool IsLoaded()
		{
			return ImguiNative.ImFont_IsLoaded(self);
		}

		public void RenderChar(ImDrawList* draw_list, float size, Vector2 pos, uint col, ushort c, Vector4* cpu_fine_clip = null)
		{
			ImguiNative.ImFont_RenderChar(self, draw_list, size, pos, col, c, cpu_fine_clip);
		}

		public void RenderText(ImDrawList* draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end, float wrap_width = 0.0f, [MarshalAs(UnmanagedType.I1)] bool cpu_fine_clip = true)
		{
			ImguiNative.ImFont_RenderText(self, draw_list, size, pos, col, clip_rect, text_begin, text_end, wrap_width, cpu_fine_clip);
		}

		public ImFont* self => (ImFont*)Unsafe.AsPointer(ref this);

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
		public ImVector_ImTextureDataPtr TexList;
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
		public ImVector_ImDrawListSharedDataPtr DrawListSharedDatas;
		public ImFontAtlasBuilder* Builder;
		public ImFontLoader* FontLoader;
		public byte* FontLoaderName;
		public void* FontLoaderData;
		public uint FontLoaderFlags;
		public int RefCount;
		public IntPtr OwnerContext;

		public ImFontAtlasRectId AddCustomRect(int width, int height, ImFontAtlasRect* out_r = null)
		{
			return ImguiNative.ImFontAtlas_AddCustomRect(self, width, height, out_r);
		}

		public ImFont* AddFont(ImFontConfig* font_cfg)
		{
			return ImguiNative.ImFontAtlas_AddFont(self, font_cfg);
		}

		public ImFont* AddFontDefault(ImFontConfig* font_cfg = null)
		{
			return ImguiNative.ImFontAtlas_AddFontDefault(self, font_cfg);
		}

		public ImFont* AddFontFromFileTTF([MarshalAs(UnmanagedType.LPUTF8Str)] string filename, float size_pixels = 0.0f, ImFontConfig* font_cfg = null, ushort* glyph_ranges = null)
		{
			return ImguiNative.ImFontAtlas_AddFontFromFileTTF(self, filename, size_pixels, font_cfg, glyph_ranges);
		}

		public ImFont* AddFontFromMemoryCompressedBase85TTF([MarshalAs(UnmanagedType.LPUTF8Str)] string compressed_font_data_base85, float size_pixels = 0.0f, ImFontConfig* font_cfg = null, ushort* glyph_ranges = null)
		{
			return ImguiNative.ImFontAtlas_AddFontFromMemoryCompressedBase85TTF(self, compressed_font_data_base85, size_pixels, font_cfg, glyph_ranges);
		}

		public ImFont* AddFontFromMemoryCompressedTTF(void* compressed_font_data, int compressed_font_data_size, float size_pixels = 0.0f, ImFontConfig* font_cfg = null, ushort* glyph_ranges = null)
		{
			return ImguiNative.ImFontAtlas_AddFontFromMemoryCompressedTTF(self, compressed_font_data, compressed_font_data_size, size_pixels, font_cfg, glyph_ranges);
		}

		public ImFont* AddFontFromMemoryTTF(void* font_data, int font_data_size, float size_pixels = 0.0f, ImFontConfig* font_cfg = null, ushort* glyph_ranges = null)
		{
			return ImguiNative.ImFontAtlas_AddFontFromMemoryTTF(self, font_data, font_data_size, size_pixels, font_cfg, glyph_ranges);
		}

		public void Clear()
		{
			ImguiNative.ImFontAtlas_Clear(self);
		}

		public void ClearFonts()
		{
			ImguiNative.ImFontAtlas_ClearFonts(self);
		}

		public void ClearInputData()
		{
			ImguiNative.ImFontAtlas_ClearInputData(self);
		}

		public void ClearTexData()
		{
			ImguiNative.ImFontAtlas_ClearTexData(self);
		}

		public void CompactCache()
		{
			ImguiNative.ImFontAtlas_CompactCache(self);
		}

		public bool GetCustomRect(ImFontAtlasRectId id, ImFontAtlasRect* out_r)
		{
			return ImguiNative.ImFontAtlas_GetCustomRect(self, id, out_r);
		}

		public ushort* GetGlyphRangesDefault()
		{
			return ImguiNative.ImFontAtlas_GetGlyphRangesDefault(self);
		}

		public void RemoveCustomRect(ImFontAtlasRectId id)
		{
			ImguiNative.ImFontAtlas_RemoveCustomRect(self, id);
		}

		public void RemoveFont(ImFont* font)
		{
			ImguiNative.ImFontAtlas_RemoveFont(self, font);
		}

		public void SetFontLoader(ImFontLoader* font_loader)
		{
			ImguiNative.ImFontAtlas_SetFontLoader(self, font_loader);
		}

		public ImFontAtlas* self => (ImFontAtlas*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImFontAtlasRect
	{
		public ushort x;
		public ushort y;
		public ushort w;
		public ushort h;
		public Vector2 uv0;
		public Vector2 uv1;
	}

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

		public void ClearOutputData()
		{
			ImguiNative.ImFontBaked_ClearOutputData(self);
		}

		public ImFontGlyph* FindGlyph(ushort c)
		{
			return ImguiNative.ImFontBaked_FindGlyph(self, c);
		}

		public ImFontGlyph* FindGlyphNoFallback(ushort c)
		{
			return ImguiNative.ImFontBaked_FindGlyphNoFallback(self, c);
		}

		public float GetCharAdvance(ushort c)
		{
			return ImguiNative.ImFontBaked_GetCharAdvance(self, c);
		}

		public bool IsGlyphLoaded(ushort c)
		{
			return ImguiNative.ImFontBaked_IsGlyphLoaded(self, c);
		}

		public ImFontBaked* self => (ImFontBaked*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImFontConfig
	{
		public fixed byte Name[40];
		public void* FontData;
		public int FontDataSize;
		public byte FontDataOwnedByAtlas;
		public byte MergeMode;
		public byte PixelSnapH;
		public byte PixelSnapV;
		public sbyte OversampleH;
		public sbyte OversampleV;
		public ushort EllipsisChar;
		public float SizePixels;
		public ushort* GlyphRanges;
		public ushort* GlyphExcludeRanges;
		public Vector2 GlyphOffset;
		public float GlyphMinAdvanceX;
		public float GlyphMaxAdvanceX;
		public float GlyphExtraAdvanceX;
		public uint FontNo;
		public uint FontLoaderFlags;
		public float RasterizerMultiply;
		public float RasterizerDensity;
		public ImFontFlags Flags;
		public ImFont* DstFont;
		public ImFontLoader* FontLoader;
		public void* FontLoaderData;
	}

	public unsafe partial struct ImFontGlyph
	{
		public uint Colored;
		public uint Visible;
		public uint SourceIdx;
		public uint Codepoint;
		public float AdvanceX;
		public float X0;
		public float Y0;
		public float X1;
		public float Y1;
		public float U0;
		public float V0;
		public float U1;
		public float V1;
		public int PackId;
	}

	public unsafe partial struct ImFontGlyphRangesBuilder
	{
		public ImVector UsedChars;

		public void AddChar(ushort c)
		{
			ImguiNative.ImFontGlyphRangesBuilder_AddChar(self, c);
		}

		public void AddRanges(ushort* ranges)
		{
			ImguiNative.ImFontGlyphRangesBuilder_AddRanges(self, ranges);
		}

		public void AddText([MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end = null)
		{
			ImguiNative.ImFontGlyphRangesBuilder_AddText(self, text, text_end);
		}

		public void BuildRanges(ImVector* out_ranges)
		{
			ImguiNative.ImFontGlyphRangesBuilder_BuildRanges(self, out_ranges);
		}

		public void Clear()
		{
			ImguiNative.ImFontGlyphRangesBuilder_Clear(self);
		}

		public bool GetBit(uint n)
		{
			return ImguiNative.ImFontGlyphRangesBuilder_GetBit(self, n);
		}

		public void SetBit(uint n)
		{
			ImguiNative.ImFontGlyphRangesBuilder_SetBit(self, n);
		}

		public ImFontGlyphRangesBuilder* self => (ImFontGlyphRangesBuilder*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiIO
	{
		public ImGuiConfigFlags ConfigFlags;
		public ImGuiBackendFlags BackendFlags;
		public Vector2 DisplaySize;
		public Vector2 DisplayFramebufferScale;
		public float DeltaTime;
		public float IniSavingRate;
		public byte* IniFilename;
		public byte* LogFilename;
		public void* UserData;
		public ImFontAtlas* Fonts;
		public ImFont* FontDefault;
		public byte FontAllowUserScaling;
		public byte ConfigNavSwapGamepadButtons;
		public byte ConfigNavMoveSetMousePos;
		public byte ConfigNavCaptureKeyboard;
		public byte ConfigNavEscapeClearFocusItem;
		public byte ConfigNavEscapeClearFocusWindow;
		public byte ConfigNavCursorVisibleAuto;
		public byte ConfigNavCursorVisibleAlways;
		public byte MouseDrawCursor;
		public byte ConfigMacOSXBehaviors;
		public byte ConfigInputTrickleEventQueue;
		public byte ConfigInputTextCursorBlink;
		public byte ConfigInputTextEnterKeepActive;
		public byte ConfigDragClickToInputText;
		public byte ConfigWindowsResizeFromEdges;
		public byte ConfigWindowsMoveFromTitleBarOnly;
		public byte ConfigWindowsCopyContentsWithCtrlC;
		public byte ConfigScrollbarScrollByPage;
		public float ConfigMemoryCompactTimer;
		public float MouseDoubleClickTime;
		public float MouseDoubleClickMaxDist;
		public float MouseDragThreshold;
		public float KeyRepeatDelay;
		public float KeyRepeatRate;
		public byte ConfigErrorRecovery;
		public byte ConfigErrorRecoveryEnableAssert;
		public byte ConfigErrorRecoveryEnableDebugLog;
		public byte ConfigErrorRecoveryEnableTooltip;
		public byte ConfigDebugIsDebuggerPresent;
		public byte ConfigDebugHighlightIdConflicts;
		public byte ConfigDebugHighlightIdConflictsShowItemPicker;
		public byte ConfigDebugBeginReturnValueOnce;
		public byte ConfigDebugBeginReturnValueLoop;
		public byte ConfigDebugIgnoreFocusLoss;
		public byte ConfigDebugIniSettings;
		public byte* BackendPlatformName;
		public byte* BackendRendererName;
		public void* BackendPlatformUserData;
		public void* BackendRendererUserData;
		public void* BackendLanguageUserData;
		public byte WantCaptureMouse;
		public byte WantCaptureKeyboard;
		public byte WantTextInput;
		public byte WantSetMousePos;
		public byte WantSaveIniSettings;
		public byte NavActive;
		public byte NavVisible;
		public float Framerate;
		public int MetricsRenderVertices;
		public int MetricsRenderIndices;
		public int MetricsRenderWindows;
		public int MetricsActiveWindows;
		public Vector2 MouseDelta;
		public IntPtr Ctx;
		public Vector2 MousePos;
		public fixed byte MouseDown[5];
		public float MouseWheel;
		public float MouseWheelH;
		public ImGuiMouseSource MouseSource;
		public byte KeyCtrl;
		public byte KeyShift;
		public byte KeyAlt;
		public byte KeySuper;
		public ImGuiKey KeyMods;
		public ImGuiKeyData KeysData_0;
		public ImGuiKeyData KeysData_1;
		public ImGuiKeyData KeysData_2;
		public ImGuiKeyData KeysData_3;
		public ImGuiKeyData KeysData_4;
		public ImGuiKeyData KeysData_5;
		public ImGuiKeyData KeysData_6;
		public ImGuiKeyData KeysData_7;
		public ImGuiKeyData KeysData_8;
		public ImGuiKeyData KeysData_9;
		public ImGuiKeyData KeysData_10;
		public ImGuiKeyData KeysData_11;
		public ImGuiKeyData KeysData_12;
		public ImGuiKeyData KeysData_13;
		public ImGuiKeyData KeysData_14;
		public ImGuiKeyData KeysData_15;
		public ImGuiKeyData KeysData_16;
		public ImGuiKeyData KeysData_17;
		public ImGuiKeyData KeysData_18;
		public ImGuiKeyData KeysData_19;
		public ImGuiKeyData KeysData_20;
		public ImGuiKeyData KeysData_21;
		public ImGuiKeyData KeysData_22;
		public ImGuiKeyData KeysData_23;
		public ImGuiKeyData KeysData_24;
		public ImGuiKeyData KeysData_25;
		public ImGuiKeyData KeysData_26;
		public ImGuiKeyData KeysData_27;
		public ImGuiKeyData KeysData_28;
		public ImGuiKeyData KeysData_29;
		public ImGuiKeyData KeysData_30;
		public ImGuiKeyData KeysData_31;
		public ImGuiKeyData KeysData_32;
		public ImGuiKeyData KeysData_33;
		public ImGuiKeyData KeysData_34;
		public ImGuiKeyData KeysData_35;
		public ImGuiKeyData KeysData_36;
		public ImGuiKeyData KeysData_37;
		public ImGuiKeyData KeysData_38;
		public ImGuiKeyData KeysData_39;
		public ImGuiKeyData KeysData_40;
		public ImGuiKeyData KeysData_41;
		public ImGuiKeyData KeysData_42;
		public ImGuiKeyData KeysData_43;
		public ImGuiKeyData KeysData_44;
		public ImGuiKeyData KeysData_45;
		public ImGuiKeyData KeysData_46;
		public ImGuiKeyData KeysData_47;
		public ImGuiKeyData KeysData_48;
		public ImGuiKeyData KeysData_49;
		public ImGuiKeyData KeysData_50;
		public ImGuiKeyData KeysData_51;
		public ImGuiKeyData KeysData_52;
		public ImGuiKeyData KeysData_53;
		public ImGuiKeyData KeysData_54;
		public ImGuiKeyData KeysData_55;
		public ImGuiKeyData KeysData_56;
		public ImGuiKeyData KeysData_57;
		public ImGuiKeyData KeysData_58;
		public ImGuiKeyData KeysData_59;
		public ImGuiKeyData KeysData_60;
		public ImGuiKeyData KeysData_61;
		public ImGuiKeyData KeysData_62;
		public ImGuiKeyData KeysData_63;
		public ImGuiKeyData KeysData_64;
		public ImGuiKeyData KeysData_65;
		public ImGuiKeyData KeysData_66;
		public ImGuiKeyData KeysData_67;
		public ImGuiKeyData KeysData_68;
		public ImGuiKeyData KeysData_69;
		public ImGuiKeyData KeysData_70;
		public ImGuiKeyData KeysData_71;
		public ImGuiKeyData KeysData_72;
		public ImGuiKeyData KeysData_73;
		public ImGuiKeyData KeysData_74;
		public ImGuiKeyData KeysData_75;
		public ImGuiKeyData KeysData_76;
		public ImGuiKeyData KeysData_77;
		public ImGuiKeyData KeysData_78;
		public ImGuiKeyData KeysData_79;
		public ImGuiKeyData KeysData_80;
		public ImGuiKeyData KeysData_81;
		public ImGuiKeyData KeysData_82;
		public ImGuiKeyData KeysData_83;
		public ImGuiKeyData KeysData_84;
		public ImGuiKeyData KeysData_85;
		public ImGuiKeyData KeysData_86;
		public ImGuiKeyData KeysData_87;
		public ImGuiKeyData KeysData_88;
		public ImGuiKeyData KeysData_89;
		public ImGuiKeyData KeysData_90;
		public ImGuiKeyData KeysData_91;
		public ImGuiKeyData KeysData_92;
		public ImGuiKeyData KeysData_93;
		public ImGuiKeyData KeysData_94;
		public ImGuiKeyData KeysData_95;
		public ImGuiKeyData KeysData_96;
		public ImGuiKeyData KeysData_97;
		public ImGuiKeyData KeysData_98;
		public ImGuiKeyData KeysData_99;
		public ImGuiKeyData KeysData_100;
		public ImGuiKeyData KeysData_101;
		public ImGuiKeyData KeysData_102;
		public ImGuiKeyData KeysData_103;
		public ImGuiKeyData KeysData_104;
		public ImGuiKeyData KeysData_105;
		public ImGuiKeyData KeysData_106;
		public ImGuiKeyData KeysData_107;
		public ImGuiKeyData KeysData_108;
		public ImGuiKeyData KeysData_109;
		public ImGuiKeyData KeysData_110;
		public ImGuiKeyData KeysData_111;
		public ImGuiKeyData KeysData_112;
		public ImGuiKeyData KeysData_113;
		public ImGuiKeyData KeysData_114;
		public ImGuiKeyData KeysData_115;
		public ImGuiKeyData KeysData_116;
		public ImGuiKeyData KeysData_117;
		public ImGuiKeyData KeysData_118;
		public ImGuiKeyData KeysData_119;
		public ImGuiKeyData KeysData_120;
		public ImGuiKeyData KeysData_121;
		public ImGuiKeyData KeysData_122;
		public ImGuiKeyData KeysData_123;
		public ImGuiKeyData KeysData_124;
		public ImGuiKeyData KeysData_125;
		public ImGuiKeyData KeysData_126;
		public ImGuiKeyData KeysData_127;
		public ImGuiKeyData KeysData_128;
		public ImGuiKeyData KeysData_129;
		public ImGuiKeyData KeysData_130;
		public ImGuiKeyData KeysData_131;
		public ImGuiKeyData KeysData_132;
		public ImGuiKeyData KeysData_133;
		public ImGuiKeyData KeysData_134;
		public ImGuiKeyData KeysData_135;
		public ImGuiKeyData KeysData_136;
		public ImGuiKeyData KeysData_137;
		public ImGuiKeyData KeysData_138;
		public ImGuiKeyData KeysData_139;
		public ImGuiKeyData KeysData_140;
		public ImGuiKeyData KeysData_141;
		public ImGuiKeyData KeysData_142;
		public ImGuiKeyData KeysData_143;
		public ImGuiKeyData KeysData_144;
		public ImGuiKeyData KeysData_145;
		public ImGuiKeyData KeysData_146;
		public ImGuiKeyData KeysData_147;
		public ImGuiKeyData KeysData_148;
		public ImGuiKeyData KeysData_149;
		public ImGuiKeyData KeysData_150;
		public ImGuiKeyData KeysData_151;
		public ImGuiKeyData KeysData_152;
		public ImGuiKeyData KeysData_153;
		public ImGuiKeyData KeysData_154;
		public byte WantCaptureMouseUnlessPopupClose;
		public Vector2 MousePosPrev;
		public Vector2 MouseClickedPos_0;
		public Vector2 MouseClickedPos_1;
		public Vector2 MouseClickedPos_2;
		public Vector2 MouseClickedPos_3;
		public Vector2 MouseClickedPos_4;
		public fixed double MouseClickedTime[5];
		public fixed byte MouseClicked[5];
		public fixed byte MouseDoubleClicked[5];
		public fixed ushort MouseClickedCount[5];
		public fixed ushort MouseClickedLastCount[5];
		public fixed byte MouseReleased[5];
		public fixed double MouseReleasedTime[5];
		public fixed byte MouseDownOwned[5];
		public fixed byte MouseDownOwnedUnlessPopupClose[5];
		public byte MouseWheelRequestAxisSwap;
		public byte MouseCtrlLeftAsRightClick;
		public fixed float MouseDownDuration[5];
		public fixed float MouseDownDurationPrev[5];
		public fixed float MouseDragMaxDistanceSqr[5];
		public float PenPressure;
		public byte AppFocusLost;
		public byte AppAcceptingEvents;
		public ushort InputQueueSurrogate;
		public ImVector InputQueueCharacters;

		public void AddFocusEvent([MarshalAs(UnmanagedType.I1)] bool focused)
		{
			ImguiNative.ImGuiIO_AddFocusEvent(self, focused);
		}

		public void AddInputCharacter(uint c)
		{
			ImguiNative.ImGuiIO_AddInputCharacter(self, c);
		}

		public void AddInputCharacterUTF16(ushort c)
		{
			ImguiNative.ImGuiIO_AddInputCharacterUTF16(self, c);
		}

		public void AddInputCharactersUTF8([MarshalAs(UnmanagedType.LPUTF8Str)] string str)
		{
			ImguiNative.ImGuiIO_AddInputCharactersUTF8(self, str);
		}

		public void AddKeyAnalogEvent(ImGuiKey key, [MarshalAs(UnmanagedType.I1)] bool down, float v)
		{
			ImguiNative.ImGuiIO_AddKeyAnalogEvent(self, key, down, v);
		}

		public void AddKeyEvent(ImGuiKey key, [MarshalAs(UnmanagedType.I1)] bool down)
		{
			ImguiNative.ImGuiIO_AddKeyEvent(self, key, down);
		}

		public void AddMouseButtonEvent(int button, [MarshalAs(UnmanagedType.I1)] bool down)
		{
			ImguiNative.ImGuiIO_AddMouseButtonEvent(self, button, down);
		}

		public void AddMousePosEvent(float x, float y)
		{
			ImguiNative.ImGuiIO_AddMousePosEvent(self, x, y);
		}

		public void AddMouseSourceEvent(ImGuiMouseSource source)
		{
			ImguiNative.ImGuiIO_AddMouseSourceEvent(self, source);
		}

		public void AddMouseWheelEvent(float wheel_x, float wheel_y)
		{
			ImguiNative.ImGuiIO_AddMouseWheelEvent(self, wheel_x, wheel_y);
		}

		public void ClearEventsQueue()
		{
			ImguiNative.ImGuiIO_ClearEventsQueue(self);
		}

		public void ClearInputKeys()
		{
			ImguiNative.ImGuiIO_ClearInputKeys(self);
		}

		public void ClearInputMouse()
		{
			ImguiNative.ImGuiIO_ClearInputMouse(self);
		}

		public void SetAppAcceptingEvents([MarshalAs(UnmanagedType.I1)] bool accepting_events)
		{
			ImguiNative.ImGuiIO_SetAppAcceptingEvents(self, accepting_events);
		}

		public void SetKeyEventNativeData(ImGuiKey key, int native_keycode, int native_scancode, int native_legacy_index = -1)
		{
			ImguiNative.ImGuiIO_SetKeyEventNativeData(self, key, native_keycode, native_scancode, native_legacy_index);
		}

		public ImGuiIO* self => (ImGuiIO*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiInputTextCallbackData
	{
		public IntPtr Ctx;
		public ImGuiInputTextFlags EventFlag;
		public ImGuiInputTextFlags Flags;
		public void* UserData;
		public ushort EventChar;
		public ImGuiKey EventKey;
		public byte* Buf;
		public int BufTextLen;
		public int BufSize;
		public byte BufDirty;
		public int CursorPos;
		public int SelectionStart;
		public int SelectionEnd;

		public void ClearSelection()
		{
			ImguiNative.ImGuiInputTextCallbackData_ClearSelection(self);
		}

		public void DeleteChars(int pos, int bytes_count)
		{
			ImguiNative.ImGuiInputTextCallbackData_DeleteChars(self, pos, bytes_count);
		}

		public bool HasSelection()
		{
			return ImguiNative.ImGuiInputTextCallbackData_HasSelection(self);
		}

		public void InsertChars(int pos, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end = null)
		{
			ImguiNative.ImGuiInputTextCallbackData_InsertChars(self, pos, text, text_end);
		}

		public void SelectAll()
		{
			ImguiNative.ImGuiInputTextCallbackData_SelectAll(self);
		}

		public ImGuiInputTextCallbackData* self => (ImGuiInputTextCallbackData*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiKeyData
	{
		public byte Down;
		public float DownDuration;
		public float DownDurationPrev;
		public float AnalogValue;
	}

	public unsafe partial struct ImGuiListClipper
	{
		public IntPtr Ctx;
		public int DisplayStart;
		public int DisplayEnd;
		public int ItemsCount;
		public float ItemsHeight;
		public double StartPosY;
		public double StartSeekOffsetY;
		public void* TempData;

		public void Begin(int items_count, float items_height = -1.0f)
		{
			ImguiNative.ImGuiListClipper_Begin(self, items_count, items_height);
		}

		public void End()
		{
			ImguiNative.ImGuiListClipper_End(self);
		}

		public void IncludeItemByIndex(int item_index)
		{
			ImguiNative.ImGuiListClipper_IncludeItemByIndex(self, item_index);
		}

		public void IncludeItemsByIndex(int item_begin, int item_end)
		{
			ImguiNative.ImGuiListClipper_IncludeItemsByIndex(self, item_begin, item_end);
		}

		public void SeekCursorForItem(int item_index)
		{
			ImguiNative.ImGuiListClipper_SeekCursorForItem(self, item_index);
		}

		public bool Step()
		{
			return ImguiNative.ImGuiListClipper_Step(self);
		}

		public ImGuiListClipper* self => (ImGuiListClipper*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiMultiSelectIO
	{
		public ImVector Requests;
		public long RangeSrcItem;
		public long NavIdItem;
		public byte NavIdSelected;
		public byte RangeSrcReset;
		public int ItemsCount;
	}

	public unsafe partial struct ImGuiOnceUponAFrame
	{
		public int RefFrame;
	}

	public unsafe partial struct ImGuiPayload
	{
		public void* Data;
		public int DataSize;
		public uint SourceId;
		public uint SourceParentId;
		public int DataFrameCount;
		public fixed byte DataType[33];
		public byte Preview;
		public byte Delivery;

		public void Clear()
		{
			ImguiNative.ImGuiPayload_Clear(self);
		}

		public bool IsDataType([MarshalAs(UnmanagedType.LPUTF8Str)] string type)
		{
			return ImguiNative.ImGuiPayload_IsDataType(self, type);
		}

		public bool IsDelivery()
		{
			return ImguiNative.ImGuiPayload_IsDelivery(self);
		}

		public bool IsPreview()
		{
			return ImguiNative.ImGuiPayload_IsPreview(self);
		}

		public ImGuiPayload* self => (ImGuiPayload*)Unsafe.AsPointer(ref this);

	}

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
		public ImVector_ImTextureDataPtr Textures;
	}

	public unsafe partial struct ImGuiPlatformImeData
	{
		public byte WantVisible;
		public byte WantTextInput;
		public Vector2 InputPos;
		public float InputLineHeight;
		public uint ViewportId;
	}

	public unsafe partial struct ImGuiSelectionBasicStorage
	{
		public int Size;
		public byte PreserveOrder;
		public void* UserData;
		public IntPtr AdapterIndexToStorageId;
		public int _SelectionOrder;
		public ImGuiStorage _Storage;

		public void ApplyRequests(ImGuiMultiSelectIO* ms_io)
		{
			ImguiNative.ImGuiSelectionBasicStorage_ApplyRequests(self, ms_io);
		}

		public void Clear()
		{
			ImguiNative.ImGuiSelectionBasicStorage_Clear(self);
		}

		public bool Contains(uint id)
		{
			return ImguiNative.ImGuiSelectionBasicStorage_Contains(self, id);
		}

		public bool GetNextSelectedItem(void** opaque_it, uint* out_id)
		{
			return ImguiNative.ImGuiSelectionBasicStorage_GetNextSelectedItem(self, opaque_it, out_id);
		}

		public uint GetStorageIdFromIndex(int idx)
		{
			return ImguiNative.ImGuiSelectionBasicStorage_GetStorageIdFromIndex(self, idx);
		}

		public void SetItemSelected(uint id, [MarshalAs(UnmanagedType.I1)] bool selected)
		{
			ImguiNative.ImGuiSelectionBasicStorage_SetItemSelected(self, id, selected);
		}

		public void Swap(ImGuiSelectionBasicStorage* r)
		{
			ImguiNative.ImGuiSelectionBasicStorage_Swap(self, r);
		}

		public ImGuiSelectionBasicStorage* self => (ImGuiSelectionBasicStorage*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiSelectionExternalStorage
	{
		public void* UserData;
		public IntPtr AdapterSetItemSelected;

		public void ApplyRequests(ImGuiMultiSelectIO* ms_io)
		{
			ImguiNative.ImGuiSelectionExternalStorage_ApplyRequests(self, ms_io);
		}

		public ImGuiSelectionExternalStorage* self => (ImGuiSelectionExternalStorage*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiSelectionRequest
	{
		public ImGuiSelectionRequestType Type;
		public byte Selected;
		public sbyte RangeDirection;
		public long RangeFirstItem;
		public long RangeLastItem;
	}

	public unsafe partial struct ImGuiSizeCallbackData
	{
		public void* UserData;
		public Vector2 Pos;
		public Vector2 CurrentSize;
		public Vector2 DesiredSize;
	}

	public unsafe partial struct ImGuiStorage
	{
		public ImVector Data;

		public void BuildSortByKey()
		{
			ImguiNative.ImGuiStorage_BuildSortByKey(self);
		}

		public void Clear()
		{
			ImguiNative.ImGuiStorage_Clear(self);
		}

		public bool GetBool(uint key, [MarshalAs(UnmanagedType.I1)] bool default_val = true)
		{
			return ImguiNative.ImGuiStorage_GetBool(self, key, default_val);
		}

		public byte* GetBoolRef(uint key, [MarshalAs(UnmanagedType.I1)] bool default_val = true)
		{
			return ImguiNative.ImGuiStorage_GetBoolRef(self, key, default_val);
		}

		public float GetFloat(uint key, float default_val = 0.0f)
		{
			return ImguiNative.ImGuiStorage_GetFloat(self, key, default_val);
		}

		public float* GetFloatRef(uint key, float default_val = 0.0f)
		{
			return ImguiNative.ImGuiStorage_GetFloatRef(self, key, default_val);
		}

		public int GetInt(uint key, int default_val = 0)
		{
			return ImguiNative.ImGuiStorage_GetInt(self, key, default_val);
		}

		public int* GetIntRef(uint key, int default_val = 0)
		{
			return ImguiNative.ImGuiStorage_GetIntRef(self, key, default_val);
		}

		public void* GetVoidPtr(uint key)
		{
			return ImguiNative.ImGuiStorage_GetVoidPtr(self, key);
		}

		public void** GetVoidPtrRef(uint key, void* default_val = null)
		{
			return ImguiNative.ImGuiStorage_GetVoidPtrRef(self, key, default_val);
		}

		public void SetAllInt(int val)
		{
			ImguiNative.ImGuiStorage_SetAllInt(self, val);
		}

		public void SetBool(uint key, [MarshalAs(UnmanagedType.I1)] bool val)
		{
			ImguiNative.ImGuiStorage_SetBool(self, key, val);
		}

		public void SetFloat(uint key, float val)
		{
			ImguiNative.ImGuiStorage_SetFloat(self, key, val);
		}

		public void SetInt(uint key, int val)
		{
			ImguiNative.ImGuiStorage_SetInt(self, key, val);
		}

		public void SetVoidPtr(uint key, void* val)
		{
			ImguiNative.ImGuiStorage_SetVoidPtr(self, key, val);
		}

		public ImGuiStorage* self => (ImGuiStorage*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiStyle
	{
		public float FontSizeBase;
		public float FontScaleMain;
		public float FontScaleDpi;
		public float Alpha;
		public float DisabledAlpha;
		public Vector2 WindowPadding;
		public float WindowRounding;
		public float WindowBorderSize;
		public float WindowBorderHoverPadding;
		public Vector2 WindowMinSize;
		public Vector2 WindowTitleAlign;
		public ImGuiDir WindowMenuButtonPosition;
		public float ChildRounding;
		public float ChildBorderSize;
		public float PopupRounding;
		public float PopupBorderSize;
		public Vector2 FramePadding;
		public float FrameRounding;
		public float FrameBorderSize;
		public Vector2 ItemSpacing;
		public Vector2 ItemInnerSpacing;
		public Vector2 CellPadding;
		public Vector2 TouchExtraPadding;
		public float IndentSpacing;
		public float ColumnsMinSpacing;
		public float ScrollbarSize;
		public float ScrollbarRounding;
		public float GrabMinSize;
		public float GrabRounding;
		public float LogSliderDeadzone;
		public float ImageBorderSize;
		public float TabRounding;
		public float TabBorderSize;
		public float TabMinWidthBase;
		public float TabMinWidthShrink;
		public float TabCloseButtonMinWidthSelected;
		public float TabCloseButtonMinWidthUnselected;
		public float TabBarBorderSize;
		public float TabBarOverlineSize;
		public float TableAngledHeadersAngle;
		public Vector2 TableAngledHeadersTextAlign;
		public ImGuiTreeNodeFlags TreeLinesFlags;
		public float TreeLinesSize;
		public float TreeLinesRounding;
		public ImGuiDir ColorButtonPosition;
		public Vector2 ButtonTextAlign;
		public Vector2 SelectableTextAlign;
		public float SeparatorTextBorderSize;
		public Vector2 SeparatorTextAlign;
		public Vector2 SeparatorTextPadding;
		public Vector2 DisplayWindowPadding;
		public Vector2 DisplaySafeAreaPadding;
		public float MouseCursorScale;
		public byte AntiAliasedLines;
		public byte AntiAliasedLinesUseTex;
		public byte AntiAliasedFill;
		public float CurveTessellationTol;
		public float CircleTessellationMaxError;
		public Vector4 Colors_0;
		public Vector4 Colors_1;
		public Vector4 Colors_2;
		public Vector4 Colors_3;
		public Vector4 Colors_4;
		public Vector4 Colors_5;
		public Vector4 Colors_6;
		public Vector4 Colors_7;
		public Vector4 Colors_8;
		public Vector4 Colors_9;
		public Vector4 Colors_10;
		public Vector4 Colors_11;
		public Vector4 Colors_12;
		public Vector4 Colors_13;
		public Vector4 Colors_14;
		public Vector4 Colors_15;
		public Vector4 Colors_16;
		public Vector4 Colors_17;
		public Vector4 Colors_18;
		public Vector4 Colors_19;
		public Vector4 Colors_20;
		public Vector4 Colors_21;
		public Vector4 Colors_22;
		public Vector4 Colors_23;
		public Vector4 Colors_24;
		public Vector4 Colors_25;
		public Vector4 Colors_26;
		public Vector4 Colors_27;
		public Vector4 Colors_28;
		public Vector4 Colors_29;
		public Vector4 Colors_30;
		public Vector4 Colors_31;
		public Vector4 Colors_32;
		public Vector4 Colors_33;
		public Vector4 Colors_34;
		public Vector4 Colors_35;
		public Vector4 Colors_36;
		public Vector4 Colors_37;
		public Vector4 Colors_38;
		public Vector4 Colors_39;
		public Vector4 Colors_40;
		public Vector4 Colors_41;
		public Vector4 Colors_42;
		public Vector4 Colors_43;
		public Vector4 Colors_44;
		public Vector4 Colors_45;
		public Vector4 Colors_46;
		public Vector4 Colors_47;
		public Vector4 Colors_48;
		public Vector4 Colors_49;
		public Vector4 Colors_50;
		public Vector4 Colors_51;
		public Vector4 Colors_52;
		public Vector4 Colors_53;
		public Vector4 Colors_54;
		public Vector4 Colors_55;
		public Vector4 Colors_56;
		public Vector4 Colors_57;
		public float HoverStationaryDelay;
		public float HoverDelayShort;
		public float HoverDelayNormal;
		public ImGuiHoveredFlags HoverFlagsForTooltipMouse;
		public ImGuiHoveredFlags HoverFlagsForTooltipNav;
		public float _MainScale;
		public float _NextFrameFontSizeBase;

		public void ScaleAllSizes(float scale_factor)
		{
			ImguiNative.ImGuiStyle_ScaleAllSizes(self, scale_factor);
		}

		public ImGuiStyle* self => (ImGuiStyle*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiTableColumnSortSpecs
	{
		public uint ColumnUserID;
		public short ColumnIndex;
		public short SortOrder;
		public ImGuiSortDirection SortDirection;
	}

	public unsafe partial struct ImGuiTableSortSpecs
	{
		public ImGuiTableColumnSortSpecs* Specs;
		public int SpecsCount;
		public byte SpecsDirty;
	}

	public unsafe partial struct ImGuiTextBuffer
	{
		public ImVector Buf;

		public void append([MarshalAs(UnmanagedType.LPUTF8Str)] string str, [MarshalAs(UnmanagedType.LPUTF8Str)] string str_end = null)
		{
			ImguiNative.ImGuiTextBuffer_append(self, str, str_end);
		}

		public void appendf( char* fmt)
		{
			ImguiNative.ImGuiTextBuffer_appendf(self, fmt);
		}

		public string begin()
		{
			return ImguiNative.ImGuiTextBuffer_begin(self);
		}

		public string c_str()
		{
			return ImguiNative.ImGuiTextBuffer_c_str(self);
		}

		public void clear()
		{
			ImguiNative.ImGuiTextBuffer_clear(self);
		}

		public bool empty()
		{
			return ImguiNative.ImGuiTextBuffer_empty(self);
		}

		public string end()
		{
			return ImguiNative.ImGuiTextBuffer_end(self);
		}

		public void reserve(int capacity)
		{
			ImguiNative.ImGuiTextBuffer_reserve(self, capacity);
		}

		public void resize(int size)
		{
			ImguiNative.ImGuiTextBuffer_resize(self, size);
		}

		public int size()
		{
			return ImguiNative.ImGuiTextBuffer_size(self);
		}

		public ImGuiTextBuffer* self => (ImGuiTextBuffer*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiTextFilter
	{
		public fixed byte InputBuf[256];
		public ImVector Filters;
		public int CountGrep;

		public void Build()
		{
			ImguiNative.ImGuiTextFilter_Build(self);
		}

		public void Clear()
		{
			ImguiNative.ImGuiTextFilter_Clear(self);
		}

		public bool Draw([MarshalAs(UnmanagedType.LPUTF8Str)] string label = "Filter(inc,-exc)", float width = 0.0f)
		{
			return ImguiNative.ImGuiTextFilter_Draw(self, label, width);
		}

		public bool IsActive()
		{
			return ImguiNative.ImGuiTextFilter_IsActive(self);
		}

		public bool PassFilter([MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end = null)
		{
			return ImguiNative.ImGuiTextFilter_PassFilter(self, text, text_end);
		}

		public ImGuiTextFilter* self => (ImGuiTextFilter*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiTextRange
	{
		public byte* b;
		public byte* e;

		public bool empty()
		{
			return ImguiNative.ImGuiTextRange_empty(self);
		}

		public void split(byte separator, ImVector* @out)
		{
			ImguiNative.ImGuiTextRange_split(self, separator, @out);
		}

		public ImGuiTextRange* self => (ImGuiTextRange*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImGuiViewport
	{
		public uint ID;
		public ImGuiViewportFlags Flags;
		public Vector2 Pos;
		public Vector2 Size;
		public Vector2 FramebufferScale;
		public Vector2 WorkPos;
		public Vector2 WorkSize;
		public void* PlatformHandle;
		public void* PlatformHandleRaw;

		public Vector2 GetCenter()
		{
			Vector2 pOut;
			ImguiNative.ImGuiViewport_GetCenter(&pOut, self);

			return pOut;
		}

		public Vector2 GetWorkCenter()
		{
			Vector2 pOut;
			ImguiNative.ImGuiViewport_GetWorkCenter(&pOut, self);

			return pOut;
		}

		public ImGuiViewport* self => (ImGuiViewport*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImTextureData
	{
		public int UniqueID;
		public ImTextureStatus Status;
		public void* BackendUserData;
		public ulong TexID;
		public ImTextureFormat Format;
		public int Width;
		public int Height;
		public int BytesPerPixel;
		public byte* Pixels;
		public ImTextureRect UsedRect;
		public ImTextureRect UpdateRect;
		public ImVector_ImTextureRect Updates;
		public int UnusedFrames;
		public ushort RefCount;
		public byte UseColors;
		public byte WantDestroyNextFrame;

		public void Create(ImTextureFormat format, int w, int h)
		{
			ImguiNative.ImTextureData_Create(self, format, w, h);
		}

		public void DestroyPixels()
		{
			ImguiNative.ImTextureData_DestroyPixels(self);
		}

		public int GetPitch()
		{
			return ImguiNative.ImTextureData_GetPitch(self);
		}

		public void* GetPixels()
		{
			return ImguiNative.ImTextureData_GetPixels(self);
		}

		public void* GetPixelsAt(int x, int y)
		{
			return ImguiNative.ImTextureData_GetPixelsAt(self, x, y);
		}

		public int GetSizeInBytes()
		{
			return ImguiNative.ImTextureData_GetSizeInBytes(self);
		}

		public ulong GetTexID()
		{
			return ImguiNative.ImTextureData_GetTexID(self);
		}

		public ImTextureRef GetTexRef()
		{
			ImTextureRef pOut;
			ImguiNative.ImTextureData_GetTexRef(&pOut, self);

			return pOut;
		}

		public void SetStatus(ImTextureStatus status)
		{
			ImguiNative.ImTextureData_SetStatus(self, status);
		}

		public void SetTexID(ulong tex_id)
		{
			ImguiNative.ImTextureData_SetTexID(self, tex_id);
		}

		public ImTextureData* self => (ImTextureData*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImTextureRect
	{
		public ushort x;
		public ushort y;
		public ushort w;
		public ushort h;
	}

	public unsafe partial struct ImTextureRef
	{
		public ImTextureData* _TexData;
		public ulong _TexID;

		public ulong GetTexID()
		{
			return ImguiNative.ImTextureRef_GetTexID(self);
		}

		public ImTextureRef* self => (ImTextureRef*)Unsafe.AsPointer(ref this);

	}

	public unsafe partial struct ImVec2
	{
		public float x;
		public float y;
	}

	public unsafe partial struct ImVec4
	{
		public float x;
		public float y;
		public float z;
		public float w;
	}

}

