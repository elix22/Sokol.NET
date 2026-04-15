namespace Sokol.GUI;

/// <summary>
/// Base controller for MVC.  Mediates between <typeparamref name="TModel"/> and <typeparamref name="TView"/>.
/// </summary>
public abstract class Controller<TModel, TView>
    where TModel : Model
    where TView  : View<TModel>
{
    public TModel Model { get; }
    public TView  View  { get; }

    protected Controller(TModel model, TView view)
    {
        Model = model;
        View  = view;
        View.SetModel(model);
        OnInitialize();
    }

    /// <summary>Override to wire commands and event handlers.</summary>
    protected virtual void OnInitialize() { }
}
