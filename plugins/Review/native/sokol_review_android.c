/* sokol_review_android.c -- Play In-App Review bridge for Sokol.NET.
   The Play work lives in the Java helper com.sokol.review.SokolReview (platform/android/java/, wired via
   AndroidJavaSource_sokolreviewPath); this file forwards calls to it and keeps the state it pushes back
   through nativeOnState.
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - com.google.android.play:review (platform/android/gradle-deps.txt)
*/
#include "sokol_review.h"
#include <android/native_activity.h>
#include <jni.h>
#include <pthread.h>
#include <stddef.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

static jclass    _rv_class;      /* global ref to com.sokol.review.SokolReview */
static jmethodID _rv_request;
static jmethodID _rv_use_fake;
static jmethodID _rv_installer;

static pthread_mutex_t _rv_lock = PTHREAD_MUTEX_INITIALIZER;
static int _rv_state = SOKOLREVIEW_IDLE;
static int _rv_error = 0;

static JNIEnv* _rv_env(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return NULL;
    JNIEnv* env = NULL;
    (*activity->vm)->AttachCurrentThread(activity->vm, &env, NULL);
    /* Do NOT DetachCurrentThread -- Sokol reuses this thread across frames. */
    return env;
}

/* Load the helper class through the activity's ClassLoader (FindClass from a native thread uses the
   bootstrap loader and cannot see app classes). */
static bool _rv_resolve(JNIEnv* env, ANativeActivity* activity)
{
    if (_rv_class) return true;

    jclass    clsLdrCls = (*env)->FindClass(env, "java/lang/ClassLoader");
    jmethodID loadClass = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                              "(Ljava/lang/String;)Ljava/lang/Class;");
    jclass    actCls    = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID getClsLdr = (*env)->GetMethodID(env, actCls, "getClassLoader",
                              "()Ljava/lang/ClassLoader;");
    jobject   loader    = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
    jstring   name      = (*env)->NewStringUTF(env, "com.sokol.review.SokolReview");
    jclass    cls       = (jclass)(*env)->CallObjectMethod(env, loader, loadClass, name);

    (*env)->DeleteLocalRef(env, name);
    (*env)->DeleteLocalRef(env, loader);
    (*env)->DeleteLocalRef(env, actCls);
    (*env)->DeleteLocalRef(env, clsLdrCls);

    if (!cls || (*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
        return false;
    }

    _rv_class    = (jclass)(*env)->NewGlobalRef(env, cls);
    _rv_request  = (*env)->GetStaticMethodID(env, _rv_class, "request", "(Landroid/app/Activity;)Z");
    _rv_use_fake = (*env)->GetStaticMethodID(env, _rv_class, "useFake", "(Z)V");
    _rv_installer = (*env)->GetStaticMethodID(env, _rv_class, "installer", "(Landroid/app/Activity;)Ljava/lang/String;");
    (*env)->DeleteLocalRef(env, cls);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); return false; }
    return true;
}

static void _rv_set(int state, int error)
{
    pthread_mutex_lock(&_rv_lock);
    _rv_state = state;
    _rv_error = error;
    pthread_mutex_unlock(&_rv_lock);
}

bool sokolreview_request(void)
{
    if (sokolreview_get_state() == SOKOLREVIEW_REQUESTING) return false;
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _rv_env();
    if (!activity || !env || !_rv_resolve(env, activity) || !_rv_request) { _rv_set(SOKOLREVIEW_FAILED, -100); return false; }
    _rv_set(SOKOLREVIEW_REQUESTING, 0);
    jboolean ok = (*env)->CallStaticBooleanMethod(env, _rv_class, _rv_request, activity->clazz);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); ok = JNI_FALSE; }
    if (ok != JNI_TRUE) { _rv_set(SOKOLREVIEW_FAILED, -100); return false; }
    return true;
}

int sokolreview_get_state(void)
{
    pthread_mutex_lock(&_rv_lock);
    int s = _rv_state;
    pthread_mutex_unlock(&_rv_lock);
    return s;
}

int sokolreview_get_error(void)
{
    pthread_mutex_lock(&_rv_lock);
    int e = _rv_error;
    pthread_mutex_unlock(&_rv_lock);
    return e;
}

void sokolreview_test_use_fake(bool on)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _rv_env();
    if (!activity || !env || !_rv_resolve(env, activity) || !_rv_use_fake) return;
    (*env)->CallStaticVoidMethod(env, _rv_class, _rv_use_fake, on ? JNI_TRUE : JNI_FALSE);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

int sokolreview_test_install_source(char* buf, int cap)
{
    if (!buf || cap <= 0) return 0;
    buf[0] = 0;
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _rv_env();
    if (!activity || !env || !_rv_resolve(env, activity) || !_rv_installer) return 0;
    jstring js = (jstring)(*env)->CallStaticObjectMethod(env, _rv_class, _rv_installer, activity->clazz);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); return 0; }
    if (!js) return 0;
    const char* c = (*env)->GetStringUTFChars(env, js, NULL);
    int n = 0;
    if (c) {
        while (c[n] && n < cap - 1) { buf[n] = c[n]; n++; }
        buf[n] = 0;
        (*env)->ReleaseStringUTFChars(env, js, c);
    }
    (*env)->DeleteLocalRef(env, js);
    return n;
}

/* Upcall from com.sokol.review.SokolReview (Android UI thread). */
JNIEXPORT void JNICALL
Java_com_sokol_review_SokolReview_nativeOnState(JNIEnv* env, jclass cls, jint state, jint error)
{
    (void)env; (void)cls;
    _rv_set((int)state, (int)error);
}
