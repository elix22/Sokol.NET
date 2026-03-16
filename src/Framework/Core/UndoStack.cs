using System.Collections.Generic;

namespace GameEditor.Framework.Core
{
    /// <summary>
    /// Central undo/redo history for the editor.
    /// Record() immediately executes the command and pushes it onto the undo stack.
    /// Undo()/Redo() reverse/re-apply the command and move it between stacks.
    /// Clear() is called on scene load / new scene to discard stale history.
    /// </summary>
    public static class UndoStack
    {
        private const int MaxHistory = 256;

        private static readonly Stack<IEditorCommand> _undo = new();
        private static readonly Stack<IEditorCommand> _redo = new();

        public static bool CanUndo => _undo.Count > 0;
        public static bool CanRedo => _redo.Count > 0;

        /// <summary>Execute the command now and push it onto the undo stack.</summary>
        public static void Record(IEditorCommand cmd)
        {
            cmd.Execute();
            Push(cmd);
            _redo.Clear();
        }

        /// <summary>
        /// Push without executing — use when the action was already applied live (e.g. ImGui drag).
        /// </summary>
        public static void RecordAlreadyExecuted(IEditorCommand cmd)
        {
            Push(cmd);
            _redo.Clear();
        }

        private static void Push(IEditorCommand cmd)
        {
            _undo.Push(cmd);
            // Trim to max history: rebuild without the oldest entry
            if (_undo.Count > MaxHistory)
            {
                var tmp = new IEditorCommand[_undo.Count];
                _undo.CopyTo(tmp, 0);  // 0=top … Count-1=oldest
                _undo.Clear();
                for (int i = 0; i < tmp.Length - 1; i++)
                    _undo.Push(tmp[i]);
            }
        }

        public static void Undo()
        {
            if (!CanUndo) return;
            var cmd = _undo.Pop();
            cmd.Undo();
            _redo.Push(cmd);
            Logger.Info($"Undo: {cmd.Description}");
        }

        public static void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redo.Pop();
            cmd.Execute();
            _undo.Push(cmd);
            Logger.Info($"Redo: {cmd.Description}");
        }

        /// <summary>Discard all history — call on scene load / new scene.</summary>
        public static void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
