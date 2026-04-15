using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Abstract base for every GUI element.  Widgets form a tree rooted at <see cref="Screen"/>.
/// </summary>
public abstract class Widget
{
    // ─── Tree ────────────────────────────────────────────────────────────────
    private readonly List<Widget> _children = [];
    public  Widget?         Parent   { get; internal set; }
    public  IReadOnlyList<Widget> Children => _children;

    public virtual void AddChild(Widget child)
    {
        if (child.Parent != null)
            child.Parent.RemoveChild(child);
        child.Parent = this;
        _children.Add(child);
        InvalidateLayout();
    }

    public virtual void RemoveChild(Widget child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
            InvalidateLayout();
        }
    }

    public void RemoveFromParent() => Parent?.RemoveChild(this);

    public void ClearChildren()
    {
        foreach (var c in _children) c.Parent = null;
        _children.Clear();
        InvalidateLayout();
    }

    // ─── Identity ────────────────────────────────────────────────────────────
    public string? Id { get; set; }

    public Widget? FindById(string id)
    {
        if (Id == id) return this;
        foreach (var c in _children)
        {
            var found = c.FindById(id);
            if (found != null) return found;
        }
        return null;
    }

    // ─── Layout ──────────────────────────────────────────────────────────────
    /// <summary>Bounding box in parent-local coordinates, set by the parent's layout pass.</summary>
    public Rect    Bounds      { get; set; }
    /// <summary>Canvas position used by <see cref="CanvasLayout"/>. Set before layout for absolute positioning.</summary>
    private Vector2 _position;
    public Vector2 Position
    {
        get => _position;
        set { _position = value; InvalidateLayout(); }
    }
    /// <summary>Override width+height; null = auto.</summary>
    public Vector2? FixedSize   { get; set; }
    public Thickness Margin     { get; set; }
    public Thickness Padding    { get; set; }
    /// <summary>Layout algorithm applied to children (default: CanvasLayout).</summary>
    public ILayout  Layout      { get; set; } = new CanvasLayout();

    private bool _layoutDirty = true;

    public void InvalidateLayout()
    {
        _layoutDirty = true;
        Parent?.InvalidateLayout();
    }

    /// <summary>Measure the widget's desired size in logical pixels.
    /// A FixedSize component of 0 means "auto" — it is measured from content for that axis.</summary>
    public virtual Vector2 PreferredSize(Renderer renderer)
    {
        if (!FixedSize.HasValue)
            return Layout.Measure(this, renderer, new Vector2(0, 0));
        var fs = FixedSize.Value;
        if (fs.X > 0f && fs.Y > 0f) return fs;
        // One or both components are 0 = "auto" — measure for those axes.
        var measured = Layout.Measure(this, renderer, new Vector2(0, 0));
        return new Vector2(fs.X > 0f ? fs.X : measured.X, fs.Y > 0f ? fs.Y : measured.Y);
    }

    /// <summary>Runs layout on this widget and all descendants.</summary>
    /// <param name="force">When true, layout runs even if the dirty flag is clear
    /// (used by containers that update child Bounds manually each frame, e.g. TabView).</param>
    public void PerformLayout(Renderer renderer, bool force = false)
    {
        if (!force && !_layoutDirty) return;
        _layoutDirty = false;

        Layout.Arrange(this, renderer, Bounds);
        foreach (var child in _children)
            child.PerformLayout(renderer, force);
    }

    // ─── Visibility / State ──────────────────────────────────────────────────
    public bool Visible  { get; set; } = true;
    public bool Enabled  { get; set; } = true;
    public bool IsFocused   { get; internal set; }
    public bool IsHovered   { get; internal set; }
    public bool IsPressed   { get; internal set; }
    public string? Tooltip  { get; set; }

    // ─── Data binding ────────────────────────────────────────────────────────
    private object? _dataContext;
    public  object? DataContext
    {
        get => _dataContext;
        set
        {
            if (_dataContext == value) return;
            _dataContext = value;
            OnDataContextChanged(value);
            // Propagate to children that don't have their own context.
            foreach (var c in _children)
                if (c._dataContext == null) c.DataContext = value;
        }
    }

    protected virtual void OnDataContextChanged(object? newContext) { }

    // ─── Drawing ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Draws this widget's children.  Each child is translated to its Bounds origin
    /// so it can draw at local (0,0) coordinates.
    /// Override to draw self before calling base for children, e.g. Panel.Draw.
    /// </summary>
    public virtual void Draw(Renderer renderer)
    {
        if (!Visible) return;

        // Translate to each child's Bounds and draw.  Save/Restore isolates transforms.
        foreach (var child in _children)
        {
            if (!child.Visible) continue;
            renderer.Save();
            renderer.Translate(child.Bounds.X, child.Bounds.Y);
            child.Draw(renderer);
            renderer.Restore();
        }
    }

    /// <summary>
    /// Called by Screen to draw this widget's popup/overlay content on top of all other widgets.
    /// Override in widgets that have dropdowns or tooltips that extend beyond their Bounds.
    /// The renderer is already translated to this widget's ScreenPosition.
    /// </summary>
    public virtual void DrawPopupOverlay(Renderer renderer) { }

    /// <summary>Called when a click occurs outside the popup area, to dismiss it.</summary>
    public virtual void OnPopupDismiss() { }

    // ─── Hit-testing ─────────────────────────────────────────────────────────
    /// <summary>Returns true if <paramref name="localPoint"/> is inside this widget.</summary>
    public virtual bool HitTest(Vector2 localPoint) =>
        localPoint.X >= 0 && localPoint.Y >= 0 &&
        localPoint.X < Bounds.Width && localPoint.Y < Bounds.Height;

    /// <summary>Walk children back-to-front; return deepest widget that contains <paramref name="screenPoint"/>.</summary>
    public virtual Widget? HitTestDeep(Vector2 screenPoint)
    {
        if (!Visible || !Enabled) return null;

        var local = ToLocal(screenPoint);
        bool hit  = HitTest(local);

        bool log = Screen.DbgFrame <= 5 || Screen.DbgFrame % 300 == 0;
        if (log)
            Sokol.SLog.Info($"HitTest[{Screen.DbgFrame}]: {GetType().Name} screenPos={ScreenPosition} Bounds={Bounds} local={local} hit={hit}", "Sokol.GUI");

        if (!hit) return null;

        // Children drawn last (top-most) get priority.
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var h2 = _children[i].HitTestDeep(screenPoint);
            if (h2 != null) return h2;
        }
        return this;
    }

    // ─── Coordinate helpers ──────────────────────────────────────────────────
    /// <summary>Convert a screen-space point to this widget's local space.</summary>
    public Vector2 ToLocal(Vector2 screenPoint)
    {
        var origin = ScreenPosition;
        return new Vector2(screenPoint.X - origin.X, screenPoint.Y - origin.Y);
    }

    /// <summary>Walk up the tree to compute screen-space top-left corner.</summary>
    public Vector2 ScreenPosition
    {
        get
        {
            var pos = new Vector2(Bounds.X, Bounds.Y);
            if (Parent != null) pos = new Vector2(pos.X + Parent.ScreenPosition.X, pos.Y + Parent.ScreenPosition.Y);
            return pos;
        }
    }

    // ─── Input event virtuals ────────────────────────────────────────────────
    public virtual bool OnMouseDown  (MouseEvent e)  => false;
    public virtual bool OnMouseUp    (MouseEvent e)  => false;
    public virtual bool OnMouseMove  (MouseEvent e)  => false;
    public virtual bool OnMouseEnter (MouseEvent e)  => false;
    public virtual bool OnMouseLeave (MouseEvent e)  => false;
    public virtual bool OnMouseScroll(MouseEvent e)  => false;
    public virtual bool OnKeyDown    (KeyEvent   e)  => false;
    public virtual bool OnKeyUp      (KeyEvent   e)  => false;
    public virtual bool OnTextInput  (KeyEvent   e)  => false;
    public virtual bool OnTouchDown  (TouchEvent e)  => false;
    public virtual bool OnTouchUp    (TouchEvent e)  => false;
    public virtual bool OnTouchMove  (TouchEvent e)  => false;
    public virtual void OnFocusGained()              { }
    public virtual void OnFocusLost  ()              { }

    // ─── Convenience ─────────────────────────────────────────────────────────
    public event Action? Clicked;
    protected void RaiseClicked() => Clicked?.Invoke();
}
