// SyntaxHighlighter.cs — Background-thread tokenizer for TextEditorWidget.
//
// Design:
//   • TextEditorWidget calls RequestHighlight() when the text changes.
//   • The highlighter runs on a ThreadPool thread, increments a version counter,
//     and when it finishes writes to a result array that the render thread reads
//     via Interlocked.Exchange (lock-free hot path).
//   • Per-line results are a span of (startCol, endCol, PaletteIndex) tokens.
//   • Multi-line block comments are handled with a carry-forward bool array.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GameEditor.CodeEditor
{
    /// <summary>Token span on a single line.</summary>
    public readonly struct SyntaxToken
    {
        public readonly int StartCol;
        public readonly int EndCol;
        public readonly PaletteIndex Color;

        public SyntaxToken(int start, int end, PaletteIndex color)
        {
            StartCol = start;
            EndCol   = end;
            Color    = color;
        }
    }

    public sealed class SyntaxHighlighter
    {
        private readonly LanguageDefinition _lang;

        // Shared between render thread (reader) and worker thread (writer).
        // Replaced atomically via Interlocked.Exchange.
        private volatile SyntaxToken[][]? _result;

        private int _pendingVersion;    // written by render thread, read by worker
        private int _processedVersion;  // written and read only by worker
        private bool _workerRunning;
        private readonly object _lock = new();

        // Errors/warnings injected externally (e.g. by build output parser).
        // Key = 0-based line index.
        private Dictionary<int, string> _errorMarkers   = new();
        private Dictionary<int, string> _warningMarkers = new();

        public SyntaxHighlighter(LanguageDefinition lang)
        {
            _lang = lang;
        }

        /// <summary>
        /// Called by the render thread after every edit.
        /// Schedules a background tokenization pass.
        /// </summary>
        public void RequestHighlight(string[] lines)
        {
            // Snapshot the text — the worker gets its own copy so no locking needed
            // during tokenisation.
            string[] snapshot = (string[])lines.Clone();

            int version;
            lock (_lock)
            {
                version = ++_pendingVersion;
                if (_workerRunning) return; // the running worker will loop and pick up latest
                _workerRunning = true;
            }

            Task.Run(() => WorkerLoop(snapshot, version));
        }

        /// <summary>
        /// Returns the latest completed token array, or null if not yet ready.
        /// Safe to call from the render thread at any time.
        /// </summary>
        public SyntaxToken[][]? GetResult() => _result;

        // ── External error/warning markers (from build output) ───────────────

        public void SetErrorMarkers(Dictionary<int, string> errors)
        {
            Interlocked.Exchange(ref _errorMarkers, errors);
        }
        public void SetWarningMarkers(Dictionary<int, string> warnings)
        {
            Interlocked.Exchange(ref _warningMarkers, warnings);
        }

        public Dictionary<int, string> ErrorMarkers   => _errorMarkers;
        public Dictionary<int, string> WarningMarkers => _warningMarkers;

        // ── Worker ────────────────────────────────────────────────────────────

        private void WorkerLoop(string[] snapshot, int myVersion)
        {
            while (true)
            {
                // Tokenize the snapshot we have
                var result = Tokenize(snapshot);

                lock (_lock)
                {
                    _processedVersion = myVersion;

                    // Is there a newer request we haven't seen yet?
                    if (_pendingVersion > myVersion)
                    {
                        myVersion = _pendingVersion;
                        // We'll loop with the same snapshot — not ideal but safe;
                        // a full re-tokenize is cheap enough for typical file sizes.
                        // (RequestHighlight always replaces the snapshot so we need
                        //  to exit and let the next call re-enter.  Here we just
                        //  finish and let the next RequestHighlight restart us.)
                        _workerRunning = false;
                        break;
                    }

                    _workerRunning = false;
                }

                // Publish atomically
                Interlocked.Exchange(ref _result, result);
                break;
            }
        }

        private SyntaxToken[][] Tokenize(string[] lines)
        {
            var result = new SyntaxToken[lines.Length][];
            bool inBlockComment = false;

            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                result[li]  = TokenizeLine(line, ref inBlockComment);
            }

            return result;
        }

        private SyntaxToken[] TokenizeLine(string line, ref bool inBlockComment)
        {
            var tokens = new List<SyntaxToken>(16);
            int len = line.Length;
            int i   = 0;

            // Fast path: if we're inside a block comment, consume until we find '*/'
            if (inBlockComment)
            {
                int end = line.IndexOf(_lang.BlockCommentEnd, StringComparison.Ordinal);
                if (end < 0)
                {
                    // Whole line is block comment
                    if (len > 0)
                        tokens.Add(new SyntaxToken(0, len, PaletteIndex.MultiLineComment));
                    return tokens.ToArray();
                }
                else
                {
                    int closeEnd = end + _lang.BlockCommentEnd.Length;
                    tokens.Add(new SyntaxToken(0, closeEnd, PaletteIndex.MultiLineComment));
                    i = closeEnd;
                    inBlockComment = false;
                }
            }

            while (i < len)
            {
                // Skip any already-tokenized region (shouldn't happen but guard)
                // ── Single-line comment ────────────────────────────────────────
                if (i + _lang.SingleLineComment.Length <= len &&
                    line.AsSpan(i, _lang.SingleLineComment.Length).SequenceEqual(_lang.SingleLineComment))
                {
                    tokens.Add(new SyntaxToken(i, len, PaletteIndex.Comment));
                    break;
                }

                // ── Block comment start ────────────────────────────────────────
                if (i + _lang.BlockCommentStart.Length <= len &&
                    line.AsSpan(i, _lang.BlockCommentStart.Length).SequenceEqual(_lang.BlockCommentStart))
                {
                    int end = line.IndexOf(_lang.BlockCommentEnd, i + _lang.BlockCommentStart.Length,
                        StringComparison.Ordinal);
                    if (end >= 0)
                    {
                        int closeEnd = end + _lang.BlockCommentEnd.Length;
                        tokens.Add(new SyntaxToken(i, closeEnd, PaletteIndex.MultiLineComment));
                        i = closeEnd;
                    }
                    else
                    {
                        tokens.Add(new SyntaxToken(i, len, PaletteIndex.MultiLineComment));
                        inBlockComment = true;
                        break;
                    }
                    continue;
                }

                // ── Try each token rule ────────────────────────────────────────
                bool matched = false;
                foreach (var (pattern, color) in _lang.TokenRules)
                {
                    var m = pattern.Match(line, i);
                    if (m.Success && m.Index == i)
                    {
                        tokens.Add(new SyntaxToken(i, i + m.Length, color));
                        i += m.Length;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    // Emit the character as default and advance
                    tokens.Add(new SyntaxToken(i, i + 1, PaletteIndex.Default));
                    i++;
                }
            }

            return tokens.ToArray();
        }
    }
}
