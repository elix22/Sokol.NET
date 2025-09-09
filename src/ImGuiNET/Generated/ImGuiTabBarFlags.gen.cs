namespace ImGuiNET
{
    [System.Flags]
    public enum ImGuiTabBarFlags
    {
        None = 0,
        Reorderable = 1,
        AutoSelectNewTabs = 2,
        TabListPopupButton = 4,
        NoCloseWithMiddleMouseButton = 8,
        NoTabListScrollingButtons = 16,
        NoTooltip = 32,
        DrawSelectedOverline = 64,
        FittingPolicyMixed = 128,
        FittingPolicyShrink = 256,
        FittingPolicyScroll = 512,
        FittingPolicyMask = 896,
        FittingPolicyDefault = 128,
    }
}
