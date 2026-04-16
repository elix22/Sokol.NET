using System;

namespace Sokol.GUI;

/// <summary>
/// Draggable, closable window panel.
/// </summary>
public class Window : Panel
{
    private bool   _dragging;
    private Vector2 _dragOffset;

    public string  Title           { get; set; } = "Window";
    public bool    IsClosable      { get; set; } = true;
    public bool    IsDraggable     { get; set; } = true;
    public bool    IsCollapsed     { get; set; } = false;

    public UIColor? TitleBarColor  { get; set; }
    public UIColor? TitleTextColor { get; set; }
    public Font?    TitleFont      { get; set; }

    private bool _hoverClose;

    public event Action? Closed;
    public event Action? Collapsed;

    private float TitleBarHeight => ThemeManager.Current.WindowTitleBarHeight;

    public Window() { DrawShadow = true; }

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme  = ThemeManager.Current;
        float w    = Bounds.Width, h = Bounds.Height;
        float tbH  = TitleBarHeight;
        float cr   = theme.WindowCornerRadius;

        // Window frame (shadow handled by Panel.Draw)
        base.Draw(renderer);

        // Title bar overlay
        var tbR = new Rect(0, 0, w, tbH);
        renderer.FillRoundedRectTop(tbR, cr, TitleBarColor ?? theme.WindowTitleBarColor);

        // Title text
        renderer.SetFont(TitleFont?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(theme.FontSize);
        renderer.SetTextAlign(TextHAlign.Left);
        renderer.DrawText(cr + 4, tbH * 0.5f, Title, TitleTextColor ?? theme.TextColor);

        // Close button
        if (IsClosable)
        {
            float btnR  = tbH * 0.28f;
            float btnCX = w - tbH * 0.5f;
            float btnCY = tbH * 0.5f;
            renderer.FillCircle(btnCX, btnCY, btnR,
                _hoverClose ? UIColor.FromHex("#FF5555") : theme.WindowCloseButtonColor);
            renderer.DrawLine(btnCX - btnR * 0.5f, btnCY - btnR * 0.5f,
                              btnCX + btnR * 0.5f, btnCY + btnR * 0.5f, 1.5f, theme.TextColor);
            renderer.DrawLine(btnCX + btnR * 0.5f, btnCY - btnR * 0.5f,
                              btnCX - btnR * 0.5f, btnCY + btnR * 0.5f, 1.5f, theme.TextColor);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseDown(MouseEvent e)
    {
        var local = e.LocalPosition;
        float tbH = TitleBarHeight;

        // Close button hit
        if (IsClosable && local.Y < tbH && local.X > Bounds.Width - tbH)
        {
            Visible = false;
            Closed?.Invoke();
            return true;
        }

        // Start drag on title bar
        if (IsDraggable && local.Y < tbH)
        {
            _dragging   = true;
            _dragOffset = new Vector2(local.X, local.Y);
            return true;
        }

        return base.OnMouseDown(e);
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        var local  = e.LocalPosition;
        float tbH  = TitleBarHeight;
        _hoverClose = IsClosable && local.Y < tbH && local.X > Bounds.Width - tbH;

        if (_dragging && Parent != null)
        {
            var parentLocal = Parent.ToLocal(e.Position);
            Bounds = new Rect(
                parentLocal.X - _dragOffset.X,
                parentLocal.Y - _dragOffset.Y,
                Bounds.Width, Bounds.Height);
            return true;
        }
        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        _dragging = false;
        return false;
    }
}
