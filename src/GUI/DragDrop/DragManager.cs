using System;

namespace Sokol.GUI;

/// <summary>
/// Coordinates a single cross-widget drag-and-drop operation.
///
/// State machine:
///   Idle → PendingDrag (mouse down on an IsDragSource widget)
///        → Dragging    (mouse moved past <see cref="DeadZone"/> pixels)
///        → Dropped     (mouse up over a target that accepted the drop)
///        OR Cancelled  (mouse up over nothing, or Esc pressed)
///
/// <see cref="InputRouter"/> feeds input events into this manager; widgets
/// receive <see cref="Widget.OnDragBegin"/> / <see cref="Widget.OnDragEnter"/>
/// / <see cref="Widget.OnDragOver"/> / <see cref="Widget.OnDragLeave"/> /
/// <see cref="Widget.OnDrop"/> / <see cref="Widget.OnDragEnd"/> as appropriate.
/// </summary>
public sealed class DragManager
{
    public enum DragState { Idle, Pending, Dragging, Dropped, Cancelled }

    /// <summary>Movement threshold before a pending drag becomes an actual drag.</summary>
    public float DeadZone { get; set; } = 5f;

    public DragState      State       { get; private set; } = DragState.Idle;
    public DragDropData?  Data        { get; private set; }
    public Widget?        Source      { get; private set; }
    public Widget?        CurrentTarget { get; private set; }
    public DragDropEffect CurrentEffect { get; private set; } = DragDropEffect.None;
    public Vector2        CurrentScreenPosition { get; private set; }

    private Vector2 _pressStartScreen;
    private Vector2 _pressStartLocal;

    /// <summary>True iff a drag is currently animating the cursor.</summary>
    public bool IsDragging => State == DragState.Dragging;

    // ─── InputRouter hooks ───────────────────────────────────────────────────

    /// <summary>Called on mouse-down. Start pending-drag if the hit widget is a drag source.</summary>
    public void OnMouseDown(Widget? hit, Vector2 screenPos)
    {
        Reset();
        if (hit == null || !hit.IsDragSource) return;
        State              = DragState.Pending;
        Source             = hit;
        _pressStartScreen  = screenPos;
        _pressStartLocal   = hit.ToLocal(screenPos);
    }

    /// <summary>Called on mouse-move. Drives the state machine and target notifications.</summary>
    public void OnMouseMove(Widget? hit, Vector2 screenPos)
    {
        CurrentScreenPosition = screenPos;

        if (State == DragState.Pending)
        {
            var delta = screenPos - _pressStartScreen;
            if (MathF.Abs(delta.X) < DeadZone && MathF.Abs(delta.Y) < DeadZone) return;

            // Threshold crossed — ask the source to build payload.
            var data = Source?.OnDragBegin(_pressStartLocal);
            if (data == null) { Reset(); return; }
            Data  = data;
            State = DragState.Dragging;
        }

        if (State != DragState.Dragging || Data == null) return;

        // Target tracking: swap CurrentTarget if the hit widget changed.
        var target = FirstDropTarget(hit);
        if (target != CurrentTarget)
        {
            if (CurrentTarget != null) CurrentTarget.OnDragLeave();
            CurrentTarget = target;
            if (CurrentTarget != null)
            {
                var local = CurrentTarget.ToLocal(screenPos);
                CurrentTarget.OnDragEnter(new DragDropEventArgs(Data, screenPos, local));
            }
        }

        // DragOver tick.
        if (CurrentTarget != null)
        {
            var local = CurrentTarget.ToLocal(screenPos);
            var e = new DragDropEventArgs(Data, screenPos, local);
            CurrentTarget.OnDragOver(e);
            CurrentEffect = e.Effect;
        }
        else
        {
            CurrentEffect = DragDropEffect.None;
        }
    }

    /// <summary>Called on mouse-up.  Completes or cancels the drag.</summary>
    public void OnMouseUp(Widget? hit, Vector2 screenPos)
    {
        if (State != DragState.Dragging || Data == null) { Reset(); return; }
        CurrentScreenPosition = screenPos;

        var target = FirstDropTarget(hit);
        if (target != null)
        {
            var local = target.ToLocal(screenPos);
            var e = new DragDropEventArgs(Data, screenPos, local) { Effect = CurrentEffect };
            target.OnDrop(e);
            if (e.Handled)
            {
                Source?.OnDragEnd(e.Effect);
                State = DragState.Dropped;
                Reset();
                return;
            }
        }
        // Cancelled.
        Source?.OnDragEnd(DragDropEffect.None);
        if (CurrentTarget != null) CurrentTarget.OnDragLeave();
        State = DragState.Cancelled;
        Reset();
    }

    /// <summary>Aborts any in-flight drag (e.g. on Escape key).</summary>
    public void Cancel()
    {
        if (State == DragState.Dragging)
        {
            Source?.OnDragEnd(DragDropEffect.None);
            if (CurrentTarget != null) CurrentTarget.OnDragLeave();
        }
        State = DragState.Cancelled;
        Reset();
    }

    private void Reset()
    {
        State         = DragState.Idle;
        Data          = null;
        Source        = null;
        CurrentTarget = null;
        CurrentEffect = DragDropEffect.None;
    }

    // Walk up from the hit widget to find the nearest ancestor that accepts drops.
    private static Widget? FirstDropTarget(Widget? w)
    {
        for (var cur = w; cur != null; cur = cur.Parent)
            if (cur.IsDropTarget) return cur;
        return null;
    }

    // ─── Ghost rendering ─────────────────────────────────────────────────────

    /// <summary>Render the drag ghost under the cursor. Called by <see cref="Screen.Draw"/>.</summary>
    public void DrawGhost(Renderer renderer)
    {
        if (State != DragState.Dragging || Data == null) return;
        var theme = ThemeManager.Current;
        float pad = 6f;
        string label = string.IsNullOrEmpty(Data.DragLabel) ? (Data.Format ?? "item") : Data.DragLabel;
        renderer.SetFont(theme.DefaultFont);
        renderer.SetFontSize(theme.FontSize);
        float textW = renderer.MeasureText(label);
        float w = textW + pad * 2f;
        float h = theme.FontSize + pad;
        var pos = CurrentScreenPosition + new Vector2(12f, 8f);

        renderer.Save();
        renderer.Translate(pos.X, pos.Y);
        renderer.FillRect(new Rect(0, 0, w, h), theme.Surface.WithAlpha(0.85f));
        renderer.StrokeRect(new Rect(0, 0, w, h), 1f,
            CurrentEffect == DragDropEffect.None ? theme.Border : theme.Primary);
        renderer.SetTextAlign(TextHAlign.Left);
        renderer.DrawText(pad, h * 0.5f, label, theme.TextColor);
        renderer.Restore();
    }
}
