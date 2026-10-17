#ifndef SOKOL_SAFEAREA_INCLUDED
/*
    sokol_safearea.h -- query the display's safe-area insets (notch / Dynamic Island /
                        punch-hole camera / home indicator / curved & waterfall edges)

    Project URL: https://github.com/elix22/Sokol.NET

    Do this:
        #define SOKOL_IMPL or #define SOKOL_SAFEAREA_IMPL
    before you include this file in *one* C or ObjC file to create the
    implementation.

    sokol_app.h must be included before sokol_safearea.h.

    WHY THIS EXISTS
    ===============
    sokol_app.h hands you a framebuffer that covers the WHOLE display, including
    the strip behind a notch/Dynamic Island and the rounded corners. Anything an
    app draws there is clipped by the hardware -- on a modern phone in landscape
    that is text and buttons along the leading edge, silently cut off.

    Both mobile OSes already compute the usable rect per device, per orientation
    and per fold state, so there is nothing to derive and no device table to
    maintain (a hardcoded one is wrong the moment the user rotates or unfolds).
    This module wraps the two platform queries behind one function.

        iOS      UIWindow.safeAreaInsets -- notch/Dynamic Island, home indicator,
                 AND the rounded-corner allowance.
        Android  WindowInsets.getDisplayCutout() safe insets (API 28+), widened by
                 DisplayCutout.getWaterfallInsets() (API 31+) for curved screens and
                 by WindowInsets.getRoundedCorner() (API 31+) on the SHORT sides
                 (left/right in landscape) -- the rounded-corner allowance iOS has.
        others   zero (desktop and web have no cutouts).

    HOW TO USE
    ==========
        float ltrb[4];
        ssafe_get(ltrb);            // left, top, right, bottom
        // inset your UI/content rect by ltrb; the background may still bleed
        // to the full framebuffer.

    UNITS
    =====
    PHYSICAL PIXELS, i.e. the same space as sapp_width()/sapp_height(), so a
    caller working in logical units divides by the same sapp_dpi_scale() it uses
    for the framebuffer size. Zeros are returned on every unsupported platform
    and on any query failure, so a caller never needs a platform #if.

    CALL ORDER
    ==========
    Any time after sapp has a window. The values change on rotation and on fold,
    so re-read them when the framebuffer size changes rather than caching once.
    Cheap on iOS (a property read); on Android it is a handful of JNI calls, so
    prefer re-reading on resize over every frame.

    ANDROID CAVEAT
    ==============
    View.getRootWindowInsets() is documented for the UI thread, and sokol runs the
    app on its own thread. The query is therefore best-effort: it attaches to the
    JVM if needed, clears any pending exception, and reports zeros if anything
    fails -- never a crash. Note also that an app which does not opt into
    LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES/ALWAYS is letterboxed away from the
    cutout on long edges by the system, in which case zero is the correct answer.

    zlib/libpng license -- same terms as the sokol headers.
*/
#define SOKOL_SAFEAREA_INCLUDED (1)

#if defined(SOKOL_IMPL) && !defined(SOKOL_SAFEAREA_IMPL)
#define SOKOL_SAFEAREA_IMPL
#endif

#include <stdint.h>
#include <stdbool.h>

#if !defined(SOKOL_APP_INCLUDED)
#error "Please include sokol_app.h before sokol_safearea.h"
#endif

#if defined(SOKOL_API_DECL) && !defined(SOKOL_SAFEAREA_API_DECL)
#define SOKOL_SAFEAREA_API_DECL SOKOL_API_DECL
#endif
#ifndef SOKOL_SAFEAREA_API_DECL
#if defined(_WIN32) && defined(SOKOL_DLL) && defined(SOKOL_SAFEAREA_IMPL)
#define SOKOL_SAFEAREA_API_DECL __declspec(dllexport)
#elif defined(_WIN32) && defined(SOKOL_DLL)
#define SOKOL_SAFEAREA_API_DECL __declspec(dllimport)
#else
#define SOKOL_SAFEAREA_API_DECL extern
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* true if this platform can report safe-area insets at all (iOS/Android) */
SOKOL_SAFEAREA_API_DECL bool ssafe_supported(void);

/* Write the safe-area insets, in PHYSICAL PIXELS, into out_ltrb as
   { left, top, right, bottom }. Always writes 4 floats; zeros where the
   platform has no cutout or the query failed. Does nothing if out_ltrb is null. */
SOKOL_SAFEAREA_API_DECL void ssafe_get(float* out_ltrb);

/* iOS only (no-op elsewhere): defer the system's bottom-edge swipe gesture, so the
   FIRST upward swipe from the home-indicator strip goes to the app instead of
   sending it to the background -- a second swipe still leaves, so the user is never
   trapped. Without this a drag started near the bottom edge is stolen by iOS, which
   matters for drag-driven scenes (jigsaw pieces, aiming, dragging a level).

   Apple's guidance is to defer only where the app genuinely needs the edge, so call
   it with true when a full-screen interactive scene appears and false when it goes
   away, rather than leaving it on for menus.

   ⛔ sokol_app owns the root UIViewController and creates it as a PLAIN
   UIViewController, so this cannot be a subclass and must not be a category (that
   would change every view controller in the process, including system sheets).
   Instead the one instance is isa-swizzled into a private subclass created on first
   use -- per-instance, allocated once, and nothing else in the app is affected. */
SOKOL_SAFEAREA_API_DECL void ssafe_defer_bottom_gestures(bool enable);

#ifdef __cplusplus
} /* extern "C" */
#endif
#endif /* SOKOL_SAFEAREA_INCLUDED */

/*=== IMPLEMENTATION =========================================================*/
#ifdef SOKOL_SAFEAREA_IMPL
#define SOKOL_SAFEAREA_IMPL_INCLUDED (1)

/*--- iOS -------------------------------------------------------------------*/
#if defined(__APPLE__)
#include <TargetConditionals.h>
#endif

#if defined(__APPLE__) && defined(TARGET_OS_IOS) && TARGET_OS_IOS

#import <UIKit/UIKit.h>
#include <objc/runtime.h>
#include <objc/message.h>

/* Edge mask reported by the swizzled getter; read live so enable/disable both work
   without ever un-swizzling. */
static UIRectEdge _ssafe_defer_edges = UIRectEdgeNone;

static NSUInteger _ssafe_deferred_edges_imp(id self, SEL _cmd) {
    (void)self; (void)_cmd;
    return (NSUInteger)_ssafe_defer_edges;
}

/* Give the ROOT view controller (and only it) an override of
   -preferredScreenEdgesDeferringSystemGestures. Runs once; safe to call again. */
static void _ssafe_install_gesture_override(UIViewController* vc) {
    static Class patched = Nil;
    if (Nil != patched) {
        return;
    }
    Class base = object_getClass(vc);
    Class sub  = objc_allocateClassPair(base, "SokolSafeAreaViewController", 0);
    if (Nil == sub) {
        /* Name already taken (double init): fall back to the registered one. */
        sub = objc_getClass("SokolSafeAreaViewController");
        if (Nil == sub) {
            return;
        }
    } else {
        class_addMethod(sub, @selector(preferredScreenEdgesDeferringSystemGestures),
                        (IMP)_ssafe_deferred_edges_imp, "Q@:");
        objc_registerClassPair(sub);
    }
    object_setClass(vc, sub);
    patched = sub;
}

static void _ssafe_get(float* o) {
    UIWindow* win = (__bridge UIWindow*) sapp_ios_get_window();
    if (nil == win) {
        return;
    }
    /* safeAreaInsets is in POINTS and already accounts for the rounded corners,
       so a notchless-but-round phone still reports a non-zero landscape inset. */
    UIEdgeInsets ins = win.safeAreaInsets;
#if defined(__IPHONE_26_0)
    /* iPadOS 26+ windowed apps draw the window controls in the top-left corner, inside
       the plain safe area. The vertically corner-adapted safe area moves the top edge
       below them. iPad only, and only its top: on an iPhone it also counts the rounded
       display corners (14 pt more on a 15 Pro Max), where there are no window controls. */
    if (@available(iOS 26.0, *)) {
        if (UIDevice.currentDevice.userInterfaceIdiom == UIUserInterfaceIdiomPad) {
            const UIEdgeInsets adapted = [win edgeInsetsForLayoutRegion:
                [UIViewLayoutRegion safeAreaLayoutRegionWithCornerAdaptation:UIViewLayoutRegionAdaptivityAxisVertical]];
            if (adapted.top > ins.top) {
                ins.top = adapted.top;
            }
        }
    }
#endif
    const float s = sapp_dpi_scale();
    o[0] = (float)ins.left   * s;
    o[1] = (float)ins.top    * s;
    o[2] = (float)ins.right  * s;
    o[3] = (float)ins.bottom * s;
}

static void _ssafe_defer(bool enable) {
    /* UIKit is main-thread only, and sokol runs the app on its own thread. */
    dispatch_async(dispatch_get_main_queue(), ^{
        UIWindow* win = (__bridge UIWindow*) sapp_ios_get_window();
        UIViewController* vc = win.rootViewController;
        if (nil == vc) {
            return;
        }
        _ssafe_defer_edges = enable ? UIRectEdgeBottom : UIRectEdgeNone;
        _ssafe_install_gesture_override(vc);
        [vc setNeedsUpdateOfScreenEdgesDeferringSystemGestures];
    });
}

#define _SSAFE_SUPPORTED (1)

/*--- Android ---------------------------------------------------------------*/
#elif defined(__ANDROID__)

#include <jni.h>
#include <android/native_activity.h>

static void _ssafe_android_cutout(JNIEnv* env, jclass cls_ins, jobject insets, float* o);
static void _ssafe_android_corners(JNIEnv* env, jclass cls_ins, jobject insets, float w, float h, float* o);

/* Best-effort: any failure leaves the caller's zeros in place. */
static void _ssafe_get(float* o) {
    const ANativeActivity* act = (const ANativeActivity*) sapp_android_get_native_activity();
    if ((NULL == act) || (NULL == act->vm) || (NULL == act->clazz)) {
        return;
    }
    JavaVM* vm = act->vm;
    JNIEnv* env = NULL;
    bool attached = false;
    if ((*vm)->GetEnv(vm, (void**)&env, JNI_VERSION_1_6) != JNI_OK) {
        /* sokol's app thread is normally attached already; attach if it is not.
           Deliberately NOT detached again: the thread lives for the whole app,
           and detach/attach churn per query is both slow and error-prone. */
        if ((*vm)->AttachCurrentThread(vm, &env, NULL) != JNI_OK) {
            return;
        }
        attached = true;
    }
    (void)attached;
    if (NULL == env) {
        return;
    }
    /* sokol's app thread never returns to Java, so a local reference is only freed
       when this frame is popped -- without it every query pins ~11 references (and
       the Java objects behind them) until the table overflows and ART aborts. */
    if ((*env)->PushLocalFrame(env, 32) != 0) {
        (*env)->ExceptionClear(env);
        return;
    }

    jobject activity = act->clazz;
    jclass  cls_act  = (*env)->GetObjectClass(env, activity);
    jmethodID m_getWindow = (*env)->GetMethodID(env, cls_act, "getWindow", "()Landroid/view/Window;");
    if (NULL == m_getWindow) { goto cleanup; }
    jobject window = (*env)->CallObjectMethod(env, activity, m_getWindow);
    if (NULL == window) { goto cleanup; }

    jclass cls_win = (*env)->GetObjectClass(env, window);
    jmethodID m_getDecor = (*env)->GetMethodID(env, cls_win, "getDecorView", "()Landroid/view/View;");
    if (NULL == m_getDecor) { goto cleanup; }
    jobject decor = (*env)->CallObjectMethod(env, window, m_getDecor);
    if (NULL == decor) { goto cleanup; }

    jclass cls_view = (*env)->GetObjectClass(env, decor);
    /* getRootWindowInsets(): API 23+ */
    jmethodID m_getInsets = (*env)->GetMethodID(env, cls_view, "getRootWindowInsets", "()Landroid/view/WindowInsets;");
    if (NULL == m_getInsets) { goto cleanup; }
    jobject insets = (*env)->CallObjectMethod(env, decor, m_getInsets);
    if (NULL == insets) { goto cleanup; }

    jclass cls_ins = (*env)->GetObjectClass(env, insets);
    _ssafe_android_cutout(env, cls_ins, insets, o);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); }

    /* The insets are in WINDOW px; the documented unit is sapp framebuffer px, and the two differ when sokol
       renders below the window size (high_dpi off: framebuffer = window / 2). Size unknown (not laid out yet):
       the cutout stays in window px and the corners are skipped. */
    jmethodID m_w = (*env)->GetMethodID(env, cls_view, "getWidth",  "()I");
    jmethodID m_h = (*env)->GetMethodID(env, cls_view, "getHeight", "()I");
    if ((NULL == m_w) || (NULL == m_h)) { goto cleanup; }
    const float w = (float)(*env)->CallIntMethod(env, decor, m_w);
    const float h = (float)(*env)->CallIntMethod(env, decor, m_h);
    if ((w <= 0.0f) || (h <= 0.0f)) { goto cleanup; }
    o[0] *= sapp_widthf() / w;  o[2] *= sapp_widthf() / w;
    o[1] *= sapp_heightf() / h; o[3] *= sapp_heightf() / h;
    _ssafe_android_corners(env, cls_ins, insets, w, h, o);

cleanup:
    /* A missing method on an older API level raises NoSuchMethodError; that is an
       expected outcome here, not a failure worth propagating into the app. */
    if ((*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
    }
    (*env)->PopLocalFrame(env, NULL);
}

/* Cutout safe insets (API 28+), widened by the waterfall insets (API 31+), in window px. No cutout = no change. */
static void _ssafe_android_cutout(JNIEnv* env, jclass cls_ins, jobject insets, float* o) {
    /* getDisplayCutout(): API 28+ -- absent (or null) means "no cutout", i.e. zeros. */
    jmethodID m_getCutout = (*env)->GetMethodID(env, cls_ins, "getDisplayCutout", "()Landroid/view/DisplayCutout;");
    if (NULL == m_getCutout) { return; }
    jobject cutout = (*env)->CallObjectMethod(env, insets, m_getCutout);
    if (NULL == cutout) { return; }

    jclass cls_cut = (*env)->GetObjectClass(env, cutout);
    jmethodID m_l = (*env)->GetMethodID(env, cls_cut, "getSafeInsetLeft",   "()I");
    jmethodID m_t = (*env)->GetMethodID(env, cls_cut, "getSafeInsetTop",    "()I");
    jmethodID m_r = (*env)->GetMethodID(env, cls_cut, "getSafeInsetRight",  "()I");
    jmethodID m_b = (*env)->GetMethodID(env, cls_cut, "getSafeInsetBottom", "()I");
    if (m_l && m_t && m_r && m_b) {
        o[0] = (float)(*env)->CallIntMethod(env, cutout, m_l);
        o[1] = (float)(*env)->CallIntMethod(env, cutout, m_t);
        o[2] = (float)(*env)->CallIntMethod(env, cutout, m_r);
        o[3] = (float)(*env)->CallIntMethod(env, cutout, m_b);
    }

    /* Curved / waterfall screens (API 31+): widen by the waterfall insets, which
       are reported separately from the cutout. android.graphics.Insets exposes
       left/top/right/bottom as public int fields. */
    jmethodID m_wf = (*env)->GetMethodID(env, cls_cut, "getWaterfallInsets", "()Landroid/graphics/Insets;");
    if (NULL != m_wf) {
        jobject wf = (*env)->CallObjectMethod(env, cutout, m_wf);
        if (NULL != wf) {
            jclass cls_wf = (*env)->GetObjectClass(env, wf);
            const char* names[4] = { "left", "top", "right", "bottom" };
            for (int i = 0; i < 4; i++) {
                jfieldID f = (*env)->GetFieldID(env, cls_wf, names[i], "I");
                if (NULL == f) { continue; }
                const float v = (float)(*env)->GetIntField(env, wf, f);
                if (v > o[i]) { o[i] = v; }
            }
        }
    }
}

/* Rounded display corners (API 31+), which the cutout insets above never include. An edge-to-edge window
   (targetSdk 35+ on Android 15+) covers the corners, and a button placed in one is clipped by the arc (Pixel 10a
   landscape, 2026-10-12). Cleared on the SHORT sides only -- left/right in landscape, top/bottom in portrait --
   the way iOS's landscape safe area does, so the long axis (a landscape board's height) is never given up. A
   corner's center is in WINDOW coordinates, so a window that does not reach the corner (letterboxed away from a
   cutout) gets only what the arc really covers, and a corner outside the window is not reported at all. */
static void _ssafe_android_corners(JNIEnv* env, jclass cls_ins, jobject insets, float w, float h, float* o) {
    jmethodID m_rc = (*env)->GetMethodID(env, cls_ins, "getRoundedCorner", "(I)Landroid/view/RoundedCorner;");
    if (NULL == m_rc) { return; }   /* < API 31: NoSuchMethodError, cleared by the caller */
    const bool landscape = (w >= h);
    /* RoundedCorner.POSITION_TOP_LEFT 0, TOP_RIGHT 1, BOTTOM_RIGHT 2, BOTTOM_LEFT 3 */
    for (int pos = 0; pos < 4; pos++) {
        jobject rc = (*env)->CallObjectMethod(env, insets, m_rc, pos);
        if ((*env)->ExceptionCheck(env)) { return; }
        if (NULL == rc) { continue; }
        jclass cls_rc = (*env)->GetObjectClass(env, rc);
        jmethodID m_rad = (*env)->GetMethodID(env, cls_rc, "getRadius", "()I");
        jmethodID m_ctr = (*env)->GetMethodID(env, cls_rc, "getCenter", "()Landroid/graphics/Point;");
        if ((NULL == m_rad) || (NULL == m_ctr)) { return; }
        const float r = (float)(*env)->CallIntMethod(env, rc, m_rad);
        jobject pt = (*env)->CallObjectMethod(env, rc, m_ctr);
        if ((r > 0.0f) && (NULL != pt)) {
            jclass cls_pt = (*env)->GetObjectClass(env, pt);
            jfieldID f_x = (*env)->GetFieldID(env, cls_pt, "x", "I");
            jfieldID f_y = (*env)->GetFieldID(env, cls_pt, "y", "I");
            if ((NULL == f_x) || (NULL == f_y)) { return; }
            const float cx = (float)(*env)->GetIntField(env, pt, f_x);
            const float cy = (float)(*env)->GetIntField(env, pt, f_y);
            const bool on_left = (pos == 0) || (pos == 3);
            const bool on_top  = (pos == 0) || (pos == 1);
            const int  side    = landscape ? (on_left ? 0 : 2) : (on_top ? 1 : 3);
            float need = landscape ? (on_left ? cx : (w - cx)) : (on_top ? cy : (h - cy));
            if (need > r) { need = r; }   /* the arc never reaches further in than its radius */
            /* Window px -> sapp framebuffer px, as for the cutout above. */
            need *= landscape ? (sapp_widthf() / w) : (sapp_heightf() / h);
            if (need > o[side]) { o[side] = need; }
            (*env)->DeleteLocalRef(env, cls_pt);
        }
        if (NULL != pt) { (*env)->DeleteLocalRef(env, pt); }
        (*env)->DeleteLocalRef(env, cls_rc);
        (*env)->DeleteLocalRef(env, rc);
    }
}

static void _ssafe_defer(bool enable) {
    /* Android's back/home gestures are not deferrable per-edge the way iOS's are
       (an app can only exclude small rects via setSystemGestureExclusionRects, which
       the framebuffer-owning native activity does not expose here). */
    (void)enable;
}

#define _SSAFE_SUPPORTED (1)

/*--- desktop / web: no cutouts --------------------------------------------*/
#else

static void _ssafe_get(float* o) {
    (void)o;
}

static void _ssafe_defer(bool enable) {
    (void)enable;   /* only iOS reserves a screen edge for a system gesture */
}

#define _SSAFE_SUPPORTED (0)

#endif

SOKOL_API_IMPL bool ssafe_supported(void) {
    return _SSAFE_SUPPORTED ? true : false;
}

SOKOL_API_IMPL void ssafe_defer_bottom_gestures(bool enable) {
    _ssafe_defer(enable);
}

SOKOL_API_IMPL void ssafe_get(float* out_ltrb) {
    if (NULL == out_ltrb) {
        return;
    }
    out_ltrb[0] = 0.0f; out_ltrb[1] = 0.0f; out_ltrb[2] = 0.0f; out_ltrb[3] = 0.0f;
    _ssafe_get(out_ltrb);
}

#endif /* SOKOL_SAFEAREA_IMPL */
