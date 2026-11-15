using System.Runtime.InteropServices;
using static Sokol.SApp;
using static Sokol.STM;

namespace Sokol.GUI;

/// <summary>
/// Translates raw sokol <c>sapp_event</c> structs into typed <see cref="InputEvent"/>
/// objects and dispatches them into the widget tree.
/// </summary>
public sealed class InputRouter
{
    private readonly Screen       _screen;
    private readonly FocusManager _focus;
    private          Widget?      _hovered;
    private          Widget?      _captured;   // widget that captured mouse-down

    // Tooltip hover-delay tracking
    private Vector2 _lastMousePos;
    private double  _hoverStartTime;
    private bool    _tooltipShown;
    private string? _lastTooltipText;
    private const double TooltipDelaySec = 0.5;

    // Button-click tracking
    private MouseButton _lastButton;
    private float       _lastClickX, _lastClickY;
    private double      _lastClickTime;
    private int         _clickCount;

    // Touch ownership: maps touch id → widget that received TOUCHES_BEGAN for that touch
    private readonly Dictionary<int, Widget> _touchOwners = new();

    // ── Drag-to-scroll (finger-flick / drag-anywhere panning over a ScrollView) ──
    private ScrollView? _scrollDrag;                 // scroll candidate for the active press
    private bool        _scrolling;                  // past the slop threshold → actively panning
    private Vector2     _scrollDownPos, _scrollLastPos, _scrollVel;
    private ScrollView? _fling;                      // active inertial target (after release)
    private Vector2     _flingVel;
    private const float ScrollSlop = 8f;             // px before a press becomes a pan

    public InputRouter(Screen screen, FocusManager focus)
    {
        _screen = screen;
        _focus  = focus;
    }

    /// <summary>True while a widget is actively capturing mouse down/move/up.</summary>
    public bool HasMouseCapture => _captured != null;

    /// <summary>The widget currently holding mouse capture, if any.</summary>
    public Widget? CapturedWidget => _captured;

    public unsafe void Dispatch(sapp_event* ev)
    {

#if __ANDROID__
        float dpi  = 1f; // TBD ELI , unreliable on Android
#else
        float dpi  = sapp_dpi_scale();
#endif
        if (_screen.PointerScale > 0f) dpi = _screen.PointerScale;   // the app's own UI scale (Screen.PointerScale)

        switch (ev->type)
        {
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
            {
                var pos   = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var delta = new Vector2(ev->mouse_dx / dpi, ev->mouse_dy / dpi);
                var me    = new MouseEvent { Position = pos, Delta = delta, Modifiers = Mods(ev) };
                UpdateHovered(pos, me);
                var moveTarget = _captured ?? _hovered;
                if (_scrolling)
                {
                    ScrollDragMove(pos, widgetConsumedMove: false);   // panning: don't feed the widget
                }
                else
                {
                    bool consumed = moveTarget?.OnMouseMove(Localize(moveTarget, me)) ?? false;
                    ScrollDragMove(pos, consumed);
                }
                // Drag-and-drop tracking runs after the widget's own move handler
                // so widgets can observe state before drag callbacks fire.
                _screen.Drag.OnMouseMove(_hovered, pos);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
            {
                // Hide tooltip on click
                _tooltipShown = false;
                TooltipControl.Shared.Hide();

                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var btn = MapButton(ev->mouse_button);
                _clickCount = IsDoubleClick(pos, btn) ? 2 : 1;
                _lastButton = btn; _lastClickX = pos.X; _lastClickY = pos.Y;
                _lastClickTime = stm_sec(stm_now());
                var me = new MouseEvent { Position = pos, Button = btn, Clicks = _clickCount, Modifiers = Mods(ev) };
                var target = _screen.HitTestDeep(pos);
                // Dismiss any open popup (ComboBox dropdown, ColorPicker, etc.) if
                // the click landed outside it.
                Screen.DismissActivePopupIfNeeded(target);
                if (target != null)
                {
                    _captured = target;
                    if (btn == MouseButton.Left)
                        _focus.SetFocus(target);
                    target.OnMouseDown(Localize(target, me));
                }
                if (btn == MouseButton.Left)
                {
                    ScrollDragBegin(target, pos);
                    _screen.Drag.OnMouseDown(target, pos);
                }
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
            {
                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var btn = MapButton(ev->mouse_button);
                var me  = new MouseEvent { Position = pos, Button = btn, Clicks = _clickCount, Modifiers = Mods(ev) };
                var upTarget = _captured ?? _hovered;
                if (!ScrollDragEnd())                             // if we were panning, swallow the up (no click)
                    upTarget?.OnMouseUp(Localize(upTarget, me));
                _captured = null;
                if (btn == MouseButton.Left)
                    _screen.Drag.OnMouseUp(_hovered, pos);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
            {
                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var me  = new MouseEvent { Position = pos, Scroll = new Vector2(ev->scroll_x, ev->scroll_y), Modifiers = Mods(ev) };
                // Use HitTestDeep so active modal popups always capture scroll even
                // when _hovered is stale (e.g. popup opened after last mouse-move).
                var scrollTarget = _screen.HitTestDeep(pos) ?? _hovered;
                while (scrollTarget != null)
                {
                    if (scrollTarget.OnMouseScroll(Localize(scrollTarget, me))) break;
                    scrollTarget = scrollTarget.Parent;
                }
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN:
            {
                if (ev->key_code == sapp_keycode.SAPP_KEYCODE_ESCAPE)
                {
                    if (Screen.CloseActivePopup())
                        break;
                }

                var ke = new KeyEvent { KeyCode = (int)ev->key_code, Repeat = ev->key_repeat, Modifiers = Mods(ev) };
                _focus.Focused?.OnKeyDown(ke);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_KEY_UP:
            {
                var ke = new KeyEvent { KeyCode = (int)ev->key_code, Modifiers = Mods(ev) };
                _focus.Focused?.OnKeyUp(ke);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_CHAR:
            {
                var ke = new KeyEvent { CharCode = ev->char_code, Modifiers = Mods(ev) };
                _focus.Focused?.OnTextInput(ke);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN:
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED:
            {
                var te = BuildTouchEvent(ev, dpi);
                DispatchTouch(ev->type, te);
                break;
            }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void UpdateHovered(Vector2 pos, MouseEvent me)
    {
        var newHover = _screen.HitTestDeep(pos);
        if (newHover != _hovered)
        {
            _hovered?.OnMouseLeave(me);
            _hovered = newHover;
            _hovered?.OnMouseEnter(me);
            // Reset tooltip tracking on hover change
            _hoverStartTime = stm_sec(stm_now());
            _tooltipShown = false;
            TooltipControl.Shared.Hide();
        }
        _lastMousePos = pos;
    }

    /// <summary>
    /// Called each frame by Screen.Update to check whether the tooltip delay has elapsed.
    /// </summary>
    internal void UpdateTooltip()
    {
        var currentText = _hovered?.Tooltip;

        // If tooltip text changed (e.g. ToolBar item changed), reset the timer
        if (currentText != _lastTooltipText)
        {
            _lastTooltipText = currentText;
            _hoverStartTime = stm_sec(stm_now());
            _tooltipShown = false;
            TooltipControl.Shared.Hide();
        }

        if (_tooltipShown) return;
        if (_hovered == null || string.IsNullOrEmpty(currentText))
        {
            TooltipControl.Shared.Hide();
            return;
        }

        double now = stm_sec(stm_now());
        if (now - _hoverStartTime >= TooltipDelaySec)
        {
            _tooltipShown = true;
            TooltipControl.Shared.Show(currentText!, _lastMousePos);
        }
    }

    private bool IsDoubleClick(Vector2 pos, MouseButton btn)
    {
        const double kDoubleClickSec = 0.35;
        const float  kDoubleClickDist = 5f;
        if (btn != _lastButton) return false;
        double now = stm_sec(stm_now());
        float dx = pos.X - _lastClickX, dy = pos.Y - _lastClickY;
        return (now - _lastClickTime) < kDoubleClickSec &&
               (dx * dx + dy * dy) < kDoubleClickDist * kDoubleClickDist;
    }

    private static MouseEvent Localize(Widget target, MouseEvent e)
    {
        var local = target.ToLocal(e.Position);
        return new MouseEvent
        {
            Position      = e.Position,
            LocalPosition = local,
            Delta         = e.Delta,
            Button        = e.Button,
            Clicks        = e.Clicks,
            Scroll        = e.Scroll,
            Modifiers     = e.Modifiers,
        };
    }

    private static MouseButton MapButton(sapp_mousebutton b) => b switch
    {
        sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT   => MouseButton.Left,
        sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE => MouseButton.Middle,
        sapp_mousebutton.SAPP_MOUSEBUTTON_RIGHT  => MouseButton.Right,
        _                                        => MouseButton.None,
    };

    private static unsafe KeyModifiers Mods(sapp_event* e)
    {
        KeyModifiers m = KeyModifiers.None;
        if ((e->modifiers & (uint)SAPP_MODIFIER_SHIFT) != 0) m |= KeyModifiers.Shift;
        if ((e->modifiers & (uint)SAPP_MODIFIER_CTRL)  != 0) m |= KeyModifiers.Control;
        if ((e->modifiers & (uint)SAPP_MODIFIER_ALT)   != 0) m |= KeyModifiers.Alt;
        if ((e->modifiers & (uint)SAPP_MODIFIER_SUPER) != 0) m |= KeyModifiers.Super;
        return m;
    }

    private static unsafe TouchEvent BuildTouchEvent(sapp_event* ev, float dpi)
    {
        int count = (int)ev->num_touches;
        var pts = new TouchPoint[count];
        for (int i = 0; i < count; i++)
        {
            pts[i] = new TouchPoint
            {
                Id       = (int)ev->touches[i].identifier,
                Position = new Vector2(ev->touches[i].pos_x / dpi, ev->touches[i].pos_y / dpi),
                Changed  = ev->touches[i].changed,
            };
        }
        return new TouchEvent { Touches = pts };
    }

    private void DispatchTouch(sapp_event_type type, TouchEvent te)
    {
        // Two-finger pinch over an IPinchZoomable ancestor claims the whole touch stream first —
        // see PinchUpdate. Inert when nothing in the tree implements the interface.
        if (PinchUpdate(type, te)) return;

        // Find primary (first changed) touch for synthetic mouse simulation.
        TouchPoint? primary = null;
        foreach (var pt in te.Touches)
            if (pt.Changed) { primary = pt; break; }
        if (primary == null && te.Touches.Length > 0)
            primary = te.Touches[0];
        if (primary == null) return;

        var pos = primary.Position;

        // Each widget must only see the touches it owns. Without filtering,
        // two fingers touching down on different widgets in the same event
        // causes both widgets to see both touch points, so a widget can
        // accidentally press a key for a touch it doesn't own — then never
        // receive the ENDED for it (since the router routes ENDED to the real
        // owner), leaving the key stuck.
        //
        // Strategy:
        //   1. For BEGAN: register new owners first so they appear in the table.
        //   2. Build per-widget touch lists (all owned touches, changed or not).
        //      Capture primaryTarget in the same pass.
        //   3. For ENDED/CANCELLED: remove ended touch IDs from the table.
        //   4. Dispatch a filtered TouchEvent to each widget that has ≥1 Changed touch.

        // Step 1 — register new owners for BEGAN.
        if (type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN)
        {
            foreach (var pt in te.Touches)
            {
                if (!pt.Changed) continue;
                var target = _screen.HitTestDeep(pt.Position);
                if (target != null) _touchOwners[pt.Id] = target;

            }
        }

        // Step 2 — build per-widget touch lists.
        Widget? primaryTarget = null;
        var targetTouches = new Dictionary<Widget, List<TouchPoint>>();
        foreach (var pt in te.Touches)
        {
            if (!_touchOwners.TryGetValue(pt.Id, out var owner)) continue;
            if (!targetTouches.TryGetValue(owner, out var list))
                targetTouches[owner] = list = new List<TouchPoint>();
            list.Add(pt);
            if (pt.Id == primary.Id) primaryTarget = owner;
        }

        // Step 3 — remove ended/cancelled touch IDs.
        if (type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED ||
            type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED)
        {
            foreach (var pt in te.Touches)
            {
                if (!pt.Changed) continue;

                _touchOwners.Remove(pt.Id);
            }
        }

        // Step 3b — ghost touch cleanup.
        // iOS can silently drop touches without TOUCHES_ENDED when the hardware touch
        // limit is exceeded. Detect any owned touch that no longer appears in the current
        // event's Touches array and fire a virtual OnTouchUp so keys don't get stuck.
        {
            var presentIds = new HashSet<int>(te.Touches.Length);
            foreach (var pt in te.Touches) presentIds.Add(pt.Id);
            var dropped = new List<(int id, Widget owner)>();
            foreach (var (id, owner) in _touchOwners)
                if (!presentIds.Contains(id)) dropped.Add((id, owner));
            foreach (var (id, owner) in dropped)
            {
                _touchOwners.Remove(id);
                var ghostPt = new TouchPoint { Id = id, Changed = true, Position = Vector2.Zero };
                owner.OnTouchUp(new TouchEvent { Touches = new[] { ghostPt } });
            }
        }

        // Step 4 — dispatch filtered events.
        bool primaryHandled = false;
        foreach (var (target, touches) in targetTouches)
        {
            if (!touches.Exists(t => t.Changed)) continue; // no state change for this widget
            var filteredTe = new TouchEvent { Touches = touches.ToArray() };
            bool handled = type switch
            {
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN     => target.OnTouchDown(filteredTe),
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED     => target.OnTouchUp(filteredTe),
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED => target.OnTouchUp(filteredTe),
                sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED     => target.OnTouchMove(filteredTe),
                _ => false,
            };
            if (target == primaryTarget) primaryHandled = handled;
        }

        // Skip synthetic mouse if no touch in the event actually changed state.
        // iOS sends TOUCHES_BEGAN with all ch=False when over the hardware touch limit —
        // if we let the synthetic path run, it calls Press(-1, semi) with no matching
        // mouse-up, leaving a key stuck. Also skip if a widget handled natively.
        if (!primary.Changed || primaryHandled) return;

        // Synthetic mouse fallback: lets widgets that only handle mouse events
        // work on touch screens without any per-widget changes.
        switch (type)
        {
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN:
            {
                var me = new MouseEvent { Position = pos, Button = MouseButton.Left, Clicks = 1 };
                var target = _screen.HitTestDeep(pos);
                // Dismiss any open popup if touch landed outside it.
                Screen.DismissActivePopupIfNeeded(target);
                if (target != null)
                {
                    _captured = target;
                    _focus.SetFocus(target);
                    // Also update hover so widgets enter hovered state
                    if (_hovered != target)
                    {
                        _hovered?.OnMouseLeave(me);
                        _hovered = target;
                        _hovered.OnMouseEnter(Localize(target, me));
                    }
                    // Fire a synthetic MouseMove before MouseDown so widgets that
                    // cache _mousePos from OnMouseMove (Accordion, TreeView, etc.)
                    // have the correct local position before OnMouseDown runs.
                    target.OnMouseMove(Localize(target, me));
                    target.OnMouseDown(Localize(target, me));
                }
                ScrollDragBegin(target, pos);
                _screen.Drag.OnMouseDown(target, pos);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
            {
                var me = new MouseEvent { Position = pos };
                UpdateHovered(pos, me);
                var moveTarget = _captured ?? _hovered;
                if (_scrolling)
                {
                    ScrollDragMove(pos, widgetConsumedMove: false);
                }
                else
                {
                    bool consumed = moveTarget?.OnMouseMove(Localize(moveTarget, me)) ?? false;
                    ScrollDragMove(pos, consumed);
                }
                _screen.Drag.OnMouseMove(_hovered, pos);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED:
            {
                var me = new MouseEvent { Position = pos, Button = MouseButton.Left, Clicks = 1 };
                var upTarget = _captured ?? _hovered;
                if (!ScrollDragEnd() && upTarget != null)         // swallow up if we were panning (no click)
                    upTarget.OnMouseUp(Localize(upTarget, me));
                // Drag.OnMouseUp must be called before clearing _hovered.
                _screen.Drag.OnMouseUp(_hovered, pos);
                // Leave hover on touch-end so the widget can visually deactivate
                _hovered?.OnMouseLeave(me);
                _hovered   = null;
                _captured  = null;
                break;
            }
        }
    }

    // ─── Pinch-to-zoom (two-finger, over an IPinchZoomable ancestor) ──────────
    // The touch-ownership model hands each finger to the deepest widget under it, so a zooming
    // CONTAINER can never assemble a pinch from its child's touches. The router arbitrates instead,
    // exactly like drag-to-scroll: arm when a second finger lands and both fingers share one
    // IPinchZoomable ancestor; un-press whatever the first finger pressed (the scroll-pan idiom, so
    // the gesture cannot double as a tap or a move); swallow the stream while the gesture lives AND
    // until every finger lifts, so the release cannot become a click either.

    private IPinchZoomable? _pinch;                  // live gesture target
    private int   _pinchIdA = -1, _pinchIdB = -1;    // the two fingers driving the scale
    private float _pinchDist;                        // finger distance at the last update
    // Every touch the gesture has swallowed, including fingers that joined it and the two that drive
    // it. ⛔ Ids, not a bool: the gesture's LAST fingers must keep being swallowed until they lift
    // (their release must not land as a tap), but a bool cooldown then also ate the user's NEXT tap
    // when both fingers lifted in one event and nothing followed to clear it. An id set cannot: a
    // fresh touch is simply not in it.
    private readonly HashSet<int> _pinchIds = new();

    private static IPinchZoomable? FindPinchAncestor(Widget? w)
    {
        for (var c = w; c != null; c = c.Parent)
            if (c is IPinchZoomable pz) return pz;
        return null;
    }

    private static float Dist(Vector2 a, Vector2 b)
        => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Feed a touch phase straight into the dispatcher, exactly as a platform event would —
    /// the ONE seam a test can use to drive a multi-finger gesture. ⛔ Test-only, and `internal` on
    /// purpose: the real path is <see cref="Dispatch"/>, which builds this same
    /// <see cref="TouchEvent"/> from the native <c>sapp_event</c>. Exists because no automated route
    /// reaches a two-finger gesture on a real device — Android's <c>sendevent</c> is SELinux-blocked
    /// to <c>adb shell</c> on every phone in the fleet, and iOS has no injection at all — so without
    /// it the pinch could only ever be "tried by hand once".</summary>
    internal void TestTouch(sapp_event_type type, TouchEvent te) => DispatchTouch(type, te);

    /// <summary>Advance the pinch gesture. Returns true when the event belongs to it (the caller must
    /// not dispatch it further).</summary>
    private bool PinchUpdate(sapp_event_type type, TouchEvent te)
    {
        bool ending = type is sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED
                           or sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED;

        if (_pinch == null && _pinchIds.Count == 0)
        {
            // Arm on the event that brings the finger count to two, when both fingers stand over the
            // SAME pinch-zoomable ancestor. Anything else is not ours — one finger never is, so a tap
            // and a drag reach the board exactly as before.
            if (type != sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN || te.Touches.Length != 2) return false;
            var a = te.Touches[0]; var b = te.Touches[1];
            var pa = FindPinchAncestor(_screen.HitTestDeep(a.Position));
            if (pa == null || !ReferenceEquals(pa, FindPinchAncestor(_screen.HitTestDeep(b.Position)))) return false;

            _pinch = pa; _pinchIdA = a.Id; _pinchIdB = b.Id;
            _pinchDist = Dist(a.Position, b.Position);
            _pinchIds.Add(a.Id); _pinchIds.Add(b.Id);

            // The first finger may already have pressed something (its BEGAN dispatched normally):
            // un-press it the way drag-to-scroll does, and close any native touch state, so the pinch
            // cannot also land as a tap, a drag or a stuck key.
            _captured?.OnMouseLeave(new MouseEvent { Position = a.Position });
            _captured  = null;
            _scrolling = false; _scrollDrag = null;
            foreach (var t in te.Touches)
                if (_touchOwners.Remove(t.Id, out var owner))
                    owner.OnTouchUp(new TouchEvent
                    {
                        Touches = new[] { new TouchPoint { Id = t.Id, Changed = true, Position = t.Position } }
                    });
            return true;
        }

        // Is this event the gesture's? Only when every touch it CHANGES belongs to it — so a fresh
        // finger landing while the gesture's last one is still down dispatches normally.
        bool mine = false, foreign = false;
        foreach (var t in te.Touches)
        {
            if (!t.Changed) continue;
            if (_pinchIds.Contains(t.Id)) mine = true; else foreign = true;
        }
        if (!mine || foreign)
        {
            // While the gesture is LIVE a new finger joins it rather than acting on its own (a third
            // finger mid-pinch is not a tap); once it is over, a foreign touch is the user's next
            // gesture and must pass through.
            if (_pinch == null) return false;
            foreach (var t in te.Touches) if (t.Changed) _pinchIds.Add(t.Id);
        }

        if (ending)
            foreach (var t in te.Touches)
                if (t.Changed) _pinchIds.Remove(t.Id);

        if (_pinch == null) return true;                  // winding down: swallow our own fingers' tail

        // Follow the two fingers that drive the scale. Either lifting ends the gesture — as does
        // either being silently DROPPED (iOS does that over its touch limit, the same hazard the
        // ghost-touch cleanup below exists for). Fingers still down stay in _pinchIds, so their
        // release is swallowed rather than landing as a tap.
        TouchPoint? ta = null, tb = null;
        foreach (var t in te.Touches)
        {
            if (t.Id == _pinchIdA) ta = t;
            else if (t.Id == _pinchIdB) tb = t;
        }
        if (ta == null || tb == null || !_pinchIds.Contains(_pinchIdA) || !_pinchIds.Contains(_pinchIdB))
        {
            _pinch = null; _pinchIdA = _pinchIdB = -1;
            return true;
        }
        if (type == sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED)
        {
            float d = Dist(ta.Position, tb.Position);
            if (d > 1f && _pinchDist > 1f)
            {
                var mid = new Vector2((ta.Position.X + tb.Position.X) * 0.5f,
                                      (ta.Position.Y + tb.Position.Y) * 0.5f);
                _pinch.OnPinchZoom(d / _pinchDist, mid);
                _pinchDist = d;
            }
        }
        return true;
    }

    // ─── Drag-to-scroll (finger-flick / drag-anywhere panning) ────────────────

    private static ScrollView? FindScrollAncestor(Widget? w)
    {
        for (var c = w; c != null; c = c.Parent)
            if (c is ScrollView sv && (sv.CanDragScrollV || sv.CanDragScrollH))
                return sv;
        return null;
    }

    private void ScrollDragBegin(Widget? target, Vector2 pos)
    {
        _fling      = null;                 // a new press cancels any momentum
        _scrolling  = false;
        _scrollDrag = FindScrollAncestor(target);
        _scrollDownPos = _scrollLastPos = pos;
        _scrollVel  = new Vector2(0f, 0f);
    }

    /// <summary>Handle a pointer move for the active press. Returns true if it was
    /// consumed as a pan (caller should not deliver the move to the widget).</summary>
    private bool ScrollDragMove(Vector2 pos, bool widgetConsumedMove)
    {
        if (_scrollDrag == null) return false;
        // A real drag-and-drop owns the gesture — never hijack it to scroll.
        if (_screen.Drag.IsActive) { _scrollDrag = null; _scrolling = false; return false; }

        if (!_scrolling)
        {
            // The widget itself handled the drag (slider, number-drag, tree-reorder) → leave it alone.
            if (widgetConsumedMove) { _scrollDrag = null; return false; }
            float ddx = pos.X - _scrollDownPos.X, ddy = pos.Y - _scrollDownPos.Y;
            bool pastV = _scrollDrag.CanDragScrollV && MathF.Abs(ddy) > ScrollSlop;
            bool pastH = _scrollDrag.CanDragScrollH && MathF.Abs(ddx) > ScrollSlop;
            if (!pastV && !pastH) return false;
            _scrolling = true;
            _captured?.OnMouseLeave(new MouseEvent { Position = pos });   // un-press the widget; no click
            _scrollLastPos = pos;
        }

        float mdx = _scrollLastPos.X - pos.X;
        float mdy = _scrollLastPos.Y - pos.Y;
        _scrollDrag.DragScrollBy(mdx, mdy);
        _scrollVel = new Vector2(0.6f * _scrollVel.X + 0.4f * mdx, 0.6f * _scrollVel.Y + 0.4f * mdy);
        _scrollLastPos = pos;
        return true;
    }

    /// <summary>End the active press. Returns true if a pan was in progress (the caller
    /// should swallow the mouse-up so it doesn't become a click).</summary>
    private bool ScrollDragEnd()
    {
        bool was = _scrolling;
        if (was && (MathF.Abs(_scrollVel.X) > 0.5f || MathF.Abs(_scrollVel.Y) > 0.5f))
        {
            _fling    = _scrollDrag;
            _flingVel = _scrollVel;
        }
        _scrolling  = false;
        _scrollDrag = null;
        return was;
    }

    /// <summary>Advance inertial scrolling. Called once per frame by Screen.Update.</summary>
    internal void UpdateFling()
    {
        if (_fling == null) return;
        bool moved = _fling.DragScrollBy(_flingVel.X, _flingVel.Y);
        _flingVel  = new Vector2(_flingVel.X * 0.88f, _flingVel.Y * 0.88f);
        if (!moved || (MathF.Abs(_flingVel.X) < 0.5f && MathF.Abs(_flingVel.Y) < 0.5f))
            _fling = null;
    }
}
