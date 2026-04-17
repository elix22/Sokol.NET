namespace Sokol.GUI;

/// <summary>
/// Defines all visual constants for the Sokol.GUI framework.
/// Subclass and override properties to create custom themes or skins.
/// </summary>
public class Theme
{
    // -------------------------------------------------------------------------
    // Font names (resolved via FontRegistry at render time)
    // -------------------------------------------------------------------------
    public virtual string DefaultFont  => "sans";
    public virtual string BoldFont     => "bold";
    public virtual string MonoFont     => "mono";
    public virtual string IconFont     => "icons";

    // -------------------------------------------------------------------------
    // Font sizes
    // -------------------------------------------------------------------------
    public virtual float FontSizeBase    => 16f;
    public virtual float ButtonFontSize  => 15f;
    public virtual float LabelFontSize   => 14f;
    public virtual float TitleFontSize   => 20f;
    public virtual float SmallFontSize   => 12f;

    // -------------------------------------------------------------------------
    // Background / surface colors
    // -------------------------------------------------------------------------
    public virtual UIColor Background      => UIColor.FromHex("#181825");
    public virtual UIColor Surface         => UIColor.FromHex("#1E1E2E");
    public virtual UIColor SurfaceVariant  => UIColor.FromHex("#313244");
    public virtual UIColor Overlay         => new UIColor(0f, 0f, 0f, 0.55f);

    // -------------------------------------------------------------------------
    // Primary / accent
    // -------------------------------------------------------------------------
    public virtual UIColor Primary         => UIColor.FromHex("#89B4FA");
    public virtual UIColor PrimaryHover    => UIColor.FromHex("#B4D0FB");
    public virtual UIColor PrimaryPressed  => UIColor.FromHex("#6793D9");
    public virtual UIColor OnPrimary       => UIColor.FromHex("#1E1E2E");

    // -------------------------------------------------------------------------
    // Text
    // -------------------------------------------------------------------------
    public virtual UIColor TextPrimary    => UIColor.FromHex("#CDD6F4");
    public virtual UIColor TextSecondary  => UIColor.FromHex("#A6ADC8");
    public virtual UIColor TextDisabled   => UIColor.FromHex("#585B70");
    public virtual UIColor TextOnPrimary  => UIColor.FromHex("#1E1E2E");

    // -------------------------------------------------------------------------
    // Borders (NanoGUI-style bevel: light highlight + dark shadow)
    // -------------------------------------------------------------------------
    public virtual UIColor Border        => UIColor.FromHex("#45475A");
    public virtual UIColor BorderFocus   => UIColor.FromHex("#89B4FA");
    public virtual UIColor BorderHover   => UIColor.FromHex("#6C7086");
    /// <summary>NanoGUI border_light — bright inner highlight for raised bevel (Color(92,255)).</summary>
    public virtual UIColor BorderLight   => new UIColor(0.361f, 0.361f, 0.361f, 1f);
    /// <summary>NanoGUI border_dark — dark outer edge for raised bevel (Color(29,255)).</summary>
    public virtual UIColor BorderDark    => new UIColor(0.114f, 0.114f, 0.114f, 1f);
    /// <summary>NanoGUI text_color_shadow — subtle drop shadow behind text (Color(0,160)).</summary>
    public virtual UIColor TextShadow    => new UIColor(0f, 0f, 0f, 0.627f);

    // -------------------------------------------------------------------------
    // Button
    // -------------------------------------------------------------------------
    public virtual UIColor ButtonGradientTop    => UIColor.FromHex("#45475A");
    public virtual UIColor ButtonGradientBottom => UIColor.FromHex("#313244");
    public virtual UIColor ButtonHoverTop       => UIColor.FromHex("#585B70");
    public virtual UIColor ButtonHoverBottom    => UIColor.FromHex("#45475A");
    public virtual UIColor ButtonPressedTop     => UIColor.FromHex("#313244");
    public virtual UIColor ButtonPressedBottom  => UIColor.FromHex("#1E1E2E");
    public virtual UIColor ButtonText           => UIColor.FromHex("#CDD6F4");

    // -------------------------------------------------------------------------
    // Window
    // -------------------------------------------------------------------------
    public virtual UIColor WindowBackground => UIColor.FromHex("#1E1E2E");
    public virtual UIColor WindowHeader     => UIColor.FromHex("#313244");
    public virtual UIColor WindowHeaderText => UIColor.FromHex("#CDD6F4");
    public virtual UIColor DropShadow       => new UIColor(0f, 0f, 0f, 0.5f);

    // -------------------------------------------------------------------------
    // Input / TextBox
    // -------------------------------------------------------------------------
    public virtual UIColor InputBackground => UIColor.FromHex("#181825");
    public virtual UIColor InputText       => UIColor.FromHex("#CDD6F4");
    public virtual UIColor InputPlaceholder=> UIColor.FromHex("#585B70");
    public virtual UIColor InputCaret      => UIColor.FromHex("#89B4FA");
    public virtual UIColor InputSelection  => UIColor.FromHex("#45475A");

    // -------------------------------------------------------------------------
    // Checkbox / Radio
    // -------------------------------------------------------------------------
    public virtual UIColor CheckMark       => UIColor.FromHex("#89B4FA");
    public virtual UIColor CheckBackground => UIColor.FromHex("#313244");

    // -------------------------------------------------------------------------
    // Slider
    // -------------------------------------------------------------------------
    public virtual UIColor SliderTrack  => UIColor.FromHex("#313244");
    public virtual UIColor SliderFill   => UIColor.FromHex("#89B4FA");
    public virtual UIColor SliderThumb  => UIColor.FromHex("#CDD6F4");

    // -------------------------------------------------------------------------
    // Scrollbar
    // -------------------------------------------------------------------------
    public virtual UIColor ScrollBarTrack  => UIColor.FromHex("#181825");
    public virtual UIColor ScrollBarThumb  => UIColor.FromHex("#45475A");
    public virtual UIColor ScrollBarThumbHover => UIColor.FromHex("#585B70");

    // -------------------------------------------------------------------------
    // Tab
    // -------------------------------------------------------------------------
    public virtual UIColor TabActive    => UIColor.FromHex("#313244");
    public virtual UIColor TabInactive  => UIColor.FromHex("#1E1E2E");
    public virtual UIColor TabText      => UIColor.FromHex("#CDD6F4");
    public virtual UIColor TabBorder    => UIColor.FromHex("#45475A");

    // -------------------------------------------------------------------------
    // Tooltip
    // -------------------------------------------------------------------------
    public virtual UIColor TooltipBackground => UIColor.FromHex("#313244");
    public virtual UIColor TooltipText       => UIColor.FromHex("#CDD6F4");
    public virtual UIColor TooltipBorder     => UIColor.FromHex("#45475A");

    // -------------------------------------------------------------------------
    // Metrics
    // -------------------------------------------------------------------------
    public virtual float WindowCornerRadius => 6f;
    public virtual float ButtonCornerRadius => 4f;
    public virtual float InputCornerRadius  => 4f;
    public virtual float PanelCornerRadius  => 4f;
    public virtual float CheckBoxSize       => 16f;
    public virtual float RadioButtonSize    => 16f;

    public virtual float WindowHeaderHeight => 30f;
    public virtual float ButtonHeight       => 30f;
    public virtual float InputHeight        => 28f;
    public virtual float TabBarHeight       => 32f;
    public virtual float ScrollBarWidth     => 12f;
    public virtual float SliderTrackHeight  => 4f;
    public virtual float SliderThumbRadius  => 8f;
    public virtual float TooltipCornerRadius=> 4f;

    public virtual float Padding            => 8f;
    public virtual float ItemSpacing        => 6f;
    public virtual float GroupSpacing       => 14f;
    public virtual float BorderWidth        => 1f;

    public virtual Vector2 ShadowOffset     => new(2f, 4f);
    public virtual float   ShadowBlur       => 8f;
    public virtual float   TooltipDelay     => 0.5f;

    // ─── Convenience aliases used by widgets ─────────────────────────────────
    public virtual UIColor SurfaceColor          => Surface;
    public virtual UIColor AccentColor           => Primary;
    public virtual UIColor ButtonColor           => ButtonGradientTop;
    public virtual UIColor ButtonHoverColor      => ButtonHoverTop;
    public virtual UIColor ButtonPressedColor    => ButtonPressedTop;
    public virtual UIColor ButtonDisabledColor   => SurfaceVariant;
    public virtual UIColor ButtonTextColor       => ButtonText;
    public virtual UIColor BorderColor           => Border;
    public virtual UIColor TextColor             => TextPrimary;
    public virtual UIColor TextDisabledColor     => TextDisabled;
    public virtual UIColor CheckBoxColor         => CheckBackground;
    public virtual UIColor CheckBoxHoverColor    => SurfaceVariant;
    public virtual UIColor InputBackColor        => InputBackground;
    public virtual UIColor SelectionColor        => InputSelection;
    public virtual UIColor ShadowColor           => DropShadow;

    public virtual float   FontSize              => FontSizeBase;
    public virtual float   ButtonPaddingH        => Padding;
    public virtual float   ButtonPaddingV        => 4f;
    public virtual float   CheckBoxCornerRadius  => 3f;
    public virtual float   CheckBoxLabelSpacing  => 6f;
    public virtual float   SliderThickness       => SliderThumbRadius * 2f;
    public virtual float   SliderTrackThickness  => SliderTrackHeight;
    public virtual float   SliderThumbSize       => SliderThumbRadius * 2f;
    public virtual UIColor SliderTrackColor      => SliderTrack;
    public virtual UIColor SliderThumbColor      => SliderThumb;
    public virtual UIColor SliderThumbHoverColor => Primary;

    // Additional widget aliases
    public virtual UIColor SeparatorColor        => Border;
    public virtual UIColor TabBarColor           => TabInactive;
    public virtual float   TabPaddingH           => 12f;
    public virtual UIColor TextMutedColor        => TextSecondary;
    public virtual UIColor PlaceholderColor      => InputPlaceholder;
    public virtual UIColor ScrollBarTrackColor   => ScrollBarTrack;
    public virtual UIColor ScrollBarThumbColor   => ScrollBarThumb;
    public virtual UIColor ScrollBarThumbHoverColor => ScrollBarThumbHover;
    public virtual float   TooltipFontSize       => SmallFontSize;
    public virtual float   TooltipPaddingH       => 8f;
    public virtual float   TooltipPaddingV       => 4f;
    public virtual UIColor TooltipBackColor      => TooltipBackground;
    public virtual UIColor TooltipTextColor      => TooltipText;
    public virtual float   ProgressBarThickness  => 8f;
    public virtual float   ProgressBarCornerRadius => 4f;
    public virtual UIColor ProgressBarTrackColor => SurfaceVariant;
    public virtual UIColor WindowTitleBarColor   => WindowHeader;
    public virtual float   WindowTitleBarHeight  => WindowHeaderHeight;
    public virtual UIColor WindowCloseButtonColor => SurfaceVariant;
    public virtual float   ScrollSpeed            => 30f;

    // ─── Layout direction ─────────────────────────────────────────────────────
    /// <summary>Default flow direction applied to the root Screen widget.</summary>
    public virtual FlowDirection DefaultFlowDirection => FlowDirection.LeftToRight;
}
