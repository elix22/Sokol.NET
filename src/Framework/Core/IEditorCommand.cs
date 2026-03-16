namespace GameEditor.Framework.Core
{
    /// <summary>Represents a reversible editor action.</summary>
    public interface IEditorCommand
    {
        string Description { get; }
        void Execute();
        void Undo();
    }

    /// <summary>
    /// AOT-safe command backed by two delegates: one to apply the action, one to reverse it.
    /// Captures old/new values as closures — no reflection required.
    /// </summary>
    public sealed class DelegateCommand : IEditorCommand
    {
        private readonly System.Action _execute;
        private readonly System.Action _undo;

        public string Description { get; }

        public DelegateCommand(string description, System.Action execute, System.Action undo)
        {
            Description = description;
            _execute    = execute;
            _undo       = undo;
        }

        public void Execute() => _execute();
        public void Undo()    => _undo();
    }
}
