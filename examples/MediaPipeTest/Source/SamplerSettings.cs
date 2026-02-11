using static Sokol.SG;

namespace Sokol;

public class SamplerSettings
{
    public sg_filter MinFilter { get; set; } = sg_filter.SG_FILTER_LINEAR;
    public sg_filter MagFilter { get; set; } = sg_filter.SG_FILTER_LINEAR;
    public sg_filter MipmapFilter { get; set; } = sg_filter.SG_FILTER_LINEAR;
    public sg_wrap WrapU { get; set; } = sg_wrap.SG_WRAP_REPEAT;
    public sg_wrap WrapV { get; set; } = sg_wrap.SG_WRAP_REPEAT;

    public override int GetHashCode()
    {
        return HashCode.Combine(MinFilter, MagFilter, MipmapFilter, WrapU, WrapV);
    }

    public override bool Equals(object? obj)
    {
        if (obj is SamplerSettings other)
        {
            return MinFilter == other.MinFilter &&
                   MagFilter == other.MagFilter &&
                   MipmapFilter == other.MipmapFilter &&
                   WrapU == other.WrapU &&
                   WrapV == other.WrapV;
        }
        return false;
    }
}
