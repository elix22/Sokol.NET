/* sokol_notifications_ios.m -- UNUserNotificationCenter implementation for the sokol_notifications
   plugin. Events flow into the shared C queue (sokol_notifications_queue.c, linked into this
   framework), so sokolnotify_poll_event works identically on both platforms.

   Local notifications need no entitlement, no APNs and no UIBackgroundModes. A calendar trigger
   fires at the wall-clock time and survives reboots.

   Objective-C rather than Swift for one reason: a tap that COLD-STARTS the app is delivered only to
   a delegate assigned before the app finishes launching -- long before the app's own code runs.
   The constructor below arranges that without touching the host's UIApplicationDelegate. */

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <UserNotifications/UserNotifications.h>
#include <stdatomic.h>
#include "sokol_notifications.h"

/* sokol_notifications_queue.c */
extern void sokolnotify__emit(int type, int value, long long time);

static NSString* const kPrefix = @"sokolnotify-";
static NSString* const kAtKey  = @"sokolnotify_at";

static atomic_int _status = SOKOLNOTIFY_STATUS_NOT_DETERMINED;
static atomic_int _opened = 0;

static int _sn_id_of(NSString* identifier)
{
    if (![identifier hasPrefix:kPrefix]) return 0;
    return [[identifier substringFromIndex:kPrefix.length] intValue];
}

static NSArray<NSString*>* _sn_all_identifiers(void)
{
    NSMutableArray<NSString*>* ids = [NSMutableArray arrayWithCapacity:SOKOLNOTIFY_MAX_ID];
    for (int i = 1; i <= SOKOLNOTIFY_MAX_ID; i++) [ids addObject:[NSString stringWithFormat:@"%@%d", kPrefix, i]];
    return ids;
}

static int _sn_map_status(UNAuthorizationStatus s)
{
    switch (s) {
        case UNAuthorizationStatusNotDetermined: return SOKOLNOTIFY_STATUS_NOT_DETERMINED;
        case UNAuthorizationStatusDenied:        return SOKOLNOTIFY_STATUS_DENIED;
        default:                                 return SOKOLNOTIFY_STATUS_GRANTED; /* authorized, provisional, ephemeral */
    }
}

static void _sn_refresh_status(void)
{
    [[UNUserNotificationCenter currentNotificationCenter]
        getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings* s) {
            atomic_store(&_status, _sn_map_status(s.authorizationStatus));
        }];
}

@interface SokolNotifyDelegate : NSObject <UNUserNotificationCenterDelegate>
@end

@implementation SokolNotifyDelegate
/* The app is in front: the user is already here, so nothing is shown. */
- (void)userNotificationCenter:(UNUserNotificationCenter*)center
       willPresentNotification:(UNNotification*)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completionHandler
{
    completionHandler(UNNotificationPresentationOptionNone);
}

- (void)userNotificationCenter:(UNUserNotificationCenter*)center
didReceiveNotificationResponse:(UNNotificationResponse*)response
         withCompletionHandler:(void (^)(void))completionHandler
{
    int id = _sn_id_of(response.notification.request.identifier);
    if (id > 0) atomic_store(&_opened, id);
    completionHandler();
}
@end

static SokolNotifyDelegate* _delegate;

static void _sn_install_delegate(void)
{
    UNUserNotificationCenter* c = [UNUserNotificationCenter currentNotificationCenter];
    if (!_delegate) _delegate = [SokolNotifyDelegate new];
    if (c.delegate == nil) c.delegate = _delegate;   /* never replace a host's own delegate */
}

/* Runs when the framework loads (it is linked into the app, so: at launch, before main). The
   observers fire inside UIKit's launch sequence, before a cold-start tap is delivered. */
__attribute__((constructor))
static void _sn_boot(void)
{
    NSNotificationCenter* nc = [NSNotificationCenter defaultCenter];
    [nc addObserverForName:UIApplicationDidFinishLaunchingNotification object:nil queue:nil
                usingBlock:^(NSNotification* n) { _sn_install_delegate(); _sn_refresh_status(); }];
    [nc addObserverForName:UIApplicationDidBecomeActiveNotification object:nil queue:nil
                usingBlock:^(NSNotification* n) { _sn_refresh_status(); }];
}

int sokolnotify_init(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{ _sn_install_delegate(); });
    _sn_refresh_status();
    return 1;
}

void sokolnotify_configure(const char* channel_name, const char* small_icon_asset)
{
    (void)channel_name; (void)small_icon_asset;   /* Android only */
}

int sokolnotify_status(void) { return atomic_load(&_status); }

void sokolnotify_request_permission(void)
{
    [[UNUserNotificationCenter currentNotificationCenter]
        requestAuthorizationWithOptions:(UNAuthorizationOptionAlert | UNAuthorizationOptionSound)
                      completionHandler:^(BOOL granted, NSError* error) {
            /* Already refused: iOS answers NO at once without showing anything. */
            int st = granted ? SOKOLNOTIFY_STATUS_GRANTED : SOKOLNOTIFY_STATUS_DENIED;
            atomic_store(&_status, st);
            sokolnotify__emit(SOKOLNOTIFY_EVENT_PERMISSION, st, 0);
        }];
}

void sokolnotify_schedule(int id, long long fire_unix, long long expire_unix,
                          const char* title, const char* body)
{
    (void)expire_unix;   /* iOS fires on time */
    if (id < 1 || id > SOKOLNOTIFY_MAX_ID) return;

    UNMutableNotificationContent* content = [UNMutableNotificationContent new];
    content.title = title ? [NSString stringWithUTF8String:title] : @"";
    content.body  = body  ? [NSString stringWithUTF8String:body]  : @"";
    content.sound = [UNNotificationSound defaultSound];
    content.userInfo = @{ kAtKey: @(fire_unix) };

    /* Local wall-clock components, so the reminder keeps its local time across a zone change. */
    NSDate* when = [NSDate dateWithTimeIntervalSince1970:(NSTimeInterval)fire_unix];
    NSDateComponents* dc = [[NSCalendar currentCalendar]
        components:(NSCalendarUnitYear | NSCalendarUnitMonth | NSCalendarUnitDay |
                    NSCalendarUnitHour | NSCalendarUnitMinute | NSCalendarUnitSecond)
          fromDate:when];
    UNCalendarNotificationTrigger* trigger =
        [UNCalendarNotificationTrigger triggerWithDateMatchingComponents:dc repeats:NO];

    NSString* identifier = [NSString stringWithFormat:@"%@%d", kPrefix, id];
    UNNotificationRequest* req = [UNNotificationRequest requestWithIdentifier:identifier
                                                                      content:content
                                                                      trigger:trigger];
    /* Same identifier = replaces the pending one. */
    [[UNUserNotificationCenter currentNotificationCenter] addNotificationRequest:req withCompletionHandler:nil];
}

void sokolnotify_cancel(int id)
{
    NSArray<NSString*>* ids = @[ [NSString stringWithFormat:@"%@%d", kPrefix, id] ];
    UNUserNotificationCenter* c = [UNUserNotificationCenter currentNotificationCenter];
    [c removePendingNotificationRequestsWithIdentifiers:ids];
    [c removeDeliveredNotificationsWithIdentifiers:ids];
}

void sokolnotify_cancel_all(void)
{
    /* By explicit identifiers, not a get-then-filter round trip: that would complete AFTER a
       schedule() issued right behind this call and remove the new request too. */
    NSArray<NSString*>* ids = _sn_all_identifiers();
    UNUserNotificationCenter* c = [UNUserNotificationCenter currentNotificationCenter];
    [c removePendingNotificationRequestsWithIdentifiers:ids];
    [c removeDeliveredNotificationsWithIdentifiers:ids];
}

void sokolnotify_query_pending(void)
{
    [[UNUserNotificationCenter currentNotificationCenter]
        getPendingNotificationRequestsWithCompletionHandler:^(NSArray<UNNotificationRequest*>* requests) {
            for (UNNotificationRequest* r in requests) {
                int id = _sn_id_of(r.identifier);
                if (id <= 0) continue;
                NSNumber* at = r.content.userInfo[kAtKey];
                sokolnotify__emit(SOKOLNOTIFY_EVENT_PENDING, id, at ? at.longLongValue : 0);
            }
            sokolnotify__emit(SOKOLNOTIFY_EVENT_PENDING_DONE, 0, 0);
        }];
}

int sokolnotify_take_opened(void) { return atomic_exchange(&_opened, 0); }

void sokolnotify_open_settings(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL* url = [NSURL URLWithString:UIApplicationOpenSettingsURLString];
        if (url) [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
    });
}
