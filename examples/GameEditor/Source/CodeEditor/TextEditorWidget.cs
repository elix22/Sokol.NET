// TextEditorWidget.cs — Pure C# / ImDrawList text editor widget.
//
// Usage:
//   var editor = new TextEditorWidget();
//   editor.SetText(File.ReadAllText(path));
//   // each frame:
//   editor.Render("##myEditor", availableSize);
//
// All rendering goes through ImDrawList — no ImGui InputText.
// All input is handled manually from igGetIO / igIsKeyPressed.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Imgui;
using static Imgui.ImguiNative;

namespace GameEditor.CodeEditor
{
    public sealed class TextEditorWidget
    {
        // ── Glyph ─────────────────────────────────────────────────────────────
        private struct Glyph
        {
            public char          Char;
            public PaletteIndex  Color;
            public Glyph(char c, PaletteIndex col) { Char = c; Color = col; }
        }

        // Lines: each line is a List<Glyph> (no newline glyph stored)
        private readonly List<List<Glyph>> _lines = new();

        // ── State ─────────────────────────────────────────────────────────────
        private Coords _cursor;
        private Coords _selStart;    // equal to _selEnd when no selection
        private Coords _selEnd;
        private bool   _selecting;

        private readonly UndoBuffer         _undo  = new();
        private readonly SyntaxHighlighter  _highlighter;
        private SyntaxToken[][]?            _tokens;

        private Palette _palette = Palette.Dark;

        // Scroll (in pixels)
        private float _scrollX;
        private float _scrollY;

        // Cached character dimensions (monospace)
        private float _charW;
        private float _charH;

        // Gutter width in pixels (computed once per frame)
        private float _gutterW;

        // Used to detect when the text changed and a highlight pass is needed
        private int  _textVersion;
        private int  _highlightVersion = -1;

        public int TabSize { get; set; } = 4;
        public bool ReadOnly { get; set; } = false;
        public bool ShowLineNumbers { get; set; } = true;

        // ── Find bar ──────────────────────────────────────────────────────────
        private bool   _findBarVisible;
        private bool   _findJustOpened;
        private readonly byte[] _findBuf = new byte[256];
        private string  _findQuery = "";
        private int     _findMatchIdx = -1;
        private readonly List<Coords> _findMatches = new();
        private int     _findTextVersion = -2; // ensures first rebuild

        // ── Go to line dialog ─────────────────────────────────────────────────
        private bool   _gotoLineVisible;
        private bool   _gotoLineJustOpened;
        private readonly byte[] _gotoLineBuf = new byte[16];

        // ── Symbol search results (references / definition) ───────────────────
        private bool   _symResultsOpen;
        private string  _symResultsTitle = "";
        private readonly List<(int Line, int ColStart, int ColEnd)> _symResults = new();

        public Palette Palette
        {
            get => _palette;
            set => _palette = value;
        }

        // Set by external caller (e.g. build error parser)
        public void SetErrorMarkers(Dictionary<int, string> markers)
            => _highlighter.SetErrorMarkers(markers);
        public void SetWarningMarkers(Dictionary<int, string> markers)
            => _highlighter.SetWarningMarkers(markers);

        // ── Construction ─────────────────────────────────────────────────────
        public TextEditorWidget()
        {
            _highlighter = new SyntaxHighlighter(LanguageDefinition.CSharp);
            _lines.Add(new List<Glyph>());  // start with one empty line
        }

        // ── Text access ──────────────────────────────────────────────────────
        public void SetText(string text)
        {
            _lines.Clear();
            _undo.Clear();
            _cursor = _selStart = _selEnd = Coords.Zero;

            var current = new List<Glyph>();
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n')
                {
                    _lines.Add(current);
                    current = new List<Glyph>();
                }
                else
                {
                    current.Add(new Glyph(c, PaletteIndex.Default));
                }
            }
            _lines.Add(current);

            _textVersion++;
        }

        public string GetText()
        {
            var sb = new StringBuilder();
            for (int li = 0; li < _lines.Count; li++)
            {
                if (li > 0) sb.Append('\n');
                foreach (var g in _lines[li])
                    sb.Append(g.Char);
            }
            return sb.ToString();
        }

        /// <summary>Returns the number of lines in the document.</summary>
        public int LineCount => _lines.Count;

        private string GetLineText(int line)
        {
            if (line < 0 || line >= _lines.Count) return "";
            var sb = new StringBuilder(_lines[line].Count);
            foreach (var g in _lines[line]) sb.Append(g.Char);
            return sb.ToString();
        }

        // ── Comment / uncomment ───────────────────────────────────────────────
        private void ToggleLineComment()
        {
            int startLine = HasSelection() ? Math.Min(_selStart.Line, _selEnd.Line) : _cursor.Line;
            int endLine   = HasSelection() ? Math.Max(_selStart.Line, _selEnd.Line) : _cursor.Line;

            // VS Code style: if the selection ends at column 0, don't include that line
            if (HasSelection() && (_selStart <= _selEnd ? _selEnd : _selStart).Column == 0 && endLine > startLine)
                endLine--;

            // All-commented → remove comment prefix; otherwise add it
            bool allCommented = true;
            for (int li = startLine; li <= endLine; li++)
            {
                string trimmed = GetLineText(li).TrimStart();
                if (!trimmed.StartsWith("//")) { allCommented = false; break; }
            }

            for (int li = startLine; li <= endLine; li++)
            {
                var ln = _lines[li];
                if (allCommented)
                {
                    // Find first "//" and remove it
                    for (int ci = 0; ci < ln.Count - 1; ci++)
                    {
                        if (ln[ci].Char == '/' && ln[ci + 1].Char == '/')
                        {
                            ln.RemoveAt(ci + 1);
                            ln.RemoveAt(ci);
                            // Remove one space after // if present
                            if (ci < ln.Count && ln[ci].Char == ' ')
                                ln.RemoveAt(ci);
                            break;
                        }
                    }
                }
                else
                {
                    // Find indent level, insert "// " there
                    int indent = 0;
                    while (indent < ln.Count && (ln[indent].Char == ' ' || ln[indent].Char == '\t'))
                        indent++;
                    ln.Insert(indent, new Glyph(' ', PaletteIndex.Default));
                    ln.Insert(indent, new Glyph('/', PaletteIndex.Default));
                    ln.Insert(indent, new Glyph('/', PaletteIndex.Default));
                }
            }

            _textVersion++;
        }

        // ── Duplicate current line / selection ────────────────────────────────
        private void DuplicateCurrentLine()
        {
            if (HasSelection())
            {
                // Duplicate selected text and place it after the selection
                string selText = GetSelectedText();
                var selMax = _selStart <= _selEnd ? _selEnd : _selStart;
                _cursor = selMax;
                InsertText(selText);
            }
            else
            {
                // Duplicate whole line below cursor
                var ln = _lines[_cursor.Line];
                var newLine = new List<Glyph>(ln);
                _lines.Insert(_cursor.Line + 1, newLine);
                _cursor = new Coords(_cursor.Line + 1, _cursor.Column);
                _selStart = _selEnd = _cursor;
                _textVersion++;
            }
        }

        // ── Move line up ──────────────────────────────────────────────────────
        private void MoveLineUp()
        {
            if (_cursor.Line == 0) return;
            var tmp = _lines[_cursor.Line - 1];
            _lines[_cursor.Line - 1] = _lines[_cursor.Line];
            _lines[_cursor.Line]     = tmp;
            _cursor.Line--;
            _selStart = _selEnd = _cursor;
            _textVersion++;
        }

        // ── Move line down ────────────────────────────────────────────────────
        private void MoveLineDown()
        {
            if (_cursor.Line >= _lines.Count - 1) return;
            var tmp = _lines[_cursor.Line + 1];
            _lines[_cursor.Line + 1] = _lines[_cursor.Line];
            _lines[_cursor.Line]     = tmp;
            _cursor.Line++;
            _selStart = _selEnd = _cursor;
            _textVersion++;
        }

        // ── Find helpers ──────────────────────────────────────────────────────

        /// Rebuild _findMatches for the current _findQuery.
        private void RebuildFindMatches()
        {
            _findMatches.Clear();
            _findMatchIdx = -1;
            if (string.IsNullOrEmpty(_findQuery)) return;

            for (int li = 0; li < _lines.Count; li++)
            {
                string lineText = GetLineText(li);
                int idx = 0;
                while ((idx = lineText.IndexOf(_findQuery, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    _findMatches.Add(new Coords(li, idx));
                    idx += _findQuery.Length;
                }
            }

            // Jump to the first match at or after cursor if possible
            _findMatchIdx = -1;
            for (int i = 0; i < _findMatches.Count; i++)
            {
                if (_findMatches[i].Line > _cursor.Line ||
                    (_findMatches[i].Line == _cursor.Line && _findMatches[i].Column >= _cursor.Column))
                {
                    _findMatchIdx = i;
                    break;
                }
            }
            if (_findMatchIdx == -1 && _findMatches.Count > 0)
                _findMatchIdx = 0;
        }

        private void NavigateToMatch(int delta)
        {
            if (_findMatches.Count == 0) return;
            _findMatchIdx = ((_findMatchIdx + delta) % _findMatches.Count + _findMatches.Count) % _findMatches.Count;
            var m = _findMatches[_findMatchIdx];
            _cursor   = new Coords(m.Line, m.Column + _findQuery.Length);
            _selStart = new Coords(m.Line, m.Column);
            _selEnd   = _cursor;
            // Scroll to match
            float cursorPixelY = _cursor.Line * _charH;
            if (cursorPixelY < _scrollY || cursorPixelY + _charH > _scrollY + 400f) // approx
                _scrollY = MathF.Max(0f, cursorPixelY - 100f);
        }

        // ── Word under cursor ─────────────────────────────────────────────────
        private string GetWordUnderCursor()
        {
            if (_cursor.Line < 0 || _cursor.Line >= _lines.Count) return "";
            var ln = _lines[_cursor.Line];
            if (ln.Count == 0) return "";

            int col = Math.Clamp(_cursor.Column, 0, ln.Count - 1);
            if (col < ln.Count && !IsWordChar(ln[col].Char) && col > 0) col--;
            if (!IsWordChar(ln[col].Char)) return "";

            int start = col;
            while (start > 0 && IsWordChar(ln[start - 1].Char)) start--;
            int end = col;
            while (end < ln.Count && IsWordChar(ln[end].Char)) end++;

            var sb = new StringBuilder(end - start);
            for (int ci = start; ci < end; ci++) sb.Append(ln[ci].Char);
            return sb.ToString();
        }

        // ── Find all references ───────────────────────────────────────────────
        private void FindAllReferences()
        {
            string word = GetWordUnderCursor();
            if (string.IsNullOrEmpty(word)) return;

            _symResults.Clear();
            _symResultsTitle = $"References to '{word}'";

            for (int li = 0; li < _lines.Count; li++)
            {
                string lineText = GetLineText(li);
                int idx = 0;
                while ((idx = lineText.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
                {
                    // Only match whole words
                    bool beforeOk = idx == 0 || !IsWordChar(lineText[idx - 1]);
                    bool afterOk  = (idx + word.Length >= lineText.Length) ||
                                   !IsWordChar(lineText[idx + word.Length]);
                    if (beforeOk && afterOk)
                        _symResults.Add((li, idx, idx + word.Length));
                    idx++;
                }
            }

            _symResultsOpen = _symResults.Count > 0;
        }

        // ── Go to definition ──────────────────────────────────────────────────
        // Text-based: looks for 'class/interface/struct/record/enum/void/etc. Word'
        private static readonly string[] DefinitionKeywords =
            { "class ", "interface ", "struct ", "record ", "enum ", "delegate ",
              "void ", "bool ", "int ", "float ", "double ", "string ", "var ",
              "public ", "private ", "protected ", "internal ", "static ",
              "override ", "virtual ", "abstract ", "async " };

        private void GotoDefinition()
        {
            string word = GetWordUnderCursor();
            if (string.IsNullOrEmpty(word)) return;

            _symResults.Clear();
            _symResultsTitle = $"Definition of '{word}'";

            for (int li = 0; li < _lines.Count; li++)
            {
                string lineText = GetLineText(li);
                // Check if this line has a declaration of 'word'
                // Heuristic: the word appears and a definition keyword precedes it on the same line
                int idx = lineText.IndexOf(word, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    bool beforeOk = idx == 0 || !IsWordChar(lineText[idx - 1]);
                    bool afterOk  = (idx + word.Length >= lineText.Length) ||
                                   !IsWordChar(lineText[idx + word.Length]);
                    if (beforeOk && afterOk)
                    {
                        string before = lineText.Substring(0, idx);
                        bool hasDefKw = false;
                        foreach (var kw in DefinitionKeywords)
                            if (before.Contains(kw)) { hasDefKw = true; break; }

                        if (hasDefKw)
                            _symResults.Add((li, idx, idx + word.Length));
                    }
                    idx = lineText.IndexOf(word, idx + 1, StringComparison.Ordinal);
                }
            }

            if (_symResults.Count == 1)
            {
                // Jump directly if exactly one result
                var r = _symResults[0];
                _cursor   = new Coords(r.Line, r.ColEnd);
                _selStart = new Coords(r.Line, r.ColStart);
                _selEnd   = _cursor;
                _scrollY  = MathF.Max(0f, r.Line * _charH - 100f);
                _symResultsOpen = false;
            }
            else
            {
                _symResultsOpen = _symResults.Count > 0;
            }
        }

        private void JumpToLine(int line)
        {
            line = Math.Clamp(line, 0, _lines.Count - 1);
            _cursor = new Coords(line, Math.Min(_cursor.Column, _lines[line].Count));
            _selStart = _selEnd = _cursor;
            _scrollY = MathF.Max(0f, line * _charH - 100f);
        }


        public unsafe void Render(string id, Vector2 size)
        {
            // Kick off a highlight pass if the text changed
            if (_textVersion != _highlightVersion)
            {
                _highlightVersion = _textVersion;
                string[] snap = new string[_lines.Count];
                for (int i = 0; i < _lines.Count; i++)
                {
                    var sb = new StringBuilder(_lines[i].Count);
                    foreach (var g in _lines[i]) sb.Append(g.Char);
                    snap[i] = sb.ToString();
                }
                _highlighter.RequestHighlight(snap);
            }

            // Grab latest token data from the highlighter (may be null on first frame)
            _tokens = _highlighter.GetResult();

            // Reserve space for the find bar strip when visible
            const float findBarH = 28f;
            float contentH = _findBarVisible ? MathF.Max(4f, size.Y - findBarH) : size.Y;

            var windowFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            igBeginChild_Str(id, new Vector2(size.X, contentH), ImGuiChildFlags.None, windowFlags);

            // Push JetBrains Mono if available — must happen inside the child so
            // subsequent igCalcTextSize uses the correct font.
            bool pushedFont = false;
            var cf = GameEditor.EditorFonts.CodeFont;
            if (cf != null)
            {
                igPushFont(cf, 0f); // 0f = use font's built-in size
                pushedFont = true;
            }

            // Measure one character (uses whichever font is now active)
            Vector2 charSz = default;
            igCalcTextSize(ref charSz, "X", null, false, 0f);
            _charW = charSz.X;
            _charH = igGetTextLineHeightWithSpacing();

            // Gutter width: enough for the line-number digits + padding
            if (ShowLineNumbers)
            {
                string gutterSample = _lines.Count.ToString().PadLeft(4);
                Vector2 gutterSz = default;
                igCalcTextSize(ref gutterSz, gutterSample, null, false, 0f);
                _gutterW = gutterSz.X + 16f;
            }
            else
            {
                _gutterW = 4f;
            }

            bool focused = igIsWindowFocused(ImGuiFocusedFlags.None);

            if (focused && !ReadOnly)
                HandleKeyboardInput();

            HandleMouseInput();

            // Capture editor screen-space origin before RenderContent overwrites the cursor
            Vector2 editorScreenPos = default;
            igGetWindowPos(ref editorScreenPos);

            RenderContent();

            // Context menu (right-click within the child window)
            RenderContextMenu();

            if (pushedFont) igPopFont();
            igEndChild();

            // ── Find bar strip (placed immediately below the content child) ───
            if (_findBarVisible)
                RenderFindBar(new Vector2(size.X, findBarH));

            // ── Go to line floating dialog ────────────────────────────────────
            RenderGotoLine();

            // ── Symbol results floating popup ─────────────────────────────────
            RenderSymbolResults(editorScreenPos);
        }

        // ── Rendering ────────────────────────────────────────────────────────
        private unsafe void RenderContent()
        {
            var drawList = igGetWindowDrawList();
            Vector2 winPos = default;
            igGetCursorScreenPos(ref winPos);
            Vector2 winSize = default;
            igGetContentRegionAvail(ref winSize);

            // Background
            ImDrawList_AddRectFilled(drawList,
                winPos,
                new Vector2(winPos.X + winSize.X, winPos.Y + winSize.Y),
                _palette[PaletteIndex.Background],
                0f, ImDrawFlags.None);

            // Clip to the window
            igPushClipRect(winPos, new Vector2(winPos.X + winSize.X, winPos.Y + winSize.Y), true);

            float contentLeft = winPos.X + _gutterW;

            // Determine visible line range
            int firstLine = (int)MathF.Floor(_scrollY / _charH);
            int lastLine  = (int)MathF.Ceiling((_scrollY + winSize.Y) / _charH);
            firstLine = Math.Clamp(firstLine, 0, _lines.Count - 1);
            lastLine  = Math.Clamp(lastLine,  0, _lines.Count - 1);

            Coords selMin = _selStart <= _selEnd ? _selStart : _selEnd;
            Coords selMax = _selStart <= _selEnd ? _selEnd   : _selStart;

            for (int li = firstLine; li <= lastLine; li++)
            {
                float lineY = winPos.Y + li * _charH - _scrollY;

                // ── Current-line highlight ───────────────────────────────────
                if (li == _cursor.Line)
                {
                    ImDrawList_AddRectFilled(drawList,
                        new Vector2(winPos.X, lineY),
                        new Vector2(winPos.X + winSize.X, lineY + _charH),
                        _palette[PaletteIndex.CurrentLine],
                        0f, ImDrawFlags.None);
                }

                // ── Error / warning marker bar on left ───────────────────────
                var errors   = _highlighter.ErrorMarkers;
                var warnings = _highlighter.WarningMarkers;
                if (errors.ContainsKey(li))
                {
                    ImDrawList_AddRectFilled(drawList,
                        new Vector2(winPos.X, lineY),
                        new Vector2(winPos.X + 3f, lineY + _charH),
                        0xFF2020FF, 0f, ImDrawFlags.None); // red bar
                }
                else if (warnings.ContainsKey(li))
                {
                    ImDrawList_AddRectFilled(drawList,
                        new Vector2(winPos.X, lineY),
                        new Vector2(winPos.X + 3f, lineY + _charH),
                        0xFF00AAFF, 0f, ImDrawFlags.None); // orange bar
                }

                // ── Gutter line number ────────────────────────────────────────
                if (ShowLineNumbers)
                {
                    string lineNum = (li + 1).ToString();
                    Vector2 numSz = default;
                    igCalcTextSize(ref numSz, lineNum, null, false, 0f);
                    float numX = winPos.X + _gutterW - numSz.X - 8f;
                    ImDrawList_AddText_Vec2(drawList,
                        new Vector2(numX, lineY),
                        _palette[PaletteIndex.LineNumber],
                        lineNum, null);
                }

                // ── Selection background ──────────────────────────────────────
                if (selMin != selMax)
                    DrawSelectionOnLine(drawList, li, lineY, contentLeft, winPos, winSize, selMin, selMax);

                // ── Find match highlights ─────────────────────────────────────
                if (_findMatches.Count > 0 && !string.IsNullOrEmpty(_findQuery))
                {
                    int matchLen = _findQuery.Length;
                    for (int mi = 0; mi < _findMatches.Count; mi++)
                    {
                        var m = _findMatches[mi];
                        if (m.Line != li) continue;
                        float mx0 = contentLeft + ColToPixel(li, m.Column) - _scrollX;
                        float mx1 = mx0 + matchLen * _charW;
                        // Current match: bright highlight; others: subtle
                        uint matchColor = (mi == _findMatchIdx)
                            ? 0xAA00A0FF   // orange-ish for active match
                            : 0x6600A0FF;  // dimmer yellow for other matches
                        ImDrawList_AddRectFilled(drawList,
                            new Vector2(mx0, lineY),
                            new Vector2(mx1, lineY + _charH),
                            matchColor,
                            2f, ImDrawFlags.None);
                    }
                }

                // ── Glyphs ────────────────────────────────────────────────────
                var line   = _lines[li];
                var tokRow = (_tokens != null && li < _tokens.Length) ? _tokens[li] : null;
                DrawGlyphs(drawList, line, tokRow, li, lineY, contentLeft);

                // ── Error squiggle ────────────────────────────────────────────
                if (errors.ContainsKey(li))
                    DrawSquiggle(drawList, li, lineY, contentLeft, 0xFF2020FF);
                else if (warnings.ContainsKey(li))
                    DrawSquiggle(drawList, li, lineY, contentLeft, 0xFF00AAFF);
            }

            // ── Cursor ───────────────────────────────────────────────────────
            bool cursorVisible = igIsWindowFocused(ImGuiFocusedFlags.None) &&
                                 (igGetTime() % 1.0) < 0.5;
            if (cursorVisible)
            {
                float cx = contentLeft + ColToPixel(_cursor.Line, _cursor.Column) - _scrollX;
                float cy = winPos.Y + _cursor.Line * _charH - _scrollY;
                ImDrawList_AddLine(drawList,
                    new Vector2(cx, cy),
                    new Vector2(cx, cy + _charH),
                    _palette[PaletteIndex.Cursor], 1.5f);
            }

            // ── Error tooltip on hover ───────────────────────────────────────
            if (igIsWindowHovered(ImGuiHoveredFlags.None))
            {
                Vector2 mp = default;
                igGetMousePos(ref mp);
                int hoverLine = (int)((mp.Y - winPos.Y + _scrollY) / _charH);
                if (hoverLine >= 0 && hoverLine < _lines.Count)
                {
                    var hoverErrors   = _highlighter.ErrorMarkers;
                    var hoverWarnings = _highlighter.WarningMarkers;
                    if (hoverErrors.TryGetValue(hoverLine, out string? errMsg))
                        igSetTooltip($"\uF057 {errMsg}");   // FA circle-times icon
                    else if (hoverWarnings.TryGetValue(hoverLine, out string? warnMsg))
                        igSetTooltip($"\uF071 {warnMsg}");  // FA warning icon
                }
            }

            igPopClipRect();

            // Invisible dummy for proper scrollbar support: tell ImGui the total content size
            float totalH = _lines.Count * _charH;
            igSetCursorScreenPos(new Vector2(winPos.X, winPos.Y + totalH));
            igDummy(Vector2.Zero);
        }

        private unsafe void DrawSelectionOnLine(
            ImDrawList* dl, int li, float lineY,
            float contentLeft, Vector2 winPos, Vector2 winSize,
            Coords selMin, Coords selMax)
        {
            if (li < selMin.Line || li > selMax.Line) return;

            int startCol = (li == selMin.Line) ? selMin.Column : 0;
            int endCol   = (li == selMax.Line) ? selMax.Column : _lines[li].Count;

            float x0 = contentLeft + ColToPixel(li, startCol) - _scrollX;
            float x1 = contentLeft + ColToPixel(li, endCol)   - _scrollX;

            // Extend selection to end of line for fully-selected lines
            if (li < selMax.Line)
                x1 = winPos.X + winSize.X;

            x0 = MathF.Max(x0, contentLeft);
            if (x1 > x0)
            {
                ImDrawList_AddRectFilled(dl,
                    new Vector2(x0, lineY),
                    new Vector2(x1, lineY + _charH),
                    _palette[PaletteIndex.Selection],
                    0f, ImDrawFlags.None);
            }
        }

        private unsafe void DrawGlyphs(
            ImDrawList* dl, List<Glyph> line, SyntaxToken[]? tokens,
            int li, float lineY, float contentLeft)
        {
            if (line.Count == 0) return;

            // Build a color map from token spans, column → PaletteIndex
            // For lines with few tokens use direct walk.
            var colColor = new PaletteIndex[line.Count];
            // Fill default
            for (int ci = 0; ci < colColor.Length; ci++)
                colColor[ci] = PaletteIndex.Default;

            if (tokens != null)
            {
                foreach (var tok in tokens)
                {
                    int s = Math.Max(0, tok.StartCol);
                    int e = Math.Min(line.Count, tok.EndCol);
                    for (int ci = s; ci < e; ci++)
                        colColor[ci] = tok.Color;
                }
            }

            // Render contiguous runs of the same colour in one text call
            int runStart = 0;
            while (runStart < line.Count)
            {
                PaletteIndex runColor = colColor[runStart];
                int runEnd = runStart + 1;
                while (runEnd < line.Count && colColor[runEnd] == runColor)
                    runEnd++;

                // Build string for this run
                var sb = new StringBuilder(runEnd - runStart);
                float runX = contentLeft - _scrollX;
                for (int ci = 0; ci < runStart; ci++)
                    runX += (line[ci].Char == '\t') ? TabToPixelAdvance(ci) : _charW;

                for (int ci = runStart; ci < runEnd; ci++)
                    sb.Append(line[ci].Char == '\t' ? ' ' : line[ci].Char);  // render tab as space

                string text = sb.ToString();
                if (runX + text.Length * _charW >= contentLeft) // cull off-screen left
                {
                    ImDrawList_AddText_Vec2(dl,
                        new Vector2(runX, lineY),
                        _palette[runColor],
                        text, null);
                }

                runStart = runEnd;
            }
        }

        private unsafe void DrawSquiggle(ImDrawList* dl, int li, float lineY, float contentLeft, uint color)
        {
            var line = _lines[li];
            if (line.Count == 0) return;

            float x0 = contentLeft - _scrollX;
            float x1 = x0 + line.Count * _charW;
            float y  = lineY + _charH - 2f;

            const float ampW = 4f;
            const float ampH = 2f;

            int steps = (int)((x1 - x0) / ampW);
            if (steps < 1) return;

            for (int s = 0; s < steps; s++)
            {
                float sx = x0 + s * ampW;
                float sy = y + ((s & 1) == 0 ? 0f : ampH);
                float ex = sx + ampW;
                float ey = y + ((s & 1) == 0 ? ampH : 0f);
                ImDrawList_AddLine(dl, new Vector2(sx, sy), new Vector2(ex, ey), color, 1f);
            }
        }

        // ── Coordinate utilities ─────────────────────────────────────────────

        private float ColToPixel(int line, int col)
        {
            if (line < 0 || line >= _lines.Count) return 0f;
            var ln = _lines[line];
            float x = 0f;
            int lim = Math.Min(col, ln.Count);
            for (int ci = 0; ci < lim; ci++)
                x += (ln[ci].Char == '\t') ? TabToPixelAdvance(ci) : _charW;
            return x;
        }

        private float TabToPixelAdvance(int col)
        {
            // Advance to next tab stop
            int tabStop = ((col / TabSize) + 1) * TabSize;
            return (tabStop - col) * _charW;
        }

        private Coords ScreenToCoords(Vector2 screenPos, Vector2 winPos, float contentLeft)
        {
            float relY = screenPos.Y - winPos.Y + _scrollY;
            float relX = screenPos.X - contentLeft + _scrollX;

            int line = (int)(relY / _charH);
            line = Math.Clamp(line, 0, _lines.Count - 1);

            var ln = _lines[line];
            float x = 0f;
            int col = 0;
            for (col = 0; col < ln.Count; col++)
            {
                float advance = (ln[col].Char == '\t') ? TabToPixelAdvance(col) : _charW;
                if (x + advance * 0.5f > relX) break;
                x += advance;
            }
            return new Coords(line, col);
        }

        private Coords SanitizeCoords(Coords c)
        {
            c.Line   = Math.Clamp(c.Line, 0, _lines.Count - 1);
            c.Column = Math.Clamp(c.Column, 0, _lines[c.Line].Count);
            return c;
        }

        // Advance cursor by one character
        private Coords Advance(Coords c)
        {
            if (c.Column < _lines[c.Line].Count)
                return new Coords(c.Line, c.Column + 1);
            if (c.Line + 1 < _lines.Count)
                return new Coords(c.Line + 1, 0);
            return c;
        }

        // Move cursor by one character back
        private Coords Retreat(Coords c)
        {
            if (c.Column > 0)
                return new Coords(c.Line, c.Column - 1);
            if (c.Line > 0)
                return new Coords(c.Line - 1, _lines[c.Line - 1].Count);
            return c;
        }

        private Coords WordStartBefore(Coords c)
        {
            c = SanitizeCoords(c);
            if (c.Column == 0) return c;
            var ln = _lines[c.Line];
            int col = c.Column - 1;
            // Skip non-word chars
            while (col > 0 && !IsWordChar(ln[col].Char)) col--;
            // Skip word chars
            while (col > 0 && IsWordChar(ln[col - 1].Char)) col--;
            return new Coords(c.Line, col);
        }

        private Coords WordEndAfter(Coords c)
        {
            c = SanitizeCoords(c);
            var ln = _lines[c.Line];
            int col = c.Column;
            // Skip non-word chars
            while (col < ln.Count && !IsWordChar(ln[col].Char)) col++;
            // Skip word chars
            while (col < ln.Count && IsWordChar(ln[col].Char)) col++;
            return new Coords(c.Line, col);
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        // ── Scroll management ─────────────────────────────────────────────────
        private void ScrollToCursor(Vector2 winSize)
        {
            float cursorPixelY = _cursor.Line * _charH;
            float cursorPixelX = ColToPixel(_cursor.Line, _cursor.Column);

            // Vertical
            if (cursorPixelY < _scrollY)
                _scrollY = cursorPixelY;
            else if (cursorPixelY + _charH > _scrollY + winSize.Y)
                _scrollY = cursorPixelY + _charH - winSize.Y;

            // Horizontal
            float visibleLeft = _gutterW;
            if (cursorPixelX < _scrollX)
                _scrollX = MathF.Max(0f, cursorPixelX - 20f);
            else if (cursorPixelX + _charW > _scrollX + winSize.X - visibleLeft)
                _scrollX = cursorPixelX + _charW - (winSize.X - visibleLeft) + 20f;

            _scrollY = MathF.Max(0f, _scrollY);
            _scrollX = MathF.Max(0f, _scrollX);
        }

        // ── Keyboard input ───────────────────────────────────────────────────
        private unsafe void HandleKeyboardInput()
        {
            var io = igGetIO_Nil();

            bool ctrl  = (io->KeyMods & ImGuiKey.ImGuiMod_Ctrl)  != 0;
            bool shift = (io->KeyMods & ImGuiKey.ImGuiMod_Shift) != 0;
            bool alt   = (io->KeyMods & ImGuiKey.ImGuiMod_Alt)   != 0;

            // ── Navigation ─────────────────────────────────────────────────────
            if (igIsKeyPressed_Bool(ImGuiKey.UpArrow, true))
            {
                if (!shift) ClearSelection();
                _cursor.Line = Math.Max(0, _cursor.Line - 1);
                _cursor.Column = Math.Min(_cursor.Column, _lines[_cursor.Line].Count);
                if (shift) ExtendSelection(_cursor);
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.DownArrow, true))
            {
                if (!shift) ClearSelection();
                _cursor.Line = Math.Min(_lines.Count - 1, _cursor.Line + 1);
                _cursor.Column = Math.Min(_cursor.Column, _lines[_cursor.Line].Count);
                if (shift) ExtendSelection(_cursor);
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.LeftArrow, true))
            {
                if (shift)
                {
                    _cursor = Retreat(_cursor);
                    ExtendSelection(_cursor);
                }
                else if (HasSelection())
                {
                    _cursor = _selStart <= _selEnd ? _selStart : _selEnd;
                    ClearSelection();
                }
                else
                {
                    if (ctrl) _cursor = WordStartBefore(_cursor);
                    else      _cursor = Retreat(_cursor);
                }
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.RightArrow, true))
            {
                if (shift)
                {
                    _cursor = Advance(_cursor);
                    ExtendSelection(_cursor);
                }
                else if (HasSelection())
                {
                    _cursor = _selStart <= _selEnd ? _selEnd : _selStart;
                    ClearSelection();
                }
                else
                {
                    if (ctrl) _cursor = WordEndAfter(_cursor);
                    else      _cursor = Advance(_cursor);
                }
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.Home, true))
            {
                if (!shift) ClearSelection();
                _cursor.Column = 0;
                if (shift) ExtendSelection(_cursor);
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.End, true))
            {
                if (!shift) ClearSelection();
                _cursor.Column = _lines[_cursor.Line].Count;
                if (shift) ExtendSelection(_cursor);
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.PageUp, true))
            {
                if (!shift) ClearSelection();
                Vector2 sz = default; igGetContentRegionAvail(ref sz);
                int pageLines = Math.Max(1, (int)(sz.Y / _charH) - 1);
                _cursor.Line = Math.Max(0, _cursor.Line - pageLines);
                _cursor.Column = Math.Min(_cursor.Column, _lines[_cursor.Line].Count);
                if (shift) ExtendSelection(_cursor);
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.PageDown, true))
            {
                if (!shift) ClearSelection();
                Vector2 sz = default; igGetContentRegionAvail(ref sz);
                int pageLines = Math.Max(1, (int)(sz.Y / _charH) - 1);
                _cursor.Line = Math.Min(_lines.Count - 1, _cursor.Line + pageLines);
                _cursor.Column = Math.Min(_cursor.Column, _lines[_cursor.Line].Count);
                if (shift) ExtendSelection(_cursor);
            }
            // ── Edit ───────────────────────────────────────────────────────────
            else if (igIsKeyPressed_Bool(ImGuiKey.Enter, true))
            {
                InsertNewline();
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.Backspace, true))
            {
                if (HasSelection()) DeleteSelection();
                else                DeleteCharBefore();
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.Delete, true))
            {
                if (HasSelection()) DeleteSelection();
                else                DeleteCharAfter();
            }
            else if (igIsKeyPressed_Bool(ImGuiKey.Tab, true))
            {
                InsertTab(shift);
            }
            // ── Clipboard / Select-all ─────────────────────────────────────────
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.A, true))
            {
                _selStart = Coords.Zero;
                _selEnd   = new Coords(_lines.Count - 1, _lines[_lines.Count - 1].Count);
                _cursor   = _selEnd;
            }
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.C, true))
            {
                CopyToClipboard();
            }
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.X, true))
            {
                CopyToClipboard();
                DeleteSelection();
            }
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.V, true))
            {
                PasteFromClipboard();
            }
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.Z, true))
            {
                DoUndo();
            }
            else if (ctrl && (igIsKeyPressed_Bool(ImGuiKey.Y, true) ||
                              (shift && igIsKeyPressed_Bool(ImGuiKey.Z, true))))
            {
                DoRedo();
            }
            // ── Comment / uncomment ────────────────────────────────────────────
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.Slash, false))
            {
                ToggleLineComment();
            }
            // ── Duplicate line ─────────────────────────────────────────────────
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.D, false))
            {
                DuplicateCurrentLine();
            }
            // ── Move line up / down ────────────────────────────────────────────
            else if (alt && igIsKeyPressed_Bool(ImGuiKey.UpArrow, false))
            {
                MoveLineUp();
            }
            else if (alt && igIsKeyPressed_Bool(ImGuiKey.DownArrow, false))
            {
                MoveLineDown();
            }
            // ── Find bar ───────────────────────────────────────────────────────
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.F, false))
            {
                _findBarVisible   = true;
                _findJustOpened   = true;
            }
            // ── Go to line ─────────────────────────────────────────────────────
            else if (ctrl && igIsKeyPressed_Bool(ImGuiKey.G, false))
            {
                _gotoLineVisible   = true;
                _gotoLineJustOpened = true;
            }
            // ── F3 next / prev find match ──────────────────────────────────────
            else if (igIsKeyPressed_Bool(ImGuiKey.F3, false))
            {
                NavigateToMatch(shift ? -1 : 1);
            }
            // ── F12 go to definition / Shift+F12 find all references ──────────
            else if (igIsKeyPressed_Bool(ImGuiKey.F12, false))
            {
                if (shift) FindAllReferences();
                else       GotoDefinition();
            }
            // ── Escape: close overlays ─────────────────────────────────────────
            else if (igIsKeyPressed_Bool(ImGuiKey.Escape, false))
            {
                if (_findBarVisible)      { _findBarVisible = false; _findMatches.Clear(); _findMatchIdx = -1; }
                else if (_gotoLineVisible) { _gotoLineVisible = false; }
                else if (_symResultsOpen)  { _symResultsOpen = false; }
            }

            // ── Character input ───────────────────────────────────────────────
            for (int qi = 0; qi < io->InputQueueCharacters.Size; qi++)
            {
                char ch = (char)io->InputQueueCharacters.Ref<ushort>(qi);
                if (ch >= 0x20 && ch != 127) // printable non-DEL
                    InsertChar(ch);
            }

            // Scroll so cursor remains visible
            Vector2 winSz = default;
            igGetContentRegionAvail(ref winSz);
            ScrollToCursor(winSz);
        }

        // ── Mouse input ───────────────────────────────────────────────────────
        private unsafe void HandleMouseInput()
        {
            if (!igIsWindowHovered(ImGuiHoveredFlags.None)) return;

            var io = igGetIO_Nil();
            Vector2 winPos = default;
            igGetCursorScreenPos(ref winPos);

            // Correct for the fact that CursorScreenPos advances after content is drawn.
            // Use window pos instead.
            Vector2 contentPos = default;
            igGetWindowPos(ref contentPos);
            contentPos.Y += igGetScrollY();

            Vector2 mp = default;
            igGetMousePos(ref mp);

            // Calculate actual window content top-left
            Vector2 childPos = new Vector2(mp.X, mp.Y); // we'll compute relative in ScreenToCoords
            // We need the top-left of the child window content area
            // igGetWindowPos gives the window position, but we scrolled → use stored scroll
            Vector2 actualWinPos = default;
            igGetWindowPos(ref actualWinPos);

            float contentLeft = actualWinPos.X + _gutterW;

            if (igIsMouseClicked_Bool(ImGuiMouseButton.Left, false))
            {
                Coords clicked = ScreenToCoords(mp, actualWinPos, contentLeft);
                if (igIsMouseDoubleClicked_Nil(ImGuiMouseButton.Left))
                {
                    // Select word
                    _selStart = WordStartBefore(new Coords(clicked.Line, Math.Min(clicked.Column + 1, _lines[clicked.Line].Count)));
                    _selEnd   = WordEndAfter(clicked);
                    _cursor   = _selEnd;
                }
                else
                {
                    bool shift = (io->KeyMods & ImGuiKey.ImGuiMod_Shift) != 0;
                    if (shift)
                        ExtendSelection(clicked);
                    else
                    {
                        _cursor   = clicked;
                        _selStart = clicked;
                        _selEnd   = clicked;
                    }
                    _selecting = true;
                }
            }

            if (_selecting && igIsMouseDown_Nil(ImGuiMouseButton.Left))
            {
                Coords dragged = ScreenToCoords(mp, actualWinPos, contentLeft);
                _selEnd  = dragged;
                _cursor  = dragged;
            }

            if (igIsMouseReleased_Nil(ImGuiMouseButton.Left))
                _selecting = false;

            // Scroll wheel
            float wheel = io->MouseWheel;
            if (wheel != 0f)
            {
                _scrollY -= wheel * _charH * 3f;
                _scrollY  = MathF.Max(0f, _scrollY);
            }
        }

        // ── Selection helpers ─────────────────────────────────────────────────
        private bool HasSelection() => _selStart != _selEnd;

        private void ClearSelection()
        {
            _selStart = _cursor;
            _selEnd   = _cursor;
        }

        private void ExtendSelection(Coords to)
        {
            // _selStart is the anchor; _selEnd and _cursor follow the moving end
            _selEnd  = to;
            _cursor  = to;
        }

        private string GetSelectedText()
        {
            var selMin = _selStart <= _selEnd ? _selStart : _selEnd;
            var selMax = _selStart <= _selEnd ? _selEnd   : _selStart;
            return GetTextRange(selMin, selMax);
        }

        private string GetTextRange(Coords from, Coords to)
        {
            var sb = new StringBuilder();
            for (int li = from.Line; li <= to.Line; li++)
            {
                var ln = _lines[li];
                int startCol = (li == from.Line) ? from.Column : 0;
                int endCol   = (li == to.Line)   ? to.Column   : ln.Count;
                for (int ci = startCol; ci < endCol; ci++)
                    sb.Append(ln[ci].Char);
                if (li < to.Line)
                    sb.Append('\n');
            }
            return sb.ToString();
        }

        // ── Clipboard ─────────────────────────────────────────────────────────
        private unsafe void CopyToClipboard()
        {
            if (!HasSelection()) return;
            string text = GetSelectedText();
            igSetClipboardText(text);
        }

        private unsafe void PasteFromClipboard()
        {
            string? text = igGetClipboardText();
            if (string.IsNullOrEmpty(text)) return;
            if (HasSelection()) DeleteSelection();
            InsertText(text);
        }

        // ── Text mutation ─────────────────────────────────────────────────────
        private void InsertChar(char ch)
        {
            if (HasSelection()) DeleteSelection();

            var before = _cursor;
            _lines[_cursor.Line].Insert(_cursor.Column, new Glyph(ch, PaletteIndex.Default));
            _cursor.Column++;
            _selStart = _selEnd = _cursor;

            _undo.AddRecord(new UndoRecord(
                ch.ToString(), before, _cursor,
                string.Empty, before, before,
                before, _cursor));

            _textVersion++;
        }

        private void InsertText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var before = _cursor;
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n')
                    InsertNewlineAt(_cursor);
                else
                {
                    _lines[_cursor.Line].Insert(_cursor.Column, new Glyph(c, PaletteIndex.Default));
                    _cursor.Column++;
                }
            }
            _selStart = _selEnd = _cursor;

            // Single undo record for the whole paste
            _undo.AddRecord(new UndoRecord(
                text, before, _cursor,
                string.Empty, before, before,
                before, _cursor));

            _textVersion++;
        }

        private void InsertNewline()
        {
            if (HasSelection()) DeleteSelection();
            var before = _cursor;

            // Calculate indentation of current line
            var ln  = _lines[_cursor.Line];
            int indent = 0;
            while (indent < ln.Count && (ln[indent].Char == ' ' || ln[indent].Char == '\t'))
                indent++;

            InsertNewlineAt(_cursor);

            // Copy leading whitespace from previous line
            var prevLn = _lines[_cursor.Line - 1];
            for (int ci = 0; ci < indent && ci < prevLn.Count; ci++)
            {
                char ws = prevLn[ci].Char;
                _lines[_cursor.Line].Insert(_cursor.Column, new Glyph(ws, PaletteIndex.Default));
                _cursor.Column++;
            }
            _selStart = _selEnd = _cursor;

            _undo.AddRecord(new UndoRecord(
                "\n", before, _cursor,
                string.Empty, before, before,
                before, _cursor));

            _textVersion++;
        }

        private void InsertNewlineAt(Coords at)
        {
            var ln = _lines[at.Line];
            var newLine = new List<Glyph>(ln.Count - at.Column);
            for (int ci = at.Column; ci < ln.Count; ci++)
                newLine.Add(ln[ci]);
            ln.RemoveRange(at.Column, ln.Count - at.Column);
            _lines.Insert(at.Line + 1, newLine);
            _cursor = new Coords(at.Line + 1, 0);
        }

        private void InsertTab(bool deIndent)
        {
            if (deIndent)
            {
                // Remove up to TabSize leading spaces from selection or current line
                int startLine = HasSelection()
                    ? Math.Min(_selStart.Line, _selEnd.Line)
                    : _cursor.Line;
                int endLine = HasSelection()
                    ? Math.Max(_selStart.Line, _selEnd.Line)
                    : _cursor.Line;
                for (int li = startLine; li <= endLine; li++)
                {
                    int removed = 0;
                    while (removed < TabSize && _lines[li].Count > 0 &&
                           (_lines[li][0].Char == ' ' || _lines[li][0].Char == '\t'))
                    {
                        _lines[li].RemoveAt(0);
                        removed++;
                        if (li == _cursor.Line)
                            _cursor.Column = Math.Max(0, _cursor.Column - 1);
                    }
                }
                _textVersion++;
            }
            else
            {
                // Insert spaces to next tab stop
                if (HasSelection()) DeleteSelection();
                int spacesToInsert = TabSize - (_cursor.Column % TabSize);
                for (int si = 0; si < spacesToInsert; si++)
                    InsertChar(' ');
            }
        }

        private void DeleteCharBefore()
        {
            if (_cursor == Coords.Zero) return;
            var before = _cursor;
            Coords prev;
            string removed;

            if (_cursor.Column > 0)
            {
                prev    = new Coords(_cursor.Line, _cursor.Column - 1);
                removed = _lines[_cursor.Line][_cursor.Column - 1].Char.ToString();
                _lines[_cursor.Line].RemoveAt(_cursor.Column - 1);
                _cursor.Column--;
            }
            else
            {
                // Merge with previous line
                var prevLn = _lines[_cursor.Line - 1];
                int prevLen = prevLn.Count;
                removed = "\n";
                foreach (var g in _lines[_cursor.Line])
                    prevLn.Add(g);
                _lines.RemoveAt(_cursor.Line);
                _cursor = new Coords(_cursor.Line - 1, prevLen);
                prev    = _cursor;
            }

            _selStart = _selEnd = _cursor;
            _undo.AddRecord(new UndoRecord(
                string.Empty, _cursor, _cursor,
                removed, _cursor, before,
                before, _cursor));
            _textVersion++;
        }

        private void DeleteCharAfter()
        {
            var ln = _lines[_cursor.Line];
            if (_cursor.Column < ln.Count)
            {
                char ch = ln[_cursor.Column].Char;
                var  end = new Coords(_cursor.Line, _cursor.Column + 1);
                ln.RemoveAt(_cursor.Column);
                _undo.AddRecord(new UndoRecord(
                    string.Empty, _cursor, _cursor,
                    ch.ToString(), _cursor, end,
                    _cursor, _cursor));
            }
            else if (_cursor.Line + 1 < _lines.Count)
            {
                var  end    = new Coords(_cursor.Line + 1, 0);
                foreach (var g in _lines[_cursor.Line + 1])
                    ln.Add(g);
                _lines.RemoveAt(_cursor.Line + 1);
                _undo.AddRecord(new UndoRecord(
                    string.Empty, _cursor, _cursor,
                    "\n", _cursor, end,
                    _cursor, _cursor));
            }
            _textVersion++;
        }

        private void DeleteSelection()
        {
            if (!HasSelection()) return;
            var selMin = _selStart <= _selEnd ? _selStart : _selEnd;
            var selMax = _selStart <= _selEnd ? _selEnd   : _selStart;
            string removed = GetTextRange(selMin, selMax);

            DeleteRange(selMin, selMax);
            _cursor   = selMin;
            _selStart = _selEnd = selMin;

            _undo.AddRecord(new UndoRecord(
                string.Empty, selMin, selMin,
                removed, selMin, selMax,
                selMax, selMin));
        }

        private void DeleteRange(Coords from, Coords to)
        {
            if (from == to) return;
            if (from.Line == to.Line)
            {
                _lines[from.Line].RemoveRange(from.Column, to.Column - from.Column);
            }
            else
            {
                // Keep chars before from on from.Line; append chars after to on to.Line
                var firstLn = _lines[from.Line];
                var lastLn  = _lines[to.Line];
                firstLn.RemoveRange(from.Column, firstLn.Count - from.Column);
                for (int ci = to.Column; ci < lastLn.Count; ci++)
                    firstLn.Add(lastLn[ci]);
                _lines.RemoveRange(from.Line + 1, to.Line - from.Line);
            }
            _textVersion++;
        }

        // ── Undo / Redo ───────────────────────────────────────────────────────
        private void DoUndo()
        {
            var r = _undo.Undo();
            if (r == null) return;

            // Reverse: re-delete added text, re-insert removed text
            if (!string.IsNullOrEmpty(r.Added))
                DeleteRange(r.AddedStart, r.AddedEnd);
            if (!string.IsNullOrEmpty(r.Removed))
            {
                // Re-insert at RemovedStart
                _cursor = r.RemovedStart;
                foreach (char c in r.Removed)
                {
                    if (c == '\n') InsertNewlineAt(_cursor);
                    else
                    {
                        _lines[_cursor.Line].Insert(_cursor.Column,
                            new Glyph(c, PaletteIndex.Default));
                        _cursor.Column++;
                    }
                }
            }
            _cursor   = r.BeforeCursor;
            _selStart = _selEnd = _cursor;
            _textVersion++;
        }

        private void DoRedo()
        {
            var r = _undo.Redo();
            if (r == null) return;

            if (!string.IsNullOrEmpty(r.Removed))
                DeleteRange(r.RemovedStart, r.RemovedEnd);

            if (!string.IsNullOrEmpty(r.Added))
            {
                _cursor = r.AddedStart;
                foreach (char c in r.Added)
                {
                    if (c == '\n') InsertNewlineAt(_cursor);
                    else
                    {
                        _lines[_cursor.Line].Insert(_cursor.Column,
                            new Glyph(c, PaletteIndex.Default));
                        _cursor.Column++;
                    }
                }
            }
            _cursor   = r.AfterCursor;
            _selStart = _selEnd = _cursor;
            _textVersion++;
        }

        // ── Context menu ──────────────────────────────────────────────────────
        private unsafe void RenderContextMenu()
        {
            // Use explicit open+begin instead of igBeginPopupContextWindow, which can
            // assert or null-deref inside a child window that uses only ImDrawList rendering
            // (no real ImGui items → unstable internal hover-item state).
            if (igIsWindowHovered(ImGuiHoveredFlags.None) &&
                igIsMouseClicked_Bool(ImGuiMouseButton.Right, false))
                igOpenPopup_Str("##editorCtx", ImGuiPopupFlags.None);

            if (!igBeginPopup("##editorCtx", ImGuiWindowFlags.None))
                return;

            bool hasSel = HasSelection();

            if (igMenuItem_Bool("Cut",   "Ctrl+X", false, hasSel))  { CopyToClipboard(); DeleteSelection(); }
            if (igMenuItem_Bool("Copy",  "Ctrl+C", false, hasSel))    CopyToClipboard();
            if (igMenuItem_Bool("Paste", "Ctrl+V", false, !ReadOnly)) PasteFromClipboard();
            if (igMenuItem_Bool("Select All", "Ctrl+A", false, true))
            {
                _selStart = Coords.Zero;
                _selEnd   = new Coords(_lines.Count - 1, _lines[_lines.Count - 1].Count);
                _cursor   = _selEnd;
            }

            igSeparator();

            if (igMenuItem_Bool("Comment / Uncomment Lines", "Ctrl+/",  false, !ReadOnly))
                ToggleLineComment();
            if (igMenuItem_Bool("Duplicate Line",             "Ctrl+D",  false, !ReadOnly))
                DuplicateCurrentLine();
            if (igMenuItem_Bool("Move Line Up",               "Alt+\u2191", false, !ReadOnly))
                MoveLineUp();
            if (igMenuItem_Bool("Move Line Down",             "Alt+\u2193", false, !ReadOnly))
                MoveLineDown();

            igSeparator();

            if (igMenuItem_Bool("Find",              "Ctrl+F",   false, true))
            { _findBarVisible = true; _findJustOpened = true; }
            if (igMenuItem_Bool("Go to Line\u2026",  "Ctrl+G",   false, true))
            { _gotoLineVisible = true; _gotoLineJustOpened = true; }
            if (igMenuItem_Bool("Next Match",        "F3",       false, _findMatches.Count > 0))
                NavigateToMatch(1);
            if (igMenuItem_Bool("Previous Match",    "Shift+F3", false, _findMatches.Count > 0))
                NavigateToMatch(-1);

            igSeparator();

            if (igMenuItem_Bool("Go to Definition",    "F12",       false, true))
                GotoDefinition();
            if (igMenuItem_Bool("Find All References", "Shift+F12", false, true))
                FindAllReferences();

            igEndPopup();
        }

        // ── Find bar ──────────────────────────────────────────────────────────
        private unsafe void RenderFindBar(Vector2 size)
        {
            igPushStyleVar_Vec2(ImGuiStyleVar.WindowPadding, new Vector2(6f, 4f));
            igBeginChild_Str("##findbar", size, ImGuiChildFlags.Borders, ImGuiWindowFlags.None);
            igPopStyleVar(1);

            if (_findJustOpened)
            {
                igSetKeyboardFocusHere(0);
                _findJustOpened = false;
            }

            // Search input – take most of the width
            float btnW  = 60f;
            float countW = 80f;
            float inputW = size.X - btnW * 2f - countW - 20f;
            igSetNextItemWidth(inputW);

            bool changed = igInputText("##findInput", ref _findBuf[0], (uint)_findBuf.Length,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll,
                null, null);

            // Read null-terminated UTF-8 from buffer
            int nullIdx = Array.IndexOf(_findBuf, (byte)0);
            string newQuery = System.Text.Encoding.UTF8.GetString(_findBuf, 0, nullIdx < 0 ? _findBuf.Length : nullIdx);

            if (newQuery != _findQuery || _findTextVersion != _textVersion)
            {
                _findQuery      = newQuery;
                _findTextVersion = _textVersion;
                RebuildFindMatches();
            }

            if (changed) NavigateToMatch(1);

            // F3 / Enter inside find bar
            if (igIsItemFocused() && igIsKeyPressed_Bool(ImGuiKey.F3, false))
            {
                var io = igGetIO_Nil();
                bool shift = (io->KeyMods & ImGuiKey.ImGuiMod_Shift) != 0;
                NavigateToMatch(shift ? -1 : 1);
            }
            if (igIsItemFocused() && igIsKeyPressed_Bool(ImGuiKey.Escape, false))
            {
                _findBarVisible = false;
                _findMatches.Clear();
                _findMatchIdx = -1;
            }

            igSameLine(0, 4f);
            if (igSmallButton("\uF060##prev")) NavigateToMatch(-1); // FA arrow-left
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip("Previous match (Shift+F3)");
            igSameLine(0, 2f);
            if (igSmallButton("\uF061##next")) NavigateToMatch(1);  // FA arrow-right
            if (igIsItemHovered(ImGuiHoveredFlags.None)) igSetTooltip("Next match (F3)");

            igSameLine(0, 8f);
            string countText = _findMatches.Count == 0
                ? (string.IsNullOrEmpty(_findQuery) ? "" : "No results")
                : $"{(_findMatchIdx >= 0 ? _findMatchIdx + 1 : 0)}/{_findMatches.Count}";
            igTextDisabled(countText);

            igEndChild();
        }

        // ── Go to line dialog ─────────────────────────────────────────────────
        private unsafe void RenderGotoLine()
        {
            if (!_gotoLineVisible) return;

            // Position the modal at the top of the parent window
            Vector2 winPos = default;
            igGetWindowPos(ref winPos);
            Vector2 winSz = default;
            igGetWindowSize(ref winSz);
            igSetNextWindowPos(new Vector2(winPos.X + winSz.X * 0.5f, winPos.Y + 40f),
                ImGuiCond.Always, new Vector2(0.5f, 0f));
            igSetNextWindowSize(new Vector2(240f, 0f), ImGuiCond.Always);

            byte open = 1;
            if (!igBegin("Go to Line##gotoLineWnd",
                    ref open,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoCollapse   | ImGuiWindowFlags.NoMove))
            {
                igEnd();
                if (open == 0) _gotoLineVisible = false;
                return;
            }
            if (open == 0) { _gotoLineVisible = false; igEnd(); return; }

            igText($"Line (1\u2013{_lines.Count}):");
            igSetNextItemWidth(-1f);

            if (_gotoLineJustOpened)
            {
                igSetKeyboardFocusHere(0);
                _gotoLineJustOpened = false;
            }

            bool submitted = igInputText("##gotoLineInput", ref _gotoLineBuf[0],
                (uint)_gotoLineBuf.Length,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue,
                null, null);

            if (submitted || (igIsItemFocused() && igIsKeyPressed_Bool(ImGuiKey.Enter, false)))
            {
                int nullIdx = Array.IndexOf(_gotoLineBuf, (byte)0);
                string raw = System.Text.Encoding.UTF8.GetString(
                    _gotoLineBuf, 0, nullIdx < 0 ? _gotoLineBuf.Length : nullIdx);
                if (int.TryParse(raw, out int targetLine))
                    JumpToLine(targetLine - 1);
                _gotoLineVisible = false;
                Array.Clear(_gotoLineBuf, 0, _gotoLineBuf.Length);
            }

            if (igIsItemFocused() && igIsKeyPressed_Bool(ImGuiKey.Escape, false))
            {
                _gotoLineVisible = false;
                Array.Clear(_gotoLineBuf, 0, _gotoLineBuf.Length);
            }

            igEnd();
        }

        // ── Symbol results popup (Find References / Go to Definition) ─────────
        private unsafe void RenderSymbolResults(Vector2 editorScreenPos)
        {
            if (!_symResultsOpen || _symResults.Count == 0) return;

            // Position below the cursor line
            float popX = editorScreenPos.X + _gutterW + ColToPixel(_cursor.Line, _cursor.Column) - _scrollX;
            float popY = editorScreenPos.Y + (_cursor.Line + 1) * _charH - _scrollY + 4f;
            // Ensure it doesn't go off screen horizontally
            popX = MathF.Max(editorScreenPos.X + _gutterW, popX);

            igSetNextWindowPos(new Vector2(popX, popY), ImGuiCond.Always, Vector2.Zero);
            igSetNextWindowSize(new Vector2(460f, Math.Min(_symResults.Count * _charH + 40f, 220f)), ImGuiCond.Always);

            byte open = 1;
            if (!igBegin("##symResults", ref open,
                    ImGuiWindowFlags.NoScrollbar   | ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoCollapse     | ImGuiWindowFlags.NoMove  |
                    ImGuiWindowFlags.NoSavedSettings))
            {
                igEnd();
                if (open == 0) _symResultsOpen = false;
                return;
            }
            if (open == 0) { _symResultsOpen = false; igEnd(); return; }

            igText(_symResultsTitle);
            igSeparator();

            for (int i = 0; i < _symResults.Count; i++)
            {
                var (rLine, rStart, rEnd) = _symResults[i];
                string lineText = GetLineText(rLine).Trim();
                string label    = $"  L{rLine + 1,4}:  {lineText}##sym{i}";
                if (igSelectable_Bool(label, false, ImGuiSelectableFlags.None, Vector2.Zero))
                {
                    _cursor   = new Coords(rLine, rEnd);
                    _selStart = new Coords(rLine, rStart);
                    _selEnd   = _cursor;
                    _scrollY  = MathF.Max(0f, rLine * _charH - 100f);
                    _symResultsOpen = false;
                }
            }

            // Close on Escape or click outside
            if (igIsKeyPressed_Bool(ImGuiKey.Escape, false) ||
                (!igIsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) &&
                  igIsMouseClicked_Bool(ImGuiMouseButton.Left, false)))
                _symResultsOpen = false;

            igEnd();
        }
        public (int Line, int Column) CursorPosition => (_cursor.Line + 1, _cursor.Column + 1);
    }
}
