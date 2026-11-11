using System;
using System.Collections.Generic;
using System.Numerics;
using Sokol;
using static Sokol.SApp;

namespace GameEditor.Framework.Input
{
    // ── Touch ─────────────────────────────────────────────────────────────────

    /// <summary>Lifecycle phase of a touch point (matches Unity's TouchPhase).</summary>
    public enum TouchPhase { Began, Moved, Stationary, Ended, Cancelled }

    /// <summary>Data for a single active touch point.</summary>
    public struct Touch
    {
        /// <summary>Platform-assigned, unique-per-gesture finger identifier.</summary>
        public int FingerId;
        /// <summary>Screen-space position in pixels (top-left origin).</summary>
        public Vector2 Position;
        /// <summary>Position delta since the previous event for this finger.</summary>
        public Vector2 DeltaPosition;
        /// <summary>Current lifecycle phase.</summary>
        public TouchPhase Phase;
    }

    // ── Gamepad ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot of a single gamepad's full state.
    /// Populate and push via <see cref="InputManager.SetGamepadState"/> from your
    /// platform bridge (SDL, browser Gamepad API, etc.).
    /// </summary>
    public struct GamepadState
    {
        /// <summary>Left analogue stick, X right / Y up, range [-1, 1].</summary>
        public Vector2 LeftStick;
        /// <summary>Right analogue stick, X right / Y up, range [-1, 1].</summary>
        public Vector2 RightStick;
        /// <summary>Left trigger [0, 1].</summary>
        public float LeftTrigger;
        /// <summary>Right trigger [0, 1].</summary>
        public float RightTrigger;

        // Face buttons (Xbox layout names)
        public bool South;         // A / Cross
        public bool East;          // B / Circle
        public bool West;          // X / Square
        public bool North;         // Y / Triangle

        // D-pad
        public bool DpadUp, DpadDown, DpadLeft, DpadRight;

        // Shoulder buttons
        public bool LeftBumper, RightBumper;

        // Stick click
        public bool LeftStickButton, RightStickButton;

        // System buttons
        public bool Start, Select;

        /// <summary>True when a gamepad is physically connected at this slot.</summary>
        public bool Connected;
    }

    // ── InputManager ──────────────────────────────────────────────────────────

    /// <summary>
    /// Central, polling-style input manager for the Sokol.NET game framework.
    ///
    /// Supports:
    ///   • Keyboard  — GetKey / GetKeyDown / GetKeyUp by <see cref="Key"/>
    ///   • Mouse     — position, delta, scroll, three buttons
    ///   • Touch     — up to 8 simultaneous fingers via GetTouch / TouchCount
    ///   • Named axes / buttons — Unity-compatible "Horizontal", "Vertical", "Jump", …
    ///   • Virtual injection — SetVirtualKey / SetVirtualAxis for on-screen joysticks and
    ///                         gamepad-to-key mapping on mobile
    ///   • Gamepad   — injectable GamepadState (platform-bridge fills it each frame)
    ///
    /// ── Host-app wiring ────────────────────────────────────────────────────
    ///   Frame()  → call <see cref="BeginFrame"/> at the very beginning
    ///   Event()  → call <see cref="ProcessEvent"/> for every sapp_event
    ///
    /// sokol_app delivers events between frame callbacks. They are kept until the next
    /// <see cref="BeginFrame"/> and applied there, so what an event sets for one frame only
    /// (GetKeyDown / GetKeyUp, mouse-button down / up, mouse and scroll deltas, a touch's
    /// Began / Moved / Ended phase) is what the scripts of that frame read.
    /// </summary>
    public static unsafe class InputManager
    {
        // ── Key state ─────────────────────────────────────────────────────────
        // 512 slots safely covers all sapp_keycode values (max = 348).

        private const int KeySlots = 512;

        // Real keyboard state
        private static readonly bool[] _held             = new bool[KeySlots];
        private static readonly bool[] _pressedThisFrame = new bool[KeySlots];
        private static readonly bool[] _releasedThisFrame = new bool[KeySlots];

        // Virtual key injection (from mobile on-screen buttons or gamepad mapping)
        private static readonly bool[] _vHeld             = new bool[KeySlots];
        private static readonly bool[] _vPressed          = new bool[KeySlots];
        private static readonly bool[] _vReleased         = new bool[KeySlots];
        // Virtual transitions since the last BeginFrame; they become _vPressed / _vReleased for the next frame.
        private static readonly bool[] _vPressedNext      = new bool[KeySlots];
        private static readonly bool[] _vReleasedNext     = new bool[KeySlots];

        // Events received since the last BeginFrame, applied by it.
        private static readonly List<sapp_event> _pendingEvents = new();

        // Where the game is drawn inside the window (SetPointerViewport).
        private static Vector2 _pointerOrigin;
        private static float   _pointerScale = 1f;

        // ── Mouse state ───────────────────────────────────────────────────────

        private static readonly bool[] _mouseHeld    = new bool[3];
        private static readonly bool[] _mousePressed  = new bool[3];
        private static readonly bool[] _mouseReleased = new bool[3];

        // Backing fields for mouse properties (needed so the ALC bridge callbacks can override reads
        // while ProcessEvent still writes to the local state).
        private static Vector2 _mousePosition;
        private static Vector2 _mouseDelta;
        private static float   _scrollDelta;

        /// <summary>Current mouse position in screen pixels (top-left origin).</summary>
        public static Vector2 MousePosition => _cbMousePosition != null ? _cbMousePosition() : _mousePosition;

        /// <summary>Mouse movement delta accumulated this frame.</summary>
        public static Vector2 MouseDelta    => _cbMouseDelta    != null ? _cbMouseDelta()    : _mouseDelta;

        /// <summary>Vertical scroll delta this frame (positive = up, matches Unity).</summary>
        public static float   ScrollDelta   => _cbScrollDelta   != null ? _cbScrollDelta()   : _scrollDelta;

        /// <summary>Horizontal scroll delta this frame.</summary>
        public static float ScrollDeltaX { get; private set; }

        // ── Touch state ───────────────────────────────────────────────────────

        private static readonly Touch[] _touches      = new Touch[8];
        private static readonly bool[]  _touchActive  = new bool[8];

        /// <summary>Number of currently active touch points.</summary>
        public static int TouchCount { get; private set; }

        // ── Named-axis virtual overrides ──────────────────────────────────────

        private static readonly Dictionary<string, float> _axisOverride   = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool>  _buttonOverride = new(StringComparer.Ordinal);

        // ── Gamepad ───────────────────────────────────────────────────────────

        private static GamepadState _gamepad;

        // ─────────────────────────────────────────────────────────────────────
        // Frame lifecycle
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Call once at the very start of each frame callback (before scripts run).
        /// Clears the previous frame's pressed/released states, deltas and finished touches, then applies
        /// the events received since the last call: GetKeyDown/GetKeyUp are true for exactly this frame.
        /// </summary>
        public static void BeginFrame()
        {
            // Clear per-frame key flash arrays; virtual transitions since the last frame take their turn
            Array.Clear(_pressedThisFrame,  0, KeySlots);
            Array.Clear(_releasedThisFrame, 0, KeySlots);
            Array.Copy(_vPressedNext,  _vPressed,  KeySlots);
            Array.Copy(_vReleasedNext, _vReleased, KeySlots);
            Array.Clear(_vPressedNext,  0, KeySlots);
            Array.Clear(_vReleasedNext, 0, KeySlots);

            // Clear per-frame mouse flash arrays
            Array.Clear(_mousePressed,  0, 3);
            Array.Clear(_mouseReleased, 0, 3);

            // Reset per-frame mouse deltas and scroll
            _mouseDelta  = Vector2.Zero;
            _scrollDelta = 0f;
            ScrollDeltaX = 0f;

            // Retire touches that ended or were cancelled last frame
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                if (!_touchActive[i]) continue;
                if (_touches[i].Phase == TouchPhase.Ended ||
                    _touches[i].Phase == TouchPhase.Cancelled)
                {
                    _touchActive[i] = false;
                    _touches[i]     = default;
                }
                else
                {
                    // Stationary unless a MOVED event arrives this frame
                    _touches[i] = _touches[i] with
                    {
                        Phase        = TouchPhase.Stationary,
                        DeltaPosition = Vector2.Zero,
                    };
                    count++;
                }
            }
            TouchCount = count;

            // Clear single-frame axis / button overrides
            _axisOverride.Clear();
            _buttonOverride.Clear();

            // This frame's events
            for (int i = 0; i < _pendingEvents.Count; i++)
            {
                sapp_event e = _pendingEvents[i];
                ApplyEvent(&e);
            }
            _pendingEvents.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Raw event processing
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Feed a raw Sokol event. Call from the host app's Event() callback.
        /// The event takes effect at the next <see cref="BeginFrame"/>.
        /// </summary>
        public static void ProcessEvent(sapp_event* e)
        {
            switch (e->type)
            {
                case sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN:
                case sapp_event_type.SAPP_EVENTTYPE_KEY_UP:
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED:
                    _pendingEvents.Add(*e);
                    break;
            }
        }

        /// <summary>
        /// For a host that shows the game in a part of its window (the editor's Game view): the window pixel
        /// where the game's picture starts and the game pixels per window pixel. Mouse and touch positions
        /// are then reported in the game's own pixels, as in the game running alone. Default: 0, 0, 1.
        /// </summary>
        public static void SetPointerViewport(float originX, float originY, float scale = 1f)
        {
            _pointerOrigin = new Vector2(originX, originY);
            _pointerScale  = scale;
        }

        private static Vector2 ToGame(float x, float y) => (new Vector2(x, y) - _pointerOrigin) * _pointerScale;

        private static void ApplyEvent(sapp_event* e)
        {
            switch (e->type)
            {
                case sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN:
                {
                    int k = (int)e->key_code;
                    if ((uint)k < KeySlots)
                    {
                        // Guard against key-repeat firing GetKeyDown every frame
                        if (!_held[k]) _pressedThisFrame[k] = true;
                        _held[k] = true;
                    }
                    break;
                }

                case sapp_event_type.SAPP_EVENTTYPE_KEY_UP:
                {
                    int k = (int)e->key_code;
                    if ((uint)k < KeySlots)
                    {
                        _releasedThisFrame[k] = true;
                        _held[k]              = false;
                    }
                    break;
                }

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
                {
                    int b = (int)e->mouse_button;
                    if ((uint)b < 3) { _mousePressed[b] = true; _mouseHeld[b] = true; }
                    break;
                }

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
                {
                    int b = (int)e->mouse_button;
                    if ((uint)b < 3) { _mouseReleased[b] = true; _mouseHeld[b] = false; }
                    break;
                }

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
                    _mousePosition = ToGame(e->mouse_x, e->mouse_y);
                    _mouseDelta   += new Vector2(e->mouse_dx, e->mouse_dy) * _pointerScale;
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
                    _scrollDelta += e->scroll_y;
                    ScrollDeltaX += e->scroll_x;
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED:
                    ProcessTouchEvent(e);
                    break;
            }
        }

        private static void ProcessTouchEvent(sapp_event* e)
        {
            TouchPhase phase = e->type switch
            {
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN     => TouchPhase.Began,
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED     => TouchPhase.Moved,
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED     => TouchPhase.Ended,
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED => TouchPhase.Cancelled,
                _                                                 => TouchPhase.Stationary,
            };

            for (int i = 0; i < e->num_touches && i < 8; i++)
            {
                ref sapp_touchpoint tp = ref e->touches[i];
                int    id     = (int)tp.identifier;
                Vector2 pos   = ToGame(tp.pos_x, tp.pos_y);

                int slot = FindTouchSlot(id);

                if (slot < 0)
                {
                    // Only allocate a new slot for BEGAN
                    if (phase != TouchPhase.Began) continue;
                    slot = AllocTouchSlot();
                    if (slot < 0) continue;  // all 8 slots occupied
                    _touchActive[slot] = true;
                    _touches[slot]     = new Touch { FingerId = id, Position = pos };
                }

                Vector2 prev = _touches[slot].Position;
                _touches[slot] = new Touch
                {
                    FingerId      = id,
                    Position      = pos,
                    DeltaPosition = pos - prev,
                    Phase         = phase,
                };
            }

            // Recount
            int count = 0;
            for (int i = 0; i < 8; i++)
            {
                if (_touchActive[i] &&
                    _touches[i].Phase != TouchPhase.Ended &&
                    _touches[i].Phase != TouchPhase.Cancelled)
                    count++;
            }
            TouchCount = count;
        }

        private static int FindTouchSlot(int fingerId)
        {
            for (int i = 0; i < 8; i++)
                if (_touchActive[i] && _touches[i].FingerId == fingerId) return i;
            return -1;
        }

        private static int AllocTouchSlot()
        {
            for (int i = 0; i < 8; i++)
                if (!_touchActive[i]) return i;
            return -1;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Keyboard queries
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// True while the key is held down (equivalent to Unity's <c>Input.GetKey</c>).
        /// </summary>
        public static bool GetKey(Key key)
        {
            int k = (int)key;
            if (_cbGetKey != null) return _cbGetKey(k);
            return (uint)k < KeySlots && (_held[k] || _vHeld[k]);
        }

        /// <summary>
        /// True only during the first frame the key is pressed (equivalent to Unity's
        /// <c>Input.GetKeyDown</c>).
        /// </summary>
        public static bool GetKeyDown(Key key)
        {
            int k = (int)key;
            if (_cbGetKeyDown != null) return _cbGetKeyDown(k);
            return (uint)k < KeySlots && (_pressedThisFrame[k] || _vPressed[k]);
        }

        /// <summary>
        /// True only during the frame the key is released (equivalent to Unity's
        /// <c>Input.GetKeyUp</c>).
        /// </summary>
        public static bool GetKeyUp(Key key)
        {
            int k = (int)key;
            if (_cbGetKeyUp != null) return _cbGetKeyUp(k);
            return (uint)k < KeySlots && (_releasedThisFrame[k] || _vReleased[k]);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mouse queries
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>True while the mouse button is held (0 = left, 1 = right, 2 = middle).</summary>
        public static bool GetMouseButton(int button)
            => _cbGetMouseButton != null ? _cbGetMouseButton(button) : (uint)button < 3 && _mouseHeld[button];

        /// <summary>True during the first frame the mouse button is pressed.</summary>
        public static bool GetMouseButtonDown(int button)
            => _cbGetMouseButtonDown != null ? _cbGetMouseButtonDown(button) : (uint)button < 3 && _mousePressed[button];

        /// <summary>True during the frame the mouse button is released.</summary>
        public static bool GetMouseButtonUp(int button)
            => _cbGetMouseButtonUp != null ? _cbGetMouseButtonUp(button) : (uint)button < 3 && _mouseReleased[button];

        // ─────────────────────────────────────────────────────────────────────
        // Named axes (Unity-style)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the value of a named axis in the range [-1, 1] (or [0, 1] for triggers).
        ///
        /// Built-in axes:
        ///   "Horizontal"        — A/D or Left/Right arrows; gamepad left stick X
        ///   "Vertical"          — W/S or Up/Down arrows;   gamepad left stick Y
        ///   "Fire1"             — Left Ctrl or LMB;         gamepad South
        ///   "Fire2"             — Left Alt  or RMB;         gamepad East
        ///   "Fire3"             — Left Shift or MMB;        gamepad West
        ///   "Jump"              — Space;                    gamepad South
        ///   "Submit"            — Return / Keypad Enter
        ///   "Cancel"            — Escape
        ///   "Mouse X"           — mouse delta X this frame
        ///   "Mouse Y"           — mouse delta Y this frame
        ///   "Mouse ScrollWheel" — vertical scroll delta
        ///   "RightStickX/Y"     — gamepad right stick
        ///   "LeftTrigger"       — gamepad left trigger [0,1]
        ///   "RightTrigger"      — gamepad right trigger [0,1]
        ///
        /// Virtual override: SetVirtualAxis overrides a named axis for the current frame.
        /// </summary>
        public static float GetAxis(string name)
        {
            if (_axisOverride.TryGetValue(name, out float ov))
                return Math.Clamp(ov, -1f, 1f);

            return name switch
            {
                "Horizontal"        => Math.Clamp(
                                         DigitalAxis(Key.A, Key.D, Key.LeftArrow, Key.RightArrow)
                                         + _gamepad.LeftStick.X,
                                         -1f, 1f),
                "Vertical"          => Math.Clamp(
                                         DigitalAxis(Key.S, Key.W, Key.DownArrow, Key.UpArrow)
                                         + _gamepad.LeftStick.Y,
                                         -1f, 1f),
                "Fire1"             => GetKey(Key.LeftControl) || _mouseHeld[0] || _gamepad.South ? 1f : 0f,
                "Fire2"             => GetKey(Key.LeftAlt)     || _mouseHeld[1] || _gamepad.East  ? 1f : 0f,
                "Fire3"             => GetKey(Key.LeftShift)   || _mouseHeld[2] || _gamepad.West  ? 1f : 0f,
                "Jump"              => GetKey(Key.Space)        || _gamepad.South                 ? 1f : 0f,
                "Submit"            => GetKey(Key.Return)       || GetKey(Key.KeypadEnter)         ? 1f : 0f,
                "Cancel"            => GetKey(Key.Escape)                                         ? 1f : 0f,
                "Mouse X"           => MouseDelta.X,
                "Mouse Y"           => MouseDelta.Y,
                "Mouse ScrollWheel" => ScrollDelta,
                "RightStickX"       => _gamepad.RightStick.X,
                "RightStickY"       => _gamepad.RightStick.Y,
                "LeftTrigger"       => _gamepad.LeftTrigger,
                "RightTrigger"      => _gamepad.RightTrigger,
                _                   => 0f,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Named buttons
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>True while the named button is held.</summary>
        public static bool GetButton(string name)
        {
            if (_buttonOverride.TryGetValue(name, out bool b)) return b;
            return name switch
            {
                "Fire1"  => GetKey(Key.LeftControl) || _mouseHeld[0] || _gamepad.South,
                "Fire2"  => GetKey(Key.LeftAlt)     || _mouseHeld[1] || _gamepad.East,
                "Fire3"  => GetKey(Key.LeftShift)   || _mouseHeld[2] || _gamepad.West,
                "Jump"   => GetKey(Key.Space)        || _gamepad.South,
                "Submit" => GetKey(Key.Return)       || GetKey(Key.KeypadEnter),
                "Cancel" => GetKey(Key.Escape),
                _        => false,
            };
        }

        /// <summary>True only during the first frame the named button is pressed.</summary>
        public static bool GetButtonDown(string name) => name switch
        {
            "Fire1"  => GetKeyDown(Key.LeftControl) || GetMouseButtonDown(0),
            "Fire2"  => GetKeyDown(Key.LeftAlt)     || GetMouseButtonDown(1),
            "Fire3"  => GetKeyDown(Key.LeftShift)   || GetMouseButtonDown(2),
            "Jump"   => GetKeyDown(Key.Space),
            "Submit" => GetKeyDown(Key.Return)      || GetKeyDown(Key.KeypadEnter),
            "Cancel" => GetKeyDown(Key.Escape),
            _        => false,
        };

        /// <summary>True only during the frame the named button is released.</summary>
        public static bool GetButtonUp(string name) => name switch
        {
            "Fire1"  => GetKeyUp(Key.LeftControl) || GetMouseButtonUp(0),
            "Fire2"  => GetKeyUp(Key.LeftAlt)     || GetMouseButtonUp(1),
            "Fire3"  => GetKeyUp(Key.LeftShift)   || GetMouseButtonUp(2),
            "Jump"   => GetKeyUp(Key.Space),
            "Submit" => GetKeyUp(Key.Return)      || GetKeyUp(Key.KeypadEnter),
            "Cancel" => GetKeyUp(Key.Escape),
            _        => false,
        };

        // ─────────────────────────────────────────────────────────────────────
        // Touch queries
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the touch at <paramref name="index"/> (0 ≤ index &lt; <see cref="TouchCount"/>).
        /// </summary>
        public static Touch GetTouch(int index)
        {
            // Walk active slots to find the nth active touch
            int found = 0;
            for (int i = 0; i < 8; i++)
            {
                if (!_touchActive[i]) continue;
                if (found == index) return _touches[i];
                found++;
            }
            return default;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Virtual injection (mobile on-screen controls, gamepad adapters)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inject a virtual key event (e.g. from an on-screen button or gamepad mapping).
        /// GetKey follows at once; GetKeyDown / GetKeyUp fire for the one frame after the change.
        /// </summary>
        public static void SetVirtualKey(Key key, bool down)
        {
            int k = (int)key;
            if ((uint)k >= KeySlots) return;

            if (down  && !_vHeld[k]) _vPressedNext[k]  = true;
            if (!down &&  _vHeld[k]) _vReleasedNext[k] = true;
            _vHeld[k] = down;
        }

        /// <summary>
        /// Override the value returned by GetAxis for the current frame only.
        /// The override is cleared at the start of the next frame by BeginFrame().
        /// </summary>
        public static void SetVirtualAxis(string name, float value)
            => _axisOverride[name] = value;

        /// <summary>
        /// Override the state returned by GetButton for the current frame only.
        /// </summary>
        public static void SetVirtualButton(string name, bool down)
            => _buttonOverride[name] = down;

        // ─────────────────────────────────────────────────────────────────────
        // Gamepad
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Push the current gamepad state (call every frame from your platform bridge).
        /// Sokol does not expose native gamepad events; the host app must supply this from
        /// SDL, the browser Gamepad API, or a similar source.
        /// </summary>
        public static void SetGamepadState(GamepadState state) => _gamepad = state;

        /// <summary>Returns a snapshot of the current gamepad state.</summary>
        public static GamepadState GetGamepadState() => _gamepad;

        // ─────────────────────────────────────────────────────────────────────        // ALC bridge (injected by GameAssemblyRunner when running in a CollectibleAssemblyLoadContext)
        // ─────────────────────────────────────────────────────────────────────

        private static Func<int, bool>?  _cbGetKey;
        private static Func<int, bool>?  _cbGetKeyDown;
        private static Func<int, bool>?  _cbGetKeyUp;
        private static Func<int, bool>?  _cbGetMouseButton;
        private static Func<int, bool>?  _cbGetMouseButtonDown;
        private static Func<int, bool>?  _cbGetMouseButtonUp;
        private static Func<Vector2>?    _cbMousePosition;
        private static Func<Vector2>?    _cbMouseDelta;
        private static Func<float>?      _cbScrollDelta;

        /// <summary>
        /// Called by the host after loading a game DLL to redirect all input queries
        /// from this isolated InputManager to the host's InputManager.
        /// GetAxis / GetButton / GetButtonDown / GetButtonUp are not bridged directly;
        /// they build on top of the bridged key/mouse queries and work automatically.
        /// </summary>
        public static void RegisterCallbacks(
            Func<int, bool>  getKey,
            Func<int, bool>  getKeyDown,
            Func<int, bool>  getKeyUp,
            Func<int, bool>  getMouseButton,
            Func<int, bool>  getMouseButtonDown,
            Func<int, bool>  getMouseButtonUp,
            Func<Vector2>    getMousePosition,
            Func<Vector2>    getMouseDelta,
            Func<float>      getScrollDelta)
        {
            _cbGetKey              = getKey;
            _cbGetKeyDown          = getKeyDown;
            _cbGetKeyUp            = getKeyUp;
            _cbGetMouseButton      = getMouseButton;
            _cbGetMouseButtonDown  = getMouseButtonDown;
            _cbGetMouseButtonUp    = getMouseButtonUp;
            _cbMousePosition       = getMousePosition;
            _cbMouseDelta          = getMouseDelta;
            _cbScrollDelta         = getScrollDelta;
        }

        // ─────────────────────────────────────────────────────────────────────        // Internal helpers
        // ─────────────────────────────────────────────────────────────────────

        private static float DigitalAxis(Key neg1, Key pos1, Key neg2, Key pos2)
        {
            float v = 0f;
            if (GetKey(neg1) || GetKey(neg2)) v -= 1f;
            if (GetKey(pos1) || GetKey(pos2)) v += 1f;
            return v;
        }
    }
}
