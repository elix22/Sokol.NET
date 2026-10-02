/* sokol_links_android.c -- open a link from the NativeActivity (pure JNI, no Java helper).
   The intents are the ones Android's "Common intents" guide gives:
     web URL : new Intent(Intent.ACTION_VIEW, uri)       -> the default browser
     mailto: : new Intent(Intent.ACTION_SENDTO, uri)     -> an email app only
   startActivity throws ActivityNotFoundException when no app handles the intent; that is the
   "false" answer. No <queries> manifest entry is needed: package visibility (Android 11+)
   restricts resolving/querying other apps, not starting an implicit intent.
   Requires sapp_android_get_native_activity() exported from sokol_app.h.
*/
#include "sokol_links.h"
#include <android/native_activity.h>
#include <jni.h>
#include <strings.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

bool sokollinks_open(const char* url)
{
    if (!url || !*url) return false;
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return false;
    JNIEnv* env = NULL;
    (*activity->vm)->AttachCurrentThread(activity->vm, &env, NULL);
    /* Do NOT DetachCurrentThread -- Sokol reuses this thread across frames. */
    if (!env) return false;

    bool mail = strncasecmp(url, "mailto:", 7) == 0;
    bool ok = false;
    (*env)->PushLocalFrame(env, 16);

    /* Framework classes resolve through FindClass even on a native thread (unlike app classes). */
    jclass    uriCls    = (*env)->FindClass(env, "android/net/Uri");
    jmethodID parse     = (*env)->GetStaticMethodID(env, uriCls, "parse", "(Ljava/lang/String;)Landroid/net/Uri;");
    jobject   uri       = (*env)->CallStaticObjectMethod(env, uriCls, parse, (*env)->NewStringUTF(env, url));
    if ((*env)->ExceptionCheck(env) || !uri) goto done;

    jclass    intentCls = (*env)->FindClass(env, "android/content/Intent");
    jmethodID ctor      = (*env)->GetMethodID(env, intentCls, "<init>", "(Ljava/lang/String;Landroid/net/Uri;)V");
    jstring   action    = (*env)->NewStringUTF(env, mail ? "android.intent.action.SENDTO" : "android.intent.action.VIEW");
    jobject   intent    = (*env)->NewObject(env, intentCls, ctor, action, uri);
    if ((*env)->ExceptionCheck(env) || !intent) goto done;

    jclass    actCls    = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID start     = (*env)->GetMethodID(env, actCls, "startActivity", "(Landroid/content/Intent;)V");
    (*env)->CallVoidMethod(env, activity->clazz, start, intent);
    ok = !(*env)->ExceptionCheck(env);   /* ActivityNotFoundException: no browser / no mail app */

done:
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
    (*env)->PopLocalFrame(env, NULL);
    return ok;
}
