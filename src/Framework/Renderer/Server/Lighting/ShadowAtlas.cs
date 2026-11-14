using System;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    /// <summary>
    /// Depth-only shadow atlas stored as a 2D array texture.
    /// Slice layout (M2): 0..3 directional cascades, 4..11 spot slices.
    /// </summary>
    public sealed class ShadowAtlas
    {
        public const int DirectionalCascades = 4;
        public const int SpotSlices = 8;
        public const int TotalSlices = DirectionalCascades + SpotSlices;
        public const int DefaultSliceSize = 2048;
        /// <summary>The size of a slice now (G8.5: the quality level's shadow resolution; <see cref="Resize"/>).</summary>
        public int SliceSize { get; private set; } = DefaultSliceSize;

        private readonly sg_view[] _depthSliceViews = new sg_view[TotalSlices];

        public sg_image DepthImage { get; private set; }
        public sg_view TextureView { get; private set; }

        public bool IsValid => DepthImage.id != 0;

        public void Init()
        {
            if (IsValid) return;

            DepthImage = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_ARRAY,
                width = SliceSize,
                height = SliceSize,
                num_slices = TotalSlices,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sample_count = 1,
                usage = { depth_stencil_attachment = true },
                label = "shadow-atlas-depth"
            });

            for (int i = 0; i < TotalSlices; i++)
            {
                _depthSliceViews[i] = sg_make_view(new sg_view_desc
                {
                    depth_stencil_attachment = new sg_image_view_desc
                    {
                        image = DepthImage,
                        slice = i
                    },
                    label = $"shadow-atlas-depth-slice-{i}"
                });
            }

            TextureView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc
                {
                    image = DepthImage
                },
                label = "shadow-atlas-tex"
            });
        }

        public sg_view GetDepthSliceView(int slice)
        {
            if ((uint)slice >= (uint)TotalSlices)
                throw new ArgumentOutOfRangeException(nameof(slice));
            return _depthSliceViews[slice];
        }

        /// <summary>Slices of <paramref name="size"/> from now on: the texture made again (its contents gone — the shadow
        /// passes draw it every frame), the old one freed.</summary>
        public void Resize(int size)
        {
            if (size == SliceSize) return;
            bool was = IsValid;
            Shutdown();
            SliceSize = size;
            if (was) Init();
        }

        public void Shutdown()
        {
            for (int i = 0; i < _depthSliceViews.Length; i++)
            {
                if (_depthSliceViews[i].id != 0)
                {
                    sg_destroy_view(_depthSliceViews[i]);
                    _depthSliceViews[i] = default;
                }
            }

            if (TextureView.id != 0)
            {
                sg_destroy_view(TextureView);
                TextureView = default;
            }

            if (DepthImage.id != 0)
            {
                sg_destroy_image(DepthImage);
                DepthImage = default;
            }
        }
    }
}