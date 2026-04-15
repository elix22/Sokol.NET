namespace Sokol.GUI;

/// <summary>A light theme variant.</summary>
public class LightTheme : Theme
{
    public override UIColor Background      => UIColor.FromHex("#EFF1F5");
    public override UIColor Surface         => UIColor.FromHex("#FFFFFF");
    public override UIColor SurfaceVariant  => UIColor.FromHex("#E6E9EF");
    public override UIColor Overlay         => new UIColor(0f, 0f, 0f, 0.3f);

    public override UIColor Primary         => UIColor.FromHex("#1E66F5");
    public override UIColor PrimaryHover    => UIColor.FromHex("#3B7CF7");
    public override UIColor PrimaryPressed  => UIColor.FromHex("#1650CC");
    public override UIColor OnPrimary       => UIColor.FromHex("#FFFFFF");

    public override UIColor TextPrimary     => UIColor.FromHex("#4C4F69");
    public override UIColor TextSecondary   => UIColor.FromHex("#6C6F85");
    public override UIColor TextDisabled    => UIColor.FromHex("#ACB0BE");
    public override UIColor TextOnPrimary   => UIColor.FromHex("#FFFFFF");

    public override UIColor Border          => UIColor.FromHex("#BCC0CC");
    public override UIColor BorderFocus     => UIColor.FromHex("#1E66F5");
    public override UIColor BorderHover     => UIColor.FromHex("#9CA0B0");

    public override UIColor ButtonGradientTop    => UIColor.FromHex("#E6E9EF");
    public override UIColor ButtonGradientBottom => UIColor.FromHex("#D4D7E3");
    public override UIColor ButtonHoverTop       => UIColor.FromHex("#EFF1F5");
    public override UIColor ButtonHoverBottom    => UIColor.FromHex("#E6E9EF");
    public override UIColor ButtonPressedTop     => UIColor.FromHex("#D4D7E3");
    public override UIColor ButtonPressedBottom  => UIColor.FromHex("#C8CBD8");
    public override UIColor ButtonText           => UIColor.FromHex("#4C4F69");

    public override UIColor WindowBackground => UIColor.FromHex("#FFFFFF");
    public override UIColor WindowHeader     => UIColor.FromHex("#E6E9EF");
    public override UIColor WindowHeaderText => UIColor.FromHex("#4C4F69");
    public override UIColor DropShadow       => new UIColor(0f, 0f, 0f, 0.18f);

    public override UIColor InputBackground => UIColor.FromHex("#FFFFFF");
    public override UIColor InputText       => UIColor.FromHex("#4C4F69");
    public override UIColor InputPlaceholder=> UIColor.FromHex("#ACB0BE");
    public override UIColor InputCaret      => UIColor.FromHex("#1E66F5");
    public override UIColor InputSelection  => UIColor.FromHex("#BCC0CC");

    public override UIColor CheckMark       => UIColor.FromHex("#1E66F5");
    public override UIColor CheckBackground => UIColor.FromHex("#EFF1F5");

    public override UIColor SliderTrack  => UIColor.FromHex("#D4D7E3");
    public override UIColor SliderFill   => UIColor.FromHex("#1E66F5");
    public override UIColor SliderThumb  => UIColor.FromHex("#4C4F69");

    public override UIColor ScrollBarTrack      => UIColor.FromHex("#EFF1F5");
    public override UIColor ScrollBarThumb      => UIColor.FromHex("#BCC0CC");
    public override UIColor ScrollBarThumbHover => UIColor.FromHex("#9CA0B0");

    public override UIColor TabActive   => UIColor.FromHex("#FFFFFF");
    public override UIColor TabInactive => UIColor.FromHex("#EFF1F5");
    public override UIColor TabText     => UIColor.FromHex("#4C4F69");
    public override UIColor TabBorder   => UIColor.FromHex("#BCC0CC");

    public override UIColor TooltipBackground => UIColor.FromHex("#E6E9EF");
    public override UIColor TooltipText       => UIColor.FromHex("#4C4F69");
    public override UIColor TooltipBorder     => UIColor.FromHex("#BCC0CC");
}
