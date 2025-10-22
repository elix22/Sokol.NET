using static Sokol.SG.sg_index_type;
using static Sokol.SG.sg_cull_mode;
using static Sokol.SG.sg_compare_func;
using static Sokol.Utils;
using static Sokol.SLog;
using static Sokol.SDebugUI;

using static Sokol.SFetch;
using static Sokol.SDebugText;
using static Sokol.StbImage;
using static Sokol.SG;
using static Sokol.SG.sg_filter;
using static Sokol.SG.sg_wrap;


public unsafe class Texture
{
    public sg_image Image { get; private set; }
    public sg_view View { get; private set; }

    public sg_sampler Sampler { get; private set; }

    public Texture(byte* data, int width, int height)
    {
        Image = sg_make_image(new sg_image_desc()
        {
            width = width,
            height = height,
            pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
            data = { mip_levels = { [0] = new sg_range() { ptr = data, size = (uint)(width * height * 4) } } },
            label = "sokol-texture"
        });

        View = sg_make_view(new sg_view_desc()
        {
            texture = new sg_texture_view_desc { image = Image },
            label = "sokol-texture-view",
        });

        Sampler = sg_make_sampler(new sg_sampler_desc()
        {
            min_filter = SG_FILTER_LINEAR,
            mag_filter = SG_FILTER_LINEAR,
            // wrap_u = SG_WRAP_CLAMP_TO_EDGE,
            // wrap_v = SG_WRAP_CLAMP_TO_EDGE,
        });
    }

}
