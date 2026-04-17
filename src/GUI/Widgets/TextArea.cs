using System;
using System.Text;
using static Sokol.SApp;

namespace Sokol.GUI;

/// <summary>
/// Scrollable multi-line text area.  Set <see cref="IsEditable"/> = true to
/// enable full editing (caret, selection, keyboard input, clipboard).
/// </summary>
public class TextArea : Widget
{
    // ── Core state ────────────────────────────────────────────────────────────
    private readonly StringBuilder _sb = new();

    public string Text
    {
        get => _sb.ToString();
        set
        {
            _sb.Clear();
            _sb.Append(value ?? string.Empty);
            _cursor   = Math.Min(_cursor, _sb.Length);
            _selStart = -1;
            TextChanged?.Invoke(Text);
        }
    }

    public bool      IsEditable { get; set; }
    public UIColor?  ForeColor  { get; set; }
    public UIColor?  BackColor  { get; set; }
    public Font?     Font       { get; set; }
    public float     FontSize   { get; set; } = 0f;
    public TextAlign Align      { get; set; } = TextAlign.Left;

    public event Action<string>? TextChanged;

    // ── Scroll state ──────────────────────────────────────────────────────────
    private float _scrollY;
    private bool  _sbDragging;
    private float _sbDragStartY;
    private float _sbDragStartScroll;
    private bool  _sbHovered;

    // ── Editing state ─────────────────────────────────────────────────────────
    private int     _cursor;
    private int     _selStart = -1;       // -1 = no selection
    private bool    _mouseDragging;
    private long    _blinkEpoch;
    private const long BlinkOnMs  = 530;
    private const long BlinkOffMs = 530;

    // ── Undo (single level) ───────────────────────────────────────────────────
    private string? _undoText;
    private int     _undoCursor;
    private bool    _undoDirty;

    // ── Layout cache (line break positions) ───────────────────────────────────
    private struct LineInfo { public int Start; public int End; public float Width; }
    private LineInfo[] _lines     = Array.Empty<LineInfo>();
    private int        _lineCount;
    private float      _lineH;           // cached line height
    private float      _textColW;        // text column width (inner - scrollbar)

    // ─────────────────────────────────────────────────────────────────────────
    // Font helper
    // ─────────────────────────────────────────────────────────────────────────
    private void ApplyFont(Renderer renderer)
    {
        var theme = ThemeManager.Current;
        renderer.SetFont(Font?.Name ?? theme.DefaultFont);
        renderer.SetFontSize(FontSize > 0 ? FontSize : theme.FontSize);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Draw
    // ─────────────────────────────────────────────────────────────────────────
    public override void Draw(Renderer renderer)
    {
        if (!Visible) return;

        var   theme = ThemeManager.Current;
        float w     = Bounds.Width, h = Bounds.Height;
        float cr    = theme.InputCornerRadius;
        var   bgCol = BackColor ?? theme.InputBackColor;

        // NanoGUI-style sunken container
        renderer.FillRoundedRect(new Rect(0, 0, w, h), cr, bgCol);
        var insetPaint = renderer.BoxGradient(
            new Rect(1, 2, w - 2, h - 2), cr, 4f,
            new UIColor(1f, 1f, 1f, 0.06f),
            new UIColor(0f, 0f, 0f, 0.15f));
        renderer.FillRoundedRectWithPaint(new Rect(0, 0, w, h), cr, insetPaint);
        renderer.StrokeRoundedRect(
            new Rect(0.5f, 0.5f, w - 1f, h - 1f),
            MathF.Max(cr - 0.5f, 0f), 1f,
            (IsEditable && IsFocused) ? theme.AccentColor : UIColor.Black.WithAlpha(0.188f));

        ApplyFont(renderer);
        var hAlign = Align switch
        {
            TextAlign.Center => TextHAlign.Center,
            TextAlign.Right  => TextHAlign.Right,
            _                => TextHAlign.Left,
        };
        renderer.SetTextAlign(hAlign, TextVAlign.Top);

        float sbW    = theme.ScrollBarWidth;
        var   inner  = new Rect(0, 0, w, h).Deflate(Padding);
        if (inner.Width <= 0 || inner.Height <= 0) return;

        float textW   = MathF.Max(10f, inner.Width - sbW);
        _textColW     = textW;
        var (_, measH) = renderer.MeasureTextBounds(inner.X, inner.Y, textW, _sb.ToString());

        // Get line height for caret / cursor navigation
        renderer.MeasureTextMetrics(out _, out _, out _lineH);
        if (_lineH <= 0) _lineH = 16f;

        bool  showSb    = measH > inner.Height;
        float maxScroll = MathF.Max(0f, measH - inner.Height);
        _scrollY        = Math.Clamp(_scrollY, 0f, maxScroll);

        // Build line cache for editing (needed for caret positioning)
        if (IsEditable)
        {
            RebuildLineCache(renderer, textW);
            ScrollCaretIntoView(inner.Height, measH);
        }

        renderer.Save();
        renderer.IntersectClip(new Rect(0, 0, w, h));

        string text = _sb.ToString();

        // Selection highlight (editable mode)
        if (IsEditable && IsFocused && _selStart >= 0 && _selStart != _cursor)
            DrawSelection(renderer, inner, text, textW);

        // Text
        renderer.DrawTextBox(inner.X, inner.Y - _scrollY, textW, text,
            ForeColor ?? theme.TextColor);

        // Blinking caret (editable mode)
        if (IsEditable && IsFocused && CaretVisible())
            DrawCaret(renderer, inner, text, textW);

        renderer.Restore();

        // Vertical scrollbar
        if (showSb)
        {
            float sbX = inner.X + textW;
            ScrollbarRenderer.DrawVertical(renderer, sbX, inner.Y, sbW, inner.Height,
                _scrollY, measH, inner.Height, _sbHovered);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Caret rendering
    // ─────────────────────────────────────────────────────────────────────────
    private void DrawCaret(Renderer renderer, Rect inner, string text, float textW)
    {
        var (line, col) = CursorToLineCol(_cursor);
        float y = inner.Y - _scrollY + line * _lineH;
        float x = inner.X;
        if (line < _lineCount)
        {
            var li = _lines[line];
            float ao = LineAlignOffset(li, textW, renderer, text);
            x += ao + VisualCaretX(renderer, text, li, col);
        }
        renderer.DrawLine(x, y, x, y + _lineH, 1.5f,
            ThemeManager.Current.AccentColor);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Selection rendering
    // ─────────────────────────────────────────────────────────────────────────
    private void DrawSelection(Renderer renderer, Rect inner, string text, float textW)
    {
        int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
        var (sLine, sCol) = CursorToLineCol(s);
        var (eLine, eCol) = CursorToLineCol(e);
        var selColor = ThemeManager.Current.SelectionColor;

        for (int li = sLine; li <= eLine && li < _lineCount; li++)
        {
            var info = _lines[li];
            int lineLen = info.End - info.Start;
            int c0 = (li == sLine) ? sCol : 0;
            int c1 = (li == eLine) ? eCol : lineLen;
            if (c0 >= c1 && li != eLine) c1 = lineLen; // full line

            string lineStr = text[info.Start..info.End];
            var (visual, v2l) = BidiHelper.ToVisualWithMap(lineStr);

            float lineY = inner.Y - _scrollY + li * _lineH;
            float ao = LineAlignOffset(info, textW, renderer, text);

            // Find contiguous visual ranges where the logical index is selected
            int rangeStart = -1;
            for (int v = 0; v <= visual.Length; v++)
            {
                bool selected = v < visual.Length && v2l[v] >= c0 && v2l[v] < c1;
                if (selected && rangeStart < 0)
                    rangeStart = v;
                else if (!selected && rangeStart >= 0)
                {
                    float x0 = inner.X + ao + renderer.MeasureTextRaw(visual[..rangeStart]);
                    float x1 = inner.X + ao + renderer.MeasureTextRaw(visual[..v]);
                    renderer.FillRect(new Rect(x0, lineY, x1 - x0, _lineH), selColor);
                    rangeStart = -1;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Focus
    // ─────────────────────────────────────────────────────────────────────────
    public override void OnFocusGained()
    {
        ResetBlink();
        sapp_show_keyboard(true);
    }

    public override void OnFocusLost()
    {
        _selStart      = -1;
        _mouseDragging = false;
        _undoDirty     = false;
        sapp_show_keyboard(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mouse
    // ─────────────────────────────────────────────────────────────────────────
    public override bool OnMouseEnter(MouseEvent e) { return true; }
    public override bool OnMouseLeave(MouseEvent e) { _sbHovered = false; return true; }

    public override bool OnMouseScroll(MouseEvent e)
    {
        float spd = ThemeManager.Current.ScrollSpeed;
        _scrollY  = MathF.Max(0f, _scrollY - e.Scroll.Y * spd);
        return true;
    }

    public override bool OnMouseDown(MouseEvent e)
    {
        if (e.Button != MouseButton.Left) return false;

        var   theme = ThemeManager.Current;
        float sbW   = theme.ScrollBarWidth;
        var   inner = new Rect(0, 0, Bounds.Width, Bounds.Height).Deflate(Padding);
        float textW = MathF.Max(10f, inner.Width - sbW);
        float sbX   = inner.X + textW;

        // Scrollbar drag
        if (e.LocalPosition.X >= sbX)
        {
            _sbDragging        = true;
            _sbDragStartY      = e.Position.Y;
            _sbDragStartScroll = _scrollY;
            return true;
        }

        // Editable: click to position caret
        if (IsEditable)
        {
            int idx = HitTestCursor(e.LocalPosition, inner, textW);
            if (e.Clicks >= 3)
            {
                // Triple-click → select all
                _selStart = 0;
                _cursor   = _sb.Length;
            }
            else if (e.Clicks == 2)
            {
                SelectWordAt(idx);
            }
            else
            {
                bool shift = (e.Modifiers & KeyModifiers.Shift) != 0;
                if (shift && _selStart < 0) _selStart = _cursor;
                else if (!shift)            _selStart = -1;
                _cursor        = idx;
                _mouseDragging = true;
            }
            ResetBlink();
            return true;
        }

        return false;
    }

    public override bool OnMouseMove(MouseEvent e)
    {
        var thm  = ThemeManager.Current;
        float sbW2  = thm.ScrollBarWidth;
        var inner2  = new Rect(0, 0, Bounds.Width, Bounds.Height).Deflate(Padding);
        float textW2 = MathF.Max(10f, inner2.Width - sbW2);
        _sbHovered   = e.LocalPosition.X >= inner2.X + textW2;

        if (_sbDragging)
        {
            var renderer = Screen.Instance?.Renderer;
            if (renderer == null) return true;
            ApplyFont(renderer);
            var   inner    = new Rect(0, 0, Bounds.Width, Bounds.Height).Deflate(Padding);
            float textW    = MathF.Max(10f, inner.Width - thm.ScrollBarWidth);
            var (_, measH) = renderer.MeasureTextBounds(inner.X, inner.Y, textW, _sb.ToString());
            float maxScroll  = MathF.Max(0f, measH - inner.Height);
            float thumbH     = MathF.Max(16f, inner.Height * inner.Height / measH);
            float trackRange = inner.Height - thumbH;
            if (trackRange > 0)
            {
                float delta = e.Position.Y - _sbDragStartY;
                _scrollY = Math.Clamp(_sbDragStartScroll + delta * maxScroll / trackRange, 0f, maxScroll);
            }
            return true;
        }

        if (IsEditable && _mouseDragging)
        {
            int idx = HitTestCursor(e.LocalPosition, inner2, textW2);
            if (_selStart < 0) _selStart = _cursor;
            _cursor = idx;
            ResetBlink();
            return true;
        }

        return false;
    }

    public override bool OnMouseUp(MouseEvent e)
    {
        if (_sbDragging) { _sbDragging = false; return true; }
        _mouseDragging = false;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyboard – character input
    // ─────────────────────────────────────────────────────────────────────────
    public override bool OnTextInput(KeyEvent e)
    {
        if (!IsEditable || !Enabled) return false;
        if ((e.Modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0) return true;
        if (e.CharCode < 32 || e.CharCode == 127) return false;

        char ch = (char)e.CharCode;
        SaveUndo();
        DeleteSelection();
        _sb.Insert(_cursor, ch);
        _cursor++;
        TextChanged?.Invoke(Text);
        ResetBlink();
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Keyboard – control keys
    // ─────────────────────────────────────────────────────────────────────────
    public override bool OnKeyDown(KeyEvent e)
    {
        if (!IsEditable || !Enabled) return false;

        const int KEY_BACKSPACE = 259;
        const int KEY_DELETE    = 261;
        const int KEY_LEFT      = 263;
        const int KEY_RIGHT     = 262;
        const int KEY_UP        = 265;
        const int KEY_DOWN      = 264;
        const int KEY_HOME      = 268;
        const int KEY_END       = 269;
        const int KEY_ENTER     = 257;
        const int KEY_KP_ENTER  = 335;

        bool shift = (e.Modifiers & KeyModifiers.Shift)   != 0;
        bool ctrl  = (e.Modifiers & KeyModifiers.Control) != 0;
        bool cmd   = (e.Modifiers & KeyModifiers.Super)   != 0;
        bool accel = ctrl || cmd;

        switch (e.KeyCode)
        {
            case KEY_LEFT:
                if (shift && _selStart < 0) _selStart = _cursor;
                else if (!shift) _selStart = -1;
                _cursor = accel ? WordStartBefore(_cursor) : Math.Max(0, _cursor - 1);
                ResetBlink(); return true;

            case KEY_RIGHT:
                if (shift && _selStart < 0) _selStart = _cursor;
                else if (!shift) _selStart = -1;
                _cursor = accel ? WordEndAfter(_cursor) : Math.Min(_sb.Length, _cursor + 1);
                ResetBlink(); return true;

            case KEY_UP:
                if (shift && _selStart < 0) _selStart = _cursor;
                else if (!shift) _selStart = -1;
                MoveCursorVertically(-1);
                ResetBlink(); return true;

            case KEY_DOWN:
                if (shift && _selStart < 0) _selStart = _cursor;
                else if (!shift) _selStart = -1;
                MoveCursorVertically(1);
                ResetBlink(); return true;

            case KEY_HOME:
                if (shift && _selStart < 0) _selStart = _cursor; else if (!shift) _selStart = -1;
                { var (ln, _) = CursorToLineCol(_cursor); _cursor = ln < _lineCount ? _lines[ln].Start : 0; }
                ResetBlink(); return true;

            case KEY_END:
                if (shift && _selStart < 0) _selStart = _cursor; else if (!shift) _selStart = -1;
                { var (ln, _) = CursorToLineCol(_cursor); _cursor = ln < _lineCount ? _lines[ln].End : _sb.Length; }
                ResetBlink(); return true;

            case KEY_ENTER:
            case KEY_KP_ENTER:
                SaveUndo();
                DeleteSelection();
                _sb.Insert(_cursor, '\n');
                _cursor++;
                TextChanged?.Invoke(Text);
                ResetBlink(); return true;

            case KEY_BACKSPACE:
                if (_selStart >= 0) { SaveUndo(); DeleteSelection(); }
                else if (accel && _cursor > 0)
                {
                    SaveUndo();
                    int start = WordStartBefore(_cursor);
                    _sb.Remove(start, _cursor - start);
                    _cursor = start;
                    TextChanged?.Invoke(Text);
                }
                else if (_cursor > 0)
                {
                    SaveUndo();
                    _sb.Remove(_cursor - 1, 1);
                    _cursor--;
                    TextChanged?.Invoke(Text);
                }
                ResetBlink(); return true;

            case KEY_DELETE:
                if (_selStart >= 0) { SaveUndo(); DeleteSelection(); }
                else if (accel && _cursor < _sb.Length)
                {
                    SaveUndo();
                    int end = WordEndAfter(_cursor);
                    _sb.Remove(_cursor, end - _cursor);
                    TextChanged?.Invoke(Text);
                }
                else if (_cursor < _sb.Length)
                {
                    SaveUndo();
                    _sb.Remove(_cursor, 1);
                    TextChanged?.Invoke(Text);
                }
                ResetBlink(); return true;
        }

        // Accelerator shortcuts
        if (accel)
        {
            const int KEY_A = 65, KEY_C = 67, KEY_V = 86, KEY_X = 88, KEY_Z = 90;
            switch (e.KeyCode)
            {
                case KEY_A:
                    _selStart = 0; _cursor = _sb.Length; ResetBlink(); return true;
                case KEY_C:
                    CopyToClipboard(); return true;
                case KEY_X:
                    SaveUndo(); CopyToClipboard(); DeleteSelection();
                    ResetBlink(); return true;
                case KEY_V:
                    PasteFromClipboard(); ResetBlink(); return true;
                case KEY_Z:
                    PerformUndo(); ResetBlink(); return true;
            }
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Editing helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void DeleteSelection()
    {
        if (_selStart < 0) return;
        int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
        _sb.Remove(s, e - s);
        _cursor   = s;
        _selStart = -1;
        TextChanged?.Invoke(Text);
    }

    private void SaveUndo()
    {
        if (_undoDirty) return;
        _undoText   = _sb.ToString();
        _undoCursor = _cursor;
        _undoDirty  = true;
    }

    private void PerformUndo()
    {
        if (_undoText == null) return;
        _sb.Clear(); _sb.Append(_undoText);
        _cursor    = Math.Min(_undoCursor, _sb.Length);
        _selStart  = -1;
        _undoText  = null;
        _undoDirty = false;
        TextChanged?.Invoke(Text);
    }

    private void CopyToClipboard()
    {
        if (_selStart < 0 || _selStart == _cursor) return;
        int s = Math.Min(_selStart, _cursor), e = Math.Max(_selStart, _cursor);
        try { sapp_set_clipboard_string(_sb.ToString()[s..e]); } catch { }
    }

    private void PasteFromClipboard()
    {
        string paste;
        try { paste = sapp_get_clipboard_string(); } catch { return; }
        if (string.IsNullOrEmpty(paste)) return;

        SaveUndo();
        DeleteSelection();
        foreach (char ch in paste)
        {
            if (ch == '\n' || ch == '\r')
            {
                // Accept newlines in multiline
                if (ch == '\r') continue; // skip \r in \r\n
                _sb.Insert(_cursor, '\n');
                _cursor++;
                continue;
            }
            if (ch < 32 || ch == 127) continue;
            _sb.Insert(_cursor, ch);
            _cursor++;
        }
        TextChanged?.Invoke(Text);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Word navigation
    // ─────────────────────────────────────────────────────────────────────────
    private int WordStartBefore(int pos)
    {
        if (pos == 0) return 0;
        int p = pos - 1;
        while (p > 0 && !IsWordChar(_sb[p - 1])) p--;
        while (p > 0 &&  IsWordChar(_sb[p - 1])) p--;
        return p;
    }

    private int WordEndAfter(int pos)
    {
        if (pos >= _sb.Length) return _sb.Length;
        int p = pos;
        while (p < _sb.Length && !IsWordChar(_sb[p])) p++;
        while (p < _sb.Length &&  IsWordChar(_sb[p])) p++;
        return p;
    }

    private void SelectWordAt(int idx)
    {
        if (_sb.Length == 0) return;
        int clamp = Math.Clamp(idx, 0, _sb.Length - 1);
        if (IsWordChar(_sb[clamp]))
        {
            int s = clamp, e = clamp;
            while (s > 0          && IsWordChar(_sb[s - 1])) s--;
            while (e < _sb.Length  && IsWordChar(_sb[e]))     e++;
            _selStart = s; _cursor = e;
        }
        else
        {
            _selStart = clamp; _cursor = clamp + 1;
        }
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    // ─────────────────────────────────────────────────────────────────────────
    // Line cache — splits text into visual lines (by \n and word-wrap width)
    // ─────────────────────────────────────────────────────────────────────────
    private void RebuildLineCache(Renderer renderer, float maxW)
    {
        string text = _sb.ToString();
        // Estimate max lines
        int estimated = Math.Max(16, text.Length / 10 + 4);
        if (_lines.Length < estimated)
            _lines = new LineInfo[estimated];

        _lineCount = 0;

        if (text.Length == 0)
        {
            AddLine(0, 0, 0f);
            return;
        }

        int pos = 0;
        while (pos <= text.Length)
        {
            // Find next hard newline or end
            int nlIdx = text.IndexOf('\n', pos);
            int lineEnd = nlIdx >= 0 ? nlIdx : text.Length;
            string lineStr = text[pos..lineEnd];

            if (lineStr.Length == 0 || maxW <= 0)
            {
                AddLine(pos, lineEnd, 0f);
            }
            else
            {
                // Wrap the line by measuring prefixes
                int wrapStart = pos;
                while (wrapStart < lineEnd)
                {
                    string remaining = text[wrapStart..lineEnd];
                    float fullW = renderer.MeasureTextRaw(remaining);
                    if (fullW <= maxW)
                    {
                        AddLine(wrapStart, lineEnd, fullW);
                        break;
                    }
                    // Binary search for the longest prefix that fits
                    int lo = 1, hi = remaining.Length;
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        float mw = renderer.MeasureTextRaw(remaining[..mid]);
                        if (mw <= maxW) lo = mid; else hi = mid - 1;
                    }
                    // Try to break at a word boundary
                    int breakAt = lo;
                    for (int b = lo; b > 0; b--)
                    {
                        if (remaining[b - 1] == ' ' || remaining[b - 1] == '\t')
                        { breakAt = b; break; }
                    }
                    if (breakAt <= 0) breakAt = Math.Max(1, lo);
                    int wrapEnd = wrapStart + breakAt;
                    float w = renderer.MeasureTextRaw(text[wrapStart..wrapEnd]);
                    AddLine(wrapStart, wrapEnd, w);
                    wrapStart = wrapEnd;
                    // Skip leading spaces on next wrapped segment
                    while (wrapStart < lineEnd && text[wrapStart] == ' ') wrapStart++;
                }
            }

            pos = lineEnd + 1; // skip past \n
            if (nlIdx < 0) break;
        }
    }

    private void AddLine(int start, int end, float width)
    {
        if (_lineCount >= _lines.Length)
            Array.Resize(ref _lines, _lines.Length * 2);
        _lines[_lineCount++] = new LineInfo { Start = start, End = end, Width = width };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cursor ↔ line/col conversion
    // ─────────────────────────────────────────────────────────────────────────
    private (int line, int col) CursorToLineCol(int cursor)
    {
        for (int i = 0; i < _lineCount; i++)
        {
            var li = _lines[i];
            // Cursor is within this line (or at the end of the last line)
            if (cursor >= li.Start && (cursor < li.End || (cursor == li.End && i == _lineCount - 1)))
                return (i, cursor - li.Start);
            // Cursor is exactly at li.End and next line starts later (hard newline boundary)
            if (cursor == li.End && i + 1 < _lineCount && _lines[i + 1].Start > li.End)
                return (i, cursor - li.Start);
        }
        // Past end — last line
        if (_lineCount > 0)
            return (_lineCount - 1, _lines[_lineCount - 1].End - _lines[_lineCount - 1].Start);
        return (0, 0);
    }

    private int LineColToCursor(int line, int col)
    {
        if (line < 0) line = 0;
        if (line >= _lineCount) line = _lineCount - 1;
        if (_lineCount == 0) return 0;
        var li = _lines[line];
        return Math.Clamp(li.Start + col, li.Start, li.End);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Vertical cursor movement (Up/Down)
    // ─────────────────────────────────────────────────────────────────────────
    private void MoveCursorVertically(int delta)
    {
        var (line, col) = CursorToLineCol(_cursor);
        int targetLine = Math.Clamp(line + delta, 0, Math.Max(0, _lineCount - 1));
        if (targetLine == line) return;

        // Try to preserve column offset
        var targetLi = _lines[targetLine];
        int targetLen = targetLi.End - targetLi.Start;
        _cursor = LineColToCursor(targetLine, Math.Min(col, targetLen));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hit-test: local mouse position → cursor index
    // ─────────────────────────────────────────────────────────────────────────
    private int HitTestCursor(Vector2 localPos, Rect inner, float textW)
    {
        if (_lineCount == 0) return 0;

        float relY = localPos.Y - inner.Y + _scrollY;
        int line = Math.Clamp((int)(relY / _lineH), 0, _lineCount - 1);
        var li = _lines[line];

        string text = _sb.ToString();
        string lineStr = text[li.Start..li.End];

        var renderer = Screen.Instance?.Renderer;
        if (renderer == null) return li.Start;
        ApplyFont(renderer);

        float ao = LineAlignOffset(li, textW, renderer, text);
        float relX = localPos.X - inner.X - ao;
        if (lineStr.Length == 0) return li.Start;

        var (visual, v2l) = BidiHelper.ToVisualWithMap(lineStr);

        // Walk visual characters to find the clicked boundary
        int visualIdx = visual.Length;
        for (int i = 0; i < visual.Length; i++)
        {
            float w0 = renderer.MeasureTextRaw(visual[..i]);
            float w1 = renderer.MeasureTextRaw(visual[..(i + 1)]);
            float mid = (w0 + w1) * 0.5f;
            if (relX <= mid) { visualIdx = i; break; }
        }

        // Map visual boundary to logical cursor position
        int cursor;
        if (visualIdx == 0)
        {
            // Before/on first visual char
            cursor = BidiHelper.IsRtlChar(visual[0]) ? v2l[0] + 1 : v2l[0];
        }
        else if (visualIdx >= visual.Length)
        {
            // After last visual char
            cursor = BidiHelper.IsRtlChar(visual[^1]) ? v2l[^1] : v2l[^1] + 1;
        }
        else
        {
            // Boundary between visual[visualIdx-1] and visual[visualIdx]
            char leftChar = visual[visualIdx - 1];
            cursor = BidiHelper.IsRtlChar(leftChar) ? v2l[visualIdx - 1] : v2l[visualIdx - 1] + 1;
        }

        return li.Start + cursor;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scroll caret into view
    // ─────────────────────────────────────────────────────────────────────────
    private void ScrollCaretIntoView(float viewH, float contentH)
    {
        var (line, _) = CursorToLineCol(_cursor);
        float caretTop    = line * _lineH;
        float caretBottom = caretTop + _lineH;
        float maxScroll   = MathF.Max(0f, contentH - viewH);

        if (caretTop < _scrollY)
            _scrollY = MathF.Max(0f, caretTop);
        else if (caretBottom > _scrollY + viewH)
            _scrollY = MathF.Min(maxScroll, caretBottom - viewH);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Blink
    // ─────────────────────────────────────────────────────────────────────────
    private void ResetBlink() => _blinkEpoch = Environment.TickCount64;
    private bool CaretVisible()
    {
        long elapsed = Environment.TickCount64 - _blinkEpoch;
        return (elapsed % (BlinkOnMs + BlinkOffMs)) < BlinkOnMs;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alignment offset per visual line
    // ─────────────────────────────────────────────────────────────────────────
    private float LineAlignOffset(LineInfo li, float textW, Renderer renderer, string text)
    {
        if (Align == TextAlign.Left) return 0f;
        float lineW = li.Width > 0 ? li.Width : renderer.MeasureTextRaw(text[li.Start..li.End]);
        return Align switch
        {
            TextAlign.Center => MathF.Max(0f, (textW - lineW) * 0.5f),
            TextAlign.Right  => MathF.Max(0f, textW - lineW),
            _                => 0f,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BiDi-aware caret X offset within a visual line
    // ─────────────────────────────────────────────────────────────────────────
    private float VisualCaretX(Renderer renderer, string text, LineInfo li, int col)
    {
        string lineStr = text[li.Start..li.End];
        int len = lineStr.Length;
        if (len == 0) return 0f;

        var (visual, v2l) = BidiHelper.ToVisualWithMap(lineStr);
        if (visual.Length == 0) return 0f;

        // Build inverse map: logicalToVisual[logicalIdx] = visualIdx
        int[] l2v = new int[len];
        for (int i = 0; i < v2l.Length; i++)
            l2v[v2l[i]] = i;

        col = Math.Clamp(col, 0, len);

        // Determine the visual boundary position for the caret.
        // For a char to the left of the caret (col-1 in logical order):
        //   LTR char → caret is at the RIGHT edge of its visual position (v+1)
        //   RTL char → caret is at the LEFT  edge of its visual position (v)
        int visualPos;
        if (col == 0)
        {
            int v = l2v[0];
            visualPos = BidiHelper.IsRtlChar(lineStr[0]) ? v + 1 : v;
        }
        else if (col >= len)
        {
            int v = l2v[len - 1];
            visualPos = BidiHelper.IsRtlChar(lineStr[len - 1]) ? v : v + 1;
        }
        else
        {
            int v = l2v[col - 1];
            visualPos = BidiHelper.IsRtlChar(lineStr[col - 1]) ? v : v + 1;
        }

        return renderer.MeasureTextRaw(visual[..visualPos]);
    }
}
