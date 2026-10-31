using System;
using System.Text;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Single-line editable text input.
/// Features: click-to-place caret, drag-to-select, double-click word select,
/// blinking caret, Shift+arrows, Ctrl+Left/Right word navigation,
/// Ctrl+A/C/V/X/Z, Ctrl+Backspace/Delete, virtual keyboard on mobile.
/// </summary>
public class TextBox : Widget
{
    // ── Core state ────────────────────────────────────────────────────────────
    private readonly StringBuilder _sb       = new();
    private int   _cursor;
    private int   _selStart  = -1;    // -1 = no selection; anchor when >= 0
    private float _scrollX;
    private bool  _mouseDragging;

    // ── Undo (single level) ───────────────────────────────────────────────────
    private string? _undoText;
    private int     _undoCursor;
    private bool    _undoDirty;       // an edit has been made since last undo-save

    // ── Blinking caret ────────────────────────────────────────────────────────
    private long   _blinkEpoch;       // Environment.TickCount64 when focus gained / reset
    private const long BlinkOnMs  = 530;
    private const long BlinkOffMs = 530;

    // ── Layout cache (from last Draw; used for click-to-caret hit testing) ────
    private float[] _charXCache  = Array.Empty<float>();
    private float   _innerLeft;
    private float   _innerWidth;
    private float   _alignOffset;

    // ─── Extensibility hooks for derived classes ──────────────────────────────
    /// <summary>Current caret position (character index). Readable/settable by derived classes.</summary>
    protected int Cursor
    {
        get => _cursor;
        set => _cursor = Math.Clamp(value, 0, _sb.Length);
    }

    /// <summary>Return false to block a typed character. Default: allow all printable chars.</summary>
    protected virtual bool IsCharAllowed(char c) => true;

    /// <summary>Override to customise the border colour (e.g. red for invalid input).</summary>
    protected virtual UIColor GetBorderColor(Theme theme) =>
        IsFocused ? theme.AccentColor : theme.BorderColor;

    /// <summary>Override to customise the border width.</summary>
    protected virtual float GetBorderWidth(Theme theme) => IsFocused ? 2f : 1.5f;

    public override bool AcceptsFocus => true;

    // ─── Mobile ───────────────────────────────────────────────────────────────
    /// <summary>
    /// When true, OnFocusGained/Lost do not call sapp_show_keyboard or EnsureVisible.
    /// Set on proxy instances inside MobileKeyboardOverlay.
    /// </summary>
    internal bool SkipKeyboardManagement { get; set; }

    // ─── Public API ───────────────────────────────────────────────────────────
    public string Text
    {
        get => _sb.ToString();
        set
        {
            _sb.Clear();
            _sb.Append(value ?? string.Empty);
            _cursor   = _sb.Length;
            _selStart = -1;
            _scrollX  = 0;
            _undoDirty = false;
            TextChanged?.Invoke(Text);
        }
    }

    public string? Placeholder       { get; set; }
    public bool    IsPassword        { get; set; }
    public int     MaxLength         { get; set; } = 0;
    public UIColor? BackColor        { get; set; }
    public UIColor? ForeColor        { get; set; }
    public UIColor? PlaceholderColor { get; set; }
    public UIColor? SelectionColor   { get; set; }
    public UIColor? CursorColor      { get; set; }
    public Font?    Font             { get; set; }
    public float    FontSize         { get; set; } = 0f;
    public TextAlign Align           { get; set; } = TextAlign.Left;

    /// <summary>When true, typed and pasted characters are forced to uppercase as they're inserted, so the
    /// field reads back in capitals on every platform regardless of OS/soft-keyboard casing. Used for
    /// case-insensitive codes (e.g. BLE group/game join codes).</summary>
    public bool    Uppercase         { get; set; }

    public event Action<string>? TextChanged;
    public event Action?         Submitted;

    /// <summary>Raised when the box loses keyboard focus — the commit-on-blur partner to
    /// <see cref="Submitted"/> (Enter). A field that only commits on Enter silently drops an edit when
    /// the user taps elsewhere, which on a phone is the usual way to leave a field.
    /// <para>⚠ On desktop, Enter fires <see cref="Submitted"/> AND advances focus, so a box wired to
    /// both gets two notifications for one edit — make the handler idempotent (no-op when the value is
    /// unchanged) rather than expecting exactly one call.</para></summary>
    public event Action?         FocusLost;

    /// <summary>Called by <see cref="MobileKeyboardOverlay"/> when the PROXY box above the virtual
    /// keyboard is submitted / dismissed. On mobile the user never types into this box — a proxy takes
    /// focus and mirrors its text back — so without these relays <see cref="Submitted"/> never fires at
    /// all on a phone and <see cref="FocusLost"/> fires at the wrong moment (when the proxy TAKES focus,
    /// before anything is typed). A field wired to either event would then work on desktop and silently
    /// do nothing on mobile until the screen closed.</summary>
    internal void NotifySubmitted() => Submitted?.Invoke();

    /// <inheritdoc cref="NotifySubmitted"/>
    internal void NotifyFocusLost() => FocusLost?.Invoke();

    // ─── Sizing ───────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        var theme = ThemeManager.Current;
        return new Vector2(200, theme.InputHeight);
    }

    // ─── Draw ─────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float cr   = theme.InputCornerRadius;
        var inner  = bounds.Deflate(new Thickness(4, 0, 4, 0));

        _innerLeft  = inner.X;
        _innerWidth = inner.Width;

        // NanoGUI-style sunken input: BoxGradient with bright center → dark edges
        var fillR  = new Rect(1, 2, bounds.Width - 2, bounds.Height - 2);
        UIColor bgInner, bgOuter;
        if (IsFocused)
        {
            bgInner = new UIColor(0.588f, 0.588f, 0.588f, 0.125f);   // Color(150, 32)
            bgOuter = new UIColor(0f, 0f, 0f, 0.15f);
        }
        else
        {
            bgInner = new UIColor(1f, 1f, 1f, 0.06f);
            bgOuter = new UIColor(0f, 0f, 0f, 0.15f);
        }
        var bgPaint = renderer.BoxGradient(fillR, 3f, 4f, bgInner, bgOuter);
        renderer.FillRoundedRectWithPaint(fillR, cr, bgPaint);

        // Dark border stroke (NanoGUI: Color(0, 48))
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, bounds.Width - 1f, bounds.Height - 1f),
            MathF.Max(cr - 0.5f, 0f), 1f,
            IsFocused ? theme.AccentColor : UIColor.Black.WithAlpha(0.188f));

        // Font setup
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Left);

        string display = IsPassword ? new string('•', _sb.Length) : _sb.ToString();

        // Rebuild character X-position cache (used for hit-testing and scrolling)
        RebuildCharCache(renderer, display);

        // Mirror Left/Right alignment for RTL so leading edge follows flow direction.
        bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        TextAlign effAlign = rtl
            ? (Align == TextAlign.Left ? TextAlign.Right
               : Align == TextAlign.Right ? TextAlign.Left
               : Align)
            : Align;

        // Compute alignment offset when text is shorter than the box
        float totalTextW = _charXCache.Length > 0 ? _charXCache[^1] : 0f;
        _alignOffset = effAlign switch
        {
            TextAlign.Center => MathF.Max(0f, (inner.Width - totalTextW) * 0.5f),
            TextAlign.Right  => MathF.Max(0f, inner.Width - totalTextW),
            _                => 0f,
        };

        // Scroll to keep caret visible
        UpdateScroll();

        float cy = bounds.Height * 0.5f;

        renderer.Save();
        renderer.IntersectClip(inner);
        renderer.Translate(-_scrollX + _alignOffset, 0);

        // Selection highlight
        if (IsFocused && _selStart >= 0 && _selStart != _cursor)
        {
            int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
            float sx = _charXCache.Length > s ? _charXCache[s] : 0f;
            float ex = _charXCache.Length > e ? _charXCache[e] : sx;
            renderer.FillRect(
                new Rect(inner.X + sx, bounds.Y + 3, ex - sx, bounds.Height - 6),
                SelectionColor ?? theme.SelectionColor);
        }

        // Text or placeholder
        if (_sb.Length == 0 && !string.IsNullOrEmpty(Placeholder))
        {
            // _alignOffset is measured on the EMPTY text (it places the caret), so a right-aligned (RTL) or centred box put
            // the placeholder's start at the far edge / middle — outside the clip in RTL, where it never showed (Hebrew and
            // Arabic, 2026-09-28). Align the placeholder by its own width instead; the caret and hit-testing are unchanged.
            float phW = renderer.MeasureText(Placeholder);
            float phOffset = effAlign switch
            {
                TextAlign.Center => MathF.Max(0f, (inner.Width - phW) * 0.5f),
                TextAlign.Right  => MathF.Max(0f, inner.Width - phW),
                _                => 0f,
            };
            renderer.DrawText(inner.X + phOffset - _alignOffset, cy, Placeholder, PlaceholderColor ?? theme.PlaceholderColor);
        }
        else
            renderer.DrawText(inner.X, cy, display, ForeColor ?? theme.TextColor);

        // Blinking caret (only when focused, blinks at ~1 Hz)
        if (IsFocused && CaretVisible())
        {
            float cx2 = inner.X + (_charXCache.Length > _cursor ? _charXCache[_cursor] : 0f);
            renderer.DrawLine(cx2, bounds.Y + 4, cx2, bounds.Bottom - 4, 1.5f,
                CursorColor ?? theme.AccentColor);
        }

        renderer.Restore();
    }

    // ─── Focus ────────────────────────────────────────────────────────────────
    public override void OnFocusGained()
    {
        ResetBlink();
        if (SkipKeyboardManagement) return;
        sapp_show_keyboard(true);
        EnsureVisible(0f);
        // Show the mobile overlay immediately on iOS/Android.
        // We can't wait for sapp_keyboard_shown() because on iOS it's async
        // (set only when the keyboard finishes animating, ~300 ms later).
#if __ANDROID__ || __IOS__
        {
            var screen = Screen.Instance;
            if (screen != null)
            {
                screen.MobileOverlay.Show(this, screen.KeyboardHeight);   // 0 = height not measured yet
            }
        }
#endif
    }

    public override void OnFocusLost()
    {
        _selStart      = -1;
        _mouseDragging = false;
        _undoDirty     = false;
        // After the internal state is consistent (a handler may read Text or rebuild the screen) and
        // BEFORE the SkipKeyboardManagement early-out, so a box that manages its own keyboard still
        // reports the blur.
        FocusLost?.Invoke();
        if (SkipKeyboardManagement) return;
        // Don't hide keyboard when focus is moving to the mobile overlay proxy.
        if (Screen.Instance?.MobileOverlay.SuppressingKeyboard == true) return;
        sapp_show_keyboard(false);
    }

    // ─── Mouse ────────────────────────────────────────────────────────────────
    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;

        var  local  = e.LocalPosition;
        float textX = local.X - _innerLeft + _scrollX - _alignOffset;
        int   idx   = XToCharIndex(textX);

        if (e.Clicks == 2)
        {
            // Double-click → select word under cursor
            SelectWordAt(idx);
        }
        else if (e.Clicks >= 3)
        {
            // Triple-click → select all
            _selStart = 0;
            _cursor   = _sb.Length;
        }
        else
        {
            bool shift = (e.Modifiers & KeyModifiers.Shift) != 0;
            if (shift && _selStart < 0) _selStart = _cursor;
            else if (!shift)            _selStart  = -1;
            _cursor        = idx;
            _mouseDragging = true;
        }

        ResetBlink();
        return true;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (!_mouseDragging) return false;

        var   local  = e.LocalPosition;
        float textX  = local.X - _innerLeft + _scrollX - _alignOffset;
        int   idx    = XToCharIndex(textX);
        if (_selStart < 0) _selStart = _cursor;
        _cursor = idx;
        ResetBlink();
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        _mouseDragging = false;
        return false;
    }

    // ─── Keyboard – character input ───────────────────────────────────────────
    public override bool OnTextInput(KeyEvent e)
    {
        if (!Enabled) return false;
        // Discard char events generated while Ctrl or Cmd is held — those are
        // shortcut keys handled by OnKeyDown (e.g. Ctrl+C/V, Cmd+C/V).
        if ((e.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0) return true;
#if __ANDROID__
        // Android virtual keyboard sends Enter as char code 10 ('\n') via TextWatcher,
        // not as a KEY_DOWN event — treat it the same as pressing Return.
        if (e.CharCode == '\n') { Submitted?.Invoke(); return true; }
#endif
        if (e.CharCode < 32 || e.CharCode == 127) return false;
        if (MaxLength > 0 && SelectionLength() == 0 && _sb.Length >= MaxLength) return false;

        char ch = (char)e.CharCode;
        if (Uppercase) ch = char.ToUpperInvariant(ch);
        if (!IsCharAllowedReplacingSelection(ch)) return false;
        SaveUndo();
        DeleteSelection();
        _sb.Insert(_cursor, ch);
        _cursor++;
        TextChanged?.Invoke(Text);
        ResetBlink();
        return true;
    }

    // ─── Keyboard – control keys ──────────────────────────────────────────────
    public override bool OnKeyDown(KeyEvent e)
    {
        if (!Enabled) return false;

        const int KEY_BACKSPACE = 259;
        const int KEY_DELETE    = 261;
        const int KEY_LEFT      = 263;
        const int KEY_RIGHT     = 262;
        const int KEY_HOME      = 268;
        const int KEY_END       = 269;
        const int KEY_ENTER     = 257;
        const int KEY_KP_ENTER  = 335;
        const int KEY_TAB       = 258;

        bool shift = (e.Modifiers & KeyModifiers.Shift)   != 0;
        bool ctrl  = (e.Modifiers & KeyModifiers.Control) != 0;
        // macOS uses Cmd (Super) for clipboard shortcuts; treat it like Ctrl here
        bool cmd   = (e.Modifiers & KeyModifiers.Super)   != 0;
        bool accel = ctrl || cmd;   // "accelerator key": Ctrl on Win/Linux, Cmd on macOS

        // ── Movement ──────────────────────────────────────────────────────────
        switch (e.KeyCode)
        {
            case KEY_LEFT:
                if (shift && _selStart < 0) _selStart = _cursor;
                else if (!shift) _selStart = -1;
                _cursor = accel ? WordStartBefore(_cursor) : Math.Max(0, _cursor - 1);
                ResetBlink(); return true;

            case KEY_RIGHT:
                if (shift && _selStart < 0) _selStart = _cursor;
                else if (!shift) _selStart = -1;
                _cursor = accel ? WordEndAfter(_cursor) : Math.Min(_sb.Length, _cursor + 1);
                ResetBlink(); return true;

            case KEY_HOME:
                if (shift && _selStart < 0) _selStart = _cursor; else if (!shift) _selStart = -1;
                _cursor = 0; ResetBlink(); return true;

            case KEY_END:
                if (shift && _selStart < 0) _selStart = _cursor; else if (!shift) _selStart = -1;
                _cursor = _sb.Length; ResetBlink(); return true;

            // ── Editing ───────────────────────────────────────────────────────
            case KEY_BACKSPACE:
                if (_selStart >= 0) { SaveUndo(); DeleteSelection(); }
                else if (accel && _cursor > 0)
                {
                    SaveUndo();
                    int start = WordStartBefore(_cursor);
                    _sb.Remove(start, _cursor - start);
                    _cursor = start;
                    TextChanged?.Invoke(Text);
                }
                else if (_cursor > 0)
                {
                    SaveUndo();
                    _sb.Remove(_cursor - 1, 1);
                    _cursor--;
                    TextChanged?.Invoke(Text);
                }
                ResetBlink(); return true;

            case KEY_DELETE:
                if (_selStart >= 0) { SaveUndo(); DeleteSelection(); }
                else if (accel && _cursor < _sb.Length)
                {
                    SaveUndo();
                    int end = WordEndAfter(_cursor);
                    _sb.Remove(_cursor, end - _cursor);
                    TextChanged?.Invoke(Text);
                }
                else if (_cursor < _sb.Length)
                {
                    SaveUndo();
                    _sb.Remove(_cursor, 1);
                    TextChanged?.Invoke(Text);
                }
                ResetBlink(); return true;

#if !__ANDROID__ && !__IOS__
            case KEY_TAB:
                if (shift) Screen.Instance?.Focus.MoveFocusPrev();
                else       Screen.Instance?.Focus.MoveFocusNext();
                return true;
#endif

            case KEY_ENTER:
            case KEY_KP_ENTER:
                Submitted?.Invoke();
#if !__ANDROID__ && !__IOS__
                Screen.Instance?.Focus.MoveFocusNext();
#endif
                return true;
        }

        // ── Accelerator shortcuts (Ctrl or Cmd) ───────────────────────────────
        if (accel)
        {
            const int KEY_A = 65, KEY_C = 67, KEY_V = 86, KEY_X = 88, KEY_Z = 90;
            switch (e.KeyCode)
            {
                case KEY_A:
                    _selStart = 0; _cursor = _sb.Length; ResetBlink(); return true;

                case KEY_C:
                    CopyToClipboard(); return true;

                case KEY_X:
                    SaveUndo(); CopyToClipboard(); DeleteSelection();
                    ResetBlink(); return true;

                case KEY_V:
                    PasteFromClipboard(); ResetBlink(); return true;

                case KEY_Z:
                    PerformUndo(); ResetBlink(); return true;
            }
        }

        return false;
    }

    // ─── Helpers – editing ────────────────────────────────────────────────────
    // A typed char replaces the selection, so IsCharAllowed must judge the text (and cursor) it
    // lands in — e.g. NumberInput's leading '-' after select-all. The text is restored either way.
    private bool IsCharAllowedReplacingSelection(char ch)
    {
        if (_selStart < 0 || _selStart == _cursor) return IsCharAllowed(ch);
        string text = _sb.ToString();
        int cursor = _cursor;
        int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
        _sb.Remove(s, e - s);
        _cursor = s;
        bool ok = IsCharAllowed(ch);
        _sb.Clear().Append(text);
        _cursor = cursor;
        return ok;
    }

    private void DeleteSelection()
    {
        if (_selStart < 0) return;
        int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
        _sb.Remove(s, e - s);
        _cursor   = s;
        _selStart = -1;
        TextChanged?.Invoke(Text);
    }

    private int SelectionLength() =>
        _selStart < 0 ? 0 : Math.Abs(_cursor - _selStart);

    private void SaveUndo()
    {
        if (_undoDirty) return;
        _undoText   = _sb.ToString();
        _undoCursor = _cursor;
        _undoDirty  = true;
    }

    private void PerformUndo()
    {
        if (_undoText == null) return;
        _sb.Clear(); _sb.Append(_undoText);
        _cursor    = Math.Min(_undoCursor, _sb.Length);
        _selStart  = -1;
        _undoText  = null;
        _undoDirty = false;
        TextChanged?.Invoke(Text);
    }

    // ─── Helpers – clipboard ──────────────────────────────────────────────────
    private void CopyToClipboard()
    {
        if (_selStart < 0 || _selStart == _cursor) return;
        string display = IsPassword ? string.Empty : _sb.ToString();
        int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
        try { sapp_set_clipboard_string(display[s..e]); } catch { /* not enabled */ }
    }

    private void PasteFromClipboard()
    {
        string paste;
        try { paste = sapp_get_clipboard_string(); } catch { return; }
        if (string.IsNullOrEmpty(paste)) return;

        SaveUndo();
        DeleteSelection();
        foreach (char raw in paste)
        {
            char ch = Uppercase ? char.ToUpperInvariant(raw) : raw;
            if (ch < 32 || ch == 127) continue;
            if (!IsCharAllowed(ch)) continue;
            if (MaxLength > 0 && _sb.Length >= MaxLength) break;
            _sb.Insert(_cursor, ch);
            _cursor++;
        }
        TextChanged?.Invoke(Text);
    }

    // ─── Helpers – word navigation ────────────────────────────────────────────
    private int WordStartBefore(int pos)
    {
        // Skip non-word chars, then skip word chars (same logic as ImGui)
        if (pos == 0) return 0;
        int p = pos - 1;
        while (p > 0 && !IsWordChar(_sb[p - 1])) p--;
        while (p > 0 &&  IsWordChar(_sb[p - 1])) p--;
        return p;
    }

    private int WordEndAfter(int pos)
    {
        if (pos >= _sb.Length) return _sb.Length;
        int p = pos;
        while (p < _sb.Length && !IsWordChar(_sb[p])) p++;
        while (p < _sb.Length &&  IsWordChar(_sb[p])) p++;
        return p;
    }

    private void SelectWordAt(int idx)
    {
        if (_sb.Length == 0) return;
        int clamp = Math.Clamp(idx, 0, _sb.Length - 1);
        if (IsWordChar(_sb[clamp]))
        {
            int s = clamp, e = clamp;
            while (s > 0         && IsWordChar(_sb[s - 1])) s--;
            while (e < _sb.Length && IsWordChar(_sb[e]))     e++;
            _selStart = s; _cursor = e;
        }
        else
        {
            // Select the single non-word character
            _selStart = clamp; _cursor = clamp + 1;
        }
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // ─── Helpers – layout & scroll ────────────────────────────────────────────
    private void RebuildCharCache(Renderer renderer, string display)
    {
        int n = display.Length;
        if (_charXCache.Length != n + 1)
            _charXCache = new float[n + 1];
        for (int i = 0; i <= n; i++)
            _charXCache[i] = renderer.MeasureText(display[..i]);
    }

    private void UpdateScroll()
    {
        if (_charXCache.Length == 0 || _innerWidth <= 0) return;
        float cx = _cursor < _charXCache.Length ? _charXCache[_cursor] : 0f;

        if (cx < _scrollX)
            _scrollX = MathF.Max(0f, cx - _innerWidth * 0.25f);
        else if (cx > _scrollX + _innerWidth)
            _scrollX = cx - _innerWidth + _innerWidth * 0.25f;
    }

    private int XToCharIndex(float textX)
    {
        int n = _charXCache.Length - 1;   // number of characters
        if (n <= 0) return 0;
        for (int i = 0; i < n; i++)
        {
            float mid = (_charXCache[i] + _charXCache[i + 1]) * 0.5f;
            if (textX <= mid) return i;
        }
        return n;
    }

    // ─── Helpers – blink ──────────────────────────────────────────────────────
    private void ResetBlink() => _blinkEpoch = Environment.TickCount64;

    private bool CaretVisible()
    {
        long elapsed = Environment.TickCount64 - _blinkEpoch;
        return (elapsed % (BlinkOnMs + BlinkOffMs)) < BlinkOnMs;
    }

    // ─── Helpers – font ───────────────────────────────────────────────────────
    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
