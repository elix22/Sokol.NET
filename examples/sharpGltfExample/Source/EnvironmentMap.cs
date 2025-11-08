using System.Numerics;
using Sokol;
using static Sokol.SG;
using static Sokol.SLog;

namespace Sokol
{
    /// <summary>
    /// Represents an environment map for Image-Based Lighting (IBL).
    /// Contains pre-filtered diffuse and specular cubemaps plus BRDF lookup tables.
    /// </summary>
    public class EnvironmentMap
    {
        // Cubemap textures
        public sg_image DiffuseCubemap { get; private set; }
        public sg_image SpecularCubemap { get; private set; }
        public sg_image SheenCubemap { get; private set; }  // Optional

        // BRDF lookup tables (2D textures)
        public sg_image GGX_LUT { get; private set; }
        public sg_image Charlie_LUT { get; private set; }  // Optional

        // Image views for texture sampling
        public sg_view DiffuseCubemapView { get; private set; }
        public sg_view SpecularCubemapView { get; private set; }
        public sg_view SheenCubemapView { get; private set; }
        public sg_view GGX_LUTView { get; private set; }
        public sg_view Charlie_LUTView { get; private set; }

        // Samplers
        public sg_sampler CubemapSampler { get; private set; }
        public sg_sampler LUTSampler { get; private set; }

        // Properties
        public int MipCount { get; private set; }
        public float Intensity { get; set; } = 1.0f;
        public Matrix4x4 Rotation { get; set; } = Matrix4x4.Identity;
        public string Name { get; private set; }

        public bool IsLoaded => DiffuseCubemap.id != 0 && SpecularCubemap.id != 0 && GGX_LUT.id != 0;

        public EnvironmentMap(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Initialize the environment map with loaded textures
        /// </summary>
        public void Initialize(
            sg_image diffuseCubemap,
            sg_image specularCubemap,
            sg_image ggxLut,
            int mipCount,
            sg_image sheenCubemap = default,
            sg_image charlieLut = default)
        {
            DiffuseCubemap = diffuseCubemap;
            SpecularCubemap = specularCubemap;
            GGX_LUT = ggxLut;
            SheenCubemap = sheenCubemap;
            Charlie_LUT = charlieLut;
            MipCount = mipCount;

            // Create image views
            DiffuseCubemapView = sg_make_view(new sg_view_desc
            {
                texture = { image = DiffuseCubemap },
                label = $"{Name}-diffuse-view"
            });

            SpecularCubemapView = sg_make_view(new sg_view_desc
            {
                texture = { image = SpecularCubemap },
                label = $"{Name}-specular-view"
            });

            GGX_LUTView = sg_make_view(new sg_view_desc
            {
                texture = { image = GGX_LUT },
                label = $"{Name}-ggx-lut-view"
            });

            if (SheenCubemap.id != 0)
            {
                SheenCubemapView = sg_make_view(new sg_view_desc
                {
                    texture = { image = SheenCubemap },
                    label = $"{Name}-sheen-view"
                });
            }

            if (Charlie_LUT.id != 0)
            {
                Charlie_LUTView = sg_make_view(new sg_view_desc
                {
                    texture = { image = Charlie_LUT },
                    label = $"{Name}-charlie-lut-view"
                });
            }

            // Create samplers
            CubemapSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_LINEAR,
                mag_filter = sg_filter.SG_FILTER_LINEAR,
                mipmap_filter = sg_filter.SG_FILTER_LINEAR,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_w = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label = $"{Name}-cubemap-sampler"
            });

            LUTSampler = sg_make_sampler(new sg_sampler_desc
            {
                min_filter = sg_filter.SG_FILTER_LINEAR,
                mag_filter = sg_filter.SG_FILTER_LINEAR,
                mipmap_filter = sg_filter.SG_FILTER_NEAREST,
                wrap_u = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                wrap_v = sg_wrap.SG_WRAP_CLAMP_TO_EDGE,
                label = $"{Name}-lut-sampler"
            });

            Info($"[IBL] Environment '{Name}' initialized with {MipCount} mip levels");
        }

        /// <summary>
        /// Create rotation matrix from Y-axis rotation in degrees
        /// </summary>
        public static Matrix4x4 CreateRotationMatrix(float degrees)
        {
            float radians = degrees * (MathF.PI / 180.0f);
            return Matrix4x4.CreateRotationY(radians);
        }

        public void Dispose()
        {
            if (DiffuseCubemap.id != 0)
            {
                sg_destroy_image(DiffuseCubemap);
                DiffuseCubemap = default;
            }

            if (SpecularCubemap.id != 0)
            {
                sg_destroy_image(SpecularCubemap);
                SpecularCubemap = default;
            }

            if (SheenCubemap.id != 0)
            {
                sg_destroy_image(SheenCubemap);
                SheenCubemap = default;
            }

            if (GGX_LUT.id != 0)
            {
                sg_destroy_image(GGX_LUT);
                GGX_LUT = default;
            }

            if (Charlie_LUT.id != 0)
            {
                sg_destroy_image(Charlie_LUT);
                Charlie_LUT = default;
            }

            if (DiffuseCubemapView.id != 0)
            {
                sg_destroy_view(DiffuseCubemapView);
                DiffuseCubemapView = default;
            }

            if (SpecularCubemapView.id != 0)
            {
                sg_destroy_view(SpecularCubemapView);
                SpecularCubemapView = default;
            }

            if (SheenCubemapView.id != 0)
            {
                sg_destroy_view(SheenCubemapView);
                SheenCubemapView = default;
            }

            if (GGX_LUTView.id != 0)
            {
                sg_destroy_view(GGX_LUTView);
                GGX_LUTView = default;
            }

            if (Charlie_LUTView.id != 0)
            {
                sg_destroy_view(Charlie_LUTView);
                Charlie_LUTView = default;
            }

            if (CubemapSampler.id != 0)
            {
                sg_destroy_sampler(CubemapSampler);
                CubemapSampler = default;
            }

            if (LUTSampler.id != 0)
            {
                sg_destroy_sampler(LUTSampler);
                LUTSampler = default;
            }

            Info($"[IBL] Environment '{Name}' disposed");
        }
    }
}
