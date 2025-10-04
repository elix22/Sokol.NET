#include "fileutil.h"
#include <stdio.h>
#import <Foundation/Foundation.h>
#include <TargetConditionals.h>

const char* fileutil_get_path(const char* filename, char* buf, size_t buf_size) {
#if TARGET_OS_IOS || TARGET_OS_TV || TARGET_OS_WATCH
    // iOS/tvOS/watchOS always run in app bundles - use resource path
    NSString* ns_str = [NSBundle mainBundle].resourcePath;
    snprintf(buf, buf_size, "%s/%s", [ns_str UTF8String], filename);
#else
    // macOS: Check if we're in a proper .app bundle by looking for .app in the bundle path
    NSString* bundlePath = [[NSBundle mainBundle] bundlePath];
    
    if (bundlePath && [bundlePath hasSuffix:@".app"]) {
        // We're in a proper .app bundle - use the resource path
        NSString* resourcePath = [NSBundle mainBundle].resourcePath;
        snprintf(buf, buf_size, "%s/%s", [resourcePath UTF8String], filename);
    } else {
        // Not in a .app bundle (debug/standalone) - use filename as-is (relative to working directory)
        snprintf(buf, buf_size, "%s", filename);
    }
#endif
    
    // All NSStrings will be autoreleased
    return buf;
}

