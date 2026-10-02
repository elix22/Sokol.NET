/* sokol_links_apple.m -- open a link with the system handler (iOS + macOS).
   iOS:   -[UIApplication openURL:options:completionHandler:] (main thread; the result is
          asynchronous, so true = handed to the system).
   macOS: -[NSWorkspace openURL:] (synchronous: false when no app handles the URL).
   A mailto: URL opens the default mail app on both. No Info.plist key is needed: the
   LSApplicationQueriesSchemes list only gates canOpenURL:, which is not used here.
*/
#include "sokol_links.h"
#import <Foundation/Foundation.h>
#include <TargetConditionals.h>
#if TARGET_OS_IPHONE
#import <UIKit/UIKit.h>
#else
#import <AppKit/AppKit.h>
#endif

bool sokollinks_open(const char* url)
{
    if (!url || !*url) return false;
    NSString* s = [NSString stringWithUTF8String:url];
    NSURL* u = s ? [NSURL URLWithString:s] : nil;
    if (!u || !u.scheme) return false;
#if TARGET_OS_IPHONE
    void (^open)(void) = ^{ [[UIApplication sharedApplication] openURL:u options:@{} completionHandler:nil]; };
    if ([NSThread isMainThread]) open(); else dispatch_async(dispatch_get_main_queue(), open);
    return true;
#else
    return [[NSWorkspace sharedWorkspace] openURL:u];
#endif
}
