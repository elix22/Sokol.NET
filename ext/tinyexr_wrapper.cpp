// TinyEXR wrapper implementation for sokol-csharp
// This provides the actual implementation called by C# bindings

#define TINYEXR_IMPLEMENTATION
#define TINYEXR_USE_MINIZ 0  // Don't use tinyexr's miniz
#define TINYEXR_USE_STB_ZLIB 0
#define TINYEXR_USE_NANOZLIB 1  // Use nanozlib (included with tinyexr)

#include "tinyexr/tinyexr.h"
#include <stdlib.h>
#include <string.h>

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

} // extern "C"

