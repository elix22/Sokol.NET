
#ifdef __EMSCRIPTEN__
// Emscripten utility functions for EM_JS code in sokol_app.h
// These are provided by the JavaScript library sokol_js_lib.js
// and linked automatically when building with Emscripten

#include <emscripten.h>

// Forward declarations - actual implementations are in sokol_js_lib.js
extern int stringToUTF8OnStack(const char* str);
extern void withStackSave(void (*func)(void));
extern void* findCanvasEventTarget(const char* target);

#endif

#define SOKOL_IMPL
#define SOKOL_DLL
#ifndef __ANDROID__
    #define SOKOL_NO_ENTRY
#endif
#define SOKOL_NO_DEPRECATED
#define SOKOL_TRACE_HOOKS
#define SOKOL_FETCH_API_DECL

#ifdef __ANDROID__
// Enable Android soft keyboard extension for sokol-csharp
// This provides enhanced keyboard support using JNI and hidden EditText
// See: docs/ANDROID_KEYBOARD_IMPLEMENTATION.md
#define SOKOL_ANDROID_KEYBOARD_EXT

#include <pthread.h>
#include <unistd.h>
#include <time.h>
#include <android/native_window.h>
#include <android/window.h>
#include <android/native_activity.h>
#include <android/looper.h>
#include <android/asset_manager.h>
#include <android/asset_manager_jni.h>
#include <android/log.h>
#include <EGL/egl.h>
#include <GLES3/gl3.h>

#define SOKOL_GLES3

#ifdef __cplusplus
extern "C"
{
#endif

   void *android_open(const char *filename);
   ssize_t android_read(void *handle, void *buf, size_t count);
   off_t android_seek(void *handle, off_t offset, int whence);
   off_t android_size(void *handle);
   int android_close(void *handle);
#ifdef __cplusplus
}
#endif
#endif


#include "sokol_defines.h"
#include "sokol_app.h"
#include "sokol_gfx.h"
#include "sokol_glue.h"
#include "sokol_audio.h"
#include "sokol_time.h"
#include "sokol_log.h"
#define SOKOL_SHAPE_IMPL
#include "sokol_shape.h"
#include "sokol_gl.h"
#include "sokol_fetch.h"
#define SOKOL_DEBUGTEXT_IMPL
#include "sokol_debugtext.h"
// TBD ELI #include "sokol_gp.h"
#define PL_MPEG_IMPLEMENTATION
#include "pl_mpeg/pl_mpeg.h"

#define SOKOL_BASISU_INCLUDED
#include "basisu/sokol_basisu.h"
#define CGLTF_IMPLEMENTATION
#include "cgltf.h"

// STB Image - for fast native image loading
#define STB_IMAGE_IMPLEMENTATION
#include "stb/stb_image.h"

// Fix X11 macro conflicts on Linux
// X11 headers (included by sokol_app.h on Linux) define a 'Status' macro
// that conflicts with the 'Status' member in ImTextureData struct
#if defined(__linux__) && defined(Status)
    #undef Status
#endif

#define CIMGUI_DEFINE_ENUMS_AND_STRUCTS
#define IMGUI_VERSION       "1.92.2b" // TBD ELI , currently manual setup
#define IMGUI_VERSION_NUM   19222
#include "cimgui/cimgui.h"
#ifndef ImTextureID_Invalid
#define ImTextureID_Invalid     ((ImTextureID)0)
#endif
#define SOKOL_IMGUI_IMPL
#include "sokol_imgui.h"

#define SOKOL_GFX_IMGUI_IMPL
#include "sokol_gfx_imgui.h"

#if defined(__clang__)
    #pragma clang diagnostic push
    #pragma clang diagnostic ignored "-Wunused-function"
#elif defined(__GNUC__)
    #pragma GCC diagnostic push
    #pragma GCC diagnostic ignored "-Wunused-function"
#endif

#define FONTSTASH_IMPLEMENTATION
#include "fontstash/fontstash.h"

#if defined(__clang__)
    #pragma clang diagnostic pop
#elif defined(__GNUC__)
    #pragma GCC diagnostic pop
#endif

#define SOKOL_FONTSTASH_IMPL
#include "sokol_fontstash.h"

/*=== STB IMAGE C# BINDINGS ==================================================
    Wrapper functions for stb_image to provide C# bindings for fast native
    image loading (especially important for WebAssembly performance).
============================================================================*/

// stbi_load wrapper that returns image data pointer
// Caller is responsible for freeing with stbi_image_free
SOKOL_API_IMPL unsigned char* stbi_load_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels) {
    return stbi_load_from_memory(buffer, len, x, y, channels_in_file, desired_channels);
}

SOKOL_API_IMPL unsigned char* stbi_load_flipped_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels) {
    
    stbi_set_flip_vertically_on_load(true);
    return stbi_load_from_memory(buffer, len, x, y, channels_in_file, desired_channels);
}

//stbi_loadf_from_memory
SOKOL_API_IMPL float* stbi_loadf_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels) {
    return stbi_loadf_from_memory(buffer, len, x, y, channels_in_file, desired_channels);
}

SOKOL_API_IMPL float* stbi_loadf_flipped_csharp(const unsigned char* buffer, int len, int* x, int* y, int* channels_in_file, int desired_channels) {
    
    stbi_set_flip_vertically_on_load(true);
    return stbi_loadf_from_memory(buffer, len, x, y, channels_in_file, desired_channels);
}


// stbi_image_free wrapper
SOKOL_API_IMPL void stbi_image_free_csharp(void* retval_from_stbi_load) {
    stbi_image_free(retval_from_stbi_load);
}

// stbi_failure_reason wrapper
SOKOL_API_IMPL const char* stbi_failure_reason_csharp(void) {
    return stbi_failure_reason();
}

/*=== C# BINDING HELPERS (elix22) ============================================
    WebAssembly/Emscripten cannot marshal structs returned by value through P/Invoke.
    These _internal helper functions work around this limitation by taking an output
    pointer parameter instead. The C# binding generator automatically creates wrappers
    that call these functions on WebAssembly builds while using the regular functions
    on other platforms.
    
    All C# binding-specific code is kept in this file (sokol.c) which is not part
    of the upstream sokol repository, keeping the upstream headers clean.
    
    The internal wrapper functions are now auto-generated by bindgen/gen_csharp.py
    and included from sokol_csharp_internal_wrappers.h
============================================================================*/

// Include auto-generated internal wrapper functions
#include "sokol_csharp_internal_wrappers.h"

int sdtx_print_wrapper(const char* str)
{
    return sdtx_printf("%s", str);
}

static sgimgui_t sgimgui_ctx = {0};

SOKOL_API_IMPL sgimgui_t * sgimgui_init_csharp(void) {
 
    sgimgui_init(&sgimgui_ctx, &(sgimgui_desc_t){0});
    return &sgimgui_ctx;
}



#ifdef __ANDROID__
extern void *AndroidMain();

static ANativeActivity *_activity = 0;
static AAssetManager *g_assetManager = NULL;


sapp_desc sokol_main(int argc, char *argv[])
{

   if (argc == 1)
   {
      _activity = (ANativeActivity *)argv[0];
      g_assetManager = _activity->assetManager;
       ANativeActivity_setWindowFlags(_activity, AWINDOW_FLAG_KEEP_SCREEN_ON, 0);
   }
   //_sapp.android.activity
   sapp_desc *desc = (sapp_desc *)AndroidMain();
   return *desc;
}


// "open": open an asset file from the APK assets folder.
void *android_open(const char *filename)
{
   if (!g_assetManager)
   {
      __android_log_print(ANDROID_LOG_ERROR, "SOKOL", "g_assetManager is NULL");
      return NULL;
   }
   AAsset *asset = AAssetManager_open(g_assetManager, filename, AASSET_MODE_BUFFER);
   if (!asset)
   {
      __android_log_print(ANDROID_LOG_ERROR, "SOKOL", "AAssetManager_open failed: %s", filename);
      return NULL;
   }

   return (void *)asset;
}

// "read": read data from the asset.
ssize_t android_read(void *handle, void *buf, size_t count)
{
   //  __android_log_print(ANDROID_LOG_INFO, "SOKOL", "android_read enter %d", count);
   if (!handle)
      return -1;
   AAsset *asset = (AAsset *)handle;
   ssize_t size = AAsset_read(asset, buf, count);
   // __android_log_print(ANDROID_LOG_INFO, "SOKOL", "android_read exit %d", size);
   return size;
}

// "seek": reposition the asset read pointer (whence: SEEK_SET, SEEK_CUR, SEEK_END)
off_t android_seek(void *handle, off_t offset, int whence)
{
   if (!handle)
      return -1;
   // __android_log_print(ANDROID_LOG_INFO, "SOKOL", "android_seek offset:%d whence:%d", offset,whence);
   AAsset *asset = (AAsset *)handle;
   return AAsset_seek(asset, offset, whence);
}

// "size": get the length of the asset.
off_t android_size(void *handle)
{
   if (!handle)
      return -1;
   AAsset *asset = (AAsset *)handle;
   off_t size = AAsset_getLength(asset);
   // __android_log_print(ANDROID_LOG_INFO, "SOKOL", "android_size %d", size);
   return size;
}

// "close": close the asset file.
int android_close(void *handle)
{
   if (handle)
   {
      AAsset *asset = (AAsset *)handle;
      AAsset_close(asset);
   }
   return 0;
}


static char* find_asset_recursive(const char* current_dir, const char* target) {
    // Open the current directory in assets
    AAssetDir* assetDir = AAssetManager_openDir(g_assetManager, current_dir);
    if (!assetDir) {
        __android_log_print(ANDROID_LOG_ERROR, "ASSET_SEARCH", "Failed to open dir: %s", current_dir);
        return NULL;
    }
    
    char* result = NULL;
    
    const char* filename;
    while ((filename = AAssetDir_getNextFileName(assetDir)) != NULL) {
        // Build the relative path for this entry
        char rel_path[PATH_MAX];
        if (strlen(current_dir) > 0)
            snprintf(rel_path, PATH_MAX, "%s/%s", current_dir, filename);
        else
            snprintf(rel_path, PATH_MAX, "%s", filename);
        
        // Check if this entry's basename matches the target.
        // Note: This simple check compares the filename (without path) to the target.
        if (strcmp(filename, target) == 0) {
            // Found the file!
            result = strdup(rel_path);
            break;
        }
        
        // Try to open the directory at this relative path; if it exists, it means this entry is a directory.
        AAssetDir* subdir = AAssetManager_openDir(g_assetManager, rel_path);
        if (subdir) {
            // Close immediately, we only needed to check if the subdirectory exists.
            AAssetDir_close(subdir);
            // Recurse into the subdirectory.
            result = find_asset_recursive(rel_path, target);
            if (result != NULL) {
                break;
            }
        }
    }
    
    AAssetDir_close(assetDir);
    return result;
}

/// Searches for an asset file by its filename (without any path).
/// If found, returns a heap allocated relative path string that you can pass to AAssetManager_open.
/// If not found, returns an empty string (which should be freed by the caller).
char* get_asset_relative_path(const char* target_filename) {
    char* path = find_asset_recursive("", target_filename);
    if (path == NULL) {
        // Not found, return an empty string
        path = strdup("");
    }
    return path;
}
#endif


