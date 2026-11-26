/* sokol_filepicker_apple.m -- the system's file pickers for Sokol.NET (iOS + macOS).
   iOS:   UIDocumentPickerViewController. Open asks for an existing document in place (no copy), so the app works on
          the user's own file wherever its provider keeps it (On My iPad, iCloud Drive, Google Drive, ...). Save writes
          the bytes to a temporary file and exports it: the picker places a copy where the user chooses and answers
          with that copy's URL.
   macOS: NSOpenPanel / NSSavePanel as a sheet on the app's window (the frame loop keeps running).
   The picked URLs are security scoped: access is started once per URL and kept while the app runs. Reads and writes
   go through NSFileCoordinator, so a document that lives in a cloud provider is fetched first and a provider that
   syncs it sees the change. Bookmarks keep access across launches. */
#include "sokol_filepicker.h"
#import <Foundation/Foundation.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>
#include <TargetConditionals.h>
#include <stdlib.h>
#include <string.h>
#if TARGET_OS_IPHONE
#import <UIKit/UIKit.h>
#else
#import <AppKit/AppKit.h>
#endif

static int g_state = SOKOLFILEPICKER_IDLE;   /* touched on the main thread only */
static char* g_handle;
static char* g_name;
static char* g_token;
static char* g_resolved;
static NSData* g_loaded;
static NSMutableDictionary<NSString*, NSURL*>* g_urls;   /* handle -> URL whose security scope is started */

static void set_str(char** slot, NSString* s)
{
    free(*slot);
    *slot = s ? strdup(s.UTF8String) : NULL;
}

/* The URL for a handle; a picked or resolved one starts its security scope the first time it is seen. */
static NSURL* url_for(NSString* handle, NSURL* known)
{
    @synchronized ([NSFileCoordinator class]) {
        if (!g_urls) g_urls = [NSMutableDictionary dictionary];
        NSURL* u = g_urls[handle];
        if (u) return u;
        u = known ? known : [NSURL URLWithString:handle];
        if (!u) return nil;
        [u startAccessingSecurityScopedResource];   /* false for URLs that need no scope: fine */
        g_urls[handle] = u;
        return u;
    }
}

static NSURL* url_from_c(const char* handle)
{
    if (!handle || !*handle) return nil;
    NSString* s = [NSString stringWithUTF8String:handle];
    return s ? url_for(s, nil) : nil;
}

static void finish(int state, NSURL* url)
{
    if (state == SOKOLFILEPICKER_PICKED && url) {
        NSString* h = url.absoluteString;
        url_for(h, url);
        set_str(&g_handle, h);
        set_str(&g_name, url.lastPathComponent);
    }
    g_state = state;
}

static NSArray<UTType*>* types_for(const char* extensions, bool* any) API_AVAILABLE(ios(14.0), macos(11.0))
{
    NSMutableArray<UTType*>* types = [NSMutableArray array];
    *any = true;
    if (extensions && *extensions) {
        for (NSString* e in [[NSString stringWithUTF8String:extensions] componentsSeparatedByString:@";"]) {
            if (e.length == 0) continue;
            UTType* t = [UTType typeWithFilenameExtension:e];
            if (!t) continue;
            [types addObject:t];
            if (!t.dynamic) *any = false;   /* a declared type filters; a made-up extension's dynamic one may not */
            else { *any = true; break; }
        }
    }
    if (types.count == 0) *any = true;
    return types;
}

static NSURL* temp_file(NSString* name, const unsigned char* data, int64_t len)
{
    NSString* dir = [NSTemporaryDirectory() stringByAppendingPathComponent:[NSUUID UUID].UUIDString];
    [[NSFileManager defaultManager] createDirectoryAtPath:dir withIntermediateDirectories:YES attributes:nil error:nil];
    NSURL* u = [NSURL fileURLWithPath:[dir stringByAppendingPathComponent:name.length ? name : @"untitled"]];
    NSData* d = [NSData dataWithBytes:data length:(NSUInteger)(len > 0 ? len : 0)];
    return [d writeToURL:u atomically:NO] ? u : nil;
}

#if TARGET_OS_IPHONE

@interface SokolFilePickerDelegate : NSObject <UIDocumentPickerDelegate>
@property (nonatomic, strong) NSURL* tempFile;   /* save: the exported temporary copy, removed afterwards */
@end

@implementation SokolFilePickerDelegate
- (void)cleanUp
{
    if (self.tempFile) {
        [[NSFileManager defaultManager] removeItemAtURL:self.tempFile.URLByDeletingLastPathComponent error:nil];
        self.tempFile = nil;
    }
}
- (void)documentPicker:(UIDocumentPickerViewController*)controller didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls
{
    [self cleanUp];
    finish(urls.count ? SOKOLFILEPICKER_PICKED : SOKOLFILEPICKER_CANCELLED, urls.firstObject);
}
- (void)documentPickerWasCancelled:(UIDocumentPickerViewController*)controller
{
    [self cleanUp];
    finish(SOKOLFILEPICKER_CANCELLED, nil);
}
@end

static SokolFilePickerDelegate* g_delegate;

static UIViewController* top_controller(void)
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

static bool show_picker(int mode, const char* extensions, NSString* name, const unsigned char* data, int64_t len)
{
    UIViewController* root = top_controller();
    if (!root) return false;
    if (!g_delegate) g_delegate = [SokolFilePickerDelegate new];
    UIDocumentPickerViewController* picker;
    if (mode == 1) {
        NSURL* tmp = temp_file(name, data, len);
        if (!tmp) return false;
        g_delegate.tempFile = tmp;
        picker = [[UIDocumentPickerViewController alloc] initForExportingURLs:@[tmp] asCopy:YES];
    } else {
        bool any;
        NSArray<UTType*>* types = types_for(extensions, &any);
        picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:any ? @[UTTypeItem] : types asCopy:NO];
        picker.allowsMultipleSelection = NO;
    }
    picker.delegate = g_delegate;
    picker.modalPresentationStyle = UIModalPresentationFormSheet;
    [root presentViewController:picker animated:YES completion:nil];
    return true;
}

#else   /* macOS */

static bool show_picker(int mode, const char* extensions, NSString* name, const unsigned char* data, int64_t len)
{
    NSWindow* win = [NSApp keyWindow];
    if (!win) win = [NSApp mainWindow];
    bool any = true;
    NSArray<UTType*>* types = @[];
    if (@available(macOS 11.0, *)) types = types_for(extensions, &any);
    if (mode == 1) {
        NSData* bytes = [NSData dataWithBytes:data length:(NSUInteger)(len > 0 ? len : 0)];
        NSSavePanel* panel = [NSSavePanel savePanel];
        if (name.length) panel.nameFieldStringValue = name;
        if (types.count) { if (@available(macOS 11.0, *)) panel.allowedContentTypes = @[types.firstObject]; }
        void (^done)(NSModalResponse) = ^(NSModalResponse r) {
            if (r != NSModalResponseOK || !panel.URL) { finish(SOKOLFILEPICKER_CANCELLED, nil); return; }
            NSURL* u = panel.URL;
            url_for(u.absoluteString, u);
            bool ok = sokolfilepicker_write(u.absoluteString.UTF8String, bytes.bytes, (int64_t)bytes.length);
            finish(ok ? SOKOLFILEPICKER_PICKED : SOKOLFILEPICKER_FAILED, u);
        };
        if (win) [panel beginSheetModalForWindow:win completionHandler:done];
        else [panel beginWithCompletionHandler:done];
    } else {
        NSOpenPanel* panel = [NSOpenPanel openPanel];
        panel.canChooseFiles = YES;
        panel.canChooseDirectories = NO;
        panel.allowsMultipleSelection = NO;
        if (!any) { if (@available(macOS 11.0, *)) panel.allowedContentTypes = types; }
        void (^done)(NSModalResponse) = ^(NSModalResponse r) {
            if (r != NSModalResponseOK || !panel.URL) finish(SOKOLFILEPICKER_CANCELLED, nil);
            else finish(SOKOLFILEPICKER_PICKED, panel.URL);
        };
        if (win) [panel beginSheetModalForWindow:win completionHandler:done];
        else [panel beginWithCompletionHandler:done];
    }
    return true;
}

#endif

bool sokolfilepicker_start(int mode, const char* extensions, const char* name, const unsigned char* data, int64_t len)
{
    if (![NSThread isMainThread]) return false;
    if (g_state == SOKOLFILEPICKER_RUNNING) return false;
    NSString* n = name ? [NSString stringWithUTF8String:name] : nil;
    g_state = SOKOLFILEPICKER_RUNNING;
    bool ok = show_picker(mode, extensions, n, data, len);
    if (!ok) g_state = SOKOLFILEPICKER_IDLE;
    return ok;
}

int sokolfilepicker_poll(void)
{
    int s = g_state;
    if (s != SOKOLFILEPICKER_RUNNING) g_state = SOKOLFILEPICKER_IDLE;
    return s;
}

const char* sokolfilepicker_result_handle(void) { return g_handle; }
const char* sokolfilepicker_result_name(void) { return g_name; }

int64_t sokolfilepicker_load(const char* handle)
{
    @autoreleasepool {
        g_loaded = nil;
        NSURL* u = url_from_c(handle);
        if (!u) return -1;
        __block NSData* d = nil;
        NSError* err = nil;
        [[[NSFileCoordinator alloc] initWithFilePresenter:nil] coordinateReadingItemAtURL:u options:0 error:&err
            byAccessor:^(NSURL* at) { d = [NSData dataWithContentsOfURL:at options:0 error:nil]; }];
        if (!d) return -1;
        g_loaded = d;
        return (int64_t)d.length;
    }
}

void sokolfilepicker_take(unsigned char* dst)
{
    if (g_loaded && dst && g_loaded.length) memcpy(dst, g_loaded.bytes, g_loaded.length);
    g_loaded = nil;
}

bool sokolfilepicker_write(const char* handle, const unsigned char* data, int64_t len)
{
    @autoreleasepool {
        NSURL* u = url_from_c(handle);
        if (!u || len < 0) return false;
        NSData* d = [NSData dataWithBytesNoCopy:(void*)data length:(NSUInteger)len freeWhenDone:NO];
        __block BOOL ok = NO;
        NSError* err = nil;
        /* Not atomic: an atomic write makes a temporary file beside the document, and the app may only hold access
           to the document itself (a provider's folder). */
        [[[NSFileCoordinator alloc] initWithFilePresenter:nil] coordinateWritingItemAtURL:u
            options:NSFileCoordinatorWritingForReplacing error:&err
            byAccessor:^(NSURL* at) { ok = [d writeToURL:at options:0 error:nil]; }];
        return ok;
    }
}

static int64_t stat_field(const char* handle, bool size)
{
    @autoreleasepool {
        NSURL* u = url_from_c(handle);
        if (!u) return -1;
        [u removeAllCachedResourceValues];
        NSNumber* n = nil;
        NSDate* m = nil;
        if (size) {
            if (![u getResourceValue:&n forKey:NSURLFileSizeKey error:nil] || !n) return -1;
            return n.longLongValue;
        }
        if (![u getResourceValue:&m forKey:NSURLContentModificationDateKey error:nil] || !m) return -1;
        return (int64_t)(m.timeIntervalSince1970 * 1000.0);
    }
}

int64_t sokolfilepicker_size(const char* handle) { return stat_field(handle, true); }
int64_t sokolfilepicker_modified(const char* handle) { return stat_field(handle, false); }

const char* sokolfilepicker_bookmark(const char* handle)
{
    @autoreleasepool {
        NSURL* u = url_from_c(handle);
        NSData* b = nil;
        if (u) {
#if TARGET_OS_IPHONE
            b = [u bookmarkDataWithOptions:0 includingResourceValuesForKeys:nil relativeToURL:nil error:nil];
#else
            b = [u bookmarkDataWithOptions:NSURLBookmarkCreationWithSecurityScope includingResourceValuesForKeys:nil relativeToURL:nil error:nil];
            if (!b) b = [u bookmarkDataWithOptions:0 includingResourceValuesForKeys:nil relativeToURL:nil error:nil];
#endif
        }
        set_str(&g_token, b ? [b base64EncodedStringWithOptions:0] : nil);
        return g_token;
    }
}

const char* sokolfilepicker_resolve(const char* token)
{
    @autoreleasepool {
        set_str(&g_resolved, nil);
        if (!token || !*token) return NULL;
        NSData* b = [[NSData alloc] initWithBase64EncodedString:[NSString stringWithUTF8String:token] options:0];
        if (!b) return NULL;
        BOOL stale = NO;
        NSURL* u = nil;
#if TARGET_OS_IPHONE
        u = [NSURL URLByResolvingBookmarkData:b options:0 relativeToURL:nil bookmarkDataIsStale:&stale error:nil];
#else
        u = [NSURL URLByResolvingBookmarkData:b options:NSURLBookmarkResolutionWithSecurityScope relativeToURL:nil bookmarkDataIsStale:&stale error:nil];
        if (!u) u = [NSURL URLByResolvingBookmarkData:b options:0 relativeToURL:nil bookmarkDataIsStale:&stale error:nil];
#endif
        if (!u) return NULL;
        NSString* h = u.absoluteString;
        url_for(h, u);
        if (![u checkResourceIsReachableAndReturnError:nil]) return NULL;
        set_str(&g_resolved, h);
        return g_resolved;
    }
}
