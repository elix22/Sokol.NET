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

    /// <summary>
    /// Optional widget content. When set, this widget is rendered in the row
    /// instead of the <see cref="Label"/> text (Avalonia-style templated items).
    /// The widget is sized to fit the available row space.
    /// </summary>
    public Widget? Content { get; set; }

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
/// Hierarchical tree widget with triangle expand/collapse indicators,
/// double-click or arrow-click to expand, keyboard navigation, and
/// support for widget content items (not just text labels).
/// </summary>
public class TreeView : Widget
{
    private TreeNode? _root;
    private TreeNode? _selected;
    private float     _scrollY;
    private bool      _sbDragging;
    private bool      _sbHovered;
    private float     _sbDragStartY;
    private float     _sbDragStartScroll;
    private int       _anchorRowIdx = -1;
    private readonly HashSet<TreeNode> _selectedNodes = [];

    // Double-click tracking
    private long      _lastClickMs;
    private TreeNode? _lastClickNode;
    // Widget content click forwarding
    private Widget?   _contentClickTarget;

    public  float ItemHeight   { get; set; } = 22f;
    public  const float IndentWidth  = 16f;
    public  const float ArrowWidth   = 16f;  // area for the disclosure triangle
    private const float ArrowSize    = 5f;   // half-size of the triangle shape

    public TreeNode? Root
    {
        get => _root;
        set { _root = value; _selected = null; _selectedNodes.Clear(); _anchorRowIdx = -1; _scrollY = 0f; InvalidateLayout(); }
    }

    public TreeNode?              SelectedNode  => _selected;
    public IReadOnlySet<TreeNode> SelectedNodes => _selectedNodes;

    /// <summary>When true, Ctrl/Cmd+Click adds to selection instead of replacing it.</summary>
    public bool MultiSelect { get; set; } = false;

    public Font?  Font     { get; set; }
    public float  FontSize { get; set; } = 0f;
    public bool   ShowRoot { get; set; } = false;

    public event Action<TreeNode>?  SelectionChanged;
    public event Action<TreeNode>?  NodeExpanded;
    public event Action<TreeNode>?  NodeDoubleClicked;

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

        // NanoGUI-style sunken container
        float cr = theme.InputCornerRadius;
        renderer.FillRoundedRect(new Rect(0, 0, w, h), cr, theme.InputBackColor);
        var tvInset = renderer.BoxGradient(
            new Rect(1, 2, w - 2, h - 2), cr, 4f,
            new UIColor(1f, 1f, 1f, 0.06f),
            new UIColor(0f, 0f, 0f, 0.15f));
        renderer.FillRoundedRectWithPaint(new Rect(0, 0, w, h), cr, tvInset);
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, w - 1f, h - 1f),
            MathF.Max(cr - 0.5f, 0f), 1f,
            IsFocused ? theme.AccentColor : UIColor.Black.WithAlpha(0.188f));

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
            bool isMultiSel = MultiSelect && _selectedNodes.Contains(node);
            if (node.IsSelected || isMultiSel)
                renderer.FillRect(rowR, theme.SelectionColor);
            else if (IsHovered && HoveredRow()?.node == node)
                renderer.FillRect(rowR, theme.AccentColor.WithAlpha(0.1f));

            // Disclosure triangle for nodes with children
            if (node.Children.Count > 0)
            {
                float cx = indentX + ArrowWidth * 0.5f;
                float cy = rowY + ItemHeight * 0.5f;

                if (node.IsExpanded)
                {
                    // Down-pointing triangle: ▾
                    renderer.FillTriangle(
                        new Vector2(cx - ArrowSize, cy - ArrowSize * 0.5f),
                        new Vector2(cx + ArrowSize, cy - ArrowSize * 0.5f),
                        new Vector2(cx, cy + ArrowSize * 0.5f),
                        theme.TextMutedColor);
                }
                else
                {
                    // Right-pointing triangle: ▸
                    renderer.FillTriangle(
                        new Vector2(cx - ArrowSize * 0.5f, cy - ArrowSize),
                        new Vector2(cx + ArrowSize * 0.5f, cy),
                        new Vector2(cx - ArrowSize * 0.5f, cy + ArrowSize),
                        theme.TextMutedColor);
                }
            }

            // Content: widget or text label
            float contentX = indentX + ArrowWidth + 2f;
            if (node.Content != null)
            {
                // Render widget content
                float contentW = viewW - contentX - 2f;
                node.Content.Bounds = new Rect(contentX, rowY, contentW, ItemHeight);
                renderer.Save();
                renderer.Translate(contentX, rowY);
                node.Content.Draw(renderer);
                renderer.Restore();
            }
            else
            {
                // Text label
                renderer.SetTextAlign(TextHAlign.Left);
                UIColor labelCol = node.IsSelected        ? theme.AccentColor
                                    : node.Children.Count > 0 ? theme.TextColor
                                    : theme.TextMutedColor;
                renderer.DrawText(contentX, rowY + ItemHeight * 0.5f, node.Label, labelCol);
            }
        }

        renderer.Restore();

        // Scrollbar
        if (needSB)
        {
            float totalHH = _flatRows.Count * ItemHeight;
            ScrollbarRenderer.DrawVertical(renderer, w - sb, 0, sb, h, _scrollY, totalHH, h, _sbHovered);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    private Vector2 _mousePos;

    public override bool OnMouseEnter(MouseEvent e) { IsHovered = true;  return true; }
    public override bool OnMouseLeave(MouseEvent e) { IsHovered = false; _sbHovered = false; return true; }
    public override bool OnMouseMove(MouseEvent e)
    {
        _mousePos = e.LocalPosition;
        float tvSb = ThemeManager.Current.ScrollBarWidth;
        float tvTotalH = _flatRows.Count * ItemHeight;
        _sbHovered = tvTotalH > Bounds.Height && _mousePos.X >= Bounds.Width - tvSb;
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
        _mousePos = e.LocalPosition;

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
        var node  = hit.Value.node;
        int depth = hit.Value.depth;

        // ── Double-click detection ──
        long nowMs = Environment.TickCount64;
        bool isDoubleClick = (node == _lastClickNode) && (nowMs - _lastClickMs < 400);
        _lastClickMs   = nowMs;
        _lastClickNode = node;

        float indentX  = depth * IndentWidth;
        float contentX = indentX + ArrowWidth + 2f;

        // ── Click on disclosure triangle → toggle expand (single click) ──
        // Use a wider hit area so finger taps on mobile can reach the
        // triangle comfortably.  The touch-friendly zone extends from
        // indentX to contentX + extra padding (total ≈ 34px at depth 0).
        float arrowHitEnd = contentX + 16f;
        bool clickedArrow = node.Children.Count > 0 &&
                            _mousePos.X >= indentX &&
                            _mousePos.X < arrowHitEnd;
        if (clickedArrow)
        {
            node.IsExpanded = !node.IsExpanded;
            if (node.IsExpanded) NodeExpanded?.Invoke(node);
            InvalidateLayout();
        }

        // ── Forward click to widget content (e.g. CheckBox) ──
        if (!clickedArrow && node.Content != null && _mousePos.X >= contentX)
        {
            float rowY = hit.Value.y;
            var widgetEvent = new MouseEvent
            {
                LocalPosition = new Vector2(_mousePos.X - contentX, _mousePos.Y + _scrollY - rowY),
                Position      = e.Position,
                Button        = e.Button,
                Modifiers     = e.Modifiers,
                Clicks        = isDoubleClick ? 2 : 1,
            };
            _contentClickTarget = node.Content;
            node.Content.OnMouseDown(widgetEvent);
        }

        // ── Double-click on row → toggle expand ──
        if (!clickedArrow && isDoubleClick && node.Children.Count > 0)
        {
            node.IsExpanded = !node.IsExpanded;
            if (node.IsExpanded) NodeExpanded?.Invoke(node);
            InvalidateLayout();
            NodeDoubleClicked?.Invoke(node);
        }
        else if (isDoubleClick)
        {
            NodeDoubleClicked?.Invoke(node);
        }

        // ── Selection logic (always on click) ──
        bool ctrl  = MultiSelect && (e.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
        bool shift = MultiSelect && (e.Modifiers & KeyModifiers.Shift) != 0;

        if (shift && _anchorRowIdx >= 0)
        {
            int curIdx = _flatRows.FindIndex(r => r.node == node);
            if (curIdx >= 0)
            {
                int lo = Math.Min(_anchorRowIdx, curIdx);
                int hi = Math.Max(_anchorRowIdx, curIdx);
                if (!ctrl)
                {
                    foreach (var n in _selectedNodes) n.IsSelected = false;
                    _selectedNodes.Clear();
                    if (_selected != null) _selected.IsSelected = false;
                }
                for (int i = lo; i <= hi; i++)
                {
                    _flatRows[i].node.IsSelected = true;
                    _selectedNodes.Add(_flatRows[i].node);
                }
                _selected = node;
                node.IsSelected = true;
                SelectionChanged?.Invoke(node);
            }
        }
        else if (ctrl)
        {
            int curIdx = _flatRows.FindIndex(r => r.node == node);
            _anchorRowIdx = curIdx;
            Select(node, addToSelection: true);
        }
        else
        {
            int curIdx = _flatRows.FindIndex(r => r.node == node);
            _anchorRowIdx = curIdx;
            Select(node, addToSelection: false);
        }

        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (_sbDragging) { _sbDragging = false; return true; }
        if (_contentClickTarget != null)
        {
            var widgetEvent = new MouseEvent
            {
                LocalPosition = new Vector2(_mousePos.X, _mousePos.Y),
                Position      = e.Position,
                Button        = e.Button,
                Modifiers     = e.Modifiers,
            };
            _contentClickTarget.OnMouseUp(widgetEvent);
            _contentClickTarget = null;
            return true;
        }
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
        bool alt   = (e.Modifiers & KeyModifiers.Alt) != 0;

        switch (e.KeyCode)
        {
            case 265: // Up
                if (curIdx > 0) Select(rows[curIdx - 1].node);
                return true;

            case 264: // Down
                if (curIdx < rows.Count - 1) Select(rows[curIdx + 1].node);
                return true;

            case 263: // Left — collapse selected node (Option+Left = collapse recursively)
                if (_selected != null && _selected.IsExpanded)
                {
                    if (alt)
                        CollapseRecursive(_selected);
                    else
                        _selected.IsExpanded = false;
                    InvalidateLayout();
                }
                else if (_selected != null)
                {
                    // Move to parent node
                    var parent = FindParent(_selected);
                    if (parent != null) Select(parent);
                }
                return true;

            case 262: // Right — expand selected node (Option+Right = expand recursively)
                if (_selected?.Children.Count > 0)
                {
                    if (!_selected.IsExpanded)
                    {
                        if (alt)
                            ExpandRecursive(_selected);
                        else
                            _selected.IsExpanded = true;
                        NodeExpanded?.Invoke(_selected);
                        InvalidateLayout();
                    }
                    else
                    {
                        // Already expanded: move to first child
                        if (_selected.Children.Count > 0)
                            Select(rows.Find(r => r.node == _selected.Children[0]).node);
                    }
                }
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

    private void Select(TreeNode node, bool addToSelection = false)
    {
        if (MultiSelect && addToSelection)
        {
            if (_selectedNodes.Contains(node))
                _selectedNodes.Remove(node);
            else
                _selectedNodes.Add(node);
        }
        else
        {
            foreach (var n in _selectedNodes) n.IsSelected = false;
            _selectedNodes.Clear();
            if (_selected != null) _selected.IsSelected = false;
            _selectedNodes.Add(node);
        }
        if (_selected != null) _selected.IsSelected = false;
        _selected = node;
        if (node != null) node.IsSelected = true;
        SelectionChanged?.Invoke(node!);

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

    private void ExpandRecursive(TreeNode node)
    {
        node.IsExpanded = true;
        foreach (var c in node.Children)
            if (c.Children.Count > 0) ExpandRecursive(c);
    }

    private void CollapseRecursive(TreeNode node)
    {
        node.IsExpanded = false;
        foreach (var c in node.Children)
            if (c.Children.Count > 0) CollapseRecursive(c);
    }

    private TreeNode? FindParent(TreeNode target)
    {
        if (_root == null) return null;
        return FindParentIn(_root, target);

        static TreeNode? FindParentIn(TreeNode parent, TreeNode target)
        {
            foreach (var child in parent.Children)
            {
                if (child == target) return parent;
                var found = FindParentIn(child, target);
                if (found != null) return found;
            }
            return null;
        }
    }

    private void ApplyFont(Renderer renderer, Theme theme)
    {
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }
}
