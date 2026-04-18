using System;

namespace Sokol.GUI;

/// <summary>
/// Marks which property of a widget receives child XML nodes by default.
/// Mirrors WPF/Avalonia's <c>[ContentProperty]</c>. Used by <see cref="XmlLoader"/>
/// to decide where to place children when the container class has several slots
/// (e.g. Panel uses Children, ScrollView uses Content, TabView uses Tabs).
/// When absent, children are appended via <see cref="Widget.AddChild"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ContentPropertyAttribute : Attribute
{
    public string PropertyName { get; }
    public ContentPropertyAttribute(string propertyName) => PropertyName = propertyName;
}
