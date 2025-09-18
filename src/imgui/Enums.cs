using System.Numerics;
using System;

namespace Imgui
{
	[Flags]
	public enum ImDrawFlags
	{
		None = 0,
		Closed = 1,
		RoundCornersTopLeft = 16,
		RoundCornersTopRight = 32,
		RoundCornersBottomLeft = 64,
		RoundCornersBottomRight = 128,
		RoundCornersNone = 256,
		RoundCornersTop = 48,
		RoundCornersBottom = 192,
		RoundCornersLeft = 80,
		RoundCornersRight = 160,
		RoundCornersAll = 240,
		RoundCornersDefault_ = 240,
		RoundCornersMask_ = 496,
	}

	[Flags]
	public enum ImDrawListFlags
	{
		None = 0,
		AntiAliasedLines = 1,
		AntiAliasedLinesUseTex = 2,
		AntiAliasedFill = 4,
		AllowVtxOffset = 8,
	}

	[Flags]
	public enum ImFontAtlasFlags
	{
		None = 0,
		NoPowerOfTwoHeight = 1,
		NoMouseCursors = 2,
		NoBakedLines = 4,
	}

	[Flags]
	public enum ImFontFlags
	{
		None = 0,
		NoLoadError = 2,
		NoLoadGlyphs = 4,
		LockBakedSizes = 8,
	}

	[Flags]
	public enum ImGuiBackendFlags
	{
		None = 0,
		HasGamepad = 1,
		HasMouseCursors = 2,
		HasSetMousePos = 4,
		RendererHasVtxOffset = 8,
		RendererHasTextures = 16,
		PlatformHasViewports = 1024,
		HasMouseHoveredViewport = 2048,
		RendererHasViewports = 4096,
	}

	[Flags]
	public enum ImGuiButtonFlags
	{
		None = 0,
		MouseButtonLeft = 1,
		MouseButtonRight = 2,
		MouseButtonMiddle = 4,
		MouseButtonMask_ = 7,
		EnableNav = 8,
	}

	[Flags]
	public enum ImGuiChildFlags
	{
		None = 0,
		Borders = 1,
		AlwaysUseWindowPadding = 2,
		ResizeX = 4,
		ResizeY = 8,
		AutoResizeX = 16,
		AutoResizeY = 32,
		AlwaysAutoResize = 64,
		FrameStyle = 128,
		NavFlattened = 256,
	}

	public enum ImGuiCol
	{
		Text = 0,
		TextDisabled = 1,
		WindowBg = 2,
		ChildBg = 3,
		PopupBg = 4,
		Border = 5,
		BorderShadow = 6,
		FrameBg = 7,
		FrameBgHovered = 8,
		FrameBgActive = 9,
		TitleBg = 10,
		TitleBgActive = 11,
		TitleBgCollapsed = 12,
		MenuBarBg = 13,
		ScrollbarBg = 14,
		ScrollbarGrab = 15,
		ScrollbarGrabHovered = 16,
		ScrollbarGrabActive = 17,
		CheckMark = 18,
		SliderGrab = 19,
		SliderGrabActive = 20,
		Button = 21,
		ButtonHovered = 22,
		ButtonActive = 23,
		Header = 24,
		HeaderHovered = 25,
		HeaderActive = 26,
		Separator = 27,
		SeparatorHovered = 28,
		SeparatorActive = 29,
		ResizeGrip = 30,
		ResizeGripHovered = 31,
		ResizeGripActive = 32,
		InputTextCursor = 33,
		TabHovered = 34,
		Tab = 35,
		TabSelected = 36,
		TabSelectedOverline = 37,
		TabDimmed = 38,
		TabDimmedSelected = 39,
		TabDimmedSelectedOverline = 40,
		DockingPreview = 41,
		DockingEmptyBg = 42,
		PlotLines = 43,
		PlotLinesHovered = 44,
		PlotHistogram = 45,
		PlotHistogramHovered = 46,
		TableHeaderBg = 47,
		TableBorderStrong = 48,
		TableBorderLight = 49,
		TableRowBg = 50,
		TableRowBgAlt = 51,
		TextLink = 52,
		TextSelectedBg = 53,
		TreeLines = 54,
		DragDropTarget = 55,
		NavCursor = 56,
		NavWindowingHighlight = 57,
		NavWindowingDimBg = 58,
		ModalWindowDimBg = 59,
		COUNT = 60,
	}

	[Flags]
	public enum ImGuiColorEditFlags
	{
		None = 0,
		NoAlpha = 2,
		NoPicker = 4,
		NoOptions = 8,
		NoSmallPreview = 16,
		NoInputs = 32,
		NoTooltip = 64,
		NoLabel = 128,
		NoSidePreview = 256,
		NoDragDrop = 512,
		NoBorder = 1024,
		AlphaOpaque = 2048,
		AlphaNoBg = 4096,
		AlphaPreviewHalf = 8192,
		AlphaBar = 65536,
		HDR = 524288,
		DisplayRGB = 1048576,
		DisplayHSV = 2097152,
		DisplayHex = 4194304,
		Uint8 = 8388608,
		Float = 16777216,
		PickerHueBar = 33554432,
		PickerHueWheel = 67108864,
		InputRGB = 134217728,
		InputHSV = 268435456,
		DefaultOptions_ = 177209344,
		AlphaMask_ = 14338,
		DisplayMask_ = 7340032,
		DataTypeMask_ = 25165824,
		PickerMask_ = 100663296,
		InputMask_ = 402653184,
	}

	[Flags]
	public enum ImGuiComboFlags
	{
		None = 0,
		PopupAlignLeft = 1,
		HeightSmall = 2,
		HeightRegular = 4,
		HeightLarge = 8,
		HeightLargest = 16,
		NoArrowButton = 32,
		NoPreview = 64,
		WidthFitPreview = 128,
		HeightMask_ = 30,
	}

	public enum ImGuiCond
	{
		None = 0,
		Always = 1,
		Once = 2,
		FirstUseEver = 4,
		Appearing = 8,
	}

	[Flags]
	public enum ImGuiConfigFlags
	{
		None = 0,
		NavEnableKeyboard = 1,
		NavEnableGamepad = 2,
		NoMouse = 16,
		NoMouseCursorChange = 32,
		NoKeyboard = 64,
		DockingEnable = 128,
		ViewportsEnable = 1024,
		IsSRGB = 1048576,
		IsTouchScreen = 2097152,
	}

	public enum ImGuiDataType
	{
		S8 = 0,
		U8 = 1,
		S16 = 2,
		U16 = 3,
		S32 = 4,
		U32 = 5,
		S64 = 6,
		U64 = 7,
		Float = 8,
		Double = 9,
		Bool = 10,
		String = 11,
		COUNT = 12,
	}

	public enum ImGuiDir
	{
		None = -1,
		Left = 0,
		Right = 1,
		Up = 2,
		Down = 3,
		COUNT = 4,
	}

	[Flags]
	public enum ImGuiDockNodeFlags
	{
		None = 0,
		KeepAliveOnly = 1,
		NoDockingOverCentralNode = 4,
		PassthruCentralNode = 8,
		NoDockingSplit = 16,
		NoResize = 32,
		AutoHideTabBar = 64,
		NoUndocking = 128,
	}

	[Flags]
	public enum ImGuiDragDropFlags
	{
		None = 0,
		SourceNoPreviewTooltip = 1,
		SourceNoDisableHover = 2,
		SourceNoHoldToOpenOthers = 4,
		SourceAllowNullID = 8,
		SourceExtern = 16,
		PayloadAutoExpire = 32,
		PayloadNoCrossContext = 64,
		PayloadNoCrossProcess = 128,
		AcceptBeforeDelivery = 1024,
		AcceptNoDrawDefaultRect = 2048,
		AcceptNoPreviewTooltip = 4096,
		AcceptPeekOnly = 3072,
	}

	[Flags]
	public enum ImGuiFocusedFlags
	{
		None = 0,
		ChildWindows = 1,
		RootWindow = 2,
		AnyWindow = 4,
		NoPopupHierarchy = 8,
		DockHierarchy = 16,
		RootAndChildWindows = 3,
	}

	[Flags]
	public enum ImGuiFreeTypeLoaderFlags
	{
		None = 0,
		NoHinting = 1,
		NoAutoHint = 2,
		ForceAutoHint = 4,
		LightHinting = 8,
		MonoHinting = 16,
		Bold = 32,
		Oblique = 64,
		Monochrome = 128,
		LoadColor = 256,
		Bitmap = 512,
	}

	[Flags]
	public enum ImGuiHoveredFlags
	{
		None = 0,
		ChildWindows = 1,
		RootWindow = 2,
		AnyWindow = 4,
		NoPopupHierarchy = 8,
		DockHierarchy = 16,
		AllowWhenBlockedByPopup = 32,
		AllowWhenBlockedByActiveItem = 128,
		AllowWhenOverlappedByItem = 256,
		AllowWhenOverlappedByWindow = 512,
		AllowWhenDisabled = 1024,
		NoNavOverride = 2048,
		AllowWhenOverlapped = 768,
		RectOnly = 928,
		RootAndChildWindows = 3,
		ForTooltip = 4096,
		Stationary = 8192,
		DelayNone = 16384,
		DelayShort = 32768,
		DelayNormal = 65536,
		NoSharedDelay = 131072,
	}

	[Flags]
	public enum ImGuiInputFlags
	{
		None = 0,
		Repeat = 1,
		RouteActive = 1024,
		RouteFocused = 2048,
		RouteGlobal = 4096,
		RouteAlways = 8192,
		RouteOverFocused = 16384,
		RouteOverActive = 32768,
		RouteUnlessBgFocused = 65536,
		RouteFromRootWindow = 131072,
		Tooltip = 262144,
	}

	[Flags]
	public enum ImGuiInputTextFlags
	{
		None = 0,
		CharsDecimal = 1,
		CharsHexadecimal = 2,
		CharsScientific = 4,
		CharsUppercase = 8,
		CharsNoBlank = 16,
		AllowTabInput = 32,
		EnterReturnsTrue = 64,
		EscapeClearsAll = 128,
		CtrlEnterForNewLine = 256,
		ReadOnly = 512,
		Password = 1024,
		AlwaysOverwrite = 2048,
		AutoSelectAll = 4096,
		ParseEmptyRefVal = 8192,
		DisplayEmptyRefVal = 16384,
		NoHorizontalScroll = 32768,
		NoUndoRedo = 65536,
		ElideLeft = 131072,
		CallbackCompletion = 262144,
		CallbackHistory = 524288,
		CallbackAlways = 1048576,
		CallbackCharFilter = 2097152,
		CallbackResize = 4194304,
		CallbackEdit = 8388608,
	}

	[Flags]
	public enum ImGuiItemFlags
	{
		None = 0,
		NoTabStop = 1,
		NoNav = 2,
		NoNavDefaultFocus = 4,
		ButtonRepeat = 8,
		AutoClosePopups = 16,
		AllowDuplicateId = 32,
	}

	public enum ImGuiKey
	{
		None = 0,
		NamedKey_BEGIN = 512,
		Tab = 512,
		LeftArrow = 513,
		RightArrow = 514,
		UpArrow = 515,
		DownArrow = 516,
		PageUp = 517,
		PageDown = 518,
		Home = 519,
		End = 520,
		Insert = 521,
		Delete = 522,
		Backspace = 523,
		Space = 524,
		Enter = 525,
		Escape = 526,
		LeftCtrl = 527,
		LeftShift = 528,
		LeftAlt = 529,
		LeftSuper = 530,
		RightCtrl = 531,
		RightShift = 532,
		RightAlt = 533,
		RightSuper = 534,
		Menu = 535,
		_0 = 536,
		_1 = 537,
		_2 = 538,
		_3 = 539,
		_4 = 540,
		_5 = 541,
		_6 = 542,
		_7 = 543,
		_8 = 544,
		_9 = 545,
		A = 546,
		B = 547,
		C = 548,
		D = 549,
		E = 550,
		F = 551,
		G = 552,
		H = 553,
		I = 554,
		J = 555,
		K = 556,
		L = 557,
		M = 558,
		N = 559,
		O = 560,
		P = 561,
		Q = 562,
		R = 563,
		S = 564,
		T = 565,
		U = 566,
		V = 567,
		W = 568,
		X = 569,
		Y = 570,
		Z = 571,
		F1 = 572,
		F2 = 573,
		F3 = 574,
		F4 = 575,
		F5 = 576,
		F6 = 577,
		F7 = 578,
		F8 = 579,
		F9 = 580,
		F10 = 581,
		F11 = 582,
		F12 = 583,
		F13 = 584,
		F14 = 585,
		F15 = 586,
		F16 = 587,
		F17 = 588,
		F18 = 589,
		F19 = 590,
		F20 = 591,
		F21 = 592,
		F22 = 593,
		F23 = 594,
		F24 = 595,
		Apostrophe = 596,
		Comma = 597,
		Minus = 598,
		Period = 599,
		Slash = 600,
		Semicolon = 601,
		Equal = 602,
		LeftBracket = 603,
		Backslash = 604,
		RightBracket = 605,
		GraveAccent = 606,
		CapsLock = 607,
		ScrollLock = 608,
		NumLock = 609,
		PrintScreen = 610,
		Pause = 611,
		Keypad0 = 612,
		Keypad1 = 613,
		Keypad2 = 614,
		Keypad3 = 615,
		Keypad4 = 616,
		Keypad5 = 617,
		Keypad6 = 618,
		Keypad7 = 619,
		Keypad8 = 620,
		Keypad9 = 621,
		KeypadDecimal = 622,
		KeypadDivide = 623,
		KeypadMultiply = 624,
		KeypadSubtract = 625,
		KeypadAdd = 626,
		KeypadEnter = 627,
		KeypadEqual = 628,
		AppBack = 629,
		AppForward = 630,
		Oem102 = 631,
		GamepadStart = 632,
		GamepadBack = 633,
		GamepadFaceLeft = 634,
		GamepadFaceRight = 635,
		GamepadFaceUp = 636,
		GamepadFaceDown = 637,
		GamepadDpadLeft = 638,
		GamepadDpadRight = 639,
		GamepadDpadUp = 640,
		GamepadDpadDown = 641,
		GamepadL1 = 642,
		GamepadR1 = 643,
		GamepadL2 = 644,
		GamepadR2 = 645,
		GamepadL3 = 646,
		GamepadR3 = 647,
		GamepadLStickLeft = 648,
		GamepadLStickRight = 649,
		GamepadLStickUp = 650,
		GamepadLStickDown = 651,
		GamepadRStickLeft = 652,
		GamepadRStickRight = 653,
		GamepadRStickUp = 654,
		GamepadRStickDown = 655,
		MouseLeft = 656,
		MouseRight = 657,
		MouseMiddle = 658,
		MouseX1 = 659,
		MouseX2 = 660,
		MouseWheelX = 661,
		MouseWheelY = 662,
		ReservedForModCtrl = 663,
		ReservedForModShift = 664,
		ReservedForModAlt = 665,
		ReservedForModSuper = 666,
		NamedKey_END = 667,
		NamedKey_COUNT = 155,
		ImGuiMod_None = 0,
		ImGuiMod_Ctrl = 4096,
		ImGuiMod_Shift = 8192,
		ImGuiMod_Alt = 16384,
		ImGuiMod_Super = 32768,
		ImGuiMod_Mask_ = 61440,
	}

	public enum ImGuiMouseButton
	{
		Left = 0,
		Right = 1,
		Middle = 2,
		COUNT = 5,
	}

	public enum ImGuiMouseCursor
	{
		None = -1,
		Arrow = 0,
		TextInput = 1,
		ResizeAll = 2,
		ResizeNS = 3,
		ResizeEW = 4,
		ResizeNESW = 5,
		ResizeNWSE = 6,
		Hand = 7,
		Wait = 8,
		Progress = 9,
		NotAllowed = 10,
		COUNT = 11,
	}

	public enum ImGuiMouseSource
	{
		Mouse = 0,
		TouchScreen = 1,
		Pen = 2,
		COUNT = 3,
	}

	[Flags]
	public enum ImGuiMultiSelectFlags
	{
		None = 0,
		SingleSelect = 1,
		NoSelectAll = 2,
		NoRangeSelect = 4,
		NoAutoSelect = 8,
		NoAutoClear = 16,
		NoAutoClearOnReselect = 32,
		BoxSelect1d = 64,
		BoxSelect2d = 128,
		BoxSelectNoScroll = 256,
		ClearOnEscape = 512,
		ClearOnClickVoid = 1024,
		ScopeWindow = 2048,
		ScopeRect = 4096,
		SelectOnClick = 8192,
		SelectOnClickRelease = 16384,
		NavWrapX = 65536,
	}

	[Flags]
	public enum ImGuiPopupFlags
	{
		None = 0,
		MouseButtonLeft = 0,
		MouseButtonRight = 1,
		MouseButtonMiddle = 2,
		MouseButtonMask_ = 31,
		MouseButtonDefault_ = 1,
		NoReopen = 32,
		NoOpenOverExistingPopup = 128,
		NoOpenOverItems = 256,
		AnyPopupId = 1024,
		AnyPopupLevel = 2048,
		AnyPopup = 3072,
	}

	[Flags]
	public enum ImGuiSelectableFlags
	{
		None = 0,
		NoAutoClosePopups = 1,
		SpanAllColumns = 2,
		AllowDoubleClick = 4,
		Disabled = 8,
		AllowOverlap = 16,
		Highlight = 32,
	}

	public enum ImGuiSelectionRequestType
	{
		None = 0,
		SetAll = 1,
		SetRange = 2,
	}

	[Flags]
	public enum ImGuiSliderFlags
	{
		None = 0,
		Logarithmic = 32,
		NoRoundToFormat = 64,
		NoInput = 128,
		WrapAround = 256,
		ClampOnInput = 512,
		ClampZeroRange = 1024,
		NoSpeedTweaks = 2048,
		AlwaysClamp = 1536,
		InvalidMask_ = 1879048207,
	}

	public enum ImGuiSortDirection
	{
		None = 0,
		Ascending = 1,
		Descending = 2,
	}

	public enum ImGuiStyleVar
	{
		Alpha = 0,
		DisabledAlpha = 1,
		WindowPadding = 2,
		WindowRounding = 3,
		WindowBorderSize = 4,
		WindowMinSize = 5,
		WindowTitleAlign = 6,
		ChildRounding = 7,
		ChildBorderSize = 8,
		PopupRounding = 9,
		PopupBorderSize = 10,
		FramePadding = 11,
		FrameRounding = 12,
		FrameBorderSize = 13,
		ItemSpacing = 14,
		ItemInnerSpacing = 15,
		IndentSpacing = 16,
		CellPadding = 17,
		ScrollbarSize = 18,
		ScrollbarRounding = 19,
		GrabMinSize = 20,
		GrabRounding = 21,
		ImageBorderSize = 22,
		TabRounding = 23,
		TabBorderSize = 24,
		TabMinWidthBase = 25,
		TabMinWidthShrink = 26,
		TabBarBorderSize = 27,
		TabBarOverlineSize = 28,
		TableAngledHeadersAngle = 29,
		TableAngledHeadersTextAlign = 30,
		TreeLinesSize = 31,
		TreeLinesRounding = 32,
		ButtonTextAlign = 33,
		SelectableTextAlign = 34,
		SeparatorTextBorderSize = 35,
		SeparatorTextAlign = 36,
		SeparatorTextPadding = 37,
		DockingSeparatorSize = 38,
		COUNT = 39,
	}

	[Flags]
	public enum ImGuiTabBarFlags
	{
		None = 0,
		Reorderable = 1,
		AutoSelectNewTabs = 2,
		TabListPopupButton = 4,
		NoCloseWithMiddleMouseButton = 8,
		NoTabListScrollingButtons = 16,
		NoTooltip = 32,
		DrawSelectedOverline = 64,
		FittingPolicyMixed = 128,
		FittingPolicyShrink = 256,
		FittingPolicyScroll = 512,
		FittingPolicyMask_ = 896,
		FittingPolicyDefault_ = 128,
	}

	[Flags]
	public enum ImGuiTabItemFlags
	{
		None = 0,
		UnsavedDocument = 1,
		SetSelected = 2,
		NoCloseWithMiddleMouseButton = 4,
		NoPushId = 8,
		NoTooltip = 16,
		NoReorder = 32,
		Leading = 64,
		Trailing = 128,
		NoAssumedClosure = 256,
	}

	public enum ImGuiTableBgTarget
	{
		None = 0,
		RowBg0 = 1,
		RowBg1 = 2,
		CellBg = 3,
	}

	[Flags]
	public enum ImGuiTableColumnFlags
	{
		None = 0,
		Disabled = 1,
		DefaultHide = 2,
		DefaultSort = 4,
		WidthStretch = 8,
		WidthFixed = 16,
		NoResize = 32,
		NoReorder = 64,
		NoHide = 128,
		NoClip = 256,
		NoSort = 512,
		NoSortAscending = 1024,
		NoSortDescending = 2048,
		NoHeaderLabel = 4096,
		NoHeaderWidth = 8192,
		PreferSortAscending = 16384,
		PreferSortDescending = 32768,
		IndentEnable = 65536,
		IndentDisable = 131072,
		AngledHeader = 262144,
		IsEnabled = 16777216,
		IsVisible = 33554432,
		IsSorted = 67108864,
		IsHovered = 134217728,
		WidthMask_ = 24,
		IndentMask_ = 196608,
		StatusMask_ = 251658240,
		NoDirectResize_ = 1073741824,
	}

	[Flags]
	public enum ImGuiTableFlags
	{
		None = 0,
		Resizable = 1,
		Reorderable = 2,
		Hideable = 4,
		Sortable = 8,
		NoSavedSettings = 16,
		ContextMenuInBody = 32,
		RowBg = 64,
		BordersInnerH = 128,
		BordersOuterH = 256,
		BordersInnerV = 512,
		BordersOuterV = 1024,
		BordersH = 384,
		BordersV = 1536,
		BordersInner = 640,
		BordersOuter = 1280,
		Borders = 1920,
		NoBordersInBody = 2048,
		NoBordersInBodyUntilResize = 4096,
		SizingFixedFit = 8192,
		SizingFixedSame = 16384,
		SizingStretchProp = 24576,
		SizingStretchSame = 32768,
		NoHostExtendX = 65536,
		NoHostExtendY = 131072,
		NoKeepColumnsVisible = 262144,
		PreciseWidths = 524288,
		NoClip = 1048576,
		PadOuterX = 2097152,
		NoPadOuterX = 4194304,
		NoPadInnerX = 8388608,
		ScrollX = 16777216,
		ScrollY = 33554432,
		SortMulti = 67108864,
		SortTristate = 134217728,
		HighlightHoveredColumn = 268435456,
		SizingMask_ = 57344,
	}

	[Flags]
	public enum ImGuiTableRowFlags
	{
		None = 0,
		Headers = 1,
	}

	[Flags]
	public enum ImGuiTreeNodeFlags
	{
		None = 0,
		Selected = 1,
		Framed = 2,
		AllowOverlap = 4,
		NoTreePushOnOpen = 8,
		NoAutoOpenOnLog = 16,
		DefaultOpen = 32,
		OpenOnDoubleClick = 64,
		OpenOnArrow = 128,
		Leaf = 256,
		Bullet = 512,
		FramePadding = 1024,
		SpanAvailWidth = 2048,
		SpanFullWidth = 4096,
		SpanLabelWidth = 8192,
		SpanAllColumns = 16384,
		LabelSpanAllColumns = 32768,
		NavLeftJumpsToParent = 131072,
		CollapsingHeader = 26,
		DrawLinesNone = 262144,
		DrawLinesFull = 524288,
		DrawLinesToNodes = 1048576,
	}

	[Flags]
	public enum ImGuiViewportFlags
	{
		None = 0,
		IsPlatformWindow = 1,
		IsPlatformMonitor = 2,
		OwnedByApp = 4,
		NoDecoration = 8,
		NoTaskBarIcon = 16,
		NoFocusOnAppearing = 32,
		NoFocusOnClick = 64,
		NoInputs = 128,
		NoRendererClear = 256,
		NoAutoMerge = 512,
		TopMost = 1024,
		CanHostOtherWindows = 2048,
		IsMinimized = 4096,
		IsFocused = 8192,
	}

	[Flags]
	public enum ImGuiWindowFlags
	{
		None = 0,
		NoTitleBar = 1,
		NoResize = 2,
		NoMove = 4,
		NoScrollbar = 8,
		NoScrollWithMouse = 16,
		NoCollapse = 32,
		AlwaysAutoResize = 64,
		NoBackground = 128,
		NoSavedSettings = 256,
		NoMouseInputs = 512,
		MenuBar = 1024,
		HorizontalScrollbar = 2048,
		NoFocusOnAppearing = 4096,
		NoBringToFrontOnFocus = 8192,
		AlwaysVerticalScrollbar = 16384,
		AlwaysHorizontalScrollbar = 32768,
		NoNavInputs = 65536,
		NoNavFocus = 131072,
		UnsavedDocument = 262144,
		NoDocking = 524288,
		NoNav = 196608,
		NoDecoration = 43,
		NoInputs = 197120,
		DockNodeHost = 8388608,
		ChildWindow = 16777216,
		Tooltip = 33554432,
		Popup = 67108864,
		Modal = 134217728,
		ChildMenu = 268435456,
	}

	public enum ImTextureFormat
	{
		RGBA32 = 0,
		Alpha8 = 1,
	}

	public enum ImTextureStatus
	{
		OK = 0,
		Destroyed = 1,
		WantCreate = 2,
		WantUpdates = 3,
		WantDestroy = 4,
	}

}
