/* sokol_filepicker.h -- the system's own file pickers for Sokol.NET apps: open a file the user picks, or save to a
   place the user picks, anywhere the system's picker reaches (device storage, iCloud Drive, Google Drive, Dropbox,
   OneDrive, ... through their document providers), and keep access to it across launches (recent files).

   Android: Storage Access Framework (ACTION_OPEN_DOCUMENT / ACTION_CREATE_DOCUMENT) from a see-through helper
            activity (platform/android/java), persisted URI permissions; I/O through the ContentResolver (JNI).
   iOS:     UIDocumentPickerViewController (open; save = export of the bytes), security-scoped bookmarks.
   macOS:   NSOpenPanel / NSSavePanel as sheets on the app's window, bookmarks.
   Windows + Linux: no native library -- the managed layer uses the common dialogs / zenity or kdialog.

   A handle is an opaque UTF-8 string (Android: a content:// URI, Apple: a file URL). Strings returned by these
   functions stay valid until the next call of the same function. Call start/poll from the game thread; load/take,
   write, size and modified may run on a worker thread (one at a time). */
#pragma once
#ifndef SOKOL_FILEPICKER_H
#define SOKOL_FILEPICKER_H

#include <stdbool.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define SOKOLFILEPICKER_IDLE      0
#define SOKOLFILEPICKER_PICKED    1
#define SOKOLFILEPICKER_CANCELLED 2
#define SOKOLFILEPICKER_FAILED    3
#define SOKOLFILEPICKER_RUNNING   4

/* Show the picker. mode 0: open an existing file; `extensions` is a ';' list without dots ("map;json", "" = any)
   used where the system can filter. mode 1: save `data` (`len` bytes) as a new file the user names and places,
   starting from `name`. False when a picker is already up or none can be shown. */
bool sokolfilepicker_start(int mode, const char* extensions, const char* name, const unsigned char* data, int64_t len);

/* The request's state; a finished state (PICKED, CANCELLED, FAILED) is reported once, then IDLE. */
int sokolfilepicker_poll(void);
/* After PICKED: the file's handle and its name as the user sees it. */
const char* sokolfilepicker_result_handle(void);
const char* sokolfilepicker_result_name(void);

/* Reads the whole file into the plugin's buffer: its length, or -1 when it cannot be read. */
int64_t sokolfilepicker_load(const char* handle);
/* Copies the loaded bytes (the length load returned) to `dst` and frees the buffer. */
void sokolfilepicker_take(unsigned char* dst);
/* Replaces the file's content. */
bool sokolfilepicker_write(const char* handle, const unsigned char* data, int64_t len);
/* Size in bytes and last change in Unix milliseconds; -1 when unknown or the file is gone. */
int64_t sokolfilepicker_size(const char* handle);
int64_t sokolfilepicker_modified(const char* handle);

/* A token that keeps access to the file after a restart (store it with the recent-files list), or NULL. */
const char* sokolfilepicker_bookmark(const char* handle);
/* The handle a token stands for, or NULL when access was lost or the file is gone. */
const char* sokolfilepicker_resolve(const char* token);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_FILEPICKER_H */
