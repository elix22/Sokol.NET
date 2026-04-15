using System;
using System.Globalization;

namespace Sokol.GUI;

/// <summary>
/// A single-line numeric text input with min/max validation.
/// The border turns red when the entered value is out of range.
/// </summary>
public class NumberInput : Widget
{
    private string _text        = "0";
    private bool   _focused;
    private int    _cursor;
    private bool   _isInvalid;

    // ─── Properties ───────────────────────────────────────────────────────────
    public float Value
    {
        get => float.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        set
        {
            _text   = FormatValue(value);
            _cursor = _text.Length;
            Validate();
        }
    }

    public float Min          { get; set; } = float.NegativeInfinity;
    public float Max          { get; set; } = float.PositiveInfinity;
    public int   DecimalPlaces { get; set; } = 2;
    public string? Placeholder { get; set; }

    public event Action<float>? ValueChanged;
    public event Action<float>? ValueCommitted; // fired on Enter / focus loss

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    // ─── PreferredSize ────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(120, ThemeManager.Current.InputHeight);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme  = ThemeManager.Current;
        float w      = Bounds.Width;
        float h      = Bounds.Height;
        float cr     = theme.InputCornerRadius;
        var   bounds = new Rect(0, 0, w, h);

        // Background
        renderer.FillRoundedRect(bounds, cr, theme.InputBackColor);

        // Border — red if value is invalid/out-of-range, accent if focused
        var borderC = _isInvalid ? new UIColor(0.9f, 0.2f, 0.2f, 1f)
                    : _focused   ? theme.AccentColor
                    :              theme.BorderColor;
        float bw = _isInvalid || _focused ? 2f : 1.5f;
        renderer.StrokeRoundedRect(bounds, cr, bw, borderC);

        // Text or placeholder
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Left);

        string display = _text;
        bool   isEmpty = string.IsNullOrEmpty(display);
        var    textC   = isEmpty ? theme.TextMutedColor : (_isInvalid ? borderC : theme.TextColor);

        renderer.Save();
        renderer.IntersectClip(new Rect(6, 0, w - 12, h));

        if (isEmpty && !_focused && Placeholder != null)
            renderer.DrawText(6, h * 0.5f, Placeholder, theme.TextMutedColor);
        else
            renderer.DrawText(6, h * 0.5f, display, textC);

        // Cursor
        if (_focused)
        {
            float cx = 6f + (display.Length > 0 && _cursor > 0
                ? renderer.MeasureText(display[.._cursor])
                : 0f);
            renderer.DrawLine(cx, h * 0.15f, cx, h * 0.85f, 1.5f, theme.AccentColor);
        }

        renderer.Restore();
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override void OnFocusGained() { _focused = true; }
    public override void OnFocusLost  ()
    {
        _focused = false;
        Commit();
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        _cursor = _text.Length; // place caret at end (simple)
        return true;
    }

    public override bool OnKeyDown(KeyEvent e)
    {
        if (!_focused) return false;

        const int KEY_BACKSPACE = 259;
        const int KEY_DELETE    = 261;
        const int KEY_LEFT      = 263;
        const int KEY_RIGHT     = 262;
        const int KEY_HOME      = 268;
        const int KEY_END       = 269;
        const int KEY_ENTER     = 257;
        const int KEY_KP_ENTER  = 335;
        const int KEY_UP        = 265;
        const int KEY_DOWN      = 264;
        const int KEY_ESCAPE    = 256;

        switch (e.KeyCode)
        {
            case KEY_BACKSPACE:
                if (_cursor > 0 && _text.Length > 0)
                {
                    _text   = _text[..(_cursor - 1)] + _text[_cursor..];
                    _cursor = Math.Max(0, _cursor - 1);
                    Validate();
                }
                return true;

            case KEY_DELETE:
                if (_cursor < _text.Length)
                {
                    _text = _text[.._cursor] + _text[(_cursor + 1)..];
                    Validate();
                }
                return true;

            case KEY_LEFT:
                _cursor = Math.Max(0, _cursor - 1);
                return true;

            case KEY_RIGHT:
                _cursor = Math.Min(_text.Length, _cursor + 1);
                return true;

            case KEY_HOME:
                _cursor = 0;
                return true;

            case KEY_END:
                _cursor = _text.Length;
                return true;

            case KEY_ENTER:
            case KEY_KP_ENTER:
                Commit();
                return true;

            case KEY_UP:
                Step(+1);
                return true;

            case KEY_DOWN:
                Step(-1);
                return true;

            case KEY_ESCAPE:
                _text   = FormatValue(Value);
                _cursor = _text.Length;
                Validate();
                return true;
        }
        return false;
    }

    public override bool OnTextInput(KeyEvent e)
    {
        if (!_focused) return false;
        if (e.CharCode < 32 || e.CharCode == 127) return false;

        char c = (char)e.CharCode;
        if (!IsValidChar(c)) return false;
        _text   = _text[.._cursor] + c + _text[_cursor..];
        _cursor = Math.Min(_text.Length, _cursor + 1);
        Validate();
        TryFireValueChanged();
        return true;
    }

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; return false; }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private bool IsValidChar(char c)
    {
        if (char.IsDigit(c)) return true;
        if (c == '.' || c == ',')
        {
            // Only allow one decimal separator
            return DecimalPlaces > 0 && !_text.Contains('.') && !_text.Contains(',');
        }
        if (c == '-') return _cursor == 0 && !_text.Contains('-');
        return false;
    }

    private void Validate()
    {
        if (!float.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            _isInvalid = !string.IsNullOrEmpty(_text);
            return;
        }
        _isInvalid = v < Min || v > Max;
    }

    private void TryFireValueChanged()
    {
        if (float.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            && v >= Min && v <= Max)
            ValueChanged?.Invoke(v);
    }

    private void Commit()
    {
        if (float.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            v       = Math.Clamp(v, Min, Max);
            _text   = FormatValue(v);
            _cursor = _text.Length;
            Validate();
            ValueCommitted?.Invoke(v);
        }
        else
        {
            // Reset to clamped last valid value
            _text   = FormatValue(Math.Clamp(Value, Min, Max));
            _cursor = _text.Length;
            _isInvalid = false;
        }
    }

    private void Step(float direction)
    {
        float step = MathF.Pow(10f, -DecimalPlaces);
        float next = Math.Clamp(Value + direction * step, Min, Max);
        _text   = FormatValue(next);
        _cursor = _text.Length;
        Validate();
        ValueChanged?.Invoke(next);
    }

    private string FormatValue(float v)
    {
        string fmt = DecimalPlaces > 0 ? $"F{DecimalPlaces}" : "F0";
        return v.ToString(fmt, CultureInfo.InvariantCulture);
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
