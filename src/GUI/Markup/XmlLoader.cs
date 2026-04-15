using System;
using System.Collections.Generic;
using System.Xml;

namespace Sokol.GUI;

/// <summary>
/// Minimal Avalonia-style XML UI loader.
///
/// Usage:
/// <code>
///   var panel = XmlLoader.Load(xmlString);
///   screen.AddChild(panel);
/// </code>
///
/// Supported elements (case-insensitive):
///   Panel, Label, Button, CheckBox, RadioButton,
///   Slider, ProgressBar, TextBox, TextArea, ComboBox,
///   TabView, ScrollView, Window, Separator, Image
///
/// Attributes are mapped to widget properties via <see cref="WidgetBuilder"/>.
/// </summary>
public static class XmlLoader
{
    // Registry: element name → factory
    private static readonly Dictionary<string, Func<XmlElement, Widget>> _factories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Panel"]        = e => BuildPanel(e),
        ["Label"]        = e => BuildLabel(e),
        ["Button"]       = e => BuildButton(e),
        ["CheckBox"]     = e => BuildCheckBox(e),
        ["RadioButton"]  = e => BuildRadioButton(e),
        ["Slider"]       = e => BuildSlider(e),
        ["ProgressBar"]  = e => BuildProgressBar(e),
        ["TextBox"]      = e => BuildTextBox(e),
        ["TextArea"]     = e => BuildTextArea(e),
        ["ScrollView"]   = e => BuildScrollView(e),
        ["Window"]       = e => BuildWindow(e),
        ["Separator"]    = e => BuildSeparator(e),
        ["Image"]        = e => BuildWidgetBase(new Image(), e),
        ["TabView"]      = e => BuildWidgetBase(new TabView(), e),
    };

    public static Widget Load(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return ParseNode(doc.DocumentElement
            ?? throw new InvalidOperationException("Empty XML document"));
    }

    private static Widget ParseNode(XmlElement el)
    {
        if (!_factories.TryGetValue(el.Name, out var factory))
            throw new NotSupportedException($"Unknown element <{el.Name}>");

        var widget = factory(el);

        // Recurse children
        foreach (XmlNode child in el.ChildNodes)
        {
            if (child is XmlElement childEl)
                widget.AddChild(ParseNode(childEl));
        }

        return widget;
    }

    // ─── Per-type builders ───────────────────────────────────────────────────

    private static Panel BuildPanel(XmlElement e)
    {
        var w = new Panel();
        if (e.HasAttribute("BackgroundColor"))
            w.BackgroundColor = ParseColor(e.GetAttribute("BackgroundColor"));
        if (e.HasAttribute("BorderColor"))
            w.BorderColor = ParseColor(e.GetAttribute("BorderColor"));
        if (e.HasAttribute("BorderWidth"))
            w.BorderWidth = float.Parse(e.GetAttribute("BorderWidth"));
        if (e.HasAttribute("DrawShadow"))
            w.DrawShadow = bool.Parse(e.GetAttribute("DrawShadow"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static Label BuildLabel(XmlElement e)
    {
        var w = new Label();
        if (e.HasAttribute("Text"))     w.Text    = e.GetAttribute("Text");
        if (e.HasAttribute("FontSize")) w.FontSize = float.Parse(e.GetAttribute("FontSize"));
        if (e.HasAttribute("Align"))    w.Align   = Enum.Parse<TextAlign>(e.GetAttribute("Align"), true);
        if (e.HasAttribute("Wrap"))     w.Wrap    = Enum.Parse<TextWrap>(e.GetAttribute("Wrap"), true);
        BuildWidgetBase(w, e);
        return w;
    }

    private static Button BuildButton(XmlElement e)
    {
        var w = new Button();
        if (e.HasAttribute("Text"))         w.Text    = e.GetAttribute("Text");
        if (e.HasAttribute("FontSize"))     w.FontSize = float.Parse(e.GetAttribute("FontSize"));
        if (e.HasAttribute("CornerRadius")) w.CornerRadius = float.Parse(e.GetAttribute("CornerRadius"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static CheckBox BuildCheckBox(XmlElement e)
    {
        var w = new CheckBox();
        if (e.HasAttribute("Label"))     w.Label     = e.GetAttribute("Label");
        if (e.HasAttribute("IsChecked")) w.IsChecked = bool.Parse(e.GetAttribute("IsChecked"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static RadioButton BuildRadioButton(XmlElement e)
    {
        var w = new RadioButton();
        if (e.HasAttribute("Label"))     w.Label = e.GetAttribute("Label");
        if (e.HasAttribute("IsChecked")) w.IsChecked = bool.Parse(e.GetAttribute("IsChecked"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static Slider BuildSlider(XmlElement e)
    {
        var w = new Slider();
        if (e.HasAttribute("Min"))   w.Min   = float.Parse(e.GetAttribute("Min"));
        if (e.HasAttribute("Max"))   w.Max   = float.Parse(e.GetAttribute("Max"));
        if (e.HasAttribute("Value")) w.Value = float.Parse(e.GetAttribute("Value"));
        if (e.HasAttribute("Step"))  w.Step  = float.Parse(e.GetAttribute("Step"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static ProgressBar BuildProgressBar(XmlElement e)
    {
        var w = new ProgressBar();
        if (e.HasAttribute("Value"))     w.Value     = float.Parse(e.GetAttribute("Value"));
        if (e.HasAttribute("ShowLabel")) w.ShowLabel = bool.Parse(e.GetAttribute("ShowLabel"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static TextBox BuildTextBox(XmlElement e)
    {
        var w = new TextBox();
        if (e.HasAttribute("Text"))        w.Text        = e.GetAttribute("Text");
        if (e.HasAttribute("Placeholder")) w.Placeholder = e.GetAttribute("Placeholder");
        if (e.HasAttribute("IsPassword"))  w.IsPassword  = bool.Parse(e.GetAttribute("IsPassword"));
        if (e.HasAttribute("MaxLength"))   w.MaxLength   = int.Parse(e.GetAttribute("MaxLength"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static TextArea BuildTextArea(XmlElement e)
    {
        var w = new TextArea();
        if (e.HasAttribute("Text")) w.Text = e.GetAttribute("Text");
        BuildWidgetBase(w, e);
        return w;
    }

    private static ScrollView BuildScrollView(XmlElement e)
    {
        var w = new ScrollView();
        if (e.HasAttribute("CanScrollHorizontal")) w.CanScrollHorizontal = bool.Parse(e.GetAttribute("CanScrollHorizontal"));
        if (e.HasAttribute("CanScrollVertical"))   w.CanScrollVertical   = bool.Parse(e.GetAttribute("CanScrollVertical"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static Window BuildWindow(XmlElement e)
    {
        var w = new Window();
        if (e.HasAttribute("Title"))       w.Title      = e.GetAttribute("Title");
        if (e.HasAttribute("IsClosable"))  w.IsClosable = bool.Parse(e.GetAttribute("IsClosable"));
        BuildWidgetBase(w, e);
        return w;
    }

    private static Separator BuildSeparator(XmlElement e)
    {
        var w = new Separator();
        if (e.HasAttribute("IsVertical")) w.IsVertical = bool.Parse(e.GetAttribute("IsVertical"));
        BuildWidgetBase(w, e);
        return w;
    }

    // Shared base-property mapping
    private static Widget BuildWidgetBase(Widget w, XmlElement e)
    {
        if (e.HasAttribute("Id"))       w.Id      = e.GetAttribute("Id");
        if (e.HasAttribute("Tooltip"))  w.Tooltip = e.GetAttribute("Tooltip");
        if (e.HasAttribute("Visible"))  w.Visible = bool.Parse(e.GetAttribute("Visible"));
        if (e.HasAttribute("Enabled"))  w.Enabled = bool.Parse(e.GetAttribute("Enabled"));

        if (e.HasAttribute("Margin"))  w.Margin  = ParseThickness(e.GetAttribute("Margin"));
        if (e.HasAttribute("Padding")) w.Padding = ParseThickness(e.GetAttribute("Padding"));

        if (e.HasAttribute("Width") && e.HasAttribute("Height"))
            w.FixedSize = new Vector2(float.Parse(e.GetAttribute("Width")), float.Parse(e.GetAttribute("Height")));
        else if (e.HasAttribute("Width"))
            w.FixedSize = new Vector2(float.Parse(e.GetAttribute("Width")), w.FixedSize?.Y ?? 0);
        else if (e.HasAttribute("Height"))
            w.FixedSize = new Vector2(w.FixedSize?.X ?? 0, float.Parse(e.GetAttribute("Height")));

        if (e.HasAttribute("X") && e.HasAttribute("Y"))
            w.Bounds = new Rect(float.Parse(e.GetAttribute("X")), float.Parse(e.GetAttribute("Y")),
                                w.Bounds.Width, w.Bounds.Height);

        if (e.HasAttribute("Layout"))
        {
            float spacing = e.HasAttribute("Spacing") ? float.Parse(e.GetAttribute("Spacing")) : 0f;
            w.Layout = e.GetAttribute("Layout").ToLowerInvariant() switch
            {
                "stack"  => new StackLayout(spacing),
                "box"    => new BoxLayout(Orientation.Vertical, Alignment.Start, spacing),
                "grid"   => new GridLayout(),
                "dock"   => new DockLayout(),
                "canvas" => new CanvasLayout(),
                _        => new CanvasLayout(),
            };
        }

        return w;
    }

    // ─── Parsers ─────────────────────────────────────────────────────────────

    private static UIColor ParseColor(string s) =>
        s.StartsWith('#') ? UIColor.FromHex(s) : UIColor.Transparent;

    /// <summary>Parses "4" | "4,8" | "4,8,4,8" → Thickness.</summary>
    private static Thickness ParseThickness(string s)
    {
        var parts = s.Split(',');
        return parts.Length switch
        {
            1 => new Thickness(float.Parse(parts[0])),
            2 => new Thickness(float.Parse(parts[0]), float.Parse(parts[1])),
            4 => new Thickness(float.Parse(parts[0]), float.Parse(parts[1]),
                               float.Parse(parts[2]), float.Parse(parts[3])),
            _ => new Thickness(0),
        };
    }
}
