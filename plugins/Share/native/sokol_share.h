/* sokol_share.h -- cross-platform share sheet for Sokol.NET
   Part of the Share plugin (plugins/Share/).

   USAGE
     #define SOKOL_SHARE_IMPL before including this header in exactly one .c / .m file.
     On Android link with sokol_share_android.c; on iOS with sokol_share_ios.m.

   THREAD SAFETY
     Android: AttachCurrentThread is called internally; safe from any thread.
     iOS:     dispatch_async(main_queue) is used internally; safe from any thread.
*/
#pragma once
#ifndef SOKOL_SHARE_H
#define SOKOL_SHARE_H

#ifdef __cplusplus
extern "C" {
#endif

/* Share an image file and a short text string via the platform share sheet.
   image_path: absolute path to a PNG produced by sfs_get_pref_path (may be NULL).
   text:       UTF-8 message string.                                              */
void sokolshare_image_text(const char* image_path, const char* text);

/* Put UTF-8 `text` on the system clipboard.
   Android and iOS ONLY -- these are the two platforms sokol_app.h leaves as
   "not implemented" in sapp_set_clipboard_string(); macOS/Windows/Linux/web
   callers should keep using sokol's own clipboard rather than this. */
void sokolshare_set_clipboard(const char* text);

/* Share one existing file of any type (PNG, JPEG, ZIP, JSON, ...) via the platform share sheet,
   keeping its name. Android and iOS ONLY.
   path:  absolute path of a readable file. Android: it must be under the app's files folder
          (sfs_get_pref_path), the root file_provider_paths.xml exposes.
   mime:  MIME type, e.g. "image/png", "application/zip"; NULL or "" = "application/octet-stream".
          Android picks the target apps by it; iOS reads the type from the file's extension.
   title: Android chooser title (NULL = the system's; Android 10+ does not show it for a send).
          Ignored on iOS.
   Returns 1 when the share sheet was asked to open, 0 when the file cannot be shared
   (missing, outside the shared folder, no FileProvider in the app). */
int sokolshare_file(const char* path, const char* mime, const char* title);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_SHARE_H */
