/* sokol_links.h -- open a link with the system's own handler, for Sokol.NET apps.
   A web URL (http/https) opens in the default browser, a mailto: URL in the mail app.
   Android: Intent ACTION_VIEW (web) / ACTION_SENDTO (mailto), started from the NativeActivity (pure JNI).
   iOS:     UIApplication openURL:options:completionHandler: (Objective-C shim).
   macOS:   NSWorkspace openURL: (same shim).
   Web:     window.open (web) / an anchor click (mailto) via Emscripten JS interop.
   Windows + Linux: no native library -- the managed layer uses ShellExecute / xdg-open.
*/
#pragma once
#ifndef SOKOL_LINKS_H
#define SOKOL_LINKS_H

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Open `url` (UTF-8, absolute, e.g. "https://example.com" or "mailto:a@b.c"). Call from the
   game thread inside the input handling that asked for it (a browser only lets a page open a
   window shortly after a user gesture).
   Returns true when the system accepted the request, false when nothing can open it (no browser
   or mail app, malformed URL, popup blocked). iOS answers asynchronously, so there true means
   "handed to the system". Let the user copy the link as the fallback. */
bool sokollinks_open(const char* url);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_LINKS_H */
