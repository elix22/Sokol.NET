/* sokol_inlinks_android.c -- take the link that opened the app from the NativeActivity (pure JNI, no Java helper).
   SokolNativeActivity keeps the ACTION_VIEW intent's URI in a static field (set in onCreate and onNewIntent,
   the activity is singleTask) and hands it out once through the static takeIncomingUri(). The plain
   NativeActivity (AndroidFullscreen=false) has no such method: then getIntent() is read once, which covers a
   cold start only.
   Requires sapp_android_get_native_activity() exported from sokol_app.h.
*/
#include "sokol_inlinks.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

enum { LOOKUP, TAKE, INTENT_ONCE, DONE };
static int       _mode = LOOKUP;
static jclass    _activity_class;   /* global ref, mode TAKE */
static jmethodID _take_method;
static char*     _taken;            /* the string handed out last time */

/* getIntent().getData().toString() when the action is ACTION_VIEW, else NULL. */
static jstring intent_uri(JNIEnv* env, jobject activity)
{
    jclass    actCls    = (*env)->GetObjectClass(env, activity);
    jobject   intent    = (*env)->CallObjectMethod(env, activity, (*env)->GetMethodID(env, actCls, "getIntent", "()Landroid/content/Intent;"));
    if ((*env)->ExceptionCheck(env) || !intent) return NULL;
    jclass    intentCls = (*env)->GetObjectClass(env, intent);
    jstring   action    = (jstring)(*env)->CallObjectMethod(env, intent, (*env)->GetMethodID(env, intentCls, "getAction", "()Ljava/lang/String;"));
    if ((*env)->ExceptionCheck(env) || !action) return NULL;
    const char* a = (*env)->GetStringUTFChars(env, action, NULL);
    bool view = a && strcmp(a, "android.intent.action.VIEW") == 0;
    if (a) (*env)->ReleaseStringUTFChars(env, action, a);
    if (!view) return NULL;
    jobject   uri       = (*env)->CallObjectMethod(env, intent, (*env)->GetMethodID(env, intentCls, "getData", "()Landroid/net/Uri;"));
    if ((*env)->ExceptionCheck(env) || !uri) return NULL;
    return (jstring)(*env)->CallObjectMethod(env, uri, (*env)->GetMethodID(env, (*env)->GetObjectClass(env, uri), "toString", "()Ljava/lang/String;"));
}

const char* sokolinlinks_take(void)
{
    free(_taken);
    _taken = NULL;
    if (_mode == DONE) return NULL;
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return NULL;
    JNIEnv* env = NULL;
    (*activity->vm)->AttachCurrentThread(activity->vm, &env, NULL);
    /* Do NOT DetachCurrentThread -- Sokol reuses this thread across frames. */
    if (!env) return NULL;

    (*env)->PushLocalFrame(env, 16);
    jstring s = NULL;
    if (_mode == LOOKUP)
    {
        /* activity->clazz is the activity instance: its class resolves without the app's ClassLoader. */
        jclass cls = (*env)->GetObjectClass(env, activity->clazz);
        _take_method = (*env)->GetStaticMethodID(env, cls, "takeIncomingUri", "()Ljava/lang/String;");
        if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); _take_method = NULL; }
        if (_take_method) { _activity_class = (jclass)(*env)->NewGlobalRef(env, cls); _mode = TAKE; }
        else _mode = INTENT_ONCE;
    }
    if (_mode == TAKE)
        s = (jstring)(*env)->CallStaticObjectMethod(env, _activity_class, _take_method);
    else if (_mode == INTENT_ONCE)
    {
        _mode = DONE;
        s = intent_uri(env, activity->clazz);
    }
    if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); s = NULL; }
    if (s)
    {
        /* JNI's modified UTF-8 differs from UTF-8 only for NUL and characters outside the BMP; a percent-encoded
           link has neither (the app validates the link's shape anyway). */
        const char* u = (*env)->GetStringUTFChars(env, s, NULL);
        if (u) { _taken = strdup(u); (*env)->ReleaseStringUTFChars(env, s, u); }
    }
    (*env)->PopLocalFrame(env, NULL);
    return _taken;
}
