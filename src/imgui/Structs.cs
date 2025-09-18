using System.Numerics;
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Imgui
{
	//TBD ELI
	using ImFontAtlasRectId = int;
	using ImFontAtlasBuilder = System.IntPtr;
	using ImVector_ImTextureDataPtr = System.IntPtr;
	using ImVector_ImFontConfigPtr = System.IntPtr;
	using ImVector_ImTextureRef = System.IntPtr;
	using ImVector_ImDrawListSharedDataPtr = System.IntPtr;
	public struct ImVector_ImTextureRect{};
	public unsafe partial struct ImColor
	{
		public Vector4 Value;
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
	}

	public unsafe partial struct ImDrawListSplitter
	{
		public int _Current;
		public int _Count;
		public ImVector _Channels;
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
		public byte ConfigDockingNoSplit;
		public byte ConfigDockingWithShift;
		public byte ConfigDockingAlwaysTabBar;
		public byte ConfigDockingTransparentPayload;
		public byte ConfigViewportsNoAutoMerge;
		public byte ConfigViewportsNoTaskBarIcon;
		public byte ConfigViewportsNoDecoration;
		public byte ConfigViewportsNoDefaultParent;
		public byte ConfigViewportPlatformFocusSetsImGuiFocus;
		public byte ConfigDpiScaleFonts;
		public byte ConfigDpiScaleViewports;
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
		public uint MouseHoveredViewport;
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
		public Vector2 MouseDragMaxDistanceAbs_0;
		public Vector2 MouseDragMaxDistanceAbs_1;
		public Vector2 MouseDragMaxDistanceAbs_2;
		public Vector2 MouseDragMaxDistanceAbs_3;
		public Vector2 MouseDragMaxDistanceAbs_4;
		public fixed float MouseDragMaxDistanceSqr[5];
		public float PenPressure;
		public byte AppFocusLost;
		public byte AppAcceptingEvents;
		public ushort InputQueueSurrogate;
		public ImVector InputQueueCharacters;
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
		public IntPtr Platform_CreateWindow;
		public IntPtr Platform_DestroyWindow;
		public IntPtr Platform_ShowWindow;
		public IntPtr Platform_SetWindowPos;
		public IntPtr Platform_GetWindowPos;
		public IntPtr Platform_SetWindowSize;
		public IntPtr Platform_GetWindowSize;
		public IntPtr Platform_GetWindowFramebufferScale;
		public IntPtr Platform_SetWindowFocus;
		public IntPtr Platform_GetWindowFocus;
		public IntPtr Platform_GetWindowMinimized;
		public IntPtr Platform_SetWindowTitle;
		public IntPtr Platform_SetWindowAlpha;
		public IntPtr Platform_UpdateWindow;
		public IntPtr Platform_RenderWindow;
		public IntPtr Platform_SwapBuffers;
		public IntPtr Platform_GetWindowDpiScale;
		public IntPtr Platform_OnChangedViewport;
		public IntPtr Platform_GetWindowWorkAreaInsets;
		public IntPtr Platform_CreateVkSurface;
		public IntPtr Renderer_CreateWindow;
		public IntPtr Renderer_DestroyWindow;
		public IntPtr Renderer_SetWindowSize;
		public IntPtr Renderer_RenderWindow;
		public IntPtr Renderer_SwapBuffers;
		public ImVector Monitors;
		public ImVector_ImTextureDataPtr Textures;
		public ImVector Viewports;
	}

	public unsafe partial struct ImGuiPlatformImeData
	{
		public byte WantVisible;
		public byte WantTextInput;
		public Vector2 InputPos;
		public float InputLineHeight;
		public uint ViewportId;
	}

	public unsafe partial struct ImGuiPlatformMonitor
	{
		public Vector2 MainPos;
		public Vector2 MainSize;
		public Vector2 WorkPos;
		public Vector2 WorkSize;
		public float DpiScale;
		public void* PlatformHandle;
	}

	public unsafe partial struct ImGuiSelectionBasicStorage
	{
		public int Size;
		public byte PreserveOrder;
		public void* UserData;
		public IntPtr AdapterIndexToStorageId;
		public int _SelectionOrder;
		public ImGuiStorage _Storage;
	}

	public unsafe partial struct ImGuiSelectionExternalStorage
	{
		public void* UserData;
		public IntPtr AdapterSetItemSelected;
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
		public float DockingSeparatorSize;
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
		public Vector4 Colors_58;
		public Vector4 Colors_59;
		public float HoverStationaryDelay;
		public float HoverDelayShort;
		public float HoverDelayNormal;
		public ImGuiHoveredFlags HoverFlagsForTooltipMouse;
		public ImGuiHoveredFlags HoverFlagsForTooltipNav;
		public float _MainScale;
		public float _NextFrameFontSizeBase;
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
	}

	public unsafe partial struct ImGuiTextFilter
	{
		public fixed byte InputBuf[256];
		public ImVector Filters;
		public int CountGrep;
	}

	public unsafe partial struct ImGuiTextRange
	{
		public byte* b;
		public byte* e;
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
		public float DpiScale;
		public uint ParentViewportId;
		public ImDrawData* DrawData;
		public void* RendererUserData;
		public void* PlatformUserData;
		public void* PlatformHandle;
		public void* PlatformHandleRaw;
		public byte PlatformWindowCreated;
		public byte PlatformRequestMove;
		public byte PlatformRequestResize;
		public byte PlatformRequestClose;
	}

	public unsafe partial struct ImGuiWindowClass
	{
		public uint ClassId;
		public uint ParentViewportId;
		public uint FocusRouteParentWindowId;
		public ImGuiViewportFlags ViewportFlagsOverrideSet;
		public ImGuiViewportFlags ViewportFlagsOverrideClear;
		public ImGuiTabItemFlags TabItemFlagsOverrideSet;
		public ImGuiDockNodeFlags DockNodeFlagsOverrideSet;
		public byte DockingAlwaysTabBar;
		public byte DockingAllowUnclassed;
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

