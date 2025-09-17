using System.Numerics;
using Imgui;
using System;
using System.Runtime.InteropServices;

//TBD ELI
using ImFontAtlasRectId = int;

public struct ImFontLoader
{

};


namespace Imgui
{

	public static unsafe partial class ImguiNative
	{

#if __IOS__
		public const string NativeSokolImport = "@rpath/sokol.framework/sokol";
#else
		public const string NativeSokolImport = "sokol";
#endif
		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImColor_HSV(ImColor* pOut, float h, float s, float v, float a);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImColor_SetHSV(ImColor* self, float h, float s, float v, float a);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ulong ImDrawCmd_GetTexID(ImDrawCmd* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawData_AddDrawList(ImDrawData* self, ImDrawList* draw_list);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawData_Clear(ImDrawData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawData_DeIndexAllBuffers(ImDrawData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawData_ScaleClipRects(ImDrawData* self, Vector2 fb_scale);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawListSplitter_Clear(ImDrawListSplitter* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawListSplitter_ClearFreeMemory(ImDrawListSplitter* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawListSplitter_Merge(ImDrawListSplitter* self, ImDrawList* draw_list);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawListSplitter_SetCurrentChannel(ImDrawListSplitter* self, ImDrawList* draw_list, int channel_idx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawListSplitter_Split(ImDrawListSplitter* self, ImDrawList* draw_list, int count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddBezierCubic(ImDrawList* self, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, uint col, float thickness, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddBezierQuadratic(ImDrawList* self, Vector2 p1, Vector2 p2, Vector2 p3, uint col, float thickness, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddCallback(ImDrawList* self, IntPtr callback, void* userdata, uint userdata_size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddCircle(ImDrawList* self, Vector2 center, float radius, uint col, int num_segments, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddCircleFilled(ImDrawList* self, Vector2 center, float radius, uint col, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddConcavePolyFilled(ImDrawList* self, Vector2* points, int num_points, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddConvexPolyFilled(ImDrawList* self, Vector2* points, int num_points, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddDrawCmd(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddEllipse(ImDrawList* self, Vector2 center, Vector2 radius, uint col, float rot, int num_segments, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddEllipseFilled(ImDrawList* self, Vector2 center, Vector2 radius, uint col, float rot, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddImage(ImDrawList* self, ImTextureRef tex_ref, Vector2 p_min, Vector2 p_max, Vector2 uv_min, Vector2 uv_max, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddImageQuad(ImDrawList* self, ImTextureRef tex_ref, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector2 uv4, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddImageRounded(ImDrawList* self, ImTextureRef tex_ref, Vector2 p_min, Vector2 p_max, Vector2 uv_min, Vector2 uv_max, uint col, float rounding, ImDrawFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddLine(ImDrawList* self, Vector2 p1, Vector2 p2, uint col, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddNgon(ImDrawList* self, Vector2 center, float radius, uint col, int num_segments, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddNgonFilled(ImDrawList* self, Vector2 center, float radius, uint col, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddPolyline(ImDrawList* self, Vector2* points, int num_points, uint col, ImDrawFlags flags, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddQuad(ImDrawList* self, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, uint col, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddQuadFilled(ImDrawList* self, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddRect(ImDrawList* self, Vector2 p_min, Vector2 p_max, uint col, float rounding, ImDrawFlags flags, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddRectFilled(ImDrawList* self, Vector2 p_min, Vector2 p_max, uint col, float rounding, ImDrawFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddRectFilledMultiColor(ImDrawList* self, Vector2 p_min, Vector2 p_max, uint col_upr_left, uint col_upr_right, uint col_bot_right, uint col_bot_left);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddText_Vec2(ImDrawList* self, Vector2 pos, uint col, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddText_FontPtr(ImDrawList* self, ImFont* font, float font_size, Vector2 pos, uint col, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end, float wrap_width, Vector4* cpu_fine_clip_rect);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddTriangle(ImDrawList* self, Vector2 p1, Vector2 p2, Vector2 p3, uint col, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_AddTriangleFilled(ImDrawList* self, Vector2 p1, Vector2 p2, Vector2 p3, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_ChannelsMerge(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_ChannelsSetCurrent(ImDrawList* self, int n);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_ChannelsSplit(ImDrawList* self, int count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImDrawList* ImDrawList_CloneOutput(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_GetClipRectMax(Vector2* pOut, ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_GetClipRectMin(Vector2* pOut, ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathArcTo(ImDrawList* self, Vector2 center, float radius, float a_min, float a_max, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathArcToFast(ImDrawList* self, Vector2 center, float radius, int a_min_of_12, int a_max_of_12);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathBezierCubicCurveTo(ImDrawList* self, Vector2 p2, Vector2 p3, Vector2 p4, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathBezierQuadraticCurveTo(ImDrawList* self, Vector2 p2, Vector2 p3, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathClear(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathEllipticalArcTo(ImDrawList* self, Vector2 center, Vector2 radius, float rot, float a_min, float a_max, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathFillConcave(ImDrawList* self, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathFillConvex(ImDrawList* self, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathLineTo(ImDrawList* self, Vector2 pos);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathLineToMergeDuplicate(ImDrawList* self, Vector2 pos);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathRect(ImDrawList* self, Vector2 rect_min, Vector2 rect_max, float rounding, ImDrawFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PathStroke(ImDrawList* self, uint col, ImDrawFlags flags, float thickness);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PopClipRect(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PopTexture(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimQuadUV(ImDrawList* self, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 uv_a, Vector2 uv_b, Vector2 uv_c, Vector2 uv_d, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimRect(ImDrawList* self, Vector2 a, Vector2 b, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimRectUV(ImDrawList* self, Vector2 a, Vector2 b, Vector2 uv_a, Vector2 uv_b, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimReserve(ImDrawList* self, int idx_count, int vtx_count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimUnreserve(ImDrawList* self, int idx_count, int vtx_count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimVtx(ImDrawList* self, Vector2 pos, Vector2 uv, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimWriteIdx(ImDrawList* self, ushort idx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PrimWriteVtx(ImDrawList* self, Vector2 pos, Vector2 uv, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PushClipRect(ImDrawList* self, Vector2 clip_rect_min, Vector2 clip_rect_max, [MarshalAs(UnmanagedType.I1)] bool intersect_with_current_clip_rect);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PushClipRectFullScreen(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList_PushTexture(ImDrawList* self, ImTextureRef tex_ref);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ImDrawList__CalcCircleAutoSegmentCount(ImDrawList* self, float radius);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__ClearFreeMemory(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__OnChangedClipRect(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__OnChangedTexture(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__OnChangedVtxOffset(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__PathArcToFastEx(ImDrawList* self, Vector2 center, float radius, int a_min_sample, int a_max_sample, int a_step);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__PathArcToN(ImDrawList* self, Vector2 center, float radius, float a_min, float a_max, int num_segments);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__PopUnusedDrawCmd(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__ResetForNewFrame(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__SetDrawListSharedData(ImDrawList* self, IntPtr data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__SetTexture(ImDrawList* self, ImTextureRef tex_ref);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImDrawList__TryMergeDrawCmds(ImDrawList* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFontAtlasRectId ImFontAtlas_AddCustomRect(ImFontAtlas* self, int width, int height, ImFontAtlasRect* out_r);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFont* ImFontAtlas_AddFont(ImFontAtlas* self, ImFontConfig* font_cfg);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFont* ImFontAtlas_AddFontDefault(ImFontAtlas* self, ImFontConfig* font_cfg);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFont* ImFontAtlas_AddFontFromFileTTF(ImFontAtlas* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, float size_pixels, ImFontConfig* font_cfg, ushort* glyph_ranges);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFont* ImFontAtlas_AddFontFromMemoryCompressedBase85TTF(ImFontAtlas* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string compressed_font_data_base85, float size_pixels, ImFontConfig* font_cfg, ushort* glyph_ranges);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFont* ImFontAtlas_AddFontFromMemoryCompressedTTF(ImFontAtlas* self, void* compressed_font_data, int compressed_font_data_size, float size_pixels, ImFontConfig* font_cfg, ushort* glyph_ranges);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFont* ImFontAtlas_AddFontFromMemoryTTF(ImFontAtlas* self, void* font_data, int font_data_size, float size_pixels, ImFontConfig* font_cfg, ushort* glyph_ranges);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_Clear(ImFontAtlas* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_ClearFonts(ImFontAtlas* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_ClearInputData(ImFontAtlas* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_ClearTexData(ImFontAtlas* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_CompactCache(ImFontAtlas* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImFontAtlas_GetCustomRect(ImFontAtlas* self, ImFontAtlasRectId id, ImFontAtlasRect* out_r);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ushort* ImFontAtlas_GetGlyphRangesDefault(ImFontAtlas* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_RemoveCustomRect(ImFontAtlas* self, ImFontAtlasRectId id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_RemoveFont(ImFontAtlas* self, ImFont* font);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontAtlas_SetFontLoader(ImFontAtlas* self, ImFontLoader* font_loader);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontBaked_ClearOutputData(ImFontBaked* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFontGlyph* ImFontBaked_FindGlyph(ImFontBaked* self, ushort c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFontGlyph* ImFontBaked_FindGlyphNoFallback(ImFontBaked* self, ushort c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ImFontBaked_GetCharAdvance(ImFontBaked* self, ushort c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImFontBaked_IsGlyphLoaded(ImFontBaked* self, ushort c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontGlyphRangesBuilder_AddChar(ImFontGlyphRangesBuilder* self, ushort c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontGlyphRangesBuilder_AddRanges(ImFontGlyphRangesBuilder* self, ushort* ranges);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontGlyphRangesBuilder_AddText(ImFontGlyphRangesBuilder* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontGlyphRangesBuilder_BuildRanges(ImFontGlyphRangesBuilder* self, ImVector* out_ranges);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontGlyphRangesBuilder_Clear(ImFontGlyphRangesBuilder* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImFontGlyphRangesBuilder_GetBit(ImFontGlyphRangesBuilder* self, uint n);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFontGlyphRangesBuilder_SetBit(ImFontGlyphRangesBuilder* self, uint n);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFont_AddRemapChar(ImFont* self, ushort from_codepoint, ushort to_codepoint);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFont_CalcTextSizeA(Vector2* pOut, ImFont* self, float size, float max_width, float wrap_width, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end, char** remaining);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string ImFont_CalcWordWrapPosition(ImFont* self, float size, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end, float wrap_width);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFont_ClearOutputData(ImFont* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string ImFont_GetDebugName(ImFont* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFontBaked* ImFont_GetFontBaked(ImFont* self, float font_size, float density);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImFont_IsGlyphInFont(ImFont* self, ushort c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImFont_IsGlyphRangeUnused(ImFont* self, uint c_begin, uint c_last);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImFont_IsLoaded(ImFont* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFont_RenderChar(ImFont* self, ImDrawList* draw_list, float size, Vector2 pos, uint col, ushort c, Vector4* cpu_fine_clip);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImFont_RenderText(ImFont* self, ImDrawList* draw_list, float size, Vector2 pos, uint col, Vector4 clip_rect, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end, float wrap_width, [MarshalAs(UnmanagedType.I1)] bool cpu_fine_clip);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiFreeType_DebugEditFontLoaderFlags(ImGuiFreeTypeLoaderFlags* p_font_loader_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFontLoader* ImGuiFreeType_GetFontLoader();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiFreeType_SetAllocatorFunctions(IntPtr alloc_func, IntPtr free_func, void* user_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddFocusEvent(ImGuiIO* self, [MarshalAs(UnmanagedType.I1)] bool focused);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddInputCharacter(ImGuiIO* self, uint c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddInputCharacterUTF16(ImGuiIO* self, ushort c);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddInputCharactersUTF8(ImGuiIO* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string str);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddKeyAnalogEvent(ImGuiIO* self, ImGuiKey key, [MarshalAs(UnmanagedType.I1)] bool down, float v);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddKeyEvent(ImGuiIO* self, ImGuiKey key, [MarshalAs(UnmanagedType.I1)] bool down);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddMouseButtonEvent(ImGuiIO* self, int button, [MarshalAs(UnmanagedType.I1)] bool down);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddMousePosEvent(ImGuiIO* self, float x, float y);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddMouseSourceEvent(ImGuiIO* self, ImGuiMouseSource source);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_AddMouseWheelEvent(ImGuiIO* self, float wheel_x, float wheel_y);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_ClearEventsQueue(ImGuiIO* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_ClearInputKeys(ImGuiIO* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_ClearInputMouse(ImGuiIO* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_SetAppAcceptingEvents(ImGuiIO* self, [MarshalAs(UnmanagedType.I1)] bool accepting_events);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiIO_SetKeyEventNativeData(ImGuiIO* self, ImGuiKey key, int native_keycode, int native_scancode, int native_legacy_index);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiInputTextCallbackData_ClearSelection(ImGuiInputTextCallbackData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiInputTextCallbackData_DeleteChars(ImGuiInputTextCallbackData* self, int pos, int bytes_count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiInputTextCallbackData_HasSelection(ImGuiInputTextCallbackData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiInputTextCallbackData_InsertChars(ImGuiInputTextCallbackData* self, int pos, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiInputTextCallbackData_SelectAll(ImGuiInputTextCallbackData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiListClipper_Begin(ImGuiListClipper* self, int items_count, float items_height);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiListClipper_End(ImGuiListClipper* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiListClipper_IncludeItemByIndex(ImGuiListClipper* self, int item_index);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiListClipper_IncludeItemsByIndex(ImGuiListClipper* self, int item_begin, int item_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiListClipper_SeekCursorForItem(ImGuiListClipper* self, int item_index);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiListClipper_Step(ImGuiListClipper* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiPayload_Clear(ImGuiPayload* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiPayload_IsDataType(ImGuiPayload* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string type);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiPayload_IsDelivery(ImGuiPayload* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiPayload_IsPreview(ImGuiPayload* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiSelectionBasicStorage_ApplyRequests(ImGuiSelectionBasicStorage* self, ImGuiMultiSelectIO* ms_io);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiSelectionBasicStorage_Clear(ImGuiSelectionBasicStorage* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiSelectionBasicStorage_Contains(ImGuiSelectionBasicStorage* self, uint id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiSelectionBasicStorage_GetNextSelectedItem(ImGuiSelectionBasicStorage* self, void** opaque_it, uint* out_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint ImGuiSelectionBasicStorage_GetStorageIdFromIndex(ImGuiSelectionBasicStorage* self, int idx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiSelectionBasicStorage_SetItemSelected(ImGuiSelectionBasicStorage* self, uint id, [MarshalAs(UnmanagedType.I1)] bool selected);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiSelectionBasicStorage_Swap(ImGuiSelectionBasicStorage* self, ImGuiSelectionBasicStorage* r);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiSelectionExternalStorage_ApplyRequests(ImGuiSelectionExternalStorage* self, ImGuiMultiSelectIO* ms_io);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStorage_BuildSortByKey(ImGuiStorage* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStorage_Clear(ImGuiStorage* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiStorage_GetBool(ImGuiStorage* self, uint key, [MarshalAs(UnmanagedType.I1)] bool default_val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern byte* ImGuiStorage_GetBoolRef(ImGuiStorage* self, uint key, [MarshalAs(UnmanagedType.I1)] bool default_val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float ImGuiStorage_GetFloat(ImGuiStorage* self, uint key, float default_val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float* ImGuiStorage_GetFloatRef(ImGuiStorage* self, uint key, float default_val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ImGuiStorage_GetInt(ImGuiStorage* self, uint key, int default_val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int* ImGuiStorage_GetIntRef(ImGuiStorage* self, uint key, int default_val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void* ImGuiStorage_GetVoidPtr(ImGuiStorage* self, uint key);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void** ImGuiStorage_GetVoidPtrRef(ImGuiStorage* self, uint key, void* default_val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStorage_SetAllInt(ImGuiStorage* self, int val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStorage_SetBool(ImGuiStorage* self, uint key, [MarshalAs(UnmanagedType.I1)] bool val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStorage_SetFloat(ImGuiStorage* self, uint key, float val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStorage_SetInt(ImGuiStorage* self, uint key, int val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStorage_SetVoidPtr(ImGuiStorage* self, uint key, void* val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiStyle_ScaleAllSizes(ImGuiStyle* self, float scale_factor);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiTextBuffer_append(ImGuiTextBuffer* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string str, [MarshalAs(UnmanagedType.LPUTF8Str)] string str_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ImGuiTextBuffer_appendf0")]
		public static extern void ImGuiTextBuffer_appendf(ImGuiTextBuffer* self, char* fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string ImGuiTextBuffer_begin(ImGuiTextBuffer* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string ImGuiTextBuffer_c_str(ImGuiTextBuffer* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiTextBuffer_clear(ImGuiTextBuffer* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiTextBuffer_empty(ImGuiTextBuffer* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string ImGuiTextBuffer_end(ImGuiTextBuffer* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiTextBuffer_reserve(ImGuiTextBuffer* self, int capacity);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiTextBuffer_resize(ImGuiTextBuffer* self, int size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ImGuiTextBuffer_size(ImGuiTextBuffer* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiTextFilter_Build(ImGuiTextFilter* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiTextFilter_Clear(ImGuiTextFilter* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiTextFilter_Draw(ImGuiTextFilter* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string label, float width);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiTextFilter_IsActive(ImGuiTextFilter* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiTextFilter_PassFilter(ImGuiTextFilter* self, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool ImGuiTextRange_empty(ImGuiTextRange* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiTextRange_split(ImGuiTextRange* self, byte separator, ImVector* @out);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiViewport_GetCenter(Vector2* pOut, ImGuiViewport* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImGuiViewport_GetWorkCenter(Vector2* pOut, ImGuiViewport* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImTextureData_Create(ImTextureData* self, ImTextureFormat format, int w, int h);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImTextureData_DestroyPixels(ImTextureData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ImTextureData_GetPitch(ImTextureData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void* ImTextureData_GetPixels(ImTextureData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void* ImTextureData_GetPixelsAt(ImTextureData* self, int x, int y);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ImTextureData_GetSizeInBytes(ImTextureData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ulong ImTextureData_GetTexID(ImTextureData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImTextureData_GetTexRef(ImTextureRef* pOut, ImTextureData* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImTextureData_SetStatus(ImTextureData* self, ImTextureStatus status);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void ImTextureData_SetTexID(ImTextureData* self, ulong tex_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ulong ImTextureRef_GetTexID(ImTextureRef* self);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiPayload* igAcceptDragDropPayload([MarshalAs(UnmanagedType.LPUTF8Str)] string type, ImGuiDragDropFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igAlignTextToFramePadding();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igArrowButton([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiDir dir);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBegin([MarshalAs(UnmanagedType.LPUTF8Str)] string name, ref byte p_open, ImGuiWindowFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginChild_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, Vector2 size, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginChild_ID(uint id, Vector2 size, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginCombo([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string preview_value, ImGuiComboFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igBeginDisabled([MarshalAs(UnmanagedType.I1)] bool disabled);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginDragDropSource(ImGuiDragDropFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginDragDropTarget();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igBeginGroup();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginItemTooltip();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginListBox([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginMainMenuBar();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginMenu([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.I1)] bool enabled);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginMenuBar();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiMultiSelectIO* igBeginMultiSelect(ImGuiMultiSelectFlags flags, int selection_size, int items_count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginPopup([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiWindowFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginPopupContextItem([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiPopupFlags popup_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginPopupContextVoid([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiPopupFlags popup_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginPopupContextWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiPopupFlags popup_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginPopupModal([MarshalAs(UnmanagedType.LPUTF8Str)] string name, byte* p_open, ImGuiWindowFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginTabBar([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiTabBarFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginTabItem([MarshalAs(UnmanagedType.LPUTF8Str)] string label, byte* p_open, ImGuiTabItemFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginTable([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, int columns, ImGuiTableFlags flags, Vector2 outer_size, float inner_width);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igBeginTooltip();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igBullet();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBulletText0")]
		public static extern void igBulletText([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igButton([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igCalcItemWidth();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igCalcTextSize(Vector2* pOut, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end, [MarshalAs(UnmanagedType.I1)] bool hide_text_after_double_hash, float wrap_width);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCheckbox([MarshalAs(UnmanagedType.LPUTF8Str)] string label, byte* v);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCheckboxFlags_IntPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* flags, int flags_value);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCheckboxFlags_UintPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, uint* flags, uint flags_value);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igCloseCurrentPopup();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCollapsingHeader_TreeNodeFlags([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiTreeNodeFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCollapsingHeader_BoolPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, byte* p_visible, ImGuiTreeNodeFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igColorButton([MarshalAs(UnmanagedType.LPUTF8Str)] string desc_id, Vector4 col, ImGuiColorEditFlags flags, Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igColorConvertFloat4ToU32(Vector4 @in);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igColorConvertHSVtoRGB(float h, float s, float v, float* out_r, float* out_g, float* out_b);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igColorConvertRGBtoHSV(float r, float g, float b, float* out_h, float* out_s, float* out_v);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igColorConvertU32ToFloat4(Vector4* pOut, uint @in);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igColorEdit3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ref Vector3 col, ImGuiColorEditFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igColorEdit4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector4* col, ImGuiColorEditFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igColorPicker3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector3* col, ImGuiColorEditFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igColorPicker4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector4* col, ImGuiColorEditFlags flags, float* ref_col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igColumns(int count, [MarshalAs(UnmanagedType.LPUTF8Str)] string id, [MarshalAs(UnmanagedType.I1)] bool borders);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCombo_Str_arr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* current_item, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8StringArrayMarshaler))] string[] items, int items_count, int popup_max_height_in_items);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCombo_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* current_item, [MarshalAs(UnmanagedType.LPUTF8Str)] string items_separated_by_zeros, int popup_max_height_in_items);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igCombo_FnStrPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* current_item, IntPtr getter, void* user_data, int items_count, int popup_max_height_in_items);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr igCreateContext(ImFontAtlas* shared_font_atlas);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDebugCheckVersionAndDataLayout([MarshalAs(UnmanagedType.LPUTF8Str)] string version_str, uint sz_io, uint sz_style, uint sz_vec2, uint sz_vec4, uint sz_drawvert, uint sz_drawidx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igDebugFlashStyleColor(ImGuiCol idx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDebugLog0")]
		public static extern void igDebugLog([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igDebugStartItemPicker();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igDebugTextEncoding([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igDestroyContext(IntPtr ctx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragFloat([MarshalAs(UnmanagedType.LPUTF8Str)] string label, float* v, float v_speed, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragFloat2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2* v, float v_speed, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragFloat3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector3* v, float v_speed, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragFloat4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector4* v, float v_speed, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragFloatRange2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, float* v_current_min, float* v_current_max, float v_speed, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, [MarshalAs(UnmanagedType.LPUTF8Str)] string format_max, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragInt([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, float v_speed, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragInt2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, float v_speed, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragInt3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, float v_speed, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragInt4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, float v_speed, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragIntRange2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v_current_min, int* v_current_max, float v_speed, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, [MarshalAs(UnmanagedType.LPUTF8Str)] string format_max, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragScalar([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiDataType data_type, void* p_data, float v_speed, void* p_min, void* p_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igDragScalarN([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiDataType data_type, void* p_data, int components, float v_speed, void* p_min, void* p_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igDummy(Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEnd();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndChild();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndCombo();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndDisabled();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndDragDropSource();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndDragDropTarget();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndFrame();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndGroup();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndListBox();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndMainMenuBar();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndMenu();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndMenuBar();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiMultiSelectIO* igEndMultiSelect();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndPopup();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndTabBar();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndTabItem();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndTable();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igEndTooltip();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetAllocatorFunctions(IntPtr* p_alloc_func, IntPtr* p_free_func, void** p_user_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImDrawList* igGetBackgroundDrawList_Nil();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string igGetClipboardText();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetColorU32_Col(ImGuiCol idx, float alpha_mul);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetColorU32_Vec4(Vector4 col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetColorU32_U32(uint col, float alpha_mul);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igGetColumnIndex();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetColumnOffset(int column_index);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetColumnWidth(int column_index);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igGetColumnsCount();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetContentRegionAvail(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr igGetCurrentContext();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetCursorPos(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetCursorPosX();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetCursorPosY();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetCursorScreenPos(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetCursorStartPos(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiPayload* igGetDragDropPayload();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImDrawData* igGetDrawData();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr igGetDrawListSharedData();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFont* igGetFont();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImFontBaked* igGetFontBaked();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetFontSize();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetFontTexUvWhitePixel(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImDrawList* igGetForegroundDrawList_Nil();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igGetFrameCount();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetFrameHeight();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetFrameHeightWithSpacing();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetID_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetID_StrStr([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string str_id_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetID_Ptr(void* ptr_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetID_Int(int int_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiIO* igGetIO_Nil();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint igGetItemID();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetItemRectMax(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetItemRectMin(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetItemRectSize(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string igGetKeyName(ImGuiKey key);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igGetKeyPressedAmount(ImGuiKey key, float repeat_delay, float rate);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiViewport* igGetMainViewport();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igGetMouseClickedCount(ImGuiMouseButton button);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiMouseCursor igGetMouseCursor();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetMouseDragDelta(Vector2* pOut, ImGuiMouseButton button, float lock_threshold);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetMousePos(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetMousePosOnOpeningCurrentPopup(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr igGetPlatformIO_Nil();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetScrollMaxX();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetScrollMaxY();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetScrollX();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetScrollY();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiStorage* igGetStateStorage();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiStyle* igGetStyle();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string igGetStyleColorName(ImGuiCol idx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern Vector4* igGetStyleColorVec4(ImGuiCol idx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetTextLineHeight();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetTextLineHeightWithSpacing();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern double igGetTime();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetTreeNodeToLabelSpacing();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string igGetVersion();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImDrawList* igGetWindowDrawList();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetWindowHeight();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetWindowPos(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igGetWindowSize(Vector2* pOut);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern float igGetWindowWidth();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igImage(ImTextureRef tex_ref, Vector2 image_size, Vector2 uv0, Vector2 uv1);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igImageButton([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImTextureRef tex_ref, Vector2 image_size, Vector2 uv0, Vector2 uv1, Vector4 bg_col, Vector4 tint_col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igImageWithBg(ImTextureRef tex_ref, Vector2 image_size, Vector2 uv0, Vector2 uv1, Vector4 bg_col, Vector4 tint_col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igIndent(float indent_w);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputDouble([MarshalAs(UnmanagedType.LPUTF8Str)] string label, double* v, double step, double step_fast, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputFloat([MarshalAs(UnmanagedType.LPUTF8Str)] string label, float* v, float step, float step_fast, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputFloat2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2* v, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputFloat3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector3* v, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputFloat4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector4* v, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputInt([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, int step, int step_fast, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputInt2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputInt3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputInt4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputScalar([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiDataType data_type, void* p_data, void* p_step, void* p_step_fast, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputScalarN([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiDataType data_type, void* p_data, int components, void* p_step, void* p_step_fast, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiInputTextFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputText([MarshalAs(UnmanagedType.LPUTF8Str)] string label, byte* buf, uint buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputTextMultiline([MarshalAs(UnmanagedType.LPUTF8Str)] string label, byte* buf, uint buf_size, Vector2 size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInputTextWithHint([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string hint, byte* buf, uint buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igInvisibleButton([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, Vector2 size, ImGuiButtonFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsAnyItemActive();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsAnyItemFocused();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsAnyItemHovered();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsAnyMouseDown();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemActivated();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemActive();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemClicked(ImGuiMouseButton mouse_button);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemDeactivated();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemDeactivatedAfterEdit();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemEdited();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemFocused();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemHovered(ImGuiHoveredFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemToggledOpen();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemToggledSelection();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsItemVisible();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsKeyChordPressed_Nil(ImGuiKey key_chord);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsKeyDown_Nil(ImGuiKey key);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsKeyPressed_Bool(ImGuiKey key, [MarshalAs(UnmanagedType.I1)] bool repeat);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsKeyReleased_Nil(ImGuiKey key);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMouseClicked_Bool(ImGuiMouseButton button, [MarshalAs(UnmanagedType.I1)] bool repeat);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMouseDoubleClicked_Nil(ImGuiMouseButton button);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMouseDown_Nil(ImGuiMouseButton button);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMouseDragging(ImGuiMouseButton button, float lock_threshold);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMouseHoveringRect(Vector2 r_min, Vector2 r_max, [MarshalAs(UnmanagedType.I1)] bool clip);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMousePosValid(Vector2* mouse_pos);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMouseReleased_Nil(ImGuiMouseButton button);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsMouseReleasedWithDelay(ImGuiMouseButton button, float delay);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsPopupOpen_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiPopupFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsRectVisible_Nil(Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsRectVisible_Vec2(Vector2 rect_min, Vector2 rect_max);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsWindowAppearing();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsWindowCollapsed();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsWindowFocused(ImGuiFocusedFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igIsWindowHovered(ImGuiHoveredFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igLabelText0")]
		public static extern void igLabelText([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igListBox_Str_arr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* current_item, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8StringArrayMarshaler))] string[] items, int items_count, int height_in_items);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igListBox_FnStrPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* current_item, IntPtr getter, void* user_data, int items_count, int height_in_items);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igLoadIniSettingsFromDisk([MarshalAs(UnmanagedType.LPUTF8Str)] string ini_filename);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igLoadIniSettingsFromMemory([MarshalAs(UnmanagedType.LPUTF8Str)] string ini_data, uint ini_size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igLogButtons();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igLogFinish();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igLogText0")]
		public static extern void igLogText([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igLogToClipboard(int auto_open_depth);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igLogToFile(int auto_open_depth, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igLogToTTY(int auto_open_depth);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void* igMemAlloc(uint size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igMemFree(void* ptr);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igMenuItem_Bool([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string shortcut, [MarshalAs(UnmanagedType.I1)] bool selected, [MarshalAs(UnmanagedType.I1)] bool enabled);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igMenuItem_BoolPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string shortcut, byte* p_selected, [MarshalAs(UnmanagedType.I1)] bool enabled);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igNewFrame();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igNewLine();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igNextColumn();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igOpenPopup_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiPopupFlags popup_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igOpenPopup_ID(uint id, ImGuiPopupFlags popup_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igOpenPopupOnItemClick([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiPopupFlags popup_flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPlotHistogram_FloatPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, float* values, int values_count, int values_offset, [MarshalAs(UnmanagedType.LPUTF8Str)] string overlay_text, float scale_min, float scale_max, Vector2 graph_size, int stride);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPlotHistogram_FnFloatPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, IntPtr values_getter, void* data, int values_count, int values_offset, [MarshalAs(UnmanagedType.LPUTF8Str)] string overlay_text, float scale_min, float scale_max, Vector2 graph_size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPlotLines_FloatPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, float* values, int values_count, int values_offset, [MarshalAs(UnmanagedType.LPUTF8Str)] string overlay_text, float scale_min, float scale_max, Vector2 graph_size, int stride);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPlotLines_FnFloatPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, IntPtr values_getter, void* data, int values_count, int values_offset, [MarshalAs(UnmanagedType.LPUTF8Str)] string overlay_text, float scale_min, float scale_max, Vector2 graph_size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopClipRect();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopFont();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopID();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopItemFlag();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopItemWidth();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopStyleColor(int count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopStyleVar(int count);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPopTextWrapPos();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igProgressBar(float fraction, Vector2 size_arg, [MarshalAs(UnmanagedType.LPUTF8Str)] string overlay);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushClipRect(Vector2 clip_rect_min, Vector2 clip_rect_max, [MarshalAs(UnmanagedType.I1)] bool intersect_with_current_clip_rect);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushFont(ImFont* font, float font_size_base_unscaled);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushID_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushID_StrStr([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id_begin, [MarshalAs(UnmanagedType.LPUTF8Str)] string str_id_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushID_Ptr(void* ptr_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushID_Int(int int_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushItemFlag(ImGuiItemFlags option, [MarshalAs(UnmanagedType.I1)] bool enabled);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushItemWidth(float item_width);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushStyleColor_U32(ImGuiCol idx, uint col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushStyleColor_Vec4(ImGuiCol idx, Vector4 col);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushStyleVar_Float(ImGuiStyleVar idx, float val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushStyleVar_Vec2(ImGuiStyleVar idx, Vector2 val);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushStyleVarX(ImGuiStyleVar idx, float val_x);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushStyleVarY(ImGuiStyleVar idx, float val_y);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igPushTextWrapPos(float wrap_local_pos_x);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igRadioButton_Bool([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.I1)] bool active);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igRadioButton_IntPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, int v_button);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igRender();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igResetMouseDragDelta(ImGuiMouseButton button);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSameLine(float offset_from_start_x, float spacing);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSaveIniSettingsToDisk([MarshalAs(UnmanagedType.LPUTF8Str)] string ini_filename);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string igSaveIniSettingsToMemory(uint* out_ini_size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSelectable_Bool([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.I1)] bool selected, ImGuiSelectableFlags flags, Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSelectable_BoolPtr([MarshalAs(UnmanagedType.LPUTF8Str)] string label, byte* p_selected, ImGuiSelectableFlags flags, Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSeparator();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSeparatorText([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetAllocatorFunctions(IntPtr alloc_func, IntPtr free_func, void* user_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetColorEditOptions(ImGuiColorEditFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetColumnOffset(int column_index, float offset_x);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetColumnWidth(int column_index, float width);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetCurrentContext(IntPtr ctx);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetCursorPos(Vector2 local_pos);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetCursorPosX(float local_x);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetCursorPosY(float local_y);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetCursorScreenPos(Vector2 pos);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSetDragDropPayload([MarshalAs(UnmanagedType.LPUTF8Str)] string type, void* data, uint sz, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetItemDefaultFocus();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetItemKeyOwner_Nil(ImGuiKey key);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetItemTooltip0")]
		public static extern void igSetItemTooltip([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetKeyboardFocusHere(int offset);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetMouseCursor(ImGuiMouseCursor cursor_type);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNavCursorVisible([MarshalAs(UnmanagedType.I1)] bool visible);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextFrameWantCaptureKeyboard([MarshalAs(UnmanagedType.I1)] bool want_capture_keyboard);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextFrameWantCaptureMouse([MarshalAs(UnmanagedType.I1)] bool want_capture_mouse);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextItemAllowOverlap();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextItemOpen([MarshalAs(UnmanagedType.I1)] bool is_open, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextItemSelectionUserData(long selection_user_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextItemShortcut(ImGuiKey key_chord, ImGuiInputFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextItemStorageID(uint storage_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextItemWidth(float item_width);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowBgAlpha(float alpha);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowCollapsed([MarshalAs(UnmanagedType.I1)] bool collapsed, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowContentSize(Vector2 size);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowFocus();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowPos(Vector2 pos, ImGuiCond cond, Vector2 pivot);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowScroll(Vector2 scroll);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowSize(Vector2 size, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetNextWindowSizeConstraints(Vector2 size_min, Vector2 size_max, ImGuiSizeCallback custom_callback, void* custom_callback_data);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetScrollFromPosX_Float(float local_x, float center_x_ratio);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetScrollFromPosY_Float(float local_y, float center_y_ratio);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetScrollHereX(float center_x_ratio);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetScrollHereY(float center_y_ratio);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetScrollX_Float(float scroll_x);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetScrollY_Float(float scroll_y);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetStateStorage(ImGuiStorage* storage);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetTabItemClosed([MarshalAs(UnmanagedType.LPUTF8Str)] string tab_or_docked_window_label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetTooltip0")]
		public static extern void igSetTooltip([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowCollapsed_Bool([MarshalAs(UnmanagedType.I1)] bool collapsed, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowCollapsed_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.I1)] bool collapsed, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowFocus_Nil();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowFocus_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowPos_Vec2(Vector2 pos, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowPos_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string name, Vector2 pos, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowSize_Vec2(Vector2 size, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSetWindowSize_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string name, Vector2 size, ImGuiCond cond);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igShortcut_Nil(ImGuiKey key_chord, ImGuiInputFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowAboutWindow(byte* p_open);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowDebugLogWindow(byte* p_open);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowDemoWindow(ref byte p_open);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowFontSelector([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowIDStackToolWindow(byte* p_open);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowMetricsWindow(byte* p_open);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowStyleEditor(ImGuiStyle* @ref);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igShowStyleSelector([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igShowUserGuide();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderAngle([MarshalAs(UnmanagedType.LPUTF8Str)] string label, float* v_rad, float v_degrees_min, float v_degrees_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderFloat([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ref float v, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderFloat2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2* v, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderFloat3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector3* v, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderFloat4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector4* v, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderInt([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderInt2([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderInt3([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderInt4([MarshalAs(UnmanagedType.LPUTF8Str)] string label, int* v, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderScalar([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiDataType data_type, void* p_data, void* p_min, void* p_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSliderScalarN([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiDataType data_type, void* p_data, int components, void* p_min, void* p_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igSmallButton([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igSpacing();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igStyleColorsClassic(ImGuiStyle* dst);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igStyleColorsDark(ImGuiStyle* dst);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igStyleColorsLight(ImGuiStyle* dst);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTabItemButton([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiTabItemFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableAngledHeadersRow();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igTableGetColumnCount();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiTableColumnFlags igTableGetColumnFlags(int column_n);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igTableGetColumnIndex();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.LPUTF8Str)]
		public static extern string igTableGetColumnName_Int(int column_n);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igTableGetHoveredColumn();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern int igTableGetRowIndex();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern ImGuiTableSortSpecs* igTableGetSortSpecs();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableHeader([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableHeadersRow();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTableNextColumn();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableNextRow(ImGuiTableRowFlags row_flags, float min_row_height);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableSetBgColor(ImGuiTableBgTarget target, uint color, int column_n);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableSetColumnEnabled(int column_n, [MarshalAs(UnmanagedType.I1)] bool v);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTableSetColumnIndex(int column_n);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableSetupColumn([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiTableColumnFlags flags, float init_width_or_weight, uint user_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTableSetupScrollFreeze(int cols, int rows);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igText0")]
		public static extern void igText([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextColored0")]
		public static extern void igTextColored(Vector4 col, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextDisabled0")]
		public static extern void igTextDisabled([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTextLink([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTextLinkOpenURL([MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string url);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTextUnformatted([MarshalAs(UnmanagedType.LPUTF8Str)] string text, [MarshalAs(UnmanagedType.LPUTF8Str)] string text_end);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextWrapped0")]
		public static extern void igTextWrapped([MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTreeNode_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string label);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNode_StrStr0")]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTreeNode_StrStr([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNode_Ptr0")]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTreeNode_Ptr(void* ptr_id, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTreeNodeEx_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string label, ImGuiTreeNodeFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNodeEx_StrStr0")]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTreeNodeEx_StrStr([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id, ImGuiTreeNodeFlags flags, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNodeEx_Ptr0")]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igTreeNodeEx_Ptr(void* ptr_id, ImGuiTreeNodeFlags flags, [MarshalAs(UnmanagedType.LPUTF8Str)] string fmt);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTreePop();

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTreePush_Str([MarshalAs(UnmanagedType.LPUTF8Str)] string str_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igTreePush_Ptr(void* ptr_id);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igUnindent(float indent_w);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igVSliderFloat([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2 size, float* v, float v_min, float v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igVSliderInt([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2 size, int* v, int v_min, int v_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		[return: MarshalAs(UnmanagedType.I1)]
		public static extern bool igVSliderScalar([MarshalAs(UnmanagedType.LPUTF8Str)] string label, Vector2 size, ImGuiDataType data_type, void* p_data, void* p_min, void* p_max, [MarshalAs(UnmanagedType.LPUTF8Str)] string format, ImGuiSliderFlags flags);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igValue_Bool([MarshalAs(UnmanagedType.LPUTF8Str)] string prefix, [MarshalAs(UnmanagedType.I1)] bool b);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igValue_Int([MarshalAs(UnmanagedType.LPUTF8Str)] string prefix, int v);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igValue_Uint([MarshalAs(UnmanagedType.LPUTF8Str)] string prefix, uint v);

		[DllImport(NativeSokolImport, CallingConvention = CallingConvention.Cdecl)]
		public static extern void igValue_Float([MarshalAs(UnmanagedType.LPUTF8Str)] string prefix, float v, [MarshalAs(UnmanagedType.LPUTF8Str)] string float_format);

	}
}
