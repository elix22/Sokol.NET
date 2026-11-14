using System;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server.Lighting
{
    /// <summary>
    /// Point-light shadow storage represented as a depth 2D-array.
    /// Each point light consumes 6 consecutive slices (cube faces).
    /// </summary>
    public sealed class CubeShadowArray
    {
        public const int MaxPointShadowLights = 4;
        public const int FacesPerLight = 6;
        public const int TotalSlices = MaxPointShadowLights * FacesPerLight;
        public const int DefaultFaceSize = 512;
        /// <summary>The size of a slice now (G8.5: the quality level's shadow resolution; <see cref="Resize"/>).</summary>
        public int FaceSize { get; private set; } = DefaultFaceSize;

        private readonly sg_view[] _depthFaceViews = new sg_view[TotalSlices];

        public sg_image DepthImage { get; private set; }
        public sg_view TextureView { get; private set; }

        public bool IsValid => DepthImage.id != 0;

        public void Init()
        {
            if (IsValid) return;

            DepthImage = sg_make_image(new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_ARRAY,
                width = FaceSize,
                height = FaceSize,
                num_slices = TotalSlices,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_DEPTH,
                sample_count = 1,
                usage = { depth_stencil_attachment = true },
                label = "cube-shadow-array-depth"
            });

            for (int i = 0; i < TotalSlices; i++)
            {
                _depthFaceViews[i] = sg_make_view(new sg_view_desc
                {
                    depth_stencil_attachment = new sg_image_view_desc
                    {
                        image = DepthImage,
                        slice = i
                    },
                    label = $"cube-shadow-face-{i}"
                });
            }

            TextureView = sg_make_view(new sg_view_desc
            {
                texture = new sg_texture_view_desc
                {
                    image = DepthImage
                },
                label = "cube-shadow-array-tex"
            });
        }

        public sg_view GetDepthFaceView(int pointLightIndex, int faceIndex)
        {
            if ((uint)pointLightIndex >= (uint)MaxPointShadowLights)
                throw new ArgumentOutOfRangeException(nameof(pointLightIndex));
            if ((uint)faceIndex >= (uint)FacesPerLight)
                throw new ArgumentOutOfRangeException(nameof(faceIndex));

            int slice = pointLightIndex * FacesPerLight + faceIndex;
            return _depthFaceViews[slice];
        }

        /// <summary>Slices of <paramref name="size"/> from now on: the texture made again (its contents gone — the shadow
        /// passes draw it every frame), the old one freed.</summary>
        public void Resize(int size)
        {
            if (size == FaceSize) return;
            bool was = IsValid;
            Shutdown();
            FaceSize = size;
            if (was) Init();
        }

        public void Shutdown()
        {
            for (int i = 0; i < _depthFaceViews.Length; i++)
            {
                if (_depthFaceViews[i].id != 0)
                {
                    sg_destroy_view(_depthFaceViews[i]);
                    _depthFaceViews[i] = default;
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