using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Overlay widget that hosts <see cref="DockPanel"/>s torn off from a
/// <see cref="DockSpace"/>. Each floating panel is rendered as a small window
/// with a title bar and content area; the user may drag it back over a
/// DockSpace to re-dock.
/// </summary>
public sealed class FloatingPanelHost : Widget
{
    private const float TitleBarH = 24f;
    private const float DragThreshold = 5f;

    private readonly List<DockPanel> _panels = [];

    public IReadOnlyList<DockPanel> Panels => _panels;

    private DockPanel? _draggingPanel;
    private Vector2    _dragOffsetLocal;
    private Vector2    _dragStartScreen;
    private bool       _dragBegun;

    internal DockManager? _manager;

    public void Add(DockPanel panel, Rect screenBounds)
    {
        panel.IsFloating     = true;
        panel.Owner          = null;
        panel.FloatingBounds = screenBounds;
        _panels.Add(panel);
        InvalidateLayout();
    }

    public bool Remove(DockPanel panel)
    {
        bool ok = _panels.Remove(panel);
        if (ok) panel.IsFloating = false;
        InvalidateLayout();
        return ok;
    }

    public DockPanel? HitTestPanel(Vector2 screenPoint)
    {
        for (int i = _panels.Count - 1; i >= 0; i--)
        {
            var p = _panels[i];
            if (p.FloatingBounds.Contains(screenPoint)) return p;
        }
        return null;
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;
        var theme = ThemeManager.Current;

        foreach (var p in _panels)
        {
            var b = p.FloatingBounds;
            renderer.Save();
            renderer.Translate(b.X, b.Y);

            renderer.DrawDropShadow(new Rect(0, 0, b.Width, b.Height),
                theme.WindowCornerRadius, theme.ShadowOffset, theme.ShadowBlur, theme.ShadowColor);

            // Body.
            renderer.FillRect(new Rect(0, 0, b.Width, b.Height), theme.WindowBackground);
            renderer.StrokeRect(new Rect(0, 0, b.Width, b.Height), 1f, theme.Border);

            // Title bar.
            renderer.FillRect(new Rect(0, 0, b.Width, TitleBarH), theme.WindowHeader);
            renderer.DrawLine(0, TitleBarH, b.Width, TitleBarH, 1f, theme.Border);
            renderer.SetFont(theme.DefaultFont);
            renderer.SetFontSize(theme.FontSize);
            renderer.SetTextAlign(TextHAlign.Left);
            renderer.DrawText(8f, TitleBarH * 0.5f, p.Title, theme.TextColor);

            // Content.
            var body = new Rect(0, TitleBarH, b.Width, MathF.Max(0, b.Height - TitleBarH));
            renderer.Save();
            renderer.Translate(body.X, body.Y);
            renderer.IntersectClip(new Rect(0, 0, body.Width, body.Height));
            p.Content.Bounds = new Rect(0, 0, body.Width, body.Height);
            p.Content.PerformLayout(renderer, force: true);
            p.Content.Draw(renderer);
            renderer.Restore();

            renderer.Restore();
        }
    }

    public override bool HitTest(Vector2 localPoint)
    {
        foreach (var p in _panels)
            if (p.FloatingBounds.Contains(localPoint)) return true;
        return false;
    }

    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (!Visible || !Enabled) return null;
        var panel = HitTestPanel(screenPoint);
        if (panel == null) return null;

        var b = panel.FloatingBounds;
        var local = screenPoint - new Vector2(b.X, b.Y);
        if (local.Y < TitleBarH) return this; // title bar: host handles drag
        // Body: dive into content.
        var contentScreen = new Vector2(b.X, b.Y + TitleBarH);
        panel.Content.Bounds = new Rect(b.X, b.Y + TitleBarH, b.Width, MathF.Max(0, b.Height - TitleBarH));
        return panel.Content.HitTestDeep(screenPoint) ?? this;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        var panel = HitTestPanel(e.Position);
        if (panel == null) return false;

        var local = e.Position - new Vector2(panel.FloatingBounds.X, panel.FloatingBounds.Y);
        if (local.Y > TitleBarH) return false; // clicks on body pass through

        _draggingPanel     = panel;
        _dragOffsetLocal   = local;
        _dragStartScreen   = e.Position;
        _dragBegun         = false;
        // bring to front
        _panels.Remove(panel);
        _panels.Add(panel);
        return true;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (_draggingPanel == null) return false;

        var delta = e.Position - _dragStartScreen;
        if (!_dragBegun && (MathF.Abs(delta.X) > DragThreshold || MathF.Abs(delta.Y) > DragThreshold))
            _dragBegun = true;

        if (_dragBegun)
        {
            var b = _draggingPanel.FloatingBounds;
            _draggingPanel.FloatingBounds = new Rect(
                e.Position.X - _dragOffsetLocal.X,
                e.Position.Y - _dragOffsetLocal.Y,
                b.Width, b.Height);
            // Let DockManager know so it can highlight drop zones.
            _manager?.UpdateFloatingDrag(_draggingPanel, e.Position);
        }
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        bool handled = _draggingPanel != null;
        if (_draggingPanel != null && _dragBegun)
            _manager?.EndFloatingDrag(_draggingPanel, e.Position);
        _draggingPanel = null;
        _dragBegun     = false;
        return handled;
    }
}
