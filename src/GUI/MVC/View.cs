namespace Sokol.GUI;

/// <summary>
/// Base view for MVC.  A view is just a <see cref="Panel"/> that knows its <see cref="Controller"/>.
/// </summary>
public abstract class View<TModel> : Panel where TModel : Model
{
    public TModel?     Model      { get; private set; }
    public BindingContext Bindings { get; } = new();

    public void SetModel(TModel model)
    {
        Bindings.Dispose();
        Model = model;
        OnModelSet(model);
    }

    /// <summary>Override to create bindings and populate children.</summary>
    protected abstract void OnModelSet(TModel model);
}
