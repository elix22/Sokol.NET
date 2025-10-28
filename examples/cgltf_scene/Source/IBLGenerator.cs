using System;
using System.Numerics;
using static Sokol.SG;

namespace Sokol
{
    /// <summary>
    /// Generates simple procedural IBL (Image-Based Lighting) textures
    /// This creates a basic sky/ground gradient environment without needing external HDR files
    /// </summary>
    public static class IBLGenerator
    {
        /// <summary>
        /// Generate a simple irradiance cubemap (for diffuse IBL)
        /// Creates a gradient from sky blue (top) to ground gray (bottom)
        /// </summary>
        public static sg_image GenerateIrradianceCubemap(int size = 32)
        {
            Vector3 skyColor = new Vector3(0.4f, 0.6f, 0.9f);    // Light blue sky
            Vector3 groundColor = new Vector3(0.3f, 0.3f, 0.3f);  // Gray ground
            
            byte[] pixels = new byte[size * size * 4 * 6]; // RGBA * 6 faces
            
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Convert pixel to direction vector
                        Vector3 dir = CubemapPixelToDirection(face, x, y, size);
                        
                        // Use Y component for gradient (up = 1, down = -1)
                        float t = dir.Y * 0.5f + 0.5f; // Map [-1,1] to [0,1]
                        Vector3 color = Vector3.Lerp(groundColor, skyColor, t);
                        
                        int idx = (face * size * size + y * size + x) * 4;
                        pixels[idx + 0] = (byte)(color.X * 255);
                        pixels[idx + 1] = (byte)(color.Y * 255);
                        pixels[idx + 2] = (byte)(color.Z * 255);
                        pixels[idx + 3] = 255;
                    }
                }
            }
            
            sg_image_desc desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_CUBE,
                width = size,
                height = size,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                num_mipmaps = 1,
            };
            
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    desc.data.mip_levels[0] = new sg_range { ptr = ptr, size = (nuint)pixels.Length };
                }
                
                return sg_make_image(desc);
            }
        }
        
        /// <summary>
        /// Generate a simple prefiltered environment map (for specular IBL)
        /// Creates multiple mip levels with increasing blur
        /// </summary>
        public static sg_image GeneratePrefilterCubemap(int baseSize = 128)
        {
            int numMips = 5; // 128, 64, 32, 16, 8
            
            sg_image_desc desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_CUBE,
                width = baseSize,
                height = baseSize,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                num_mipmaps = numMips,
            };
            
            Vector3 skyColor = new Vector3(0.5f, 0.7f, 1.0f);    // Bright sky for reflections
            Vector3 groundColor = new Vector3(0.2f, 0.2f, 0.2f);
            
            unsafe
            {
                for (int mip = 0; mip < numMips; mip++)
                {
                    int mipSize = baseSize >> mip;
                    float roughness = (float)mip / (numMips - 1);
                    
                    byte[] pixels = new byte[mipSize * mipSize * 4 * 6];
                    
                    for (int face = 0; face < 6; face++)
                    {
                        for (int y = 0; y < mipSize; y++)
                        {
                            for (int x = 0; x < mipSize; x++)
                            {
                                Vector3 dir = CubemapPixelToDirection(face, x, y, mipSize);
                                
                                // Gradient with some variation based on roughness
                                float t = dir.Y * 0.5f + 0.5f;
                                Vector3 color = Vector3.Lerp(groundColor, skyColor, t);
                                
                                // Dim the color based on roughness (rougher = dimmer)
                                color *= (1.0f - roughness * 0.5f);
                                
                                int idx = (face * mipSize * mipSize + y * mipSize + x) * 4;
                                pixels[idx + 0] = (byte)(Math.Min(color.X * 255, 255));
                                pixels[idx + 1] = (byte)(Math.Min(color.Y * 255, 255));
                                pixels[idx + 2] = (byte)(Math.Min(color.Z * 255, 255));
                                pixels[idx + 3] = 255;
                            }
                        }
                    }
                    
                    fixed (byte* ptr = pixels)
                    {
                        desc.data.mip_levels[mip] = new sg_range { ptr = ptr, size = (nuint)pixels.Length };
                    }
                }
                
                return sg_make_image(desc);
            }
        }
        
        /// <summary>
        /// Generate BRDF integration lookup table
        /// This is a 2D texture used for split-sum approximation in IBL
        /// </summary>
        public static sg_image GenerateBRDFLUT(int size = 512)
        {
            byte[] pixels = new byte[size * size * 2]; // RG format (only need 2 channels)
            
            for (int y = 0; y < size; y++)
            {
                float NdotV = (y + 0.5f) / size;
                
                for (int x = 0; x < size; x++)
                {
                    float roughness = (x + 0.5f) / size;
                    
                    // Simplified BRDF integration approximation
                    // This is a rough approximation - ideally you'd compute this properly
                    Vector2 brdf = IntegrateBRDF(NdotV, roughness);
                    
                    int idx = (y * size + x) * 2;
                    pixels[idx + 0] = (byte)(brdf.X * 255);
                    pixels[idx + 1] = (byte)(brdf.Y * 255);
                }
            }
            
            sg_image_desc desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_2D,
                width = size,
                height = size,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RG8,
                num_mipmaps = 1,
            };
            
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    desc.data.mip_levels[0] = new sg_range { ptr = ptr, size = (nuint)pixels.Length };
                }
                
                return sg_make_image(desc);
            }
        }
        
        // Helper: Convert cubemap pixel coordinates to 3D direction
        private static Vector3 CubemapPixelToDirection(int face, int x, int y, int size)
        {
            float u = (x + 0.5f) / size * 2.0f - 1.0f;
            float v = (y + 0.5f) / size * 2.0f - 1.0f;
            
            Vector3 dir = face switch
            {
                0 => new Vector3(1, -v, -u),   // +X
                1 => new Vector3(-1, -v, u),   // -X
                2 => new Vector3(u, 1, v),     // +Y
                3 => new Vector3(u, -1, -v),   // -Y
                4 => new Vector3(u, -v, 1),    // +Z
                5 => new Vector3(-u, -v, -1),  // -Z
                _ => Vector3.UnitZ
            };
            
            return Vector3.Normalize(dir);
        }
        
        // Simplified BRDF integration (approximation)
        private static Vector2 IntegrateBRDF(float NdotV, float roughness)
        {
            // This is a simplified approximation
            // For production, you'd want to properly integrate the BRDF
            
            const int SAMPLE_COUNT = 64;
            Vector2 result = Vector2.Zero;
            
            for (int i = 0; i < SAMPLE_COUNT; i++)
            {
                // Hammersley sequence for sampling
                float u1 = (float)i / SAMPLE_COUNT;
                float u2 = ReverseBits((uint)i) / (float)0x100000000;
                
                // Importance sampling GGX
                float a = roughness * roughness;
                float phi = 2.0f * MathF.PI * u1;
                float cosTheta = MathF.Sqrt((1.0f - u2) / (1.0f + (a * a - 1.0f) * u2));
                float sinTheta = MathF.Sqrt(1.0f - cosTheta * cosTheta);
                
                Vector3 H = new Vector3(
                    MathF.Cos(phi) * sinTheta,
                    MathF.Sin(phi) * sinTheta,
                    cosTheta
                );
                
                Vector3 V = new Vector3(MathF.Sqrt(1.0f - NdotV * NdotV), 0, NdotV);
                Vector3 L = 2.0f * Vector3.Dot(V, H) * H - V;
                
                float NdotL = Math.Max(L.Z, 0.0f);
                float NdotH = Math.Max(H.Z, 0.0f);
                float VdotH = Math.Max(Vector3.Dot(V, H), 0.0f);
                
                if (NdotL > 0.0f)
                {
                    float G = GeometrySmith(NdotV, NdotL, roughness);
                    float G_Vis = (G * VdotH) / (NdotH * NdotV);
                    float Fc = MathF.Pow(1.0f - VdotH, 5.0f);
                    
                    result.X += (1.0f - Fc) * G_Vis;
                    result.Y += Fc * G_Vis;
                }
            }
            
            return result / SAMPLE_COUNT;
        }
        
        private static float GeometrySmith(float NdotV, float NdotL, float roughness)
        {
            float a = roughness;
            float k = (a * a) / 2.0f;
            
            float ggxV = NdotV / (NdotV * (1.0f - k) + k);
            float ggxL = NdotL / (NdotL * (1.0f - k) + k);
            
            return ggxV * ggxL;
        }
        
        private static uint ReverseBits(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x55555555) << 1) | ((bits & 0xAAAAAAAA) >> 1);
            bits = ((bits & 0x33333333) << 2) | ((bits & 0xCCCCCCCC) >> 2);
            bits = ((bits & 0x0F0F0F0F) << 4) | ((bits & 0xF0F0F0F0) >> 4);
            bits = ((bits & 0x00FF00FF) << 8) | ((bits & 0xFF00FF00) >> 8);
            return bits;
        }
    }
}
