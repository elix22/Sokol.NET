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

        /// <summary>Raised after an <see cref="Undo"/> or a <see cref="Redo"/> was applied.</summary>
        public static event System.Action? Applied;

        public static bool CanUndo => _undo.Count > 0;
        public static bool CanRedo => _redo.Count > 0;

        /// <summary>What <see cref="Undo"/> would undo, or null when there is nothing to undo.</summary>
        public static string? UndoDescription => _undo.Count > 0 ? _undo.Peek().Description : null;
        /// <summary>What <see cref="Redo"/> would redo, or null when there is nothing to redo.</summary>
        public static string? RedoDescription => _redo.Count > 0 ? _redo.Peek().Description : null;

        /// <summary>Execute the command now and push it onto the undo stack.</summary>
        public static void Record(IEditorCommand cmd)
        {
            cmd.Execute();
            Push(cmd);
            ClearRedo();
        }

        /// <summary>
        /// Push without executing — use when the action was already applied live (e.g. ImGui drag).
        /// </summary>
        public static void RecordAlreadyExecuted(IEditorCommand cmd)
        {
            Push(cmd);
            ClearRedo();
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
                (tmp[^1] as IDiscardableCommand)?.Discard();
            }
        }

        public static void Undo()
        {
            if (!CanUndo) return;
            var cmd = _undo.Pop();
            cmd.Undo();
            _redo.Push(cmd);
            Logger.Info($"Undo: {cmd.Description}");
            Applied?.Invoke();
        }

        public static void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redo.Pop();
            cmd.Execute();
            _undo.Push(cmd);
            Logger.Info($"Redo: {cmd.Description}");
            Applied?.Invoke();
        }

        /// <summary>Discard all history — call on scene load / new scene.</summary>
        public static void Clear()
        {
            foreach (var cmd in _undo) (cmd as IDiscardableCommand)?.Discard();
            _undo.Clear();
            ClearRedo();
        }

        private static void ClearRedo()
        {
            foreach (var cmd in _redo) (cmd as IDiscardableCommand)?.Discard();
            _redo.Clear();
        }
    }
}
