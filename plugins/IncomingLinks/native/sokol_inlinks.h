/* sokol_inlinks.h -- receive the link that opened the app (App Link / custom URL scheme), for Sokol.NET apps.
   Android: SokolNativeActivity records the ACTION_VIEW intent's URI (onCreate = cold start, onNewIntent = the
            app was already running) and this library takes it over JNI. Requires AndroidFullscreen=true
            (SokolNativeActivity); with the plain NativeActivity only the cold-start link is seen.
   Other platforms: not implemented yet -- the managed layer reports Supported == false.
*/
#pragma once
#ifndef SOKOL_INLINKS_H
#define SOKOL_INLINKS_H

#ifdef __cplusplus
extern "C" {
#endif

/* The link that opened the app since the last call (UTF-8, exactly as the system delivered it, fragment
   included), or NULL when there is none. Each link is returned once. The string stays valid until the next
   call. Call it from the game thread, e.g. once per frame. */
const char* sokolinlinks_take(void);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_INLINKS_H */
