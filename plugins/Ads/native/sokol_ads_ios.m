/* sokol_ads_ios.m -- Google Mobile Ads implementation for Sokol.NET (iOS).
   Requires GoogleMobileAds.xcframework + UserMessagingPlatform.xcframework
   (fetched by scripts/fetch-googlemobileads-ios.sh — not vendored in the repo).

   ⛔ The Google SDK is linked into the APP EXECUTABLE, not into this dylib.
   GoogleMobileAds 13+ is a STATIC SWIFT framework; absorbed into a clang-built
   plugin dylib the Swift runtime aborts at launch resolving symbolic type
   references (device-verified on iPhone SE). The app builder links the SDK
   into the app binary (IOSStaticFrameworks_* prop, with -ObjC so the classes
   survive although nothing in the exe references them); this shim stays a THIN
   dylib that reaches the SDK classes through the ObjC runtime
   (NSClassFromString), so it carries no GMA/UMP/Swift link dependencies at
   all. The headers are still required at compile time for the types,
   protocols and block signatures.

   The APPLICATION_ID comes from Info.plist's GADApplicationIdentifier, injected
   by the app builder. Interstitials present from the key window's root VC. */
#import <UIKit/UIKit.h>
#import <objc/message.h>
#import <GoogleMobileAds/GoogleMobileAds.h>
#import <UserMessagingPlatform/UserMessagingPlatform.h>

#include "sokol_ads.h"

/* sokol_ads_queue.c */
extern void sokolads__emit(int type, int code);
extern void sokolads__consume(void);
extern void sokolads__consume_rewarded(void);

/* Emitted as the code of *_FAILED when the app executable was built without
   the Google SDK — the managed layer then backs off exactly like a no-fill. */
#define SOKOLADS_ERR_NO_SDK (-1)

static bool _sa_npa_only;
static GADInterstitialAd* _sa_loaded;
static id<GADFullScreenContentDelegate> _sa_delegate;
static GADRewardedAd* _sa_loaded_rw;
static id<GADFullScreenContentDelegate> _sa_delegate_rw;

@interface SokolAdsDelegate : NSObject <GADFullScreenContentDelegate>
@end

@implementation SokolAdsDelegate
- (void)adWillPresentFullScreenContent:(id<GADFullScreenPresentingAd>)ad {
    sokolads__emit(SOKOLADS_EVENT_SHOWN, 0);
}
- (void)adDidDismissFullScreenContent:(id<GADFullScreenPresentingAd>)ad {
    sokolads__emit(SOKOLADS_EVENT_DISMISSED, 0);
}
- (void)ad:(id<GADFullScreenPresentingAd>)ad
    didFailToPresentFullScreenContentWithError:(NSError*)error {
    /* Never shown — report failure, then DISMISSED so the app's resume path
       (music/navigation) runs exactly once either way. */
    sokolads__emit(SOKOLADS_EVENT_LOAD_FAILED, (int)error.code);
    sokolads__emit(SOKOLADS_EVENT_DISMISSED, 0);
}
@end

/* The rewarded slot's own delegate, so its events never mix with the
   interstitial's (both may be preloaded at once). */
@interface SokolAdsRewardedDelegate : NSObject <GADFullScreenContentDelegate>
@end

@implementation SokolAdsRewardedDelegate
- (void)adWillPresentFullScreenContent:(id<GADFullScreenPresentingAd>)ad {
    sokolads__emit(SOKOLADS_EVENT_REWARDED_SHOWN, 0);
}
- (void)adDidDismissFullScreenContent:(id<GADFullScreenPresentingAd>)ad {
    sokolads__emit(SOKOLADS_EVENT_REWARDED_DISMISSED, 0);
}
- (void)ad:(id<GADFullScreenPresentingAd>)ad
    didFailToPresentFullScreenContentWithError:(NSError*)error {
    sokolads__emit(SOKOLADS_EVENT_REWARDED_LOAD_FAILED, (int)error.code);
    sokolads__emit(SOKOLADS_EVENT_REWARDED_DISMISSED, 0);
}
@end

/* The view controller to present from: the KEY window of the foreground-active
   window scene, walked down to the topmost already-presented controller.
   Not `UIApplication.windows.firstObject` — that API is deprecated, its order is
   undefined (a system text-effects/keyboard window can come first), and
   presenting on a controller that already presents one fails outright. */
static UIViewController* _sa_root_vc(void) {
    UIWindow* key = nil;
    UIWindow* anyVisible = nil;
    for (UIScene* scene in UIApplication.sharedApplication.connectedScenes) {
        if (![scene isKindOfClass:UIWindowScene.class]) continue;
        if (scene.activationState != UISceneActivationStateForegroundActive) continue;
        for (UIWindow* w in ((UIWindowScene*)scene).windows) {
            if (w.hidden) continue;
            if (w.isKeyWindow) { key = w; break; }
            if (!anyVisible) anyVisible = w;
        }
        if (key) break;
    }
    UIWindow* win = key ?: anyVisible;
    UIViewController* vc = win.rootViewController;
    /* Presenting on a controller that already presents one fails outright. */
    while (vc.presentedViewController) vc = vc.presentedViewController;
    return vc;
}

/* SDK singletons resolved from the MAIN IMAGE at runtime; the typed
   objc_msgSend casts keep exactly the signatures the headers declare. */
static GADMobileAds* _sa_gad(void) {
    Class cls = NSClassFromString(@"GADMobileAds");
    if (!cls) return nil;
    return ((GADMobileAds* (*)(Class, SEL))objc_msgSend)(cls, @selector(sharedInstance));
}

void sokolads_init(const char* app_id, bool npa_only, bool child_directed) {
    (void)app_id;   /* iOS reads GADApplicationIdentifier from Info.plist */
    _sa_npa_only = npa_only;
    dispatch_async(dispatch_get_main_queue(), ^{
        GADMobileAds* gad = _sa_gad();
        if (!gad) return;   /* SDK not linked into the app — shim stays inert */
        /* Family app: never serve above-G inventory (MONETIZATION_FEATURES §1.3).
           @"G" == GADMaxAdContentRatingGeneral (verified against the GMA 13.7
           binary — this dylib deliberately links no GMA symbols, so it cannot
           use the extern constant). */
        gad.requestConfiguration.maxAdContentRating = @"G";
        if (child_directed) {
            gad.requestConfiguration.tagForChildDirectedTreatment = @YES;
        }
        [gad startWithCompletionHandler:nil];
    });
}

void sokolads_consent_gather(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        Class infoCls   = NSClassFromString(@"UMPConsentInformation");
        Class paramsCls = NSClassFromString(@"UMPRequestParameters");
        Class formCls   = NSClassFromString(@"UMPConsentForm");
        if (!infoCls || !paramsCls || !formCls) {
            sokolads__emit(SOKOLADS_EVENT_CONSENT_FAILED, SOKOLADS_ERR_NO_SDK);
            return;
        }
        UMPRequestParameters* params = [[paramsCls alloc] init];
        UMPConsentInformation* info =
            ((UMPConsentInformation* (*)(Class, SEL))objc_msgSend)(infoCls, @selector(sharedInstance));
        [info requestConsentInfoUpdateWithParameters:params
            completionHandler:^(NSError* _Nullable error) {
                if (error) {
                    sokolads__emit(SOKOLADS_EVENT_CONSENT_FAILED, (int)error.code);
                    return;
                }
                [formCls loadAndPresentIfRequiredFromViewController:_sa_root_vc()
                    completionHandler:^(NSError* _Nullable formError) {
                        if (formError) sokolads__emit(SOKOLADS_EVENT_CONSENT_FAILED, (int)formError.code);
                        else           sokolads__emit(SOKOLADS_EVENT_CONSENT_READY, 0);
                    }];
            }];
    });
}

bool sokolads_privacy_options_required(void) {
    /* sokol_app's frame runs on the main thread on iOS, where UMP's state lives */
    Class infoCls = NSClassFromString(@"UMPConsentInformation");
    if (!infoCls) return false;
    UMPConsentInformation* info =
        ((UMPConsentInformation* (*)(Class, SEL))objc_msgSend)(infoCls, @selector(sharedInstance));
    return info.privacyOptionsRequirementStatus == UMPPrivacyOptionsRequirementStatusRequired;
}

void sokolads_show_privacy_options(void) {
    dispatch_async(dispatch_get_main_queue(), ^{
        Class formCls = NSClassFromString(@"UMPConsentForm");
        if (!formCls) { sokolads__emit(SOKOLADS_EVENT_PRIVACY_DONE, SOKOLADS_ERR_NO_SDK); return; }
        [formCls presentPrivacyOptionsFormFromViewController:_sa_root_vc()
            completionHandler:^(NSError* _Nullable formError) {
                sokolads__emit(SOKOLADS_EVENT_PRIVACY_DONE, formError ? (int)formError.code : 0);
            }];
    });
}

static GADRequest* _sa_request(Class reqCls) {
    GADRequest* request = ((GADRequest* (*)(Class, SEL))objc_msgSend)(reqCls, @selector(request));
    if (_sa_npa_only) {
        Class extrasCls = NSClassFromString(@"GADExtras");
        if (extrasCls) {
            GADExtras* extras = [[extrasCls alloc] init];
            extras.additionalParameters = @{ @"npa" : @"1" };
            [request registerAdNetworkExtras:extras];
        }
    }
    return request;
}

void sokolads_load_interstitial(const char* ad_unit_id) {
    if (!ad_unit_id) return;
    NSString* unit = [NSString stringWithUTF8String:ad_unit_id];
    dispatch_async(dispatch_get_main_queue(), ^{
        Class interCls = NSClassFromString(@"GADInterstitialAd");
        Class reqCls   = NSClassFromString(@"GADRequest");
        if (!interCls || !reqCls) {
            _sa_loaded = nil;
            sokolads__emit(SOKOLADS_EVENT_LOAD_FAILED, SOKOLADS_ERR_NO_SDK);
            return;
        }
        GADRequest* request = _sa_request(reqCls);
        [interCls loadWithAdUnitID:unit
                           request:request
                 completionHandler:^(GADInterstitialAd* ad, NSError* error) {
            if (error) {
                _sa_loaded = nil;
                sokolads__emit(SOKOLADS_EVENT_LOAD_FAILED, (int)error.code);
                return;
            }
            if (!_sa_delegate) _sa_delegate = [[SokolAdsDelegate alloc] init];
            ad.fullScreenContentDelegate = _sa_delegate;
            _sa_loaded = ad;
            sokolads__emit(SOKOLADS_EVENT_LOADED, 0);
        }];
    });
}

void sokolads_show_interstitial(void) {
    if (!sokolads_interstitial_ready()) return;
    sokolads__consume();
    dispatch_async(dispatch_get_main_queue(), ^{
        GADInterstitialAd* ad = _sa_loaded;
        _sa_loaded = nil;
        if (!ad) return;
        [ad presentFromRootViewController:_sa_root_vc()];
    });
}

void sokolads_load_rewarded(const char* ad_unit_id) {
    if (!ad_unit_id) return;
    NSString* unit = [NSString stringWithUTF8String:ad_unit_id];
    dispatch_async(dispatch_get_main_queue(), ^{
        Class rwCls  = NSClassFromString(@"GADRewardedAd");
        Class reqCls = NSClassFromString(@"GADRequest");
        if (!rwCls || !reqCls) {
            _sa_loaded_rw = nil;
            sokolads__emit(SOKOLADS_EVENT_REWARDED_LOAD_FAILED, SOKOLADS_ERR_NO_SDK);
            return;
        }
        [rwCls loadWithAdUnitID:unit
                        request:_sa_request(reqCls)
              completionHandler:^(GADRewardedAd* ad, NSError* error) {
            if (error) {
                _sa_loaded_rw = nil;
                sokolads__emit(SOKOLADS_EVENT_REWARDED_LOAD_FAILED, (int)error.code);
                return;
            }
            if (!_sa_delegate_rw) _sa_delegate_rw = [[SokolAdsRewardedDelegate alloc] init];
            ad.fullScreenContentDelegate = _sa_delegate_rw;
            _sa_loaded_rw = ad;
            sokolads__emit(SOKOLADS_EVENT_REWARDED_LOADED, 0);
        }];
    });
}

void sokolads_show_rewarded(void) {
    if (!sokolads_rewarded_ready()) return;
    sokolads__consume_rewarded();
    dispatch_async(dispatch_get_main_queue(), ^{
        GADRewardedAd* ad = _sa_loaded_rw;
        _sa_loaded_rw = nil;
        if (!ad) return;
        [ad presentFromRootViewController:_sa_root_vc()
                 userDidEarnRewardHandler:^{
            sokolads__emit(SOKOLADS_EVENT_REWARD_EARNED, ad.adReward.amount.intValue);
        }];
    });
}
