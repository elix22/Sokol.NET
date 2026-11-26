/* sokol_filepicker_android.c -- the Storage Access Framework pickers for Sokol.NET (JNI).
   The work is in com.sokol.filepicker.FilePickerBridge (platform/android/java, compiled into the APK); this file only
   calls its static methods. App classes are loaded through the activity's ClassLoader: FindClass on a native thread
   only sees the system's. Worker threads attach to the VM for a call and detach after it. */
#include "sokol_filepicker.h"
#include <android/native_activity.h>
#include <jni.h>
#include <stdlib.h>
#include <string.h>

extern const void* sapp_android_get_native_activity(void);

static jclass g_bridge;            /* global ref to FilePickerBridge */
static char* g_handle;             /* last result / resolve answers */
static char* g_name;
static char* g_token;
static char* g_resolved;
static unsigned char* g_loaded;    /* load() -> take() */
static int64_t g_loaded_len;

typedef struct { JNIEnv* env; bool attached; ANativeActivity* act; } jni_ctx;

static bool ctx_begin(jni_ctx* c)
{
    c->act = (ANativeActivity*)sapp_android_get_native_activity();
    c->env = NULL;
    c->attached = false;
    if (!c->act) return false;
    JavaVM* vm = c->act->vm;
    if ((*vm)->GetEnv(vm, (void**)&c->env, JNI_VERSION_1_6) != JNI_OK) {
        if ((*vm)->AttachCurrentThread(vm, &c->env, NULL) != JNI_OK) return false;
        c->attached = true;
    }
    if (!g_bridge) {
        JNIEnv* env = c->env;
        jclass actCls = (*env)->GetObjectClass(env, c->act->clazz);
        jmethodID getLoader = (*env)->GetMethodID(env, actCls, "getClassLoader", "()Ljava/lang/ClassLoader;");
        jobject loader = (*env)->CallObjectMethod(env, c->act->clazz, getLoader);
        jclass loaderCls = (*env)->FindClass(env, "java/lang/ClassLoader");
        jmethodID loadClass = (*env)->GetMethodID(env, loaderCls, "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
        jstring name = (*env)->NewStringUTF(env, "com.sokol.filepicker.FilePickerBridge");
        jclass cls = (jclass)(*env)->CallObjectMethod(env, loader, loadClass, name);
        if ((*env)->ExceptionCheck(env)) { (*env)->ExceptionClear(env); cls = NULL; }
        if (cls) { g_bridge = (jclass)(*env)->NewGlobalRef(env, cls); (*env)->DeleteLocalRef(env, cls); }
        (*env)->DeleteLocalRef(env, name);
        (*env)->DeleteLocalRef(env, loaderCls);
        (*env)->DeleteLocalRef(env, loader);
        (*env)->DeleteLocalRef(env, actCls);
    }
    if (!g_bridge) { if (c->attached) (*c->act->vm)->DetachCurrentThread(c->act->vm); return false; }
    return true;
}

static void ctx_end(jni_ctx* c)
{
    if (c->env && (*c->env)->ExceptionCheck(c->env)) (*c->env)->ExceptionClear(c->env);
    if (c->attached) (*c->act->vm)->DetachCurrentThread(c->act->vm);
}

static char* dup_jstring(JNIEnv* env, jstring s)
{
    if (!s) return NULL;
    const char* u = (*env)->GetStringUTFChars(env, s, NULL);
    char* r = u ? strdup(u) : NULL;
    if (u) (*env)->ReleaseStringUTFChars(env, s, u);
    return r;
}

static void set_str(char** slot, char* value) { free(*slot); *slot = value; }

/* MIME type for a new document from the first extension ("png" -> image/png); anything else: octet-stream. */
static const char* mime_for(const char* extensions)
{
    if (!extensions || !*extensions) return "application/octet-stream";
    if (!strncmp(extensions, "png", 3)) return "image/png";
    if (!strncmp(extensions, "jpg", 3) || !strncmp(extensions, "jpeg", 4)) return "image/jpeg";
    if (!strncmp(extensions, "json", 4)) return "application/json";
    if (!strncmp(extensions, "zip", 3)) return "application/zip";
    return "application/octet-stream";
}

bool sokolfilepicker_start(int mode, const char* extensions, const char* name, const unsigned char* data, int64_t len)
{
    jni_ctx c;
    if (!ctx_begin(&c)) return false;
    JNIEnv* env = c.env;
    jmethodID m = (*env)->GetStaticMethodID(env, g_bridge, "start",
        "(Landroid/app/Activity;ILjava/lang/String;Ljava/lang/String;[B)Z");
    jstring jname = name ? (*env)->NewStringUTF(env, name) : NULL;
    jstring jmime = (*env)->NewStringUTF(env, mime_for(extensions));
    jbyteArray jdata = NULL;
    if (mode == 1 && data && len >= 0) {
        jdata = (*env)->NewByteArray(env, (jsize)len);
        if (jdata) (*env)->SetByteArrayRegion(env, jdata, 0, (jsize)len, (const jbyte*)data);
    }
    jboolean ok = m ? (*env)->CallStaticBooleanMethod(env, g_bridge, m, c.act->clazz, (jint)mode, jname, jmime, jdata) : JNI_FALSE;
    if (jdata) (*env)->DeleteLocalRef(env, jdata);
    if (jname) (*env)->DeleteLocalRef(env, jname);
    (*env)->DeleteLocalRef(env, jmime);
    ctx_end(&c);
    return ok == JNI_TRUE;
}

int sokolfilepicker_poll(void)
{
    jni_ctx c;
    if (!ctx_begin(&c)) return SOKOLFILEPICKER_IDLE;
    JNIEnv* env = c.env;
    jmethodID poll = (*env)->GetStaticMethodID(env, g_bridge, "poll", "()I");
    int s = poll ? (*env)->CallStaticIntMethod(env, g_bridge, poll) : SOKOLFILEPICKER_IDLE;
    if (s == SOKOLFILEPICKER_PICKED) {
        jmethodID u = (*env)->GetStaticMethodID(env, g_bridge, "resultUri", "()Ljava/lang/String;");
        jmethodID n = (*env)->GetStaticMethodID(env, g_bridge, "resultName", "()Ljava/lang/String;");
        jstring ju = (jstring)(*env)->CallStaticObjectMethod(env, g_bridge, u);
        jstring jn = (jstring)(*env)->CallStaticObjectMethod(env, g_bridge, n);
        set_str(&g_handle, dup_jstring(env, ju));
        set_str(&g_name, dup_jstring(env, jn));
        if (ju) (*env)->DeleteLocalRef(env, ju);
        if (jn) (*env)->DeleteLocalRef(env, jn);
        if (!g_handle) s = SOKOLFILEPICKER_FAILED;
    }
    ctx_end(&c);
    return s;
}

const char* sokolfilepicker_result_handle(void) { return g_handle; }
const char* sokolfilepicker_result_name(void) { return g_name; }

int64_t sokolfilepicker_load(const char* handle)
{
    free(g_loaded); g_loaded = NULL; g_loaded_len = 0;
    if (!handle) return -1;
    jni_ctx c;
    if (!ctx_begin(&c)) return -1;
    JNIEnv* env = c.env;
    jmethodID m = (*env)->GetStaticMethodID(env, g_bridge, "read", "(Landroid/content/Context;Ljava/lang/String;)[B");
    jstring ju = (*env)->NewStringUTF(env, handle);
    jbyteArray arr = m ? (jbyteArray)(*env)->CallStaticObjectMethod(env, g_bridge, m, c.act->clazz, ju) : NULL;
    int64_t n = -1;
    if (arr) {
        n = (*env)->GetArrayLength(env, arr);
        g_loaded = (unsigned char*)malloc(n > 0 ? (size_t)n : 1);
        if (g_loaded) { (*env)->GetByteArrayRegion(env, arr, 0, (jsize)n, (jbyte*)g_loaded); g_loaded_len = n; }
        else n = -1;
        (*env)->DeleteLocalRef(env, arr);
    }
    (*env)->DeleteLocalRef(env, ju);
    ctx_end(&c);
    return n;
}

void sokolfilepicker_take(unsigned char* dst)
{
    if (g_loaded && dst && g_loaded_len > 0) memcpy(dst, g_loaded, (size_t)g_loaded_len);
    free(g_loaded); g_loaded = NULL; g_loaded_len = 0;
}

bool sokolfilepicker_write(const char* handle, const unsigned char* data, int64_t len)
{
    if (!handle || len < 0) return false;
    jni_ctx c;
    if (!ctx_begin(&c)) return false;
    JNIEnv* env = c.env;
    jmethodID m = (*env)->GetStaticMethodID(env, g_bridge, "write", "(Landroid/content/Context;Ljava/lang/String;[B)Z");
    jstring ju = (*env)->NewStringUTF(env, handle);
    jbyteArray arr = (*env)->NewByteArray(env, (jsize)len);
    jboolean ok = JNI_FALSE;
    if (arr && m) {
        if (len > 0) (*env)->SetByteArrayRegion(env, arr, 0, (jsize)len, (const jbyte*)data);
        ok = (*env)->CallStaticBooleanMethod(env, g_bridge, m, c.act->clazz, ju, arr);
    }
    if (arr) (*env)->DeleteLocalRef(env, arr);
    (*env)->DeleteLocalRef(env, ju);
    ctx_end(&c);
    return ok == JNI_TRUE;
}

/* stat() answers "size|modified" (or null): `field` 0 = size, 1 = modified. */
static int64_t stat_field(const char* handle, int field)
{
    if (!handle) return -1;
    jni_ctx c;
    if (!ctx_begin(&c)) return -1;
    JNIEnv* env = c.env;
    jmethodID m = (*env)->GetStaticMethodID(env, g_bridge, "stat", "(Landroid/content/Context;Ljava/lang/String;)Ljava/lang/String;");
    jstring ju = (*env)->NewStringUTF(env, handle);
    jstring js = m ? (jstring)(*env)->CallStaticObjectMethod(env, g_bridge, m, c.act->clazz, ju) : NULL;
    int64_t v = -1;
    char* s = dup_jstring(env, js);
    if (s) {
        char* bar = strchr(s, '|');
        if (bar) v = field == 0 ? strtoll(s, NULL, 10) : strtoll(bar + 1, NULL, 10);
        free(s);
    }
    if (js) (*env)->DeleteLocalRef(env, js);
    (*env)->DeleteLocalRef(env, ju);
    ctx_end(&c);
    return v;
}

int64_t sokolfilepicker_size(const char* handle) { return stat_field(handle, 0); }
int64_t sokolfilepicker_modified(const char* handle) { return stat_field(handle, 1); }

/* Android: the URI itself is the token (its permission was made persistent when it was picked). */
const char* sokolfilepicker_bookmark(const char* handle)
{
    set_str(&g_token, handle ? strdup(handle) : NULL);
    return g_token;
}

const char* sokolfilepicker_resolve(const char* token)
{
    if (!token) return NULL;
    jni_ctx c;
    if (!ctx_begin(&c)) return NULL;
    JNIEnv* env = c.env;
    jmethodID m = (*env)->GetStaticMethodID(env, g_bridge, "hasAccess", "(Landroid/content/Context;Ljava/lang/String;)Z");
    jstring ju = (*env)->NewStringUTF(env, token);
    jboolean ok = m ? (*env)->CallStaticBooleanMethod(env, g_bridge, m, c.act->clazz, ju) : JNI_FALSE;
    (*env)->DeleteLocalRef(env, ju);
    ctx_end(&c);
    set_str(&g_resolved, ok == JNI_TRUE ? strdup(token) : NULL);
    return g_resolved;
}
