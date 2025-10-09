// machine generated, do not edit
using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;

namespace Sokol
{
public static unsafe partial class SApp
{
public const int SAPP_MAX_TOUCHPOINTS = 8;
public const int SAPP_MAX_MOUSEBUTTONS = 3;
public const int SAPP_MAX_KEYCODES = 512;
public const int SAPP_MAX_ICONIMAGES = 8;
public enum sapp_event_type
{
    SAPP_EVENTTYPE_INVALID,
    SAPP_EVENTTYPE_KEY_DOWN,
    SAPP_EVENTTYPE_KEY_UP,
    SAPP_EVENTTYPE_CHAR,
    SAPP_EVENTTYPE_MOUSE_DOWN,
    SAPP_EVENTTYPE_MOUSE_UP,
    SAPP_EVENTTYPE_MOUSE_SCROLL,
    SAPP_EVENTTYPE_MOUSE_MOVE,
    SAPP_EVENTTYPE_MOUSE_ENTER,
    SAPP_EVENTTYPE_MOUSE_LEAVE,
    SAPP_EVENTTYPE_TOUCHES_BEGAN,
    SAPP_EVENTTYPE_TOUCHES_MOVED,
    SAPP_EVENTTYPE_TOUCHES_ENDED,
    SAPP_EVENTTYPE_TOUCHES_CANCELLED,
    SAPP_EVENTTYPE_RESIZED,
    SAPP_EVENTTYPE_ICONIFIED,
    SAPP_EVENTTYPE_RESTORED,
    SAPP_EVENTTYPE_FOCUSED,
    SAPP_EVENTTYPE_UNFOCUSED,
    SAPP_EVENTTYPE_SUSPENDED,
    SAPP_EVENTTYPE_RESUMED,
    SAPP_EVENTTYPE_QUIT_REQUESTED,
    SAPP_EVENTTYPE_CLIPBOARD_PASTED,
    SAPP_EVENTTYPE_FILES_DROPPED,
    _SAPP_EVENTTYPE_NUM,
    _SAPP_EVENTTYPE_FORCE_U32 = 2147483647,
}
public enum sapp_keycode
{
    SAPP_KEYCODE_INVALID = 0,
    SAPP_KEYCODE_SPACE = 32,
    SAPP_KEYCODE_APOSTROPHE = 39,
    SAPP_KEYCODE_COMMA = 44,
    SAPP_KEYCODE_MINUS = 45,
    SAPP_KEYCODE_PERIOD = 46,
    SAPP_KEYCODE_SLASH = 47,
    SAPP_KEYCODE_0 = 48,
    SAPP_KEYCODE_1 = 49,
    SAPP_KEYCODE_2 = 50,
    SAPP_KEYCODE_3 = 51,
    SAPP_KEYCODE_4 = 52,
    SAPP_KEYCODE_5 = 53,
    SAPP_KEYCODE_6 = 54,
    SAPP_KEYCODE_7 = 55,
    SAPP_KEYCODE_8 = 56,
    SAPP_KEYCODE_9 = 57,
    SAPP_KEYCODE_SEMICOLON = 59,
    SAPP_KEYCODE_EQUAL = 61,
    SAPP_KEYCODE_A = 65,
    SAPP_KEYCODE_B = 66,
    SAPP_KEYCODE_C = 67,
    SAPP_KEYCODE_D = 68,
    SAPP_KEYCODE_E = 69,
    SAPP_KEYCODE_F = 70,
    SAPP_KEYCODE_G = 71,
    SAPP_KEYCODE_H = 72,
    SAPP_KEYCODE_I = 73,
    SAPP_KEYCODE_J = 74,
    SAPP_KEYCODE_K = 75,
    SAPP_KEYCODE_L = 76,
    SAPP_KEYCODE_M = 77,
    SAPP_KEYCODE_N = 78,
    SAPP_KEYCODE_O = 79,
    SAPP_KEYCODE_P = 80,
    SAPP_KEYCODE_Q = 81,
    SAPP_KEYCODE_R = 82,
    SAPP_KEYCODE_S = 83,
    SAPP_KEYCODE_T = 84,
    SAPP_KEYCODE_U = 85,
    SAPP_KEYCODE_V = 86,
    SAPP_KEYCODE_W = 87,
    SAPP_KEYCODE_X = 88,
    SAPP_KEYCODE_Y = 89,
    SAPP_KEYCODE_Z = 90,
    SAPP_KEYCODE_LEFT_BRACKET = 91,
    SAPP_KEYCODE_BACKSLASH = 92,
    SAPP_KEYCODE_RIGHT_BRACKET = 93,
    SAPP_KEYCODE_GRAVE_ACCENT = 96,
    SAPP_KEYCODE_WORLD_1 = 161,
    SAPP_KEYCODE_WORLD_2 = 162,
    SAPP_KEYCODE_ESCAPE = 256,
    SAPP_KEYCODE_ENTER = 257,
    SAPP_KEYCODE_TAB = 258,
    SAPP_KEYCODE_BACKSPACE = 259,
    SAPP_KEYCODE_INSERT = 260,
    SAPP_KEYCODE_DELETE = 261,
    SAPP_KEYCODE_RIGHT = 262,
    SAPP_KEYCODE_LEFT = 263,
    SAPP_KEYCODE_DOWN = 264,
    SAPP_KEYCODE_UP = 265,
    SAPP_KEYCODE_PAGE_UP = 266,
    SAPP_KEYCODE_PAGE_DOWN = 267,
    SAPP_KEYCODE_HOME = 268,
    SAPP_KEYCODE_END = 269,
    SAPP_KEYCODE_CAPS_LOCK = 280,
    SAPP_KEYCODE_SCROLL_LOCK = 281,
    SAPP_KEYCODE_NUM_LOCK = 282,
    SAPP_KEYCODE_PRINT_SCREEN = 283,
    SAPP_KEYCODE_PAUSE = 284,
    SAPP_KEYCODE_F1 = 290,
    SAPP_KEYCODE_F2 = 291,
    SAPP_KEYCODE_F3 = 292,
    SAPP_KEYCODE_F4 = 293,
    SAPP_KEYCODE_F5 = 294,
    SAPP_KEYCODE_F6 = 295,
    SAPP_KEYCODE_F7 = 296,
    SAPP_KEYCODE_F8 = 297,
    SAPP_KEYCODE_F9 = 298,
    SAPP_KEYCODE_F10 = 299,
    SAPP_KEYCODE_F11 = 300,
    SAPP_KEYCODE_F12 = 301,
    SAPP_KEYCODE_F13 = 302,
    SAPP_KEYCODE_F14 = 303,
    SAPP_KEYCODE_F15 = 304,
    SAPP_KEYCODE_F16 = 305,
    SAPP_KEYCODE_F17 = 306,
    SAPP_KEYCODE_F18 = 307,
    SAPP_KEYCODE_F19 = 308,
    SAPP_KEYCODE_F20 = 309,
    SAPP_KEYCODE_F21 = 310,
    SAPP_KEYCODE_F22 = 311,
    SAPP_KEYCODE_F23 = 312,
    SAPP_KEYCODE_F24 = 313,
    SAPP_KEYCODE_F25 = 314,
    SAPP_KEYCODE_KP_0 = 320,
    SAPP_KEYCODE_KP_1 = 321,
    SAPP_KEYCODE_KP_2 = 322,
    SAPP_KEYCODE_KP_3 = 323,
    SAPP_KEYCODE_KP_4 = 324,
    SAPP_KEYCODE_KP_5 = 325,
    SAPP_KEYCODE_KP_6 = 326,
    SAPP_KEYCODE_KP_7 = 327,
    SAPP_KEYCODE_KP_8 = 328,
    SAPP_KEYCODE_KP_9 = 329,
    SAPP_KEYCODE_KP_DECIMAL = 330,
    SAPP_KEYCODE_KP_DIVIDE = 331,
    SAPP_KEYCODE_KP_MULTIPLY = 332,
    SAPP_KEYCODE_KP_SUBTRACT = 333,
    SAPP_KEYCODE_KP_ADD = 334,
    SAPP_KEYCODE_KP_ENTER = 335,
    SAPP_KEYCODE_KP_EQUAL = 336,
    SAPP_KEYCODE_LEFT_SHIFT = 340,
    SAPP_KEYCODE_LEFT_CONTROL = 341,
    SAPP_KEYCODE_LEFT_ALT = 342,
    SAPP_KEYCODE_LEFT_SUPER = 343,
    SAPP_KEYCODE_RIGHT_SHIFT = 344,
    SAPP_KEYCODE_RIGHT_CONTROL = 345,
    SAPP_KEYCODE_RIGHT_ALT = 346,
    SAPP_KEYCODE_RIGHT_SUPER = 347,
    SAPP_KEYCODE_MENU = 348,
}
public enum sapp_android_tooltype
{
    SAPP_ANDROIDTOOLTYPE_UNKNOWN = 0,
    SAPP_ANDROIDTOOLTYPE_FINGER = 1,
    SAPP_ANDROIDTOOLTYPE_STYLUS = 2,
    SAPP_ANDROIDTOOLTYPE_MOUSE = 3,
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_touchpoint
{
    public nuint identifier;
    public float pos_x;
    public float pos_y;
    public sapp_android_tooltype android_tooltype;
#if WEB
    private byte _changed;
    public bool changed { get => _changed != 0; set => _changed = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool changed;
#endif
}
public enum sapp_mousebutton
{
    SAPP_MOUSEBUTTON_LEFT = 0,
    SAPP_MOUSEBUTTON_RIGHT = 1,
    SAPP_MOUSEBUTTON_MIDDLE = 2,
    SAPP_MOUSEBUTTON_INVALID = 256,
}
public const int SAPP_MODIFIER_SHIFT = 1;
public const int SAPP_MODIFIER_CTRL = 2;
public const int SAPP_MODIFIER_ALT = 4;
public const int SAPP_MODIFIER_SUPER = 8;
public const int SAPP_MODIFIER_LMB = 256;
public const int SAPP_MODIFIER_RMB = 512;
public const int SAPP_MODIFIER_MMB = 1024;
[StructLayout(LayoutKind.Sequential)]
public struct sapp_event
{
    public ulong frame_count;
    public sapp_event_type type;
    public sapp_keycode key_code;
    public uint char_code;
#if WEB
    private byte _key_repeat;
    public bool key_repeat { get => _key_repeat != 0; set => _key_repeat = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool key_repeat;
#endif
    public uint modifiers;
    public sapp_mousebutton mouse_button;
    public float mouse_x;
    public float mouse_y;
    public float mouse_dx;
    public float mouse_dy;
    public float scroll_x;
    public float scroll_y;
    public int num_touches;
    #pragma warning disable 169
    public struct touchesCollection
    {
        public ref sapp_touchpoint this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sapp_touchpoint _item0;
        private sapp_touchpoint _item1;
        private sapp_touchpoint _item2;
        private sapp_touchpoint _item3;
        private sapp_touchpoint _item4;
        private sapp_touchpoint _item5;
        private sapp_touchpoint _item6;
        private sapp_touchpoint _item7;
    }
    #pragma warning restore 169
    public touchesCollection touches;
    public int window_width;
    public int window_height;
    public int framebuffer_width;
    public int framebuffer_height;
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_range
{
    public void* ptr;
    public nuint size;
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_image_desc
{
    public int width;
    public int height;
    public int cursor_hotspot_x;
    public int cursor_hotspot_y;
    public sapp_range pixels;
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_icon_desc
{
#if WEB
    private byte _sokol_default;
    public bool sokol_default { get => _sokol_default != 0; set => _sokol_default = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool sokol_default;
#endif
    #pragma warning disable 169
    public struct imagesCollection
    {
        public ref sapp_image_desc this[int index] => ref MemoryMarshal.CreateSpan(ref _item0, 8)[index];
        private sapp_image_desc _item0;
        private sapp_image_desc _item1;
        private sapp_image_desc _item2;
        private sapp_image_desc _item3;
        private sapp_image_desc _item4;
        private sapp_image_desc _item5;
        private sapp_image_desc _item6;
        private sapp_image_desc _item7;
    }
    #pragma warning restore 169
    public imagesCollection images;
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_allocator
{
    public delegate* unmanaged<nuint, void*, void*> alloc_fn;
    public delegate* unmanaged<void*, void*, void> free_fn;
    public void* user_data;
}
public enum sapp_log_item
{
    SAPP_LOGITEM_OK,
    SAPP_LOGITEM_MALLOC_FAILED,
    SAPP_LOGITEM_MACOS_INVALID_NSOPENGL_PROFILE,
    SAPP_LOGITEM_WIN32_LOAD_OPENGL32_DLL_FAILED,
    SAPP_LOGITEM_WIN32_CREATE_HELPER_WINDOW_FAILED,
    SAPP_LOGITEM_WIN32_HELPER_WINDOW_GETDC_FAILED,
    SAPP_LOGITEM_WIN32_DUMMY_CONTEXT_SET_PIXELFORMAT_FAILED,
    SAPP_LOGITEM_WIN32_CREATE_DUMMY_CONTEXT_FAILED,
    SAPP_LOGITEM_WIN32_DUMMY_CONTEXT_MAKE_CURRENT_FAILED,
    SAPP_LOGITEM_WIN32_GET_PIXELFORMAT_ATTRIB_FAILED,
    SAPP_LOGITEM_WIN32_WGL_FIND_PIXELFORMAT_FAILED,
    SAPP_LOGITEM_WIN32_WGL_DESCRIBE_PIXELFORMAT_FAILED,
    SAPP_LOGITEM_WIN32_WGL_SET_PIXELFORMAT_FAILED,
    SAPP_LOGITEM_WIN32_WGL_ARB_CREATE_CONTEXT_REQUIRED,
    SAPP_LOGITEM_WIN32_WGL_ARB_CREATE_CONTEXT_PROFILE_REQUIRED,
    SAPP_LOGITEM_WIN32_WGL_OPENGL_VERSION_NOT_SUPPORTED,
    SAPP_LOGITEM_WIN32_WGL_OPENGL_PROFILE_NOT_SUPPORTED,
    SAPP_LOGITEM_WIN32_WGL_INCOMPATIBLE_DEVICE_CONTEXT,
    SAPP_LOGITEM_WIN32_WGL_CREATE_CONTEXT_ATTRIBS_FAILED_OTHER,
    SAPP_LOGITEM_WIN32_D3D11_CREATE_DEVICE_AND_SWAPCHAIN_WITH_DEBUG_FAILED,
    SAPP_LOGITEM_WIN32_D3D11_GET_IDXGIFACTORY_FAILED,
    SAPP_LOGITEM_WIN32_D3D11_GET_IDXGIADAPTER_FAILED,
    SAPP_LOGITEM_WIN32_D3D11_QUERY_INTERFACE_IDXGIDEVICE1_FAILED,
    SAPP_LOGITEM_WIN32_REGISTER_RAW_INPUT_DEVICES_FAILED_MOUSE_LOCK,
    SAPP_LOGITEM_WIN32_REGISTER_RAW_INPUT_DEVICES_FAILED_MOUSE_UNLOCK,
    SAPP_LOGITEM_WIN32_GET_RAW_INPUT_DATA_FAILED,
    SAPP_LOGITEM_WIN32_DESTROYICON_FOR_CURSOR_FAILED,
    SAPP_LOGITEM_LINUX_GLX_LOAD_LIBGL_FAILED,
    SAPP_LOGITEM_LINUX_GLX_LOAD_ENTRY_POINTS_FAILED,
    SAPP_LOGITEM_LINUX_GLX_EXTENSION_NOT_FOUND,
    SAPP_LOGITEM_LINUX_GLX_QUERY_VERSION_FAILED,
    SAPP_LOGITEM_LINUX_GLX_VERSION_TOO_LOW,
    SAPP_LOGITEM_LINUX_GLX_NO_GLXFBCONFIGS,
    SAPP_LOGITEM_LINUX_GLX_NO_SUITABLE_GLXFBCONFIG,
    SAPP_LOGITEM_LINUX_GLX_GET_VISUAL_FROM_FBCONFIG_FAILED,
    SAPP_LOGITEM_LINUX_GLX_REQUIRED_EXTENSIONS_MISSING,
    SAPP_LOGITEM_LINUX_GLX_CREATE_CONTEXT_FAILED,
    SAPP_LOGITEM_LINUX_GLX_CREATE_WINDOW_FAILED,
    SAPP_LOGITEM_LINUX_X11_CREATE_WINDOW_FAILED,
    SAPP_LOGITEM_LINUX_EGL_BIND_OPENGL_API_FAILED,
    SAPP_LOGITEM_LINUX_EGL_BIND_OPENGL_ES_API_FAILED,
    SAPP_LOGITEM_LINUX_EGL_GET_DISPLAY_FAILED,
    SAPP_LOGITEM_LINUX_EGL_INITIALIZE_FAILED,
    SAPP_LOGITEM_LINUX_EGL_NO_CONFIGS,
    SAPP_LOGITEM_LINUX_EGL_NO_NATIVE_VISUAL,
    SAPP_LOGITEM_LINUX_EGL_GET_VISUAL_INFO_FAILED,
    SAPP_LOGITEM_LINUX_EGL_CREATE_WINDOW_SURFACE_FAILED,
    SAPP_LOGITEM_LINUX_EGL_CREATE_CONTEXT_FAILED,
    SAPP_LOGITEM_LINUX_EGL_MAKE_CURRENT_FAILED,
    SAPP_LOGITEM_LINUX_X11_OPEN_DISPLAY_FAILED,
    SAPP_LOGITEM_LINUX_X11_QUERY_SYSTEM_DPI_FAILED,
    SAPP_LOGITEM_LINUX_X11_DROPPED_FILE_URI_WRONG_SCHEME,
    SAPP_LOGITEM_LINUX_X11_FAILED_TO_BECOME_OWNER_OF_CLIPBOARD,
    SAPP_LOGITEM_ANDROID_UNSUPPORTED_INPUT_EVENT_INPUT_CB,
    SAPP_LOGITEM_ANDROID_UNSUPPORTED_INPUT_EVENT_MAIN_CB,
    SAPP_LOGITEM_ANDROID_READ_MSG_FAILED,
    SAPP_LOGITEM_ANDROID_WRITE_MSG_FAILED,
    SAPP_LOGITEM_ANDROID_MSG_CREATE,
    SAPP_LOGITEM_ANDROID_MSG_RESUME,
    SAPP_LOGITEM_ANDROID_MSG_PAUSE,
    SAPP_LOGITEM_ANDROID_MSG_FOCUS,
    SAPP_LOGITEM_ANDROID_MSG_NO_FOCUS,
    SAPP_LOGITEM_ANDROID_MSG_SET_NATIVE_WINDOW,
    SAPP_LOGITEM_ANDROID_MSG_SET_INPUT_QUEUE,
    SAPP_LOGITEM_ANDROID_MSG_DESTROY,
    SAPP_LOGITEM_ANDROID_UNKNOWN_MSG,
    SAPP_LOGITEM_ANDROID_LOOP_THREAD_STARTED,
    SAPP_LOGITEM_ANDROID_LOOP_THREAD_DONE,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONSTART,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONRESUME,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONSAVEINSTANCESTATE,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONWINDOWFOCUSCHANGED,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONPAUSE,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONSTOP,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONNATIVEWINDOWCREATED,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONNATIVEWINDOWDESTROYED,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONINPUTQUEUECREATED,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONINPUTQUEUEDESTROYED,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONCONFIGURATIONCHANGED,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONLOWMEMORY,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONDESTROY,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_DONE,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_ONCREATE,
    SAPP_LOGITEM_ANDROID_CREATE_THREAD_PIPE_FAILED,
    SAPP_LOGITEM_ANDROID_NATIVE_ACTIVITY_CREATE_SUCCESS,
    SAPP_LOGITEM_WGPU_DEVICE_LOST,
    SAPP_LOGITEM_WGPU_DEVICE_LOG,
    SAPP_LOGITEM_WGPU_DEVICE_UNCAPTURED_ERROR,
    SAPP_LOGITEM_WGPU_SWAPCHAIN_CREATE_SURFACE_FAILED,
    SAPP_LOGITEM_WGPU_SWAPCHAIN_SURFACE_GET_CAPABILITIES_FAILED,
    SAPP_LOGITEM_WGPU_SWAPCHAIN_CREATE_DEPTH_STENCIL_TEXTURE_FAILED,
    SAPP_LOGITEM_WGPU_SWAPCHAIN_CREATE_DEPTH_STENCIL_VIEW_FAILED,
    SAPP_LOGITEM_WGPU_SWAPCHAIN_CREATE_MSAA_TEXTURE_FAILED,
    SAPP_LOGITEM_WGPU_SWAPCHAIN_CREATE_MSAA_VIEW_FAILED,
    SAPP_LOGITEM_WGPU_SWAPCHAIN_GETCURRENTTEXTURE_FAILED,
    SAPP_LOGITEM_WGPU_REQUEST_DEVICE_STATUS_ERROR,
    SAPP_LOGITEM_WGPU_REQUEST_DEVICE_STATUS_UNKNOWN,
    SAPP_LOGITEM_WGPU_REQUEST_ADAPTER_STATUS_UNAVAILABLE,
    SAPP_LOGITEM_WGPU_REQUEST_ADAPTER_STATUS_ERROR,
    SAPP_LOGITEM_WGPU_REQUEST_ADAPTER_STATUS_UNKNOWN,
    SAPP_LOGITEM_WGPU_CREATE_INSTANCE_FAILED,
    SAPP_LOGITEM_IMAGE_DATA_SIZE_MISMATCH,
    SAPP_LOGITEM_DROPPED_FILE_PATH_TOO_LONG,
    SAPP_LOGITEM_CLIPBOARD_STRING_TOO_BIG,
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_logger
{
    public delegate* unmanaged<byte*, uint, uint, byte*, uint, byte*, void*, void> func;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_desc
{
    public delegate* unmanaged<void> init_cb;
    public delegate* unmanaged<void> frame_cb;
    public delegate* unmanaged<void> cleanup_cb;
    public delegate* unmanaged<sapp_event*, void> event_cb;
    public void* user_data;
    public delegate* unmanaged<void*, void> init_userdata_cb;
    public delegate* unmanaged<void*, void> frame_userdata_cb;
    public delegate* unmanaged<void*, void> cleanup_userdata_cb;
    public delegate* unmanaged<sapp_event*, void*, void> event_userdata_cb;
    public int width;
    public int height;
    public int sample_count;
    public int swap_interval;
#if WEB
    private byte _high_dpi;
    public bool high_dpi { get => _high_dpi != 0; set => _high_dpi = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool high_dpi;
#endif
#if WEB
    private byte _fullscreen;
    public bool fullscreen { get => _fullscreen != 0; set => _fullscreen = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool fullscreen;
#endif
#if WEB
    private byte _alpha;
    public bool alpha { get => _alpha != 0; set => _alpha = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool alpha;
#endif
#if WEB
    private IntPtr _window_title;
    public string window_title { get => Marshal.PtrToStringAnsi(_window_title);  set { if (_window_title != IntPtr.Zero) { Marshal.FreeHGlobal(_window_title); _window_title = IntPtr.Zero; } if (value != null) { _window_title = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string window_title;
#endif
#if WEB
    private byte _enable_clipboard;
    public bool enable_clipboard { get => _enable_clipboard != 0; set => _enable_clipboard = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enable_clipboard;
#endif
    public int clipboard_size;
#if WEB
    private byte _enable_dragndrop;
    public bool enable_dragndrop { get => _enable_dragndrop != 0; set => _enable_dragndrop = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool enable_dragndrop;
#endif
    public int max_dropped_files;
    public int max_dropped_file_path_length;
    public sapp_icon_desc icon;
    public sapp_allocator allocator;
    public sapp_logger logger;
    public int gl_major_version;
    public int gl_minor_version;
#if WEB
    private byte _win32_console_utf8;
    public bool win32_console_utf8 { get => _win32_console_utf8 != 0; set => _win32_console_utf8 = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool win32_console_utf8;
#endif
#if WEB
    private byte _win32_console_create;
    public bool win32_console_create { get => _win32_console_create != 0; set => _win32_console_create = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool win32_console_create;
#endif
#if WEB
    private byte _win32_console_attach;
    public bool win32_console_attach { get => _win32_console_attach != 0; set => _win32_console_attach = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool win32_console_attach;
#endif
#if WEB
    private IntPtr _html5_canvas_selector;
    public string html5_canvas_selector { get => Marshal.PtrToStringAnsi(_html5_canvas_selector);  set { if (_html5_canvas_selector != IntPtr.Zero) { Marshal.FreeHGlobal(_html5_canvas_selector); _html5_canvas_selector = IntPtr.Zero; } if (value != null) { _html5_canvas_selector = Marshal.StringToHGlobalAnsi(value); } } }
#else
    [M(U.LPUTF8Str)] public string html5_canvas_selector;
#endif
#if WEB
    private byte _html5_canvas_resize;
    public bool html5_canvas_resize { get => _html5_canvas_resize != 0; set => _html5_canvas_resize = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_canvas_resize;
#endif
#if WEB
    private byte _html5_preserve_drawing_buffer;
    public bool html5_preserve_drawing_buffer { get => _html5_preserve_drawing_buffer != 0; set => _html5_preserve_drawing_buffer = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_preserve_drawing_buffer;
#endif
#if WEB
    private byte _html5_premultiplied_alpha;
    public bool html5_premultiplied_alpha { get => _html5_premultiplied_alpha != 0; set => _html5_premultiplied_alpha = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_premultiplied_alpha;
#endif
#if WEB
    private byte _html5_ask_leave_site;
    public bool html5_ask_leave_site { get => _html5_ask_leave_site != 0; set => _html5_ask_leave_site = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_ask_leave_site;
#endif
#if WEB
    private byte _html5_update_document_title;
    public bool html5_update_document_title { get => _html5_update_document_title != 0; set => _html5_update_document_title = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_update_document_title;
#endif
#if WEB
    private byte _html5_bubble_mouse_events;
    public bool html5_bubble_mouse_events { get => _html5_bubble_mouse_events != 0; set => _html5_bubble_mouse_events = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_bubble_mouse_events;
#endif
#if WEB
    private byte _html5_bubble_touch_events;
    public bool html5_bubble_touch_events { get => _html5_bubble_touch_events != 0; set => _html5_bubble_touch_events = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_bubble_touch_events;
#endif
#if WEB
    private byte _html5_bubble_wheel_events;
    public bool html5_bubble_wheel_events { get => _html5_bubble_wheel_events != 0; set => _html5_bubble_wheel_events = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_bubble_wheel_events;
#endif
#if WEB
    private byte _html5_bubble_key_events;
    public bool html5_bubble_key_events { get => _html5_bubble_key_events != 0; set => _html5_bubble_key_events = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_bubble_key_events;
#endif
#if WEB
    private byte _html5_bubble_char_events;
    public bool html5_bubble_char_events { get => _html5_bubble_char_events != 0; set => _html5_bubble_char_events = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_bubble_char_events;
#endif
#if WEB
    private byte _html5_use_emsc_set_main_loop;
    public bool html5_use_emsc_set_main_loop { get => _html5_use_emsc_set_main_loop != 0; set => _html5_use_emsc_set_main_loop = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_use_emsc_set_main_loop;
#endif
#if WEB
    private byte _html5_emsc_set_main_loop_simulate_infinite_loop;
    public bool html5_emsc_set_main_loop_simulate_infinite_loop { get => _html5_emsc_set_main_loop_simulate_infinite_loop != 0; set => _html5_emsc_set_main_loop_simulate_infinite_loop = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool html5_emsc_set_main_loop_simulate_infinite_loop;
#endif
#if WEB
    private byte _ios_keyboard_resizes_canvas;
    public bool ios_keyboard_resizes_canvas { get => _ios_keyboard_resizes_canvas != 0; set => _ios_keyboard_resizes_canvas = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool ios_keyboard_resizes_canvas;
#endif
}
public enum sapp_html5_fetch_error
{
    SAPP_HTML5_FETCH_ERROR_NO_ERROR,
    SAPP_HTML5_FETCH_ERROR_BUFFER_TOO_SMALL,
    SAPP_HTML5_FETCH_ERROR_OTHER,
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_html5_fetch_response
{
#if WEB
    private byte _succeeded;
    public bool succeeded { get => _succeeded != 0; set => _succeeded = value ? (byte)1 : (byte)0; }
#else
    [M(U.I1)] public bool succeeded;
#endif
    public sapp_html5_fetch_error error_code;
    public int file_index;
    public sapp_range data;
    public sapp_range buffer;
    public void* user_data;
}
[StructLayout(LayoutKind.Sequential)]
public struct sapp_html5_fetch_request
{
    public int dropped_file_index;
    public delegate* unmanaged<sapp_html5_fetch_response*, void> callback;
    public sapp_range buffer;
    public void* user_data;
}
public enum sapp_mouse_cursor
{
    SAPP_MOUSECURSOR_DEFAULT = 0,
    SAPP_MOUSECURSOR_ARROW,
    SAPP_MOUSECURSOR_IBEAM,
    SAPP_MOUSECURSOR_CROSSHAIR,
    SAPP_MOUSECURSOR_POINTING_HAND,
    SAPP_MOUSECURSOR_RESIZE_EW,
    SAPP_MOUSECURSOR_RESIZE_NS,
    SAPP_MOUSECURSOR_RESIZE_NWSE,
    SAPP_MOUSECURSOR_RESIZE_NESW,
    SAPP_MOUSECURSOR_RESIZE_ALL,
    SAPP_MOUSECURSOR_NOT_ALLOWED,
    SAPP_MOUSECURSOR_CUSTOM_0,
    SAPP_MOUSECURSOR_CUSTOM_1,
    SAPP_MOUSECURSOR_CUSTOM_2,
    SAPP_MOUSECURSOR_CUSTOM_3,
    SAPP_MOUSECURSOR_CUSTOM_4,
    SAPP_MOUSECURSOR_CUSTOM_5,
    SAPP_MOUSECURSOR_CUSTOM_6,
    SAPP_MOUSECURSOR_CUSTOM_7,
    SAPP_MOUSECURSOR_CUSTOM_8,
    SAPP_MOUSECURSOR_CUSTOM_9,
    SAPP_MOUSECURSOR_CUSTOM_10,
    SAPP_MOUSECURSOR_CUSTOM_11,
    SAPP_MOUSECURSOR_CUSTOM_12,
    SAPP_MOUSECURSOR_CUSTOM_13,
    SAPP_MOUSECURSOR_CUSTOM_14,
    SAPP_MOUSECURSOR_CUSTOM_15,
    _SAPP_MOUSECURSOR_NUM,
}
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_isvalid", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_isvalid", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sapp_is_valid();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_width", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_width", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_width();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_widthf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_widthf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float sapp_widthf();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_height", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_height", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_height();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_heightf", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_heightf", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float sapp_heightf();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_color_format", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_color_format", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_color_format();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_depth_format", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_depth_format", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_depth_format();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_sample_count", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_sample_count", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_sample_count();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_high_dpi", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_high_dpi", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sapp_high_dpi();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_dpi_scale", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_dpi_scale", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern float sapp_dpi_scale();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_show_keyboard", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_show_keyboard", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_show_keyboard(bool show);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_keyboard_shown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_keyboard_shown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sapp_keyboard_shown();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_is_fullscreen", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_is_fullscreen", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sapp_is_fullscreen();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_toggle_fullscreen", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_toggle_fullscreen", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_toggle_fullscreen();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_show_mouse", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_show_mouse", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_show_mouse(bool show);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_mouse_shown", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_mouse_shown", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sapp_mouse_shown();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_lock_mouse", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_lock_mouse", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_lock_mouse(bool dolock);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_mouse_locked", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_mouse_locked", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sapp_mouse_locked();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_set_mouse_cursor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_set_mouse_cursor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_set_mouse_cursor(sapp_mouse_cursor cursor);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_get_mouse_cursor", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_get_mouse_cursor", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sapp_mouse_cursor sapp_get_mouse_cursor();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_bind_mouse_cursor_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_bind_mouse_cursor_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sapp_mouse_cursor sapp_bind_mouse_cursor_image(sapp_mouse_cursor cursor, in sapp_image_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_unbind_mouse_cursor_image", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_unbind_mouse_cursor_image", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_unbind_mouse_cursor_image(sapp_mouse_cursor cursor);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_userdata", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_userdata", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_userdata();

#if WEB
public static sapp_desc sapp_query_desc()
{
    sapp_desc result = default;
    sapp_query_desc_internal(ref result);
    return result;
}
#else
#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_query_desc", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_query_desc", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern sapp_desc sapp_query_desc();
#endif

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_request_quit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_request_quit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_request_quit();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_cancel_quit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_cancel_quit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_cancel_quit();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_quit", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_quit", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_quit();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_consume_event", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_consume_event", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_consume_event();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_frame_count", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_frame_count", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern ulong sapp_frame_count();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_frame_duration", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_frame_duration", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern double sapp_frame_duration();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_set_clipboard_string", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_set_clipboard_string", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_set_clipboard_string([M(U.LPUTF8Str)] string str);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_get_clipboard_string", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_get_clipboard_string", CallingConvention = CallingConvention.Cdecl)]
#endif
private static extern IntPtr sapp_get_clipboard_string_native();

public static string sapp_get_clipboard_string()
{
    IntPtr ptr = sapp_get_clipboard_string_native();
    if (ptr == IntPtr.Zero)
        return "";

    // Manual UTF-8 to string conversion to avoid marshalling corruption
    try
    {
        return Marshal.PtrToStringUTF8(ptr) ?? "";
    }
    catch
    {
        // Fallback in case of any marshalling issues
        return "";
    }
}

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_set_window_title", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_set_window_title", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_set_window_title([M(U.LPUTF8Str)] string str);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_set_icon", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_set_icon", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_set_icon(in sapp_icon_desc icon_desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_get_num_dropped_files", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_get_num_dropped_files", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_get_num_dropped_files();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_get_dropped_file_path", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_get_dropped_file_path", CallingConvention = CallingConvention.Cdecl)]
#endif
private static extern IntPtr sapp_get_dropped_file_path_native(int index);

public static string sapp_get_dropped_file_path(int index)
{
    IntPtr ptr = sapp_get_dropped_file_path_native(index);
    if (ptr == IntPtr.Zero)
        return "";

    // Manual UTF-8 to string conversion to avoid marshalling corruption
    try
    {
        return Marshal.PtrToStringUTF8(ptr) ?? "";
    }
    catch
    {
        // Fallback in case of any marshalling issues
        return "";
    }
}

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_run", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_run", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_run(in sapp_desc desc);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_egl_get_display", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_egl_get_display", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_egl_get_display();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_egl_get_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_egl_get_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_egl_get_context();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_html5_ask_leave_site", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_html5_ask_leave_site", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_html5_ask_leave_site(bool ask);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_html5_get_dropped_file_size", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_html5_get_dropped_file_size", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint sapp_html5_get_dropped_file_size(int index);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_html5_fetch_dropped_file", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_html5_fetch_dropped_file", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_html5_fetch_dropped_file(in sapp_html5_fetch_request request);

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_metal_get_device", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_metal_get_device", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_metal_get_device();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_metal_get_current_drawable", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_metal_get_current_drawable", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_metal_get_current_drawable();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_metal_get_depth_stencil_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_metal_get_depth_stencil_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_metal_get_depth_stencil_texture();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_metal_get_msaa_color_texture", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_metal_get_msaa_color_texture", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_metal_get_msaa_color_texture();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_macos_get_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_macos_get_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_macos_get_window();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_ios_get_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_ios_get_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_ios_get_window();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_d3d11_get_device", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_d3d11_get_device", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_d3d11_get_device();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_d3d11_get_device_context", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_d3d11_get_device_context", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_d3d11_get_device_context();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_d3d11_get_swap_chain", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_d3d11_get_swap_chain", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_d3d11_get_swap_chain();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_d3d11_get_render_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_d3d11_get_render_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_d3d11_get_render_view();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_d3d11_get_resolve_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_d3d11_get_resolve_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_d3d11_get_resolve_view();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_d3d11_get_depth_stencil_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_d3d11_get_depth_stencil_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_d3d11_get_depth_stencil_view();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_win32_get_hwnd", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_win32_get_hwnd", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_win32_get_hwnd();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_wgpu_get_device", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_wgpu_get_device", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_wgpu_get_device();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_wgpu_get_render_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_wgpu_get_render_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_wgpu_get_render_view();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_wgpu_get_resolve_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_wgpu_get_resolve_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_wgpu_get_resolve_view();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_wgpu_get_depth_stencil_view", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_wgpu_get_depth_stencil_view", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_wgpu_get_depth_stencil_view();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_gl_get_framebuffer", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_gl_get_framebuffer", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern uint sapp_gl_get_framebuffer();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_gl_get_major_version", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_gl_get_major_version", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_gl_get_major_version();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_gl_get_minor_version", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_gl_get_minor_version", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern int sapp_gl_get_minor_version();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_gl_is_gles", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_gl_is_gles", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern bool sapp_gl_is_gles();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_x11_get_window", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_x11_get_window", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_x11_get_window();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_x11_get_display", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_x11_get_display", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_x11_get_display();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_android_get_native_activity", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_android_get_native_activity", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void* sapp_android_get_native_activity();

#if __IOS__
[DllImport("@rpath/sokol.framework/sokol", EntryPoint = "sapp_query_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#else
[DllImport("sokol", EntryPoint = "sapp_query_desc_internal", CallingConvention = CallingConvention.Cdecl)]
#endif
public static extern void sapp_query_desc_internal(ref sapp_desc result);

}
}
