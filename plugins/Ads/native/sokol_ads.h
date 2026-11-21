/* sokol_ads.h -- Google AdMob interstitials + rewarded ads for Sokol.NET apps.
   Android: Google Mobile Ads SDK + UMP (via a Java helper class).
   iOS:     Google Mobile Ads SDK (Obj-C, presented from sapp's root VC).

   All calls are non-blocking; results surface as queued events drained by
   sokolads_poll_event() — call it once per frame from the game thread (the
   NearNet.Poll model). Events carry no strings.

   The AdMob APPLICATION_ID is platform configuration, not an API input:
   Android reads it from the manifest <meta-data>, iOS from Info.plist's
   GADApplicationIdentifier (both injected by the app builder from
   Directory.Build.props). The app_id parameter of sokolads_init is therefore
   reserved/ignored today — pass NULL.

   Ad UNIT ids are runtime parameters. Google's published TEST units work in
   any build without store setup:
     Android interstitial: ca-app-pub-3940256099942544/1033173712
     iOS interstitial:     ca-app-pub-3940256099942544/4411468910
     Android rewarded:     ca-app-pub-3940256099942544/5224354917
     iOS rewarded:         ca-app-pub-3940256099942544/1712485313

   Rewarded ads are a second, independent slot with their own events (7..11),
   so an interstitial and a rewarded ad can be preloaded at the same time.
   Grant the reward on REWARD_EARNED only — never on DISMISSED (a player who
   closes the ad early gets DISMISSED without REWARD_EARNED).
*/
#pragma once
#ifndef SOKOL_ADS_H
#define SOKOL_ADS_H

#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum sokolads_event_type {
    SOKOLADS_EVENT_NONE           = 0,
    SOKOLADS_EVENT_LOADED         = 1, /* interstitial preloaded, ready to show   */
    SOKOLADS_EVENT_LOAD_FAILED    = 2, /* code = SDK error code (no fill/network) */
    SOKOLADS_EVENT_SHOWN          = 3, /* fullscreen content on screen            */
    SOKOLADS_EVENT_DISMISSED      = 4, /* user closed it — resume music/navigation */
    SOKOLADS_EVENT_CONSENT_READY  = 5, /* UMP flow finished; ads may be requested */
    SOKOLADS_EVENT_CONSENT_FAILED = 6, /* code = UMP error; proceed without ads   */
    SOKOLADS_EVENT_REWARDED_LOADED      = 7,  /* rewarded ad preloaded              */
    SOKOLADS_EVENT_REWARDED_LOAD_FAILED = 8,  /* code = SDK error (also failed show) */
    SOKOLADS_EVENT_REWARDED_SHOWN       = 9,  /* rewarded ad on screen              */
    SOKOLADS_EVENT_REWARDED_DISMISSED   = 10, /* closed — after REWARD_EARNED if the player watched it */
    SOKOLADS_EVENT_REWARD_EARNED        = 11, /* code = reward amount; grant it now */
    SOKOLADS_EVENT_PRIVACY_DONE         = 12, /* privacy options form closed; code = UMP error or 0 */
} sokolads_event_type;

typedef struct sokolads_event {
    int type;   /* sokolads_event_type                        */
    int code;   /* SDK-specific error code on *_FAILED, else 0 */
} sokolads_event;

/* Start the SDK. npa_only: request non-personalized ads only (no ATT/limited
   consent surface, §1.3); child_directed: COPPA tag. Max ad content rating is
   pinned to G in the shims. app_id is reserved — pass NULL (see header note). */
void sokolads_init(const char* app_id, bool npa_only, bool child_directed);

/* Run the UMP consent flow (shows the form only where required). Call at the
   first ad-eligible moment, not at app start -> CONSENT_READY | CONSENT_FAILED. */
void sokolads_consent_gather(void);

/* Preload an interstitial -> LOADED | LOAD_FAILED. Safe to call again after
   the previous ad was consumed or failed. */
void sokolads_load_interstitial(const char* ad_unit_id);

/* True while a loaded interstitial is waiting to be shown. */
bool sokolads_interstitial_ready(void);

/* Show the loaded interstitial (consumes it) -> SHOWN then DISMISSED.
   No-op when none is ready. */
void sokolads_show_interstitial(void);

/* Preload a rewarded ad -> REWARDED_LOADED | REWARDED_LOAD_FAILED. */
void sokolads_load_rewarded(const char* ad_unit_id);

/* True while a loaded rewarded ad is waiting to be shown. */
bool sokolads_rewarded_ready(void);

/* Show the loaded rewarded ad (consumes it) -> REWARDED_SHOWN, REWARD_EARNED
   (only when watched long enough), then REWARDED_DISMISSED. A failed show
   emits REWARDED_LOAD_FAILED then REWARDED_DISMISSED. No-op when none ready. */
void sokolads_show_rewarded(void);

/* True when the player must be able to change their consent choice (UMP's
   privacy options requirement: EEA / UK users once a GDPR message is published
   in AdMob). Known after the consent flow (CONSENT_READY / CONSENT_FAILED);
   false before it and where not required. Show a "Privacy options" entry in
   the app's settings while it is true. */
bool sokolads_privacy_options_required(void);
/* Open UMP's privacy options form (the player changes or withdraws consent)
   -> PRIVACY_DONE (code = UMP error, 0 when the form closed normally). */
void sokolads_show_privacy_options(void);
/* Pop one queued event. Returns false when the queue is empty. */
bool sokolads_poll_event(sokolads_event* out);

#ifdef __cplusplus
}
#endif
#endif /* SOKOL_ADS_H */
