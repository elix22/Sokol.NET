// TinyEXR C bindings for C# code generation
// This file defines simplified TinyEXR functions for the binding generator
// Similar to stb_image.c wrapper pattern
// NOTE: All functions must start with EXR prefix to be picked up by the generator

// Load EXR image from memory buffer and return RGBA float data
// Returns pointer to float array (width * height * 4 floats) that must be freed
// Parameters:
//   memory: pointer to EXR file data in memory
//   size: size of memory buffer in bytes
//   width: output image width
//   height: output image height
//   out_rgba: output pointer to RGBA float data
//   err: output error message (must be freed with EXRFreeErrorMessage)
// Returns 0 on success, non-zero on error
int EXRLoadFromMemory(
    const unsigned char* memory,
    int size,
    int* width,
    int* height,
    float** out_rgba,
    const char** err);

// Load EXR image from file and return RGBA float data
// Returns pointer to float array (width * height * 4 floats) that must be freed
// Parameters:
//   filename: path to EXR file
//   width: output image width
//   height: output image height
//   out_rgba: output pointer to RGBA float data
//   err: output error message (must be freed with EXRFreeErrorMessage)
// Returns 0 on success, non-zero on error
int EXRLoad(
    const char* filename,
    int* width,
    int* height,
    float** out_rgba,
    const char** err);

// Check if data in memory is a valid EXR file
// Returns 1 if valid EXR, 0 otherwise
int EXRIsFromMemory(
    const unsigned char* memory,
    int size);

// Free RGBA float data returned by LoadEXR functions
void EXRFreeImage(float* rgba_data);

// Free error message string returned by LoadEXR functions
void EXRFreeErrorMessage(const char* err);

// Get failure reason for last error (for debugging)
const char* EXRGetFailureReason(void);

// ============================================================================
// Panorama to Cubemap Conversion Functions (Cross-platform C++ implementation)
// ============================================================================

// Convert panorama to diffuse irradiance cubemap
// Returns RGBA8 byte data (6 faces concatenated), must be freed with EXRFreeCubemapData
// panorama_rgba: Input panorama as RGBA float data
// pano_width, pano_height: Panorama dimensions
// cube_size: Size of each cubemap face (e.g., 64)
// sample_count: Number of hemisphere samples per pixel (e.g., 256)
unsigned char* EXRConvertPanoramaToDiffuseCubemap(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    int sample_count);

// Convert panorama to diffuse irradiance for SINGLE FACE (thread-safe, for parallel processing)
// Returns RGBA8 byte data for one face only, must be freed with EXRFreeCubemapData
// C# should call this in Parallel.For loop across 6 faces
// face_index: Which cubemap face to process (0-5)
unsigned char* EXRConvertPanoramaToDiffuseCubemapFace(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    int face_index,
    int sample_count);

// Convert panorama to specular GGX cubemap (one mip level)
// Returns RGBA8 byte data (6 faces concatenated), must be freed with EXRFreeCubemapData
// panorama_rgba: Input panorama as RGBA float data
// pano_width, pano_height: Panorama dimensions
// cube_size: Size of each cubemap face (e.g., 256, 128, 64, etc.)
// roughness: Roughness level for this mip (0.0 = sharp, 1.0 = rough)
// sample_count: Number of GGX samples per pixel (e.g., 128)
unsigned char* EXRConvertPanoramaToSpecularCubemap(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    float roughness,
    int sample_count);

// Convert panorama to specular GGX for SINGLE FACE (thread-safe, for parallel processing)
// Returns RGBA8 byte data for one face only, must be freed with EXRFreeCubemapData
// C# should call this in Parallel.For loop across 6 faces for each mip level
// face_index: Which cubemap face to process (0-5)
unsigned char* EXRConvertPanoramaToSpecularCubemapFace(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    int face_index,
    float roughness,
    int sample_count);

// Free cubemap data allocated by conversion functions
void EXRFreeCubemapData(unsigned char* cubemap_data);
