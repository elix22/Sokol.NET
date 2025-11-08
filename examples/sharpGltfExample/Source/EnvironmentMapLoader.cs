using System;
using System.Numerics;
using Sokol;
using static Sokol.SG;
using static Sokol.SLog;
using static Sokol.StbImage;
using static Sokol.TinyEXR;

namespace Sokol
{
    /// <summary>
    /// Loads environment maps for Image-Based Lighting (IBL).
    /// Supports HDR panorama loading via FileSystem and procedural fallback.
    /// </summary>
    public static class EnvironmentMapLoader
    {
        /// <summary>
        /// Callback for HDR environment loading completion
        /// </summary>
        public delegate void HDRLoadCallback(EnvironmentMap? environmentMap);

        /// <summary>
        /// Load HDR environment map asynchronously from file.
        /// Uses FileSystem for async loading, then converts panorama to cubemaps.
        /// </summary>
        public static unsafe void LoadHDREnvironmentAsync(string hdrFileName, HDRLoadCallback onComplete)
        {
            Info($"[IBL] Starting async load of HDR: {hdrFileName}");

            FileSystem.Instance.LoadFile(hdrFileName, (filePath, data, status) =>
            {
                if (status != FileLoadStatus.Success || data == null)
                {
                    Warning($"[IBL] Failed to load HDR file: {hdrFileName} (status: {status})");
                    onComplete?.Invoke(null);
                    return;
                }

                Info($"[IBL] HDR file loaded ({data.Length} bytes), decoding...");

                try
                {
                    // Decode HDR image using stb_image
                    int width = 0, height = 0, channels = 0;
                    byte* pixels;
                    
                    fixed (byte* dataPtr = data)
                    {
                        pixels = stbi_load_csharp(in *dataPtr, data.Length, ref width, ref height, ref channels, 4);
                    }

                    if (pixels == null)
                    {
                        string error = stbi_failure_reason_csharp();
                        Warning($"[IBL] Failed to decode HDR image: {error}");
                        onComplete?.Invoke(null);
                        return;
                    }

                    Info($"[IBL] HDR decoded: {width}x{height}, {channels} channels");

                    // Convert panorama to cubemaps
                    var envMap = ConvertPanoramaToCubemap(pixels, width, height, "hdr-environment");
                    
                    stbi_image_free_csharp(pixels);
                    
                    if (envMap != null && envMap.IsLoaded)
                    {
                        Info("[IBL] Successfully created environment map from HDR panorama");
                        onComplete?.Invoke(envMap);
                    }
                    else
                    {
                        Warning("[IBL] Failed to create environment map from HDR, using procedural");
                        onComplete?.Invoke(null);
                    }
                }
                catch (Exception ex)
                {
                    Warning($"[IBL] Error processing HDR: {ex.Message}");
                    onComplete?.Invoke(null);
                }
            });
        }

        /// <summary>
        /// Load EXR environment map asynchronously from file.
        /// Uses FileSystem for async loading, then converts EXR float data to cubemaps.
        /// EXR files load much faster than HDR since they can be pre-filtered offline.
        /// </summary>
        public static unsafe void LoadEXREnvironmentAsync(string exrFileName, HDRLoadCallback onComplete)
        {
            Info($"[IBL] Starting async load of EXR: {exrFileName}");

            FileSystem.Instance.LoadFile(exrFileName, (filePath, data, status) =>
            {
                if (status != FileLoadStatus.Success || data == null)
                {
                    Warning($"[IBL] Failed to load EXR file: {exrFileName} (status: {status})");
                    onComplete?.Invoke(null);
                    return;
                }

                Info($"[IBL] EXR file loaded ({data.Length} bytes), decoding...");

                try
                {
                    // Decode EXR image using TinyEXR
                    int width = 0, height = 0;
                    float* rgbaData = null;
                    IntPtr errPtr = IntPtr.Zero;
                    
                    fixed (byte* dataPtr = data)
                    {
                        int result = EXRLoadFromMemory(in *dataPtr, data.Length, ref width, ref height, out rgbaData, errPtr);
                        
                        if (result != 0)
                        {
                            string error = EXRGetFailureReason();
                            if (string.IsNullOrEmpty(error))
                            {
                                error = "Unknown EXR decode error";
                            }
                            Warning($"[IBL] Failed to decode EXR image: {error}");
                            onComplete?.Invoke(null);
                            return;
                        }
                    }

                    if (rgbaData == null)
                    {
                        Warning($"[IBL] Failed to decode EXR image: no data returned");
                        onComplete?.Invoke(null);
                        return;
                    }

                    Info($"[IBL] EXR decoded: {width}x{height} (RGBA float)");

                    // Convert EXR float data to byte array for processing
                    // EXR data is already linear HDR, so we can process it directly
                    int pixelCount = width * height * 4;
                    byte[] byteData = new byte[pixelCount];
                    
                    // Convert float HDR to LDR bytes for now (tone mapping)
                    // TODO: Keep as float for true HDR processing
                    for (int i = 0; i < pixelCount; i++)
                    {
                        float value = rgbaData[i];
                        // Simple Reinhard tone mapping
                        value = value / (1.0f + value);
                        byteData[i] = (byte)Math.Clamp(value * 255.0f, 0, 255);
                    }
                    
                    // Convert panorama to cubemaps
                    fixed (byte* bytePtr = byteData)
                    {
                        var envMap = ConvertPanoramaToCubemap(bytePtr, width, height, "exr-environment");
                        
                        // Free EXR data
                        EXRFreeImage(ref *rgbaData);
                        
                        if (envMap != null && envMap.IsLoaded)
                        {
                            Info("[IBL] Successfully created environment map from EXR");
                            onComplete?.Invoke(envMap);
                        }
                        else
                        {
                            Warning("[IBL] Failed to create environment map from EXR");
                            onComplete?.Invoke(null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Warning($"[IBL] Error processing EXR: {ex.Message}");
                    onComplete?.Invoke(null);
                }
            });
        }

        /// <summary>
        /// Load IBL from glTF model if available, otherwise create procedural test environment.
        /// </summary>
        /// <param name="modelRoot">The loaded glTF model root (can be null)</param>
        /// <param name="name">Name for the environment map</param>
        /// <returns>EnvironmentMap or null if creation failed</returns>
        public static unsafe EnvironmentMap? LoadFromGltfOrCreateTest(SharpGLTF.Schema2.ModelRoot? modelRoot, string name = "environment")
        {
            // Check for glTF extension (currently not implemented)
            if (modelRoot != null && modelRoot.ExtensionsUsed.Contains("EXT_lights_image_based"))
            {
                Info("[IBL] Found EXT_lights_image_based extension (parsing not yet implemented)");
                // TODO: Parse and load IBL from glTF extension
                // For now, return null to keep existing environment
                return null;
            }

            // Return null if no IBL in model - this keeps existing HDR environment
            Info("[IBL] No IBL extension in model, keeping existing environment");
            return null;
        }

        /// <summary>
        /// Convert HDR panorama (equirectangular) to cubemap with pre-filtering for IBL.
        /// </summary>
        private static unsafe EnvironmentMap? ConvertPanoramaToCubemap(byte* panoramaPixels, int panoWidth, int panoHeight, string name)
        {
            try
            {
                Info($"[IBL] Converting {panoWidth}x{panoHeight} panorama to cubemap (multi-threaded)...");

                const int diffuseSize = 64;   // Low-res for diffuse
                const int specularSize = 256; // Higher-res for specular
                int specularMipCount = (int)Math.Floor(Math.Log2(specularSize)) + 1;
                specularMipCount = Math.Min(specularMipCount, 8);

                var startTime = System.Diagnostics.Stopwatch.StartNew();

                // Create diffuse cubemap (irradiance)
                Info($"[IBL] Pre-filtering diffuse irradiance ({diffuseSize}x{diffuseSize}, 256 samples/pixel)...");
                var diffuseCubemap = CreateDiffuseCubemapFromPanorama(panoramaPixels, panoWidth, panoHeight, diffuseSize);
                Info($"[IBL] Diffuse complete in {startTime.ElapsedMilliseconds}ms");
                
                // Create specular cubemap with mipmaps (roughness levels)
                startTime.Restart();
                Info($"[IBL] Pre-filtering specular GGX ({specularSize}x{specularSize}, {specularMipCount} mips)...");
                var (specularCubemap, mipCount) = CreateSpecularCubemapFromPanorama(panoramaPixels, panoWidth, panoHeight, specularSize);
                Info($"[IBL] Specular complete in {startTime.ElapsedMilliseconds}ms");
                
                // Create BRDF LUT (same as procedural for now)
                var ggxLut = CreateBRDFLUT(256);

                var envMap = new EnvironmentMap(name);
                envMap.Initialize(diffuseCubemap, specularCubemap, ggxLut, mipCount);
                
                Info($"[IBL] Cubemap conversion complete: diffuse={diffuseSize}x{diffuseSize}, specular={specularSize}x{specularSize} ({mipCount} mips)");
                return envMap;
            }
            catch (Exception ex)
            {
                Error($"[IBL] Error converting panorama: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sample panorama at UV coordinates (equirectangular projection)
        /// </summary>
        private static unsafe Vector3 SamplePanorama(byte* pixels, int width, int height, float u, float v)
        {
            // Wrap UV coordinates
            u = u - MathF.Floor(u);
            v = Math.Clamp(v, 0f, 1f);

            // Convert to pixel coordinates
            int x = (int)(u * width) % width;
            int y = (int)(v * height);
            y = Math.Clamp(y, 0, height - 1);

            // Read RGBA pixel (4 bytes per pixel)
            int idx = (y * width + x) * 4;
            byte r = pixels[idx + 0];
            byte g = pixels[idx + 1];
            byte b = pixels[idx + 2];

            // Convert to linear float (0-1 range)
            return new Vector3(r / 255f, g / 255f, b / 255f);
        }

        /// <summary>
        /// Convert cubemap direction to panorama UV coordinates
        /// </summary>
        private static (float u, float v) DirectionToEquirectangularUV(Vector3 dir)
        {
            float u = 0.5f + MathF.Atan2(dir.Z, dir.X) / (2f * MathF.PI);
            float v = 0.5f - MathF.Asin(dir.Y) / MathF.PI;
            return (u, v);
        }

        /// <summary>
        /// Get cubemap face direction from UV coordinates
        /// </summary>
        private static Vector3 GetCubemapDirection(int face, float u, float v)
        {
            // Convert UV from [0,1] to [-1,1]
            float uc = 2f * u - 1f;
            float vc = 2f * v - 1f;

            return face switch
            {
                0 => Vector3.Normalize(new Vector3(1f, -vc, -uc)),   // +X
                1 => Vector3.Normalize(new Vector3(-1f, -vc, uc)),   // -X
                2 => Vector3.Normalize(new Vector3(uc, 1f, vc)),     // +Y
                3 => Vector3.Normalize(new Vector3(uc, -1f, -vc)),   // -Y
                4 => Vector3.Normalize(new Vector3(uc, -vc, 1f)),    // +Z
                5 => Vector3.Normalize(new Vector3(-uc, -vc, -1f)),  // -Z
                _ => Vector3.UnitX
            };
        }

        /// <summary>
        /// Create diffuse irradiance cubemap from panorama
        /// </summary>
        private static unsafe sg_image CreateDiffuseCubemapFromPanorama(byte* panoramaPixels, int panoWidth, int panoHeight, int cubeSize)
        {
            int faceSize = cubeSize * cubeSize * 4; // RGBA per face
            int totalSize = faceSize * 6;
            byte[] allFaces = new byte[totalSize];

            // Reduced sample count for faster processing (256 is still good quality)
            const int sampleCount = 256;

            // Process each face in parallel for much better performance
            System.Threading.Tasks.Parallel.For(0, 6, face =>
            {
                for (int y = 0; y < cubeSize; y++)
                {
                    for (int x = 0; x < cubeSize; x++)
                    {
                        float u = (x + 0.5f) / cubeSize;
                        float v = (y + 0.5f) / cubeSize;

                        Vector3 normal = GetCubemapDirection(face, u, v);
                        Vector3 irradiance = Vector3.Zero;

                        // Convolve with cosine-weighted hemisphere
                        int validSamples = 0;
                        for (int i = 0; i < sampleCount; i++)
                        {
                            // Generate random sample on hemisphere (using simple distribution)
                            float phi = 2f * MathF.PI * (i + 0.5f) / sampleCount;
                            float cosTheta = MathF.Sqrt((i + 0.5f) / sampleCount);
                            float sinTheta = MathF.Sqrt(1f - cosTheta * cosTheta);

                            // Local to world space
                            Vector3 tangent = Math.Abs(normal.Y) < 0.999f 
                                ? Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normal))
                                : Vector3.Normalize(Vector3.Cross(Vector3.UnitX, normal));
                            Vector3 bitangent = Vector3.Cross(normal, tangent);

                            Vector3 sampleDir = Vector3.Normalize(
                                tangent * sinTheta * MathF.Cos(phi) +
                                bitangent * sinTheta * MathF.Sin(phi) +
                                normal * cosTheta
                            );

                            var (su, sv) = DirectionToEquirectangularUV(sampleDir);
                            Vector3 sampleColor = SamplePanorama(panoramaPixels, panoWidth, panoHeight, su, sv);
                            
                            irradiance += sampleColor * cosTheta;
                            validSamples++;
                        }

                        irradiance /= validSamples;

                        // Write to face data
                        int idx = (face * faceSize) + (y * cubeSize + x) * 4;
                        allFaces[idx + 0] = (byte)Math.Clamp(irradiance.X * 255, 0, 255);
                        allFaces[idx + 1] = (byte)Math.Clamp(irradiance.Y * 255, 0, 255);
                        allFaces[idx + 2] = (byte)Math.Clamp(irradiance.Z * 255, 0, 255);
                        allFaces[idx + 3] = 255;
                    }
                }
            });

            var desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_CUBE,
                width = cubeSize,
                height = cubeSize,
                num_slices = 6,
                num_mipmaps = 1,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                label = "ibl-diffuse-from-hdr"
            };

            fixed (byte* ptr = allFaces)
            {
                desc.data.mip_levels[0] = new sg_range { ptr = ptr, size = (nuint)totalSize };
            }

            return sg_make_image(desc);
        }

        /// <summary>
        /// Create specular cubemap with mipmaps from panorama (GGX pre-filtering)
        /// </summary>
        private static unsafe (sg_image, int) CreateSpecularCubemapFromPanorama(byte* panoramaPixels, int panoWidth, int panoHeight, int baseSize)
        {
            int mipCount = (int)Math.Floor(Math.Log2(baseSize)) + 1;
            mipCount = Math.Min(mipCount, 8);

            var desc = new sg_image_desc
            {
                type = sg_image_type.SG_IMAGETYPE_CUBE,
                width = baseSize,
                height = baseSize,
                num_slices = 6,
                num_mipmaps = mipCount,
                pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                label = "ibl-specular-from-hdr"
            };

            // Generate each mip level with increasing roughness
            for (int mip = 0; mip < mipCount; mip++)
            {
                int mipSize = Math.Max(1, baseSize >> mip);
                float roughness = mip / (float)(mipCount - 1);
                
                int mipFaceSize = mipSize * mipSize * 4;
                int mipTotalSize = mipFaceSize * 6;
                byte[] mipAllFaces = new byte[mipTotalSize];

                // Reduced sample counts for faster processing
                int sampleCount = mip == 0 ? 128 : Math.Max(32, 128 >> mip);

                // Process each face in parallel
                System.Threading.Tasks.Parallel.For(0, 6, face =>
                {
                    for (int y = 0; y < mipSize; y++)
                    {
                        for (int x = 0; x < mipSize; x++)
                        {
                            float u = (x + 0.5f) / mipSize;
                            float v = (y + 0.5f) / mipSize;

                            Vector3 normal = GetCubemapDirection(face, u, v);
                            Vector3 reflection = normal; // View = normal for pre-filtering
                            Vector3 prefilteredColor = Vector3.Zero;

                            // Pre-filter with GGX distribution
                            float totalWeight = 0f;
                            for (int i = 0; i < sampleCount; i++)
                            {
                                // Generate GGX sample (simplified importance sampling)
                                float xi1 = (i + 0.5f) / sampleCount;
                                float xi2 = ((i * 7 + 13) % sampleCount + 0.5f) / sampleCount;
                                
                                float phi = 2f * MathF.PI * xi1;
                                float cosTheta = MathF.Sqrt((1f - xi2) / (1f + (roughness * roughness - 1f) * xi2));
                                float sinTheta = MathF.Sqrt(1f - cosTheta * cosTheta);

                                // Local to world space (around reflection vector)
                                Vector3 tangent = Math.Abs(reflection.Y) < 0.999f 
                                    ? Vector3.Normalize(Vector3.Cross(Vector3.UnitY, reflection))
                                    : Vector3.Normalize(Vector3.Cross(Vector3.UnitX, reflection));
                                Vector3 bitangent = Vector3.Cross(reflection, tangent);

                                Vector3 sampleDir = Vector3.Normalize(
                                    tangent * sinTheta * MathF.Cos(phi) +
                                    bitangent * sinTheta * MathF.Sin(phi) +
                                    reflection * cosTheta
                                );

                                float NdotL = Math.Max(Vector3.Dot(normal, sampleDir), 0f);
                                if (NdotL > 0f)
                                {
                                    var (su, sv) = DirectionToEquirectangularUV(sampleDir);
                                    Vector3 sampleColor = SamplePanorama(panoramaPixels, panoWidth, panoHeight, su, sv);
                                    
                                    prefilteredColor += sampleColor * NdotL;
                                    totalWeight += NdotL;
                                }
                            }

                            if (totalWeight > 0f)
                            {
                                prefilteredColor /= totalWeight;
                            }

                            // Write to mip data
                            int idx = (face * mipFaceSize) + (y * mipSize + x) * 4;
                            mipAllFaces[idx + 0] = (byte)Math.Clamp(prefilteredColor.X * 255, 0, 255);
                            mipAllFaces[idx + 1] = (byte)Math.Clamp(prefilteredColor.Y * 255, 0, 255);
                            mipAllFaces[idx + 2] = (byte)Math.Clamp(prefilteredColor.Z * 255, 0, 255);
                            mipAllFaces[idx + 3] = 255;
                        }
                    }
                });

                fixed (byte* ptr = mipAllFaces)
                {
                    desc.data.mip_levels[mip] = new sg_range { ptr = ptr, size = (nuint)mipTotalSize };
                }
            }

            return (sg_make_image(desc), mipCount);
        }

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
