using System;

namespace Sokol.GUI;

/// <summary>
/// Shared scrollbar rendering used by all scrollable widgets.
/// Draws a track + raised/3-D thumb with gradient and border stroke.
/// </summary>
public static class ScrollbarRenderer
{
    /// <summary>
    /// Draw a vertical scrollbar.
    /// </summary>
    /// <param name="renderer">The NanoVG renderer.</param>
    /// <param name="trackX">X position of the track (left edge).</param>
    /// <param name="trackY">Y position of the track (top edge).</param>
    /// <param name="trackW">Width of the track (typically <c>theme.ScrollBarWidth</c>).</param>
    /// <param name="trackH">Height of the track (visible viewport height).</param>
    /// <param name="scrollOffset">Current scroll offset (0 … contentSize − viewSize).</param>
    /// <param name="contentSize">Total content height.</param>
    /// <param name="viewSize">Visible viewport height.</param>
    /// <param name="isHovered">Whether the scrollbar thumb is hovered.</param>
    public static void DrawVertical(Renderer renderer, float trackX, float trackY,
        float trackW, float trackH, float scrollOffset, float contentSize, float viewSize,
        bool isHovered = false)
    {
        if (contentSize <= viewSize || trackH <= 0f) return;

        var theme = ThemeManager.Current;

        // Track
        var trackR = new Rect(trackX, trackY, trackW, trackH);
        renderer.FillRect(trackR, theme.ScrollBarTrackColor);

        // Thumb geometry
        float maxScroll = MathF.Max(contentSize - viewSize, 1f);
        float ratio     = viewSize / contentSize;
        float thumbH    = MathF.Max(trackH * ratio, 20f);
        float t         = Math.Clamp(scrollOffset / maxScroll, 0f, 1f);
        float thumbY    = trackY + t * (trackH - thumbH);
        var   thumbR    = new Rect(trackX + 2f, thumbY, trackW - 4f, thumbH);
        float cr        = (trackW - 4f) * 0.5f;

        // Thumb: gradient + border for raised 3-D look
        var thumbCol  = isHovered ? theme.ScrollBarThumbHoverColor : theme.ScrollBarThumbColor;
        var thumbGrad = renderer.LinearGradient(
            new Vector2(thumbR.X, thumbR.Y), new Vector2(thumbR.Right, thumbR.Y),
            thumbCol.Lighten(0.18f), thumbCol.Darken(0.12f));
        renderer.FillRoundedRectWithPaint(thumbR, cr, thumbGrad);
        renderer.StrokeRoundedRect(thumbR, cr, 1f, thumbCol.Darken(0.25f));
    }

    /// <summary>
    /// Draw a horizontal scrollbar.
    /// </summary>
    public static void DrawHorizontal(Renderer renderer, float trackX, float trackY,
        float trackW, float trackH, float scrollOffset, float contentSize, float viewSize,
        bool isHovered = false)
    {
        if (contentSize <= viewSize || trackW <= 0f) return;

        var theme = ThemeManager.Current;

        // Track
        var trackR = new Rect(trackX, trackY, trackW, trackH);
        renderer.FillRect(trackR, theme.ScrollBarTrackColor);

        // Thumb geometry
        float maxScroll = MathF.Max(contentSize - viewSize, 1f);
        float ratio     = viewSize / contentSize;
        float thumbW    = MathF.Max(trackW * ratio, 20f);
        float t         = Math.Clamp(scrollOffset / maxScroll, 0f, 1f);
        float thumbX    = trackX + t * (trackW - thumbW);
        var   thumbR    = new Rect(thumbX, trackY + 2f, thumbW, trackH - 4f);
        float cr        = (trackH - 4f) * 0.5f;

        // Thumb: gradient + border for raised 3-D look
        var thumbCol  = isHovered ? theme.ScrollBarThumbHoverColor : theme.ScrollBarThumbColor;
        var thumbGrad = renderer.LinearGradient(
            new Vector2(thumbR.X, thumbR.Y), new Vector2(thumbR.X, thumbR.Bottom),
            thumbCol.Lighten(0.18f), thumbCol.Darken(0.12f));
        renderer.FillRoundedRectWithPaint(thumbR, cr, thumbGrad);
        renderer.StrokeRoundedRect(thumbR, cr, 1f, thumbCol.Darken(0.25f));
    }
}
