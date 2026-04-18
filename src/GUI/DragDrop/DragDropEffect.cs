using System;

namespace Sokol.GUI;

/// <summary>
/// Effects a drop target accepts for a drag operation. Drag sources advertise
/// the set they allow; targets restrict that set to what makes sense for them.
/// </summary>
[Flags]
public enum DragDropEffect
{
    None = 0,
    Move = 1,
    Copy = 2,
    Link = 4,
    All  = Move | Copy | Link,
}
