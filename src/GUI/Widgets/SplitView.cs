using System;

namespace Sokol.GUI;

/// <summary>
/// Two-pane container with a draggable divider.
/// </summary>
public class SplitView : Widget
{
    private const float DividerWidth = 6f;
    private bool  _dragging;
    private float _dragStartPos;
    private float _dragStartRatio;

    private Widget? _first;
    private Widget? _second;

    private float _splitRatio = 0.5f;

    public SliderOrientation Orientation { get; set; } = SliderOrientation.Horizontal;

    public float SplitRatio
    {
        get => _splitRatio;
        set { _splitRatio = Math.Clamp(value, 0f, 1f); InvalidateLayout(); }
    }

    public float MinSize { get; set; } = 40f;

    public Widget? First
    {
        get => _first;
        set
        {
            if (_first != null) RemoveChild(_first);
            _first = value;
            if (value != null) AddChild(value);
        }
    }

    public Widget? Second
    {
        get => _second;
        set
        {
            if (_second != null) RemoveChild(_second);
            _second = value;
            if (value != null) AddChild(value);
        }
    }

    public event Action<float>? SplitRatioChanged;

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        float w   = Bounds.Width, h = Bounds.Height;

        ArrangeChildren(w, h);

        // Draw children
        if (_first != null)
        {
            renderer.Save();
            renderer.Translate(_first.Bounds.X, _first.Bounds.Y);
            renderer.IntersectClip(new Rect(0, 0, _first.Bounds.Width, _first.Bounds.Height));
            _first.Draw(renderer);
            renderer.Restore();
        }
        if (_second != null)
        {
            renderer.Save();
            renderer.Translate(_second.Bounds.X, _second.Bounds.Y);
            renderer.IntersectClip(new Rect(0, 0, _second.Bounds.Width, _second.Bounds.Height));
            _second.Draw(renderer);
            renderer.Restore();
        }

        // Divider
        var divR = DividerRect(w, h);
        var divColor = _dividerHovered || _dragging
            ? theme.AccentColor.WithAlpha(0.6f)
            : theme.BorderColor.WithAlpha(0.7f);
        renderer.FillRect(divR, divColor);

        // Grip dots
        DrawGrip(renderer, divR, theme);
    }

    private void DrawGrip(Renderer renderer, Rect divR, Theme theme)
    {
        float cx    = divR.X + divR.Width  * 0.5f;
        float cy    = divR.Y + divR.Height * 0.5f;
        float dotR  = 1.8f;
        float gap   = 5f;
        var   dotC  = theme.TextMutedColor.WithAlpha(0.7f);

        if (Orientation == SliderOrientation.Horizontal)
        {
            for (int i = -1; i <= 1; i++)
                renderer.FillCircle(cx, cy + i * gap, dotR, dotC);
        }
        else
        {
            for (int i = -1; i <= 1; i++)
                renderer.FillCircle(cx + i * gap, cy, dotR, dotC);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    private bool _dividerHovered;

    public override bool OnMouseEnter(MouseEvent e) { return true; }
    public override bool OnMouseLeave(MouseEvent e)
    {
        _dividerHovered = false;
        // Do NOT clear _dragging here: mouse can leave bounds during a fast drag
        // and the InputRouter still routes moves/up through _captured.
        return true;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        float w = Bounds.Width, h = Bounds.Height;
        var divR = DividerRect(w, h);
        var localPos = e.LocalPosition;
        _dividerHovered = divR.Contains(localPos);

        if (_dragging)
        {
            float total  = Orientation == SliderOrientation.Horizontal ? w : h;
            float travel = Orientation == SliderOrientation.Horizontal
                ? e.Position.X - _dragStartPos
                : e.Position.Y - _dragStartPos;

            // RTL: dragging right (positive travel) should decrease _splitRatio
            if (Orientation == SliderOrientation.Horizontal &&
                ResolvedFlowDirection == FlowDirection.RightToLeft)
                travel = -travel;

            float rawRatio = _dragStartRatio + travel / (total - DividerWidth);
            float minR = MinSize / total;
            float maxR = 1f - minR;
            float newR = Math.Clamp(rawRatio, minR, maxR);
            if (MathF.Abs(newR - _splitRatio) > 0.001f)
            {
                _splitRatio = newR;
                InvalidateLayout();
                SplitRatioChanged?.Invoke(_splitRatio);
            }
            return true;
        }
        return _dividerHovered;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        float w = Bounds.Width, h = Bounds.Height;
        var localPos = e.LocalPosition;  // already in widget-local coords via InputRouter
        if (!DividerRect(w, h).Contains(localPos)) return false;
        _dragging = true;
        _dragStartPos   = Orientation == SliderOrientation.Horizontal ? e.Position.X : e.Position.Y;
        _dragStartRatio = _splitRatio;
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        bool wasDragging = _dragging;
        _dragging = false;
        return wasDragging;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private Rect DividerRect(float w, float h)
    {
        if (Orientation == SliderOrientation.Horizontal)
        {
            bool rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
            float ratio = rtl ? (1f - _splitRatio) : _splitRatio;
            float x = (w - DividerWidth) * ratio;
            return new Rect(x, 0, DividerWidth, h);
        }
        else
        {
            float y = (h - DividerWidth) * _splitRatio;
            return new Rect(0, y, w, DividerWidth);
        }
    }

    private void ArrangeChildren(float w, float h)
    {
        var divR = DividerRect(w, h);
        if (Orientation == SliderOrientation.Horizontal)
        {
            if (_first  != null) _first.Bounds  = new Rect(0,          0, divR.X,          h);
            if (_second != null) _second.Bounds = new Rect(divR.Right, 0, w - divR.Right,  h);
        }
        else
        {
            if (_first  != null) _first.Bounds  = new Rect(0, 0,          w, divR.Y);
            if (_second != null) _second.Bounds = new Rect(0, divR.Bottom, w, h - divR.Bottom);
        }

        var renderer = Screen.Instance?.Renderer;
        if (renderer != null)
        {
            _first?.PerformLayout(renderer, force: true);
            _second?.PerformLayout(renderer, force: true);
        }
    }
}
