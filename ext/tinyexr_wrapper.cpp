// TinyEXR wrapper implementation for sokol-csharp
// This provides the actual implementation called by C# bindings

#define TINYEXR_IMPLEMENTATION
#define TINYEXR_USE_MINIZ 0  // Don't use tinyexr's miniz
#define TINYEXR_USE_STB_ZLIB 0
#define TINYEXR_USE_NANOZLIB 1  // Use nanozlib (included with tinyexr)

#include "tinyexr/tinyexr.h"
#include <stdlib.h>
#include <string.h>
#include <cmath>
#include <algorithm>

// Enable OpenMP for parallel processing if available
#ifdef _OPENMP
#include <omp.h>
#endif

// C wrapper functions for C# binding generator
// Function names MUST start with EXR prefix to be picked up by the generator

extern "C" {

// Load EXR from memory and return RGBA float data
int EXRLoadFromMemory(
    const unsigned char* memory,
    int size,
    int* width,
    int* height,
    float** out_rgba,
    const char** err)
{
    return LoadEXRFromMemory(out_rgba, width, height, memory, (size_t)size, err);
}

// Load EXR from file and return RGBA float data
int EXRLoad(
    const char* filename,
    int* width,
    int* height,
    float** out_rgba,
    const char** err)
{
    return LoadEXR(out_rgba, width, height, filename, err);
}

// Check if memory contains valid EXR data
int EXRIsFromMemory(
    const unsigned char* memory,
    int size)
{
    return IsEXRFromMemory(memory, (size_t)size);
}

// Free RGBA data returned by LoadEXR functions
void EXRFreeImage(float* rgba_data)
{
    if (rgba_data != nullptr) {
        free(rgba_data);
    }
}

// Free error message string
void EXRFreeErrorMessage(const char* err)
{
    FreeEXRErrorMessage(err);
}

// Get last error reason (for debugging)
static const char* last_error = nullptr;

const char* EXRGetFailureReason(void)
{
    return last_error ? last_error : "No error";
}

// ============================================================================
// Panorama to Cubemap Conversion (Cross-platform C++ implementation)
// ============================================================================

// 3D vector math helpers
struct Vec3 {
    float x, y, z;
    
    Vec3() : x(0), y(0), z(0) {}
    Vec3(float x_, float y_, float z_) : x(x_), y(y_), z(z_) {}
    
    Vec3 operator+(const Vec3& v) const { return Vec3(x + v.x, y + v.y, z + v.z); }
    Vec3 operator-(const Vec3& v) const { return Vec3(x - v.x, y - v.y, z - v.z); }
    Vec3 operator*(float s) const { return Vec3(x * s, y * s, z * s); }
    Vec3 operator/(float s) const { return Vec3(x / s, y / s, z / s); }
    
    float dot(const Vec3& v) const { return x * v.x + y * v.y + z * v.z; }
    
    Vec3 cross(const Vec3& v) const {
        return Vec3(
            y * v.z - z * v.y,
            z * v.x - x * v.z,
            x * v.y - y * v.x
        );
    }
    
    float length() const { return std::sqrt(x * x + y * y + z * z); }
    
    Vec3 normalize() const {
        float len = length();
        return (len > 0.0f) ? (*this / len) : Vec3(0, 0, 0);
    }
};

// Sample panorama texture at UV coordinates
static Vec3 SamplePanorama(const float* rgba_data, int width, int height, float u, float v) {
    // Wrap U, clamp V
    u = u - std::floor(u);
    v = std::max(0.0f, std::min(1.0f, v));
    
    // Convert to pixel coordinates
    int x = static_cast<int>(u * width) % width;
    int y = static_cast<int>(v * height);
    y = std::max(0, std::min(height - 1, y));
    
    // Read RGB (skip alpha)
    int idx = (y * width + x) * 4;
    return Vec3(rgba_data[idx], rgba_data[idx + 1], rgba_data[idx + 2]);
}

// Convert 3D direction to equirectangular UV
static void DirectionToEquirectangularUV(const Vec3& dir, float* u, float* v) {
    *u = 0.5f + std::atan2(dir.z, dir.x) / (2.0f * 3.14159265359f);
    *v = 0.5f - std::asin(dir.y) / 3.14159265359f;
}

// Get cubemap direction from face and UV
static Vec3 GetCubemapDirection(int face, float u, float v) {
    // Convert UV from [0,1] to [-1,1]
    float uc = 2.0f * u - 1.0f;
    float vc = 2.0f * v - 1.0f;
    
    Vec3 dir;
    switch (face) {
        case 0: dir = Vec3(1.0f, -vc, -uc); break;   // +X
        case 1: dir = Vec3(-1.0f, -vc, uc); break;   // -X
        case 2: dir = Vec3(uc, 1.0f, vc); break;     // +Y
        case 3: dir = Vec3(uc, -1.0f, -vc); break;   // -Y
        case 4: dir = Vec3(uc, -vc, 1.0f); break;    // +Z
        case 5: dir = Vec3(-uc, -vc, -1.0f); break;  // -Z
        default: dir = Vec3(1.0f, 0.0f, 0.0f); break;
    }
    return dir.normalize();
}

// Convert panorama to diffuse irradiance for a SINGLE FACE (thread-safe)
// C# will call this in parallel for each face
// Returns RGBA8 byte data for one face only
unsigned char* EXRConvertPanoramaToDiffuseCubemapFace(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    int face_index,
    int sample_count)
{
    const int face_size = cube_size * cube_size * 4; // RGBA
    unsigned char* output = (unsigned char*)malloc(face_size);
    
    if (!output) return nullptr;
    
    // Process single face (thread-safe, no shared state)
    for (int y = 0; y < cube_size; y++) {
        for (int x = 0; x < cube_size; x++) {
            float u = (x + 0.5f) / cube_size;
            float v = (y + 0.5f) / cube_size;
            
            Vec3 normal = GetCubemapDirection(face_index, u, v);
            Vec3 irradiance(0, 0, 0);
            
            // Convolve with cosine-weighted hemisphere
            int valid_samples = 0;
            for (int i = 0; i < sample_count; i++) {
                // Generate sample on hemisphere
                float phi = 2.0f * 3.14159265359f * (i + 0.5f) / sample_count;
                float cos_theta = std::sqrt((i + 0.5f) / sample_count);
                float sin_theta = std::sqrt(1.0f - cos_theta * cos_theta);
                
                // Local to world space
                Vec3 up = (std::abs(normal.y) < 0.999f) ? Vec3(0, 1, 0) : Vec3(1, 0, 0);
                Vec3 tangent = up.cross(normal).normalize();
                Vec3 bitangent = normal.cross(tangent);
                
                Vec3 sample_dir = (
                    tangent * (sin_theta * std::cos(phi)) +
                    bitangent * (sin_theta * std::sin(phi)) +
                    normal * cos_theta
                ).normalize();
                
                float su, sv;
                DirectionToEquirectangularUV(sample_dir, &su, &sv);
                Vec3 sample_color = SamplePanorama(panorama_rgba, pano_width, pano_height, su, sv);
                
                irradiance = irradiance + sample_color * cos_theta;
                valid_samples++;
            }
            
            if (valid_samples > 0) {
                irradiance = irradiance / static_cast<float>(valid_samples);
            }
            
            // Write to output buffer
            int idx = (y * cube_size + x) * 4;
            output[idx + 0] = static_cast<unsigned char>(std::max(0.0f, std::min(255.0f, irradiance.x * 255.0f)));
            output[idx + 1] = static_cast<unsigned char>(std::max(0.0f, std::min(255.0f, irradiance.y * 255.0f)));
            output[idx + 2] = static_cast<unsigned char>(std::max(0.0f, std::min(255.0f, irradiance.z * 255.0f)));
            output[idx + 3] = 255;
        }
    }
    
    return output;
}

// Convert panorama to diffuse irradiance cubemap (all 6 faces)
// Returns RGBA8 byte data (6 faces concatenated)
// NOTE: Kept for backward compatibility, but C# should use per-face version for parallelization
unsigned char* EXRConvertPanoramaToDiffuseCubemap(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    int sample_count)
{
    const int face_size = cube_size * cube_size * 4; // RGBA
    const int total_size = face_size * 6;
    unsigned char* output = (unsigned char*)malloc(total_size);
    
    if (!output) return nullptr;
    
    // Process each face sequentially (C# will parallelize instead)
    for (int face = 0; face < 6; face++) {
        unsigned char* face_data = EXRConvertPanoramaToDiffuseCubemapFace(
            panorama_rgba, pano_width, pano_height, cube_size, face, sample_count);
        
        if (face_data) {
            memcpy(output + face * face_size, face_data, face_size);
            free(face_data);
        }
    }
    
    return output;
}

// Convert panorama to specular GGX for a SINGLE FACE (thread-safe)
// C# will call this in parallel for each face
// Returns RGBA8 byte data for one face only
unsigned char* EXRConvertPanoramaToSpecularCubemapFace(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    int face_index,
    float roughness,
    int sample_count)
{
    const int face_size = cube_size * cube_size * 4; // RGBA
    unsigned char* output = (unsigned char*)malloc(face_size);
    
    if (!output) return nullptr;
    
    // Process single face (thread-safe, no shared state)
    for (int y = 0; y < cube_size; y++) {
        for (int x = 0; x < cube_size; x++) {
            float u = (x + 0.5f) / cube_size;
            float v = (y + 0.5f) / cube_size;
            
            Vec3 normal = GetCubemapDirection(face_index, u, v);
            Vec3 reflection = normal; // View = normal for pre-filtering
            Vec3 prefiltered_color(0, 0, 0);
            
            // Pre-filter with GGX distribution
            float total_weight = 0.0f;
            for (int i = 0; i < sample_count; i++) {
                // Generate GGX sample (simplified importance sampling)
                float xi1 = (i + 0.5f) / sample_count;
                float xi2 = ((i * 7 + 13) % sample_count + 0.5f) / sample_count;
                
                float phi = 2.0f * 3.14159265359f * xi1;
                float cos_theta = std::sqrt((1.0f - xi2) / (1.0f + (roughness * roughness - 1.0f) * xi2));
                float sin_theta = std::sqrt(1.0f - cos_theta * cos_theta);
                
                // Local to world space (around reflection vector)
                Vec3 up = (std::abs(reflection.y) < 0.999f) ? Vec3(0, 1, 0) : Vec3(1, 0, 0);
                Vec3 tangent = up.cross(reflection).normalize();
                Vec3 bitangent = reflection.cross(tangent);
                
                Vec3 sample_dir = (
                    tangent * (sin_theta * std::cos(phi)) +
                    bitangent * (sin_theta * std::sin(phi)) +
                    reflection * cos_theta
                ).normalize();
                
                float NdotL = std::max(normal.dot(sample_dir), 0.0f);
                if (NdotL > 0.0f) {
                    float su, sv;
                    DirectionToEquirectangularUV(sample_dir, &su, &sv);
                    Vec3 sample_color = SamplePanorama(panorama_rgba, pano_width, pano_height, su, sv);
                    
                    prefiltered_color = prefiltered_color + sample_color * NdotL;
                    total_weight += NdotL;
                }
            }
            
            if (total_weight > 0.0f) {
                prefiltered_color = prefiltered_color / total_weight;
            }
            
            // Write to output buffer
            int idx = (y * cube_size + x) * 4;
            output[idx + 0] = static_cast<unsigned char>(std::max(0.0f, std::min(255.0f, prefiltered_color.x * 255.0f)));
            output[idx + 1] = static_cast<unsigned char>(std::max(0.0f, std::min(255.0f, prefiltered_color.y * 255.0f)));
            output[idx + 2] = static_cast<unsigned char>(std::max(0.0f, std::min(255.0f, prefiltered_color.z * 255.0f)));
            output[idx + 3] = 255;
        }
    }
    
    return output;
}

// Convert panorama to specular GGX cubemap (one mip level, all 6 faces)
// Returns RGBA8 byte data (6 faces concatenated)
// NOTE: Kept for backward compatibility, but C# should use per-face version for parallelization
unsigned char* EXRConvertPanoramaToSpecularCubemap(
    const float* panorama_rgba,
    int pano_width,
    int pano_height,
    int cube_size,
    float roughness,
    int sample_count)
{
    const int face_size = cube_size * cube_size * 4; // RGBA
    const int total_size = face_size * 6;
    unsigned char* output = (unsigned char*)malloc(total_size);
    
    if (!output) return nullptr;
    
    // Process each face sequentially (C# will parallelize instead)
    for (int face = 0; face < 6; face++) {
        unsigned char* face_data = EXRConvertPanoramaToSpecularCubemapFace(
            panorama_rgba, pano_width, pano_height, cube_size, face, roughness, sample_count);
        
        if (face_data) {
            memcpy(output + face * face_size, face_data, face_size);
            free(face_data);
        }
    }
    
    return output;
}

// Free cubemap data allocated by conversion functions
void EXRFreeCubemapData(unsigned char* cubemap_data)
{
    if (cubemap_data) {
        free(cubemap_data);
    }
}

} // extern "C"


