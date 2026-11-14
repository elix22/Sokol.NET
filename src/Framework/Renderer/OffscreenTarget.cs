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
        /// <summary>The colour image's format: RGBA8, or a float one for an HDR target (G8.3).</summary>
        public sg_pixel_format ColorFormat { get; private set; } = sg_pixel_format.SG_PIXELFORMAT_RGBA8;
        /// <summary>A float target (G8.3, docs HDR_TONEMAP_DESIGN.md): the scene drawn before its tone curve.</summary>
        public bool IsHdr => ColorFormat != sg_pixel_format.SG_PIXELFORMAT_RGBA8;
        // The target whose depth image this one draws into (an HDR companion of a view): its depth is not this one's own.
        private OffscreenTarget? _depthOf;

        public void Create(int width, int height) => Create(width, height, sg_pixel_format.SG_PIXELFORMAT_RGBA8, null);

        /// <summary>
        /// A target of <paramref name="format"/>; with <paramref name="depthOf"/> it draws into that target's depth image
        /// (which must be of the same size) instead of one of its own — an HDR companion of a view, whose picture is then
        /// tonemapped into the view's target with the depth kept.
        /// </summary>
        public void Create(int width, int height, sg_pixel_format format, OffscreenTarget? depthOf)
        {
            Destroy();
            Width = width;
            Height = height;
            ColorFormat = format;
            _depthOf = depthOf;

            // sample_count is pinned to 1: a render-target image otherwise inherits the swapchain's
            // sample count (sokol's environment default — e.g. 4 in an MSAA standalone), which would
            // mismatch the single-sampled pipelines that draw into this target. Offscreen 3D targets
            // here are single-sampled by design (the swapchain keeps its own MSAA for presentation).
            ColorImage = sg_make_image(new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = format,
                sample_count = 1,
                usage = { color_attachment = true },
                label = depthOf == null ? "offscreen-color" : "offscreen-hdr-color"
            });

            if (depthOf != null)
            {
                DepthImage = depthOf.DepthImage;
                DepthAttView = depthOf.DepthAttView;
            }
            else DepthImage = sg_make_image(new sg_image_desc
            {
                width = width,
                height = height,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sample_count = 1,
                usage = { depth_stencil_attachment = true },
                label = "offscreen-depth"
            });

            ColorAttView = sg_make_view(new sg_view_desc
            {
                color_attachment = new sg_image_view_desc { image = ColorImage },
                label = "offscreen-color-att"
            });

            if (depthOf == null)
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
                Create(width, height, ColorFormat, _depthOf);
        }

        /// <summary>A companion of <paramref name="view"/> in <paramref name="format"/>, sharing its depth: made or remade
        /// when the view's size or depth image changed.</summary>
        public void Follow(OffscreenTarget view, sg_pixel_format format)
        {
            if (!IsValid || Width != view.Width || Height != view.Height || ColorFormat != format || DepthImage.id != view.DepthImage.id)
                Create(view.Width, view.Height, format, view);
        }

        public void Destroy()
        {
            if (ColorImage.id != 0) sg_destroy_image(ColorImage);
            if (DepthImage.id != 0 && _depthOf == null) sg_destroy_image(DepthImage);   // a shared depth is its owner's
            if (ColorAttView.id != 0) sg_destroy_view(ColorAttView);
            if (DepthAttView.id != 0 && _depthOf == null) sg_destroy_view(DepthAttView);
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
                        // STORE (not DONTCARE) so a follow-up pass on this target — e.g. the
                        // post-opaque transmission pass — can LOAD this depth and test refractive
                        // geometry against the opaque scene. Negligible cost; harmless to other passes.
                        store_action = sg_store_action.SG_STOREACTION_STORE,
                        clear_value = 1.0f
                    }
                }
            };
        }
    }
}
