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
                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var delta = new Vector2(ev->mouse_dx / dpi, ev->mouse_dy / dpi);
                var me = new MouseEvent { Position = pos, Delta = delta, Modifiers = Mods(ev) };
                UpdateHovered(pos, me);
                (_captured ?? _hovered)?.OnMouseMove(me);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
            {
                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var btn = MapButton(ev->mouse_button);
                _clickCount = IsDoubleClick(pos, btn) ? 2 : 1;
                _lastButton = btn; _lastClickX = pos.X; _lastClickY = pos.Y;
                _lastClickTime = stm_sec(stm_now());
                var me = new MouseEvent { Position = pos, Button = btn, Clicks = _clickCount, Modifiers = Mods(ev) };
                var target = _screen.HitTestDeep(pos);
                Sokol.SLog.Info($"GUI: MouseDown ({pos.X:F0},{pos.Y:F0}) btn={btn} → {target?.GetType().Name ?? "none"}[{target?.Id ?? "-"}]", "Sokol.GUI");
                if (target != null)
                {
                    _captured = target;
                    if (btn == MouseButton.Left)
                        _focus.SetFocus(target);
                    target.OnMouseDown(me);
                }
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
            {
                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var btn = MapButton(ev->mouse_button);
                var me = new MouseEvent { Position = pos, Button = btn, Clicks = _clickCount, Modifiers = Mods(ev) };
                (_captured ?? _hovered)?.OnMouseUp(me);
                _captured = null;
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
            {
                var pos = new Vector2(ev->mouse_x / dpi, ev->mouse_y / dpi);
                var me = new MouseEvent { Position = pos, Scroll = new Vector2(ev->scroll_x, ev->scroll_y), Modifiers = Mods(ev) };
                _hovered?.OnMouseScroll(me);
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
                if (target != null)
                {
                    _captured = target;
                    _focus.SetFocus(target);
                    // Also update hover so widgets enter hovered state
                    if (_hovered != target)
                    {
                        _hovered?.OnMouseLeave(me);
                        _hovered = target;
                        _hovered.OnMouseEnter(me);
                    }
                    target.OnMouseDown(me);
                }
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
            {
                var me = new MouseEvent { Position = pos };
                UpdateHovered(pos, me);
                (_captured ?? _hovered)?.OnMouseMove(me);
                break;
            }
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
            case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_CANCELLED:
            {
                var me = new MouseEvent { Position = pos, Button = MouseButton.Left, Clicks = 1 };
                (_captured ?? _hovered)?.OnMouseUp(me);
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
