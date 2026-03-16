using static Sokol.SG;

namespace GameEditor.Framework.Renderer
{
    /// <summary>
    /// An offscreen render target: color image + depth image + views + pass.
    /// </summary>
    public unsafe class OffscreenTarget
    {
        public sg_image ColorImage { get; private set; }
        public sg_image DepthImage { get; private set; }
        public sg_view ColorAttView { get; private set; }
        public sg_view DepthAttView { get; private set; }
        public sg_view TexView { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public bool IsValid => ColorImage.id != 0;

        public void Create(int width, int height)
        {
            Destroy();
            Width = width;
            Height = height;

            ColorImage = sg_make_image(new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                usage = { color_attachment = true },
                label = "offscreen-color"
            });

            DepthImage = sg_make_image(new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                usage = { depth_stencil_attachment = true },
                label = "offscreen-depth"
            });

            ColorAttView = sg_make_view(new sg_view_desc
            {
                color_attachment = new sg_image_view_desc { image = ColorImage },
                label = "offscreen-color-att"
            });

            DepthAttView = sg_make_view(new sg_view_desc
            {
                depth_stencil_attachment = new sg_image_view_desc { image = DepthImage },
                label = "offscreen-depth-att"
            });

            TexView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc { image = ColorImage },
                label = "offscreen-tex"
            });
        }

        public void Resize(int width, int height)
        {
            if (Width != width || Height != height)
                Create(width, height);
        }

        public void Destroy()
        {
            if (ColorImage.id != 0) sg_destroy_image(ColorImage);
            if (DepthImage.id != 0) sg_destroy_image(DepthImage);
            if (ColorAttView.id != 0) sg_destroy_view(ColorAttView);
            if (DepthAttView.id != 0) sg_destroy_view(DepthAttView);
            if (TexView.id != 0) sg_destroy_view(TexView);
            ColorImage = default;
            DepthImage = default;
            ColorAttView = default;
            DepthAttView = default;
            TexView = default;
        }

        public sg_pass MakePass(sg_color clearColor)
        {
            return new sg_pass
            {
                attachments =
                {
                    colors = { [0] = ColorAttView },
                    depth_stencil = DepthAttView
                },
                action =
                {
                    colors = { [0] = new sg_color_attachment_action
                    {
                        load_action = sg_load_action.SG_LOADACTION_CLEAR,
                        store_action = sg_store_action.SG_STOREACTION_STORE,
                        clear_value = clearColor
                    }},
                    depth = new sg_depth_attachment_action
                    {
                        load_action = sg_load_action.SG_LOADACTION_CLEAR,
                        store_action = sg_store_action.SG_STOREACTION_DONTCARE,
                        clear_value = 1.0f
                    }
                }
            };
        }
    }
}
