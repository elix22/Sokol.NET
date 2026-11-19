/* sokol_billing_android.c -- Google Play Billing bridge for Sokol.NET.
   The heavy lifting lives in the Java helper com.sokol.billing.SokolBilling
   (platform/android/java/, wired via AndroidJavaSource_sokolbillingPath);
   this file forwards calls to it and receives events via nativeOnEvent.
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - com.android.billingclient:billing in app/build.gradle (gradle-deps.txt)
*/
#include "sokol_billing.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stddef.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

/* sokol_billing_queue.c */
extern void sokolbilling__emit(int type, int code, const char* sku, const char* price,
                               const char* proof, const char* signature);

static jclass    _sb_class;        /* global ref to com.sokol.billing.SokolBilling */
static jmethodID _sb_init;
static jmethodID _sb_query;
static jmethodID _sb_purchase;
static jmethodID _sb_restore;
static jmethodID _sb_sync;
static jmethodID _sb_consume;
static jmethodID _sb_finish;

static JNIEnv* _sb_env(void)
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
static bool _sb_resolve(JNIEnv* env, ANativeActivity* activity)
{
    if (_sb_class) return true;

    jclass    clsLdrCls = (*env)->FindClass(env, "java/lang/ClassLoader");
    jmethodID loadClass = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                              "(Ljava/lang/String;)Ljava/lang/Class;");
    jclass    actCls    = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID getClsLdr = (*env)->GetMethodID(env, actCls, "getClassLoader",
                              "()Ljava/lang/ClassLoader;");
    jobject   loader    = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
    jstring   name      = (*env)->NewStringUTF(env, "com.sokol.billing.SokolBilling");
    jclass    cls       = (jclass)(*env)->CallObjectMethod(env, loader, loadClass, name);

    (*env)->DeleteLocalRef(env, name);
    (*env)->DeleteLocalRef(env, loader);
    (*env)->DeleteLocalRef(env, actCls);
    (*env)->DeleteLocalRef(env, clsLdrCls);

    if (!cls || (*env)->ExceptionCheck(env)) {
        (*env)->ExceptionClear(env);
        return false;
    }

    _sb_class    = (jclass)(*env)->NewGlobalRef(env, cls);
    _sb_init     = (*env)->GetStaticMethodID(env, _sb_class, "init",
                       "(Landroid/app/Activity;)V");
    _sb_query    = (*env)->GetStaticMethodID(env, _sb_class, "queryProduct",
                       "(Ljava/lang/String;)V");
    _sb_purchase = (*env)->GetStaticMethodID(env, _sb_class, "purchase",
                       "(Ljava/lang/String;)V");
    _sb_restore  = (*env)->GetStaticMethodID(env, _sb_class, "restore", "()V");
    _sb_sync     = (*env)->GetStaticMethodID(env, _sb_class, "sync", "()V");
    _sb_consume  = (*env)->GetStaticMethodID(env, _sb_class, "consume",
                       "(Ljava/lang/String;)V");
    _sb_finish   = (*env)->GetStaticMethodID(env, _sb_class, "finishConsumable",
                       "(Ljava/lang/String;)V");
    if ((*env)->ExceptionCheck(env)) {   /* an older helper without consumables */
        (*env)->ExceptionClear(env);
        _sb_finish = NULL;
    }
    (*env)->DeleteLocalRef(env, cls);
    return true;
}

void sokolbilling_init(void)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    JNIEnv* env = _sb_env();
    if (!activity || !env || !_sb_resolve(env, activity)) return;
    (*env)->CallStaticVoidMethod(env, _sb_class, _sb_init, activity->clazz);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

static void _sb_call_str(jmethodID method, const char* sku)
{
    if (!_sb_class || !method || !sku) return;
    JNIEnv* env = _sb_env();
    if (!env) return;
    jstring jsku = (*env)->NewStringUTF(env, sku);
    (*env)->CallStaticVoidMethod(env, _sb_class, method, jsku);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
    (*env)->DeleteLocalRef(env, jsku);
}

void sokolbilling_query_product(const char* sku) { _sb_call_str(_sb_query, sku); }
void sokolbilling_purchase(const char* sku)      { _sb_call_str(_sb_purchase, sku); }

static void _sb_call_void(jmethodID method)
{
    if (!_sb_class || !method) return;
    JNIEnv* env = _sb_env();
    if (!env) return;
    (*env)->CallStaticVoidMethod(env, _sb_class, method);
    if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);
}

void sokolbilling_restore(void) { _sb_call_void(_sb_restore); }
void sokolbilling_sync(void)    { _sb_call_void(_sb_sync); }
void sokolbilling_consume(const char* sku) { _sb_call_str(_sb_consume, sku); }
void sokolbilling_finish_consumable(const char* sku) { _sb_call_str(_sb_finish, sku); }

/* Upcall from com.sokol.billing.SokolBilling (any Java thread). */
JNIEXPORT void JNICALL
Java_com_sokol_billing_SokolBilling_nativeOnEvent(JNIEnv* env, jclass cls,
    jint type, jint code, jstring sku, jstring price, jstring proof, jstring signature)
{
    (void)cls;
    const char* cSku   = sku       ? (*env)->GetStringUTFChars(env, sku, NULL)       : NULL;
    const char* cPrice = price     ? (*env)->GetStringUTFChars(env, price, NULL)     : NULL;
    const char* cProof = proof     ? (*env)->GetStringUTFChars(env, proof, NULL)     : NULL;
    const char* cSig   = signature ? (*env)->GetStringUTFChars(env, signature, NULL) : NULL;

    sokolbilling__emit((int)type, (int)code, cSku, cPrice, cProof, cSig);

    if (cSku)   (*env)->ReleaseStringUTFChars(env, sku, cSku);
    if (cPrice) (*env)->ReleaseStringUTFChars(env, price, cPrice);
    if (cProof) (*env)->ReleaseStringUTFChars(env, proof, cProof);
    if (cSig)   (*env)->ReleaseStringUTFChars(env, signature, cSig);
}
