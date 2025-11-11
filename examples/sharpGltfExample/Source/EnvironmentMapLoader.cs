using System;
using System.Numerics;
using System.Runtime.InteropServices;
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
        /// State for tracking 6-face cubemap loading
        /// </summary>
        private class CubemapFaceLoadState
        {
            public int LoadedFaces = 0;
            public byte[][] FaceData = new byte[6][];
            public int FaceSize = 0;
            public bool Failed = false;
            public HDRLoadCallback? Callback;
            public string Name = "cubemap-environment";
        }

        /// <summary>
        /// Load HDR environment map asynchronously from file.
        /// Uses FileSystem for async loading, then converts panorama to cubemaps.
        /// </summary>
        public static unsafe void LoadHDREnvironmentAsync(string hdrFileName, HDRLoadCallback onComplete)
        {
#if WEB
            // HDR loading not supported on WebAssembly - fallback to procedural
            Warning($"[IBL] HDR loading not supported on Web, using procedural environment");
            onComplete?.Invoke(null);
            return;
#else
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
                    // Decode HDR image using stb_image (float version for HDR)
                    int width = 0, height = 0, channels = 0;
                    float* pixels;
                    
                    fixed (byte* dataPtr = data)
                    {
                        pixels = stbi_loadf_csharp(in *dataPtr, data.Length, ref width, ref height, ref channels, 4);
                    }

                    if (pixels == null)
                    {
                        string error = stbi_failure_reason_csharp();
                        Warning($"[IBL] Failed to decode HDR image: {error}");
                        onComplete?.Invoke(null);
                        return;
                    }

                    Info($"[IBL] HDR decoded: {width}x{height}, {channels} channels");

                    // Convert panorama to cubemaps (now using float data directly)
                    var envMap = ConvertPanoramaToCubemap(pixels, width, height, "hdr-environment");
                    
                    stbi_image_free_csharp((byte*)pixels);
                    
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
#endif
        }

        /// <summary>
        /// Load EXR environment map asynchronously from file.
        /// Uses FileSystem for async loading, then converts EXR float data to cubemaps.
        /// 
        /// PERFORMANCE WARNING: Runtime panorama-to-cubemap conversion is SLOW (10-15 seconds).
        /// This is unavoidable due to the math involved (GGX importance sampling, 256+ samples/pixel).
        /// 
        /// RECOMMENDED WORKFLOW:
        /// 1. Use offline tools (cmftStudio, IBLBaker, glTF-IBL-Sampler) to pre-filter panoramas
        /// 2. Load pre-filtered cubemaps directly (6 faces + mip levels) - instant
        /// 3. Only use runtime conversion for prototyping/testing
        /// 
        /// TODO: Implement LoadPreFilteredCubemap() to load 6-face EXR directly
        /// </summary>
        public static unsafe void LoadEXREnvironmentAsync(string exrFileName, HDRLoadCallback onComplete)
        {
#if WEB
            // EXR loading not supported on WebAssembly - fallback to procedural
            Warning($"[IBL] EXR loading not supported on Web, using procedural environment");
            onComplete?.Invoke(null);
            return;
#else
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
                
                // ============================================================================
                // PERFORMANCE WARNING: Runtime conversion takes 10-15 seconds!
                // ============================================================================
                // This is doing expensive math:
                //   - Diffuse: 64x64x6 faces * 256 samples/pixel = ~6M samples
                //   - Specular: 256x256x6 * 8 mips * 128+ samples/pixel = ~40M samples
                //
                // RECOMMENDED: Pre-filter offline using cmftStudio/IBLBaker, then load 
                // the pre-filtered cubemaps directly. This approach is used by:
                //   - Unity (Skybox Baking)
                //   - Unreal Engine (Reflection Capture)
                //   - glTF Sample Viewer (pre-filtered KTX2 cubemaps)
                // ============================================================================
                
                Info($"[IBL] WARNING: Runtime conversion will take 10-15 seconds...");

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

                    // Convert panorama to cubemaps (using float data directly, no conversion needed)
                    var envMap = ConvertPanoramaToCubemap(rgbaData, width, height, "exr-environment");
                    
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
                catch (Exception ex)
                {
                    Warning($"[IBL] Error processing EXR: {ex.Message}");
                    onComplete?.Invoke(null);
                }
            });
#endif
        }

        /// <summary>
        /// Load pre-filtered IBL environment from 6 separate cubemap face images.
        /// This is fast and works on all platforms including WebAssembly.
        /// Faces are loaded asynchronously and the cubemap is created once all faces are loaded.
        /// </summary>
        /// <param name="faceFileNames">Array of 6 filenames in order: +X, -X, +Y, -Y, +Z, -Z</param>
        /// <param name="onComplete">Callback when loading completes (or fails)</param>
        /// <param name="name">Name for the environment map</param>
        public static unsafe void LoadCubemapFacesAsync(string[] faceFileNames, HDRLoadCallback onComplete, string name = "cubemap-environment")
        {
            if (faceFileNames.Length != 6)
            {
                Error($"[IBL] LoadCubemapFacesAsync requires exactly 6 face filenames, got {faceFileNames.Length}");
                onComplete?.Invoke(null);
                return;
            }

            Info($"[IBL] Loading cubemap environment from 6 faces: {name}");

            var loadState = new CubemapFaceLoadState
            {
                Callback = onComplete,
                Name = name
            };

            // Load all 6 faces asynchronously
            for (int face = 0; face < 6; face++)
            {
                int faceIndex = face; // Capture for closure
                string fileName = faceFileNames[face];

                FileSystem.Instance.LoadFile(fileName, (filePath, data, status) =>
                {
                    if (loadState.Failed)
                        return; // Already failed, skip

                    if (status != FileLoadStatus.Success || data == null)
                    {
                        Warning($"[IBL] Failed to load cubemap face {faceIndex}: {fileName} (status: {status})");
                        loadState.Failed = true;
                        loadState.Callback?.Invoke(null);
                        return;
                    }

                    // Decode image using stb_image
                    int width = 0, height = 0, channels = 0;
                    byte* pixels;

                    fixed (byte* dataPtr = data)
                    {
                        pixels = stbi_load_csharp(in *dataPtr, data.Length, ref width, ref height, ref channels, 4);
                    }

                    if (pixels == null)
                    {
                        string error = stbi_failure_reason_csharp();
                        Warning($"[IBL] Failed to decode cubemap face {faceIndex}: {error}");
                        loadState.Failed = true;
                        loadState.Callback?.Invoke(null);
                        return;
                    }

                    // Validate all faces are same size
                    if (loadState.FaceSize == 0)
                    {
                        loadState.FaceSize = width;
                        Info($"[IBL] Cubemap face size: {width}x{height}");
                    }
                    else if (width != loadState.FaceSize || height != loadState.FaceSize)
                    {
                        Warning($"[IBL] Cubemap face {faceIndex} size mismatch: {width}x{height}, expected {loadState.FaceSize}x{loadState.FaceSize}");
                        stbi_image_free_csharp(pixels);
                        loadState.Failed = true;
                        loadState.Callback?.Invoke(null);
                        return;
                    }

                    // Copy pixel data
                    int faceDataSize = width * height * 4; // RGBA
                    loadState.FaceData[faceIndex] = new byte[faceDataSize];
                    
                    fixed (byte* dest = loadState.FaceData[faceIndex])
                    {
                        Buffer.MemoryCopy(pixels, dest, faceDataSize, faceDataSize);
                    }

                    stbi_image_free_csharp(pixels);

                    // Increment loaded count
                    loadState.LoadedFaces++;
                    Info($"[IBL] Loaded cubemap face {faceIndex}/6: {fileName}");

                    // All faces loaded?
                    if (loadState.LoadedFaces == 6)
                    {
                        Info($"[IBL] All 6 faces loaded, creating cubemap environment...");
                        var envMap = CreateCubemapEnvironment(loadState.FaceData, loadState.FaceSize, loadState.Name);
                        loadState.Callback?.Invoke(envMap);
                    }
                });
            }
        }

        /// <summary>
        /// Create environment map from pre-loaded 6 cubemap faces.
        /// This assumes the faces are already pre-filtered (diffuse = low-res, specular = with mipmaps).
        /// For simple cubemaps without pre-filtering, this creates a basic environment.
        /// </summary>
        private static unsafe EnvironmentMap CreateCubemapEnvironment(byte[][] faceData, int faceSize, string name)
        {
            try
            {
                // Combine all 6 faces into single buffer
                int faceSizeBytes = faceSize * faceSize * 4; // RGBA
                int totalSize = faceSizeBytes * 6;
                byte[] allFaces = new byte[totalSize];

                for (int face = 0; face < 6; face++)
                {
                    Array.Copy(faceData[face], 0, allFaces, face * faceSizeBytes, faceSizeBytes);
                }

                // Create diffuse cubemap (use loaded faces directly, or downscale if too large)
                // For now, we'll use the loaded faces as-is for both diffuse and specular
                sg_image diffuseCubemap;
                
                fixed (byte* ptr = allFaces)
                {
                    var desc = new sg_image_desc
                    {
                        type = sg_image_type.SG_IMAGETYPE_CUBE,
                        width = faceSize,
                        height = faceSize,
                        num_slices = 6,
                        num_mipmaps = 1,
                        pixel_format = sg_pixel_format.SG_PIXELFORMAT_RGBA8,
                        label = $"ibl-{name}-diffuse"
                    };
                    desc.data.mip_levels[0] = new sg_range { ptr = ptr, size = (nuint)totalSize };
                    diffuseCubemap = sg_make_image(desc);
                }

                // Create specular cubemap with mipmaps
                // Generate mipmaps from base level
                int mipCount = (int)Math.Floor(Math.Log2(faceSize)) + 1;
                mipCount = Math.Min(mipCount, 8);

                var (specularCubemap, actualMipCount) = CreateMipmappedCubemapFromFaces(allFaces, faceSize, name);

                // Create BRDF LUT
                var ggxLut = CreateBRDFLUT(256);

                var envMap = new EnvironmentMap(name);
                envMap.Initialize(diffuseCubemap, specularCubemap, ggxLut, actualMipCount);

                Info($"[IBL] Cubemap environment '{name}' created successfully: {faceSize}x{faceSize}, {actualMipCount} mips");
                return envMap;
            }
            catch (Exception ex)
            {
                Error($"[IBL] Error creating cubemap environment: {ex.Message}");
                return CreateTestEnvironment(name);
            }
        }

        /// <summary>
        /// Create mipmapped cubemap from base level faces by generating mip levels
        /// </summary>
        private static unsafe (sg_image, int) CreateMipmappedCubemapFromFaces(byte[] baseFaces, int baseSize, string name)
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
                label = $"ibl-{name}-specular-mip"
            };

            // Mip 0: Use original faces
            fixed (byte* ptr = baseFaces)
            {
                desc.data.mip_levels[0] = new sg_range { ptr = ptr, size = (nuint)baseFaces.Length };
            }

            // Generate additional mip levels by downsampling
            for (int mip = 1; mip < mipCount; mip++)
            {
                int mipSize = Math.Max(1, baseSize >> mip);
                int mipFaceSize = mipSize * mipSize * 4; // RGBA per face
                int mipTotalSize = mipFaceSize * 6;
                byte[] mipFaces = new byte[mipTotalSize];

                int srcMipSize = Math.Max(1, baseSize >> (mip - 1));
                
                // Simple box filter downsampling for each face
                for (int face = 0; face < 6; face++)
                {
                    DownsampleFace(baseFaces, face, srcMipSize, mipFaces, face, mipSize);
                }

                fixed (byte* ptr = mipFaces)
                {
                    desc.data.mip_levels[mip] = new sg_range { ptr = ptr, size = (nuint)mipTotalSize };
                }
            }

            return (sg_make_image(desc), mipCount);
        }

        /// <summary>
        /// Simple box filter downsampling for a cubemap face
        /// </summary>
        private static void DownsampleFace(byte[] srcData, int srcFace, int srcSize, byte[] dstData, int dstFace, int dstSize)
        {
            int srcFaceOffset = srcFace * srcSize * srcSize * 4;
            int dstFaceOffset = dstFace * dstSize * dstSize * 4;

            for (int y = 0; y < dstSize; y++)
            {
                for (int x = 0; x < dstSize; x++)
                {
                    // Sample 2x2 block from source
                    int sx = x * 2;
                    int sy = y * 2;

                    Vector4 sum = Vector4.Zero;
                    int samples = 0;

                    for (int dy = 0; dy < 2 && (sy + dy) < srcSize; dy++)
                    {
                        for (int dx = 0; dx < 2 && (sx + dx) < srcSize; dx++)
                        {
                            int srcIdx = srcFaceOffset + ((sy + dy) * srcSize + (sx + dx)) * 4;
                            sum.X += srcData[srcIdx + 0];
                            sum.Y += srcData[srcIdx + 1];
                            sum.Z += srcData[srcIdx + 2];
                            sum.W += srcData[srcIdx + 3];
                            samples++;
                        }
                    }

                    // Average
                    sum /= samples;

                    int dstIdx = dstFaceOffset + (y * dstSize + x) * 4;
                    dstData[dstIdx + 0] = (byte)Math.Clamp(sum.X, 0, 255);
                    dstData[dstIdx + 1] = (byte)Math.Clamp(sum.Y, 0, 255);
                    dstData[dstIdx + 2] = (byte)Math.Clamp(sum.Z, 0, 255);
                    dstData[dstIdx + 3] = (byte)Math.Clamp(sum.W, 0, 255);
                }
            }
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
        /// Uses native C++ conversion for massive performance improvement (10x-100x faster).
        /// </summary>
        private static unsafe EnvironmentMap? ConvertPanoramaToCubemap(float* panoramaPixels, int panoWidth, int panoHeight, string name)
        {
            try
            {
                Info($"[IBL] Converting {panoWidth}x{panoHeight} panorama to cubemap (C++ native)...");

                const int diffuseSize = 64;   // Low-res for diffuse
                const int specularSize = 256; // Higher-res for specular
                int specularMipCount = (int)Math.Floor(Math.Log2(specularSize)) + 1;
                specularMipCount = Math.Min(specularMipCount, 8);

                var startTime = System.Diagnostics.Stopwatch.StartNew();

                // Create diffuse cubemap (irradiance) using C++
                Info($"[IBL] Pre-filtering diffuse irradiance ({diffuseSize}x{diffuseSize}, 256 samples/pixel)...");
                var diffuseCubemap = CreateDiffuseCubemapFromPanorama(panoramaPixels, panoWidth, panoHeight, diffuseSize);
                Info($"[IBL] Diffuse complete in {startTime.ElapsedMilliseconds}ms");
                
                // Create specular cubemap with mipmaps (roughness levels) using C++
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
        /// Create diffuse irradiance cubemap from panorama using native C++ conversion
        /// </summary>
        private static unsafe sg_image CreateDiffuseCubemapFromPanorama(float* panoramaFloat, int panoWidth, int panoHeight, int cubeSize)
        {
            const int sampleCount = 256;
            
            int faceSize = cubeSize * cubeSize * 4; // RGBA per face
            int totalSize = faceSize * 6;
            
            // Allocate buffer for all 6 faces
            byte* cubemapData = (byte*)Marshal.AllocHGlobal(totalSize);
            
            // Track C++ allocated face pointers for cleanup
            byte*[] facePointers = new byte*[6];
            
            try
            {
                // Process each face in parallel using C# threading (cross-platform, including WebAssembly)
                // C++ does the heavy math computation per face (thread-safe, no shared state)
                System.Threading.Tasks.Parallel.For(0, 6, face =>
                {
                    byte* faceData = EXRConvertPanoramaToDiffuseCubemapFace(
                        in *panoramaFloat, panoWidth, panoHeight, cubeSize, face, sampleCount);
                    
                    if (faceData != null)
                    {
                        // Copy face data to output buffer
                        Buffer.MemoryCopy(faceData, cubemapData + (face * faceSize), faceSize, faceSize);
                        facePointers[face] = faceData;
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

                desc.data.mip_levels[0] = new sg_range { ptr = cubemapData, size = (nuint)totalSize };

                var image = sg_make_image(desc);
                
                return image;
            }
            finally
            {
                // Free all C++ allocated face data
                for (int i = 0; i < 6; i++)
                {
                    if (facePointers[i] != null)
                    {
                        EXRFreeCubemapData(facePointers[i]);
                    }
                }
                
                // Free combined buffer
                Marshal.FreeHGlobal((IntPtr)cubemapData);
            }
        }

        /// <summary>
        /// Create specular cubemap with mipmaps from panorama using native C++ GGX pre-filtering
        /// </summary>
        private static unsafe (sg_image, int) CreateSpecularCubemapFromPanorama(float* panoramaFloat, int panoWidth, int panoHeight, int baseSize)
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

            // Track all allocated mip data buffers for cleanup
            byte*[] mipDataBuffers = new byte*[mipCount];

            try
            {
                // Generate each mip level with increasing roughness
                for (int mip = 0; mip < mipCount; mip++)
                {
                    int mipSize = Math.Max(1, baseSize >> mip);
                    float roughness = mip / (float)(mipCount - 1);
                    
                    // Reduced sample counts for faster processing
                    int sampleCount = mip == 0 ? 128 : Math.Max(32, 128 >> mip);

                    int mipFaceSize = mipSize * mipSize * 4; // RGBA per face
                    int mipTotalSize = mipFaceSize * 6;
                    
                    // Allocate buffer for all 6 faces of this mip level
                    byte* mipCubemapData = (byte*)Marshal.AllocHGlobal(mipTotalSize);
                    mipDataBuffers[mip] = mipCubemapData;
                    
                    // Track C++ allocated face pointers for cleanup
                    byte*[] facePointers = new byte*[6];
                    
                    try
                    {
                        // Process each face in parallel using C# threading (cross-platform)
                        // C++ does the heavy GGX computation per face (thread-safe)
                        System.Threading.Tasks.Parallel.For(0, 6, face =>
                        {
                            byte* faceData = EXRConvertPanoramaToSpecularCubemapFace(
                                in *panoramaFloat, panoWidth, panoHeight, mipSize, face, roughness, sampleCount);
                            
                            if (faceData != null)
                            {
                                // Copy face data to mip buffer
                                Buffer.MemoryCopy(faceData, mipCubemapData + (face * mipFaceSize), mipFaceSize, mipFaceSize);
                                facePointers[face] = faceData;
                            }
                        });
                    }
                    finally
                    {
                        // Free C++ allocated face data
                        for (int f = 0; f < 6; f++)
                        {
                            if (facePointers[f] != null)
                            {
                                EXRFreeCubemapData(facePointers[f]);
                            }
                        }
                    }

                    desc.data.mip_levels[mip] = new sg_range { ptr = mipCubemapData, size = (nuint)mipTotalSize };
                }

                var image = sg_make_image(desc);
                
                return (image, mipCount);
            }
            finally
            {
                // Free all allocated mip buffers
                for (int i = 0; i < mipCount; i++)
                {
                    if (mipDataBuffers[i] != null)
                    {
                        Marshal.FreeHGlobal((IntPtr)mipDataBuffers[i]);
                    }
                }
            }
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
            string? charlieLutPath = null)
        {
            // TODO: Implement file loading using StbImage
            // For now, return test environment
            Warning("[IBL] File loading not yet implemented, using test environment");
            return CreateTestEnvironment("loaded");
        }
    }
}
