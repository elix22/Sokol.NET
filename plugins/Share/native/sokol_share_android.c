/* sokol_share_android.c -- Android share sheet implementation for Sokol.NET.
   Uses JNI + androidx.core.content.FileProvider for content:// URI (API 24+).
   Requires:
     - sapp_android_get_native_activity() exported from sokol_app.h
     - androidx.core:core:1.12.0 in app/build.gradle
     - FileProvider <provider> in AndroidManifest.xml
     - file_provider_paths.xml in res/xml/
*/
#include "sokol_share.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>

/* Already declared in sokol_app.h; returns ANativeActivity* as const void*. */
extern const void* sapp_android_get_native_activity(void);

void sokolshare_image_text(const char* image_path, const char* text)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return;

    JavaVM* vm  = activity->vm;
    JNIEnv* env = NULL;
    (*vm)->AttachCurrentThread(vm, &env, NULL);
    if (!env) return;

    jstring jtext = (*env)->NewStringUTF(env, text       ? text       : "");
    jstring jpath = (*env)->NewStringUTF(env, image_path ? image_path : "");

    /* ── Resolve package name → FileProvider authority ─────────────────── */
    jclass    ctxClass = (*env)->FindClass(env, "android/content/Context");
    jmethodID getPkg   = (*env)->GetMethodID(env, ctxClass,
                             "getPackageName", "()Ljava/lang/String;");
    jstring   jpkg     = (jstring)(*env)->CallObjectMethod(env, activity->clazz, getPkg);

    const char* pkgStr = (*env)->GetStringUTFChars(env, jpkg, NULL);
    char authority[256];
    snprintf(authority, sizeof(authority), "%s.fileprovider", pkgStr);
    (*env)->ReleaseStringUTFChars(env, jpkg, pkgStr);
    jstring jauthority = (*env)->NewStringUTF(env, authority);

    /* ── Build content:// URI via FileProvider ──────────────────────────── */
    /* NOTE: FindClass() from a native thread uses the bootstrap class loader
       and cannot see app classes (e.g. androidx.*). We must load them through
       the activity's own ClassLoader, which has the full APK class path. */
    jobject uri = NULL;
    if (image_path && image_path[0])
    {
        jclass    fileClass  = (*env)->FindClass(env, "java/io/File");
        jmethodID fileInit   = (*env)->GetMethodID(env, fileClass,
                                   "<init>", "(Ljava/lang/String;)V");
        jobject   fileObj    = (*env)->NewObject(env, fileClass, fileInit, jpath);

        /* Load FileProvider via app ClassLoader (dots, not slashes). */
        jclass    clsLdrCls  = (*env)->FindClass(env, "java/lang/ClassLoader");
        jmethodID loadClass  = (*env)->GetMethodID(env, clsLdrCls, "loadClass",
                                   "(Ljava/lang/String;)Ljava/lang/Class;");
        jclass    actCls2    = (*env)->GetObjectClass(env, activity->clazz);
        jmethodID getClsLdr  = (*env)->GetMethodID(env, actCls2, "getClassLoader",
                                   "()Ljava/lang/ClassLoader;");
        jobject   loader     = (*env)->CallObjectMethod(env, activity->clazz, getClsLdr);
        jstring   fpName     = (*env)->NewStringUTF(env,
                                   "androidx.core.content.FileProvider");
        jclass    fpClass    = (jclass)(*env)->CallObjectMethod(env, loader,
                                   loadClass, fpName);

        jmethodID getUri     = (*env)->GetStaticMethodID(env, fpClass, "getUriForFile",
            "(Landroid/content/Context;Ljava/lang/String;Ljava/io/File;)"
            "Landroid/net/Uri;");
        uri = (*env)->CallStaticObjectMethod(env, fpClass, getUri,
                  activity->clazz, jauthority, fileObj);

        (*env)->DeleteLocalRef(env, fpName);
        (*env)->DeleteLocalRef(env, loader);
        (*env)->DeleteLocalRef(env, actCls2);
        (*env)->DeleteLocalRef(env, clsLdrCls);
        (*env)->DeleteLocalRef(env, fileObj);
        (*env)->DeleteLocalRef(env, fileClass);
        (*env)->DeleteLocalRef(env, fpClass);
    }

    /* ── Build ACTION_SEND intent ───────────────────────────────────────── */
    jclass    intentClass = (*env)->FindClass(env, "android/content/Intent");
    jmethodID intentInit  = (*env)->GetMethodID(env, intentClass,
                                "<init>", "(Ljava/lang/String;)V");
    jstring   action      = (*env)->NewStringUTF(env, "android.intent.action.SEND");
    jobject   intent      = (*env)->NewObject(env, intentClass, intentInit, action);

    jmethodID setType      = (*env)->GetMethodID(env, intentClass, "setType",
                                 "(Ljava/lang/String;)Landroid/content/Intent;");
    jmethodID putExtraStr  = (*env)->GetMethodID(env, intentClass, "putExtra",
                                 "(Ljava/lang/String;Ljava/lang/String;)"
                                 "Landroid/content/Intent;");
    jmethodID putExtraParc = (*env)->GetMethodID(env, intentClass, "putExtra",
                                 "(Ljava/lang/String;Landroid/os/Parcelable;)"
                                 "Landroid/content/Intent;");
    jmethodID addFlags     = (*env)->GetMethodID(env, intentClass, "addFlags",
                                 "(I)Landroid/content/Intent;");

    jstring mime      = (*env)->NewStringUTF(env, uri ? "image/png" : "text/plain");
    jstring keyText   = (*env)->NewStringUTF(env, "android.intent.extra.TEXT");
    jstring keyStream = (*env)->NewStringUTF(env, "android.intent.extra.STREAM");

    (*env)->CallObjectMethod(env, intent, setType, mime);
    (*env)->CallObjectMethod(env, intent, putExtraStr, keyText, jtext);
    if (uri)
    {
        (*env)->CallObjectMethod(env, intent, putExtraParc, keyStream, uri);
        /* FLAG_GRANT_READ_URI_PERMISSION = 1 */
        (*env)->CallObjectMethod(env, intent, addFlags, (jint)1);
    }

    /* ── Wrap in chooser and present ────────────────────────────────────── */
    jstring   title   = (*env)->NewStringUTF(env, "Share your score");
    jmethodID chooser = (*env)->GetStaticMethodID(env, intentClass, "createChooser",
        "(Landroid/content/Intent;Ljava/lang/CharSequence;)"
        "Landroid/content/Intent;");
    jobject chooserIntent = (*env)->CallStaticObjectMethod(env, intentClass,
                                chooser, intent, title);

    jclass    actClass = (*env)->GetObjectClass(env, activity->clazz);
    jmethodID startAct = (*env)->GetMethodID(env, actClass, "startActivity",
                             "(Landroid/content/Intent;)V");
    (*env)->CallVoidMethod(env, activity->clazz, startAct, chooserIntent);

    /* ── Cleanup ─────────────────────────────────────────────────────────── */
    (*env)->DeleteLocalRef(env, chooserIntent);
    (*env)->DeleteLocalRef(env, intent);
    if (uri) (*env)->DeleteLocalRef(env, uri);
    (*env)->DeleteLocalRef(env, jtext);
    (*env)->DeleteLocalRef(env, jpath);
    (*env)->DeleteLocalRef(env, jauthority);
    (*env)->DeleteLocalRef(env, jpkg);
    (*env)->DeleteLocalRef(env, title);
    (*env)->DeleteLocalRef(env, action);
    (*env)->DeleteLocalRef(env, mime);
    (*env)->DeleteLocalRef(env, keyText);
    (*env)->DeleteLocalRef(env, keyStream);
    (*env)->DeleteLocalRef(env, intentClass);
    (*env)->DeleteLocalRef(env, ctxClass);
    (*env)->DeleteLocalRef(env, actClass);
    /* Do NOT DetachCurrentThread — Sokol reuses this thread across frames. */
}

/* ── Clipboard ────────────────────────────────────────────────────────────────
   sapp_set_clipboard_string() is a no-op on Android (sokol_app.h implements it
   for macOS/Win32/X11/emscripten only), so the clipboard lives here.

   THREADING: this runs on sokol's game thread, not the Java UI thread, and that
   is safe on purpose. ClipboardManager builds its internal Handler from
   ContextImpl.mMainThread.getHandler() -- the MAIN looper, never the calling
   thread's -- so getSystemService() cannot throw the "Can't create handler
   inside thread that has not called Looper.prepare()" that background
   ClipboardManager use is often blamed for, and setPrimaryClip() is a plain
   binder call. That is why this needs no com.sokol.* Java helper the way the
   Ads/Billing plugins do; keeping the Share plugin pure JNI means no
   AndroidJavaSource_* wiring in the consuming project.

   Android 10+ only honours a clipboard write while the app holds input focus,
   which is exactly when a player taps Copy. */
void sokolshare_set_clipboard(const char* text)
{
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return;

    JavaVM* vm  = activity->vm;
    JNIEnv* env = NULL;
    (*vm)->AttachCurrentThread(vm, &env, NULL);
    if (!env) return;

    /* ── ClipboardManager cm = (ClipboardManager)getSystemService(CLIPBOARD_SERVICE) ── */
    jclass   ctxClass = (*env)->FindClass(env, "android/content/Context");
    jfieldID svcField = (*env)->GetStaticFieldID(env, ctxClass,
                            "CLIPBOARD_SERVICE", "Ljava/lang/String;");
    jstring  svcName  = (jstring)(*env)->GetStaticObjectField(env, ctxClass, svcField);
    jmethodID getSvc  = (*env)->GetMethodID(env, ctxClass, "getSystemService",
                            "(Ljava/lang/String;)Ljava/lang/Object;");
    jobject  clipMgr  = (*env)->CallObjectMethod(env, activity->clazz, getSvc, svcName);

    if (clipMgr)
    {
        /* ── ClipData clip = ClipData.newPlainText("text", text) ───────────── */
        jclass    clipDataCls = (*env)->FindClass(env, "android/content/ClipData");
        jmethodID newPlain    = (*env)->GetStaticMethodID(env, clipDataCls, "newPlainText",
            "(Ljava/lang/CharSequence;Ljava/lang/CharSequence;)Landroid/content/ClipData;");
        jstring   label = (*env)->NewStringUTF(env, "text");
        jstring   jtext = (*env)->NewStringUTF(env, text ? text : "");
        jobject   clip  = (*env)->CallStaticObjectMethod(env, clipDataCls, newPlain, label, jtext);

        /* ── cm.setPrimaryClip(clip) ───────────────────────────────────────── */
        jclass    cmClass    = (*env)->GetObjectClass(env, clipMgr);
        jmethodID setPrimary = (*env)->GetMethodID(env, cmClass, "setPrimaryClip",
                                   "(Landroid/content/ClipData;)V");
        (*env)->CallVoidMethod(env, clipMgr, setPrimary, clip);

        /* A denied write (no focus) throws rather than returning a status -- swallow it
           so a background Copy can never take the app down. */
        if ((*env)->ExceptionCheck(env)) (*env)->ExceptionClear(env);

        (*env)->DeleteLocalRef(env, cmClass);
        if (clip) (*env)->DeleteLocalRef(env, clip);
        (*env)->DeleteLocalRef(env, jtext);
        (*env)->DeleteLocalRef(env, label);
        (*env)->DeleteLocalRef(env, clipDataCls);
        (*env)->DeleteLocalRef(env, clipMgr);
    }

    (*env)->DeleteLocalRef(env, svcName);
    (*env)->DeleteLocalRef(env, ctxClass);
    /* Do NOT DetachCurrentThread -- Sokol reuses this thread across frames. */
}

/* ── Any file ─────────────────────────────────────────────────────────────────
   ACTION_SEND with the file's content:// URI (FileProvider, as the image share above) and the caller's MIME type.
   The URI also goes in the intent's ClipData: from Android 10 the chooser reads the grant and its preview from there.
   Every Java exception is cleared and reported as 0 (a path outside file_provider_paths.xml throws
   IllegalArgumentException), so a refused share can never take the app down. A local frame frees every reference. */

static int sokolshare_failed(JNIEnv* env)
{
    if (!(*env)->ExceptionCheck(env)) return 0;
    (*env)->ExceptionClear(env);
    return 1;
}

int sokolshare_file(const char* path, const char* mime, const char* title)
{
    if (!path || !path[0] || access(path, R_OK) != 0) return 0;
    ANativeActivity* activity = (ANativeActivity*)sapp_android_get_native_activity();
    if (!activity) return 0;

    JavaVM* vm  = activity->vm;
    JNIEnv* env = NULL;
    (*vm)->AttachCurrentThread(vm, &env, NULL);
    if (!env) return 0;
    if ((*env)->PushLocalFrame(env, 48) != 0) { sokolshare_failed(env); return 0; }

    int ok = 0;
    jobject ctx = activity->clazz;
    do {
        /* authority = <package>.fileprovider (Providers.xml) */
        jclass    ctxClass = (*env)->FindClass(env, "android/content/Context");
        jmethodID getPkg   = (*env)->GetMethodID(env, ctxClass, "getPackageName", "()Ljava/lang/String;");
        jstring   jpkg     = (jstring)(*env)->CallObjectMethod(env, ctx, getPkg);
        if (sokolshare_failed(env) || !jpkg) break;
        const char* pkgStr = (*env)->GetStringUTFChars(env, jpkg, NULL);
        char authority[256];
        snprintf(authority, sizeof(authority), "%s.fileprovider", pkgStr);
        (*env)->ReleaseStringUTFChars(env, jpkg, pkgStr);

        /* FileProvider through the app's ClassLoader (a native thread's FindClass cannot see androidx). */
        jclass    actCls    = (*env)->GetObjectClass(env, ctx);
        jmethodID getClsLdr = (*env)->GetMethodID(env, actCls, "getClassLoader", "()Ljava/lang/ClassLoader;");
        jobject   loader    = (*env)->CallObjectMethod(env, ctx, getClsLdr);
        jclass    clsLdrCls = (*env)->FindClass(env, "java/lang/ClassLoader");
        jmethodID loadClass = (*env)->GetMethodID(env, clsLdrCls, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
        jclass    fpClass   = (jclass)(*env)->CallObjectMethod(env, loader, loadClass,
                                  (*env)->NewStringUTF(env, "androidx.core.content.FileProvider"));
        if (sokolshare_failed(env) || !fpClass) break;

        jclass    fileClass = (*env)->FindClass(env, "java/io/File");
        jmethodID fileInit  = (*env)->GetMethodID(env, fileClass, "<init>", "(Ljava/lang/String;)V");
        jobject   fileObj   = (*env)->NewObject(env, fileClass, fileInit, (*env)->NewStringUTF(env, path));
        jmethodID getUri    = (*env)->GetStaticMethodID(env, fpClass, "getUriForFile",
            "(Landroid/content/Context;Ljava/lang/String;Ljava/io/File;)Landroid/net/Uri;");
        jobject uri = (*env)->CallStaticObjectMethod(env, fpClass, getUri, ctx,
                          (*env)->NewStringUTF(env, authority), fileObj);
        if (sokolshare_failed(env) || !uri) break;

        jclass    intentClass = (*env)->FindClass(env, "android/content/Intent");
        jmethodID intentInit  = (*env)->GetMethodID(env, intentClass, "<init>", "(Ljava/lang/String;)V");
        jobject   intent      = (*env)->NewObject(env, intentClass, intentInit,
                                    (*env)->NewStringUTF(env, "android.intent.action.SEND"));
        jmethodID setType     = (*env)->GetMethodID(env, intentClass, "setType",
                                    "(Ljava/lang/String;)Landroid/content/Intent;");
        jmethodID putExtra    = (*env)->GetMethodID(env, intentClass, "putExtra",
                                    "(Ljava/lang/String;Landroid/os/Parcelable;)Landroid/content/Intent;");
        jmethodID addFlags    = (*env)->GetMethodID(env, intentClass, "addFlags", "(I)Landroid/content/Intent;");
        jmethodID setClip     = (*env)->GetMethodID(env, intentClass, "setClipData", "(Landroid/content/ClipData;)V");
        (*env)->CallObjectMethod(env, intent, setType,
            (*env)->NewStringUTF(env, mime && mime[0] ? mime : "application/octet-stream"));
        (*env)->CallObjectMethod(env, intent, putExtra,
            (*env)->NewStringUTF(env, "android.intent.extra.STREAM"), uri);
        (*env)->CallObjectMethod(env, intent, addFlags, (jint)1 /* FLAG_GRANT_READ_URI_PERMISSION */);

        jclass    clipCls = (*env)->FindClass(env, "android/content/ClipData");
        jmethodID newRaw  = (*env)->GetStaticMethodID(env, clipCls, "newRawUri",
                                "(Ljava/lang/CharSequence;Landroid/net/Uri;)Landroid/content/ClipData;");
        jobject   clip    = (*env)->CallStaticObjectMethod(env, clipCls, newRaw, (*env)->NewStringUTF(env, ""), uri);
        if (sokolshare_failed(env)) break;
        if (clip) (*env)->CallVoidMethod(env, intent, setClip, clip);
        if (sokolshare_failed(env)) break;

        jmethodID chooser = (*env)->GetStaticMethodID(env, intentClass, "createChooser",
            "(Landroid/content/Intent;Ljava/lang/CharSequence;)Landroid/content/Intent;");
        jobject chooserIntent = (*env)->CallStaticObjectMethod(env, intentClass, chooser, intent,
                                    title && title[0] ? (*env)->NewStringUTF(env, title) : NULL);
        if (sokolshare_failed(env) || !chooserIntent) break;

        jmethodID startAct = (*env)->GetMethodID(env, actCls, "startActivity", "(Landroid/content/Intent;)V");
        (*env)->CallVoidMethod(env, ctx, startAct, chooserIntent);
        if (sokolshare_failed(env)) break;
        ok = 1;
    } while (0);

    sokolshare_failed(env);
    (*env)->PopLocalFrame(env, NULL);
    /* Do NOT DetachCurrentThread -- Sokol reuses this thread across frames. */
    return ok;
}
