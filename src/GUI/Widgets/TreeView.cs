using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// A node in the <see cref="TreeView"/> hierarchy.
/// </summary>
public class TreeNode
{
    public string          Label      { get; set; } = string.Empty;
    public List<TreeNode>  Children   { get; }      = [];
    public bool            IsExpanded { get; set; } = false;
    public bool            IsSelected { get; set; } = false;
    public object?         Tag        { get; set; }

    public TreeNode(string label = "") => Label = label;

    public TreeNode Add(string label)
    {
        var n = new TreeNode(label);
        Children.Add(n);
        return n;
    }

    public TreeNode Add(TreeNode child)
    {
        Children.Add(child);
        return child;
    }
}

/// <summary>
/// Hierarchical tree widget with expand/collapse and keyboard navigation.
/// </summary>
public class TreeView : Widget
{
    private TreeNode? _root;
    private TreeNode? _selected;
    private float     _scrollY;
    private bool      _sbDragging;
    private float     _sbDragStartY;
    private float     _sbDragStartScroll;
    public  const float ItemHeight   = 22f;
    public  const float IndentWidth  = 16f;
    public  const float ChevronWidth = 14f;

    public TreeNode? Root
    {
        get => _root;
        set { _root = value; _selected = null; _scrollY = 0f; InvalidateLayout(); }
    }

    public TreeNode? SelectedNode => _selected;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;
    public bool   ShowRoot { get; set; } = false;

    public event Action<TreeNode>? SelectionChanged;
    public event Action<TreeNode>? NodeExpanded;

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(200, 200);
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    private readonly List<(TreeNode node, float y, int depth)> _flatRows = [];

    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var theme = ThemeManager.Current;
        float w   = Bounds.Width, h = Bounds.Height;
        float sb  = theme.ScrollBarWidth;
        float totalH = BuildFlatRows() * ItemHeight;
        bool needSB  = totalH > h;
        float viewW  = needSB ? w - sb : w;

        // Background
        renderer.FillRoundedRect(new Rect(0, 0, w, h), theme.InputCornerRadius, theme.InputBackColor);
        renderer.StrokeRoundedRect(new Rect(0, 0, w, h), theme.InputCornerRadius, 1.5f,
            IsFocused ? theme.AccentColor : theme.BorderColor);

        // Clipped rows
        renderer.Save();
        renderer.IntersectClip(new Rect(1, 1, viewW - 2, h - 2));
        renderer.Translate(0, -_scrollY);

        ApplyFont(renderer, theme);
        foreach (var (node, rowY, depth) in _flatRows)
        {
            if (rowY + ItemHeight < _scrollY)      continue;
            if (rowY > _scrollY + h)               break;

            float indentX = depth * IndentWidth;
            var rowR = new Rect(0, rowY, viewW, ItemHeight);

            // Selection / hover
            if (node.IsSelected)
                renderer.FillRect(rowR, theme.SelectionColor);
            else if (IsHovered && HoveredRow()?.node == node)
                renderer.FillRect(rowR, theme.AccentColor.WithAlpha(0.1f));

            // Chevron for nodes with children
            if (node.Children.Count > 0)
            {
                float cx = indentX + ChevronWidth * 0.5f;
                float cy = rowY + ItemHeight * 0.5f;
                renderer.SetTextAlign(TextHAlign.Center);
                renderer.DrawText(cx, cy, node.IsExpanded ? "▾" : "▸", theme.TextMutedColor);
            }

            // Label
            renderer.SetTextAlign(TextHAlign.Left);
            UIColor labelCol = node.IsSelected        ? theme.AccentColor
                                : node.Children.Count > 0 ? theme.TextColor
                                : theme.TextMutedColor;
            renderer.DrawText(
                indentX + ChevronWidth + 2f,
                rowY + ItemHeight * 0.5f,
                node.Label,
                labelCol);
        }

        renderer.Restore();

        // Scrollbar
        if (needSB)
        {
            float maxScroll = totalH - h;
            float ratio     = h / totalH;
            float thumbH    = MathF.Max(h * ratio, 20f);
            float thumbY    = (maxScroll > 0 ? _scrollY / maxScroll : 0f) * (h - thumbH);
            var trackR = new Rect(w - sb, 0, sb, h);
            renderer.FillRect(trackR, theme.ScrollBarTrackColor);
            renderer.FillRoundedRect(
                new Rect(trackR.X + 2f, thumbY, sb - 4f, thumbH),
                (sb - 4f) * 0.5f, theme.ScrollBarThumbColor);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    private Vector2 _mousePos;

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; return true; }
    public override bool OnMouseMove(MouseEvent e)
    {
        _mousePos = ToLocal(e.Position);
        if (_sbDragging)
        {
            float totalH    = _flatRows.Count * ItemHeight;
            float h         = Bounds.Height;
            float maxScroll = MathF.Max(0f, totalH - h);
            float thumbH    = MathF.Max(h * h / totalH, 20f);
            float trackRange = h - thumbH;
            if (trackRange > 0)
            {
                float delta = e.Position.Y - _sbDragStartY;
                _scrollY = Math.Clamp(_sbDragStartScroll + delta * maxScroll / trackRange, 0f, maxScroll);
            }
            return true;
        }
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        _mousePos = ToLocal(e.Position);

        // Scrollbar click → start drag
        float sb     = ThemeManager.Current.ScrollBarWidth;
        float totalH = _flatRows.Count * ItemHeight;
        float h      = Bounds.Height;
        if (totalH > h && _mousePos.X >= Bounds.Width - sb)
        {
            _sbDragging        = true;
            _sbDragStartY      = e.Position.Y;
            _sbDragStartScroll = _scrollY;
            return true;
        }

        var hit = HoveredRow();
        if (hit == null) return true;
        var node = hit.Value.node;

        // Any click on a parent row → toggle expand + select
        if (node.Children.Count > 0)
        {
            node.IsExpanded = !node.IsExpanded;
            if (node.IsExpanded) NodeExpanded?.Invoke(node);
            InvalidateLayout();
            Select(node);
            return true;
        }

        // Leaf row click → select only
        Select(node);
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (_sbDragging) { _sbDragging = false; return true; }
        return false;
    }

    public override bool OnMouseScroll(MouseEvent e)
    {
        float totalH    = _flatRows.Count * ItemHeight;
        float maxScroll = MathF.Max(0f, totalH - Bounds.Height);
        _scrollY = Math.Clamp(_scrollY - e.Scroll.Y * ItemHeight * 2f, 0f, maxScroll);
        return true;
    }

    public override bool OnKeyDown(KeyEvent e)
    {
        var rows = _flatRows;
        if (rows.Count == 0) return false;

        int curIdx = rows.FindIndex(r => r.node == _selected);
        switch (e.KeyCode)
        {
            case 265:
                if (curIdx > 0) Select(rows[curIdx - 1].node);
                return true;
            case 264:
                if (curIdx < rows.Count - 1) Select(rows[curIdx + 1].node);
                return true;
            case 263:
                if (_selected != null && _selected.IsExpanded)
                { _selected.IsExpanded = false; InvalidateLayout(); }
                return true;
            case 262:
                if (_selected?.Children.Count > 0 && !_selected.IsExpanded)
                { _selected.IsExpanded = true; NodeExpanded?.Invoke(_selected); InvalidateLayout(); }
                return true;
        }
        return false;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private int BuildFlatRows()
    {
        _flatRows.Clear();
        if (_root == null) return 0;
        if (ShowRoot)
            WalkNode(_root, 0);
        else
            foreach (var child in _root.Children) WalkNode(child, 0);
        return _flatRows.Count;

        void WalkNode(TreeNode n, int depth)
        {
            float y = _flatRows.Count * ItemHeight;
            _flatRows.Add((n, y, depth));
            if (n.IsExpanded)
                foreach (var c in n.Children) WalkNode(c, depth + 1);
        }
    }

    private (TreeNode node, float y, int depth)? HoveredRow()
    {
        float localY = _mousePos.Y + _scrollY;
        int idx = (int)(localY / ItemHeight);
        if (idx < 0 || idx >= _flatRows.Count) return null;
        return _flatRows[idx];
    }

    private void Select(TreeNode node)
    {
        if (_selected != null) _selected.IsSelected = false;
        _selected = node;
        if (node != null) node.IsSelected = true;
        SelectionChanged?.Invoke(node!);

        // Scroll to make selected item visible
        if (node != null)
        {
            int idx = _flatRows.FindIndex(r => r.node == node);
            if (idx >= 0)
            {
                float itemY  = idx * ItemHeight;
                float viewH  = Bounds.Height;
                float maxScr = MathF.Max(0f, _flatRows.Count * ItemHeight - viewH);
                if (itemY < _scrollY)            _scrollY = itemY;
                else if (itemY + ItemHeight > _scrollY + viewH) _scrollY = itemY + ItemHeight - viewH;
                _scrollY = Math.Clamp(_scrollY, 0f, maxScr);
            }
        }
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
