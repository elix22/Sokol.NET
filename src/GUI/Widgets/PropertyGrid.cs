using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Describes a single editable property row in a <see cref="PropertyGrid"/>.
/// Use <see cref="PropertyGrid.AddProperty"/> to register rows.
/// </summary>
public class PropertyDescriptor
{
    public string   Name         { get; set; } = string.Empty;
    public string?  Category     { get; set; }
    public Type     PropertyType { get; set; } = typeof(string);

    public Func<object?>?       Get { get; set; }
    public Action<object?>?     Set { get; set; }
}

/// <summary>
/// Two-column property editor. Left column shows property name; right column
/// shows a typed editor widget (TextBox, NumberInput, CheckBox, ColorButton, ComboBox).
/// Group rows by <see cref="PropertyDescriptor.Category"/> to get section headers.
/// Subscribe to <see cref="ObservableObject.PropertyChanged"/> on <see cref="Target"/>
/// for live updates.
/// </summary>
public class PropertyGrid : Widget
{
    private const float RowHeight    = 28f;
    private const float SplitRatio   = 0.4f;  // fraction of width for name column
    private const float HeaderHeight = 22f;

    private readonly List<PropertyDescriptor>     _rows   = [];
    private readonly Dictionary<int, Widget>      _editors = new();
    private object?  _target;
    private float    _scrollY;
    private int      _activeEditorIdx = -1;

    public object? Target
    {
        get => _target;
        set { _target = value; RefreshEditors(); }
    }

    // ─── Registration ────────────────────────────────────────────────────────

    public void AddProperty<T>(
        string    name,
        Func<T?>  getter,
        Action<T?>? setter   = null,
        string?   category   = null)
    {
        _rows.Add(new PropertyDescriptor
        {
            Name         = name,
            Category     = category,
            PropertyType = typeof(T),
            Get          = () => getter(),
            Set          = setter != null ? v => setter(v is T t ? t : default) : null,
        });
        _editors.Clear();  // invalidate editor cache
    }

    // ─── Layout ──────────────────────────────────────────────────────────────
    public override Vector2 PreferredSize(Renderer renderer)
    {
        if (FixedSize.HasValue) return FixedSize.Value;
        return new Vector2(280, CalcTotalHeight());
    }

    private float CalcTotalHeight()
    {
        var categories = GetCategories();
        float totalH = 0;
        foreach (var cat in categories)
        {
            if (cat != null) totalH += HeaderHeight;
            totalH += GetRowsInCategory(cat).Count * RowHeight;
        }
        return totalH;
    }

    // ─── Draw ────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme   = ThemeManager.Current;
        float w       = Bounds.Width;
        float h       = Bounds.Height;
        float sb      = theme.ScrollBarWidth;
        float totalH  = CalcTotalHeight();
        bool  needSB  = totalH > h;
        float viewW   = needSB ? w - sb : w;
        float nameW   = viewW * SplitRatio;
        float editorW = viewW - nameW;
        bool  rtl = ResolvedFlowDirection == FlowDirection.RightToLeft;
        // RTL: name (label) on the right, editor on the left
        float nameColX  = rtl ? editorW : 0f;
        float editorColX = rtl ? 0f    : nameW;

        // Background
        renderer.FillRect(new Rect(0, 0, w, h), theme.InputBackColor);
        renderer.StrokeRoundedRect(new Rect(0, 0, w, h), theme.PanelCornerRadius, 1f, theme.BorderColor);

        renderer.Save();
        renderer.IntersectClip(new Rect(0, 0, viewW, h));
        renderer.Translate(0, -_scrollY);

        float y = 0;
        int   editorIdx = 0;

        ApplyFont(renderer, theme);
        foreach (var cat in GetCategories())
        {
            // Category header
            if (cat != null)
            {
                renderer.FillRect(new Rect(0, y, viewW, HeaderHeight), theme.SurfaceColor);
                renderer.SetTextAlign(rtl ? TextHAlign.Right : TextHAlign.Left);
                renderer.DrawText(rtl ? viewW - 8f : 8f, y + HeaderHeight * 0.5f, cat, theme.TextMutedColor);
                y += HeaderHeight;
            }

            foreach (var desc in GetRowsInCategory(cat))
            {
                var rowR   = new Rect(0, y, viewW, RowHeight);
                // Alternating row tint
                if (editorIdx % 2 == 1)
                    renderer.FillRect(rowR, theme.SurfaceVariant.WithAlpha(0.3f));

                // Divider line
                renderer.DrawLine(0, y + RowHeight, viewW, y + RowHeight, 1f,
                    theme.BorderColor.WithAlpha(0.3f));

                // Name column
                renderer.SetTextAlign(rtl ? TextHAlign.Right : TextHAlign.Left);
                float nameTx = rtl ? nameColX + nameW - 8f : nameColX + 8f;
                renderer.DrawText(nameTx, y + RowHeight * 0.5f, desc.Name, theme.TextColor);

                // Vertical divider
                renderer.DrawLine(nameColX, y, nameColX, y + RowHeight, 1f,
                    theme.BorderColor.WithAlpha(0.4f));

                // Editor widget
                var editor = GetOrCreateEditor(editorIdx, desc);
                if (editor != null)
                {
                    editor.Bounds = new Rect(0, 0, editorW - 4, RowHeight - 4);
                    renderer.Save();
                    renderer.Translate(editorColX + 2, y + 2);
                    editor.Draw(renderer);
                    renderer.Restore();
                }

                y += RowHeight;
                editorIdx++;
            }
        }

        renderer.Restore();

        // Scrollbar
        if (needSB)
        {
            float maxScroll = totalH - h;
            float thumbH    = MathF.Max(20f, h * (h / totalH));
            float thumbY    = (maxScroll > 0 ? _scrollY / maxScroll : 0f) * (h - thumbH);
            renderer.FillRect(new Rect(w - sb, 0, sb, h), theme.ScrollBarTrackColor);
            renderer.FillRoundedRect(
                new Rect(w - sb + 2, thumbY, sb - 4, thumbH),
                (sb - 4) * 0.5f, theme.ScrollBarThumbColor);
        }
    }

    // ─── Input ───────────────────────────────────────────────────────────────
    public override bool OnMouseScroll(MouseEvent e)
    {
        float maxScroll = MathF.Max(0f, CalcTotalHeight() - Bounds.Height);
        _scrollY = Math.Clamp(_scrollY - e.Scroll.Y * RowHeight * 2f, 0f, maxScroll);
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;
        float localY = e.LocalPosition.Y + _scrollY;
        float w      = Bounds.Width;
        float sb     = theme.ScrollBarWidth;
        float totalH = CalcTotalHeight();
        bool  needSB = totalH > Bounds.Height;
        float viewW  = needSB ? w - sb : w;
        float nameW  = viewW * SplitRatio;

        // Only forward clicks on the editor column
        if (e.LocalPosition.X <= nameW) return true;

        int idx;
        if (!RowIndexFromY(localY, out idx)) return false;
        var editor = _editors.TryGetValue(idx, out var ed) ? ed : null;
        if (editor == null) return false;

        float ex = e.LocalPosition.X - nameW - 2;
        float ey = e.LocalPosition.Y + _scrollY - RowYOfIndex(idx) - 2;
        var local = new Vector2(ex, ey);
        editor.OnMouseDown(new MouseEvent { Position = local, LocalPosition = local, Button = e.Button, Clicks = e.Clicks });
        Screen.Instance.Focus.SetFocus(editor);
        _activeEditorIdx = idx;
        return true;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        if (_activeEditorIdx < 0) return false;
        if (!_editors.TryGetValue(_activeEditorIdx, out var editor)) return false;

        var local = EditorLocalPos(e.LocalPosition);
        editor.OnMouseMove(new MouseEvent { Position = local, LocalPosition = local, Button = e.Button });
        return true;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (_activeEditorIdx < 0) return false;
        if (!_editors.TryGetValue(_activeEditorIdx, out var editor)) { _activeEditorIdx = -1; return false; }

        var local = EditorLocalPos(e.LocalPosition);
        editor.OnMouseUp(new MouseEvent { Position = local, LocalPosition = local, Button = e.Button });
        _activeEditorIdx = -1;
        return true;
    }

    private Vector2 EditorLocalPos(Vector2 localPos)
    {
        float w      = Bounds.Width;
        float sb     = theme.ScrollBarWidth;
        float totalH = CalcTotalHeight();
        bool  needSB = totalH > Bounds.Height;
        float viewW  = needSB ? w - sb : w;
        float nameW  = viewW * SplitRatio;
        float ex = localPos.X - nameW - 2;
        float ey = localPos.Y + _scrollY - RowYOfIndex(_activeEditorIdx) - 2;
        return new Vector2(ex, ey);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private Theme theme => ThemeManager.Current;

    private void ApplyFont(Renderer renderer, Theme t)
    {
        renderer.SetFont(t.DefaultFont);
        renderer.SetFontSize(t.FontSize);
    }

    private List<string?> GetCategories()
    {
        var cats   = new List<string?>();
        var seen   = new HashSet<string?>();
        foreach (var r in _rows)
        {
            if (seen.Add(r.Category))
                cats.Add(r.Category);
        }
        return cats;
    }

    private List<PropertyDescriptor> GetRowsInCategory(string? cat) =>
        _rows.FindAll(r => r.Category == cat);

    private Widget? GetOrCreateEditor(int idx, PropertyDescriptor desc)
    {
        if (_editors.TryGetValue(idx, out var cached)) return cached;

        Widget? editor = null;

        if (desc.PropertyType == typeof(bool))
        {
            bool cur = desc.Get?.Invoke() is bool b && b;
            var cb   = new CheckBox { IsChecked = cur };
            cb.CheckedChanged += v =>
            {
                desc.Set?.Invoke(v);
            };
            editor = cb;
        }
        else if (desc.PropertyType == typeof(UIColor))
        {
            var col = desc.Get?.Invoke() is UIColor c ? c : UIColor.White;
            var btn = new ColorButton { Color = col };
            btn.ColorChanged += v =>
            {
                desc.Set?.Invoke(v);
            };
            editor = btn;
        }
        else if (desc.PropertyType == typeof(float) || desc.PropertyType == typeof(double))
        {
            float cur = desc.Get?.Invoke() is float fv ? fv
                      : desc.Get?.Invoke() is double dv ? (float)dv : 0f;
            var ni = new NumberInput { Value = cur, Min = float.MinValue, Max = float.MaxValue };
            ni.ValueChanged += v =>
            {
                if (desc.PropertyType == typeof(double))
                    desc.Set?.Invoke((double)v);
                else
                    desc.Set?.Invoke(v);
            };
            editor = ni;
        }
        else if (desc.PropertyType == typeof(int))
        {
            int cur = desc.Get?.Invoke() is int iv ? iv : 0;
            var ni  = new NumberInput { Value = cur, Min = int.MinValue, Max = int.MaxValue, DecimalPlaces = 0 };
            ni.ValueChanged += v =>
            {
                desc.Set?.Invoke((int)MathF.Round(v));
            };
            editor = ni;
        }
        else if (desc.PropertyType.IsEnum)
        {
            var names = Enum.GetNames(desc.PropertyType);
            var combo = new ComboBox();
            foreach (var n in names) combo.AddItem(n);
            var cur = desc.Get?.Invoke();
            if (cur != null) combo.SelectedIndex = Array.IndexOf(Enum.GetValues(desc.PropertyType), cur);
            combo.SelectionChanged += (selIdx, _) =>
            {
                if (selIdx >= 0 && selIdx < names.Length)
                {
                    desc.Set?.Invoke(Enum.Parse(desc.PropertyType, names[selIdx]));
                }
            };
            editor = combo;
        }
        else
        {
            // Default: TextBox
            string cur = desc.Get?.Invoke()?.ToString() ?? string.Empty;
            var tb      = new TextBox { Text = cur };
            tb.TextChanged += v =>
            {
                desc.Set?.Invoke(v);
            };
            editor = tb;
        }

        if (editor != null)
        {
            editor.Parent = this;
            _editors[idx] = editor;
        }
        return editor;
    }

    private void RefreshEditors()
    {
        // Re-read current values into cached editors
        int editorIdx = 0;
        foreach (var cat in GetCategories())
        {
            foreach (var desc in GetRowsInCategory(cat))
            {
                if (_editors.TryGetValue(editorIdx, out var editor))
                {
                    var val = desc.Get?.Invoke();
                    switch (editor)
                    {
                        case CheckBox   cb:  if (val is bool b) cb.IsChecked = b; break;
                        case ColorButton btn: if (val is UIColor col) btn.Color = col; break;
                        case NumberInput ni: if (val is float fv) ni.Value = fv;
                            else if (val is int iv) ni.Value = iv;
                            else if (val is double dv) ni.Value = (float)dv; break;
                        case TextBox tb: tb.Text = val?.ToString() ?? string.Empty; break;
                    }
                }
                editorIdx++;
            }
        }
    }

    private bool RowIndexFromY(float localY, out int index)
    {
        float y     = 0;
        int   rowIdx = 0;
        foreach (var cat in GetCategories())
        {
            if (cat != null) y += HeaderHeight;
            foreach (var _ in GetRowsInCategory(cat))
            {
                if (localY >= y && localY < y + RowHeight) { index = rowIdx; return true; }
                y += RowHeight;
                rowIdx++;
            }
        }
        index = -1;
        return false;
    }

    private float RowYOfIndex(int target)
    {
        float y     = 0;
        int   rowIdx = 0;
        foreach (var cat in GetCategories())
        {
            if (cat != null) y += HeaderHeight;
            foreach (var _ in GetRowsInCategory(cat))
            {
                if (rowIdx == target) return y;
                y += RowHeight;
                rowIdx++;
            }
        }
        return y;
    }
}
