using System;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Root widget.  One per application.  Owns the <see cref="Renderer"/>,
/// <see cref="FontRegistry"/>, <see cref="FocusManager"/> and <see cref="InputRouter"/>.
/// </summary>
public sealed class Screen : Widget
{
    // ─── Singleton ───────────────────────────────────────────────────────────
    private static Screen? _instance;
    public  static Screen   Instance =>
        _instance ?? throw new InvalidOperationException("Screen.Initialize() not called.");

    // ─── Owned resources ─────────────────────────────────────────────────────
    public Renderer      Renderer      { get; private set; } = null!;
    public FontRegistry  Fonts         { get; } = FontRegistry.Instance;
    public FocusManager  Focus         { get; } = new();
    public InputRouter   Input         { get; private set; } = null!;

    // ─── Frame overlays ──────────────────────────────────────────────────────
    // Widgets registered here are drawn during Draw() at an explicit position,
    // outside the Screen child hierarchy. Used to embed Sokol.GUI panels inside
    // ImGui docked windows until Wave 8 (DockManager) replaces docking entirely.
    private readonly System.Collections.Generic.List<(Widget Widget, Rect Bounds)> _frameOverlays = new();
    // Persisted snapshot used for hit-testing between frames (events arrive before the next Draw).
    private (Widget Widget, Rect Bounds)[] _hitTestOverlays = System.Array.Empty<(Widget, Rect)>();

    // Top overlays draw AFTER ImGui (via DrawTopOverlays) so a modal dialog appears above ImGui
    // viewport windows (Scene/Game) — unlike frame overlays, which draw before ImGui.
    private readonly System.Collections.Generic.List<(Widget Widget, Rect Bounds)> _topOverlays = new();
    private (Widget Widget, Rect Bounds)[] _topOverlaysToDraw = System.Array.Empty<(Widget, Rect)>();
    private (Widget Widget, Rect Bounds)[] _topHitTest        = System.Array.Empty<(Widget, Rect)>();

    public void AddFrameOverlay(Widget widget, Rect bounds)
    {
        // Set bounds with screen-space position so ScreenPosition (used by HitTestDeep) is correct.
        widget.Bounds = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        _frameOverlays.Add((widget, bounds));
    }

    /// <summary>
    /// Register a widget to be drawn ON TOP of everything this frame, after ImGui (see
    /// <see cref="DrawTopOverlays"/>) — for modal dialogs that must appear above ImGui viewport
    /// windows. Laid out + hit-tested like a frame overlay, but composited in the post-ImGui pass.
    /// </summary>
    public void AddTopOverlay(Widget widget, Rect bounds)
    {
        widget.Bounds = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        _topOverlays.Add((widget, bounds));
    }

    /// <summary>
    /// Lazily-constructed docking manager. The first access wires a root
    /// <see cref="DockSpace"/> and a <see cref="FloatingPanelHost"/> into the
    /// screen's children so dock panels are drawn and hit-tested alongside
    /// other root-level UI.
    /// </summary>
    public DockManager DockManager
    {
        get
        {
            if (_dockManager == null)
            {
                var dockSpace = new DockSpace { Id = "__RootDockSpace__" };
                var floatHost = new FloatingPanelHost { Id = "__FloatingPanelHost__" };
                AddChild(dockSpace);
                AddChild(floatHost); // added last → draws on top.
                _dockManager = new DockManager(dockSpace, floatHost);
            }
            return _dockManager;
        }
    }
    private DockManager? _dockManager;

    /// <summary>Returns the DockManager only if already created (no lazy init).</summary>
    internal DockManager? DockManagerOrNull => _dockManager;

    /// <summary>Manages cross-widget drag-and-drop.</summary>
    public DragManager Drag { get; } = new();

    // ─── Logical size ────────────────────────────────────────────────────────
    public float LogicalWidth  { get; private set; }
    public float LogicalHeight { get; private set; }

    // ─── Safe area ───────────────────────────────────────────────────────────
    /// <summary>
    /// The display's safe-area insets in LOGICAL px — the band a notch, Dynamic Island, punch-hole
    /// camera, home indicator or curved edge would otherwise clip. Asked of the OS through
    /// <c>ext/sokol_safearea.h</c>; zero on desktop, on web, and on any platform that reports no
    /// cutout. Re-asked only when the framebuffer changes (rotation / fold / resize), since the
    /// insets cannot change without it.
    /// </summary>
    public Thickness SafeAreaInsets { get; private set; }

    private float _safeAreaW = -1f, _safeAreaH = -1f, _safeAreaDpi = -1f;
    private bool  _safeAreaUnavailable;

    private void RefreshSafeArea(float width, float height, float dpiScale)
    {
        if (_safeAreaUnavailable) return;
        if (width == _safeAreaW && height == _safeAreaH && dpiScale == _safeAreaDpi) return;
        _safeAreaW = width; _safeAreaH = height; _safeAreaDpi = dpiScale;

        float dpi = dpiScale > 0f ? dpiScale : 1f;
        try
        {
            if (!Sokol.SSafeArea.ssafe_supported())
            {
                _safeAreaUnavailable = true;
                SafeAreaInsets       = Thickness.Zero;
                return;
            }
            Span<float> ltrb = stackalloc float[4];
            Sokol.SSafeArea.ssafe_get(ref ltrb[0]);   // physical px
            SafeAreaInsets = new Thickness(ltrb[0] / dpi, ltrb[1] / dpi, ltrb[2] / dpi, ltrb[3] / dpi);
        }
        catch
        {
            // A native library built before sokol_safearea.h has no entry point — that simply
            // means "no insets", and must never take the app down.
            _safeAreaUnavailable = true;
            SafeAreaInsets       = Thickness.Zero;
        }
    }

    // ─── Debug frame counter ─────────────────────────────────────────────────
    /// <summary>Increments each Update(). Used to gate per-frame debug logs.</summary>
    internal static int DbgFrame { get; private set; }

    // ─── Virtual keyboard ────────────────────────────────────────────────────
    /// <summary>
    /// Height (logical pixels) currently occupied by the virtual keyboard.
    /// Auto-detected from window-height reduction when keyboard is shown
    /// (requires keyboard_resizes_canvas=true on iOS or adjustResize on Android).
    /// Zero on desktop or when keyboard is hidden.
    /// </summary>
    public float KeyboardHeight { get; private set; }
    private float _noKeyboardHeight;
#if __ANDROID__ || __IOS__
    private bool _keyboardConfirmed; // true once sapp_keyboard_shown() returns true after Show()
#endif

    /// <summary>
    /// Overlay that floats a proxy TextBox/TextArea above the virtual keyboard.
    /// Added to the screen as a top-level child on first access.
    /// </summary>
    public MobileKeyboardOverlay MobileOverlay
    {
        get
        {
            if (_mobileOverlay == null)
                _mobileOverlay = new MobileKeyboardOverlay();
            // Re-attach if a ClearChildren() detached it (so it stays live + on top).
            if (_mobileOverlay.Parent != this)
                AddChild(_mobileOverlay);
            return _mobileOverlay;
        }
    }
    private MobileKeyboardOverlay? _mobileOverlay;

    // ─── Popup / overlay ─────────────────────────────────────────────────────
    /// <summary>
    /// The currently active popup widget (e.g. an open ComboBox dropdown).
    /// Screen.HitTestDeep tests this widget first, bypassing the normal tree walk,
    /// so clicks on overlay regions outside the widget's Bounds still reach it.
    /// </summary>
    private static Widget? _activePopup;

    private NotificationHost _notificationHost = new();

    public static bool HasActivePopup => _activePopup != null;

    public static void SetActivePopup(Widget? popup)
    {
        _activePopup = popup;
    }

    /// <summary>
    /// Close and dismiss the active popup, if any.
    /// Returns true when a popup was closed.
    /// </summary>
    public static bool CloseActivePopup()
    {
        if (_activePopup == null) return false;
        _activePopup.OnPopupDismiss();
        _activePopup = null;
        return true;
    }

    // ─── Init / Shutdown ─────────────────────────────────────────────────────
    public static Screen Initialize(IntPtr vg)
    {
        _instance          = new Screen();
        _instance.Renderer = new Renderer(vg);
        _instance.Input    = new InputRouter(_instance, _instance.Focus);
        _ = new AnimationManager();  // sets AnimationManager.Instance
        Sokol.SLog.Info("GUI: Screen initialized", "Sokol.GUI");
        return _instance;
    }

    /// <summary>
    /// Fired once during <see cref="Shutdown"/> before the singleton is cleared.
    /// Applications can use this to persist layout / widget state.
    /// </summary>
    public static event Action? ShuttingDown;

    public static void Shutdown()
    {
        try { ShuttingDown?.Invoke(); } catch { /* persistence errors must not prevent shutdown */ }
        _instance?.Fonts.Clear();
        _instance = null;
    }

    // ─── Per-frame ───────────────────────────────────────────────────────────
    public void Update(float width, float height, float dpiScale)
    {
        LogicalWidth  = width;
        LogicalHeight = height;

        if (Renderer == null || Renderer.VGContext == IntPtr.Zero) return; // can happen if Update is called before Init

        RefreshSafeArea(width, height, dpiScale);

        // Track actual keyboard height from window-height reduction
        // (keyboard_resizes_canvas on iOS, adjustResize on Android).
        bool kbShown = sapp_keyboard_shown();
        if (!kbShown)
        {
            _noKeyboardHeight = height;
            KeyboardHeight    = 0f;
        }
        else if (_noKeyboardHeight > 0 && height < _noKeyboardHeight)
        {
            KeyboardHeight = _noKeyboardHeight - height;
        }

        // Mobile overlay lifecycle — driven by focus state, not by sapp_keyboard_shown().
        // sapp_keyboard_shown() is async on iOS (set only when keyboard finishes animating),
        // so we can't use it to gate Show(). Instead we watch Focus.Focused.
#if __ANDROID__ || __IOS__
        {
            // Pass the MEASURED keyboard height, 0 meaning "unknown" — the overlay places itself out
            // of a keyboard's way differently in each case. Substituting a guessed fraction here is
            // what used to bury the proxy under a landscape keyboard.
            float kbH = KeyboardHeight;
            var focused = Focus.Focused;

            bool nonProxyTextFocused =
                focused is TextBox ftb && !ftb.SkipKeyboardManagement ||
                focused is TextArea fta && fta.IsEditable && !fta.SkipKeyboardManagement;

            bool proxyHasFocus =
                _mobileOverlay?.IsActive == true &&
                _mobileOverlay.Children.Count > 0 &&
                focused == _mobileOverlay.Children[0];

            if (nonProxyTextFocused && !MobileOverlay.IsActive)
                MobileOverlay.Show(focused!, kbH);
            else if (!nonProxyTextFocused && !proxyHasFocus && MobileOverlay.IsActive)
                MobileOverlay.Hide();
            else if (MobileOverlay.IsActive && KeyboardHeight > 0)
                MobileOverlay.UpdateKeyboardHeight(kbH);

            // Also hide if keyboard was externally dismissed (Android back button, iOS swipe-down)
            // while proxy still has focus. Only fires after keyboard was confirmed shown.
            if (_mobileOverlay?.IsActive == true)
            {
                if (kbShown)  _keyboardConfirmed = true;
                else if (_keyboardConfirmed) { MobileOverlay.Hide(); _keyboardConfirmed = false; }
            }
            else
            {
                _keyboardConfirmed = false;
            }
        }
#endif

        // Keep Bounds in sync with the window size.
        if (Bounds.Width != width || Bounds.Height != height)
        {
            Sokol.SLog.Info($"GUI: Window resized → {width:F0}x{height:F0} (dpi={dpiScale:F2})", "Sokol.GUI");
            Bounds = new Rect(0, 0, width, height);
            InvalidateLayout();
        }

        DbgFrame++;

        // Animate all widgets.
        AnimationManager.Instance?.Update();

        // Check tooltip hover-delay each frame.
        Input.UpdateTooltip();

        // Advance inertial (fling) scrolling each frame.
        Input.UpdateFling();

        // The mobile keyboard overlay must be the LAST root child. Root children are drawn — and
        // hit-tested — in order, and the overlay is attached once and then left alone, so ANY root
        // child added afterwards buries it: an app that mounts a new screen by appending its
        // container (the usual overlapping push/pop transition) leaves the floating edit box
        // *behind* the screen the box belongs to. Re-appending moves it back to the end.
        if (_mobileOverlay != null && _mobileOverlay.Parent == this &&
            !ReferenceEquals(Children[^1], _mobileOverlay))
            AddChild(_mobileOverlay);

        // Screen root children fill the window — bypass CanvasLayout measurement.
        // CanvasLayout would measure TabView as (0,0) because tabs live in _tabs not _children.
        // Instead, set each root child's Bounds to the full window then run its internal layout.
        // bool logLayout = DbgFrame <= 5 || DbgFrame % 300 == 0;
        // if (logLayout)
        //     Sokol.SLog.Info($"GUI.Layout[{DbgFrame}]: filling {Children.Count} root children to {width:F0}x{height:F0}", "Sokol.GUI");

        foreach (var child in Children)
        {
            child.Bounds = new Rect(0, 0, width, height);
            child.PerformLayout(Renderer, true);
            // if (logLayout)
            //     Sokol.SLog.Info($"GUI.Layout[{DbgFrame}]:   {child.GetType().Name} Bounds={child.Bounds}", "Sokol.GUI");
        }
    }

    public void Draw(float width, float height, float dpiScale, bool drawActiveOverlay = true)
    {
        if (Renderer == null || Renderer.VGContext == IntPtr.Zero ) return; // can happen if Draw is called before Init   

        Renderer.BeginFrame(width, height, dpiScale);
        DrawChildren(Renderer);

        // Draw widgets registered via AddFrameOverlay (e.g. console/inspector inside ImGui dock).
        foreach (var (w, r) in _frameOverlays)
        {
            // Include screen position in Bounds so ScreenPosition (used by HitTestDeep) is correct.
            w.Bounds = new Rect(r.X, r.Y, r.Width, r.Height);
            // force=true: re-layout every frame so dock resize is reflected immediately.
            w.PerformLayout(Renderer, true);
            Renderer.Save();
            Renderer.Translate(r.X, r.Y);
            Renderer.ClipRect(new Rect(0, 0, r.Width, r.Height));
            w.Draw(Renderer);
            Renderer.Restore();
        }
        // Snapshot overlays for hit-testing between frames before clearing.
        _hitTestOverlays = _frameOverlays.ToArray();
        _frameOverlays.Clear();

        // Top overlays: lay out now (for hit-testing + the deferred post-ImGui draw) but do NOT
        // draw them in this pass — DrawTopOverlays() composites them after ImGui.
        foreach (var (w, r) in _topOverlays)
        {
            w.Bounds = new Rect(r.X, r.Y, r.Width, r.Height);
            w.PerformLayout(Renderer, true);
        }
        _topHitTest        = _topOverlays.ToArray();
        _topOverlaysToDraw = _topOverlays.ToArray();
        _topOverlays.Clear();

        // Draw notification toasts on top of everything.
        _notificationHost.Bounds = new Rect(0, 0, width, height);
        _notificationHost.Draw(Renderer);

        // Drag ghost on top of popups/notifications, under tooltip.
        Drag.DrawGhost(Renderer);

        // Draw tooltip overlay on top of everything else.
        var tip = TooltipControl.Shared;
        if (tip.Visible)
        {
            Renderer.Save();
            Renderer.Translate(tip.Bounds.X, tip.Bounds.Y);
            tip.Draw(Renderer);
            Renderer.Restore();
        }

        // Popups + top overlays must render ON TOP of everything. There are two strategies, chosen by
        // whether the caller layers something (e.g. ImGui) BETWEEN the UI and the popup:
        //
        //  • drawActiveOverlay == true (no external layer — pure Sokol.GUI apps): draw them VISIBLY
        //    here, last, within THIS single NanoVG frame. Crucially this avoids opening a SECOND NanoVG
        //    frame: a second frame's font-atlas flush is a second sg_update_image on the font texture in
        //    the SAME Sokol frame, which trips VALIDATE_UPDIMG_ONCE → panic whenever the popup baked a
        //    new glyph (e.g. a ComboBox dropdown opening). One frame ⇒ one atlas update ⇒ no panic.
        //
        //  • drawActiveOverlay == false (caller draws ImGui first — GameEditor): only PRE-WARM the
        //    glyphs here (invisibly) so the caller's later DrawActivePopupOverlay/DrawTopOverlays second
        //    frame finds them already in the atlas and never re-flushes it.
        if (drawActiveOverlay)
        {
            if (_activePopup != null)
            {
                Renderer.Save();
                var sp = _activePopup.ScreenPosition;
                Renderer.Translate(sp.X, sp.Y);
                _activePopup.DrawPopupOverlay(Renderer);
                Renderer.Restore();
            }
            foreach (var (w, r) in _topOverlaysToDraw)
            {
                Renderer.Save();
                Renderer.Translate(r.X, r.Y);
                Renderer.ClipRect(new Rect(0, 0, r.Width, r.Height));
                w.Draw(Renderer);
                Renderer.Restore();
            }
            Renderer.EndFrame();
        }
        else
        {
            if (_activePopup != null)
            {
                Renderer.Save();
                Renderer.SetGlobalAlpha(0f);
                var sp = _activePopup.ScreenPosition;
                Renderer.Translate(sp.X, sp.Y);
                _activePopup.DrawPopupOverlay(Renderer);
                Renderer.Restore();
            }
            foreach (var (w, r) in _topOverlaysToDraw)
            {
                Renderer.Save();
                Renderer.SetGlobalAlpha(0f);
                Renderer.Translate(r.X, r.Y);
                Renderer.ClipRect(new Rect(0, 0, r.Width, r.Height));
                w.Draw(Renderer);
                Renderer.Restore();
            }
            Renderer.EndFrame();
            // The caller composites the REAL popup/top overlays after its layered (ImGui) render via
            // DrawActivePopupOverlay() / DrawTopOverlays().
        }
    }

    /// <summary>
    /// Draws the active popup overlay in a separate NanoVG pass.
    /// Call this AFTER any layered rendering (e.g. ImGui) that should appear
    /// below the popup, so the popup renders on top of those layers.
    /// </summary>
    public void DrawActivePopupOverlay(float width, float height, float dpiScale)
    {
        if (Renderer == null || Renderer.VGContext == IntPtr.Zero) return;
        if (_activePopup == null) return;

        Renderer.BeginFrame(width, height, dpiScale);
        var sp = _activePopup.ScreenPosition;
        Renderer.Save();
        Renderer.Translate(sp.X, sp.Y);
        _activePopup.DrawPopupOverlay(Renderer);
        Renderer.Restore();
        Renderer.EndFrame();
    }

    /// <summary>
    /// Draws top overlays (modal dialogs registered via <see cref="AddTopOverlay"/>) in a separate
    /// NanoVG pass AFTER ImGui, so they appear above ImGui viewport windows. Glyphs were pre-warmed
    /// in <see cref="Draw"/> so this second frame never re-bakes the atlas (avoids a duplicate
    /// sg_update_image in the same Sokol frame).
    /// </summary>
    public void DrawTopOverlays(float width, float height, float dpiScale)
    {
        if (Renderer == null || Renderer.VGContext == IntPtr.Zero) return;
        if (_topOverlaysToDraw.Length == 0) return;

        Renderer.BeginFrame(width, height, dpiScale);
        foreach (var (w, r) in _topOverlaysToDraw)
        {
            Renderer.Save();
            Renderer.Translate(r.X, r.Y);
            Renderer.ClipRect(new Rect(0, 0, r.Width, r.Height));
            w.Draw(Renderer);
            Renderer.Restore();
        }
        Renderer.EndFrame();
    }

    /// <summary>Draw only children (Screen itself has no visual background).</summary>
    private void DrawChildren(Renderer renderer)
    {
        // bool logDraw = false; // DbgFrame <= 5 || DbgFrame % 300 == 0;
        // if (logDraw)
        //     Sokol.SLog.Info($"GUI.Draw[{DbgFrame}]: {Children.Count} direct screen children", "Sokol.GUI");

        // Snapshot the list to avoid "collection modified during enumeration" if a
        // child adds/removes siblings during Draw (e.g. Notification, Tooltip, Popup).
        var count = Children.Count;
        var list = new Widget[count];
        for (int i = 0; i < count; i++) list[i] = Children[i];

        for (int i = 0; i < count; i++)
        {
            var child = list[i];
            // if (logDraw)
            //     Sokol.SLog.Info($"GUI.Draw[{DbgFrame}]:   {child.GetType().Name} Bounds={child.Bounds} Visible={child.Visible}", "Sokol.GUI");

            if (!child.Visible) continue;
            renderer.Save();
            renderer.Translate(child.Bounds.X, child.Bounds.Y);
            child.Draw(renderer);
            renderer.Restore();
        }
    }

    public unsafe void DispatchEvent(sapp_event* e) => Input.Dispatch(e);

    // ─── Popup hit-test override ─────────────────────────────────────────────
    /// <summary>
    /// Check the active popup widget first (it may draw outside its parent's bounds),
    /// then fall back to the normal tree walk.
    /// </summary>
    // ─── Popup support ───────────────────────────────────────────────────────
    /// <summary>
    /// Dismiss the active popup if the click target is outside it.
    /// Called only on mouse-down so hover/move never close the popup prematurely.
    /// </summary>
    internal static void DismissActivePopupIfNeeded(Widget? clickTarget)
    {
        if (_activePopup != null && clickTarget != _activePopup)
        {
            CloseActivePopup();
        }
    }

    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        // Top overlays (modal dialogs drawn above ImGui) get first claim on input.
        for (int i = _topHitTest.Length - 1; i >= 0; i--)
        {
            var hit = _topHitTest[i].Widget.HitTestDeep(screenPoint);
            if (hit != null) return hit;
        }

        // Popup has highest priority (may extend beyond widget bounds).
        if (_activePopup != null)
        {
            // Ask the popup to test the point using its own local coordinate space.
            var local = _activePopup.ToLocal(screenPoint);
            if (_activePopup.HitTest(local))
            {
                // Sokol.SLog.Info($"HitTest: popup {_activePopup.GetType().Name} captured screenPoint={screenPoint} local={local}", "Sokol.GUI");
                return _activePopup;
            }
            // Mouse is outside the popup — do NOT dismiss here (dismissal
            // happens only on click via DismissActivePopupIfNeeded).
            // Fall through so overlays and other widgets can be hovered.
        }

        // Frame overlays draw on top of screen children — check them before normal tree walk.
        // w.Bounds includes the screen-space position, so w.HitTestDeep uses correct coords.
        for (int i = _hitTestOverlays.Length - 1; i >= 0; i--)
        {
            var (w, _) = _hitTestOverlays[i];
            var hit = w.HitTestDeep(screenPoint);
            if (hit != null) return hit;
        }

        return base.HitTestDeep(screenPoint);
    }

    // ─── Override Draw to avoid double-translation ───────────────────────────
    public override void Draw(Renderer renderer) => DrawChildren(renderer);
}
