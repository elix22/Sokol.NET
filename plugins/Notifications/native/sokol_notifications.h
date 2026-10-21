/* sokol_notifications.h -- local (on-device) scheduled notifications for Sokol.NET apps.
   Android: WorkManager one-time work posts the notification (via a Java helper class).
   iOS:     UNUserNotificationCenter with a calendar trigger (Objective-C).

   No push, no server: the app schedules a notification with its own text and the OS shows it.
   The plugin holds no text of its own -- title and body come from the app, already localized.

   Ids are small integers chosen by the app, 1..63 (iOS keeps at most 64 pending requests per
   app). Scheduling an id that is already pending replaces it.

   Asynchronous answers (permission result, pending list) surface as queued events drained by
   sokolnotify_poll_event() -- call it from the game thread once per frame.
*/
#pragma once
#ifndef SOKOL_NOTIFICATIONS_H
#define SOKOL_NOTIFICATIONS_H

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

#define SOKOLNOTIFY_MAX_ID 63

typedef enum sokolnotify_status_t {
    SOKOLNOTIFY_STATUS_NOT_DETERMINED = 0, /* never asked: request_permission() may show a prompt */
    SOKOLNOTIFY_STATUS_GRANTED        = 1,
    SOKOLNOTIFY_STATUS_DENIED         = 2, /* refused, or switched off in the system settings     */
} sokolnotify_status_t;

typedef enum sokolnotify_event_type {
    SOKOLNOTIFY_EVENT_NONE         = 0,
    SOKOLNOTIFY_EVENT_PERMISSION   = 1, /* value = sokolnotify_status_t after the request      */
    SOKOLNOTIFY_EVENT_PENDING      = 2, /* value = id, time = fire time (unix seconds, UTC)     */
    SOKOLNOTIFY_EVENT_PENDING_DONE = 3, /* end of the list started by sokolnotify_query_pending */
} sokolnotify_event_type;

typedef struct sokolnotify_event {
    int type;       /* sokolnotify_event_type */
    int value;
    long long time;
} sokolnotify_event;

/* Connect to the platform. Returns 1 when notifications are available. Call once at app start. */
int  sokolnotify_init(void);

/* Android: the notification channel's user-visible name (shown in the system settings) and an
   optional small icon, a path inside the APK's assets (a white silhouette on transparency; NULL =
   the app icon). Used for notifications scheduled after this call. No-op on iOS. */
void sokolnotify_configure(const char* channel_name, const char* small_icon_asset);

/* Current permission, sokolnotify_status_t. Synchronous; on iOS the value is refreshed at
   launch, on every return to the foreground and after a request. */
int  sokolnotify_status(void);

/* Ask the user (Android 13+: POST_NOTIFICATIONS; iOS: alert + sound) -> PERMISSION event.
   Answers at once when the outcome is already known (granted, or no prompt possible). */
void sokolnotify_request_permission(void);

/* Show (title, body) at fire_unix (UTC seconds). Android may run late (Doze); when it would run
   after expire_unix (> 0) the notification is dropped instead. iOS fires on time. */
void sokolnotify_schedule(int id, long long fire_unix, long long expire_unix,
                          const char* title, const char* body);

/* Cancel one id / every id: the pending notification and, if shown, the shown one. */
void sokolnotify_cancel(int id);
void sokolnotify_cancel_all(void);

/* List what is pending -> zero or more PENDING events, then PENDING_DONE. */
void sokolnotify_query_pending(void);

/* The id of the notification the user tapped to open the app, once; 0 when there is none.
   Poll it at start and whenever the app returns to the foreground. */
int  sokolnotify_take_opened(void);

/* Open this app's notification settings (to switch notifications back on after a denial). */
void sokolnotify_open_settings(void);

/* Pop one queued event. Returns false when the queue is empty. */
bool sokolnotify_poll_event(sokolnotify_event* out);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_NOTIFICATIONS_H */
