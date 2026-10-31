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
    // The exact value last set or committed, and the text it was shown as: while the text is
    // unchanged, Value returns it instead of the rounded display (an untouched 2.1256592 shown
    // as "2.13" must not read back as 2.13).
    private float   _exact;
    private string? _exactText = "0";
    // Text as of the last commit or programmatic set: focus loss commits only when it differs.
    private string _committedText = "0";

    // ─── Properties ────────────────────────────────────────────────────────────
    public float Value
    {
        get => Text == _exactText ? _exact
             : float.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        set { SetExact(value); _committedText = Text; Validate(); }
    }

    public float Min           { get; set; } = float.NegativeInfinity;
    public float Max           { get; set; } = float.PositiveInfinity;
    public int   DecimalPlaces { get; set; } = 2;
    public float Step          { get; set; } = 0.1f;

    /// <summary>When true, forces <see cref="DecimalPlaces"/>=0 and <see cref="Step"/>=1 so only integers are accepted.</summary>
    public bool IsInteger
    {
        get => DecimalPlaces == 0;
        set { if (value) { DecimalPlaces = 0; Step = 1f; } }
    }

    public event Action<float>? ValueChanged;
    public event Action<float>? ValueCommitted; // fired on Enter / focus loss

    public NumberInput()
    {
        Text = "0";
        TextChanged += _ => _exactText = null;   // any edit drops the exact value; SetExact re-arms it
    }

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
        // Enter has already committed (and moved focus here); only an edit since then commits again.
        if (Text != _committedText) Commit();
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
                Commit();
                base.OnKeyDown(e);  // fires Submitted (→ overlay Hide) and desktop MoveFocusNext
                return true;
            case KEY_UP:
                StepBy(+1); return true;
            case KEY_DOWN:
                StepBy(-1); return true;
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

    /// <summary>
    /// Called by MobileKeyboardOverlay to fire ValueChanged after syncing
    /// text from a proxy, bypassing the normal OnTextInput path.
    /// </summary>
    internal void NotifyValueChanged() => TryFireValueChanged();

    /// <summary>
    /// Set value during interactive scrub/drag and fire live ValueChanged.
    /// </summary>
    public void SetValueFromDrag(float value)
    {
        value = Math.Clamp(value, Min, Max);
        SetExact(value);
        Validate();
        ValueChanged?.Invoke(value);
    }

    /// <summary>
    /// Commit current value and fire ValueCommitted.
    /// </summary>
    public void CommitDraggedValue() => Commit();

    private void Commit()
    {
        string t = Text;
        if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            v    = Math.Clamp(v, Min, Max);
            SetExact(v);
            _committedText = Text;
            Validate();
            ValueCommitted?.Invoke(v);
        }
        else
        {
            Text       = FormatValue(Math.Clamp(Value, Min, Max));
            _isInvalid = false;
        }
    }

    private void StepBy(float direction)
    {
        float next = Math.Clamp(Value + direction * Step, Min, Max);
        SetExact(next);
        Validate();
        ValueChanged?.Invoke(next);
    }

    private void SetExact(float v)
    {
        _exact     = v;
        Text       = FormatValue(v);
        _exactText = Text;
    }

    private string FormatValue(float v)
    {
        string fmt = DecimalPlaces > 0 ? $"F{DecimalPlaces}" : "F0";
        return v.ToString(fmt, CultureInfo.InvariantCulture);
    }
}
