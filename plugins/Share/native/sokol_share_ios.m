/* sokol_share_ios.m -- iOS share sheet implementation for Sokol.NET.
   Uses UIActivityViewController dispatched to the main queue.
   No special entitlements or Info.plist keys required for sharing in-app
   generated images (UIActivityViewController handles everything).
*/
#import <UIKit/UIKit.h>
#include "sokol_share.h"

void sokolshare_image_text(const char* image_path, const char* text)
{
    NSString* nsText = [NSString stringWithUTF8String:text ? text : ""];
    UIImage*  image  = nil;
    if (image_path && image_path[0])
    {
        NSString* nsPath = [NSString stringWithUTF8String:image_path];
        image = [UIImage imageWithContentsOfFile:nsPath];
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        NSMutableArray* items = [NSMutableArray array];
        if (image)    [items addObject:image];
        [items addObject:nsText];

        UIActivityViewController* vc =
            [[UIActivityViewController alloc] initWithActivityItems:items
                                              applicationActivities:nil];

        UIViewController* root =
            [UIApplication sharedApplication].windows.firstObject.rootViewController;
        if (!root) return;

        /* iPad requires a popover source anchor */
        if (UIDevice.currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad)
        {
            vc.popoverPresentationController.sourceView = root.view;
            vc.popoverPresentationController.sourceRect =
                CGRectMake(root.view.bounds.size.width  / 2.0,
                           root.view.bounds.size.height / 2.0, 1.0, 1.0);
        }

        [root presentViewController:vc animated:YES completion:nil];
    });
}

/* Clipboard. sapp_set_clipboard_string() is a no-op on iOS (sokol_app.h implements
   it for macOS/Win32/X11/emscripten only), so it lives here. UIPasteboard is UIKit
   state, so the write is dispatched to the main queue like the share sheet above. */
void sokolshare_set_clipboard(const char* text)
{
    NSString* nsText = [NSString stringWithUTF8String:text ? text : ""];
    dispatch_async(dispatch_get_main_queue(), ^{
        [UIPasteboard generalPasteboard].string = nsText;
    });
}

/* Any file: the file's URL as the one item, so receivers get the file itself with its name and type (Save to Files,
   AirDrop, Mail, Messages, ...). Presented from the top-most controller: the app may still be presenting something
   (a document picker that just closed). iPad needs a popover anchor, as above. */
static UIViewController* sokolshare_top_controller(void)
{
    UIWindow* win = nil;
    UIWindow* first = nil;
    for (UIScene* scene in [UIApplication sharedApplication].connectedScenes) {
        if (![scene isKindOfClass:[UIWindowScene class]]) continue;
        for (UIWindow* w in ((UIWindowScene*)scene).windows) {
            if (!first) first = w;
            if (w.isKeyWindow) { win = w; break; }
        }
        if (win) break;
    }
    if (!win) win = first;
    UIViewController* vc = win.rootViewController;
    while (vc.presentedViewController) vc = vc.presentedViewController;
    return vc;
}

int sokolshare_file(const char* path, const char* mime, const char* title)
{
    (void)mime; (void)title;
    if (!path || !path[0]) return 0;
    NSString* nsPath = [NSString stringWithUTF8String:path];
    if (!nsPath || ![[NSFileManager defaultManager] isReadableFileAtPath:nsPath]) return 0;
    NSURL* url = [NSURL fileURLWithPath:nsPath];

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* top = sokolshare_top_controller();
        if (!top) return;
        UIActivityViewController* vc =
            [[UIActivityViewController alloc] initWithActivityItems:@[url] applicationActivities:nil];
        if (UIDevice.currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad)
        {
            vc.popoverPresentationController.sourceView = top.view;
            vc.popoverPresentationController.sourceRect =
                CGRectMake(top.view.bounds.size.width / 2.0, top.view.bounds.size.height / 2.0, 1.0, 1.0);
            vc.popoverPresentationController.permittedArrowDirections = 0;
        }
        [top presentViewController:vc animated:YES completion:nil];
    });
    return 1;
}
