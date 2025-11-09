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
