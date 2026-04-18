using System;
using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// AOT-safe widget + attribute registry used by <see cref="XmlLoader"/>.
///
/// Instead of reflection, callers pre-register:
///   1. element-name → factory delegate
///   2. (widget type, attribute name) → strongly-typed setter
///   3. widget type → "content" property name (for XML child nodes)
///
/// Built-in widgets are auto-registered on first access; applications may
/// register custom widgets or extra attributes by calling
/// <see cref="Register{T}"/>, <see cref="RegisterAttr{TWidget, TValue}"/>,
/// and <see cref="RegisterContentProperty{TWidget}"/>.
/// </summary>
public static class WidgetRegistry
{
    private static readonly Dictionary<string, Func<Widget>> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    // key = (widget type full name, attribute name) → string-value setter
    private static readonly Dictionary<(string TypeKey, string Attr), Action<Widget, string>> _setters =
        new(AttrKeyComparer.Instance);

    // widget type full name → content property (from ContentPropertyAttribute or explicit register)
    private static readonly Dictionary<string, string> _contentProps = [];

    private static bool _initialized;

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Register a widget factory for an XML element name.</summary>
    public static void Register<T>(string elementName, Func<T> factory) where T : Widget
    {
        _factories[elementName] = () => factory();
    }

    /// <summary>Register a typed attribute setter. The value string is parsed via
    /// <see cref="AttributeParser"/> into <typeparamref name="TValue"/> before the
    /// setter runs.</summary>
    public static void RegisterAttr<TWidget, TValue>(
        string attrName,
        Action<TWidget, TValue> setter) where TWidget : Widget
    {
        var typeKey = typeof(TWidget).FullName ?? typeof(TWidget).Name;
        _setters[(typeKey, attrName)] = (w, v) =>
        {
            if (AttributeParser.TryParse<TValue>(v, out var parsed))
                setter((TWidget)w, parsed!);
        };
    }

    /// <summary>Register the property that receives XML child nodes for this widget type.</summary>
    public static void RegisterContentProperty<TWidget>(string propertyName) where TWidget : Widget
    {
        var typeKey = typeof(TWidget).FullName ?? typeof(TWidget).Name;
        _contentProps[typeKey] = propertyName;
    }

    /// <summary>Create a new widget instance for the given element name, or null if unknown.</summary>
    public static Widget? Create(string elementName)
    {
        EnsureInitialized();
        return _factories.TryGetValue(elementName, out var f) ? f() : null;
    }

    /// <summary>Known element names (for diagnostics / auto-complete).</summary>
    public static IEnumerable<string> RegisteredElements
    {
        get { EnsureInitialized(); return _factories.Keys; }
    }

    /// <summary>
    /// Apply an attribute to a widget. Walks the widget type hierarchy so base-class
    /// attributes (Id, Margin, Padding, …) resolve on every derived widget without
    /// needing to be registered per-type.
    /// Returns true if a setter was found and applied.
    /// </summary>
    public static bool TrySetAttr(Widget w, string attrName, string value)
    {
        EnsureInitialized();
        var t = w.GetType();
        while (t != null && t != typeof(object))
        {
            var typeKey = t.FullName ?? t.Name;
            if (_setters.TryGetValue((typeKey, attrName), out var setter))
            {
                setter(w, value);
                return true;
            }
            t = t.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Returns the registered content-property name for a widget's type, or null if
    /// none was registered (in which case children are attached via AddChild).
    /// </summary>
    public static string? GetContentProperty(Widget w)
    {
        EnsureInitialized();
        var t = w.GetType();
        while (t != null && t != typeof(object))
        {
            var typeKey = t.FullName ?? t.Name;
            if (_contentProps.TryGetValue(typeKey, out var prop))
                return prop;
            t = t.BaseType;
        }
        return null;
    }

    // ─── Bootstrap ───────────────────────────────────────────────────────────

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        RegisterBuiltins();
    }

    private static void RegisterBuiltins()
    {
        // Element names
        Register("Panel",       () => new Panel());
        Register("Label",       () => new Label());
        Register("Button",      () => new Button());
        Register("CheckBox",    () => new CheckBox());
        Register("RadioButton", () => new RadioButton());
        Register("Slider",      () => new Slider());
        Register("ProgressBar", () => new ProgressBar());
        Register("TextBox",     () => new TextBox());
        Register("TextArea",    () => new TextArea());
        Register("ScrollView",  () => new ScrollView());
        Register("Window",      () => new Window());
        Register("Separator",   () => new Separator());
        Register("Image",       () => new Image());
        Register("TabView",     () => new TabView());
        Register("GroupBox",    () => new GroupBox());
        Register("ListBox",     () => new ListBox());
        Register("ComboBox",    () => new ComboBox());
        Register("SplitView",   () => new SplitView());

        // Shared Widget-level attributes — registered once on Widget,
        // discoverable via the base-type walk in TrySetAttr.
        RegisterAttr<Widget, string>     ("Id",        (w, v) => w.Id       = v);
        RegisterAttr<Widget, string>     ("Tooltip",   (w, v) => w.Tooltip  = v);
        RegisterAttr<Widget, bool>       ("Visible",   (w, v) => w.Visible  = v);
        RegisterAttr<Widget, bool>       ("Enabled",   (w, v) => w.Enabled  = v);
        RegisterAttr<Widget, bool>       ("Expand",    (w, v) => w.Expand   = v);
        RegisterAttr<Widget, Thickness>  ("Margin",    (w, v) => w.Margin   = v);
        RegisterAttr<Widget, Thickness>  ("Padding",   (w, v) => w.Padding  = v);
        RegisterAttr<Widget, float>      ("Width",     (w, v) => w.FixedSize = new Vector2(v, w.FixedSize?.Y ?? 0));
        RegisterAttr<Widget, float>      ("Height",    (w, v) => w.FixedSize = new Vector2(w.FixedSize?.X ?? 0, v));
        RegisterAttr<Widget, FlowDirection>("FlowDirection", (w, v) => w.FlowDirection = v);

        // Panel
        RegisterAttr<Panel, UIColor>("BackgroundColor", (w, v) => w.BackgroundColor = v);
        RegisterAttr<Panel, UIColor>("BorderColor",     (w, v) => w.BorderColor     = v);
        RegisterAttr<Panel, float>  ("BorderWidth",     (w, v) => w.BorderWidth     = v);
        RegisterAttr<Panel, bool>   ("DrawShadow",      (w, v) => w.DrawShadow      = v);

        // Label
        RegisterAttr<Label, string>    ("Text",     (w, v) => w.Text     = v);
        RegisterAttr<Label, float>     ("FontSize", (w, v) => w.FontSize = v);
        RegisterAttr<Label, TextAlign> ("Align",    (w, v) => w.Align    = v);
        RegisterAttr<Label, TextWrap>  ("Wrap",     (w, v) => w.Wrap     = v);

        // Button
        RegisterAttr<Button, string>("Text",         (w, v) => w.Text         = v);
        RegisterAttr<Button, float> ("FontSize",     (w, v) => w.FontSize     = v);
        RegisterAttr<Button, float> ("CornerRadius", (w, v) => w.CornerRadius = v);

        // CheckBox / RadioButton
        RegisterAttr<CheckBox,    string>("Label",     (w, v) => w.Label     = v);
        RegisterAttr<CheckBox,    bool>  ("IsChecked", (w, v) => w.IsChecked = v);
        RegisterAttr<RadioButton, string>("Label",     (w, v) => w.Label     = v);
        RegisterAttr<RadioButton, bool>  ("IsChecked", (w, v) => w.IsChecked = v);

        // Slider
        RegisterAttr<Slider, float>("Min",   (w, v) => w.Min   = v);
        RegisterAttr<Slider, float>("Max",   (w, v) => w.Max   = v);
        RegisterAttr<Slider, float>("Value", (w, v) => w.Value = v);
        RegisterAttr<Slider, float>("Step",  (w, v) => w.Step  = v);

        // ProgressBar
        RegisterAttr<ProgressBar, float>("Value",     (w, v) => w.Value     = v);
        RegisterAttr<ProgressBar, bool> ("ShowLabel", (w, v) => w.ShowLabel = v);

        // TextBox / TextArea
        RegisterAttr<TextBox, string>("Text",        (w, v) => w.Text        = v);
        RegisterAttr<TextBox, string>("Placeholder", (w, v) => w.Placeholder = v);
        RegisterAttr<TextBox, bool>  ("IsPassword",  (w, v) => w.IsPassword  = v);
        RegisterAttr<TextBox, int>   ("MaxLength",   (w, v) => w.MaxLength   = v);
        RegisterAttr<TextArea, string>("Text",       (w, v) => w.Text        = v);

        // ScrollView
        RegisterAttr<ScrollView, bool>("CanScrollHorizontal", (w, v) => w.CanScrollHorizontal = v);
        RegisterAttr<ScrollView, bool>("CanScrollVertical",   (w, v) => w.CanScrollVertical   = v);

        // Window
        RegisterAttr<Window, string>("Title",      (w, v) => w.Title      = v);
        RegisterAttr<Window, bool>  ("IsClosable", (w, v) => w.IsClosable = v);

        // Separator
        RegisterAttr<Separator, bool>("IsVertical", (w, v) => w.IsVertical = v);

        // Content properties (for XML child nodes).
        RegisterContentProperty<Panel>("Children");
        RegisterContentProperty<ScrollView>("Children");
        RegisterContentProperty<GroupBox>("Children");
        RegisterContentProperty<Window>("Children");
    }

    // Case-insensitive compare for the attribute half of the key.
    private sealed class AttrKeyComparer : IEqualityComparer<(string TypeKey, string Attr)>
    {
        public static readonly AttrKeyComparer Instance = new();
        public bool Equals((string TypeKey, string Attr) x, (string TypeKey, string Attr) y)
            => x.TypeKey == y.TypeKey && StringComparer.OrdinalIgnoreCase.Equals(x.Attr, y.Attr);
        public int GetHashCode((string TypeKey, string Attr) k)
            => HashCode.Combine(k.TypeKey, StringComparer.OrdinalIgnoreCase.GetHashCode(k.Attr));
    }
}
