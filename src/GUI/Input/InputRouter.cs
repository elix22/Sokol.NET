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

    public InputRouter(Screen screen, FocusManager focus)
    {
        _screen = screen;
        _focus  = focus;
    }

    public unsafe void Dispatch(sapp_event* ev)
    {

#if __ANDROID__
        float dpi  = 1f; // TBD ELI , unreliable on Android
#else
        float dpi  = sapp_dpi_scale();
#endif

        switch (ev->type)
        {
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
            {
                var pos   = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var delta = new Vector2(ev->mouse_dx / dpi, ev->mouse_dy / dpi);
                var me    = new MouseEvent { Position = pos, Delta = delta, Modifiers = Mods(ev) };
                UpdateHovered(pos, me);
                var moveTarget = _captured ?? _hovered;
                moveTarget?.OnMouseMove(Localize(moveTarget, me));
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
                    _screen.Drag.OnMouseDown(target, pos);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
            {
                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var btn = MapButton(ev->mouse_button);
                var me  = new MouseEvent { Position = pos, Button = btn, Clicks = _clickCount, Modifiers = Mods(ev) };
                var upTarget = _captured ?? _hovered;
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
                // Bubble up the parent chain until a widget consumes the scroll.
                var scrollTarget = _hovered;
                while (scrollTarget != null)
                {
                    if (scrollTarget.OnMouseScroll(Localize(scrollTarget, me))) break;
                    scrollTarget = scrollTarget.Parent;
                }
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN:
            {
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
        // Translate primary touch (id==0 or first changed point) into synthetic
        // mouse events so all widgets work on mobile without per-widget changes.
        TouchPoint? primary = null;
        foreach (var pt in te.Touches)
        {
            if (pt.Changed) { primary = pt; break; }
        }
        if (primary == null && te.Touches.Length > 0)
            primary = te.Touches[0];
        if (primary == null) return;

        var pos = primary.Position;

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
                _screen.Drag.OnMouseDown(target, pos);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
            {
                var me = new MouseEvent { Position = pos };
                UpdateHovered(pos, me);
                var moveTarget = _captured ?? _hovered;
                if (moveTarget != null)
                    moveTarget.OnMouseMove(Localize(moveTarget, me));
                _screen.Drag.OnMouseMove(_hovered, pos);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED:
            {
                var me = new MouseEvent { Position = pos, Button = MouseButton.Left, Clicks = 1 };
                var upTarget = _captured ?? _hovered;
                if (upTarget != null)
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

        // Also forward raw touch events to widgets that handle them explicitly.
        foreach (var pt in te.Touches)
        {
            var target = _screen.HitTestDeep(pt.Position);
            if (target == null) continue;
            switch (type)
            {
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN:   target.OnTouchDown(te); break;
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED: target.OnTouchUp(te); break;
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:   target.OnTouchMove(te); break;
            }
        }
    }
}
