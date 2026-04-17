using System;

namespace Sokol.GUI;

/// <summary>
/// A small colored swatch button that opens a <see cref="ColorPicker"/> popup when clicked.
/// </summary>
public class ColorButton : Widget
{
    private UIColor      _color;
    private bool         _open;
    private ColorPicker? _pickerCache;

    public UIColor Color
    {
        get => _color;
        set { _color = value; if (_pickerCache != null) _pickerCache.Color = value; }
    }

    public event Action<UIColor>? ColorChanged;

    public bool ShowAlpha { get; set; } = true;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;

    public ColorButton() : this(UIColor.Red) { }
    public ColorButton(UIColor initial) { _color = initial; }

    // ─── PreferredSize ────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        float h = ThemeManager.Current.InputHeight;
        return new Vector2(h * 2.5f, h);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme = ThemeManager.Current;
        float w = Bounds.Width, h = Bounds.Height;
        float cr = theme.ButtonCornerRadius;

        // Swatch fill
        renderer.FillRoundedRect(new Rect(0, 0, w, h), cr, _color);

        // Border
        var borderC = IsFocused ? theme.AccentColor
                    : IsHovered ? theme.BorderColor.Lighten(0.3f)
                    :             theme.BorderColor;
        renderer.StrokeRoundedRect(new Rect(0, 0, w, h), cr, 1.5f, borderC);

        // Drop-down hint
        bool dark = _color.R < 0.4f && _color.G < 0.4f && _color.B < 0.4f;
        UIColor labelC = dark ? UIColor.White.WithAlpha(0.7f) : UIColor.Black.WithAlpha(0.5f);
        ApplyFont(renderer, theme);
        renderer.SetTextAlign(TextHAlign.Center);
        renderer.DrawText(w * 0.5f, h * 0.5f, "▼", labelC);
    }

    // ─── Popup overlay ────────────────────────────────────────────────────────
    public override void DrawPopupOverlay(Renderer renderer)
    {
        if (_pickerCache == null) return;
        var   theme = ThemeManager.Current;
        var   pref  = _pickerCache.PreferredSize(renderer);
        float gap   = 4f;

        // Flip upward if the popup would extend below the screen.
        var   sp       = ScreenPosition;
        float screenH  = Screen.Instance.LogicalHeight;
        float belowY   = Bounds.Height + gap;
        float belowBot = sp.Y + belowY + pref.Y;
        float popY     = belowBot <= screenH ? belowY : -(pref.Y + gap);

        var   popR  = new Rect(0, popY, pref.X, pref.Y);

        renderer.DrawDropShadow(popR, 4f, new Vector2(2f, 2f), 8f,
            new UIColor(0f, 0f, 0f, 0.3f));
        renderer.FillRoundedRect(popR, 5f, theme.SurfaceVariant);
        renderer.StrokeRoundedRect(popR, 5f, 1f, theme.BorderColor);

        _pickerCache.Bounds = popR;
        renderer.Save();
        renderer.Translate(popR.X, popR.Y);
        _pickerCache.Draw(renderer);
        renderer.Restore();
    }

    public override bool HitTest(Vector2 localPoint)
    {
        if (_open && _pickerCache != null && _pickerCache.Bounds.Width > 0)
        {
            if (_pickerCache.Bounds.Contains(localPoint)) return true;
        }
        return base.HitTest(localPoint);
    }

    public override void OnPopupDismiss() { _open = false; }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;

        if (_open && _pickerCache != null)
        {
            var local = e.LocalPosition;
            var popR  = _pickerCache.Bounds;
            if (popR.Contains(local))
            {
                var pl = new Vector2(local.X - popR.X, local.Y - popR.Y);
                _pickerCache.OnMouseDown(new MouseEvent { Position = pl, LocalPosition = pl, Button = e.Button, Clicks = e.Clicks });
                return true;
            }
            _open = false;
            Screen.SetActivePopup(null);
            return true;
        }

        _pickerCache        ??= new ColorPicker(_color) { ShowAlpha = ShowAlpha };
        _pickerCache.Color   = _color;
        _pickerCache.ColorChanged += OnPickerColorChanged;
        _open = true;
        Screen.SetActivePopup(this);
        return true;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (!_open || _pickerCache == null) return false;
        var local = e.LocalPosition;
        var popR  = _pickerCache.Bounds;
        var pl    = new Vector2(local.X - popR.X, local.Y - popR.Y);
        return _pickerCache.OnMouseMove(new MouseEvent { Position = pl, LocalPosition = pl, Button = e.Button });
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (!_open || _pickerCache == null) return false;
        var local = e.LocalPosition;
        var popR  = _pickerCache.Bounds;
        var pl    = new Vector2(local.X - popR.X, local.Y - popR.Y);
        return _pickerCache.OnMouseUp(new MouseEvent { Position = pl, LocalPosition = pl, Button = e.Button });
    }

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; return false; }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private void OnPickerColorChanged(UIColor c)
    {
        _color = c;
        ColorChanged?.Invoke(c);
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0f ? FontSize : theme.SmallFontSize);
    }
}
