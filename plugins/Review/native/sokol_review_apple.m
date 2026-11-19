/* sokol_review_apple.m -- StoreKit's rating prompt (iOS).
   iOS 14+: -[SKStoreReviewController requestReviewInScene:] with the foreground-active window scene
   (falls back to +requestReview when no scene is found); before iOS 14: +requestReview.
   StoreKit decides by itself whether the prompt appears (at most 3 times in 365 days per user; in a
   development build it always appears, and nothing is submitted; in TestFlight it never appears) and
   does not report the outcome -- so the state goes REQUESTING -> DONE.
   Must run on the main thread; a call from another thread is dispatched there.
*/
#include "sokol_review.h"
#import <Foundation/Foundation.h>
#import <StoreKit/StoreKit.h>
#import <UIKit/UIKit.h>
#include <stdatomic.h>

static _Atomic int _rv_state = SOKOLREVIEW_IDLE;

static void _rv_ask(void)
{
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    if (@available(iOS 14.0, *)) {
        UIWindowScene* scene = nil;
        for (UIScene* s in [UIApplication sharedApplication].connectedScenes) {
            if (s.activationState == UISceneActivationStateForegroundActive && [s isKindOfClass:[UIWindowScene class]]) {
                scene = (UIWindowScene*)s;
                break;
            }
        }
        if (scene) [SKStoreReviewController requestReviewInScene:scene];
        else       [SKStoreReviewController requestReview];
    } else {
        [SKStoreReviewController requestReview];
    }
#pragma clang diagnostic pop
    atomic_store(&_rv_state, SOKOLREVIEW_DONE);
}

bool sokolreview_request(void)
{
    if (atomic_load(&_rv_state) == SOKOLREVIEW_REQUESTING) return false;
    atomic_store(&_rv_state, SOKOLREVIEW_REQUESTING);
    if ([NSThread isMainThread]) _rv_ask();
    else dispatch_async(dispatch_get_main_queue(), ^{ _rv_ask(); });
    return true;
}

int  sokolreview_get_state(void)        { return atomic_load(&_rv_state); }
int  sokolreview_get_error(void)        { return 0; }
void sokolreview_test_use_fake(bool on) { (void)on; }
