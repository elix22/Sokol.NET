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
using Sokol;


public unsafe class Texture
{
    public sg_image Image { get; private set; } = default;
    public sg_view View { get; private set; } = default;

    public sg_sampler Sampler { get; private set; } = default;

    public bool IsValid => Image.id != 0;

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

    public Texture(string filePath)
    {
        filePath = util_get_file_path(filePath);
        FileSystem.Instance.LoadFile(filePath, OnTextureLoaded);
    }

    private void Set(byte* data, int width, int height)
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

    unsafe void OnTextureLoaded(string filePath, byte[]? buffer, FileLoadStatus status)
    {
        if (status == FileLoadStatus.Success && buffer != null)
        {
            Console.WriteLine($"Assimp: Texture file '{filePath}' loaded successfully, size: {buffer.Length} bytes");
            // Further processing of the texture data would go here
            int png_width = 0, png_height = 0, channels = 0, desired_channels = 4;
            byte* pixels = stbi_load_csharp(
                in buffer[0],
                (int)buffer.Length,
                ref png_width,
                ref png_height,
                ref channels,
                desired_channels
            );

            if (pixels == null)
            {
                Console.WriteLine($"Assimp: Failed to decode texture file: {filePath}");
                return;
            }

            Set(pixels, png_width, png_height);

            // Free the native STB image data
            stbi_image_free_csharp(pixels);
        }
        else
        {
            Console.WriteLine($"Assimp: Failed to load texture file: {status}");
        }

    }

}
