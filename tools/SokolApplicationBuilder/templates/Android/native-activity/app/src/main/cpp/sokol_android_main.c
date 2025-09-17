
#define SOKOL_IMPL
#define SOKOL_GLES3
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
#include "sokol_app.h"
#include "sokol_gfx.h"
#include "sokol_glue.h"
#include "sokol_audio.h"
#include "sokol_time.h"
#include "sokol_log.h"
#include "sokol_shape.h"
#include "sokol_gl.h"
#define SOKOL_DEBUGTEXT_IMPL
#include "sokol_debugtext.h"
#define PL_MPEG_IMPLEMENTATION
#include "pl_mpeg.h"
#define CGLTF_IMPLEMENTATION
#include "cgltf.h"

#define CIMGUI_DEFINE_ENUMS_AND_STRUCTS
#include "cimgui.h"
#ifndef ImTextureID_Invalid
#define ImTextureID_Invalid     ((ImTextureID)0)
#endif
#define SOKOL_IMGUI_IMPL
#include "sokol_imgui.h"

#ifdef __cplusplus
extern "C"
{
#endif

   void *android_open(const char *filename);
   ssize_t android_read(void *handle, void *buf, size_t count);
   off_t android_seek(void *handle, off_t offset, int whence);
   off_t android_size(void *handle);
   int android_close(void *handle);

   const char *fileutil_get_path(const char *filename, char *buf, size_t buf_size)
   {
      snprintf(buf, buf_size, "%s", filename);
      return buf;
   }

#ifdef __cplusplus
}
#endif

#include "sokol_fetch.h"



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

int sdtx_print_wrapper(const char* str)
{
    return sdtx_printf("%s", str);
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
