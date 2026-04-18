using System;

namespace Sokol.GUI;

/// <summary>
/// Payload carried by a drag-and-drop operation.
/// <see cref="Format"/> identifies what <see cref="Payload"/> actually is
/// (e.g. "text/plain", "sokol.listbox.item", an application-defined string)
/// so drop targets can decide whether to accept the drop.
/// </summary>
public sealed class DragDropData
{
    /// <summary>Logical format identifier — negotiate between source and targets.</summary>
    public string Format { get; init; } = "application/x-sokol-any";

    /// <summary>Opaque payload object. Not serialized; valid only during the drag.</summary>
    public object? Payload { get; init; }

    /// <summary>The widget that started the drag; null if originated outside the GUI.</summary>
    public Widget? Source { get; init; }

    /// <summary>
    /// Optional miniature "ghost" visual shown under the cursor while dragging.
    /// May be null — in which case a textual label is rendered instead.
    /// </summary>
    public UIImage? DragIcon { get; init; }

    /// <summary>Text label shown next to or on top of the ghost icon.</summary>
    public string DragLabel { get; init; } = "";

    /// <summary>Bitmask of effects the source permits.</summary>
    public DragDropEffect AllowedEffects { get; init; } = DragDropEffect.Move | DragDropEffect.Copy;

    public override string ToString() => $"DragDropData[{Format}] {DragLabel}";
}
