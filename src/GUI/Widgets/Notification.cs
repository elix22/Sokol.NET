using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>Severity levels for toast notifications.</summary>
public enum NotificationType { Info, Success, Warning, Error }

/// <summary>
/// Static helper to show toast notifications via the <see cref="NotificationHost"/>.
/// </summary>
public static class Notification
{
    public static void Show(string message, float durationSeconds = 3f,
                             NotificationType type = NotificationType.Info)
    {
        NotificationHost.Instance?.Enqueue(message, durationSeconds, type);
    }
}

// ─── Internal notification entry ─────────────────────────────────────────────
internal sealed class NotificationEntry
{
    public string           Message      { get; }
    public NotificationType Type         { get; }
    public float            Alpha        { get; set; } = 0f;
    public float            TimeLeft     { get; set; }
    public Tween?           AlphaTween   { get; set; }

    public NotificationEntry(string msg, float duration, NotificationType type)
    {
        Message  = msg;
        Type     = type;
        TimeLeft = duration;
    }
}

/// <summary>
/// Screen-level overlay that renders stacked toast notifications in the bottom-right.
/// One instance is created by <see cref="Screen.Initialize"/> and accessible via
/// <see cref="Instance"/>.
/// </summary>
public sealed class NotificationHost : Widget
{
    public static NotificationHost? Instance { get; private set; }

    private readonly List<NotificationEntry> _active = [];

    private const float ToastW       = 280f;
    private const float ToastH       =  48f;
    private const float ToastPadding =   8f;
    private const float ToastMarginR =  16f;
    private const float ToastMarginB =  16f;

    public NotificationHost()
    {
        Instance = this;
        Visible  = true;
    }

    public void Enqueue(string message, float duration, NotificationType type)
    {
        var entry = new NotificationEntry(message, duration, type);
        _active.Add(entry);

        // Fade in — null AlphaTween on complete so the fade-out check can trigger.
        entry.AlphaTween = AnimationManager.Instance?.Animate(
            from: 0f, to: 1f, duration: 0.25f,
            onUpdate: v => entry.Alpha = v,
            onComplete: () => entry.AlphaTween = null);
        if (entry.AlphaTween == null) entry.Alpha = 1f;
    }

    // ─── Draw ─────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (_active.Count == 0) return;

        var theme = ThemeManager.Current;
        float screenW = Bounds.Width;
        float screenH = Bounds.Height;
        float dt      = 1f / 60f;   // approx; sufficient for per-frame decay

        float startX = screenW - ToastW - ToastMarginR;
        float startY = screenH - ToastMarginB - ToastH;

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var e = _active[i];

            // Update timer (fade-out when nearing end)
            e.TimeLeft -= dt;
            if (e.TimeLeft <= 0.3f && e.AlphaTween == null)
            {
                e.AlphaTween = AnimationManager.Instance?.Animate(
                    from: e.Alpha, to: 0f, duration: 0.28f,
                    onUpdate: v => e.Alpha = v,
                    onComplete: () => _active.Remove(e));
                if (e.AlphaTween == null) { _active.RemoveAt(i); continue; }
            }

            if (e.Alpha < 0.01f) continue;

            float y = startY - ((_active.Count - 1 - i) * (ToastH + 6f));
            DrawToast(renderer, theme, new Rect(startX, y, ToastW, ToastH), e);
        }
    }

    private void DrawToast(Renderer renderer, Theme theme, Rect r, NotificationEntry e)
    {
        renderer.Save();
        renderer.SetGlobalAlpha(e.Alpha);

        float cr = 6f;
        var   bg = theme.SurfaceVariant;
        var   accent = TypeColor(e.Type, theme);

        // Shadow
        renderer.DrawDropShadow(r, cr, theme.ShadowOffset, 8f, theme.ShadowColor.WithAlpha(0.5f * e.Alpha));

        // Background
        renderer.FillRoundedRect(r, cr, bg);

        // Left accent bar
        renderer.FillRoundedRect(new Rect(r.X, r.Y + 4f, 3f, r.Height - 8f), 2f, accent);

        // Border
        renderer.StrokeRoundedRect(r, cr, 1f, accent.WithAlpha(0.6f));

        // Message text
        renderer.SetFont(theme.DefaultFont);
        renderer.SetFontSize(theme.SmallFontSize);
        renderer.SetTextAlign(TextHAlign.Left);
        renderer.DrawText(r.X + 12f, r.Y + r.Height * 0.5f, e.Message, theme.TextColor);

        renderer.Restore();
    }

    private static UIColor TypeColor(NotificationType t, Theme theme) => t switch
    {
        NotificationType.Success => UIColor.FromHex("#A6E3A1"),
        NotificationType.Warning => UIColor.FromHex("#F9E2AF"),
        NotificationType.Error   => UIColor.FromHex("#F38BA8"),
        _                        => theme.AccentColor,
    };

    // Screen-size notification host doesn't need hit-testing; pass through all input.
    public override bool HitTest(Vector2 localPoint) => false;
}
