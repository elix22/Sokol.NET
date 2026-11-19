/* sokol_ads_android.c -- Google Mobile Ads bridge for Sokol.NET.
   The SDK integration lives in the Java helper com.sokol.ads.SokolAds
   (platform/android/java/, wired via AndroidJavaSource_sokoladsPath); this
   file forwards calls to it and receives events via nativeOnEvent.
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - play-services-ads + user-messaging-platform in app/build.gradle
     - <meta-data com.google.android.gms.ads.APPLICATION_ID> in the manifest
       (injected by the builder from the app's AdMobAppId_Android property)
*/
#include "sokol_ads.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stddef.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

/* sokol_ads_queue.c */
extern void sokolads__emit(int type, int code);
extern void sokolads__consume(void);
extern void sokolads__consume_rewarded(void);

static jclass    _sa_class;      /* global ref to com.sokol.ads.SokolAds */
static jmethodID _sa_init;
static jmethodID _sa_consent;
static jmethodID _sa_load;
static jmethodID _sa_show;
static jmethodID _sa_load_rw;
static jmethodID _sa_show_rw;

static JNIEnv* _sa_env(void)
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
static bool _sa_resolve(JNIEnv* env, ANativeActivity* activity)
{
    if (_sa_class) return true;

    jclass    clsLdrCls = (*env)->FindClass(env, "java/lang/ClassLoader");
    jmethodID loadClass = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                              "(Ljava/lang/String;)Ljava/lang/Class;");
    jclass    actCls    = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID getClsLdr = (*env)->GetMethodID(env, actCls, "getClassLoader",
                              "()Ljava/lang/ClassLoader;");
    jobject   loader    = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
    jstring   name      = (*env)->NewStringUTF(env, "com.sokol.ads.SokolAds");
    jclass    cls       = (jclass)(*env)->CallObjectMethod(env, loader, loadClass, name);

    (*env)->DeleteLocalRef(env, name);
    (*env)->DeleteLocalRef(env, loader);
    (*env)->DeleteLocalRef(env, actCls);
    (*env)->DeleteLocalRef(env, clsLdrCls);

    if (!cls || (*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
        return false;
    }

    _sa_class   = (jclass)(*env)->NewGlobalRef(env, cls);
    _sa_init    = (*env)->GetStaticMethodID(env, _sa_class, "init",
                      "(Landroid/app/Activity;ZZ)V");
    _sa_consent = (*env)->GetStaticMethodID(env, _sa_class, "gatherConsent", "()V");
    _sa_load    = (*env)->GetStaticMethodID(env, _sa_class, "loadInterstitial",
                      "(Ljava/lang/String;)V");
    _sa_show    = (*env)->GetStaticMethodID(env, _sa_class, "showInterstitial", "()V");
    _sa_load_rw = (*env)->GetStaticMethodID(env, _sa_class, "loadRewarded",
                      "(Ljava/lang/String;)V");
    _sa_show_rw = (*env)->GetStaticMethodID(env, _sa_class, "showRewarded", "()V");
    if ((*env)->ExceptionCheck(env)) {   /* an older helper without rewarded */
        (*env)->ExceptionClear(env);
        _sa_load_rw = NULL;
        _sa_show_rw = NULL;
    }
    (*env)->DeleteLocalRef(env, cls);
    return true;
}

void sokolads_init(const char* app_id, bool npa_only, bool child_directed)
{
    (void)app_id;   /* Android reads the APPLICATION_ID from the manifest */
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _sa_env();
    if (!activity || !env || !_sa_resolve(env, activity)) return;
    (*env)->CallStaticVoidMethod(env, _sa_class, _sa_init, activity->clazz,
                                 (jboolean)npa_only, (jboolean)child_directed);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

void sokolads_consent_gather(void)
{
    if (!_sa_class || !_sa_consent) return;
    JNIEnv* env = _sa_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _sa_class, _sa_consent);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

void sokolads_load_interstitial(const char* ad_unit_id)
{
    if (!_sa_class || !_sa_load || !ad_unit_id) return;
    JNIEnv* env = _sa_env();
    if (!env) return;
    jstring junit = (*env)->NewStringUTF(env, ad_unit_id);
    (*env)->CallStaticVoidMethod(env, _sa_class, _sa_load, junit);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
    (*env)->DeleteLocalRef(env, junit);
}

void sokolads_show_interstitial(void)
{
    if (!_sa_class || !_sa_show || !sokolads_interstitial_ready()) return;
    sokolads__consume();
    JNIEnv* env = _sa_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _sa_class, _sa_show);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

void sokolads_load_rewarded(const char* ad_unit_id)
{
    if (!ad_unit_id) return;
    if (!_sa_class || !_sa_load_rw) {
        sokolads__emit(SOKOLADS_EVENT_REWARDED_LOAD_FAILED, -1);
        return;
    }
    JNIEnv* env = _sa_env();
    if (!env) return;
    jstring junit = (*env)->NewStringUTF(env, ad_unit_id);
    (*env)->CallStaticVoidMethod(env, _sa_class, _sa_load_rw, junit);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
    (*env)->DeleteLocalRef(env, junit);
}

void sokolads_show_rewarded(void)
{
    if (!_sa_class || !_sa_show_rw || !sokolads_rewarded_ready()) return;
    sokolads__consume_rewarded();
    JNIEnv* env = _sa_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _sa_class, _sa_show_rw);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

/* Upcall from com.sokol.ads.SokolAds (any Java thread). */
JNIEXPORT void JNICALL
Java_com_sokol_ads_SokolAds_nativeOnEvent(JNIEnv* env, jclass cls, jint type, jint code)
{
    (void)env; (void)cls;
    sokolads__emit((int)type, (int)code);
}
