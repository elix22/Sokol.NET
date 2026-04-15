using System.Collections.Generic;

namespace Sokol.GUI;

/// <summary>
/// A <see cref="Theme"/> subclass that stores per-key overrides.
/// Useful for loading partial skin customizations from XML/JSON.
/// </summary>
public class Skin : Theme
{
    private readonly Dictionary<string, object> _overrides = new();
    private readonly Theme _base;

    public Skin(Theme baseTheme) => _base = baseTheme;

    // -------------------------------------------------------------------------
    // Override setters
    // -------------------------------------------------------------------------

    public void Set(string key, UIColor value) => _overrides[key] = value;
    public void Set(string key, float value)   => _overrides[key] = value;
    public void Set(string key, string value)  => _overrides[key] = value;

    // -------------------------------------------------------------------------
    // Override getters (fall back to base theme)
    // -------------------------------------------------------------------------

    public T Get<T>(string key, T fallback) =>
        _overrides.TryGetValue(key, out var v) && v is T typed ? typed : fallback;

    // Delegate all virtual properties to the base theme so callers see a full
    // theme; individual Set() calls overlay on top.
    public override UIColor Background      => Get(nameof(Background),      _base.Background);
    public override UIColor Surface         => Get(nameof(Surface),         _base.Surface);
    public override UIColor SurfaceVariant  => Get(nameof(SurfaceVariant),  _base.SurfaceVariant);
    public override UIColor Primary         => Get(nameof(Primary),         _base.Primary);
    public override UIColor PrimaryHover    => Get(nameof(PrimaryHover),    _base.PrimaryHover);
    public override UIColor PrimaryPressed  => Get(nameof(PrimaryPressed),  _base.PrimaryPressed);
    public override UIColor OnPrimary       => Get(nameof(OnPrimary),       _base.OnPrimary);
    public override UIColor TextPrimary     => Get(nameof(TextPrimary),     _base.TextPrimary);
    public override UIColor TextSecondary   => Get(nameof(TextSecondary),   _base.TextSecondary);
    public override UIColor TextDisabled    => Get(nameof(TextDisabled),    _base.TextDisabled);
    public override UIColor Border               => Get(nameof(Border),               _base.Border);
    public override UIColor BorderFocus          => Get(nameof(BorderFocus),          _base.BorderFocus);
    public override UIColor ButtonGradientTop    => Get(nameof(ButtonGradientTop),    _base.ButtonGradientTop);
    public override UIColor ButtonGradientBottom => Get(nameof(ButtonGradientBottom), _base.ButtonGradientBottom);
    public override UIColor ButtonHoverTop       => Get(nameof(ButtonHoverTop),       _base.ButtonHoverTop);
    public override UIColor ButtonHoverBottom    => Get(nameof(ButtonHoverBottom),    _base.ButtonHoverBottom);
    public override UIColor ButtonPressedTop     => Get(nameof(ButtonPressedTop),     _base.ButtonPressedTop);
    public override UIColor ButtonPressedBottom  => Get(nameof(ButtonPressedBottom),  _base.ButtonPressedBottom);
    public override UIColor ButtonText           => Get(nameof(ButtonText),           _base.ButtonText);
    public override UIColor CheckMark            => Get(nameof(CheckMark),            _base.CheckMark);
    public override UIColor SliderFill           => Get(nameof(SliderFill),           _base.SliderFill);
    public override UIColor SliderThumb          => Get(nameof(SliderThumb),          _base.SliderThumb);
    public override UIColor WindowBackground     => Get(nameof(WindowBackground),     _base.WindowBackground);
    public override UIColor WindowHeader         => Get(nameof(WindowHeader),         _base.WindowHeader);
    public override UIColor WindowHeaderText     => Get(nameof(WindowHeaderText),     _base.WindowHeaderText);
    public override string  DefaultFont     => Get(nameof(DefaultFont),     _base.DefaultFont);
    public override string  BoldFont        => Get(nameof(BoldFont),        _base.BoldFont);
    public override float   FontSizeBase    => Get(nameof(FontSizeBase),    _base.FontSizeBase);
    public override float   ButtonFontSize  => Get(nameof(ButtonFontSize),  _base.ButtonFontSize);
    public override float   LabelFontSize   => Get(nameof(LabelFontSize),   _base.LabelFontSize);
    public override float   Padding         => Get(nameof(Padding),         _base.Padding);
    public override float   ItemSpacing     => Get(nameof(ItemSpacing),     _base.ItemSpacing);
}
