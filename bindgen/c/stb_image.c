// STB Image C bindings for C# code generation
// This file defines the subset of stb_image functions we want to expose to C#

// Load an image from memory buffer
// Returns pointer to RGBA pixel data (must be freed with stbi_image_free_csharp)
// Parameters:
//   buffer: pointer to compressed image data in memory
//   len: length of buffer in bytes
//   x: output width
//   y: output height  
//   channels_in_file: output number of channels in source image
//   desired_channels: requested number of channels (0=auto, 1=grey, 2=grey+alpha, 3=rgb, 4=rgba)
unsigned char* stbi_load_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels);

// Load an image from memory buffer , with y-axis flipped
// Returns pointer to RGBA pixel data (must be freed with stbi_image_free_csharp)
// Parameters:
//   buffer: pointer to compressed image data in memory
//   len: length of buffer in bytes
//   x: output width
//   y: output height  
//   channels_in_file: output number of channels in source image
//   desired_channels: requested number of channels (0=auto, 1=grey, 2=grey+alpha, 3=rgb, 4=rgba)
unsigned char* stbi_load_flipped_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels);


float* stbi_loadf_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels);
float* stbi_loadf_flipped_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels);

// Free image data returned by stbi_load_csharp
void stbi_image_free_csharp(void* retval_from_stbi_load);

// Get reason for last load failure
const char* stbi_failure_reason_csharp(void);

