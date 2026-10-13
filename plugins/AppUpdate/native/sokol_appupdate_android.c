/* sokol_appupdate_android.c -- Play In-App Updates bridge for Sokol.NET.
   The Play work lives in the Java helper com.sokol.appupdate.SokolAppUpdate
   (platform/android/java/, wired via AndroidJavaSource_sokolappupdatePath); this
   file forwards calls to it and keeps the state snapshot it pushes through
   nativeOnState.
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - com.google.android.play:app-update (platform/android/gradle-deps.txt)
*/
#include "sokol_appupdate.h"
#include <android/native_activity.h>
#include <jni.h>
#include <pthread.h>
#include <stddef.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

static jclass    _au_class;        /* global ref to com.sokol.appupdate.SokolAppUpdate */
static jmethodID _au_init;
static jmethodID _au_check;
static jmethodID _au_start_flexible;
static jmethodID _au_complete;
static jmethodID _au_use_fake;
static jmethodID _au_fake;
static jmethodID _au_fake_flags;

static pthread_mutex_t      _au_lock = PTHREAD_MUTEX_INITIALIZER;
static sokolappupdate_state _au_state = { .staleness_days = -1 };

static JNIEnv* _au_env(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return NULL;
    JNIEnv* env = NULL;
    (*activity->vm)->AttachCurrentThread(activity->vm, &env, NULL);
    /* Do NOT DetachCurrentThread -- Sokol reuses this thread across frames. */
    return env;
}

/* Load the helper class through the activity's ClassLoader (FindClass from a
   native thread uses the bootstrap loader and cannot see app classes). */
static bool _au_resolve(JNIEnv* env, ANativeActivity* activity)
{
    if (_au_class) return true;

    jclass    clsLdrCls = (*env)->FindClass(env, "java/lang/ClassLoader");
    jmethodID loadClass = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                              "(Ljava/lang/String;)Ljava/lang/Class;");
    jclass    actCls    = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID getClsLdr = (*env)->GetMethodID(env, actCls, "getClassLoader",
                              "()Ljava/lang/ClassLoader;");
    jobject   loader    = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
    jstring   name      = (*env)->NewStringUTF(env, "com.sokol.appupdate.SokolAppUpdate");
    jclass    cls       = (jclass)(*env)->CallObjectMethod(env, loader, loadClass, name);

    (*env)->DeleteLocalRef(env, name);
    (*env)->DeleteLocalRef(env, loader);
    (*env)->DeleteLocalRef(env, actCls);
    (*env)->DeleteLocalRef(env, clsLdrCls);

    if (!cls || (*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
        return false;
    }

    _au_class          = (jclass)(*env)->NewGlobalRef(env, cls);
    _au_init           = (*env)->GetStaticMethodID(env, _au_class, "init", "(Landroid/app/Activity;)V");
    _au_check          = (*env)->GetStaticMethodID(env, _au_class, "check", "()V");
    _au_start_flexible = (*env)->GetStaticMethodID(env, _au_class, "startFlexible", "()Z");
    _au_complete       = (*env)->GetStaticMethodID(env, _au_class, "complete", "()V");
    _au_use_fake       = (*env)->GetStaticMethodID(env, _au_class, "useFake", "(I)V");
    _au_fake           = (*env)->GetStaticMethodID(env, _au_class, "fake", "(Ljava/lang/String;)V");
    _au_fake_flags     = (*env)->GetStaticMethodID(env, _au_class, "fakeFlags", "()I");
    (*env)->DeleteLocalRef(env, cls);
    return true;
}

static void _au_set_error(int code)
{
    pthread_mutex_lock(&_au_lock);
    _au_state.availability = SOKOLAPPUPDATE_AVAIL_ERROR;
    _au_state.error_code   = code;
    _au_state.serial++;
    pthread_mutex_unlock(&_au_lock);
}

void sokolappupdate_init(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _au_env();
    if (!activity || !env || !_au_resolve(env, activity)) { _au_set_error(-100); return; }
    (*env)->CallStaticVoidMethod(env, _au_class, _au_init, activity->clazz);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); _au_set_error(-100); }
}

static void _au_call_void(jmethodID method)
{
    if (!_au_class || !method) return;
    JNIEnv* env = _au_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _au_class, method);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

void sokolappupdate_check(void)    { _au_call_void(_au_check); }
void sokolappupdate_complete(void) { _au_call_void(_au_complete); }

bool sokolappupdate_start_flexible(void)
{
    if (!_au_class) return false;
    JNIEnv* env = _au_env();
    if (!env) return false;
    jboolean ok = (*env)->CallStaticBooleanMethod(env, _au_class, _au_start_flexible);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); ok = JNI_FALSE; }
    return ok == JNI_TRUE;
}

void sokolappupdate_get_state(sokolappupdate_state* out)
{
    if (!out) return;
    pthread_mutex_lock(&_au_lock);
    *out = _au_state;
    pthread_mutex_unlock(&_au_lock);
}

void sokolappupdate_test_use_fake(int version_code)
{
    if (!_au_class) return;
    JNIEnv* env = _au_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _au_class, _au_use_fake, (jint)version_code);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

void sokolappupdate_test_fake(const char* action)
{
    if (!_au_class || !action) return;
    JNIEnv* env = _au_env();
    if (!env) return;
    jstring jaction = (*env)->NewStringUTF(env, action);
    (*env)->CallStaticVoidMethod(env, _au_class, _au_fake, jaction);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
    (*env)->DeleteLocalRef(env, jaction);
}

int sokolappupdate_test_fake_flags(void)
{
    if (!_au_class) return 0;
    JNIEnv* env = _au_env();
    if (!env) return 0;
    jint flags = (*env)->CallStaticIntMethod(env, _au_class, _au_fake_flags);
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); flags = 0; }
    return (int)flags;
}

/* Upcall from com.sokol.appupdate.SokolAppUpdate (Android UI thread). */
JNIEXPORT void JNICALL
Java_com_sokol_appupdate_SokolAppUpdate_nativeOnState(JNIEnv* env, jclass cls,
    jint availability, jint flexibleAllowed, jint installStatus, jint versionCode,
    jint stalenessDays, jint flow, jint errorCode, jlong bytesDownloaded, jlong totalBytes)
{
    (void)env; (void)cls;
    pthread_mutex_lock(&_au_lock);
    _au_state.availability           = (int)availability;
    _au_state.flexible_allowed       = (int)flexibleAllowed;
    _au_state.install_status         = (int)installStatus;
    _au_state.available_version_code = (int)versionCode;
    _au_state.staleness_days         = (int)stalenessDays;
    _au_state.flow                   = (int)flow;
    _au_state.error_code             = (int)errorCode;
    _au_state.bytes_downloaded       = (long long)bytesDownloaded;
    _au_state.total_bytes            = (long long)totalBytes;
    _au_state.serial++;
    pthread_mutex_unlock(&_au_lock);
}
