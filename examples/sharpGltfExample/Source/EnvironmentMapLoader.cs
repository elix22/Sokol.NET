using System;
using System.Numerics;
using Sokol;
using static Sokol.SG;
using static Sokol.SLog;
using static Sokol.StbImage;

namespace Sokol
{
    /// <summary>
    /// Loads environment maps for Image-Based Lighting (IBL).
    /// For now, creates simple procedural environment maps for testing.
    /// </summary>
    public static class EnvironmentMapLoader
    {
        /// <summary>
        /// Create a simple test environment with basic gradient lighting.
        /// This is a placeholder until we have proper pre-filtered HDR environment maps.
        /// </summary>
        public static unsafe EnvironmentMap CreateTestEnvironment(string name = "test")
        {
            Info($"[IBL] Creating test environment '{name}'...");

            var envMap = new EnvironmentMap(name);

            // Create simple diffuse cubemap (single mip, low-res gradient)
            var diffuseCubemap = CreateGradientCubemap(64, "diffuse");

            // Create simple specular cubemap (multiple mips for roughness)
            var (specularCubemap, mipCount) = CreateMipmappedCubemap(128, "specular");

            // Create BRDF LUT (procedural approximation)
            var ggxLut = CreateBRDFLUT(256);

            envMap.Initialize(
                diffuseCubemap,
                specularCubemap,
                ggxLut,
                mipCount
            );

            return envMap;
        }

        /// <summary>
        /// Create a simple gradient cubemap (sky-like lighting from top)
        /// </summary>
        private static unsafe sg_image CreateGradientCubemap(int size, string label)
        {
            int faceSize = size * size * 4; // RGBA per face
            int totalSize = faceSize * 6; // 6 faces
            byte[] allFaces = new byte[totalSize];

            // Generate gradient for each face
            // Simple sky-like gradient: brighter at top, darker at bottom
            for (int face = 0; face < 6; face++)
            {
                byte[] faceData = new byte[faceSize];
                FillGradientFace(faceData, size, face);
                Array.Copy(faceData, 0, allFaces, face * faceSize, faceSize);
            }

            var desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_CUBE,
                width = size,
                height = size,
                num_slices = 6,
                num_mipmaps = 1,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                label = $"ibl-{label}-cubemap"
            };

            fixed (byte* ptr = allFaces)
            {
                desc.data.mip_levels[0] = new sg_range
                {
                    ptr = ptr,
                    size = (nuint)totalSize
                };
            }

            return sg_make_image(desc);
        }

        private static void FillGradientFace(byte[] data, int size, int face)
        {
            // Create a simple gradient based on face orientation
            // +Y (top) = bright, -Y (bottom) = dark, sides = medium
            Vector3 baseColor = face switch
            {
                2 => new Vector3(0.8f, 0.85f, 1.0f),  // +Y (top) - sky blue
                3 => new Vector3(0.3f, 0.25f, 0.2f),  // -Y (bottom) - ground
                _ => new Vector3(0.5f, 0.55f, 0.65f)  // sides - horizon
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = (y * size + x) * 4;

                    // Add some vertical gradient
                    float t = y / (float)size;
                    float brightness = face == 2 ? 1.0f : (face == 3 ? 0.3f : (1.0f - t * 0.3f));

                    Vector3 color = baseColor * brightness;

                    data[idx + 0] = (byte)(Math.Clamp(color.X * 255, 0, 255));
                    data[idx + 1] = (byte)(Math.Clamp(color.Y * 255, 0, 255));
                    data[idx + 2] = (byte)(Math.Clamp(color.Z * 255, 0, 255));
                    data[idx + 3] = 255;
                }
            }
        }

        /// <summary>
        /// Create a mipmapped cubemap for specular reflections
        /// Each mip level represents a different roughness level
        /// </summary>
        private static unsafe (sg_image, int) CreateMipmappedCubemap(int baseSize, string label)
        {
            // Calculate mip count
            int mipCount = (int)Math.Floor(Math.Log2(baseSize)) + 1;
            mipCount = Math.Min(mipCount, 8); // Limit to 8 mips

            var desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_CUBE,
                width = baseSize,
                height = baseSize,
                num_slices = 6,
                num_mipmaps = mipCount,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                label = $"ibl-{label}-cubemap-mip"
            };

            // Generate data for each mip level
            for (int mip = 0; mip < mipCount; mip++)
            {
                int mipSize = Math.Max(1, baseSize >> mip);
                int mipFaceSize = mipSize * mipSize * 4; // RGBA per face
                int mipTotalSize = mipFaceSize * 6; // All 6 faces
                byte[] mipAllFaces = new byte[mipTotalSize];

                // Blur factor increases with mip level (simulating roughness)
                float blur = mip / (float)(mipCount - 1);

                for (int face = 0; face < 6; face++)
                {
                    byte[] faceData = new byte[mipFaceSize];
                    FillBlurredFace(faceData, mipSize, face, blur);
                    Array.Copy(faceData, 0, mipAllFaces, face * mipFaceSize, mipFaceSize);
                }

                fixed (byte* ptr = mipAllFaces)
                {
                    desc.data.mip_levels[mip] = new sg_range
                    {
                        ptr = ptr,
                        size = (nuint)mipTotalSize
                    };
                }
            }

            return (sg_make_image(desc), mipCount);
        }

        private static void FillBlurredFace(byte[] data, int size, int face, float blur)
        {
            // Similar to gradient but with blur factor applied
            Vector3 baseColor = face switch
            {
                2 => new Vector3(0.8f, 0.85f, 1.0f),
                3 => new Vector3(0.3f, 0.25f, 0.2f),
                _ => new Vector3(0.5f, 0.55f, 0.65f)
            };

            // Reduce contrast with blur (simulates rough reflections)
            float contrast = 1.0f - blur * 0.7f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = (y * size + x) * 4;

                    float t = y / (float)size;
                    float brightness = face == 2 ? 1.0f : (face == 3 ? 0.3f : (1.0f - t * 0.3f));
                    brightness = 0.5f + (brightness - 0.5f) * contrast;

                    Vector3 color = baseColor * brightness;

                    data[idx + 0] = (byte)(Math.Clamp(color.X * 255, 0, 255));
                    data[idx + 1] = (byte)(Math.Clamp(color.Y * 255, 0, 255));
                    data[idx + 2] = (byte)(Math.Clamp(color.Z * 255, 0, 255));
                    data[idx + 3] = 255;
                }
            }
        }

        /// <summary>
        /// Create a procedural BRDF LUT texture.
        /// This is a simplified version - ideally should be pre-computed offline.
        /// </summary>
        private static unsafe sg_image CreateBRDFLUT(int size)
        {
            int dataSize = size * size * 4; // RGBA
            byte[] lutData = new byte[dataSize];

            // Generate split-sum approximation LUT
            // X axis = NdotV, Y axis = roughness
            // R channel = scale, G channel = bias
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = (y * size + x) * 4;

                    float NdotV = x / (float)(size - 1);
                    float roughness = y / (float)(size - 1);

                    // Simplified approximation of the split-sum BRDF integral
                    // This is a rough approximation - proper LUT should be pre-computed
                    float scale = 1.0f - roughness * (1.0f - NdotV);
                    float bias = roughness * (1.0f - NdotV) * 0.5f;

                    lutData[idx + 0] = (byte)(Math.Clamp(scale * 255, 0, 255));
                    lutData[idx + 1] = (byte)(Math.Clamp(bias * 255, 0, 255));
                    lutData[idx + 2] = 0;
                    lutData[idx + 3] = 255;
                }
            }

            var desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_2D,
                width = size,
                height = size,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                label = "ibl-ggx-lut"
            };

            fixed (byte* ptr = lutData)
            {
                desc.data.mip_levels[0] = new sg_range
                {
                    ptr = ptr,
                    size = (nuint)dataSize
                };
            }

            return sg_make_image(desc);
        }

        /// <summary>
        /// Load environment from PNG files (future implementation)
        /// </summary>
        public static EnvironmentMap LoadFromFiles(
            string diffusePattern,
            string specularMipPattern,
            string ggxLutPath,
            string charlieLutPath = null)
        {
            // TODO: Implement file loading using StbImage
            // For now, return test environment
            Warning("[IBL] File loading not yet implemented, using test environment");
            return CreateTestEnvironment("loaded");
        }
    }
}
