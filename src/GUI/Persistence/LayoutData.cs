using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// Serializable representation of a <see cref="DockPanel"/>. Only the id is
/// required to rebuild the layout — the content widget is looked up by id in
/// an application-supplied resolver, since widgets themselves are not (and
/// should not be) serialized.
/// </summary>
public sealed record DockPanelData
{
    public string Id           { get; init; } = "";
    public string Title        { get; init; } = "";
    public bool   IsFloating   { get; init; }
    public float  FloatingX    { get; init; }
    public float  FloatingY    { get; init; }
    public float  FloatingW    { get; init; }
    public float  FloatingH    { get; init; }
}

/// <summary>
/// Serializable representation of a <see cref="DockNode"/>. Mirrors the
/// in-memory binary tree shape.
/// </summary>
public sealed record DockNodeData
{
    public string        Id               { get; init; } = "";
    public DockNodeType  Type             { get; init; } = DockNodeType.Leaf;
    public float         SplitRatio       { get; init; } = 0.5f;
    public int           ActivePanelIndex { get; init; }
    public List<string>  PanelIds         { get; init; } = [];
    public DockNodeData? First            { get; init; }
    public DockNodeData? Second           { get; init; }
}

/// <summary>
/// Root layout document: full dock tree + all known panels (docked + floating).
/// </summary>
public sealed record LayoutData
{
    public int                 Version       { get; init; } = 1;
    public DockNodeData?       Root          { get; init; }
    public List<DockPanelData> Panels        { get; init; } = [];
    public List<DockPanelData> Floating      { get; init; } = [];
}
