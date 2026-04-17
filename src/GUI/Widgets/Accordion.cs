using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// A single collapsible section in an <see cref="Accordion"/>.
/// </summary>
public class AccordionSection
{
    public string  Header     { get; set; } = string.Empty;
    public Widget? Content    { get; set; }
    public bool    IsExpanded { get; set; } = false;

    /// <summary>Current animated height of the body (0 = collapsed, natural = fully open).</summary>
    internal float AnimatedHeight { get; set; } = 0f;
    internal Tween? AnimTween     { get; set; }
}

/// <summary>
/// A vertical stack of collapsible sections with animated open/close.
/// </summary>
public class Accordion : Widget
{
    private readonly List<AccordionSection> _sections = [];

    public IReadOnlyList<AccordionSection> Sections => _sections;

    /// <summary>When true, expanding one section collapses all others.</summary>
    public bool Exclusive { get; set; } = false;

    public float HeaderHeight { get; set; } = 32f;
    public Font?  Font         { get; set; }
    public float  FontSize     { get; set; } = 0f;

    public event Action<int>? SectionToggled;

    // ─── API ─────────────────────────────────────────────────────────────────
    public AccordionSection AddSection(string header, Widget? content = null, bool expanded = false)
    {
        var s = new AccordionSection
        {
            Header     = header,
            Content    = content,
            IsExpanded = expanded,
        };
        // If content has a measured size, seed the animated height.
        s.AnimatedHeight = expanded ? NaturalHeight(s) : 0f;
        _sections.Add(s);
        if (content != null)
        {
            content.Parent = this; // logical parent to resolve ScreenPosition
        }
        InvalidateLayout();
        return s;
    }

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        float totalH = 0;
        foreach (var s in _sections)
            totalH += HeaderHeight + s.AnimatedHeight;
        float h = MathF.Max(totalH, HeaderHeight);

        if (FixedSize.HasValue)
        {
            var fs = FixedSize.Value;
            // A zero component means "auto" — consistent with BoxLayout.EffectiveSize semantics.
            return new Vector2(fs.X > 0f ? fs.X : 240f, fs.Y > 0f ? fs.Y : h);
        }
        return new Vector2(240f, h);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        float w   = Bounds.Width;
        float y   = 0f;
        float cr  = theme.ButtonCornerRadius;

        for (int i = 0; i < _sections.Count; i++)
        {
            var s = _sections[i];

            // ── Header ────────────────────────────────────────────────────────
            var hdrR = new Rect(0, y, w, HeaderHeight);
            // Gradient background: top of first, no rounding on bottom except if collapsed last item
            bool isLast     = i == _sections.Count - 1;
            bool roundTop   = i == 0;
            bool roundBot   = isLast && s.AnimatedHeight < 1f;

            float crTL  = roundTop ? cr : 0f;
            float crTR  = roundTop ? cr : 0f;
            float crBR  = roundBot ? cr : 0f;
            float crBL  = roundBot ? cr : 0f;

            // NanoGUI-style gradient header + double bevel
            var hdrGrad = renderer.LinearGradient(
                new Vector2(0, y), new Vector2(0, y + HeaderHeight),
                theme.ButtonGradientTop, theme.ButtonGradientBottom);
            renderer.FillRoundedRectWithPaint(hdrR, 0f, hdrGrad);

            // Inner bright highlight (light top edge = raised)
            renderer.StrokeRoundedRect(
                new Rect(0.5f, y + 1.5f, w - 1f, HeaderHeight - 2f), 0f,
                1f, theme.BorderLight);
            // Outer dark border
            renderer.StrokeRoundedRect(
                new Rect(0.5f, y + 0.5f, w - 1f, HeaderHeight - 1f), 0f,
                1f, theme.BorderDark);

            // Header hover highlight
            if (IsHovered && HoveredSection() == i)
                renderer.FillRoundedRect(hdrR, 0f, theme.ButtonHoverColor.WithAlpha(0.15f));

            // Chevron: RTL = left side, LTR = right side
            bool rtlAcc = ResolvedFlowDirection == FlowDirection.RightToLeft;
            renderer.Save();
            float chevX = rtlAcc ? HeaderHeight * 0.5f : w - HeaderHeight * 0.5f;
            renderer.Translate(chevX, y + HeaderHeight * 0.5f);
            ApplyFont(renderer, theme);
            renderer.SetTextAlign(TextHAlign.Center);
            renderer.DrawText(0, 0, s.IsExpanded ? "▾" : (rtlAcc ? "◂" : "▸"), theme.TextMutedColor);
            renderer.Restore();

            // Header label: RTL = right-aligned from right margin
            ApplyFont(renderer, theme);
            if (rtlAcc)
            {
                renderer.SetTextAlign(TextHAlign.Right);
                renderer.DrawText(w - HeaderHeight - 4f, y + HeaderHeight * 0.5f, s.Header, theme.TextColor);
            }
            else
            {
                renderer.SetTextAlign(TextHAlign.Left);
                renderer.DrawText(12f, y + HeaderHeight * 0.5f, s.Header, theme.TextColor);
            }

            // Separator line under header
            renderer.DrawLine(0, y + HeaderHeight, w, y + HeaderHeight, 1f, theme.BorderColor);

            y += HeaderHeight;

            // ── Body ──────────────────────────────────────────────────────────
            if (s.AnimatedHeight > 0.5f)
            {
                float bodyH = s.AnimatedHeight;
                var bodyR = new Rect(0, y, w, bodyH);
                renderer.FillRect(bodyR, theme.SurfaceColor);

                if (s.Content != null)
                {
                    // Set Bounds.Y = y so ScreenPosition reflects the actual position
                    // in accordion-local space — required for correct HitTestDeep routing.
                    s.Content.Bounds = new Rect(0, y, w, bodyH);
                    renderer.Save();
                    renderer.IntersectClip(bodyR);
                    renderer.Translate(0, y);
                    s.Content.PerformLayout(renderer, force: true);
                    s.Content.Draw(renderer);
                    renderer.Restore();
                }

                // Bottom separator
                renderer.DrawLine(0, y + bodyH, w, y + bodyH, 1f, theme.BorderColor);
                y += bodyH;
            }
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    private Vector2 _mousePos;

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; return true; }
    public override bool OnMouseMove(MouseEvent e)  { _mousePos = e.LocalPosition; return true; }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;

        int idx = HoveredSection();
        if (idx < 0 || idx >= _sections.Count) return false;

        var s = _sections[idx];
        Toggle(s, idx);
        SectionToggled?.Invoke(idx);
        return true;
    }

    // ─── HitTestDeep ─────────────────────────────────────────────────────────
    /// <summary>
    /// Overriden to route events into expanded section content widgets
    /// (they are not in _children so the base walk would miss them).
    /// </summary>
    public override Widget? HitTestDeep(Vector2 screenPoint)
    {
        var local = ToLocal(screenPoint);
        if (!HitTest(local)) return null;

        // Walk expanded section bodies — content.Bounds.Y is set each Draw frame.
        foreach (var s in _sections)
        {
            if (s.AnimatedHeight > 0.5f && s.Content != null)
            {
                var hit = s.Content.HitTestDeep(screenPoint);
                if (hit != null) return hit;
            }
        }

        return this;  // hit the accordion (header area or collapsed body)
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private int HoveredSection()
    {
        float y = 0f;
        for (int i = 0; i < _sections.Count; i++)
        {
            var s = _sections[i];
            if (_mousePos.Y >= y && _mousePos.Y < y + HeaderHeight) return i;
            y += HeaderHeight + s.AnimatedHeight;
        }
        return -1;
    }

    private void Toggle(AccordionSection s, int idx)
    {
        bool expanding = !s.IsExpanded;
        if (Exclusive && expanding)
        {
            foreach (var other in _sections)
                if (other != s && other.IsExpanded) CollapseSection(other);
        }
        s.IsExpanded = expanding;
        if (expanding) ExpandSection(s);
        else           CollapseSection(s);
        InvalidateLayout();
    }

    private float NaturalHeight(AccordionSection s)
    {
        if (s.Content == null) return 120f; // default placeholder height
        var ps = s.Content.FixedSize;
        return ps.HasValue && ps.Value.Y > 0 ? ps.Value.Y : 120f;
    }

    private void ExpandSection(AccordionSection s)
    {
        float target = NaturalHeight(s);
        s.AnimTween?.Stop();
        s.AnimTween = AnimationManager.Instance?.Animate(
            from:     s.AnimatedHeight,
            to:       target,
            duration: 0.22f,
            onUpdate: v => { s.AnimatedHeight = v; InvalidateLayout(); },
            easing:   Easing.EaseOutCubic);
        if (s.AnimTween == null) s.AnimatedHeight = target; // no anim manager
    }

    private void CollapseSection(AccordionSection s)
    {
        s.IsExpanded = false;
        s.AnimTween?.Stop();
        s.AnimTween = AnimationManager.Instance?.Animate(
            from:     s.AnimatedHeight,
            to:       0f,
            duration: 0.18f,
            onUpdate: v => { s.AnimatedHeight = v; InvalidateLayout(); },
            easing:   Easing.EaseInCubic);
        if (s.AnimTween == null) s.AnimatedHeight = 0f;
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
