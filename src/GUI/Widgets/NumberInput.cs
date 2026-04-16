using System;
using System.Globalization;

namespace Sokol.GUI;

/// <summary>
/// Single-line numeric input derived from <see cref="TextBox"/>.
/// Adds <see cref="Value"/>, <see cref="Min"/>/<see cref="Max"/> clamping,
/// Up/Down step, and a red border when the value is invalid or out of range.
/// All text editing (caret, drag-select, Ctrl+A/C/V/X/Z, undo) is inherited.
/// </summary>
public class NumberInput : TextBox
{
    private bool _isInvalid;

    // ─── Properties ────────────────────────────────────────────────────────────
    public float Value
    {
        get => float.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        set { Text = FormatValue(value); Validate(); }
    }

    public float Min           { get; set; } = float.NegativeInfinity;
    public float Max           { get; set; } = float.PositiveInfinity;
    public int   DecimalPlaces { get; set; } = 2;

    public event Action<float>? ValueChanged;
    public event Action<float>? ValueCommitted; // fired on Enter / focus loss

    public NumberInput() { Text = "0"; }

    // ─── Preferred size ──────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(120, ThemeManager.Current.InputHeight);
    }

    // ─── TextBox hooks ────────────────────────────────────────────────────────
    protected override bool IsCharAllowed(char c)
    {
        if (char.IsDigit(c)) return true;
        if (c == '.' || c == ',')
            return DecimalPlaces > 0 && !Text.Contains('.') && !Text.Contains(',');
        if (c == '-')
            return Cursor == 0 && !Text.Contains('-');
        return false;
    }

    protected override UIColor GetBorderColor(Theme theme) =>
        _isInvalid ? new UIColor(0.9f, 0.2f, 0.2f, 1f)
                   : base.GetBorderColor(theme);

    protected override float GetBorderWidth(Theme theme) =>
        _isInvalid ? 2f : base.GetBorderWidth(theme);

    // ─── Focus ────────────────────────────────────────────────────────────────
    public override void OnFocusLost()
    {
        base.OnFocusLost();
        Commit();
    }

    // ─── Input ────────────────────────────────────────────────────────────────
    public override bool OnTextInput(KeyEvent e)
    {
        bool handled = base.OnTextInput(e);
        if (handled) { Validate(); TryFireValueChanged(); }
        return handled;
    }

    public override bool OnKeyDown(KeyEvent e)
    {
        const int KEY_ENTER    = 257;
        const int KEY_KP_ENTER = 335;
        const int KEY_UP       = 265;
        const int KEY_DOWN     = 264;
        const int KEY_ESCAPE   = 256;

        switch (e.KeyCode)
        {
            case KEY_ENTER:
            case KEY_KP_ENTER:
                Commit(); return true;
            case KEY_UP:
                Step(+1); return true;
            case KEY_DOWN:
                Step(-1); return true;
            case KEY_ESCAPE:
                Text = FormatValue(Value); Validate(); return true;
        }

        bool handled = base.OnKeyDown(e);
        if (handled) Validate();
        return handled;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────
    private void Validate()
    {
        string t = Text;
        if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        { _isInvalid = !string.IsNullOrEmpty(t); return; }
        _isInvalid = v < Min || v > Max;
    }

    private void TryFireValueChanged()
    {
        if (float.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            && v >= Min && v <= Max)
            ValueChanged?.Invoke(v);
    }

    private void Commit()
    {
        string t = Text;
        if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            v    = Math.Clamp(v, Min, Max);
            Text = FormatValue(v);
            Validate();
            ValueCommitted?.Invoke(v);
        }
        else
        {
            Text       = FormatValue(Math.Clamp(Value, Min, Max));
            _isInvalid = false;
        }
    }

    private void Step(float direction)
    {
        float step = MathF.Pow(10f, -DecimalPlaces);
        float next = Math.Clamp(Value + direction * step, Min, Max);
        Text = FormatValue(next);
        Validate();
        ValueChanged?.Invoke(next);
    }

    private string FormatValue(float v)
    {
        string fmt = DecimalPlaces > 0 ? $"F{DecimalPlaces}" : "F0";
        return v.ToString(fmt, CultureInfo.InvariantCulture);
    }
}
