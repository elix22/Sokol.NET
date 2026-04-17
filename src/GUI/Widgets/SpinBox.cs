using System;

namespace Sokol.GUI;

/// <summary>
/// Numeric spinner with decrement / increment buttons.
/// Layout: [−] [ value text ] [+]
/// Supports scroll-wheel and keyboard Up/Down.
/// </summary>
public class SpinBox : Widget
{
    private float  _value;
    private bool   _leftHovered, _rightHovered;
    private bool   _leftPressed,  _rightPressed;
    // Repeat-fire state
    private bool   _holdActive;
    private float  _holdDir;          // −1 or +1
    private float  _holdTimer;
    private float  _repeatTimer;
    private const float HoldDelay  = 0.45f;
    private const float RepeatRate = 0.08f;

    public float Min           { get; set; } = 0f;
    public float Max           { get; set; } = 100f;
    public float Step          { get; set; } = 1f;
    public int   DecimalPlaces { get; set; } = 0;

    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, Min, Max);
            if (MathF.Abs(clamped - _value) < 1e-6f) return;
            _value = clamped;
            ValueChanged?.Invoke(_value);
        }
    }

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    public event Action<float>? ValueChanged;

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        var t = ThemeManager.Current;
        return new Vector2(160, t.InputHeight);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float w    = Bounds.Width, h = Bounds.Height;
        float btnW = h;  // square buttons
        float cr   = theme.InputCornerRadius;

        var fullR   = new Rect(0, 0, w, h);
        bool rtl    = ResolvedFlowDirection == FlowDirection.RightToLeft;
        // RTL: [+] on left, [−] on right
        var leftR   = new Rect(0, 0, btnW, h);
        var textR   = new Rect(btnW, 0, w - btnW * 2f, h);
        var rightR  = new Rect(w - btnW, 0, btnW, h);
        string minusBtnLabel = "−", plusBtnLabel = "+";
        Rect minusBtnR = rtl ? rightR : leftR;
        Rect plusBtnR  = rtl ? leftR  : rightR;

        // Background box — NanoGUI-style sunken input
        var bgPaint = renderer.BoxGradient(
            new Rect(1, 1, w - 2, h - 2), 3f, 4f,
            new UIColor(1f, 1f, 1f, 0.125f),
            new UIColor(0.125f, 0.125f, 0.125f, 0.125f));
        renderer.FillRoundedRectWithPaint(fullR, cr, bgPaint);
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, w - 1f, h - 1f),
            MathF.Max(cr - 0.5f, 0f), 1f,
            IsFocused ? theme.AccentColor : UIColor.Black.WithAlpha(0.188f));

        // Left button [−] or [+] depending on RTL
        DrawArrowButton(renderer, theme, minusBtnR, minusBtnLabel, _leftHovered, _leftPressed, !rtl, cr);

        // Right button [+] or [−] depending on RTL
        DrawArrowButton(renderer, theme, plusBtnR, plusBtnLabel, _rightHovered, _rightPressed, rtl, cr);

        // Divider lines
        renderer.DrawLine(btnW, 3f, btnW, h - 3f, 1f, theme.BorderColor);
        renderer.DrawLine(w - btnW, 3f, w - btnW, h - 3f, 1f, theme.BorderColor);

        // Value text
        string text = DecimalPlaces == 0
            ? ((int)MathF.Round(_value)).ToString()
            : _value.ToString($"F{DecimalPlaces}");

        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Center);
        renderer.DrawText(textR.X + textR.Width * 0.5f, h * 0.5f, text, theme.TextColor);

        // Tick repeat logic (called on draw as a proxy for update)
        TickRepeat();
    }

    private void DrawArrowButton(Renderer renderer, Theme theme, Rect r, string label,
                                  bool hovered, bool pressed, bool isLeft, float cr)
    {
        // NanoGUI-style gradient button fill
        UIColor gradTop, gradBot;
        if (pressed)      { gradTop = theme.ButtonPressedTop; gradBot = theme.ButtonPressedBottom; }
        else if (hovered) { gradTop = theme.ButtonHoverTop;   gradBot = theme.ButtonHoverBottom; }
        else              { gradTop = theme.ButtonGradientTop; gradBot = theme.ButtonGradientBottom; }

        var corners = isLeft
            ? new CornerRadius(cr, 0f, 0f, cr)
            : new CornerRadius(0f, cr, cr, 0f);

        var fillR = new Rect(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        var grad = renderer.LinearGradient(
            new Vector2(r.X, r.Y), new Vector2(r.X, r.Bottom),
            gradTop, gradBot);
        renderer.FillRoundedRectWithPaint(fillR, MathF.Max(0f, cr - 1f), grad);

        // Inner highlight
        if (!pressed)
            renderer.StrokeRoundedRect(
                new Rect(r.X + 0.5f, r.Y + 1.5f, r.Width - 1f, r.Height - 2f),
                corners, 1f, theme.BorderLight);
        // Dark outer
        renderer.StrokeRoundedRect(
            new Rect(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f),
            corners, 1f, theme.BorderDark);

        // Symbol
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Center);
        float ty = r.Height * 0.5f + (pressed ? 1f : 0f);
        if (!pressed)
            renderer.DrawText(r.X + r.Width * 0.5f, ty + 1f, label, theme.TextShadow);
        renderer.DrawText(r.X + r.Width * 0.5f, ty, label,
            hovered ? theme.TextColor : theme.TextMutedColor);
    }

    private void TickRepeat()
    {
        if (!_holdActive) return;
        float dt = 1f / 60f; // approximate; good enough for repeat fire
        _holdTimer += dt;
        if (_holdTimer >= HoldDelay)
        {
            _repeatTimer += dt;
            while (_repeatTimer >= RepeatRate)
            {
                _repeatTimer -= RepeatRate;
                ChangeValue(_holdDir);
            }
        }
    }

    private void ChangeValue(float dir)
    {
        Value += dir * Step;
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { return true; }
    public override bool OnMouseLeave(MouseEvent e)
    {
        _leftHovered = _rightHovered = false;
        // Only stop auto-repeat when no button is held (mouse can leave during a
        // fast press; InputRouter still routes MouseUp through _captured).
        if (!_leftPressed && !_rightPressed)
            StopHold();
        return true;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        float btnW  = Bounds.Height;
        float localX = e.LocalPosition.X;
        _leftHovered  = localX < btnW;
        _rightHovered = localX >= Bounds.Width - btnW;
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        float btnW   = Bounds.Height;
        float localX = e.LocalPosition.X;
        if (localX < btnW)
        {
            _leftPressed  = true;
            ChangeValue(-1f);
            StartHold(-1f);
        }
        else if (localX >= Bounds.Width - btnW)
        {
            _rightPressed = true;
            ChangeValue(+1f);
            StartHold(+1f);
        }
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        _leftPressed = _rightPressed = false;
        StopHold();
        return true;
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        ChangeValue(e.Scroll.Y > 0 ? 1f : -1f);
        return true;
    }

    public override bool OnKeyDown(KeyEvent e)
    {
        if (e.KeyCode == 265 ||
            e.KeyCode == 262)
        { ChangeValue(+1f); return true; }
        if (e.KeyCode == 264 ||
            e.KeyCode == 263)
        { ChangeValue(-1f); return true; }
        return false;
    }

    private void StartHold(float dir) { _holdActive = true; _holdDir = dir; _holdTimer = 0f; _repeatTimer = 0f; }
    private void StopHold()           { _holdActive = false; }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
