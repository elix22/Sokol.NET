using System;
using System.Text;

namespace Sokol.GUI;

/// <summary>
/// Single-line editable text input.
/// </summary>
public class TextBox : Widget
{
    private readonly StringBuilder _sb = new();
    private int    _cursor;
    private int    _selStart = -1;
    private float  _scrollX;

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
            TextChanged?.Invoke(Text);
        }
    }

    public string? Placeholder   { get; set; }
    public bool    IsPassword    { get; set; }
    public int     MaxLength     { get; set; } = 0;  // 0 = unlimited
    public UIColor? BackColor    { get; set; }
    public UIColor? ForeColor    { get; set; }
    public UIColor? PlaceholderColor { get; set; }
    public UIColor? SelectionColor   { get; set; }
    public UIColor? CursorColor      { get; set; }
    public Font?    Font             { get; set; }
    public float    FontSize         { get; set; } = 0f;

    public event Action<string>? TextChanged;
    public event Action?         Submitted;    // Enter pressed

    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        var theme = ThemeManager.Current;
        return new Vector2(200, theme.InputHeight);
    }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme   = ThemeManager.Current;
        var bounds  = new Rect(0, 0, Bounds.Width, Bounds.Height);
        float cr    = theme.InputCornerRadius;
        var inner   = bounds.Deflate(new Thickness(4, 0, 4, 0));

        // Background + border
        renderer.FillRoundedRect(bounds, cr, BackColor ?? theme.InputBackColor);
        renderer.StrokeRoundedRect(bounds, cr, 1.5f,
            IsFocused ? theme.AccentColor : theme.BorderColor);

        // Font setup
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Left);
        float cy = bounds.Height * 0.5f;

        string display = IsPassword ? new string('•', _sb.Length) : _sb.ToString();

        // Clip to inner area
        renderer.Save();
        renderer.IntersectClip(inner);
        renderer.Translate(-_scrollX, 0);

        // Selection highlight
        if (IsFocused && _selStart >= 0 && _selStart != _cursor)
        {
            int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
            float sx = renderer.MeasureText(display[..s]);
            float ex = renderer.MeasureText(display[..e]);
            renderer.FillRect(new Rect(inner.X + sx, bounds.Y + 3, ex - sx, bounds.Height - 6),
                SelectionColor ?? theme.SelectionColor);
        }

        // Text or placeholder
        if (_sb.Length == 0 && !IsFocused && !string.IsNullOrEmpty(Placeholder))
            renderer.DrawText(inner.X, cy, Placeholder, PlaceholderColor ?? theme.PlaceholderColor);
        else
            renderer.DrawText(inner.X, cy, display, ForeColor ?? theme.TextColor);

        // Cursor
        if (IsFocused)
        {
            float cx2 = inner.X + renderer.MeasureText(display[.._cursor]);
            renderer.DrawLine(cx2, bounds.Y + 4, cx2, bounds.Bottom - 4, 1.5f,
                CursorColor ?? theme.AccentColor);
        }

        renderer.Restore();
    }

    // ─── Focus ───────────────────────────────────────────────────────────────
    public override void OnFocusGained() { }
    public override void OnFocusLost()   { _selStart = -1; }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseDown(MouseEvent e)
    {
        _selStart = -1;
        return true;
    }

    public override bool OnTextInput(KeyEvent e)
    {
        if (!Enabled) return false;
        if (e.CharCode < 32 || e.CharCode == 127) return false; // control chars
        if (MaxLength > 0 && _sb.Length >= MaxLength) return false;

        DeleteSelection();
        char ch = (char)e.CharCode;
        _sb.Insert(_cursor, ch);
        _cursor++;
        TextChanged?.Invoke(Text);
        return true;
    }

    public override bool OnKeyDown(KeyEvent e)
    {
        if (!Enabled) return false;

        const int SAPP_KEYCODE_BACKSPACE = 259;
        const int SAPP_KEYCODE_DELETE    = 261;
        const int SAPP_KEYCODE_LEFT      = 263;
        const int SAPP_KEYCODE_RIGHT     = 262;
        const int SAPP_KEYCODE_HOME      = 268;
        const int SAPP_KEYCODE_END       = 269;
        const int SAPP_KEYCODE_ENTER     = 257;
        const int SAPP_KEYCODE_KP_ENTER  = 335;

        bool shift = (e.Modifiers & KeyModifiers.Shift) != 0;

        switch (e.KeyCode)
        {
            case SAPP_KEYCODE_BACKSPACE:
                if (_selStart >= 0) DeleteSelection();
                else if (_cursor > 0) { _sb.Remove(_cursor - 1, 1); _cursor--; TextChanged?.Invoke(Text); }
                return true;
            case SAPP_KEYCODE_DELETE:
                if (_selStart >= 0) DeleteSelection();
                else if (_cursor < _sb.Length) { _sb.Remove(_cursor, 1); TextChanged?.Invoke(Text); }
                return true;
            case SAPP_KEYCODE_LEFT:
                if (!shift) _selStart = -1;
                else if (_selStart < 0) _selStart = _cursor;
                if (_cursor > 0) _cursor--;
                return true;
            case SAPP_KEYCODE_RIGHT:
                if (!shift) _selStart = -1;
                else if (_selStart < 0) _selStart = _cursor;
                if (_cursor < _sb.Length) _cursor++;
                return true;
            case SAPP_KEYCODE_HOME:
                if (!shift) _selStart = -1; else if (_selStart < 0) _selStart = _cursor;
                _cursor = 0;
                return true;
            case SAPP_KEYCODE_END:
                if (!shift) _selStart = -1; else if (_selStart < 0) _selStart = _cursor;
                _cursor = _sb.Length;
                return true;
            case SAPP_KEYCODE_ENTER:
            case SAPP_KEYCODE_KP_ENTER:
                Submitted?.Invoke();
                return true;
        }

        // Ctrl+A
        if ((e.Modifiers & KeyModifiers.Control) != 0 && e.KeyCode == 'A')
        {
            _selStart = 0; _cursor = _sb.Length;
            return true;
        }
        return false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void DeleteSelection()
    {
        if (_selStart < 0) return;
        int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
        _sb.Remove(s, e - s);
        _cursor = s;
        _selStart = -1;
        TextChanged?.Invoke(Text);
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
