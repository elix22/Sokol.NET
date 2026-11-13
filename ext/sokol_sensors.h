#ifndef SOKOL_SENSORS_INCLUDED
/*
    sokol_sensors.h -- the device's gravity vector, for tilt controls

    Project URL: https://github.com/elix22/Sokol.NET

    Do this:
        #define SOKOL_IMPL or #define SOKOL_SENSORS_IMPL
    before you include this file in *one* C or ObjC file to create the
    implementation.

    sokol_app.h must be included before sokol_sensors.h.

    WHY THIS EXISTS
    ===============
    sokol_app.h gives no motion sensor on any platform. A game that steers by tilting
    the phone needs one number: which way gravity pulls, as the screen is turned now.
    This module reads the operating system's own gravity vector (its fusion, not ours)
    and turns it into the screen's frame - nothing more: no filtering, no averaging.

        Android  the NDK sensor queue (ASensorManager / ASensorEventQueue) on the app
                 thread's looper: the GRAVITY sensor, the accelerometer when there is
                 none; turned by the display's rotation (one JNI call when started).
        iOS      CoreMotion: CMMotionManager.deviceMotion.gravity, turned by the
                 interface orientation. No permission prompt is needed for motion.
        Web      the 'devicemotion' event (accelerationIncludingGravity), turned by
                 screen.orientation.angle. iOS Safari grants it only after the player
                 allows it: ssens_start() asks on the page's next tap.
        others   none: ssens_gravity() returns false.

    HOW TO USE
    ==========
        ssens_start();                      // once (the app thread on Android)
        float g[3];
        if (ssens_gravity(g)) {             // 1: x right, y up, z out of the screen,
            float steer = g[0] / 9.81f;     // metres a second each second: lying
        }                                   // face up, about (0, 0, -9.81)
        ssens_stop();                       // when no longer needed (battery)

    zlib/libpng license -- same terms as the sokol headers.
*/
#define SOKOL_SENSORS_INCLUDED (1)

#if defined(SOKOL_IMPL) && !defined(SOKOL_SENSORS_IMPL)
#define SOKOL_SENSORS_IMPL
#endif

#include <stdint.h>
#include <stdbool.h>

#if !defined(SOKOL_APP_INCLUDED)
#error "Please include sokol_app.h before sokol_sensors.h"
#endif

#if defined(SOKOL_API_DECL) && !defined(SOKOL_SENSORS_API_DECL)
#define SOKOL_SENSORS_API_DECL SOKOL_API_DECL
#endif
#ifndef SOKOL_SENSORS_API_DECL
#if defined(_WIN32) && defined(SOKOL_DLL) && defined(SOKOL_SENSORS_IMPL)
#define SOKOL_SENSORS_API_DECL __declspec(dllexport)
#elif defined(_WIN32) && defined(SOKOL_DLL)
#define SOKOL_SENSORS_API_DECL __declspec(dllimport)
#else
#define SOKOL_SENSORS_API_DECL extern
#endif
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Starts reading gravity; true when this device can (false on desktop, or with no sensor). */
SOKOL_SENSORS_API_DECL bool ssens_start(void);
/* Stops reading it (the sensor off: battery). */
SOKOL_SENSORS_API_DECL void ssens_stop(void);
/* The latest gravity vector in the screen's frame (x right, y up, z out of the screen), m/s^2, into out_xyz[3];
   1 when there is one, 0 (and zeros) when none has arrived or the device has none. (An int, not a bool: the C#
   binding of a bool-returning function that writes through a pointer has no web form.) */
SOKOL_SENSORS_API_DECL int ssens_gravity(float* out_xyz);

#ifdef __cplusplus
} /* extern "C" */
#endif
#endif /* SOKOL_SENSORS_INCLUDED */

/*=== IMPLEMENTATION =========================================================*/
#ifdef SOKOL_SENSORS_IMPL
#define SOKOL_SENSORS_IMPL_INCLUDED (1)

#if defined(__APPLE__)
#include <TargetConditionals.h>
#endif

/* Device frame -> screen frame, by how the screen is turned (0, 90, 180, 270 degrees). */
static void _ssens_turn(const float* d, int rotation, float* o) {
    switch (((rotation % 360) + 360) % 360) {
        case 90:  o[0] = -d[1]; o[1] =  d[0]; break;
        case 180: o[0] = -d[0]; o[1] = -d[1]; break;
        case 270: o[0] =  d[1]; o[1] = -d[0]; break;
        default:  o[0] =  d[0]; o[1] =  d[1]; break;
    }
    o[2] = d[2];
}

/*--- iOS -------------------------------------------------------------------*/
#if defined(__APPLE__) && defined(TARGET_OS_IOS) && TARGET_OS_IOS

#import <UIKit/UIKit.h>
#import <CoreMotion/CoreMotion.h>

static CMMotionManager* _ssens_ios_mgr;

bool ssens_start(void) {
    if (nil == _ssens_ios_mgr) _ssens_ios_mgr = [[CMMotionManager alloc] init];
    if (!_ssens_ios_mgr.deviceMotionAvailable) return false;
    _ssens_ios_mgr.deviceMotionUpdateInterval = 1.0 / 60.0;
    if (!_ssens_ios_mgr.deviceMotionActive) [_ssens_ios_mgr startDeviceMotionUpdates];
    return true;
}

void ssens_stop(void) {
    if (nil != _ssens_ios_mgr && _ssens_ios_mgr.deviceMotionActive) [_ssens_ios_mgr stopDeviceMotionUpdates];
}

static int _ssens_ios_rotation(void) {
    UIInterfaceOrientation o = UIInterfaceOrientationPortrait;
    for (UIScene* scene in [UIApplication sharedApplication].connectedScenes) {
        if ([scene isKindOfClass:[UIWindowScene class]]) { o = ((UIWindowScene*)scene).interfaceOrientation; break; }
    }
    /* The screen's turn from the device's natural (portrait) frame, counter-clockwise. */
    switch (o) {
        case UIInterfaceOrientationLandscapeLeft:      return 270;   /* home button on the left */
        case UIInterfaceOrientationLandscapeRight:     return 90;    /* home button on the right */
        case UIInterfaceOrientationPortraitUpsideDown: return 180;
        default:                                       return 0;
    }
}

int ssens_gravity(float* o) {
    if (NULL == o) return 0;
    o[0] = o[1] = o[2] = 0.0f;
    CMDeviceMotion* m = (nil != _ssens_ios_mgr) ? _ssens_ios_mgr.deviceMotion : nil;
    if (nil == m) return 0;
    /* CoreMotion: units of g, pointing towards the earth - the same direction as ours. */
    const float d[3] = { (float)m.gravity.x * 9.80665f, (float)m.gravity.y * 9.80665f, (float)m.gravity.z * 9.80665f };
    _ssens_turn(d, _ssens_ios_rotation(), o);
    return 1;
}

/*--- Android ---------------------------------------------------------------*/
#elif defined(__ANDROID__)

#include <jni.h>
#include <android/looper.h>
#include <android/sensor.h>
#include <android/native_activity.h>

static ASensorManager*    _ssens_mgr;
static const ASensor*     _ssens_sensor;
static ASensorEventQueue* _ssens_queue;
static float _ssens_last[3];
static bool  _ssens_have;
static int   _ssens_rotation;

/* The display's rotation (Surface.ROTATION_0..270 -> degrees), by JNI: once, when started. */
static int _ssens_android_rotation(void) {
    const ANativeActivity* act = (const ANativeActivity*) sapp_android_get_native_activity();
    if ((NULL == act) || (NULL == act->vm) || (NULL == act->clazz)) return 0;
    JavaVM* vm = act->vm;
    JNIEnv* env = NULL;
    if ((*vm)->GetEnv(vm, (void**)&env, JNI_VERSION_1_6) != JNI_OK) {
        if ((*vm)->AttachCurrentThread(vm, &env, NULL) != JNI_OK) return 0;
    }
    if ((NULL == env) || (*env)->PushLocalFrame(env, 16) != 0) { if (env) (*env)->ExceptionClear(env); return 0; }
    int degrees = 0;
    jclass cls_act = (*env)->GetObjectClass(env, act->clazz);
    jmethodID m_wm = (*env)->GetMethodID(env, cls_act, "getWindowManager", "()Landroid/view/WindowManager;");
    jobject wm = m_wm ? (*env)->CallObjectMethod(env, act->clazz, m_wm) : NULL;
    if (wm) {
        jclass cls_wm = (*env)->GetObjectClass(env, wm);
        jmethodID m_disp = (*env)->GetMethodID(env, cls_wm, "getDefaultDisplay", "()Landroid/view/Display;");
        jobject disp = m_disp ? (*env)->CallObjectMethod(env, wm, m_disp) : NULL;
        if (disp) {
            jclass cls_disp = (*env)->GetObjectClass(env, disp);
            jmethodID m_rot = (*env)->GetMethodID(env, cls_disp, "getRotation", "()I");
            if (m_rot) degrees = 90 * (int)(*env)->CallIntMethod(env, disp, m_rot);
        }
    }
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); degrees = 0; }
    (*env)->PopLocalFrame(env, NULL);
    return degrees;
}

bool ssens_start(void) {
    if (NULL == _ssens_mgr) _ssens_mgr = ASensorManager_getInstance();
    if (NULL == _ssens_mgr) return false;
    if (NULL == _ssens_sensor) {
        _ssens_sensor = ASensorManager_getDefaultSensor(_ssens_mgr, 9 /* ASENSOR_TYPE_GRAVITY */);
        if (NULL == _ssens_sensor) _ssens_sensor = ASensorManager_getDefaultSensor(_ssens_mgr, ASENSOR_TYPE_ACCELEROMETER);
    }
    if (NULL == _ssens_sensor) return false;
    if (NULL == _ssens_queue) {
        ALooper* looper = ALooper_forThread();
        if (NULL == looper) looper = ALooper_prepare(ALOOPER_PREPARE_ALLOW_NON_CALLBACKS);
        _ssens_queue = ASensorManager_createEventQueue(_ssens_mgr, looper, 3 /* an ident sokol does not use */, NULL, NULL);
        if (NULL == _ssens_queue) return false;
    }
    ASensorEventQueue_enableSensor(_ssens_queue, _ssens_sensor);
    ASensorEventQueue_setEventRate(_ssens_queue, _ssens_sensor, 16667);   /* 60 a second */
    _ssens_rotation = _ssens_android_rotation();
    return true;
}

void ssens_stop(void) {
    if (_ssens_queue && _ssens_sensor) ASensorEventQueue_disableSensor(_ssens_queue, _ssens_sensor);
    _ssens_have = false;
}

int ssens_gravity(float* o) {
    if (NULL == o) return 0;
    o[0] = o[1] = o[2] = 0.0f;
    if (NULL == _ssens_queue) return 0;
    ASensorEvent ev[8];
    ssize_t n;
    while ((n = ASensorEventQueue_getEvents(_ssens_queue, ev, 8)) > 0) {
        /* Android reports the push against gravity (face up: z = +9.81): ours is the pull, the other way. */
        const ASensorEvent* e = &ev[n - 1];
        _ssens_last[0] = -e->vector.x; _ssens_last[1] = -e->vector.y; _ssens_last[2] = -e->vector.z;
        _ssens_have = true;
    }
    if (!_ssens_have) return 0;
    _ssens_turn(_ssens_last, _ssens_rotation, o);
    return 1;
}

/*--- Web -------------------------------------------------------------------*/
#elif defined(__EMSCRIPTEN__)

#include <emscripten/emscripten.h>

EM_JS(int, _ssens_js_start, (void), {
    if (typeof window === 'undefined' || typeof DeviceMotionEvent === 'undefined') return 0;
    if (!Module._ssens) {
        Module._ssens = { x: 0, y: 0, z: 0, have: false, on: false };
        window.addEventListener('devicemotion', function (e) {
            var g = e.accelerationIncludingGravity;
            if (!Module._ssens.on || !g || g.x === null) return;
            /* The spec's push against gravity (face up: z = +9.81) - ours is the pull. */
            Module._ssens.x = -g.x; Module._ssens.y = -g.y; Module._ssens.z = -g.z; Module._ssens.have = true;
        });
        /* iOS Safari: motion only after the player allows it, asked from a tap. */
        if (typeof DeviceMotionEvent.requestPermission === 'function') {
            var ask = function () {
                DeviceMotionEvent.requestPermission().catch(function () {});
                window.removeEventListener('touchend', ask); window.removeEventListener('click', ask);
            };
            window.addEventListener('touchend', ask); window.addEventListener('click', ask);
        }
    }
    Module._ssens.on = true;
    return 1;
});

EM_JS(void, _ssens_js_stop, (void), { if (Module._ssens) { Module._ssens.on = false; Module._ssens.have = false; } });

EM_JS(int, _ssens_js_read, (float* o), {
    var s = Module._ssens;
    if (!s || !s.have) return 0;
    var a = (screen.orientation && typeof screen.orientation.angle === 'number') ? screen.orientation.angle : (window.orientation || 0);
    HEAPF32[(o >> 2) + 0] = s.x; HEAPF32[(o >> 2) + 1] = s.y; HEAPF32[(o >> 2) + 2] = s.z; HEAPF32[(o >> 2) + 3] = a;
    return 1;
});

bool ssens_start(void) { return _ssens_js_start() != 0; }
void ssens_stop(void) { _ssens_js_stop(); }

int ssens_gravity(float* o) {
    if (NULL == o) return 0;
    o[0] = o[1] = o[2] = 0.0f;
    float raw[4];
    if (!_ssens_js_read(raw)) return 0;
    _ssens_turn(raw, (int)raw[3], o);
    return 1;
}

/*--- everything else -------------------------------------------------------*/
#else

bool ssens_start(void) { return false; }
void ssens_stop(void) { }
int ssens_gravity(float* o) {
    if (NULL != o) { o[0] = o[1] = o[2] = 0.0f; }
    return 0;
}

#endif
#endif /* SOKOL_SENSORS_IMPL */
