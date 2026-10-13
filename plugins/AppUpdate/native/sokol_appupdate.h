/* sokol_appupdate.h -- Google Play In-App Updates for Sokol.NET apps (Android only).
   Android: com.google.android.play:app-update (AppUpdateManager) via a Java helper class.
   Other platforms have no native library; the managed layer reports "unsupported".

   All calls are non-blocking. Play's answers arrive on the Android UI thread and are
   folded into ONE state snapshot; the app reads it with sokolappupdate_get_state() from
   its game thread (cheap -- no JNI) and watches `serial` for changes.

   Only an app INSTALLED FROM GOOGLE PLAY gets update information. A sideloaded build
   reports an error (availability -1); sokolappupdate_test_use_fake() swaps in Play's
   FakeAppUpdateManager so the whole flow can be driven on such a build.
*/
#pragma once
#ifndef SOKOL_APPUPDATE_H
#define SOKOL_APPUPDATE_H

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

/* availability: Play's UpdateAvailability, plus -1 for "the check failed" (see error_code). */
#define SOKOLAPPUPDATE_AVAIL_ERROR        (-1)
#define SOKOLAPPUPDATE_AVAIL_UNKNOWN        0   /* not checked yet / Play does not know  */
#define SOKOLAPPUPDATE_AVAIL_NONE           1   /* UPDATE_NOT_AVAILABLE                  */
#define SOKOLAPPUPDATE_AVAIL_AVAILABLE      2   /* UPDATE_AVAILABLE                      */
#define SOKOLAPPUPDATE_AVAIL_IN_PROGRESS    3   /* DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS */

/* flow: what became of the last sokolappupdate_start_flexible(). */
#define SOKOLAPPUPDATE_FLOW_IDLE      0
#define SOKOLAPPUPDATE_FLOW_SHOWING   1   /* Play's confirmation sheet is up        */
#define SOKOLAPPUPDATE_FLOW_ACCEPTED  2   /* RESULT_OK: the download is starting    */
#define SOKOLAPPUPDATE_FLOW_DECLINED  3   /* RESULT_CANCELED: the user said no      */
#define SOKOLAPPUPDATE_FLOW_FAILED    4   /* RESULT_IN_APP_UPDATE_FAILED / an error */

typedef struct sokolappupdate_state {
    int serial;                 /* bumps on every change                                       */
    int availability;           /* SOKOLAPPUPDATE_AVAIL_*                                       */
    int flexible_allowed;       /* 1 = AppUpdateType.FLEXIBLE is allowed for this update        */
    int install_status;         /* Play's InstallStatus: 0 unknown, 1 pending, 2 downloading,
                                   3 installing, 4 installed, 5 failed, 6 canceled, 11 downloaded */
    int available_version_code; /* the update's versionCode (0 = none)                          */
    int staleness_days;         /* days since Play learned of the update, -1 = unknown          */
    int flow;                   /* SOKOLAPPUPDATE_FLOW_*                                        */
    int error_code;             /* Play's InstallErrorCode of the last failure, 0 = none        */
    long long bytes_downloaded;
    long long total_bytes;
} sokolappupdate_state;

/* Create the AppUpdateManager and listen for install progress. Idempotent. */
void sokolappupdate_init(void);

/* Ask Play whether an update exists (async; the state's availability answers).
   Call at start and on every resume -- a download finished while the app was in the
   background shows up as install_status 11 (DOWNLOADED). */
void sokolappupdate_check(void);

/* Show Play's FLEXIBLE update sheet for the update the last check found. Returns false
   when there is nothing to start (no fresh check, or FLEXIBLE not allowed). Each check
   result starts at most one flow -- check again before asking again. */
bool sokolappupdate_start_flexible(void);

/* Install a DOWNLOADED flexible update: Play shows its full-screen install UI and
   restarts the app. */
void sokolappupdate_complete(void);

/* Copy the current state snapshot (any thread). */
void sokolappupdate_get_state(sokolappupdate_state* out);

/* ── Testing (Play's FakeAppUpdateManager) ─────────────────────────────────────
   use_fake: replace the real manager with a fake that offers `version_code` as a
   FLEXIBLE update (0 = back to the real manager).
   fake: drive the fake -- "accept", "reject", "download-start", "download-complete",
   "download-fail", "cancel-download", "install-complete", "install-fail".
   fake_flags: bit 0 = confirmation sheet visible, bit 1 = install splash visible. */
void sokolappupdate_test_use_fake(int version_code);
void sokolappupdate_test_fake(const char* action);
int  sokolappupdate_test_fake_flags(void);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_APPUPDATE_H */
