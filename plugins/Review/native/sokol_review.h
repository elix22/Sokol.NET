/* sokol_review.h -- the platform's own in-app rating prompt, for Sokol.NET apps.
   Android: Google Play In-App Review (com.google.android.play:review) via a Java helper class.
   iOS:     StoreKit AppStore.requestReview(in:) (iOS 16+; Swift, sokol_review_apple.swift). iOS 15: no prompt --
            its only API, SKStoreReviewController, is deprecated; sokolreview_request() returns false there.
   Other platforms have no native library; the managed layer reports "unsupported".

   Both stores decide by themselves whether the prompt really appears (quota: iOS at most 3 times in
   365 days per user; Google Play a time-bound quota) and NEITHER reports whether it was shown or what
   the user did -- by design. So the state only says the request was made and the flow finished.

   Store guidance the APP must follow (the plugin opens what it is asked to):
     - ask at a natural pause after a positive moment (a finished level, a win), never mid-task,
       never at first launch, never from a button ("Rate us" buttons must open the store page instead);
     - do not ask more often than the store would show it anyway.
*/
#pragma once
#ifndef SOKOL_REVIEW_H
#define SOKOL_REVIEW_H

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

#define SOKOLREVIEW_IDLE        0   /* never requested                                           */
#define SOKOLREVIEW_REQUESTING  1   /* asked the store; its flow is running                      */
#define SOKOLREVIEW_DONE        2   /* the store's flow finished (shown or not -- never reported) */
#define SOKOLREVIEW_FAILED      3   /* the store refused the request (see error code)            */

/* Ask the store to show its rating prompt (async, non-blocking). Call from the app's main/game thread.
   Returns false when nothing could be started (no activity yet, or a request is already running). */
bool sokolreview_request(void);

/* SOKOLREVIEW_* of the last request (any thread). */
int sokolreview_get_state(void);

/* Android: Play's ReviewErrorCode of the last failure (0 = none); iOS: always 0. */
int sokolreview_get_error(void);

/* Android testing: true = Play's FakeReviewManager (completes without UI and without any call to Play),
   false = the real manager. iOS: no effect. */
void sokolreview_test_use_fake(bool on);

/* Testing: how this copy was installed, so a test can refuse to ask the real store where a real review could be posted.
   Android: the installer package ("com.android.vending" = Google Play — for a debug/test build that can only be Play's
   internal app sharing, where Google disables the submit button; "" = sideloaded / adb).
   iOS: "development" (a development-signed build: StoreKit always shows the prompt and submits nothing), "testflight",
   "appstore" or "adhoc". Copies a NUL-terminated string into buf (at most cap bytes) and returns its length. */
int sokolreview_test_install_source(char* buf, int cap);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_REVIEW_H */
