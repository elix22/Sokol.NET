/* sokol_notifications_android.c -- local notifications bridge for Sokol.NET.
   The work lives in the Java helper com.sokol.notifications.SokolNotifications
   (platform/android/java/, wired via AndroidJavaSource_sokolnotificationsPath);
   this file forwards calls to it and receives events via nativeOnEvent.
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - androidx.work:work-runtime in app/build.gradle (gradle-deps.txt)
*/
#include "sokol_notifications.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stddef.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

/* sokol_notifications_queue.c */
extern void sokolnotify__emit(int type, int value, long long time);

static jclass    _sn_class;        /* global ref to com.sokol.notifications.SokolNotifications */
static jmethodID _sn_init;
static jmethodID _sn_configure;
static jmethodID _sn_status;
static jmethodID _sn_request;
static jmethodID _sn_schedule;
static jmethodID _sn_cancel;
static jmethodID _sn_cancel_all;
static jmethodID _sn_query;
static jmethodID _sn_take_opened;
static jmethodID _sn_open_settings;

static JNIEnv* _sn_env(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return NULL;
    JNIEnv* env = NULL;
    (*activity->vm)->AttachCurrentThread(activity->vm, &env, NULL);
    /* Do NOT DetachCurrentThread — Sokol reuses this thread across frames. */
    return env;
}

/* Load the helper class through the activity's ClassLoader (FindClass from a
   native thread uses the bootstrap loader and cannot see app classes). */
static bool _sn_resolve(JNIEnv* env, ANativeActivity* activity)
{
    if (_sn_class) return true;

    jclass    clsLdrCls = (*env)->FindClass(env, "java/lang/ClassLoader");
    jmethodID loadClass = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                              "(Ljava/lang/String;)Ljava/lang/Class;");
    jclass    actCls    = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID getClsLdr = (*env)->GetMethodID(env, actCls, "getClassLoader",
                              "()Ljava/lang/ClassLoader;");
    jobject   loader    = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
    jstring   name      = (*env)->NewStringUTF(env, "com.sokol.notifications.SokolNotifications");
    jclass    cls       = (jclass)(*env)->CallObjectMethod(env, loader, loadClass, name);

    (*env)->DeleteLocalRef(env, name);
    (*env)->DeleteLocalRef(env, loader);
    (*env)->DeleteLocalRef(env, actCls);
    (*env)->DeleteLocalRef(env, clsLdrCls);

    if (!cls || (*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
        return false;
    }

    _sn_class         = (jclass)(*env)->NewGlobalRef(env, cls);
    _sn_init          = (*env)->GetStaticMethodID(env, _sn_class, "init", "(Landroid/app/Activity;)V");
    _sn_configure     = (*env)->GetStaticMethodID(env, _sn_class, "configure",
                            "(Ljava/lang/String;Ljava/lang/String;)V");
    _sn_status        = (*env)->GetStaticMethodID(env, _sn_class, "status", "()I");
    _sn_request       = (*env)->GetStaticMethodID(env, _sn_class, "requestPermission", "()V");
    _sn_schedule      = (*env)->GetStaticMethodID(env, _sn_class, "schedule",
                            "(IJJLjava/lang/String;Ljava/lang/String;)V");
    _sn_cancel        = (*env)->GetStaticMethodID(env, _sn_class, "cancel", "(I)V");
    _sn_cancel_all    = (*env)->GetStaticMethodID(env, _sn_class, "cancelAll", "()V");
    _sn_query         = (*env)->GetStaticMethodID(env, _sn_class, "queryPending", "()V");
    _sn_take_opened   = (*env)->GetStaticMethodID(env, _sn_class, "takeOpened", "()I");
    _sn_open_settings = (*env)->GetStaticMethodID(env, _sn_class, "openSettings", "()V");
    (*env)->DeleteLocalRef(env, cls);
    if ((*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
        return false;
    }
    return true;
}

int sokolnotify_init(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _sn_env();
    if (!activity || !env || !_sn_resolve(env, activity)) return 0;
    (*env)->CallStaticVoidMethod(env, _sn_class, _sn_init, activity->clazz);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); return 0; }
    return 1;
}

static void _sn_call_void(jmethodID method)
{
    if (!_sn_class || !method) return;
    JNIEnv* env = _sn_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _sn_class, method);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

static int _sn_call_int(jmethodID method)
{
    if (!_sn_class || !method) return 0;
    JNIEnv* env = _sn_env();
    if (!env) return 0;
    jint r = (*env)->CallStaticIntMethod(env, _sn_class, method);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); return 0; }
    return (int)r;
}

static jstring _sn_str(JNIEnv* env, const char* s) { return s ? (*env)->NewStringUTF(env, s) : NULL; }

void sokolnotify_configure(const char* channel_name, const char* small_icon_asset)
{
    if (!_sn_class) return;
    JNIEnv* env = _sn_env();
    if (!env) return;
    jstring name = _sn_str(env, channel_name);
    jstring icon = _sn_str(env, small_icon_asset);
    (*env)->CallStaticVoidMethod(env, _sn_class, _sn_configure, name, icon);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
    if (name) (*env)->DeleteLocalRef(env, name);
    if (icon) (*env)->DeleteLocalRef(env, icon);
}

int  sokolnotify_status(void)              { return _sn_call_int(_sn_status); }
void sokolnotify_request_permission(void)  { _sn_call_void(_sn_request); }
void sokolnotify_cancel_all(void)          { _sn_call_void(_sn_cancel_all); }
void sokolnotify_query_pending(void)       { _sn_call_void(_sn_query); }
int  sokolnotify_take_opened(void)         { return _sn_call_int(_sn_take_opened); }
void sokolnotify_open_settings(void)       { _sn_call_void(_sn_open_settings); }

void sokolnotify_schedule(int id, long long fire_unix, long long expire_unix,
                          const char* title, const char* body)
{
    if (!_sn_class || id < 1 || id > SOKOLNOTIFY_MAX_ID) return;
    JNIEnv* env = _sn_env();
    if (!env) return;
    jstring jt = _sn_str(env, title);
    jstring jb = _sn_str(env, body);
    (*env)->CallStaticVoidMethod(env, _sn_class, _sn_schedule, (jint)id,
                                 (jlong)fire_unix, (jlong)expire_unix, jt, jb);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
    if (jt) (*env)->DeleteLocalRef(env, jt);
    if (jb) (*env)->DeleteLocalRef(env, jb);
}

void sokolnotify_cancel(int id)
{
    if (!_sn_class) return;
    JNIEnv* env = _sn_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _sn_class, _sn_cancel, (jint)id);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

/* Upcall from com.sokol.notifications.SokolNotifications (any Java thread). */
JNIEXPORT void JNICALL
Java_com_sokol_notifications_SokolNotifications_nativeOnEvent(JNIEnv* env, jclass cls,
    jint type, jint value, jlong time)
{
    (void)env; (void)cls;
    sokolnotify__emit((int)type, (int)value, (long long)time);
}
