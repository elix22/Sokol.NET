// UndoBuffer.cs — Undo/redo stack for TextEditorWidget.
using System;
using System.Collections.Generic;

namespace GameEditor.CodeEditor
{
    /// <summary>
    /// Represents the coordinates (line, column) of a position in the text.
    /// Both fields are zero-based.
    /// </summary>
    public struct Coords : IEquatable<Coords>, IComparable<Coords>
    {
        public int Line;
        public int Column;

        public Coords(int line, int column) { Line = line; Column = column; }

        public static readonly Coords Zero = new Coords(0, 0);

        public int CompareTo(Coords other)
        {
            int cmp = Line.CompareTo(other.Line);
            return cmp != 0 ? cmp : Column.CompareTo(other.Column);
        }

        public bool Equals(Coords other) => Line == other.Line && Column == other.Column;
        public override bool Equals(object? obj) => obj is Coords c && Equals(c);
        public override int GetHashCode() => HashCode.Combine(Line, Column);

        public static bool operator ==(Coords a, Coords b) => a.Equals(b);
        public static bool operator !=(Coords a, Coords b) => !a.Equals(b);
        public static bool operator  <(Coords a, Coords b) => a.CompareTo(b) < 0;
        public static bool operator  >(Coords a, Coords b) => a.CompareTo(b) > 0;
        public static bool operator <=(Coords a, Coords b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Coords a, Coords b) => a.CompareTo(b) >= 0;

        public override string ToString() => $"({Line},{Column})";
    }

    /// <summary>
    /// A single undo/redo record.  Records the text added and/or removed, plus
    /// the cursor positions before and after — everything needed to reverse or
    /// re-apply the change.
    /// </summary>
    public sealed class UndoRecord
    {
        public string  Added;
        public Coords  AddedStart;
        public Coords  AddedEnd;

        public string  Removed;
        public Coords  RemovedStart;
        public Coords  RemovedEnd;

        public Coords  BeforeCursor;
        public Coords  AfterCursor;

        public UndoRecord(
            string added,  Coords addedStart,  Coords addedEnd,
            string removed, Coords removedStart, Coords removedEnd,
            Coords beforeCursor, Coords afterCursor)
        {
            Added          = added;
            AddedStart     = addedStart;
            AddedEnd       = addedEnd;
            Removed        = removed;
            RemovedStart   = removedStart;
            RemovedEnd     = removedEnd;
            BeforeCursor   = beforeCursor;
            AfterCursor    = afterCursor;
        }
    }

    /// <summary>
    /// Linear undo/redo stack.  The index points at the next slot to write;
    /// everything at or above the index is "future" and gets discarded on the
    /// next mutation.
    /// </summary>
    public sealed class UndoBuffer
    {
        private readonly List<UndoRecord> _records = new();
        private int _index; // one past the last committed record

        public bool CanUndo => _index > 0;
        public bool CanRedo => _index < _records.Count;

        public void AddRecord(UndoRecord r)
        {
            // Discard any redo history above the current position
            if (_index < _records.Count)
                _records.RemoveRange(_index, _records.Count - _index);

            _records.Add(r);
            _index++;

            // Cap to a reasonable depth
            const int MaxDepth = 500;
            if (_records.Count > MaxDepth)
            {
                _records.RemoveAt(0);
                _index--;
            }
        }

        /// <summary>
        /// Undo the last record and return it (caller must apply the reverse).
        /// Returns null if nothing to undo.
        /// </summary>
        public UndoRecord? Undo()
        {
            if (!CanUndo) return null;
            _index--;
            return _records[_index];
        }

        /// <summary>
        /// Redo the next record and return it (caller must re-apply it).
        /// Returns null if nothing to redo.
        /// </summary>
        public UndoRecord? Redo()
        {
            if (!CanRedo) return null;
            var r = _records[_index];
            _index++;
            return r;
        }

        public void Clear()
        {
            _records.Clear();
            _index = 0;
        }
    }
}
