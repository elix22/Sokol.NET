using System;

namespace Sokol.GUI;

/// <summary>
/// Tracks keyboard focus within a widget tree.
/// </summary>
public sealed class FocusManager
{
    private Widget? _focused;

    public Widget? Focused => _focused;

    public void SetFocus(Widget? widget)
    {
        if (_focused == widget) return;

        var prev = _focused;
        _focused = widget;

        if (prev != null)
        {
            prev.IsFocused = false;
            prev.OnFocusLost();
        }
        if (_focused != null)
        {
            _focused.IsFocused = true;
            _focused.OnFocusGained();
        }
    }

    public void ClearFocus() => SetFocus(null);

    public void MoveFocusNext(Widget root)
    {
        var flat = new System.Collections.Generic.List<Widget>();
        Flatten(root, flat);
        if (flat.Count == 0) return;

        int idx = _focused != null ? flat.IndexOf(_focused) : -1;
        SetFocus(flat[(idx + 1) % flat.Count]);
    }

    public void MoveFocusPrev(Widget root)
    {
        var flat = new System.Collections.Generic.List<Widget>();
        Flatten(root, flat);
        if (flat.Count == 0) return;

        int idx = _focused != null ? flat.IndexOf(_focused) : 0;
        SetFocus(flat[(idx - 1 + flat.Count) % flat.Count]);
    }

    private static void Flatten(Widget w, System.Collections.Generic.List<Widget> list)
    {
        if (w.Enabled && w.Visible) list.Add(w);
        foreach (var c in w.Children) Flatten(c, list);
    }
}
